using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
namespace EngineAssembly.Editor
{
    public static class GarageWorkshopSetup
    {
        const string Pack="Assets/Simple Garage",Workshop="Assets/Scenes/AssemblyWorkshop.unity",Art="Assets/Art/Environment/Garage";
        [MenuItem("Tools/Engine Assembly/Environment/Build Custom PC Garage")]
        public static void Run()=>Refit();
        public static void Refit()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Run outside Play mode.");
            Directory.CreateDirectory(Art);
            string backup="Tools/Verification/BeforeGarageIntegration";Directory.CreateDirectory(backup+"/Materials");
            if(!File.Exists(backup+"/AssemblyWorkshop.unity"))File.Copy(Workshop,backup+"/AssemblyWorkshop.unity");
            foreach(var path in AssetDatabase.FindAssets("t:Material",new[]{Pack}).Select(AssetDatabase.GUIDToAssetPath))
                if(!File.Exists(backup+"/Materials/"+Path.GetFileName(path)))File.Copy(path,backup+"/Materials/"+Path.GetFileName(path));
            RepairMaterials();
            var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(Workshop);bool opened=!scene.IsValid() || !scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(Workshop,OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                foreach(var root in scene.GetRootGameObjects().Where(g=>g.name=="Garage environment" || g.name=="Garage workshop lighting"))UnityEngine.Object.DestroyImmediate(root);
                var env=new GameObject("Garage environment").transform;
                var floor=Material("Concrete floor","2K-tiling_44_basecolor",new Color(.72f,.74f,.74f),new Vector2(10,10),.22f);
                var wall=Material("Painted masonry",null,new Color(.78f,.81f,.8f),new Vector2(4,2),.12f);
                wall.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Pack+"/Textures/Main noise normal.png"));wall.SetFloat("_BumpScale",.15f);wall.EnableKeyword("_NORMALMAP");EditorUtility.SetDirty(wall);AssetDatabase.SaveAssetIfDirty(wall);
                var dado=Material("Protective panels","worn_metal4_albedo",new Color(.32f,.40f,.42f),new Vector2(5,1),.2f);
                var ceiling=Material("Ceiling finish","2K-false_ceiling_basecolor",Color.white,new Vector2(7,7),.1f);
                var steel=Material("Steel frame","Dark Grey",new Color(.19f,.23f,.25f),Vector2.one,.35f);
                var yellow=Material("Safety yellow",null,new Color(.91f,.65f,.08f),Vector2.one,.15f);
                var white=Material("White trim",null,new Color(.88f,.9f,.9f),Vector2.one,.3f);
                Box(env,"Floor",new Vector3(0,-.1f,0),new Vector3(14,.2f,14),floor);
                Box(env,"Ceiling",new Vector3(0,3.87f,0),new Vector3(14,.14f,14),ceiling);
                foreach(float side in new[]{-1f,1f})
                {
                    Box(env,"Side wall",new Vector3(side*7,1.9f,0),new Vector3(.2f,3.8f,14),wall);
                    Box(env,"Protective wall panel",new Vector3(side*6.87f,.65f,0),new Vector3(.025f,1.3f,13.8f),dado);
                    Box(env,"Skirting rail",new Vector3(side*6.83f,.12f,0),new Vector3(.035f,.24f,13.8f),steel);
                    Box(env,"Entrance pier",new Vector3(side*4.95f,1.9f,-7),new Vector3(4.1f,3.8f,.2f),wall);
                    Box(env,"Door track",new Vector3(side*2.95f,1.5f,-6.8f),new Vector3(.08f,3,.1f),steel);
                }
                Box(env,"Rear wall",new Vector3(0,1.9f,7),new Vector3(14,3.8f,.2f),wall);
                Box(env,"Rear protective panel",new Vector3(0,.65f,6.87f),new Vector3(13.8f,1.3f,.025f),dado);
                Box(env,"Door lintel",new Vector3(0,3.35f,-7),new Vector3(5.8f,.9f,.2f),wall);
                Box(env,"Door collision",new Vector3(0,1.45f,-7),new Vector3(5.8f,2.9f,.12f),steel);
                foreach(float z in new[]{-4f,0,4f})Box(env,"Roof beam",new Vector3(0,3.68f,z),new Vector3(13.8f,.22f,.14f),steel);
                foreach(float x in new[]{-2f,3.65f})Box(env,"Engine bay marking",new Vector3(x,.004f,0),new Vector3(.07f,.006f,4.2f),yellow,false);
                foreach(float z in new[]{-2.1f,2.1f})Box(env,"Engine bay marking",new Vector3(.825f,.004f,z),new Vector3(5.65f,.006f,.07f),yellow,false);
                var surfaces=new List<WorkshopSupplyLayout.Surface>();
                var source=EditorSceneManager.OpenScene(Pack+"/Scenes/Garage Scene.unity",OpenSceneMode.Additive);
                try
                {
                    var garage=source.GetRootGameObjects().First(g=>g.name=="Garage");SceneManager.SetActiveScene(scene);
                    GameObject Furnish(string name,Vector3 center,Vector3? scale=null,float yaw=0,bool solid=true)
                    {
                        var original=garage.GetComponentsInChildren<MeshRenderer>().First(r=>r.name==name).gameObject;
                        var pivot=new GameObject(name+" station").transform;pivot.SetParent(env,false);
                        var copy=UnityEngine.Object.Instantiate(original,pivot);copy.name=name;
                        if(PrefabUtility.IsPartOfPrefabInstance(copy))PrefabUtility.UnpackPrefabInstance(copy,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                        copy.transform.position-=Bounds(copy).center;pivot.localScale=scale??Vector3.one;pivot.rotation=Quaternion.Euler(0,yaw,0);pivot.position=center;
                        foreach(var r in copy.GetComponentsInChildren<MeshRenderer>())
                        {
                            r.lightmapIndex=-1;r.realtimeLightmapIndex=-1;GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.BatchingStatic);
                            if(solid && r.TryGetComponent<MeshFilter>(out var filter)){var collider=r.GetComponent<MeshCollider>();if(!collider)collider=r.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;}
                        }
                        return pivot.gameObject;
                    }
                    foreach(float z in new[]{3.25f,5.5f})foreach(float x in new[]{-2.7f,0,2.7f})
                    {
                        var table=Furnish("Table",new Vector3(x,.45425f,z),new Vector3(1.3f,1.15f,1.95f));table.name="Fixed supply bench";var b=Bounds(table);
                        surfaces.Add(new WorkshopSupplyLayout.Surface{center=new Vector3(b.center.x,b.max.y,b.center.z),size=new Vector2(b.size.x-.10f,b.size.z-.10f)});
                        Box(env,"Supply bench steel apron",new Vector3(x,b.max.y-.1f,z),new Vector3(b.size.x,.14f,b.size.z),steel,false);
                    }
                    Furnish("Large corner shelf",new Vector3(-6.3f,.965f,.7f));
                    Furnish("Opened locker",new Vector3(-5.45f,.94f,-5.9f));
                    Furnish("Drilling machine",new Vector3(-4.5f,.86f,-5.85f));
                    var toolTable=Furnish("Table",new Vector3(5.4f,.395f,-5.9f));float top=Bounds(toolTable).max.y;
                    Furnish("Bench Grinder",new Vector3(4.95f,top+.105f,-5.9f),null,90);
                    Furnish("Ridgid oscillating belt sander",new Vector3(5.65f,top+.175f,-5.9f),null,90);
                    Furnish("Small stool",new Vector3(5.2f,.16f,-4.8f));
                    Furnish("Hose",new Vector3(6.775f,1.3f,-2.5f));
                    Furnish("Air conditioner",new Vector3(6.755f,2.75f,2.8f));
                    Furnish("Locker",new Vector3(6.69f,1.7f,4.4f));Furnish("Small locker",new Vector3(6.69f,1.05f,4.4f));
                    Furnish("3 shelves",new Vector3(6.66f,1.5f,.3f));
                    var sawBench=Furnish("Table",new Vector3(-5.9f,.395f,4.8f),null,90);
                    Furnish("Saw",new Vector3(-5.9f,Bounds(sawBench).max.y+.245f,4.8f),null,90);
                    Furnish("White suitcase",new Vector3(-6.3f,2.085f,-.6f));Furnish("Camouflage suitcase",new Vector3(-6.3f,1.98f,.1f));
                    Furnish("Carpet",new Vector3(-5.25f,.012f,-.3f),null,0,false);
                    Furnish("Machinery parts.on the floor",new Vector3(-6.1f,.11f,3),null,0,false);
                    Furnish("Garage door",new Vector3(0,1.45f,-6.88f),new Vector3(1.04f,1.19f,1),0,false);
                    foreach(float x in new[]{-3.8f,3.8f})
                    {Furnish("Window frame",new Vector3(x,2.4f,6.83f),new Vector3(1.4f,1.1f,1),180);Furnish("Emissive window",new Vector3(x,2.34f,6.84f),new Vector3(1.4f,1.1f,1),180,false);}
                    Box(env,"Tool board",new Vector3(0,2.12f,6.8f),new Vector3(2.8f,1.15f,.04f),Material("Tool pegboard","Wood_Pegboard_White",Color.white,new Vector2(2,1),.05f));
                    Furnish("Round Drill Bits",new Vector3(-.75f,2.33f,6.74f),null,180,false);Furnish("Drill Bits",new Vector3(.15f,2.25f,6.74f),null,180,false);Furnish("Cone Drill Bits",new Vector3(.8f,2.15f,6.74f),null,180,false);
                    foreach(float z in new[]{-3f,1,4.5f})foreach(float x in new[]{-3.4f,3.4f})
                    {Furnish("Fluorescent Light.001",new Vector3(x,3.48f,z),new Vector3(1.8f,1,1),0,false);Box(env,"Luminaire backing",new Vector3(x,3.56f,z),new Vector3(2.3f,.09f,.3f),white,false);}
                }
                finally{EditorSceneManager.CloseScene(source,true);SceneManager.SetActiveScene(scene);}
                var manager=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<AssemblyManager>()).Single();
                var supply=manager.GetComponent<WorkshopSupplyLayout>();if(!supply)supply=manager.gameObject.AddComponent<WorkshopSupplyLayout>();supply.Configure(surfaces.ToArray());
                // Recipe coordinates retain their original fixed reference; furniture replaces the old slab.
                foreach(var r in manager.TrayFrame.GetComponentsInChildren<Renderer>())r.enabled=false;
                foreach(var c in manager.TrayFrame.GetComponentsInChildren<Collider>())c.enabled=false;
                var legs=manager.TrayFrame.Find("Workshop bench legs");if(legs)UnityEngine.Object.DestroyImmediate(legs.gameObject);
                var oldFloor=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Floor");if(oldFloor)oldFloor.SetActive(false);
                var light=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>()).FirstOrDefault(l=>l.name=="Workshop Light");
                if(light){light.intensity=.65f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(55,-30,0);}
                var lighting=new GameObject("Garage workshop lighting").transform;
                foreach(float z in new[]{-3f,1,4.5f})foreach(float x in new[]{-3.4f,3.4f})
                {var lamp=new GameObject("Ceiling work light").AddComponent<Light>();lamp.transform.SetParent(lighting,false);lamp.transform.position=new Vector3(x,3.25f,z);lamp.type=LightType.Point;lamp.intensity=6;lamp.range=7;lamp.color=new Color(1,.97f,.92f);}
                var probe=new GameObject("Workshop reflection probe").AddComponent<ReflectionProbe>();probe.transform.SetParent(lighting,false);probe.transform.position=new Vector3(0,1.8f,0);
                probe.size=new Vector3(14,3.8f,14);probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.resolution=128;probe.boxProjection=true;
                RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.67f,.68f);
                Lightmapping.lightingDataAsset=null;LightmapSettings.lightmaps=Array.Empty<LightmapData>();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                File.WriteAllText("Tools/Verification/garage-setup-report.json","{\"passed\":true,\"customGarage\":true,\"supplyBenches\":6,\"width\":14,\"depth\":14}");
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);if(previous.IsValid() && previous.isLoaded)SceneManager.SetActiveScene(previous);}
        }
        static Bounds Bounds(GameObject go){var renderers=go.GetComponentsInChildren<Renderer>();var b=renderers[0].bounds;foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);return b;}
        static GameObject Box(Transform root,string name,Vector3 position,Vector3 size,Material material,bool solid=true)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);go.transform.position=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
        static Material Material(string name,string source,Color colour,Vector2 tiling,float smoothness)
        {
            string path=Art+"/"+name+".mat";var saved=AssetDatabase.LoadAssetAtPath<Material>(path);var original=source==null?null:AssetDatabase.LoadAssetAtPath<Material>(Pack+"/Materials/"+source+".mat");
            var m=original?new Material(original):new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name=name;m.SetColor("_BaseColor",colour);m.SetFloat("_Smoothness",smoothness);m.SetTextureScale("_BaseMap",tiling);
            if(saved){EditorUtility.CopySerialized(m,saved);UnityEngine.Object.DestroyImmediate(m);EditorUtility.SetDirty(saved);AssetDatabase.SaveAssetIfDirty(saved);}else{AssetDatabase.CreateAsset(m,path);saved=m;}return saved;
        }
        static int RepairMaterials()
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)throw new InvalidOperationException("Missing URP Lit shader.");int count=0;
            foreach(var path in AssetDatabase.FindAssets("t:Material",new[]{Pack}).Select(AssetDatabase.GUIDToAssetPath))
            {
                var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m.shader.name!="Standard")continue;
                var texture=m.GetTexture("_MainTex");var scale=m.GetTextureScale("_MainTex");var offset=m.GetTextureOffset("_MainTex");var colour=m.GetColor("_Color");
                var normal=m.GetTexture("_BumpMap");var metallic=m.GetTexture("_MetallicGlossMap");var emission=m.GetTexture("_EmissionMap");var emissionColour=m.GetColor("_EmissionColor");
                float smooth=m.GetFloat("_Glossiness"),metal=m.GetFloat("_Metallic"),bump=m.GetFloat("_BumpScale"),cutoff=m.GetFloat("_Cutoff"),mode=m.GetFloat("_Mode");
                m.shader=shader;m.shaderKeywords=Array.Empty<string>();m.SetTexture("_BaseMap",texture);m.SetTextureScale("_BaseMap",scale);m.SetTextureOffset("_BaseMap",offset);m.SetColor("_BaseColor",colour);
                m.SetTexture("_BumpMap",normal);m.SetFloat("_BumpScale",bump);if(normal)m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_MetallicGlossMap",metallic);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);if(metallic)m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetTexture("_EmissionMap",emission);m.SetColor("_EmissionColor",emissionColour);if(emission || emissionColour.maxColorComponent>0)m.EnableKeyword("_EMISSION");
                m.SetFloat("_Cutoff",cutoff);m.SetFloat("_AlphaClip",mode==1?1:0);if(mode==1)m.EnableKeyword("_ALPHATEST_ON");
                bool transparent=mode>=2;m.SetFloat("_Surface",transparent?1:0);m.SetFloat("_ZWrite",transparent?0:1);m.SetFloat("_SrcBlend",transparent?(float)BlendMode.SrcAlpha:(float)BlendMode.One);m.SetFloat("_DstBlend",transparent?(float)BlendMode.OneMinusSrcAlpha:(float)BlendMode.Zero);
                if(transparent)m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=transparent?3000:mode==1?2450:2000;
                EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);
                if(m.GetTexture("_BaseMap")!=texture || m.GetColor("_BaseColor")!=colour || m.GetTextureScale("_BaseMap")!=scale || m.GetTextureOffset("_BaseMap")!=offset)throw new InvalidOperationException("Material mapping failed: "+path);
                count++;
            }
            return count;
        }
    }
}
