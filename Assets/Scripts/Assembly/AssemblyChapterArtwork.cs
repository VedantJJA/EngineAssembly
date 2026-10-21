using System;
using System.Collections.Generic;
using UnityEngine;
namespace EngineAssembly
{
    [CreateAssetMenu(menuName="Engine Assembly/Chapter artwork")]
    public sealed class AssemblyChapterArtwork : ScriptableObject
    {
        [Serializable] public class Card { public string recipeTitle,chapterId; public Texture2D image,replaces; }
        public List<Card> cards=new List<Card>();
        public Texture2D Find(string title,string id)=>cards.Find(c=>c.recipeTitle==title && c.chapterId==id)?.image;
        public Texture2D PresentationImage(string title,string id)
        {
            var original=Find(title,id);var light=Resources.Load<AssemblyChapterArtwork>("LightChapterArtwork");
            var entry=light?light.cards.Find(c=>c.recipeTitle==title && c.chapterId==id):null;
            // A manually assigned chapter photo takes precedence over the
            // alternative artwork supplied for the project's default photos.
            return entry!=null && (!original || original==entry.replaces)?entry.image:original;
        }
    }
}
