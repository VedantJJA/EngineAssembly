using System.Collections.Generic;
using UnityEngine;
namespace EngineAssembly
{
    // Imported CAD vertices can be microscopic before the object's import scale is applied.
    // Cook exact surfaces at a stable size without welding away small cavities.
    public static class AssemblyQueryGeometry
    {
        public const MeshColliderCookingOptions Cooking=MeshColliderCookingOptions.CookForFasterSimulation|MeshColliderCookingOptions.EnableMeshCleaning|MeshColliderCookingOptions.UseFastMidphase;
        static readonly Dictionary<Mesh,Mesh> meshes=new Dictionary<Mesh,Mesh>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){foreach(var mesh in meshes.Values)if(mesh)Object.Destroy(mesh);meshes.Clear();}
        public static Mesh Normalize(Mesh source,bool refresh=false)
        {
            if(meshes.TryGetValue(source,out var saved) && saved){if(!refresh)return saved;if(Application.isPlaying)Object.Destroy(saved);else Object.DestroyImmediate(saved);}
            var bounds=source.bounds;float scale=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            var vertices=source.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]=(vertices[i]-bounds.center)/Mathf.Max(scale,1e-12f);
            var mesh=new Mesh{name=source.name+"_Exact_Normalized",indexFormat=source.indexFormat,vertices=vertices,triangles=source.triangles,hideFlags=HideFlags.DontSave};mesh.RecalculateBounds();meshes[source]=mesh;return mesh;
        }
        public static void SetFrame(Transform frame,Mesh source)
        {
            var bounds=source.bounds;frame.localPosition=bounds.center;frame.localRotation=Quaternion.identity;frame.localScale=Vector3.one*Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
        }
        public static MeshCollider Add(Transform parent,Mesh source)
        {
            var go=new GameObject("Normalized CAD query");go.transform.SetParent(parent,false);SetFrame(go.transform,source);
            var collider=go.AddComponent<MeshCollider>();collider.cookingOptions=Cooking;collider.sharedMesh=Normalize(source);return collider;
        }
    }
}
