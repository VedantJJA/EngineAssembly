using System;
using UnityEngine;
namespace EngineAssembly
{
    // Normalized collision meshes retain their original CAD frame through these transforms.
    public sealed class ColliderHullSet : ScriptableObject
    {
        [Serializable] public class Hull { public Mesh mesh;public Vector3 center;public float scale=1; }
        public Hull[] hulls;
    }
}
