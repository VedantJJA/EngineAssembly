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
    [InitializeOnLoad] public static class SpatialWorkshopVerification
    {
        static SpatialWorkshopVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch()
        {
            if(!Application.isBatchMode)throw new Exception("Use the isolated verification project.");
            SpatialFallbackSceneBuilder.Create();
            SessionState.SetBool("SpatialWorkshop.Verify",true);
            EditorSceneManager.OpenScene(AssemblyWorkshopBuilder.ScenePath,OpenSceneMode.Single);EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("SpatialWorkshop.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Spatial workshop checks").AddComponent<SpatialWorkshopVerificationRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("SpatialWorkshop.Verify",false);EditorApplication.Exit(SessionState.GetBool("SpatialWorkshop.Result",false)?0:1);}
        }
    }
    public sealed class SpatialWorkshopVerificationRunner:MonoBehaviour
    {
        [Serializable] class Report{public bool passed;public string[] checks;public string error;}
        readonly List<string> checks=new List<string>();
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";
            while(stack.Count>0)
            {
                bool more=false;object step=null;try{more=stack.Peek().MoveNext();if(more)step=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();break;}
                if(!more){stack.Pop();continue;}if(step is IEnumerator nested){stack.Push(nested);continue;}yield return step;
            }
            File.WriteAllText("Tools/Verification/spatial-workshop-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,checks=checks.ToArray(),error=error},true));
            SessionState.SetBool("SpatialWorkshop.Result",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Scenario()
        {
            yield return null;yield return null;yield return null;
            var manager=AssemblyManager.Instance;var ui=manager.GetComponent<AssemblyWorkshopUI>();ui.SetOpen(false);
            var player=FindAnyObjectByType<PlayerAssemblyController>();player.enabled=false;
            var assist=manager.GetComponent<AssemblyStepAssist>();var lift=manager.GetComponent<WorkpieceLift>();
            Assert(assist && lift,"PC exposes lift and direct assembly assistance");
            var frame=manager.AssemblyRoot;var part=manager.NextHintPart();var target=part.GetTargetPose(part.TargetSocket);
            float initialY=frame.position.y;
            Assert(lift.Move(.2f) && Mathf.Abs(frame.position.y-initialY-.2f)<.001f,"Lift raises the engine at original CAD scale");
            Assert(Vector3.Distance(part.GetTargetPose(part.TargetSocket).position,target.position+Vector3.up*.2f)<.001f,"Engine ghost follows lift movement exactly");
            lift.Move(-.2f);var homeRotation=frame.rotation;frame.rotation=Quaternion.Euler(47,29,113);
            Assert(manager.CanInstall(part,part.TargetSocket,out _),"Engine may be assembled in an arbitrary orientation");
            frame.rotation=homeRotation;
            var foot=lift.StationBase(0);float footHeight=foot.position.y;var footSize=foot.localScale;
            lift.Move(-100);yield return null;
            Assert(Mathf.Abs(manager.WorkpieceBounds(frame).min.y-lift.GroundHeight)<.002f,"Main lift lowers model geometry all the way to the ground");
            Assert(Mathf.Abs(foot.position.y-footHeight)<.001f && foot.localScale==footSize,"Lift base height and thickness remain fixed while the deck moves");
            lift.Move(1.5f);
            Assert(assist.NextStep() && !part.IsBusy && part.IsSnapped,"PC Next Step installs immediately without a travel animation");
            Assert(Vector3.Distance(part.transform.position,part.GetTargetPose(part.TargetSocket).position)<.001f,"Direct placement preserves the exact CAD socket pose");
            lift.Move(.12f);yield return WaitForPart(part);
            Assert(part.IsSnapped && Vector3.Distance(part.transform.position,part.GetTargetPose(part.TargetSocket).position)<.001f,"Placed part follows the moved socket with normal installation state");
            manager.SelectChapter(1);yield return null;yield return null;
            Assert(lift.StationCount==4 && lift.StationFrame(0)==manager.AssemblyRoot,"Main engine and three pistons have separate selectable lifts");
            lift.Select(0);yield return null;
            Assert(lift.StationColor(0).r>.9f && lift.StationColor(1).r<.5f,"Selected lift is white and inactive lifts are grey");
            lift.Cycle(1);yield return null;
            var engineY=manager.AssemblyRoot.position.y;var pistonFrame=lift.ActiveFrame;float pistonY=pistonFrame.position.y;
            Assert(pistonFrame!=manager.AssemblyRoot && lift.Move(.1f) && Mathf.Abs(manager.AssemblyRoot.position.y-engineY)<.001f && pistonFrame.position.y>pistonY,"Right Arrow selects an independently moving subassembly lift");
            lift.Cycle(-1);Assert(lift.ActiveFrame==manager.AssemblyRoot,"Left Arrow switches back to the main-engine lift");
            var arrow=ui.GetComponentInChildren<WorkshopQuestArrow>(true);part=manager.NextHintPart();
            var centre=part.transform.TransformPoint(AssemblyMotion.LocalBounds(part).center);var camera=player.ViewCamera;
            var oldCameraPosition=camera.transform.position;var oldCameraRotation=camera.transform.rotation;
            var axis=part.transform.rotation*AssemblyMotion.ShapeAxis(part,false);
            camera.transform.position=centre+Vector3.Cross(axis,Mathf.Abs(Vector3.Dot(axis,Vector3.up))>.9f?Vector3.right:Vector3.up).normalized*3;camera.transform.rotation=Quaternion.LookRotation(centre-camera.transform.position,axis);arrow.Toggle(true);yield return null;yield return null;
            Assert(arrow.ArrowVisible && arrow.TargetOnScreen && Mathf.Abs(arrow.ArrowDirection.y)>.99f,"V hovers vertically over an onscreen part");
            camera.transform.Rotate(0,0,90);yield return null;yield return null;
            Assert(arrow.ArrowVisible && Mathf.Abs(arrow.ArrowDirection.x)>.99f,"Onscreen V marker can point horizontally");
            player.PickUp(part);yield return null;yield return null;
            var destination=AssemblyMotion.TargetCenter(part);camera.transform.position=destination+Vector3.forward*3;camera.transform.LookAt(destination);yield return null;yield return null;
            Assert(arrow.PointsToPlacement && arrow.TargetPart==part && arrow.TargetOnScreen,"Picking up a part switches the hovering pointer to its placement position");
            player.DropHeldPart(false);camera.transform.rotation=Quaternion.LookRotation(camera.transform.position-destination);yield return null;yield return null;
            Assert(arrow.ArrowVisible && !arrow.TargetOnScreen,"Offscreen target retains its screen-border pointer");
            arrow.Toggle(false);camera.transform.SetPositionAndRotation(oldCameraPosition,oldCameraRotation);
            manager.SelectChapter(3);yield return null;yield return null;
            var head=manager.NextHintPart();frame=manager.FlipFrame();var before=manager.WorkpieceBounds(frame).center;
            Assert(head.PartId=="401_Cylinder_Head_01" && Vector3.Distance(before,frame.position)>.05f,"Empty head fixture uses geometry centre instead of the CAD edge origin");
            var platform=frame.GetComponent<PickupablePlatform>();var originalRotation=frame.rotation;
            manager.FlipWorkpiece();Assert(platform.Phase==PickupablePlatform.TurnPhase.Lowering,"Flip starts by lowering the selected workpiece");
            yield return new WaitForSeconds(.61f);
            Assert(platform.Phase==PickupablePlatform.TurnPhase.Turning && manager.WorkpieceBounds(frame).max.y<lift.GroundHeight,"Rotation happens only after the complete workpiece is underground");
            Assert(platform.GetComponentsInChildren<Collider>(true).All(c=>!c.enabled),"Workpiece colliders are disabled throughout the underground flip");
            yield return new WaitForSeconds(.7f);
            Assert(platform.Phase==PickupablePlatform.TurnPhase.Raising,"Turned workpiece rises back out of the ground");
            yield return WaitForFlip(platform);
            Assert(Quaternion.Angle(originalRotation,frame.rotation)>179 && !platform.IsFlipping,"Flip completes a half turn before restoring interaction");
            Assert(Vector3.Distance(manager.WorkpieceBounds(frame).center,before)<.002f,"Cylinder head flip preserves the workpiece centre");
            target=head.GetTargetPose(head.TargetSocket);initialY=frame.position.y;lift.Move(.15f);
            Assert(frame==lift.ActiveFrame && frame.position.y>initialY,"Lift follows the current bench subassembly");
            Assert(Vector3.Distance(head.GetTargetPose(head.TargetSocket).position,target.position+(frame.position.y-initialY)*Vector3.up)<.002f,"Head ghost follows its fixture lift");
            manager.FlipWorkpiece();yield return WaitForFlip(platform);
            Assert(assist.NextStep(),"Head base supports animated assistance");yield return WaitForPart(head);
            var colliderStates=platform.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>c.enabled);
            manager.FlipWorkpiece();yield return WaitForFlip(platform);
            Assert(colliderStates.All(pair=>pair.Key.enabled==pair.Value),"Installed workpiece collider states are restored after flipping");
            var valve=manager.NextHintPart();
            Assert(manager.CanInstall(valve,valve.TargetSocket,out _),"Valve installation has no upright or upside-down restriction");
            ui.Open(AssemblyWorkshopUI.Page.Pause);
            Assert(ui.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t=>t.text=="NEXT STEP · AUTO INSTALL"),"PC pause menu has a clickable Next Step card");ui.SetOpen(false);
            manager.SetMode(WorkshopMode.Edit);Assert(!assist.NextStep(),"Assistance cannot mutate editor preview state");

            yield return SceneManager.LoadSceneAsync("MobileAssemblyARFallback");yield return null;yield return null;yield return null;
            manager=AssemblyManager.Instance;var mobile=FindAnyObjectByType<MobileAssemblyController>();assist=manager.GetComponent<AssemblyStepAssist>();
            Assert(!FindAnyObjectByType<PlayerAssemblyController>(),"Mobile assembly fallback has no player locomotion");
            Assert(mobile.GetComponentsInChildren<WorkshopCard>().Any(c=>c.name=="Next Step"),"Mobile provides a rounded Next Step touch card");
            var rig=manager.GetComponent<SpatialExperienceRig>();
            Assert(rig && manager.AssemblyRoot.IsChildOf(rig.ContentAnchor),"Mobile model is isolated below a spatial content anchor");
            Assert(mobile.ViewCamera.backgroundColor.r>.9f,"Mobile assembly uses the light theme");
            Capture(mobile.GetComponentInChildren<Canvas>(),mobile.ViewCamera,"mobile-assembly-light");
            Capture(mobile.GetComponentInChildren<Canvas>(),mobile.ViewCamera,"mobile-assembly-light-portrait",1080,1920);
            mobile.ChooseChapter(1);yield return null;
            var firstWorkspace=mobile.ActiveWorkpiece;
            Assert(firstWorkspace.IsChildOf(rig.ContentAnchor),"Each piston workspace belongs to the future AR anchor");
            var stages=manager.Recipe.subAssemblies.Where(g=>manager.FindPart(g.rootPartId).ChapterId==manager.CurrentChapterId).Select(g=>mobile.WorkspaceFor(g.id)).ToArray();
            Assert(stages.All(s=>Vector3.Distance(s.position,stages[1].position)<3),"Piston workspaces remain within the spatial content area rather than metres offscreen");
            mobile.RotateEngine(new Vector2(129,280));part=manager.NextHintPart();
            Assert(assist.NextStep() && part.IsSnapped && !part.IsBusy,"Mobile Next Step installs immediately in the rotated workspace");
            yield return WaitForPart(part);
            Assert(part.IsSnapped && !mobile.VisiblePartIds.Contains(part.PartId),"Mobile removes the card after direct placement");
            foreach(var d in manager.Recipe.parts.Where(d=>d.chapterId==manager.CurrentChapterId && !string.IsNullOrEmpty(d.subAssemblyId)).OrderBy(d=>d.order))
            {
                var member=manager.FindPart(d.id);if(!member.IsSnapped)member.InstallImmediate(member.TargetSocket);
            }
            yield return null;yield return null;
            part=manager.NextHintPart();var pistonGroup=manager.CarrierDefinition(part.PartId);
            Assert(pistonGroup!=null && assist.NextStep(),"Next Step can install a completed piston subassembly");
            yield return WaitForPart(part);
            Assert(part.IsSnapped && manager.Members(pistonGroup).All(p=>p.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled)),"The complete piston and all members are visible at the engine socket");
            mobile.ChooseChapter(0);yield return null;yield return null;part=manager.NextHintPart();
            Assert(mobile.SelectPart(part.PartId),"Mobile manual placement still requires selecting a part card first");
            var guide=manager.GetComponent<AssemblyEasyGuide>();var viewport=mobile.ViewCamera.pixelRect;Vector2 tap=default;bool found=false;
            for(int y=1;y<24 && !found;y++)for(int x=1;x<24 && !found;x++)
            {var point=new Vector2(viewport.xMin+viewport.width*x/24,viewport.yMin+viewport.height*y/24);if(guide.Aim(mobile.ViewCamera.ScreenPointToRay(point),100,mobile.ViewCamera)==part){tap=point;found=true;}}
            Assert(found && mobile.TapTarget(tap) && part.IsSnapped && !part.IsBusy,"A visible mobile ghost installs directly after a manual tap");
            yield return WaitForPart(part);
            Assert(!mobile.VisiblePartIds.Contains(part.PartId),"Manual placement removes its installed card");
            var trackingCamera=new GameObject("Test tracked camera").AddComponent<Camera>();trackingCamera.transform.SetPositionAndRotation(new Vector3(2,3,-4),Quaternion.Euler(7,11,0));
            var trackedPose=new Pose(trackingCamera.transform.position,trackingCamera.transform.rotation);
            rig.BeginTrackedSession(trackingCamera,new Pose(new Vector3(3,0,1),Quaternion.Euler(0,25,0)));yield return null;
            mobile.ResetView();yield return null;
            Assert(mobile.ViewCamera==trackingCamera && Vector3.Distance(trackingCamera.transform.position,trackedPose.position)<.001f && Quaternion.Angle(trackingCamera.transform.rotation,trackedPose.rotation)<.001f,"AR camera-provider hook preserves externally tracked camera poses");
            rig.EndTrackedSession();Destroy(trackingCamera.gameObject);

            yield return SceneManager.LoadSceneAsync("MobileDisplayARFallback");yield return null;yield return null;
            var display=FindAnyObjectByType<MobileEngineDisplay>();rig=display.GetComponent<SpatialExperienceRig>();
            Assert(display.Ready && display.EngineRoot.IsChildOf(rig.ContentAnchor),"Display fallback uses the same spatial anchor boundary");
            Assert(display.ViewCamera.backgroundColor.r>.9f && display.GetComponentsInChildren<WorkshopCard>().All(c=>c.GetComponent<UnityEngine.UI.Image>().sprite),"Display uses light colours and rounded touch controls");
            Capture(display.GetComponentInChildren<Canvas>(),display.ViewCamera,"mobile-display-light");
            Capture(display.GetComponentInChildren<Canvas>(),display.ViewCamera,"mobile-display-light-portrait",1080,1920);
            display.SetTimeline(.45f);Assert(display.VisiblePartCount>0,"AR-ready display retains the assembly timeline");
            rig.SetAnchorPose(new Pose(new Vector3(4,0,2),Quaternion.Euler(0,30,0)));display.ResetView();
            Assert(Vector3.Distance(display.EngineRoot.position,rig.ContentAnchor.position)<.001f,"Reset view preserves a relocated spatial anchor");

            yield return SceneManager.LoadSceneAsync("RepairVRFallback");yield return null;yield return null;yield return null;yield return null;
            manager=AssemblyManager.Instance;
            Assert(manager.GetComponent<RepairVRFallback>().Ready && manager.RepairActive,"VR fallback starts the bonus repair scenario");
            Assert(manager.Repair.Replacement && manager.Repair.StepCount==14,"VR fallback contains the separate replacement and full 14-step repair");
            Assert(manager.GetComponent<SpatialExperienceRig>() && manager.AssemblyRoot.IsChildOf(manager.GetComponent<SpatialExperienceRig>().ContentAnchor),"Repair separates spatial content from fallback locomotion");
            Assert(!manager.GetComponent<AssemblyWorkshopUI>().IsOpen && Time.timeScale==1,"Repair scene opens playable with instructions");
        }
        void Assert(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
        IEnumerator WaitForPart(AssemblyPart part){float deadline=Time.time+20;while(part.IsBusy && Time.time<deadline)yield return null;Assert(!part.IsBusy && part.IsSnapped,"Component animation finishes installed within its timeout");yield return null;yield return null;}
        IEnumerator WaitForFlip(PickupablePlatform platform){float deadline=Time.time+4;while(platform.IsFlipping && Time.time<deadline)yield return null;Assert(!platform.IsFlipping,"Lift animation finishes within its timeout");}
        void Capture(Canvas canvas,Camera camera,string name,int width=1440,int height=900)
        {
            var mode=canvas.renderMode;var rect=camera.rect;camera.rect=new Rect(0,0,1,1);
            var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();var resolution=scaler.referenceResolution;scaler.referenceResolution=new Vector2(width,height);
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.5f;
            var target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;Canvas.ForceUpdateCanvases();
            WorkshopPresentationBuilder.Render(camera,"Tools/Verification/"+name+".png",width,height);camera.targetTexture=null;target.Release();Destroy(target);canvas.renderMode=mode;camera.rect=rect;scaler.referenceResolution=resolution;
        }
    }
}
