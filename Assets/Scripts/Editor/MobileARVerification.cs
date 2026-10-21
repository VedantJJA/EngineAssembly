using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class MobileARVerification
    {
        static MobileARVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch()
        {
            if(!Application.isBatchMode)throw new Exception("Use the isolated verification project.");
            VuforiaSceneSetup.Create();SessionState.SetBool("MobileAR.Verify",true);
            EditorSceneManager.OpenScene(ExperienceMenuBuilder.ScenePath,OpenSceneMode.Single);EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("MobileAR.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("AR flow verification").AddComponent<MobileARVerificationRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("MobileAR.Verify",false);EditorApplication.Exit(SessionState.GetBool("MobileAR.Result",false)?0:1);}
        }
    }
    // Deterministic test double; never installed in a shipped scene.
    public sealed class ARTestTrackingProvider:ARTrackingProvider
    {
        Camera camera;
        public bool reliable=true,surface=true,stopped;
        public int startupBehavior;
        public Transform anchor;
        public override Camera TrackingCamera=>camera;
        public override bool CanRunHere=>true;
        public override bool RequiresCameraPermission=>false;
        public override bool HasSurface=>surface && reliable;
        public override bool TrackingReliable=>reliable;
        public override void Begin(){if(startupBehavior==1)throw new InvalidOperationException("Simulated SDK startup exception");if(startupBehavior==2)return;camera=new GameObject("Test tracked camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,1,-3);ReportReady();}
        public override Transform PlaceAnchor(){anchor=new GameObject("Test world anchor").transform;anchor.position=new Vector3(.4f,0,.2f);return anchor;}
        public override void BeginRepositioning(){}
        public void FailAs(ARFailure failure)=>ReportFailure(failure);
        public override void Shutdown(){stopped=true;if(camera)Destroy(camera.gameObject);if(anchor)Destroy(anchor.gameObject);}
    }
    public sealed class MobileARVerificationRunner:MonoBehaviour
    {
        [Serializable] class Report{public bool passed;public string[] checks;public string error;}
        readonly List<string> checks=new List<string>();
        ARTestTrackingProvider tracking;
        int testStartupBehavior;
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";
            while(stack.Count>0)
            {
                bool more=false;object step=null;try{more=stack.Peek().MoveNext();if(more)step=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();break;}
                if(!more){stack.Pop();continue;}if(step is IEnumerator nested){stack.Push(nested);continue;}yield return step;
            }
            File.WriteAllText("Tools/Verification/mobile-ar-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,checks=checks.ToArray(),error=error},true));
            SessionState.SetBool("MobileAR.Result",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Wait(Func<bool> condition)
        {
            float end=Time.realtimeSinceStartup+20;while(!condition() && Time.realtimeSinceStartup<end)yield return null;
            Assert(condition(),"Asynchronous scene/session transition completed");
        }
        void PrepareFake(Scene scene,LoadSceneMode mode)
        {
            var session=FindAnyObjectByType<MobileARSession>();if(!session)return;
            tracking=session.gameObject.AddComponent<ARTestTrackingProvider>();session.Configure(tracking,session.FallbackScene);
            tracking.startupBehavior=testStartupBehavior;
            if(testStartupBehavior==2){var settings=new SerializedObject(session);settings.FindProperty("initializationTimeout").floatValue=.25f;settings.ApplyModifiedPropertiesWithoutUndo();}
        }
        IEnumerator Scenario()
        {
#if ENGINE_VUFORIA
            bool hitReceived=false;UnityEngine.Events.UnityAction<Vuforia.HitTestResult> onHit=_=>hitReceived=true;
            var factory=typeof(VuforiaGroundPlaneProvider).GetMethod("CreatePlaneFinder",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            var finder=(Vuforia.PlaneFinderBehaviour)factory.Invoke(null,new object[]{null,onHit});finder.OnAutomaticHitTest.Invoke(null);
            Assert(hitReceived && finder.OnInteractiveHitTest!=null && !finder.gameObject.activeSelf,"Production runtime Plane Finder accepts hit events before activation");Destroy(finder.gameObject);
#endif
            for(int behavior=1;behavior<=2;behavior++)foreach(string scene in new[]{"MobileAssemblyAR","MobileDisplayAR"})
            {
                testStartupBehavior=behavior;SceneManager.sceneLoaded+=PrepareFake;ExperienceMenu.Launch(scene);
                yield return null;
                yield return Wait(()=>!FindAnyObjectByType<MobileARSession>() && FindAnyObjectByType<MobileAROverlay>()?.NoticeVisible==true);SceneManager.sceneLoaded-=PrepareFake;
                Assert(FindAnyObjectByType<MobileAROverlay>().NoticeText.StartsWith("AR is unavailable"),(behavior==1?"SDK startup exception":"Missing SDK ready callback")+" reaches a usable fallback in "+scene);
            }
            testStartupBehavior=0;
            foreach(string scene in new[]{"MobileAssemblyAR","MobileDisplayAR"})
            {
                ExperienceMenu.Launch(scene);
                yield return null;
                yield return Wait(()=>!FindAnyObjectByType<MobileARSession>() && FindAnyObjectByType<MobileAROverlay>()?.NoticeVisible==true);
                var notice=FindAnyObjectByType<MobileAROverlay>();
                Assert(notice.NoticeText=="Opening the standard phone view.","Desktop uses phone view without claiming unsupported phone hardware");
                Assert(notice.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length==0 && notice.GetComponentsInChildren<TMPro.TextMeshProUGUI>().All(t=>t.font),"AR availability UI uses custom cards and project fonts");
                if(scene=="MobileAssemblyAR")Capture(notice,Camera.main,"ar-phone-fallback");
                Assert(scene.Contains("Assembly")?FindAnyObjectByType<MobileAssemblyController>().Ready:FindAnyObjectByType<MobileEngineDisplay>().Ready,"Each AR experience falls back to its matching ready mobile scene");
                Assert(!FindAnyObjectByType<PlayerAssemblyController>(),"Phone fallback has no walking player");notice.DismissNotice();Assert(!notice.NoticeVisible,"Fallback explanation can be dismissed");
            }
            SceneManager.sceneLoaded+=PrepareFake;ExperienceMenu.Launch("MobileAssemblyAR");
            yield return Wait(()=>FindAnyObjectByType<MobileARSession>()?.State==MobileARSession.SessionState.Scanning);
            SceneManager.sceneLoaded-=PrepareFake;
            var session=FindAnyObjectByType<MobileARSession>();var mobile=session.GetComponent<MobileAssemblyController>();var manager=session.GetComponent<AssemblyManager>();var rig=session.GetComponent<SpatialExperienceRig>();
            var cadScale=manager.NextHintPart().transform.lossyScale;
            Assert(!rig.ContentAnchor.gameObject.activeSelf && !session.AllowsInteraction,"Scanning hides content and blocks assembly");
            Assert(!session.GetComponent<AssemblyStepAssist>().Available && !mobile.SelectPart(manager.NextHintPart().PartId),"No part selection or automatic assembly before anchoring");
            tracking.surface=false;session.Place();Assert(session.State==MobileARSession.SessionState.Scanning,"Placement requires a detected real surface");tracking.surface=true;session.Place();yield return null;
            Assert(!FindAnyObjectByType<ARWorkbench>(),"AR creates no table geometry");
            Assert(session.AllowsInteraction && rig.ContentAnchor.parent==session.WorkspaceSurface && session.WorkspaceSurface.parent==tracking.anchor,"Engine is anchored on top of the separately placed workbench");
            Assert(AssemblyPresentationBounds.World(manager.AssemblyRoot,manager).min.y>=session.WorkspaceSurface.position.y+.019f,"Engine rests above the workbench instead of near the ground");
            Assert(manager.TrayFrame.parent==rig.ContentAnchor,"Source parts and socket frames inherit one presentation scale");
            Assert(Mathf.Abs(rig.ContentAnchor.localScale.x-.25f)<.001f,"AR uses a tabletop presentation scale without editing CAD meshes");
            var arrowButton=mobile.GetComponentsInChildren<WorkshopCard>(true).First(c=>c.name=="Quest arrow toggle");arrowButton.clicked();yield return null;yield return null;
            Assert(mobile.QuestArrow.Tracking && mobile.QuestArrow.ArrowVisible && mobile.QuestArrow.PointsToPlacement && mobile.QuestArrow.TargetPart==manager.NextHintPart(),"AR arrow toggle points to the next placement ghost");
            arrowButton.clicked();Assert(!mobile.QuestArrow.Tracking && !mobile.QuestArrow.ArrowVisible,"AR arrow button can turn the pointer off");
            var camera=mobile.ViewCamera;var pose=new Pose(camera.transform.position,camera.transform.rotation);var center=mobile.WorldFocus;
            mobile.PanModel(new Vector2(0,100));center=mobile.WorldFocus;
            var benchPose=new Pose(session.WorkspaceSurface.position,session.WorkspaceSurface.rotation);
            mobile.ZoomModel(1.2f);Assert(Vector3.Distance(center,mobile.WorldFocus)<.001f,"AR pinch keeps the elevated focused point stationary");
            Assert(Mathf.Abs(rig.ContentAnchor.localScale.x-.25f)<.001f && Vector3.Distance(benchPose.position,session.WorkspaceSurface.position)<.001f,"Pinch scales only the active build, never the workbench or shared anchor");
            mobile.RotateEngine(new Vector2(20,8));mobile.PanModel(new Vector2(8,2));
            Assert(Vector3.Distance(camera.transform.position,pose.position)<.001f && Quaternion.Angle(camera.transform.rotation,pose.rotation)<.001f,"Gestures never move the tracked camera");
            Assert(camera.rect==new Rect(0,0,1,1),"AR camera remains full screen behind cards");
            Capture(session.GetComponent<MobileAROverlay>(),camera,"ar-anchored-controls");
            var next=manager.NextHintPart();Assert(mobile.SelectPart(next.PartId),"Anchored engine accepts the next part card");
            Assert(Vector3.Distance(next.transform.lossyScale,cadScale*rig.ContentAnchor.localScale.x*1.2f)<.001f,"Selected source matches the independently zoomed active engine scale");
            tracking.reliable=false;yield return null;Assert(!session.AllowsInteraction && session.State==MobileARSession.SessionState.TrackingLost,"Tracking loss pauses interaction instead of placing in unstable space");
            Assert(!session.GetComponent<AssemblyStepAssist>().Available,"Next-step assistance pauses with tracking");tracking.reliable=true;yield return null;Assert(session.AllowsInteraction,"Tracking recovery restores interaction");
            session.Reposition();Assert(session.State==MobileARSession.SessionState.Scanning && !session.AllowsInteraction,"Reposition returns to surface scanning");
            session.Place();Assert(mobile.SelectedPart==next && next.IsSelected && !next.IsSnapped,"Reposition preserves selected part and assembly progress");
            mobile.ChooseChapter(1);yield return null;yield return null;
            var pistonGroups=manager.Recipe.subAssemblies.Where(g=>manager.FindPart(g.rootPartId).ChapterId==manager.CurrentChapterId).ToArray();
            Assert(pistonGroups.Length==3,"AR piston chapter retains three independent workspaces");
            Assert(pistonGroups.All(g=>Mathf.Abs(mobile.WorkspaceFor(g.id).lossyScale.x-rig.ContentAnchor.lossyScale.x)<.001f),"New chapter workspaces inherit the current AR scale");
            var engineBefore=new Pose(manager.AssemblyRoot.position,manager.AssemblyRoot.rotation);var engineScale=manager.AssemblyRoot.lossyScale;
            var active=mobile.ActiveWorkpiece;var other=pistonGroups.Select(g=>mobile.WorkspaceFor(g.id)).First(s=>s!=active);var otherBefore=new Pose(other.position,other.rotation);var otherScale=other.lossyScale;
            Assert(pistonGroups.All(g=>session.WorkspaceSurface.InverseTransformPoint(mobile.WorkspaceFor(g.id).position).x>0),"All three piston workspaces are beside the main engine on the right");
            mobile.PanModel(new Vector2(0,200));var pistonCenter=mobile.WorldFocus;mobile.RotateEngine(new Vector2(85,30));
            Assert(Vector3.Distance(pistonCenter,mobile.WorldFocus)<.001f,"Piston rotation uses its geometry center instead of an offset CAD edge");
            mobile.ZoomModel(1.3f);mobile.PanModel(new Vector2(10,3));
            Assert(Vector3.Distance(manager.AssemblyRoot.position,engineBefore.position)<.001f && Quaternion.Angle(manager.AssemblyRoot.rotation,engineBefore.rotation)<.001f && Vector3.Distance(manager.AssemblyRoot.lossyScale,engineScale)<.001f,"Piston gestures leave the main engine stationary");
            Assert(Vector3.Distance(other.position,otherBefore.position)<.001f && Quaternion.Angle(other.rotation,otherBefore.rotation)<.001f && Vector3.Distance(other.lossyScale,otherScale)<.001f,"Piston gestures leave other piston workspaces stationary");
            mobile.PanModel(new Vector2(0,-10000));Assert(AssemblyPresentationBounds.World(active,manager).min.y>=session.WorkspaceSurface.position.y+.019f,"Active build cannot be dragged under the workbench");
            mobile.ResetView();Assert(Vector3.Distance(active.lossyScale,rig.ContentAnchor.lossyScale)<.001f,"Reset restores only the active workspace scale");
            var wristPart=manager.NextHintPart();Assert(mobile.SelectPart(wristPart.PartId),"Chapter changes retain usable part cards");
            Assert(session.GetComponent<AssemblyStepAssist>().NextStep() && wristPart.IsSnapped,"AR next step places the part directly at its tracked socket");
            tracking.reliable=false;yield return null;yield return null;
            Assert(!session.GetComponent<AssemblyStepAssist>().NextStep(),"Automatic placement is blocked while tracking is lost");tracking.reliable=true;yield return null;
            Assert(Vector3.Distance(wristPart.transform.position,wristPart.GetTargetPose(wristPart.TargetSocket).position)<.001f,"Tracking recovery preserves the directly placed part at its socket");
            var partCenter=mobile.WorldFocus;mobile.PanModel(new Vector2(0,80));partCenter=mobile.WorldFocus;mobile.RotateEngine(new Vector2(90,40));
            Assert(Vector3.Distance(partCenter,mobile.WorldFocus)<.001f,"Partially built piston rotates about its installed mesh center");
            mobile.ZoomModel(1.25f);
            var assisted=session.GetComponent<AssemblyStepAssist>();int guard=0;
            while(manager.NextHintPart() && manager.MemberGroup(manager.NextHintPart())!=null && guard++<50)
            {Assert(assisted.NextStep(),"Active piston member installs at its own workspace socket");yield return null;yield return null;}
            Assert(pistonGroups.All(manager.GroupComplete),"All three AR piston units can be completed independently");
            Assert(pistonGroups.All(g=>AssemblyPresentationBounds.World(mobile.WorkspaceFor(g.id),manager).min.y>=session.WorkspaceSurface.position.y+.019f),"Growing piston assemblies stay above the tabletop as rods and rings are added");
            var centers=pistonGroups.Select(g=>AssemblyHeldView.WorldBounds(manager.FindPart(g.rootPartId)).center).ToArray();
            Assert(Vector3.Distance(centers[0],centers[1])>.1f && Vector3.Distance(centers[1],centers[2])>.1f,"Completed piston units stay at distinct stations beside the engine");
            Assert(pistonGroups.SelectMany(manager.Members).All(p=>p.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled)),"Completed inactive piston units remain visible while the next unit is built");
            for(int i=0;i<3;i++){Assert(assisted.NextStep(),"Complete piston unit installs from its side station into the engine");yield return null;yield return null;}
            Assert(pistonGroups.All(g=>manager.IsPlaced(g.rootPartId)),"All three completed units install into the engine after independent workspace gestures");
            tracking.FailAs(ARFailure.UnsupportedDevice);
            yield return Wait(()=>!FindAnyObjectByType<MobileARSession>() && FindAnyObjectByType<MobileAROverlay>()?.NoticeVisible==true);
            Assert(FindAnyObjectByType<MobileAROverlay>().NoticeText.Contains("Your device does not support AR"),"Unsupported AR device receives the requested message and playable fallback");
            SceneManager.sceneLoaded+=PrepareFake;ExperienceMenu.Launch("MobileDisplayAR");
            yield return Wait(()=>FindAnyObjectByType<MobileARSession>()?.State==MobileARSession.SessionState.Scanning);SceneManager.sceneLoaded-=PrepareFake;
            session=FindAnyObjectByType<MobileARSession>();var display=session.GetComponent<MobileEngineDisplay>();session.Place();yield return null;
            Assert(AssemblyPresentationBounds.World(display.EngineRoot).min.y>=session.WorkspaceSurface.position.y+.019f,"Display engine is placed at workbench height");
            var before=display.ViewCamera.transform.position;display.ZoomModel(1.1f);display.RotateModel(new Vector2(20,10));display.SetTimeline(.5f);
            Assert(display.VisiblePartCount>0 && display.TimelinePosition==.5f,"AR display retains the timeline and component visuals");
            Assert(Vector3.Distance(display.ViewCamera.transform.position,before)<.001f,"Display gestures preserve camera tracking");tracking.FailAs(ARFailure.CameraDenied);
            yield return Wait(()=>!FindAnyObjectByType<MobileARSession>() && FindAnyObjectByType<MobileAROverlay>()?.NoticeVisible==true);
            Assert(FindAnyObjectByType<MobileAROverlay>().NoticeText.StartsWith("Camera access is required"),"Camera denial has its own explanation rather than an unsupported-device claim");
            Assert(!FindAnyObjectByType<ARTestTrackingProvider>() && !FindAnyObjectByType<VuforiaGroundPlaneProvider>(),"Fallback unloads AR providers and tracked cameras");
#if ENGINE_VUFORIA
            Assert(Vuforia.VuforiaConfiguration.Instance.Vuforia.DelayedInitialization,"Saved SDK configuration uses delayed initialization");
            Assert(UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Any(p=>p.name=="com.ptc.vuforia.engine" && p.version=="11.4.4"),"Integration compiles against installed Vuforia Engine 11.4.4");
            if(string.IsNullOrWhiteSpace(Vuforia.VuforiaConfiguration.Instance.Vuforia.LicenseKey))
            {
                SceneManager.sceneLoaded+=AllowSDKPreview;ExperienceMenu.Launch("MobileDisplayAR");
                yield return null;
                yield return Wait(()=>!FindAnyObjectByType<MobileARSession>() && FindAnyObjectByType<MobileAROverlay>()?.NoticeVisible==true);
                SceneManager.sceneLoaded-=AllowSDKPreview;
                Assert(FindAnyObjectByType<MobileAROverlay>().NoticeText.StartsWith("AR is unavailable right now"),"Actual SDK provider handles a missing licence with configuration fallback");
            }
#endif
            var directory="Tools/Verification/manifest-test/src/main";Directory.CreateDirectory(directory);
            File.WriteAllText(directory+"/AndroidManifest.xml","<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"><application /></manifest>");
            new OptionalARAndroidBuild().OnPostGenerateGradleAndroidProject("Tools/Verification/manifest-test");
            string manifest=File.ReadAllText(directory+"/AndroidManifest.xml");Assert(manifest.Contains("optional") && manifest.Contains("false"),"Android manifest keeps AR hardware optional for fallback users");
        }
#if ENGINE_VUFORIA
        void AllowSDKPreview(Scene scene,LoadSceneMode mode)
        {
            var provider=FindAnyObjectByType<VuforiaGroundPlaneProvider>();if(!provider)return;
            var serialized=new SerializedObject(provider);serialized.FindProperty("allowEditorCameraPreview").boolValue=true;serialized.ApplyModifiedPropertiesWithoutUndo();
        }
#endif
        void Assert(bool value,string description){if(!value)throw new Exception(description);checks.Add(description);}
        void Capture(MobileAROverlay overlay,Camera camera,string name)
        {
            var canvas=overlay.Canvas;var mode=canvas.renderMode;var oldRect=camera.rect;
            var oldTarget=camera.targetTexture;var target=new RenderTexture(1080,1920,24);target.Create();camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=Mathf.Max(.5f,camera.nearClipPlane+.1f);Canvas.ForceUpdateCanvases();
            foreach(var text in canvas.GetComponentsInChildren<TMPro.TextMeshProUGUI>())text.ForceMeshUpdate();
            WorkshopPresentationBuilder.Render(camera,"Tools/Verification/"+name+".png",1080,1920);
            canvas.renderMode=mode;camera.targetTexture=oldTarget;camera.rect=oldRect;target.Release();Destroy(target);Canvas.ForceUpdateCanvases();
        }
    }
}
