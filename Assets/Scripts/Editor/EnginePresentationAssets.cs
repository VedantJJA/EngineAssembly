using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EngineAssembly.Editor
{
    // Creates a separate palette and alternative artwork. Existing prefabs,
    // scenes, meshes and the original chapter catalogue are never saved here.
    public static class EnginePresentationAssets
    {
        const string Folder="Assets/Art/Materials/Engine";
        [Serializable] sealed class Report{public bool passed;public int materials,pictures;public string error;}
        [MenuItem("Tools/Engine Assembly/Presentation/Create Light Artwork and Surface Palette")]
        public static void Schedule()=>EditorApplication.delayCall+=Run;
        public static void RunBatch()
        {
            if(!Application.isBatchMode || !Application.dataPath.Replace('\\','/').EndsWith("/Tools/Verification/UnityProject/Assets"))throw new InvalidOperationException("Use the isolated verification project.");
            EditorSceneManager.OpenScene(ExperienceMenuBuilder.ScenePath,OpenSceneMode.Single);
            Run();var report=JsonUtility.FromJson<Report>(File.ReadAllText("Tools/Verification/presentation-assets-report.json"));EditorApplication.Exit(report.passed?0:1);
        }
        static void Run()
        {
            var report=new Report();
            try{if(EditorApplication.isPlaying)throw new InvalidOperationException("Create presentation assets outside Play mode.");CreatePalette();report.materials=8;report.pictures=CreatePictures();report.passed=true;}
            catch(Exception error){report.error=error.ToString();Debug.LogException(error);}
            Directory.CreateDirectory("Tools/Verification");File.WriteAllText("Tools/Verification/presentation-assets-report.json",JsonUtility.ToJson(report,true));
        }
        static Texture2D Map(string asset,string suffix)
        {
            string path="Assets/Art/Textures/ambientCG/"+asset+"/"+asset+"_1K-JPG_"+suffix+".jpg";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);if(!importer)throw new FileNotFoundException("Missing downloaded texture",path);
            importer.sRGBTexture=suffix=="Color";importer.wrapMode=TextureWrapMode.Repeat;importer.maxTextureSize=1024;importer.mipmapEnabled=true;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Material CreateMaterial(string name,string texture,Color colour,float metal,float smooth,float variation,float detail)
        {
            string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader=Shader.Find("EngineAssembly/Surface Triplanar");if(!shader)throw new InvalidOperationException("Missing engine surface shader.");
            if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}else material.shader=shader;
            material.SetColor("_BaseColor",colour);material.SetTexture("_BaseMap",Map(texture,"Color"));material.SetTexture("_RoughnessMap",Map(texture,"Roughness"));material.SetTexture("_DetailNormal",Map(texture,"NormalGL"));
            material.SetFloat("_Metallic",metal);material.SetFloat("_Smoothness",smooth);material.SetFloat("_TextureStrength",variation);material.SetFloat("_NormalStrength",detail);material.SetFloat("_TextureScale",3200);material.enableInstancing=true;
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);return material;
        }
        static void CreatePalette()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            const string path="Assets/Resources/EngineSurfaceLibrary.asset";
            var library=AssetDatabase.LoadAssetAtPath<EngineSurfaceLibrary>(path);if(!library){library=ScriptableObject.CreateInstance<EngineSurfaceLibrary>();AssetDatabase.CreateAsset(library,path);}
            library.surfaces.Clear();
            void Add(string name,string texture,Color colour,float metal,float smooth,float variation,float detail)=>library.surfaces.Add(new EngineSurfaceLibrary.Surface{name=name,material=CreateMaterial(name,texture,colour,metal,smooth,variation,detail)});
            Add("Cast aluminium","Metal006",new Color(.64f,.68f,.71f),.65f,.38f,.12f,.10f);
            Add("Machined aluminium","Metal012",new Color(.78f,.80f,.82f),.75f,.62f,.09f,.07f);
            Add("Cast iron","Metal006",new Color(.26f,.29f,.32f),.5f,.30f,.12f,.10f);
            Add("Machined steel","Metal012",new Color(.49f,.54f,.59f),.78f,.64f,.09f,.07f);
            Add("Fastener steel","Metal012",new Color(.27f,.31f,.35f),.7f,.46f,.08f,.06f);
            Add("Bearing alloy","Metal012",new Color(.65f,.59f,.48f),.68f,.53f,.06f,.06f);
            Add("Rubber","Rubber004",new Color(.07f,.08f,.09f),0,.24f,.18f,.12f);
            Add("Painted motor housing","Metal006",new Color(.13f,.22f,.24f),.2f,.42f,.07f,.06f);
            EditorUtility.SetDirty(library);AssetDatabase.SaveAssetIfDirty(library);
        }
        static int CreatePictures()
        {
            var recipe=AssemblyRecipeStore.Load(AssemblyWorkshopBuilder.RecipePath);
            var original=AssetDatabase.LoadAssetAtPath<AssemblyChapterArtwork>("Assets/Resources/AssemblyChapterArtwork.asset");
            const string cataloguePath="Assets/Resources/LightChapterArtwork.asset";
            var catalogue=AssetDatabase.LoadAssetAtPath<AssemblyChapterArtwork>(cataloguePath);if(!catalogue){catalogue=ScriptableObject.CreateInstance<AssemblyChapterArtwork>();AssetDatabase.CreateAsset(catalogue,cataloguePath);}
            catalogue.cards.Clear();Directory.CreateDirectory("Assets/UI/ChapterImages/Light");
            var palette=AssetDatabase.LoadAssetAtPath<EngineSurfaceLibrary>("Assets/Resources/EngineSurfaceLibrary.asset");
            var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            var highlight=new Material(palette.Find("Cast aluminium"));highlight.SetColor("_BaseColor",new Color(.36f,.55f,.80f));highlight.SetFloat("_Metallic",.2f);int count=0;
            try
            {
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.7f,.74f,.77f);
                var root=new GameObject("Temporary chapter photograph").transform;
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ThreeCylinderEngine_CoACD.prefab");
                var groups=new Dictionary<string,Transform>();foreach(var group in recipe.subAssemblies){var carrier=recipe.parts.First(p=>p.id==group.rootPartId);var t=new GameObject(group.id).transform;t.SetParent(root,false);t.SetLocalPositionAndRotation(carrier.targetPosition,carrier.targetRotation);groups[group.id]=t;}
                var nodes=new List<(PartDefinition part,MeshRenderer renderer)>();
                foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    var part=recipe.parts.FirstOrDefault(p=>p.id==filter.name);if(part==null || !filter.GetComponent<MeshRenderer>())continue;
                    var node=new GameObject(part.id);node.layer=30;node.transform.SetParent(string.IsNullOrEmpty(part.subAssemblyId)?root:groups[part.subAssemblyId],false);node.transform.localScale=filter.transform.lossyScale;node.transform.SetLocalPositionAndRotation(part.targetPosition,part.targetRotation);
                    node.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;nodes.Add((part,node.AddComponent<MeshRenderer>()));
                }
                var camera=new GameObject("Temporary photograph camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=WorkshopTheme.Background;camera.cullingMask=1<<30;camera.fieldOfView=34;camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                var key=new GameObject("Temporary key light").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.6f;key.transform.rotation=Quaternion.Euler(35,-35,0);
                var fill=new GameObject("Temporary fill light").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.8f;fill.transform.rotation=Quaternion.Euler(15,145,0);
                for(int i=0;i<recipe.chapters.Count;i++)
                {
                    var chapter=recipe.chapters[i];foreach(var item in nodes){item.renderer.enabled=recipe.chapters.FindIndex(c=>c.id==item.part.chapterId)<=i;item.renderer.sharedMaterial=item.part.chapterId==chapter.id?highlight:palette.Find(EngineSurfaceLibrary.Category(item.part.id));}
                    var visible=nodes.Where(n=>n.renderer.enabled).Select(n=>n.renderer).ToArray();if(visible.Length==0)continue;
                    var bounds=visible[0].bounds;foreach(var renderer in visible)bounds.Encapsulate(renderer.bounds);
                    float distance=Mathf.Max(.25f,bounds.extents.magnitude)/Mathf.Sin(camera.fieldOfView*Mathf.Deg2Rad*.5f)*1.05f;
                    camera.transform.position=bounds.center+new Vector3(1,.6f,-1).normalized*distance;camera.transform.LookAt(bounds.center);
                    string path="Assets/UI/ChapterImages/Light/"+chapter.id+".png";WorkshopPresentationBuilder.Render(camera,path,960,400);AssetDatabase.ImportAsset(path);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.npotScale=TextureImporterNPOTScale.None;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
                    catalogue.cards.Add(new AssemblyChapterArtwork.Card{recipeTitle=recipe.title,chapterId=chapter.id,image=AssetDatabase.LoadAssetAtPath<Texture2D>(path),replaces=original?original.Find(recipe.title,chapter.id):null});count++;
                }
                EditorUtility.SetDirty(catalogue);AssetDatabase.SaveAssetIfDirty(catalogue);
            }
            finally{UnityEngine.Object.DestroyImmediate(highlight);EditorSceneManager.CloseScene(scene,true);if(previous.IsValid() && previous.isLoaded)UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);}
            return count;
        }
    }
}
