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
    [InitializeOnLoad] public static class WorkspaceReachVerification
    {
        static WorkspaceReachVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch(){SessionState.SetBool("Reach.Verify",true);EditorSceneManager.OpenScene(AssemblyWorkshopBuilder.ScenePath,OpenSceneMode.Single);EditorApplication.isPlaying=true;}
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("Reach.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Workspace verification").AddComponent<WorkspaceReachVerificationRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("Reach.Verify",false);EditorApplication.Exit(SessionState.GetBool("Reach.Result",false)?0:1);}
        }
    }
    public sealed class WorkspaceReachVerificationRunner:MonoBehaviour
    {
        [Serializable] class Report{public bool passed;public string[] checks;public string error;}
        readonly List<string> checks=new List<string>();
        IEnumerator Start()
        {
            var routine=Scenario();string error="";
            while(true){bool more=false;object step=null;try{more=routine.MoveNext();if(more)step=routine.Current;}catch(Exception ex){error=ex.ToString();}if(!more || error.Length>0)break;yield return step;}
            File.WriteAllText("Tools/Verification/workspace-reach-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,checks=checks.ToArray(),error=error},true));SessionState.SetBool("Reach.Result",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Scenario()
        {
            yield return null;yield return null;yield return null;
            var manager=AssemblyManager.Instance;var ui=manager.GetComponent<AssemblyWorkshopUI>();ui.SetOpen(false);
            manager.SetMode(WorkshopMode.Edit);manager.SelectChapter(8);yield return null;
            var bounds=manager.AssemblyParts.Where(p=>manager.CarrierDefinition(p.PartId)==null).Select(AssemblyHeldView.WorldBounds).ToArray();
            Assert(Vector3.Distance(manager.AssemblyRoot.lossyScale,Vector3.one)<.001f && bounds.Min(b=>b.min.y)>.1f,"PC engine retains the original model scale with floor clearance");
            manager.SetMode(WorkshopMode.Assembly);manager.SelectChapter(3);yield return null;
            var head=manager.FindPart("401_Cylinder_Head_01");var player=FindAnyObjectByType<PlayerAssemblyController>();
            Assert(AssemblyHeldView.WorldBounds(head).min.y>1 && AssemblyHeldView.WorldBounds(head).center.y<1.8f,"Full-size loose head is staged on a reachable parts table");
            var scale=head.transform.lossyScale;player.PickUp(head);yield return null;
            Assert(player.CurrentHeldPart==head && Vector3.Distance(scale,head.transform.lossyScale)<.01f,"PC pickup retains the object's workspace size");player.DropHeldPart(false);
            head.InstallImmediate(head.TargetSocket);yield return null;
            var fixture=manager.FindPart("480_Complete_Cylinder_Head").GetComponent<PickupablePlatform>();
            Assert(Vector3.Distance(fixture.transform.lossyScale,manager.AssemblyRoot.lossyScale)<.001f,"Bench subassemblies inherit the engine's presentation scale");
            var center=fixture.GeometryCenter();Assert(center.y<1.8f,"Head bench assembly is below standing reach");fixture.Flip();yield return new WaitForSeconds(.6f);
            Assert(Mathf.Abs(center.x-manager.AssemblyRoot.position.x)>2.0f && Mathf.Abs(center.x-manager.AssemblyRoot.position.x)<3.5f,"Cylinder-head workspace is in the requested middle area with room around the engine");
            Assert(Vector3.Distance(center,fixture.GeometryCenter())<.001f,"Head flips in place around its geometry centre");
            Assert(Vector3.Dot(fixture.transform.up,Vector3.up)<-.95f,"Centred flip still satisfies upside-down assembly orientation");
            player.enabled=false;var arrow=ui.GetComponentInChildren<WorkshopQuestArrow>(true);arrow.Toggle(true);
            var target=manager.NextHintPart();player.ViewCamera.transform.LookAt(AssemblyHeldView.WorldBounds(target).center);yield return null;
            Assert(arrow.Tracking && !arrow.ArrowVisible,"Onscreen target hides the quest arrow while keeping tracking enabled");
            player.ViewCamera.transform.Rotate(0,180,0);yield return null;Assert(arrow.ArrowVisible,"Offscreen target brings the quest arrow back");
            player.PickUp(target);var pose=target.GetTargetPose(target.TargetSocket);var localCenter=target.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(target).center);var destination=pose.position+pose.rotation*Vector3.Scale(localCenter,target.transform.lossyScale);
            player.ViewCamera.transform.LookAt(destination);yield return null;Assert(arrow.PointsToPlacement && !arrow.ArrowVisible,"Onscreen placement target also hides the quest arrow");player.DropHeldPart(false);
        }
        void Assert(bool value,string text){if(!value)throw new Exception(text);checks.Add(text);}
    }
}
