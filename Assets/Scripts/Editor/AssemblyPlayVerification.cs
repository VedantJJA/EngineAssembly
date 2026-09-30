using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

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
            var settings=new SerializedObject(manager);settings.FindProperty("showWorkshopUI").boolValue=false;settings.ApplyModifiedPropertiesWithoutUndo();
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
            manager.SelectChapter(0);root.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            manager.SetDifficulty(SnapDifficulty.Hard);a.BeginGrab();a.MoveHeld(a.TargetSocket.SnapPosition,Quaternion.Euler(90,0,0));
            Assert(!a.Evaluate(a.TargetSocket,true,out _),"Hard rejects incorrect rotation");
            manager.SetDifficulty(SnapDifficulty.Medium);
            Assert(a.Evaluate(a.TargetSocket,true,out _),"Medium accepts position without matching rotation");
            a.MoveHeld(Vector3.up,Quaternion.identity);Assert(!a.Evaluate(a.TargetSocket,true,out _),"Medium still requires proximity");
            manager.SetInteraction(WorkshopInteraction.Move);a.MoveHeld(a.TargetSocket.SnapPosition,Quaternion.identity);
            Assert(!a.Release() && !a.IsBusy,"Move mode cannot accidentally snap on release");
            manager.SetInteraction(WorkshopInteraction.Build);manager.SelectChapter(0);manager.SetDifficulty(SnapDifficulty.Easy);
            var guide=manager.GetComponent<AssemblyEasyGuide>();
            var ray=new Ray(new Vector3(0,0,-2),Vector3.forward);
            Assert(!guide.Place(ray,5),"Easy requires a held part before placement");
            Assert(a.BeginGrab(),"Easy lets the player pick up the loose part");
            Assert(guide.Aim(ray,5)==a,"Easy targets actual ghost mesh along view ray");
            Assert(guide.Aim(new Ray(new Vector3(0,0,-2),Vector3.back),5)==null,"Easy cannot place a ghost behind the view");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,0,-1);wall.transform.localScale=new Vector3(.5f,.5f,.1f);Physics.SyncTransforms();
            Assert(!guide.Place(ray,5),"Easy placement is blocked by solid occluders");
            wall.SetActive(false);Physics.SyncTransforms();
            Assert(guide.Place(ray,5),"Easy click starts placement from the tray");
            yield return new WaitForSeconds(.3f);Assert(a.IsSnapped,"Easy placement completes normally");
            manager.SetMode(WorkshopMode.Edit);manager.AddChapter("Draft");
            Assert(manager.Recipe.Validate().Count==0 && !manager.ChapterComplete,"Empty draft chapters can be saved but never report completion");
            Assert(a.IsSnapped && b.IsSnapped,"Earlier chapters are assembled when visiting a new draft");
            manager.SetMode(WorkshopMode.Assembly);manager.SelectChapter(0);manager.Recipe.parts.Find(p=>p.id=="b").order=2;
            manager.SetTask(AssemblyTask.Disassemble);
            Assert(a.IsSnapped && b.IsSnapped,"Disassembly starts with the selected section assembled");
            Assert(!manager.CanRemove(a,out _) && manager.NextHintPart()==b,"Disassembly and hints enforce reverse priority");
            Assert(!manager.CanInstall(b,b.TargetSocket,out _),"Disassembly does not permit snapping parts back");
            b.BeginGrab();b.Release(false);a.BeginGrab();a.Release(false);
            Assert(manager.ChapterComplete,"Removing all section parts completes disassembly");
            manager.SetTask(AssemblyTask.Assemble);Assert(!a.IsSnapped && !b.IsSnapped && !manager.ChapterComplete,"Assembly toggle stages the section again");
            if(!manager.GetComponent<AssemblyHint>().Enabled)manager.GetComponent<AssemblyHint>().Toggle();yield return null;
            Assert(manager.GetComponent<AssemblyHint>().HighlightedPart==a,"Hint highlights next required component");
            Assert(manager.GetComponentsInChildren<AssemblyVisual>().Length>=2,"Hint includes source and target visual channels");
            var hintMeshes=Resources.FindObjectsOfTypeAll<MeshRenderer>().Where(r=>r.gameObject.activeInHierarchy && r.gameObject.name=="Visual" && r.sharedMaterial && r.sharedMaterial.GetInt("_ZTest")==8).ToArray();
            Assert(hintMeshes.Length>=2,"Hint source and ghost use through-wall depth testing");
            manager.GetComponent<AssemblyHint>().Toggle();
            manager.SetMode(WorkshopMode.Edit);manager.SetInteraction(WorkshopInteraction.Move);
            var playerObject=new GameObject("Control test player");playerObject.AddComponent<CharacterController>();var cameraObject=new GameObject("View");cameraObject.transform.SetParent(playerObject.transform,false);cameraObject.AddComponent<Camera>();
            playerObject.transform.position=new Vector3(0,0,-2);var player=playerObject.AddComponent<PlayerAssemblyController>();
            player.PickUp(a);Assert(root.GetComponent<PickupablePlatform>().IsHeld && a.IsSnapped,"Edit Move picks up the engine frame instead of displacing an installed block");
            var relative=Quaternion.Inverse(player.ViewCamera.transform.rotation)*root.rotation;
            playerObject.transform.Rotate(0,110,0);yield return null;
            Assert(Quaternion.Angle(Quaternion.Inverse(player.ViewCamera.transform.rotation)*root.rotation,relative)<.1f,"Held workpiece retains its facing side while turning");
            Assert(Vector3.Distance(a.transform.position,a.GetTargetPose(a.TargetSocket).position)<.001f && Vector3.Distance(b.transform.position,b.GetTargetPose(b.TargetSocket).position)<.001f,"Every socket stays aligned when the engine frame moves");
            player.DropHeldPart();manager.SetInteraction(WorkshopInteraction.Build);manager.SetMode(WorkshopMode.Assembly);player.PickUp(a);
            relative=Quaternion.Inverse(player.ViewCamera.transform.rotation)*a.transform.rotation;playerObject.transform.Rotate(0,-65,0);yield return null;
            Assert(Quaternion.Angle(Quaternion.Inverse(player.ViewCamera.transform.rotation)*a.transform.rotation,relative)<.1f,"Held loose part retains its facing side while turning");
            player.DropHeldPart();
            // An oversized simulation hull must not hide a visible part or shift selection.
            var selection=playerObject.GetComponent<AssemblyPickupQuery>();
            var pickPart=GameObject.CreatePrimitive(PrimitiveType.Cube).AddComponent<AssemblyPart>();pickPart.SetIdentity("pickup-test","Pickup test");
            pickPart.transform.position=new Vector3(30,1,2);pickPart.transform.localScale=Vector3.one*.2f;pickPart.Body.isKinematic=true;
            var oversized=pickPart.GetComponent<BoxCollider>();oversized.center=Vector3.up*8;oversized.size=Vector3.one*12;
            foreach(float eyeHeight in new[]{1.65f,.9f})
            {
                var origin=new Vector3(30,eyeHeight,0);var pickRay=new Ray(origin,(pickPart.transform.position-origin).normalized);
                Assert(selection.Raycast(pickRay,5,~0,out var picked,out _,out _) && picked==pickPart,"Visible part selection works at eye height "+eyeHeight);
            }
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=new Vector3(30,1.3f,1);blocker.transform.localScale=new Vector3(1,2,.1f);
            var standingRay=new Ray(new Vector3(30,1.65f,0),(pickPart.transform.position-new Vector3(30,1.65f,0)).normalized);
            Assert(selection.Raycast(standingRay,5,~0,out var obscured,out _,out var wallHit) && !obscured && wallHit.collider==blocker.GetComponent<Collider>(),"Pickup respects solid environment occlusion");
            pickPart.gameObject.SetActive(false);blocker.SetActive(false);Destroy(pickPart.gameObject);Destroy(blocker);
