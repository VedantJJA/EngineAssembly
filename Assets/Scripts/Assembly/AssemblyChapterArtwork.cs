using System;
using System.Collections.Generic;
using UnityEngine;
namespace EngineAssembly
{
    [CreateAssetMenu(menuName="Engine Assembly/Chapter artwork")]
    public sealed class AssemblyChapterArtwork : ScriptableObject
    {
        [Serializable] public class Card { public string recipeTitle,chapterId; public Texture2D image; }
        public List<Card> cards=new List<Card>();
        public Texture2D Find(string title,string id)=>cards.Find(c=>c.recipeTitle==title && c.chapterId==id)?.image;
    }
}
