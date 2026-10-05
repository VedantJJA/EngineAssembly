using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class ValvePlacementVerification
    {
        static ValvePlacementVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch(){SessionState.SetBool("ValvePlacement.Verify",true);EditorSceneManager.OpenScene(AssemblyWorkshopBuilder.ScenePath,OpenSceneMode.Single);EditorApplication.isPlaying=true;}
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("ValvePlacement.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Valve placement verification").AddComponent<ValvePlacementVerificationRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("ValvePlacement.Verify",false);EditorApplication.Exit(SessionState.GetBool("ValvePlacement.Result",false)?0:1);}
        }
        public static void UpdateWorkspace()
        {
            if(!Application.isBatchMode)throw new Exception("Use isolated batch project.");
            var scene=EditorSceneManager.OpenScene(AssemblyWorkshopBuilder.ScenePath,OpenSceneMode.Single);
            var frame=scene.GetRootGameObjects().Single(o=>o.name=="Movable Engine Base").transform;frame.localScale=Vector3.one;frame.position=new Vector3(frame.position.x,1.55f,frame.position.z);
            var table=scene.GetRootGameObjects().Single(o=>o.name=="Parts Table").transform;table.localScale*=9/table.localScale.x;table.position=new Vector3(table.position.x,.9f,table.position.z);
            var stand=scene.GetRootGameObjects().Single(o=>o.name=="Engine Stand").transform;stand.position=new Vector3(0,.075f,0);stand.localScale=new Vector3(2,.15f,1.8f);
            var manager=UnityEngine.Object.FindAnyObjectByType<AssemblyManager>();var settings=new SerializedObject(manager);settings.FindProperty("useHeadBenchPosition").boolValue=true;settings.FindProperty("headBenchPosition").vector3Value=new Vector3(2.4f,-.5f,0);settings.ApplyModifiedPropertiesWithoutUndo();
            foreach(var precision in UnityEngine.Object.FindObjectsByType<AssemblyPrecisionSurface>(FindObjectsInactive.Include))precision.RefreshPhysics();
            AssemblyBatchSetup.AddPrecisionSurface(UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include).Single(f=>f.name=="401_Cylinder_Head_01").gameObject);
            EditorSceneManager.SaveScene(scene);
            scene=EditorSceneManager.OpenScene(MobileAssemblyBuilder.ScenePath,OpenSceneMode.Single);
            AssemblyBatchSetup.AddPrecisionSurface(UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include).Single(f=>f.name=="401_Cylinder_Head_01").gameObject);EditorSceneManager.SaveScene(scene);
            foreach(var path in new[]{"Assets/Prefabs/ThreeCylinderEngine_CoACD.prefab","Assets/Prefabs/CylinderHead_Precise.prefab"})
            {
                var root=PrefabUtility.LoadPrefabContents(path);
                try{var head=root.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="401_Cylinder_Head_01" || f.sharedMesh.name=="401_Cylinder_Head_01");AssemblyBatchSetup.AddPrecisionSurface(head.gameObject);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();EditorApplication.Exit(0);
        }
    }
    public sealed class ValvePlacementVerificationRunner:MonoBehaviour
    {
        [Serializable] class Report{public bool passed;public string[] checks;public string error;}
        readonly List<string> checks=new List<string>();
        IEnumerator Start()
        {
            var routine=Scenario();string error="";
            while(true){bool more=false;object step=null;try{more=routine.MoveNext();if(more)step=routine.Current;}catch(Exception ex){error=ex.ToString();}if(!more || error.Length>0)break;yield return step;}
            File.WriteAllText("Tools/Verification/valve-placement-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,checks=checks.ToArray(),error=error},true));SessionState.SetBool("ValvePlacement.Result",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Scenario()
        {
            yield return null;yield return null;yield return null;
            var manager=AssemblyManager.Instance;manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);manager.SetDifficulty(SnapDifficulty.Easy,false);manager.SelectChapter(3);
            var player=FindAnyObjectByType<PlayerAssemblyController>();player.enabled=false;player.gameObject.GetComponent<CharacterController>().enabled=false;
            var camera=player.ViewCamera;var guide=manager.GetComponent<AssemblyEasyGuide>();
            foreach(var part in manager.AssemblyParts){var s=new SerializedObject(part);s.FindProperty("snapDuration").floatValue=0;s.ApplyModifiedPropertiesWithoutUndo();}
            var head=manager.FindPart("401_Cylinder_Head_01");head.InstallImmediate(head.TargetSocket);
            var frame=manager.TargetFrame(head.Definition);frame.rotation=Quaternion.Euler(180,0,0);
            var valves=manager.AssemblyParts.Where(p=>p.PartId.StartsWith("404_")).ToArray();foreach(var valve in valves)valve.InstallImmediate(valve.TargetSocket);frame.rotation=Quaternion.identity;
            var surface=head.transform.Find("Precision CAD surface").GetComponent<MeshCollider>();head.GetComponent<AssemblyPrecisionSurface>().RefreshPhysics();Physics.SyncTransforms();
            Assert(surface.enabled && !surface.convex && head.GetComponentInChildren<GeneratedColliderGroup>().GetComponentsInChildren<Collider>(true).All(c=>!c.enabled),"Head assembly uses the exact open CAD surface and disables all cavity-filling convex hulls");
            foreach(var valve in valves)
            {
                var mesh=valve.GetComponent<MeshFilter>().sharedMesh;var ext=mesh.bounds.extents;int axis=ext.x>ext.y?(ext.x>ext.z?0:2):(ext.y>ext.z?1:2);var local=Vector3.zero;local[axis]=1;
                var direction=valve.transform.TransformVector(local).normalized;float length=valve.transform.TransformVector(local*mesh.bounds.size[axis]).magnitude;var center=valve.transform.TransformPoint(mesh.bounds.center);
                Assert(!surface.Raycast(new Ray(center-direction*length*.5f,direction),out _,length),"Open head bore: "+valve.PartId);
            }
            foreach(var valve in valves)
            {
                var seat=manager.AssemblyParts.Single(p=>p.PartId.StartsWith("402_") && p.Definition.prerequisites.Contains(valve.PartId));
                var seal=manager.AssemblyParts.Single(p=>p.PartId.StartsWith("403_") && p.Definition.prerequisites.Contains(seat.PartId));
                var spring=manager.AssemblyParts.Single(p=>p.PartId.StartsWith("405_") && p.Definition.prerequisites.Contains(seal.PartId));
                var retainer=manager.AssemblyParts.Single(p=>p.PartId.StartsWith("406_") && p.Definition.prerequisites.Contains(spring.PartId));
                var keepers=manager.AssemblyParts.Where(p=>p.PartId.StartsWith("407_") && p.Definition.prerequisites.Contains(retainer.PartId));
                foreach(var part in new[]{seat,seal,spring,retainer}.Concat(keepers))
                {
                    Assert(part.BeginGrab(),"Eligible valve-stack pickup: "+part.PartId);var pose=part.GetTargetPose(part.TargetSocket);part.MoveHeld(pose.position+Vector3.right*8,pose.rotation);guide.RefreshTargets();
                    var mesh=part.GetComponent<MeshFilter>().sharedMesh;var center=pose.position+pose.rotation*Vector3.Scale(mesh.bounds.center,part.transform.lossyScale);
                    int visible=0;Ray clearRay=default;
                    for(int i=0;i<16;i++)
                    {
                        float angle=i*Mathf.PI/4;float horizontal=i<8?.35f:.7f;camera.transform.position=center+new Vector3(Mathf.Cos(angle)*horizontal,1,Mathf.Sin(angle)*horizontal);camera.transform.LookAt(center);
                        var ray=camera.ScreenPointToRay(camera.pixelRect.center);bool before=Physics.queriesHitBackfaces;
                        if(guide.Aim(ray,10,camera)==part){visible++;clearRay=ray;}
                        Assert(before==Physics.queriesHitBackfaces,"Backface query setting restored: "+part.PartId+" view "+i);
                    }
                    if(visible<2)
                    {
                        var diagnostics=new List<string>();
                        foreach(var obstacle in new[]{head,seat,valve})
                        {
                            var renderer=obstacle.GetComponent<MeshRenderer>();renderer.enabled=false;guide.RefreshTargets();int unblocked=0;
                            for(int i=0;i<16;i++){float angle=i*Mathf.PI/4;float horizontal=i<8?.35f:.7f;camera.transform.position=center+new Vector3(Mathf.Cos(angle)*horizontal,1,Mathf.Sin(angle)*horizontal);camera.transform.LookAt(center);if(guide.Aim(camera.ScreenPointToRay(camera.pixelRect.center),10,camera)==part)unblocked++;}
                            diagnostics.Add("without "+obstacle.PartId+": "+unblocked);renderer.enabled=true;
                        }
                        File.WriteAllText("Tools/Verification/valve-occlusion-diagnostic.txt",part.PartId+" mesh size "+Vector3.Scale(mesh.bounds.size,part.transform.lossyScale)+" center "+center+"\n"+string.Join("\n",diagnostics));
                    }
                    Assert(visible>=2,"Valve ghost can be aimed from multiple normal working views: "+part.PartId+" ("+visible+"/16)");
                    camera.transform.position=clearRay.origin;camera.transform.rotation=Quaternion.LookRotation(clearRay.direction);
                    if(part.PartId.StartsWith("402_") || part.PartId.StartsWith("403_"))
                    {
                        foreach(var difficulty in new[]{SnapDifficulty.Hard,SnapDifficulty.Medium}){manager.SetDifficulty(difficulty,false);Assert(guide.VisibleFrom(part,camera),difficulty+" sees the real recessed CAD target: "+part.PartId);}
                        manager.SetDifficulty(SnapDifficulty.Easy,false);
                    }
                    if(part.PartId.StartsWith("403_") || part.PartId.StartsWith("407_"))
                    {
                        int nearHits=0;var forward=clearRay.direction;var right=Vector3.Cross(forward,Vector3.up).normalized;var up=Vector3.Cross(right,forward);float depth=Vector3.Dot(center-clearRay.origin,forward);
                        foreach(var offset in new[]{right,-right,up,-up})if(guide.Aim(new Ray(clearRay.origin,clearRay.GetPoint(depth)+offset*.015f-clearRay.origin),10,camera)==part)nearHits++;
                        Assert(nearHits>0,"Tiny-part targeting tolerates a nearby cursor: "+part.PartId);
                    }
                    var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=clearRay.GetPoint(.35f);wall.transform.rotation=Quaternion.LookRotation(clearRay.direction);wall.transform.localScale=new Vector3(.4f,.4f,.05f);Physics.SyncTransforms();
                    Assert(!guide.Place(clearRay,10,camera),"Occluded valve ghost rejects placement: "+part.PartId);wall.SetActive(false);Destroy(wall);Physics.SyncTransforms();
                    Assert(guide.Place(clearRay,10,camera) && part.IsSnapped,"Visible valve-stack placement: "+part.PartId);
                    Assert(Vector3.Distance(part.transform.position,pose.position)<.0001f && Quaternion.Angle(part.transform.rotation,pose.rotation)<.01f,"Guided insertion retains the exact CAD target: "+part.PartId);yield return null;
                }
            }
        }
        void Assert(bool value,string text){if(!value)throw new Exception(text);checks.Add(text);}
    }
}
