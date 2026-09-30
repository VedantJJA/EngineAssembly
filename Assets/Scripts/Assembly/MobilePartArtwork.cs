using System;
using System.Collections.Generic;
using UnityEngine;
namespace EngineAssembly
{
    public sealed class MobilePartArtwork : ScriptableObject
    {
        [Serializable] public class Entry { public string id; public Texture2D image; }
        public List<Entry> parts=new List<Entry>();
        public Texture2D Find(string id)=>parts.Find(p=>p.id==id)?.image;
    }
}
