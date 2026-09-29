using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EngineAssembly.Editor
{
    [InitializeOnLoad]
    public static class AssemblyPlayVerification
    {
        static AssemblyPlayVerification() { EditorApplication.playModeStateChanged+=Changed; }
        // Run only in the isolated project, without -quit (the runner exits on completion).
        public static void RunBatch()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Use the isolated batch verification project.");
            SessionState.SetBool("Assembly.PlayVerification",true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("Assembly.PlayVerification",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Play verification").AddComponent<AssemblyPlayVerificationRunner>();
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool("Assembly.PlayVerification",false);
                EditorApplication.Exit(SessionState.GetBool("Assembly.PlayVerificationPassed",false)?0:1);
            }
        }
    }
    public class AssemblyPlayVerificationRunner : MonoBehaviour
    {
        readonly List<string> checks=new List<string>();
        [Serializable] class Report { public bool passed;public string[] checks;public string error; }
        IEnumerator Start()
        {
            var test=Scenario();string failure="";
            while(true)
            {
                bool next=false;object current=null;
                try { next=test.MoveNext();if(next)current=test.Current; }catch(Exception ex) { failure=ex.ToString(); }
                if(!next || failure.Length>0)break;
                yield return current;
            }
            Directory.CreateDirectory("Tools/Verification");File.WriteAllText("Tools/Verification/play-report.json",JsonUtility.ToJson(new Report{passed=failure.Length==0,checks=checks.ToArray(),error=failure},true));
            SessionState.SetBool("Assembly.PlayVerificationPassed",failure.Length==0);
            EditorApplication.isPlaying=false;
        }
        IEnumerator Scenario()
        {
            var go=new GameObject("Manager");go.SetActive(false);var manager=go.AddComponent<AssemblyManager>();
            var root=new GameObject("Workpiece").transform;
            var a=GameObject.CreatePrimitive(PrimitiveType.Cube).AddComponent<AssemblyPart>();a.SetIdentity("a","A");a.transform.localScale=Vector3.one*.1f;
            var b=GameObject.CreatePrimitive(PrimitiveType.Cube).AddComponent<AssemblyPart>();b.SetIdentity("b","B");b.transform.localScale=Vector3.one*.1f;
            var recipe=new AssemblyRecipe { chapters=new List<ChapterDefinition>{new ChapterDefinition{id="one",title="Test"}},parts=new List<PartDefinition>{
                new PartDefinition{id="a",displayName="A",chapterId="one",order=1,geometryGroup="test",trayPosition=new Vector3(1,1,0)},
                new PartDefinition{id="b",displayName="B",chapterId="one",order=1,geometryGroup="test",targetPosition=Vector3.right,trayPosition=new Vector3(2,1,0)}
            }};
            manager.Configure(root,new[]{a,b});manager.LoadRecipe(recipe);go.SetActive(true);
            yield return null;
            Assert(a.BeginGrab(),"Loose part can be picked up");
            a.MoveHeld(new Vector3(.05f,0,0),Quaternion.identity);
            Assert(a.SnapToSocket(a.TargetSocket),"Nearby compatible part starts smooth snap");
            Assert(!a.TargetSocket.Reserve(b),"Reservation blocks another part during animation");
            yield return new WaitForSeconds(.05f);
            root.SetPositionAndRotation(new Vector3(.4f,.2f,0),Quaternion.Euler(180,0,0));
            yield return new WaitForSeconds(.3f);
            Assert(a.IsSnapped && Vector3.Distance(a.transform.position,a.TargetSocket.SnapPosition)<.001f,"Smooth snap follows a moving and flipping base");
            Assert(a.transform.IsChildOf(root),"Installed part follows workpiece hierarchy");
            Assert(b.BeginGrab(),"Second part can be picked up");
            var pose=b.GetTargetPose(b.TargetSocket);b.MoveHeld(pose.position,pose.rotation);
            Assert(b.SnapToSocket(b.TargetSocket),"Second snap begins");
            b.gameObject.SetActive(false);yield return null;
            Assert(!b.TargetSocket.IsOccupied && !b.IsBusy,"Disabling a snapping part releases its reservation");
            b.gameObject.SetActive(true);
            Assert(b.BeginGrab(),"Cancelled part can be picked up again");
            pose=b.GetTargetPose(b.TargetSocket);b.MoveHeld(pose.position,pose.rotation);b.SnapToSocket(b.TargetSocket);
            manager.SelectChapter(0);yield return new WaitForSeconds(.3f);
            Assert(!a.IsSnapped && !b.IsSnapped && !b.TargetSocket.IsOccupied,"Chapter restart cancels in-flight snaps");
            int completed=0;manager.onChapterCompleted.AddListener(()=>completed++);
            a.InstallImmediate(a.TargetSocket);b.InstallImmediate(b.TargetSocket);
            Assert(manager.ChapterComplete && completed==1,"Chapter completion event fires once");
            manager.NotifyPartSnapped(b);Assert(completed==1,"Repeated notification cannot double-complete a chapter");
            var platform=root.GetComponent<PickupablePlatform>();
            Assert(platform.BeginGrab(),"Workpiece platform can be picked up");
            platform.Flip();yield return new WaitForSeconds(.6f);
            Assert(!platform.IsFlipping && a.IsSnapped && b.IsSnapped,"Flipping preserves installed parts");
            platform.Release();Assert(!platform.IsHeld && root.GetComponent<Rigidbody>().isKinematic,"Released workpiece remains a stable platform");
            Assert(Resources.Load<Material>("AssemblyGhost")!=null,"Ghost material available during Play mode");
        }
        void Assert(bool condition,string label) { if(!condition)throw new Exception(label);checks.Add(label); }
    }
}
