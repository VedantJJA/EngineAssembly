using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EngineAssembly.Editor
{
    public static class CylinderHeadColliderBake
    {
        const string Part="401_Cylinder_Head_01";
        const string Output="Assets/GeneratedColliders/CylinderHeadDetailed.asset";
        [Serializable] class Report { public bool passed;public int hulls,clearValveBores,materialSurfaceHits;public string error; }
        public static void ApplyAndVerify()=>Verify(true);
        public static void VerifyOriginalGeometry()=>Verify(false);
        static void Verify(bool apply)
        {
            string error="";int clear=0,hullCount=0,solidHits=0;
            try
            {
                if(apply)Apply();
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var engine=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ThreeCylinderEngine_CoACD.prefab"));
                var head=engine.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name==Part);
                if(!apply)AssemblyBatchSetup.AddPrecisionSurface(head.gameObject);
                var surface=head.transform.Find("Precision CAD surface").GetComponent<MeshCollider>();
                hullCount=head.GetComponentInChildren<GeneratedColliderGroup>().GetComponentsInChildren<MeshCollider>(true).Length;
                if(!surface.enabled || surface.convex || surface.sharedMesh.vertexCount!=head.sharedMesh.vertexCount || (apply && hullCount<=24))throw new Exception("Detailed head configuration is incomplete.");
                var original=head.sharedMesh.vertices;var collision=surface.sharedMesh.vertices;
                for(int i=0;i<original.Length;i+=Mathf.Max(1,original.Length/128))if(Vector3.Distance(head.transform.TransformPoint(original[i]),surface.transform.TransformPoint(collision[i]))>.0001f)throw new Exception("Normalized head collision frame changed the CAD surface.");
                Physics.SyncTransforms();
                if(surface.bounds.size.sqrMagnitude==0)throw new Exception("Precision head collider did not cook successfully.");
                var vertices=head.sharedMesh.vertices;var indices=head.sharedMesh.triangles;
                for(int t=0;t<indices.Length;t+=Mathf.Max(3,indices.Length/30/3*3))
                {
                    var a=head.transform.TransformPoint(vertices[indices[t]]);var b=head.transform.TransformPoint(vertices[indices[t+1]]);var c=head.transform.TransformPoint(vertices[indices[t+2]]);
                    var normal=Vector3.Cross(b-a,c-a).normalized;if(normal.sqrMagnitude==0)continue;
                    var center=(a+b+c)/3;
                    if(surface.Raycast(new Ray(center+normal*.01f,-normal),out _,.02f) || surface.Raycast(new Ray(center-normal*.01f,normal),out _,.02f))solidHits++;
                }
                if(solidHits==0)throw new Exception("Precision collider fails material-surface hit probes.");
                foreach(var valve in engine.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("404_")))
                {
                    var bounds=valve.sharedMesh.bounds;var extent=bounds.extents;int axis=extent.x>extent.y?(extent.x>extent.z?0:2):(extent.y>extent.z?1:2);
                    var local=Vector3.zero;local[axis]=1;var direction=valve.transform.TransformVector(local).normalized;
                    var center=valve.transform.TransformPoint(bounds.center);float length=valve.transform.TransformVector(local*bounds.size[axis]).magnitude;
                    var ray=new Ray(center-direction*length*.5f,direction);
                    if(surface.Raycast(ray,out var hit,length))throw new Exception("Valve bore axis blocked in exact CAD surface: "+valve.name+" at "+hit.point);
                    clear++;
                }
                UnityEngine.Object.DestroyImmediate(engine);
            }
            catch(Exception ex){error=ex.ToString();}
            Directory.CreateDirectory("Tools/Verification");File.WriteAllText("Tools/Verification/"+(apply?"head-collider-report.json":"head-surface-report.json"),JsonUtility.ToJson(new Report{passed=error.Length==0,hulls=hullCount,clearValveBores=clear,materialSurfaceHits=solidHits,error=error},true));
            EditorApplication.Exit(error.Length==0?0:1);
        }
        [MenuItem("Tools/Engine Assembly/Apply Precise Cylinder Head")]
        public static void ApplyToOpenScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode before replacing colliders.");
            var meshes=AssetDatabase.LoadAllAssetsAtPath(Output).OfType<Mesh>().OrderBy(m=>m.name).ToArray();
            if(meshes.Length==0)throw new InvalidOperationException("The detailed cylinder-head bake asset is missing.");
            var heads=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include).Where(f=>f.name==Part && f.gameObject.scene.IsValid()).ToArray();
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Apply precise cylinder head");
            foreach(var head in heads){Install(head.gameObject,meshes);EditorSceneManager.MarkSceneDirty(head.gameObject.scene);}
            Undo.CollapseUndoOperations(undo);
            Debug.Log("Applied detailed head colliders to "+heads.Length+" objects. Existing scene edits retained; Undo is available.");
        }
        public static void Apply()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Apply in the isolated verification project.");
            var result=JsonUtility.FromJson<EngineColliderBake.Result>(File.ReadAllText("Tools/Colliders/HeadDetailed/head.output.json"));
            if(result.hulls==null || result.hulls.Length<1)throw new InvalidDataException("No detailed head hulls.");
            var meshes=AssetDatabase.LoadAllAssetsAtPath(Output).OfType<Mesh>().OrderBy(m=>m.name).ToArray();
            if(meshes.Length==0)
            {
                meshes=result.hulls.Select((h,i)=>{
                    if(h.vertices.Length>128 || h.triangles.Length/3>255)throw new InvalidDataException("Invalid convex head hull.");
                    var mesh=new Mesh{name="Head Hull "+i.ToString("000"),vertices=h.vertices,triangles=h.triangles};mesh.RecalculateBounds();mesh.RecalculateNormals();return mesh;
                }).ToArray();
                AssetDatabase.CreateAsset(meshes[0],Output);foreach(var mesh in meshes.Skip(1))AssetDatabase.AddObjectToAsset(mesh,Output);
            }
            foreach(var path in new[]{AssemblyWorkshopBuilder.ScenePath,MobileAssemblyBuilder.ScenePath})
            {
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                var head=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<MeshFilter>(true)).Single(f=>f.name==Part).gameObject;
                Install(head,meshes);EditorSceneManager.SaveScene(scene);
            }
            const string prefab="Assets/Prefabs/ThreeCylinderEngine_CoACD.prefab";
            var root=PrefabUtility.LoadPrefabContents(prefab);
            try { var head=root.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name==Part).gameObject;Install(head,meshes);PrefabUtility.SaveAsPrefabAsset(root,prefab); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/Edit/3cyl_Labeled.fbx");
            var standalone=UnityEngine.Object.Instantiate(model.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name==Part).gameObject);
            standalone.name="Cylinder Head - Detailed CoACD and Precision Surface";standalone.transform.position=Vector3.zero;
            Install(standalone,meshes);PrefabUtility.SaveAsPrefabAsset(standalone,"Assets/Prefabs/CylinderHead_Precise.prefab");UnityEngine.Object.DestroyImmediate(standalone);
            AssetDatabase.SaveAssets();Debug.Log("HEAD COLLIDERS READY: "+meshes.Length+" CoACD hulls, 128 vertices maximum; precise kinematic CAD surface in both scenes and prefabs.");
        }
        static void Install(GameObject head,Mesh[] meshes)
        {
            ColliderHullSet set=null;
            if(AssetDatabase.IsValidFolder("Assets/GeneratedColliders/EngineCoACDValidated"))
                set=AssetDatabase.LoadAssetAtPath<ColliderHullSet>("Assets/GeneratedColliders/EngineCoACDValidated/"+EngineColliderBake.GeometryKey(head.GetComponent<MeshFilter>().sharedMesh)+".asset");
            if(!set)
            {
                throw new InvalidOperationException("No validated CoACD bake matches this cylinder-head geometry. Rebuild its colliders before applying the precise head; an older head bake cannot be reused.");
            }
            foreach(var old in head.GetComponentsInChildren<GeneratedColliderGroup>(true).Where(g=>g.transform.parent==head.transform).ToArray())Undo.DestroyObjectImmediate(old.gameObject);
            foreach(var old in head.GetComponents<Collider>())if(!old.isTrigger){Undo.RecordObject(old,"Disable rough head collider");old.enabled=false;}
            var root=new GameObject("Generated Colliders (CoACD Detailed)");root.transform.SetParent(head.transform,false);root.AddComponent<GeneratedColliderGroup>().backend="CoACD";
            Undo.RegisterCreatedObjectUndo(root,"Detailed head colliders");
            foreach(var hull in set.hulls)ColliderHullCooking.Add(root.transform,hull,true);
            AssemblyBatchSetup.AddPrecisionSurface(head);
        }
    }
}
