using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EngineAssembly.Editor
{
    public static class AssemblyWorkshopBuilder
    {
        public const string ScenePath="Assets/Scenes/AssemblyWorkshop.unity";
        public const string RecipePath="Assets/AssemblyRecipes/three-cylinder.json";
        [MenuItem("Tools/Engine Assembly/Create Three-Cylinder Workshop")]
        public static void Create()
        {
            if(File.Exists(ScenePath)) { EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));Debug.Log("Workshop already exists. Open it from Assets/Scenes.");return; }
            if(File.Exists(RecipePath))throw new InvalidOperationException("A three-cylinder recipe already exists. Rename it before generating a fresh workshop to preserve your edits.");
            var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/Edit/3cyl_Labeled.fbx");
            if(!model)throw new InvalidOperationException("Missing labeled FBX model.");
            var old=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var manager=new GameObject("Assembly Workshop").AddComponent<AssemblyManager>();
                var workpiece=new GameObject("Movable Engine Base").transform;
                var engine=(GameObject)PrefabUtility.InstantiatePrefab(model,scene);
                PrefabUtility.UnpackPrefabInstance(engine,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                var renderers=engine.GetComponentsInChildren<Renderer>();Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                engine.transform.position+=new Vector3(0,1.8f,0)-bounds.center;
                workpiece.position=new Vector3(0,1.8f,0);engine.transform.SetParent(workpiece,true);
                workpiece.gameObject.AddComponent<PickupablePlatform>();
                var recipe=new AssemblyRecipe { title="Three-cylinder engine workshop" };
                string[] titles={"Block and crankshaft","Pistons and connecting rods","Lower crankcase and oil sump","Cylinder head and valves","Camshafts and timing drive","Head cover and fuel system","Turbocharger and exhaust","Intake and external plumbing","Accessories and motor"};
                for(int i=0;i<titles.Length;i++)recipe.chapters.Add(new ChapterDefinition{id="chapter-"+(i+1),title=titles[i],description=i==2?"Turn the base upside down to install the lower crankcase and sump. Turn it upright for the flywheel.":"Complete each priority group, then continue to the next chapter."});
                var parts=new List<AssemblyPart>();
                var packing=new Dictionary<int,Vector3>();
                foreach(var mesh in engine.GetComponentsInChildren<MeshFilter>().OrderBy(m=>m.name))
                {
                    if(!mesh.sharedMesh || !int.TryParse(mesh.name.Split('_')[0],out int priority))continue;
                    var part=AssemblyBatchSetup.SetupPart(mesh.gameObject);part.SetIdentity(mesh.name,mesh.name.Substring(4).Replace('_',' '));parts.Add(part);
                    if(!mesh.GetComponent<Collider>()) { var collider=mesh.gameObject.AddComponent<MeshCollider>();collider.convex=true;collider.sharedMesh=mesh.sharedMesh; }
                    int chapter=Mathf.Clamp(priority/100,1,9);
                    var r=mesh.GetComponent<Renderer>();var b=r.bounds;
                    if(!packing.TryGetValue(chapter,out var cursor))cursor=new Vector3(-4,2,0);
                    float width=Mathf.Max(.12f,b.size.x)+.12f,depth=Mathf.Max(.12f,b.size.z)+.12f;
                    if(cursor.x+width>4) { cursor.x=-4;cursor.y+=cursor.z;cursor.z=0; }
                    Vector3 trayCenter=new Vector3(cursor.x+width/2,1.03f+b.extents.y,cursor.y+depth/2);
                    Vector3 trayOrigin=mesh.transform.position+(trayCenter-b.center);
                    cursor.x+=width;cursor.z=Mathf.Max(cursor.z,depth);packing[chapter]=cursor;
                    recipe.parts.Add(new PartDefinition { id=part.PartId,displayName=part.PartDisplayName,chapterId="chapter-"+chapter,order=priority,
                        targetPosition=workpiece.InverseTransformPoint(mesh.transform.position),targetRotation=Quaternion.Inverse(workpiece.rotation)*mesh.transform.rotation,
                        trayPosition=trayOrigin,trayRotation=mesh.transform.rotation,isBase=false,
                        orientation=chapter==3 && priority<305?AssemblyOrientation.UpsideDown:chapter==3 && priority<390?AssemblyOrientation.Upright:AssemblyOrientation.Any,
                        snapDistance=.18f,snapAngle=55,rotationMode=SnapRotationMode.Exact });
                }
                // A fixed workpiece frame supports the first block placement; the block remains an installable part.
                manager.Configure(workpiece,parts);
                AssemblyRecipeStore.Save(recipe,"three-cylinder");AssetDatabase.Refresh();AssemblyRecipeCatalogBuilder.Rebuild();
                manager.Configure(workpiece,parts,AssetDatabase.LoadAssetAtPath<TextAsset>(RecipePath));
                AddBox("Floor",new Vector3(0,-.15f,3),new Vector3(18,.3f,18),new Color(.12f,.15f,.2f));
                float far=packing.Values.Max(v=>v.y+v.z)+.4f;
                AddBox("Parts Table",new Vector3(0,.9f,(2+far)/2),new Vector3(9,.2f,far-2+.4f),new Color(.18f,.24f,.3f));
                AddBox("Engine Stand",new Vector3(0,.45f,0),new Vector3(2,.9f,1.8f),new Color(.22f,.28f,.34f));
                AddBox("Portable Work Platform",new Vector3(-3,1,0),new Vector3(1.2f,.15f,.9f),new Color(.12f,.3f,.36f)).AddComponent<PickupablePlatform>();
                var player=new GameObject("Player");player.transform.position=new Vector3(0,0,-4);var controller=player.AddComponent<CharacterController>();controller.height=1.8f;controller.center=new Vector3(0,.9f,0);controller.radius=.25f;
                var camera=new GameObject("Main Camera");camera.tag="MainCamera";camera.transform.SetParent(player.transform,false);camera.transform.localPosition=new Vector3(0,1.65f,0);camera.AddComponent<Camera>();camera.AddComponent<AudioListener>();player.AddComponent<PlayerAssemblyController>();
                var light=new GameObject("Workshop Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(45,-30,0);
                RenderSettings.ambientLight=new Color(.55f,.6f,.7f);
                EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
                Debug.Log("Created AssemblyWorkshop scene and nine-chapter recipe ("+parts.Count+" parts).");
            }
            finally { if(old.IsValid() && old.isLoaded)SceneManager.SetActiveScene(old);EditorSceneManager.CloseScene(scene,true); }
        }
        static GameObject AddBox(string name,Vector3 position,Vector3 size,Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=position;go.transform.localScale=size;
            string path="Assets/Resources/Workshop_"+name.Replace(" ","")+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material) { material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=color;AssetDatabase.CreateAsset(material,path); }
            go.GetComponent<Renderer>().sharedMaterial=material;
            return go;
        }
    }
}
