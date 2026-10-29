using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace EngineAssembly
{
    public static class DisplayWorkpieceOrientation
    {
        public static Quaternion For(AssemblyRecipe recipe,IReadOnlyList<PartDefinition> steps,int index,string workspace)
        {
            for(int i=Mathf.Min(index,steps.Count-1);i>=0;i--)
            {
                var p=steps[i];if((p.subAssemblyId??"")!=(workspace??""))continue;
                if(p.orientation==AssemblyOrientation.UpsideDown)return Quaternion.Euler(180,0,0);
                if(p.orientation==AssemblyOrientation.Upright)return Quaternion.identity;
            }
            var group=recipe.subAssemblies.FirstOrDefault(g=>g.id==workspace);
            return group!=null?AssemblyManager.InitialFixtureRotation(group):Quaternion.identity;
        }
    }
}
