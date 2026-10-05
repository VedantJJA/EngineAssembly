using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EngineAssembly.Editor
{
    public static class ValidatedEngineColliderBake
    {
        const string Cache="Tools/Colliders/EngineBake";
        const string Output="Assets/GeneratedColliders/EngineCoACDValidated";
        [Serializable] class Report{public bool passed;public int parts,uniqueMeshes,hulls,failedCaseHulls;public string error;}
        public static void RunBatch()
        {
            if(!Application.isBatchMode || !Application.dataPath.Replace('\\','/').EndsWith("/Tools/Verification/UnityProject/Assets"))throw new InvalidOperationException("Use the isolated verification project.");
            string error="";int count=0,hulls=0,failedCase=0;
            try
            {
                // Exercise the exact returned decomposition from the user's failed tool job too.
                string failedPath="Tools/Verification/failing-hulls.json";
                if(File.Exists(failedPath))
                {
                    var failed=JsonUtility.FromJson<EngineColliderBake.Result>(File.ReadAllText(failedPath));
                    for(int i=0;i<failed.hulls.Length;i++){var h=failed.hulls[i];var mesh=new Mesh{vertices=h.vertices,triangles=h.triangles};mesh.RecalculateBounds();var cooked=ColliderHullCooking.Prepare(mesh,"Cached job hull "+i);UnityEngine.Object.DestroyImmediate(mesh);if(cooked!=null){UnityEngine.Object.DestroyImmediate(cooked.mesh);failedCase++;}}
                }
                var manifest=JsonUtility.FromJson<EngineColliderBake.Manifest>(File.ReadAllText(Cache+"/manifest.json"));
                Directory.CreateDirectory(Output);AssetDatabase.Refresh();var sets=new Dictionary<string,ColliderHullSet>();
                foreach(var record in manifest.parts.GroupBy(p=>p.key).Select(g=>g.First()))
                {
                    string path=Output+"/"+record.key+".asset";var saved=AssetDatabase.LoadAssetAtPath<ColliderHullSet>(path);
                    if(saved && saved.hulls.Any(h=>!h.mesh))
                    {
                        var stored=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToArray();
                        for(int i=0;i<saved.hulls.Length;i++){string suffix="_Hull_"+i.ToString("000");saved.hulls[i].mesh=stored.Single(m=>m.name.EndsWith(suffix));}
                        EditorUtility.SetDirty(saved);
                    }
                    if(!saved)
                    {
                        var rawPath=record.name=="401_Cylinder_Head_01"?"Tools/Colliders/HeadDetailed/head.output.json":Cache+"/"+record.key+".output.json";
                        var raw=JsonUtility.FromJson<EngineColliderBake.Result>(File.ReadAllText(rawPath));
                        var prepared=new List<ColliderHullSet.Hull>();
                        for(int i=0;i<raw.hulls.Length;i++)
                        {
                            var h=raw.hulls[i];var mesh=new Mesh{vertices=h.vertices,triangles=h.triangles};mesh.RecalculateBounds();
                            var hull=ColliderHullCooking.Prepare(mesh,record.name+"_Hull_"+i.ToString("000"));if(hull!=null)prepared.Add(hull);UnityEngine.Object.DestroyImmediate(mesh);
                        }
                        if(prepared.Count==0)throw new InvalidOperationException("No solid hulls for "+record.name);
                        saved=ScriptableObject.CreateInstance<ColliderHullSet>();saved.hulls=prepared.ToArray();AssetDatabase.CreateAsset(saved,path);
                        foreach(var hull in saved.hulls)AssetDatabase.AddObjectToAsset(hull.mesh,path);
                        EditorUtility.SetDirty(saved);
                    }
                    sets[record.key]=saved;Debug.Log("VALIDATED "+record.name+": "+saved.hulls.Length+" hulls");
                }
                count=manifest.parts.Count;
                foreach(var scenePath in new[]{AssemblyWorkshopBuilder.ScenePath,MobileAssemblyBuilder.ScenePath})
                {
                    var scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);
                    foreach(var root in scene.GetRootGameObjects())foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true).ToArray())
                    {
                        var record=manifest.parts.FirstOrDefault(r=>r.name==filter.name);if(record==null)continue;
                        Install(filter.gameObject,sets[record.key]);hulls+=sets[record.key].hulls.Length;
                    }
                    if(scenePath==AssemblyWorkshopBuilder.ScenePath)
                    {
                        var frame=scene.GetRootGameObjects().First(o=>o.name=="Movable Engine Base").transform;
                        frame.position=new Vector3(frame.position.x,1.55f,frame.position.z);frame.localScale=Vector3.one;
                        var table=scene.GetRootGameObjects().First(o=>o.name=="Parts Table").transform;
                        table.localScale*=9f/table.localScale.x;table.position=new Vector3(table.position.x,.9f,table.position.z);
                        var stand=scene.GetRootGameObjects().First(o=>o.name=="Engine Stand").transform;stand.position=new Vector3(0,.075f,0);stand.localScale=new Vector3(2,.15f,1.8f);
                        var settings=new SerializedObject(scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<AssemblyManager>()).Single());
                        settings.FindProperty("useHeadBenchPosition").boolValue=true;settings.FindProperty("headBenchPosition").vector3Value=new Vector3(2.4f,-.5f,0);settings.ApplyModifiedPropertiesWithoutUndo();
                    }
                    VerifyScene(scene);EditorSceneManager.SaveScene(scene);
                }
                const string prefabPath="Assets/Prefabs/ThreeCylinderEngine_CoACD.prefab";
                var prefab=PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true).ToArray()){var record=manifest.parts.FirstOrDefault(r=>r.name==filter.name);if(record!=null)Install(filter.gameObject,sets[record.key]);}
                    VerifyGeometry(prefab);PrefabUtility.SaveAsPrefabAsset(prefab,prefabPath);
                }
                finally{PrefabUtility.UnloadPrefabContents(prefab);}
                const string headPath="Assets/Prefabs/CylinderHead_Precise.prefab";var head=PrefabUtility.LoadPrefabContents(headPath);
                try{Install(head,sets[manifest.parts.First(r=>r.name=="401_Cylinder_Head_01").key]);PrefabUtility.SaveAsPrefabAsset(head,headPath);}finally{PrefabUtility.UnloadPrefabContents(head);}
                AssetDatabase.SaveAssets();Debug.Log("ENGINE COLLIDER REBUILD COMPLETE: "+count+" parts; "+sets.Count+" geometry sets; "+hulls+" scene hulls.");
                File.WriteAllText("Tools/Verification/validated-colliders-report.json",JsonUtility.ToJson(new Report{passed=true,parts=count,uniqueMeshes=sets.Count,hulls=hulls,failedCaseHulls=failedCase},true));
            }
            catch(Exception ex){error=ex.ToString();Debug.LogError(error);File.WriteAllText("Tools/Verification/validated-colliders-report.json",JsonUtility.ToJson(new Report{passed=false,parts=count,hulls=hulls,failedCaseHulls=failedCase,error=error},true));}
            EditorApplication.Exit(error.Length==0?0:1);
        }
        static void Install(GameObject target,ColliderHullSet set)
        {
            foreach(var old in target.GetComponentsInChildren<GeneratedColliderGroup>(true).Where(g=>g.transform.parent==target.transform).ToArray())UnityEngine.Object.DestroyImmediate(old.gameObject);
            foreach(var collider in target.GetComponents<Collider>().Where(c=>!c.isTrigger))collider.enabled=false;
            var root=new GameObject("Generated Colliders (CoACD Validated)");root.transform.SetParent(target.transform,false);root.AddComponent<GeneratedColliderGroup>().backend="CoACD";
            foreach(var hull in set.hulls)ColliderHullCooking.Add(root.transform,hull);
            AssemblyBatchSetup.AddPrecisionSurface(target); // exact mating surface on every static/carried component
        }
        static void VerifyScene(UnityEngine.SceneManagement.Scene scene){foreach(var root in scene.GetRootGameObjects())VerifyGeometry(root);}
        static void VerifyGeometry(GameObject root)
        {
            foreach(var precision in root.GetComponentsInChildren<AssemblyPrecisionSurface>(true))
            {
                var body=precision.GetComponent<Rigidbody>();bool temporary=!body;if(temporary){precision.PreparePhysics(false);body=precision.gameObject.AddComponent<Rigidbody>();body.isKinematic=true;}
                precision.PreparePhysics(false);body.isKinematic=false;precision.RefreshPhysics();Physics.SyncTransforms();
                var hulls=precision.GetComponentInChildren<GeneratedColliderGroup>().GetComponentsInChildren<MeshCollider>(true);
                if(hulls.Any(c=>!c.enabled || c.bounds.size.sqrMagnitude<=0))throw new InvalidOperationException("Empty dynamic hull: "+precision.name);
                body.isKinematic=true;precision.RefreshPhysics();
                var surface=precision.transform.Find("Precision CAD surface").GetComponent<MeshCollider>();
                if(!surface.enabled)throw new InvalidOperationException("Missing precise static surface: "+precision.name);
                if(temporary){UnityEngine.Object.DestroyImmediate(body);precision.RefreshPhysics();}
            }
        }
    }
}