#if ENABLE_INPUT_SYSTEM
            InputSystem.settings=Instantiate(InputSystem.settings);
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(10,-.25f,0);floor.transform.localScale=new Vector3(4,.5f,4);playerObject.transform.position=new Vector3(10,0,0);Physics.SyncTransforms();
            var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
            yield return new WaitForSeconds(.15f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftCtrl));InputSystem.Update();yield return null;
            Assert(playerObject.GetComponent<CharacterController>().height<1.2f,"Control key crouches the character collider");
            var ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);ceiling.transform.position=new Vector3(10,1.45f,0);ceiling.transform.localScale=new Vector3(2,.2f,2);Physics.SyncTransforms();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();yield return null;
            Assert(playerObject.GetComponent<CharacterController>().height<1.2f,"Crouch cannot stand into a low ceiling");
            ceiling.SetActive(false);Physics.SyncTransforms();yield return null;
            Assert(playerObject.GetComponent<CharacterController>().height>1.8f,"Character stands when headroom is clear");
            float beforeJump=playerObject.transform.position.y;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));InputSystem.Update();player.SendMessage("Update");
            Assert(playerObject.transform.position.y>beforeJump,"Space jumps from the floor");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));InputSystem.Update();yield return new WaitForSeconds(.2f);
            Assert(player.ViewCamera.fieldOfView<50,"Right click zooms when hands are empty");
            InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();yield return new WaitForSeconds(.2f);
            Assert(player.ViewCamera.fieldOfView>55,"Releasing right click restores normal field of view");
            InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);Destroy(floor);Destroy(ceiling);
#endif
            Destroy(playerObject);
            var bench=GameObject.CreatePrimitive(PrimitiveType.Cube);bench.transform.position=new Vector3(0,-2,0);bench.transform.localScale=new Vector3(2,.1f,2);var movable=bench.AddComponent<PickupablePlatform>();
            var staged=GameObject.CreatePrimitive(PrimitiveType.Cube);staged.transform.position=new Vector3(0,-1.89f,0);staged.transform.localScale=Vector3.one*.1f;var stagedBody=staged.AddComponent<Rigidbody>();stagedBody.isKinematic=true;
            movable.BeginGrab();Assert(staged.transform.IsChildOf(bench.transform),"Moving a bench carries staged kinematic parts");
            movable.MoveHeld(new Vector3(3,-2,0),Quaternion.Euler(0,35,0));movable.Release();
            Assert(!staged.transform.parent && stagedBody.isKinematic && staged.transform.position.x>2.9f,"Releasing bench restores passenger physics at the moved location");
            Destroy(bench);Destroy(staged);
            manager.SetDifficulty(SnapDifficulty.Hard);
        }
        void Assert(bool condition,string label) { if(!condition)throw new Exception(label);checks.Add(label); }
    }
}
