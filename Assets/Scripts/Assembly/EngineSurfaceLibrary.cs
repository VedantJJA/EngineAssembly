using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EngineAssembly
{
    public sealed class EngineSurfaceLibrary : ScriptableObject
    {
        [Serializable] public class Surface { public string name;public Material material; }
        public List<Surface> surfaces=new List<Surface>();
        static EngineSurfaceLibrary library;
        public Material Find(string name)=>surfaces.FirstOrDefault(s=>s.name==name)?.material;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){library=null;SceneManager.sceneLoaded-=ApplyScene;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()=>SceneManager.sceneLoaded+=ApplyScene;
        static void ApplyScene(Scene scene,LoadSceneMode mode)
        {
            foreach(var root in scene.GetRootGameObjects())Apply(root.transform);
        }
        public static void Apply(Transform root)
        {
            if(!library)library=Resources.Load<EngineSurfaceLibrary>("EngineSurfaceLibrary");
            if(!library || !root)return;
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var id=renderer.name;var mesh=renderer.GetComponent<MeshFilter>();if(!Numbered(id) && mesh && mesh.sharedMesh)id=mesh.sharedMesh.name;if(!Numbered(id)){var part=renderer.GetComponentInParent<AssemblyPart>();if(part)id=part.PartId;}if(!Numbered(id))continue;
                var material=library.Find(Category(id));if(material && renderer.sharedMaterials.Any(m=>m!=material))renderer.sharedMaterials=Enumerable.Repeat(material,Mathf.Max(1,renderer.sharedMaterials.Length)).ToArray();
            }
        }
        static bool Numbered(string id)=>!string.IsNullOrEmpty(id) && id.Length>4 && char.IsDigit(id[0]) && char.IsDigit(id[1]) && char.IsDigit(id[2]) && id[3]=='_';
        public static string Category(string id)
        {
            bool Has(string name)=>id.IndexOf(name,StringComparison.OrdinalIgnoreCase)>=0;
            if(Has("Belt") || Has("Gasket") || Has("Stem_Seal") || Has("O_Ring") || Has("O-Ring") || Has("Hose"))return "Rubber";
            if(Has("Bolt") || Has("Screw") || Has("Nut") || Has("Circlip") || Has("Keeper") || Has("Clamp"))return "Fastener steel";
            if(Has("Bearing_Shell") || Has("Bushing"))return "Bearing alloy";
            if(Has("Cylinder_Block") || Has("Exhaust_Manifold"))return "Cast iron";
            if(Has("Motor"))return "Painted motor housing";
            if(Has("Piston_Cyl") || Has("Piston_Head"))return "Machined aluminium";
            if(Has("Shaft") || Has("Rod") || Has("Valve") || Has("Spring") || Has("Bearing") || Has("Gear") || Has("Washer") || Has("Ring") || Has("Pulley"))return "Machined steel";
            return "Cast aluminium";
        }
    }
}
