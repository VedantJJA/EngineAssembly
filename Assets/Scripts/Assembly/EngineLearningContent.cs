using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EngineAssembly
{
    [Serializable] public class AssemblyInstruction
    {
        public string id, chapterId, caption, narration, voiceId, purpose;
        public List<string> partIds=new List<string>();
    }
    [Serializable] public class EnginePartDetail { public string partId,title,description,voiceId; }
    [Serializable] public class EngineLearningData
    {
        public int version;
        public string recipeTitle;
        public List<AssemblyInstruction> steps=new List<AssemblyInstruction>();
        public List<EnginePartDetail> details=new List<EnginePartDetail>();
    }
    public static class EngineLearningContent
    {
        static EngineLearningData cached;
        public static EngineLearningData Data
        {
            get
            {
                if(cached==null)
                {
                    var asset=Resources.Load<TextAsset>("EngineLearning");
                    cached=asset?JsonUtility.FromJson<EngineLearningData>(asset.text):new EngineLearningData();
                }
                return cached;
            }
        }
        public static AssemblyInstruction ForPart(PartDefinition part)=>part==null?new AssemblyInstruction{id=""}:Data.steps.FirstOrDefault(s=>s.chapterId==part.chapterId && s.partIds.Contains(part.id))
            ?? new AssemblyInstruction{id=part.id,chapterId=part.chapterId,caption="Fit "+part.displayName+" at its matching mounting position.",narration="Fit "+part.displayName+" at its matching mounting position.",partIds=new List<string>{part.id}};
        public static EnginePartDetail Detail(PartDefinition part)=>Data.details.FirstOrDefault(d=>d.partId==part.id)
            ?? new EnginePartDetail{partId=part.id,title=part.displayName,description="An engine component in this assembly recipe."};
        // Stable dependency order, including virtual carrier steps. No physics or interaction hints needed.
        public static List<PartDefinition> Timeline(AssemblyRecipe recipe)
        {
            var pending=recipe.parts.OrderBy(p=>recipe.chapters.FindIndex(c=>c.id==p.chapterId)).ThenBy(p=>p.order).ToList();
            var placed=new HashSet<string>();var result=new List<PartDefinition>();
            while(pending.Count>0)
            {
                var next=pending.FirstOrDefault(p=>AssemblyRecipe.Dependencies(p).All(placed.Contains));
                if(next==null)throw new InvalidOperationException("Assembly timeline contains unresolved dependencies.");
                pending.Remove(next);placed.Add(next.id);if(!next.isBase)result.Add(next);
            }
            return result;
        }
    }
}
