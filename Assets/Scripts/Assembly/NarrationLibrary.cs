using System;
using UnityEngine;
namespace EngineAssembly
{
    public sealed class NarrationLibrary : ScriptableObject
    {
        [Serializable] public struct Cue { public string id;public AudioClip clip; }
        public Cue[] cues=Array.Empty<Cue>();
        public AudioClip Find(string id){foreach(var cue in cues)if(cue.id==id)return cue.clip;return null;}
    }
}
