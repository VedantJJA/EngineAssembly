using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EngineAssembly.Editor
{
    public static class ColliderToolVerification
    {
        static AssemblyBatchSetup window;
        static GameObject cube;
        static GameObject bearing;
        static double start;
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        [Serializable] class Report { public bool passed;public string error;public int hulls; }
        public static void RunBatch()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Use the isolated verification project.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name="Detailed CoACD verification";
            // Reproduce CAD import units: tiny mesh coordinates, large Transform scale.
            var original=cube.GetComponent<MeshFilter>().sharedMesh;var tiny=UnityEngine.Object.Instantiate(original);tiny.vertices=tiny.vertices.Select(v=>v*1e-4f+Vector3.one*.002f).ToArray();tiny.RecalculateBounds();cube.GetComponent<MeshFilter>().sharedMesh=tiny;cube.transform.localScale=Vector3.one*10000;
            var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/Edit/3cyl_Labeled.fbx");
            bearing=UnityEngine.Object.Instantiate(model.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="102_Upper_Main_Bearing_Shell_04").gameObject);bearing.transform.position=new Vector3(4,0,0);
            window=ScriptableObject.CreateInstance<AssemblyBatchSetup>();
            var type=window.GetType();var field=type.GetField("method",Flags);field.SetValue(window,Enum.Parse(field.FieldType,"CoACD"));
            type.GetField("detailed",Flags).SetValue(window,true);type.GetField("threshold",Flags).SetValue(window,.01f);
            type.GetField("maxHulls",Flags).SetValue(window,64);
            var selected=(HashSet<GameObject>)type.GetField("selected",Flags).GetValue(window);selected.Add(cube);selected.Add(bearing);
            type.GetMethod("StartBatch",Flags).Invoke(window,null);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;
        }
        static void Poll()
        {
            string error="";int hulls=0;
            try
            {
                bool running=(bool)window.GetType().GetField("running",Flags).GetValue(window);
                if(running && EditorApplication.timeSinceStartup-start<600)return;
                if(running)throw new Exception("Tool smoke test timed out.");
                var group=cube.GetComponentInChildren<GeneratedColliderGroup>(true);
                if(!group || group.backend!="CoACD")throw new Exception((string)window.GetType().GetField("status",Flags).GetValue(window));
                var convex=group.GetComponentsInChildren<MeshCollider>(true);hulls=convex.Length;
                if(hulls<1 || convex.Any(c=>!c.sharedMesh || c.sharedMesh.bounds.size.sqrMagnitude==0 || Mathf.Max(c.sharedMesh.bounds.size.x,c.sharedMesh.bounds.size.y,c.sharedMesh.bounds.size.z)<.9f))throw new Exception("CoACD hulls were not normalized for stable cooking.");
                var surface=cube.transform.Find("Precision CAD surface").GetComponent<MeshCollider>();var precision=cube.GetComponent<AssemblyPrecisionSurface>();
                if(!surface.enabled || convex.Any(c=>c.enabled))throw new Exception("Kinematic CAD surface was not activated.");
                precision.PreparePhysics(false);cube.GetComponent<Rigidbody>().isKinematic=false;precision.SendMessage("LateUpdate");
                if(surface.enabled || convex.Any(c=>!c.enabled))throw new Exception("Dynamic collider transition failed.");
                var bearingGroup=bearing.GetComponentInChildren<GeneratedColliderGroup>(true);if(!bearingGroup)throw new Exception("Reported bearing shell did not receive CoACD colliders.");
                var bearingHulls=bearingGroup.GetComponentsInChildren<MeshCollider>(true);hulls+=bearingHulls.Length;
                var bearingPrecision=bearing.GetComponent<AssemblyPrecisionSurface>();bearingPrecision.PreparePhysics(false);bearing.GetComponent<Rigidbody>().isKinematic=false;bearingPrecision.RefreshPhysics();Physics.SyncTransforms();
                if(bearingHulls.Any(c=>!c.enabled || c.bounds.size.sqrMagnitude==0))throw new Exception("Reported bearing has invalid dynamic hulls.");
                foreach(var asset in AssetDatabase.FindAssets("t:ColliderHullSet",new[]{"Assets/GeneratedColliders/EngineCoACDValidated"}))
                {
                    var set=AssetDatabase.LoadAssetAtPath<ColliderHullSet>(AssetDatabase.GUIDToAssetPath(asset));if(set.hulls.Any(h=>!h.mesh))throw new Exception("Saved hull-set references were not persisted.");
                }
            }
            catch(Exception ex){error=ex.ToString();}
            EditorApplication.update-=Poll;Directory.CreateDirectory("Tools/Verification");
            File.WriteAllText("Tools/Verification/coacd-tool-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,error=error,hulls=hulls},true));
            UnityEngine.Object.DestroyImmediate(window);EditorApplication.Exit(error.Length==0?0:1);
        }
    }
}
