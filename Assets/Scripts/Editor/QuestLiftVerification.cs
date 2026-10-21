using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class QuestLiftVerification
    {
        static QuestLiftVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch(){QuestWorkshopSetup.Configure();SessionState.SetBool("QuestLift.Verify",true);EditorSceneManager.OpenScene("Assets/Scenes/AssemblyWorkshop.unity");EditorApplication.isPlaying=true;}
        static void Changed(PlayModeStateChange state){if(!SessionState.GetBool("QuestLift.Verify",false))return;if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Quest and lift verification").AddComponent<QuestLiftVerificationRunner>();if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("QuestLift.Verify",false);EditorApplication.Exit(SessionState.GetBool("QuestLift.Result",false)?0:1);}}
    }
    public sealed class QuestLiftVerificationRunner:MonoBehaviour
    {
        [Serializable] class Report{public bool passed;public string[] checks;public string error;}
        readonly List<string> checks=new List<string>();
        void Check(bool result,string text){if(!result)throw new Exception(text);checks.Add(text);}
        IEnumerator Start(){DontDestroyOnLoad(gameObject);var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";while(stack.Count>0){bool next=false;object value=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();break;}if(!next){stack.Pop();continue;}if(value is IEnumerator nested)stack.Push(nested);else yield return value;}File.WriteAllText("Tools/Verification/quest-lift-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,checks=checks.ToArray(),error=error},true));SessionState.SetBool("QuestLift.Result",error.Length==0);EditorApplication.isPlaying=false;}
        IEnumerator Scenario()
        {
            yield return null;yield return null;yield return null;
            var m=AssemblyManager.Instance;var player=FindAnyObjectByType<PlayerAssemblyController>();player.enabled=false;m.GetComponent<AssemblyWorkshopUI>().Open(AssemblyWorkshopUI.Page.Closed);var lift=m.GetComponent<WorkpieceLift>();
            Check(WorkshopMusicAvailable(),"Downloaded CC0 background music is imported and playing");
            var steel=Resources.Load<Material>("Lift/Steel");Check(steel && steel.GetTexture("_BaseMap") && Resources.Load<Material>("Lift/Void").GetColor("_BaseColor")==Color.black,"Lift uses textured grey steel and an unlit black recess");
            m.SelectChapter(1);yield return null;yield return null;Check(lift.StationCount==4,"Main engine and three subassemblies each have a lift");
            Check(lift.StationDeck(0).localScale.x<1.2f && lift.StationDeck(0).localScale.z<1.2f,"Main working deck is compact without changing engine scale");
            var mainFoot=lift.StationBase(0).GetComponent<Renderer>().bounds.center;
            var mainDeck=lift.StationDeck(0).GetComponent<Renderer>().bounds.center;
            Check(Mathf.Abs(mainFoot.x-mainDeck.x)<.001f && Mathf.Abs(mainFoot.z-mainDeck.z)<.001f,"Engine deck and floor base share one fixed center");
            var firstTray=lift.StationBase(1).parent.Find("Subassembly parts table");
            Check(firstTray && firstTray.gameObject.activeSelf && firstTray.GetComponentsInChildren<Transform>().Count(t=>t.name=="Supply tray leg" && t.localScale.x>=.1f)==4,"Subassembly supply table appears with four substantial legs while parts remain");
            for(int i=1;i<4;i++)Check(Vector3.Dot(lift.StationFrame(i).up,Vector3.down)>.99f,"Piston crown starts facing downward at station "+i);
            var supply=m.GetComponent<WorkshopSupplyLayout>();
            foreach(var member in m.Recipe.parts.Where(p=>p.chapterId==m.CurrentChapterId && !string.IsNullOrEmpty(p.subAssemblyId)))
            {
                var part=m.FindPart(member.id);var group=m.Recipe.subAssemblies.First(g=>g.id==member.subAssemblyId);var root=m.FindPart(group.rootPartId);
                Check(supply.Contains(AssemblyHeldView.WorldBounds(part)) && Vector3.Distance(AssemblyHeldView.WorldBounds(part).center,m.PlannedWorkpieceBounds(root.transform).center)<2,"Subassembly supply is on a nearby reachable tray: "+member.id);
            }
            var pistonGroup=m.Recipe.subAssemblies.First(g=>g.rootPartId==lift.StationFrame(1).name);
            var pistonParts=m.Recipe.parts.Where(p=>p.subAssemblyId==pistonGroup.id).OrderBy(p=>p.order).ToArray();
            lift.MoveStation(1,-100);yield return null;
            var firstPiston=m.FindPart(pistonParts[0].id);firstPiston.InstallImmediate(firstPiston.TargetSocket);yield return null;
            Check(lift.FlipStation(1),"Partially built piston can flip at its lowest lift position");float partialDeadline=Time.realtimeSinceStartup+5;while(lift.IsTransitioning && Time.realtimeSinceStartup<partialDeadline)yield return null;yield return null;
            Check(!lift.IsTransitioning && m.PlannedWorkpieceBounds(lift.StationFrame(1)).min.y>=lift.GroundHeight+.159f,"Flipped partial piston reserves support clearance for its future rod");
            Check(lift.FlipStation(1),"Partial piston can return crown-down");partialDeadline=Time.realtimeSinceStartup+5;while(lift.IsTransitioning && Time.realtimeSinceStartup<partialDeadline)yield return null;
            foreach(var definition in pistonParts){var part=m.FindPart(definition.id);part.InstallImmediate(part.TargetSocket);yield return null;Check(m.WorkpieceBounds(lift.StationFrame(1)).min.y>=lift.StationDeck(1).GetComponent<Renderer>().bounds.max.y-.003f,"Growing piston remains above deck: "+definition.id);}
            lift.MoveStation(1,.76f);yield return null;
            Check(m.Recipe.parts.Where(p=>p.id.StartsWith("305_Flywheel_") || p.id.StartsWith("306_Flywheel_") || p.id.StartsWith("390_Bolt_ISO_4017_M10_x_20_Fasteners_")).All(p=>p.orientation==AssemblyOrientation.Any),"Flywheel, retaining plate and bolts have no flip requirement");
            var fixedPanels=Enumerable.Range(0,lift.StationCount).Select(i=>new Pose(lift.StationPanel(i).position,lift.StationPanel(i).rotation)).ToArray();
            for(int i=0;i<lift.StationCount;i++)
            {
                var planned=m.PlannedWorkpieceBounds(lift.StationFrame(i));var opening=lift.StationOpeningBounds(i);
                Check(planned.size.magnitude<3 && Mathf.Abs(opening.center.x)+opening.extents.x<5.5f && opening.min.z>-5.5f && opening.max.z<2.11f,"Completed fixture remains inside the garage work area "+i+" ("+planned+")");
                Check(planned.min.x>opening.min.x && planned.max.x<opening.max.x && planned.min.z>opening.min.z && planned.max.z<opening.max.z,"Completed workpiece fits entirely inside hatch "+i);
                var panel=lift.StationPanel(i).GetComponentInChildren<Collider>().bounds;var foot=lift.StationBase(i).GetComponent<Renderer>().bounds;
                Check(!panel.Intersects(foot) && lift.StationPanel(i).localScale.x<.8f,"Compact control panel is outside hatch footprint "+i);
                for(int j=0;j<i;j++)
                {var other=lift.StationBase(j).GetComponent<Renderer>().bounds;float gapX=Mathf.Max(foot.min.x-other.max.x,other.min.x-foot.max.x);float gapZ=Mathf.Max(foot.min.z-other.max.z,other.min.z-foot.max.z);Check(Mathf.Max(gapX,gapZ)>.69f,"Station footprints have walking clearance "+j+" / "+i);}
            }
            var pcCamera=new GameObject("PC lever verification camera").AddComponent<Camera>();var pcLever=lift.StationLever(2);var shaft=pcLever.transform.Find("Lever shaft");
            var redGrip=pcLever.transform.Find("Red joystick grip");var gripOrigin=redGrip.position-lift.StationPanel(2).forward*1.5f;
            Check(LiftControlHandle.RayControl(new Ray(gripOrigin,(redGrip.position-gripOrigin).normalized),2)==pcLever,"Crosshair on the visible red joystick grip selects its lever");
            pcCamera.transform.position=shaft.position-lift.StationPanel(2).forward*1.5f;pcCamera.transform.LookAt(shaft.position+lift.StationPanel(2).right*.065f);Physics.SyncTransforms();
            Check(LiftControlHandle.ProcessDesktop(pcCamera,true,true,0,2) && pcLever.Held,"PC cursor can grab the lever from a normal walking distance");
            var pcY=lift.StationFrame(2).position.y;LiftControlHandle.ProcessDesktop(pcCamera,false,true,35,2);yield return new WaitForSeconds(.2f);
            Check(lift.StationFrame(2).position.y>pcY+.03f,"PC hold-and-pull lifts the directly targeted station");LiftControlHandle.ProcessDesktop(pcCamera,false,false,0,2);Check(!pcLever.Held && pcLever.Value==0,"PC release stops and centers the lever");Destroy(pcCamera.gameObject);
            pcCamera=new GameObject("Oblique PC lever camera").AddComponent<Camera>();pcCamera.transform.position=pcLever.AimPosition-lift.StationPanel(2).forward*1.45f+lift.StationPanel(2).right*.55f;pcCamera.transform.LookAt(pcLever.AimPosition+lift.StationPanel(2).up*.12f);Physics.SyncTransforms();
            Check(LiftControlHandle.ProcessDesktop(pcCamera,true,true,0,2) && pcLever.Held,"Oblique PC view can grab the lever with a forgiving hit area");LiftControlHandle.ProcessDesktop(pcCamera,false,false,0,2);Destroy(pcCamera.gameObject);
            lift.Select(0);var mainY=lift.StationFrame(0).position.y;var subY=lift.StationFrame(2).position.y;var direct=lift.StationLever(2);Check(direct.Begin(),"Inactive station lever operates directly without selecting its lift");direct.SetValue(1);yield return new WaitForSeconds(.2f);direct.Release();
            Check(lift.SelectedStation==0 && Mathf.Abs(lift.StationFrame(0).position.y-mainY)<.001f && lift.StationFrame(2).position.y>subY+.05f,"Direct lever moves only its own station and preserves keyboard selection");
            lift.Select(0);var basePosition=lift.StationBase(0).position;var lever=lift.StationLever(0);var initial=lift.ActiveFrame.position.y;
            Check(lever.Begin(),"Physical lift joystick can be held");lever.SetValue(1);yield return new WaitForSeconds(.2f);Check(lift.ActiveFrame.position.y>initial+.05f,"Holding joystick up raises its selected lift");
            lever.Release();float released=lift.ActiveFrame.position.y;yield return new WaitForSeconds(.15f);Check(!lever.Held && lever.Value==0 && Mathf.Abs(lift.ActiveFrame.position.y-released)<.001f,"Releasing the joystick centers it and stops the lift");
            lever.Begin();lever.SetValue(-1);yield return new WaitForSeconds(.2f);lever.Release();Check(lift.ActiveFrame.position.y<released-.05f,"Holding joystick down lowers its selected lift");
            Check(Mathf.Abs(lift.StationBase(0).position.y-basePosition.y)<.001f,"Lift base stays at a fixed height while the assembly moves");
            var sub=lift.StationLever(1);var panelY=lift.StationPanel(1).position.y;lift.Select(1);lift.Move(.2f);yield return null;Check(Mathf.Abs(lift.StationPanel(1).position.y-panelY)<.001f,"Subassembly control panel stays on its ground-mounted stand");
            lift.Select(0);yield return null;Check(lift.StationColor(0).r>.9f && lift.StationColor(1).r<.5f,"Active lift remains white and inactive lifts grey");
            var frame=lift.ActiveFrame;var platform=frame.GetComponent<PickupablePlatform>();var before=m.WorkpieceBounds(frame).center;var rotation=frame.rotation;
            var flip=lift.StationFlip(0);var angledOrigin=flip.AimPosition-lift.StationPanel(0).forward*1.4f+lift.StationPanel(0).right*.5f;
            var angledTarget=flip.AimPosition+lift.StationPanel(0).up*.12f;
            var redButton=flip.GetComponent<Renderer>().bounds.center;var buttonOrigin=redButton-lift.StationPanel(0).forward*1.5f;
            Check(LiftControlHandle.RayControl(new Ray(buttonOrigin,(redButton-buttonOrigin).normalized),2)==flip,"Crosshair on the visible red flip button selects the button");
            Check(LiftControlHandle.RayControl(new Ray(angledOrigin,(angledTarget-angledOrigin).normalized),2)==flip,"Flip button is selectable from an oblique view without pixel-perfect aim");
            pcCamera=new GameObject("PC flip verification camera").AddComponent<Camera>();pcCamera.transform.position=lift.StationFlip(0).transform.position-lift.StationPanel(0).forward*1.5f;pcCamera.transform.LookAt(lift.StationFlip(0).transform.position+lift.StationPanel(0).right*.06f);Physics.SyncTransforms();
            Check(LiftControlHandle.ProcessDesktop(pcCamera,true,true,0,2) && lift.IsTransitioning,"Clicking the flip button begins the hatch sequence");Destroy(pcCamera.gameObject);Check(!platform.IsFlipping,"Hatches open before the engine starts descending");
            yield return new WaitForSeconds(.4f);Check(lift.GateOpening(0)>.99f && platform.Phase==PickupablePlatform.TurnPhase.Lowering,"Grey hatch doors fully open before lowering the engine");
            yield return new WaitForSeconds(.6f);Check(platform.Phase==PickupablePlatform.TurnPhase.Turning && m.WorkpieceBounds(frame).max.y<lift.GroundHeight,"Engine turns entirely beneath the black recess");
            float deadline=Time.realtimeSinceStartup+5;while(lift.IsTransitioning && Time.realtimeSinceStartup<deadline)yield return null;
            Check(!lift.IsTransitioning && lift.GateOpening(0)==0 && Quaternion.Angle(rotation,frame.rotation)>179,"Engine returns, then the hatch closes");
            Check(Vector3.Distance(before,m.WorkpieceBounds(frame).center)<.003f,"Detailed lift flip preserves the geometry center");
            Check(Enumerable.Range(0,lift.StationCount).All(i=>Vector3.Distance(lift.StationPanel(i).position,fixedPanels[i].position)<.001f && Quaternion.Angle(lift.StationPanel(i).rotation,fixedPanels[i].rotation)<.01f),"Every control panel stays fixed through lifting and flipping");
            for(float a=0;a<=90;a+=15)
            {
                var hinges=lift.StationBase(0).parent.GetComponentsInChildren<Transform>().Where(t=>t.name.Contains("inward hatch hinge")).ToArray();
                foreach(var hinge in hinges){hinge.localRotation=Quaternion.Euler(0,0,hinge.name.StartsWith("Left")?-a:a);var door=hinge.GetComponentInChildren<Renderer>().bounds;var foot=lift.StationBase(0).GetComponent<Renderer>().bounds;Check(door.min.x>=foot.min.x && door.max.x<=foot.max.x && door.min.z>=foot.min.z && door.max.z<=foot.max.z && !door.Intersects(lift.StationPanel(0).GetComponentInChildren<Collider>().bounds),"Hatch sweep remains inside base and clear of control panel at "+a+" degrees");}
                foreach(var hinge in hinges)hinge.localRotation=Quaternion.identity;
            }
            var photo=new GameObject("Lift detail camera").AddComponent<Camera>();photo.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();photo.transform.position=before+new Vector3(1.8f,.65f,-2.2f);photo.transform.LookAt(before+Vector3.down*.25f);photo.nearClipPlane=.03f;WorkshopPresentationBuilder.Render(photo,"Tools/Verification/detailed-lift.png",1920,1080);Destroy(photo.gameObject);
            lift.Move(-100);yield return null;yield return null;Check(Mathf.Abs(m.WorkpieceBounds(frame).min.y-lift.GroundHeight-.16f)<.002f && lift.GateOpening(0)==0,"Lowest lift position keeps the workpiece and deck above the closed hatch");
            Check(lift.FlipStation(0),"Lowest-position workpiece opens its hatch and flips");deadline=Time.realtimeSinceStartup+5;while(lift.IsTransitioning && Time.realtimeSinceStartup<deadline)yield return null;yield return null;
            Check(!lift.IsTransitioning && lift.GateOpening(0)==0 && m.WorkpieceBounds(frame).min.y>=lift.GroundHeight+.159f,"Returned workpiece clears the deck and closed hatch");
            lift.Move(.65f);yield return null;yield return null;Check(lift.GateOpening(0)==0,"Hatch closes after the raised workpiece clears it");
            foreach(var chapterId in m.Recipe.subAssemblies.Select(g=>m.FindPart(g.rootPartId).ChapterId).Distinct().ToArray())
            {
                m.SelectChapter(m.Recipe.chapters.FindIndex(c=>c.id==chapterId));yield return null;yield return null;
                foreach(var definition in m.Recipe.parts.Where(p=>p.chapterId==chapterId && !string.IsNullOrEmpty(p.subAssemblyId)))
                {var part=m.FindPart(definition.id);Check(supply.Contains(AssemblyHeldView.WorldBounds(part)),"Part is supported by its local supply tray: "+definition.id);}
                foreach(var member in m.Recipe.parts.Where(p=>p.chapterId==chapterId && !string.IsNullOrEmpty(p.subAssemblyId)).OrderBy(p=>p.order))m.FindPart(member.id).InstallImmediate(m.FindPart(member.id).TargetSocket);
                yield return null;yield return null;
                for(int i=1;i<lift.StationCount;i++)
                {
                    var tray=lift.StationBase(i).parent.Find("Subassembly parts table");
                    Check(tray && !tray.gameObject.activeSelf,"Empty subassembly parts table hides itself: "+lift.StationFrame(i).name);
                    var actual=m.WorkpieceBounds(lift.StationFrame(i));var opening=lift.StationOpeningBounds(i);var planned=m.PlannedWorkpieceBounds(lift.StationFrame(i));
                    Check(Vector3.Distance(actual.center,planned.center)<.01f && Vector3.Distance(actual.size,planned.size)<.01f,"Planned CAD envelope matches completed subassembly "+lift.StationFrame(i).name);
                    Check(actual.min.x>opening.min.x && actual.max.x<opening.max.x && actual.min.z>opening.min.z && actual.max.z<opening.max.z && opening.max.x<5.5f && opening.min.z>-5.5f && opening.max.z<2.11f,"Completed subassembly and hatch fit the clear garage area "+lift.StationFrame(i).name);
                }
            }
            m.SetMode(WorkshopMode.Edit);yield return null;yield return null;
            var mounted=m.Recipe.subAssemblies.Where(g=>m.FindPart(g.rootPartId).ChapterId==m.CurrentChapterId).Select(g=>m.FindPart(g.rootPartId)).ToArray();
            Check(mounted.All(p=>p.IsSnapped && Vector3.Distance(p.transform.position,p.GetTargetPose(p.TargetSocket).position)<.002f),"Lift layout leaves installed editor-preview subassemblies attached at their sockets");
            m.SetMode(WorkshopMode.Assembly);m.SetTask(AssemblyTask.Disassemble);yield return null;yield return null;
            Check(mounted.All(p=>p.IsSnapped && Vector3.Distance(p.transform.position,p.GetTargetPose(p.TargetSocket).position)<.002f),"Lift layout preserves mounted subassemblies when starting disassembly");
            yield return SceneManager.LoadSceneAsync("RepairVRFallback");yield return null;yield return null;yield return null;
            m=AssemblyManager.Instance;Check(m.RepairActive && GameObject.Find("Garage environment"),"VR repair starts with its assembled engine inside the furnished garage");
            Check(!QuestVRSession.Instance.Active,"Normal Windows and phone starts do not initialize XR");
            QuestVRSession.Instance.EnableVerification();yield return null;yield return null;
            var rig=FindAnyObjectByType<QuestVRRig>();Check(rig && rig.HeadCamera && FindAnyObjectByType<PlayerAssemblyController>().enabled==false,"Supported VR replaces desktop walking and the fallback camera");
            Check(FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==2 && FindAnyObjectByType<XRUIInputModule>(),"Both tracked controllers have XR UI rays and input module");
            Check(FindObjectsByType<Canvas>(FindObjectsSortMode.None).All(c=>c.renderMode==RenderMode.WorldSpace && c.GetComponent<TrackedDeviceGraphicRaycaster>()),"VR menus and HUD use ray-navigable world-space canvases");
            Check(rig.CanTeleport(new Vector3(-2,0,-1),Vector3.up) && !rig.CanTeleport(Vector3.zero,Vector3.right) && !rig.CanTeleport(new Vector3(-2,1,-1),Vector3.up),"Teleport accepts clear floor and rejects walls and tables");
            float eye=rig.HeadCamera.transform.localPosition.y;rig.Teleport(new Vector3(-2,0,-1));Check(Mathf.Abs(rig.HeadCamera.transform.position.y-eye)<.01f,"Teleport preserves room-scale head height");
            var p=rig.transform.position;rig.Move(new Vector2(0,1),.2f);Check(Vector3.Distance(p,rig.transform.position)>.1f,"Controller locomotion moves through the workshop with collision");
            Check(QuestVRSession.Instance.Actions.actionMaps.Count==3 && QuestVRSession.Instance.Input("Right/Grip").enabled && QuestVRSession.Instance.Input("Left/Trigger").enabled,"Quest head, grip, trigger, pose and navigation actions are enabled");
            yield return CheckUIRay(rig);
        }
        bool WorkshopMusicAvailable(){var music=FindAnyObjectByType<WorkshopMusic>();return music && music.Source.isPlaying && music.Source.clip.length>60 && music.Source.loop && music.Source.volume<=.13f;}
        IEnumerator CheckUIRay(QuestVRRig rig)
        {
            var ui=AssemblyManager.Instance.GetComponent<AssemblyWorkshopUI>();ui.Open(AssemblyWorkshopUI.Page.Pause);yield return null;yield return null;
            bool clicked=false;var canvas=FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c=>c.name=="Workshop Canvas");
            var go=new GameObject("VR test card",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(WorkshopCard));go.transform.SetParent(canvas.transform,false);var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(200,100);go.GetComponent<WorkshopCard>().clicked=()=>clicked=true;go.transform.SetAsLastSibling();
            InputSystem.RegisterLayout<OculusTouchControllerProfile.OculusTouchController>("VerificationTouch");var device=InputSystem.AddDevice("VerificationTouch");InputSystem.SetDeviceUsage(device,"RightHand");
            var pos=rig.HeadCamera.transform.localPosition+new Vector3(.15f,-.15f,.2f);var direction=rect.position-rig.transform.TransformPoint(pos);var rot=Quaternion.Inverse(rig.transform.rotation)*Quaternion.LookRotation(direction);
            SetButton(device,"isTracked",1);InputSystem.QueueDeltaStateEvent((Vector3Control)device["devicePosition"],pos);InputSystem.QueueDeltaStateEvent((QuaternionControl)device["deviceRotation"],rot);InputSystem.QueueDeltaStateEvent((Vector3Control)device["pointerPosition"],pos);InputSystem.QueueDeltaStateEvent((QuaternionControl)device["pointerRotation"],rot);InputSystem.Update();
            for(int i=0;i<6;i++)yield return null;
            Check(FindObjectsByType<XRRayInteractor>(FindObjectsSortMode.None).Any(r=>r.TryGetCurrentUIRaycastResult(out var hit) && hit.gameObject==go),"Tracked controller ray reaches a real world-space custom card");
            SetButton(device,"triggerPressed",1);yield return null;yield return null;SetButton(device,"triggerPressed",0);for(int i=0;i<6;i++)yield return null;
            Check(clicked,"Quest trigger activates the custom UI card through the XR input module");Destroy(go);ui.Open(AssemblyWorkshopUI.Page.Closed);yield return null;yield return null;
            var lift=AssemblyManager.Instance.GetComponent<WorkpieceLift>();var lever=lift.StationLever(0);var engineY=lift.StationFrame(0).position.y;
            var handlePosition=lever.GrabPosition;PoseDevice(device,rig,handlePosition,Vector3.forward);for(int i=0;i<3;i++)yield return null;
            SetButton(device,"gripPressed",1);yield return null;yield return null;Check(lever.Held,"Quest controller directly grips the physical joystick without a ray click");
            PoseDevice(device,rig,handlePosition+Vector3.up*.12f,Vector3.forward);yield return new WaitForSeconds(.2f);Check(lift.StationFrame(0).position.y>engineY+.05f,"Direct VR joystick follows vertical controller motion");
            SetButton(device,"gripPressed",0);yield return null;yield return null;Check(!lever.Held && lever.Value==0,"Releasing VR grip centers the physical joystick");
            var button=lift.StationFlip(0);PoseDevice(device,rig,button.transform.position-button.transform.parent.forward*.023f,Vector3.forward);yield return null;yield return null;
            Check(lift.IsTransitioning,"Touching the flip button directly begins the VR hatch sequence");
            float finish=Time.realtimeSinceStartup+5;while(lift.IsTransitioning && Time.realtimeSinceStartup<finish)yield return null;yield return null;yield return null;
            Check(!lift.IsTransitioning,"Holding a VR controller at the button does not repeatedly flip the engine");
            // Restore the repair view before its independent pickup test.
            lift.FlipStation(0);finish=Time.realtimeSinceStartup+5;while(lift.IsTransitioning && Time.realtimeSinceStartup<finish)yield return null;
            var part=AssemblyManager.Instance.Repair.NextPart;var center=AssemblyHeldView.WorldBounds(part).center;var query=rig.GetComponent<AssemblyPickupQuery>();Vector3 origin=Vector3.zero;bool found=false;
            foreach(var axis in new[]{Vector3.forward,Vector3.back,Vector3.right,Vector3.left,Vector3.up,new Vector3(1,1,-1).normalized})
            {var candidate=center+axis*1.4f;if(query.Raycast(new Ray(candidate,(center-candidate).normalized),4,~0,out var hitPart,out _,out _) && AssemblyManager.Instance.InteractionPart(hitPart)==part){origin=candidate;found=true;break;}}
            Check(found,"VR controller can aim at the actual exposed repair component");
            pos=rig.transform.InverseTransformPoint(origin);rot=Quaternion.Inverse(rig.transform.rotation)*Quaternion.LookRotation(center-origin);
            InputSystem.QueueDeltaStateEvent((Vector3Control)device["devicePosition"],pos);InputSystem.QueueDeltaStateEvent((QuaternionControl)device["deviceRotation"],rot);InputSystem.QueueDeltaStateEvent((Vector3Control)device["pointerPosition"],pos);InputSystem.QueueDeltaStateEvent((QuaternionControl)device["pointerRotation"],rot);InputSystem.Update();for(int i=0;i<5;i++)yield return null;
            var scale=part.transform.lossyScale;SetButton(device,"gripPressed",1);for(int i=0;i<4;i++)yield return null;
            Check(part.IsSelected && Vector3.Distance(scale,part.transform.lossyScale)<.001f,"Quest grip picks up the actual component without changing its scale");
            var heldPosition=part.transform.position;SetButton(device,"isTracked",0);yield return null;yield return null;
            Check(part.IsSelected && part.gameObject.activeInHierarchy && Vector3.Distance(heldPosition,part.transform.position)<.001f,"Controller tracking loss freezes the held component without dropping it");
            SetButton(device,"isTracked",1);yield return null;SetButton(device,"gripPressed",0);for(int i=0;i<4;i++)yield return null;
            Check(!part.IsSelected && AssemblyManager.Instance.Repair.CompletedSteps==1,"Releasing Quest grip sets aside the repair component and advances the step");InputSystem.RemoveDevice(device);
        }
        static void SetButton(UnityEngine.InputSystem.InputDevice device,string name,float value)
        {using(StateEvent.From(device,out var ev)){((ButtonControl)device[name]).WriteValueIntoEvent(value,ev);InputSystem.QueueEvent(ev);}InputSystem.Update();}
        static void PoseDevice(UnityEngine.InputSystem.InputDevice device,QuestVRRig rig,Vector3 world,Vector3 direction)
        {
            var position=rig.transform.InverseTransformPoint(world);var rotation=Quaternion.Inverse(rig.transform.rotation)*Quaternion.LookRotation(direction);
            InputSystem.QueueDeltaStateEvent((Vector3Control)device["devicePosition"],position);InputSystem.QueueDeltaStateEvent((QuaternionControl)device["deviceRotation"],rotation);
            InputSystem.QueueDeltaStateEvent((Vector3Control)device["pointerPosition"],position);InputSystem.QueueDeltaStateEvent((QuaternionControl)device["pointerRotation"],rotation);InputSystem.Update();
        }
    }
}

