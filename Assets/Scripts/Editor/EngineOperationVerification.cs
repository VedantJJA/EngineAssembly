using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class EngineOperationVerification
    {
        static EngineOperationVerification(){EditorApplication.playModeStateChanged+=Changed;}
        public static void RunBatch(){AssemblyAudioSetup.Run();SessionState.SetBool("EngineOperation.Run",true);EditorSceneManager.OpenScene("Assets/Scenes/MobileEngineDisplay.unity");EditorApplication.isPlaying=true;}
        public static void ExportGeometryBatch(){SessionState.SetBool("EngineOperation.GeometryOnly",true);RunBatch();}
        public static void InspectFastenersBatch(){SessionState.SetBool("EngineOperation.InspectFasteners",true);RunBatch();}
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("EngineOperation.Run",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Operating engine verification").AddComponent<EngineOperationRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("EngineOperation.Run",false);EditorApplication.Exit(SessionState.GetBool("EngineOperation.Passed",false)?0:1);}
        }
    }
    public sealed class EngineOperationRunner:MonoBehaviour
    {
        [Serializable] class Report{public bool passed;public string error;public string[] checks;}
        readonly List<string> checks=new List<string>();
        readonly List<string> runtimeErrors=new List<string>();
        bool inspection;
        void OnEnable()=>Application.logMessageReceived+=Log;
        void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string stack,LogType type){if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert)runtimeErrors.Add(message+"\n"+stack);}
        void Check(bool condition,string text){if(!condition)throw new Exception(text);checks.Add(text);}
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";
            while(stack.Count>0){bool more=false;object next=null;try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception e){error=e.ToString();break;}if(!more){stack.Pop();continue;}if(next is IEnumerator nested)stack.Push(nested);else yield return next;}
            if(runtimeErrors.Count>0)error+="\nRuntime errors: "+string.Join("\n",runtimeErrors);Directory.CreateDirectory("Tools/Verification");File.WriteAllText("Tools/Verification/"+(inspection?"fastener-inspection-report.json":"engine-operation-report.json"),JsonUtility.ToJson(new Report{passed=error.Length==0,error=error,checks=checks.ToArray()},true));SessionState.SetBool("EngineOperation.Passed",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Scenario()
        {
            var display=FindAnyObjectByType<MobileEngineDisplay>();while(!display || !display.Ready){yield return null;display=FindAnyObjectByType<MobileEngineDisplay>();}yield return null;
            yield return VerifyTutorial(display.gameObject,"Mobile");
            Check(Resources.Load<Material>("EngineCycleGas"),"Combustion material is included in Resources");
            foreach(var name in new[]{"EngineAssembly/Surface Triplanar","EngineAssembly/Cycle Gas"})Check(!ShaderUtil.GetShaderMessages(Shader.Find(name)).Any(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),name+" has no shader errors");
            var steps=EngineLearningContent.Timeline(display.Recipe);var filters=display.EngineRoot.GetComponentsInChildren<MeshFilter>(true).Where(m=>display.Recipe.parts.Any(p=>p.id==m.name)).ToArray();
            if(SessionState.GetBool("EngineOperation.InspectFasteners",false))
            {
                inspection=true;SessionState.SetBool("EngineOperation.InspectFasteners",false);display.SetMode(MobileEngineDisplay.DisplayMode.OperatingEngine);display.Operation.Paused=true;display.Operation.SectionEnabled=true;
                foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.enabled=false;
                var camera=display.ViewCamera;camera.rect=new Rect(0,0,1,1);var bounds=new Bounds();bool first=true;foreach(var f in filters){var b=f.GetComponent<Renderer>().bounds;if(first){bounds=b;first=false;}else bounds.Encapsulate(b);}
                foreach(int side in new[]{1,-1})foreach(int angle in new[]{0,90,180,270})
                {
                    camera.transform.position=bounds.center+display.EngineRoot.TransformDirection(new Vector3(side,.18f,-.12f).normalized)*bounds.extents.magnitude*2.5f;camera.transform.LookAt(bounds.center);display.Operation.SetCrankAngle(angle);
                    var name="fasteners-"+side+"-"+angle;WorkshopPresentationBuilder.Render(camera,"Tools/Verification/"+name+".png",1440,900);
                    File.WriteAllText("Tools/Verification/"+name+".json",Newtonsoft.Json.JsonConvert.SerializeObject(filters.Select(m=>{var r=m.GetComponent<Renderer>();var p=camera.WorldToViewportPoint(r.bounds.center);return new {id=m.name,screen=new[]{p.x,p.y,p.z},clipped=display.Operation.IsClipped(r,r.bounds.center),size=r.bounds.size.magnitude};})));
                }
                yield break;
            }
            File.WriteAllText("Tools/Verification/cam-contact-geometry.json",Newtonsoft.Json.JsonConvert.SerializeObject(filters.Where(m=>m.name.StartsWith("103_") || m.name.StartsWith("404_") || m.name.StartsWith("502_") || m.name.StartsWith("405_") || m.name.StartsWith("406_")).Select(m=>new {id=m.name,position=new[]{display.EngineRoot.InverseTransformPoint(m.transform.position).x,display.EngineRoot.InverseTransformPoint(m.transform.position).y,display.EngineRoot.InverseTransformPoint(m.transform.position).z},vertices=m.sharedMesh.vertices.Select(v=>display.EngineRoot.InverseTransformPoint(m.transform.TransformPoint(v))).Select(v=>new[]{v.x,v.y,v.z}).ToArray()})));
            if(SessionState.GetBool("EngineOperation.GeometryOnly",false)){SessionState.SetBool("EngineOperation.GeometryOnly",false);yield break;}
            bool fadedContext=false;
            for(int index=0;index<steps.Count;index++)
            {
                display.SetTimeline((index+.9f)/steps.Count);
                string group=steps[index].subAssemblyId;
                if(!string.IsNullOrEmpty(group))
                {
                    var previous=steps.Take(index+1).Where(p=>p.subAssemblyId==group).Concat(display.Recipe.parts.Where(p=>p.isBase && p.subAssemblyId==group));
                    Check(previous.All(p=>display.IsPartVisible(p.id)),"Earlier subassembly pieces stay visible at "+steps[index].id);
                }
                if(display.CutawayActive){fadedContext=true;var head=filters.First(m=>m.name=="401_Cylinder_Head_01");if(group==display.Recipe.parts.First(p=>p.id==head.name).subAssemblyId)Check(head.GetComponent<Renderer>().enabled,"Cylinder-head housing remains visible through enclosed fittings");}
                Check(display.CurrentStepSeconds>=2.6f,"Insertion has an approach and seated pause for "+steps[index].id);
            }
            Check(fadedContext,"Enclosed fittings use see-through context");
            var easing=typeof(MobileEngineDisplay).GetMethod("InsertionProgress",BindingFlags.Static|BindingFlags.NonPublic);
            Check((float)easing.Invoke(null,new object[]{.15f})==0 && (float)easing.Invoke(null,new object[]{.9f})==1,"Timelapse holds before insertion and after seating");
            display.SetTimeline(1);float overview=display.PresentationRadius;int seal=steps.FindIndex(p=>p.id.StartsWith("403_"));display.SetTimeline((seal+.5f)/steps.Count);
            Check(display.PresentationRadius<overview*.8f,"Small fitting receives a closer contextual view");
            Capture(display,"valve-context",false);
            display.SetTimeline(1);var poses=filters.ToDictionary(m=>m.name,m=>(m.transform.localPosition,m.transform.localRotation,m.transform.localScale,m.sharedMesh));
            display.ToggleEngineAnimation();var op=display.Operation;op.Paused=true;op.SetCrankAngle(0);
            Check(op.Active && op.MovingCylinderCount==3 && op.MovingValveCount==12,"Operating model has three linked pistons and twelve valves");
            Check(!display.EngineRoot.GetComponentsInChildren<MeshCollider>().Any(c=>c.enabled),"Mobile operating display removes moving CAD meshes from continuous physics");
            bool tapped=filters.Where(f=>f.GetComponent<Renderer>().enabled).Any(f=>display.TapPart(display.ViewCamera.WorldToScreenPoint(f.GetComponent<Renderer>().bounds.center)));
            Check(tapped && !string.IsNullOrEmpty(display.SelectedPartId),"Mobile operating display can still inspect an exact visible component by tapping");
            Check(!display.EngineRoot.GetComponentsInChildren<MeshCollider>().Any(c=>c.enabled),"Inspection restores collider suppression after its one-off raycast");
            var crank=filters.First(m=>m.name=="103_Crankshaft_01").transform;var cam=filters.First(m=>m.name.StartsWith("502_")).transform;
            var crankStart=crank.localRotation;var camStart=cam.localRotation;
            op.SetCrankAngle(90);Check(Mathf.Abs(Quaternion.Angle(crankStart,crank.localRotation)-90)<.1f && Mathf.Abs(Quaternion.Angle(camStart,cam.localRotation)-45)<.1f,"Camshafts turn at half crankshaft speed");
            foreach(var pulley in filters.Where(m=>m.name.StartsWith("506_Camshaft")))
            {
                op.SetCrankAngle(0);var bolts=filters.Where(m=>m.name.StartsWith("590_Bolt_ISO_4162_M8_x_35_")).OrderBy(m=>Vector3.Distance(m.GetComponent<Renderer>().bounds.center,pulley.GetComponent<Renderer>().bounds.center)).Take(5).ToArray();
                Check(bolts.Length==5,"Five perimeter screws are present on "+pulley.name);
                var local=bolts.ToDictionary(m=>m.name,m=>(pulley.transform.InverseTransformPoint(m.transform.position),Quaternion.Inverse(pulley.transform.rotation)*m.transform.rotation));
                for(int a=0;a<=720;a+=60)
                {
                    op.SetCrankAngle(a);
                    foreach(var bolt in bolts){var relative=local[bolt.name];Check(Vector3.Distance(pulley.transform.InverseTransformPoint(bolt.transform.position),relative.Item1)<.000001f && Quaternion.Angle(Quaternion.Inverse(pulley.transform.rotation)*bolt.transform.rotation,relative.Item2)<.1f,"Pulley screw stays attached at "+a+" degrees: "+bolt.name);Check(!op.IsClipped(bolt.GetComponent<Renderer>(),bolt.GetComponent<Renderer>().bounds.center),"Rotating pulley screw remains visible: "+bolt.name);}
                }
            }
            foreach(string tag in new[]{"Cyl01","Cyl02","Cyl03"})
            {
                var piston=filters.First(m=>m.name.StartsWith("201_Piston_"+tag));var pin=filters.First(m=>m.name.StartsWith("203_Piston_Wrist_Pin_"+tag));var rod=filters.First(m=>m.name.StartsWith("202_Connecting_Rod_"+tag));
                var pinInitial=display.EngineRoot.InverseTransformPoint(pin.transform.TransformPoint(pin.sharedMesh.bounds.center));var rodInitial=display.EngineRoot.InverseTransformPoint(rod.transform.position);
                // Save source joint points at the original pose, then verify every quarter turn keeps rod length and wrist-pin attachment.
                op.SetCrankAngle(0);var small=display.EngineRoot.InverseTransformPoint(pin.transform.TransformPoint(pin.sharedMesh.bounds.center));var big=display.EngineRoot.InverseTransformPoint(rod.transform.position);big.z=small.z;
                var smallLocal=rod.transform.InverseTransformPoint(display.EngineRoot.TransformPoint(small));float length=Vector3.Distance(small,big);
                for(int angle=0;angle<=720;angle+=45)
                {
                    op.SetCrankAngle(angle);var wrist=pin.transform.TransformPoint(pin.sharedMesh.bounds.center);var rodWrist=rod.transform.TransformPoint(smallLocal);
                    Check(Vector3.Distance(wrist,rodWrist)<.0001f,"Rod stays attached to piston "+tag+" at "+angle+" degrees");
                }
                Check(Vector3.Distance(poses[piston.name].Item1,piston.transform.localPosition)<.0001f,"Piston returns to its initial pose after two revolutions "+tag);
            }
            var valve=filters.First(m=>m.name.StartsWith("404_") && m.name.Contains("Cyl01") && m.name.Contains("Intake"));var spring=filters.First(m=>m.name==display.Recipe.parts.First(p=>p.id.StartsWith("405_") && p.displayName.EndsWith("C1 IN1")).id);var seat=filters.First(m=>m.name.StartsWith("402_"));
            op.SetCrankAngle(0);var valveClosed=valve.transform.position;var springClosed=spring.transform.localScale;var seatStart=seat.transform.position;
            op.SetCrankAngle(450);Check(Vector3.Distance(valveClosed,valve.transform.position)>.01f && Vector3.Distance(springClosed,spring.transform.localScale)>.00001f,"Intake valves open and springs compress during the intake stroke");
            for(int angle=0;angle<720;angle+=15){op.SetCrankAngle(angle);Check(op.MaximumCamContactError<.001f,"Tappets stay in contact with the visible cam profile at "+angle+" degrees");}
            var camBlock=new MaterialPropertyBlock();cam.GetComponent<Renderer>().GetPropertyBlock(camBlock);Check(camBlock.GetFloat("_CamPoseWarp")==1 && camBlock.GetVector("_CamPhaseOffsets").sqrMagnitude>1,"Display cam lobe phases follow the supplied crank layout without editing mesh assets");
            var fixedBolt=filters.First(m=>m.name.StartsWith("490_") && m.name.Contains("Bolt") && display.EngineRoot.InverseTransformPoint(m.GetComponent<Renderer>().bounds.center).x>.2f).GetComponent<Renderer>();fixedBolt.GetPropertyBlock(camBlock);Check(camBlock.GetFloat("_SectionEnabled")==1 && op.IsClipped(fixedBolt,fixedBolt.bounds.center),"Fasteners on the removed half are clipped with their housings");
            Check(!fixedBolt.enabled,"Opened-side fasteners disappear completely instead of leaving floating fragments");Check(op.IsClipped(fixedBolt,display.EngineRoot.TransformPoint(new Vector3(-10,0,0))),"Invisible screw fragments cannot intercept component inspection rays");op.SectionEnabled=false;op.SetCrankAngle(0);Check(fixedBolt.enabled,"Closing the section restores its complete stationary fasteners");op.SectionEnabled=true;
            var gas=display.EngineRoot.Find("Cylinder 1 working gas");op.SetCrankAngle(0);float compressed=gas.localScale.y;op.SetCrankAngle(90);float expanding=gas.localScale.y;op.SetCrankAngle(180);Check(compressed<expanding && expanding<gas.localScale.y,"Power-stroke gas expands continuously above the falling piston");op.SetCrankAngle(540);float intakeVolume=gas.localScale.y;op.SetCrankAngle(630);Check(gas.localScale.y<intakeVolume && gas.localScale.y>compressed,"Compression volume follows the rising piston");
            Check(Vector3.Distance(seatStart,seat.transform.position)<.000001f,"Fixed valve spring seats remain stationary");
            VerifySpringTravel(op,display.EngineRoot);
            foreach(var pipe in filters.Where(m=>m.name.StartsWith("802_Intake_Pipe_") || m.name=="803_External_Service_Tube_01"))Check(!pipe.GetComponent<Renderer>().enabled,"Cutaway hides disconnected intake plumbing: "+pipe.name);
            Check(filters.All(m=>m.sharedMesh==poses[m.name].Item4),"Cross-section leaves every source mesh unchanged");
            var block=filters.First(m=>m.name=="101_Cylinder_Block_01").GetComponent<Renderer>();var property=new MaterialPropertyBlock();block.GetPropertyBlock(property);Check(property.GetFloat("_SectionEnabled")==1,"Cylinder block uses shader clipping");
            Check(display.EngineRoot.GetComponentsInChildren<Renderer>().Count(r=>r.name.EndsWith("working gas"))==3,"Three visible volumes illustrate air, compression, combustion and exhaust");
            op.SetCrankAngle(75);Capture(display,"engine-running-section",true);op.SectionEnabled=false;op.SetCrankAngle(75);block.GetPropertyBlock(property);Check(property.GetFloat("_SectionEnabled")==0,"Exterior view can be restored while the engine runs");
            foreach(var pipe in filters.Where(m=>m.name.StartsWith("802_Intake_Pipe_") || m.name=="803_External_Service_Tube_01"))Check(pipe.GetComponent<Renderer>().enabled,"Exterior view restores intake plumbing: "+pipe.name);
            op.Stop();Check(filters.All(m=>Vector3.Distance(m.transform.localPosition,poses[m.name].Item1)<.00001f && Quaternion.Angle(m.transform.localRotation,poses[m.name].Item2)<.01f && Vector3.Distance(m.transform.localScale,poses[m.name].Item3)<.00001f),"Stopping motion restores all original part transforms");
            display.ToggleEngineAnimation();op.SetSpeed(30);float before=op.CrankAngle;yield return null;yield return null;Check(op.CrankAngle!=before,"Engine advances across real Play-mode frames");op.Paused=true;before=op.CrankAngle;yield return null;yield return null;Check(op.CrankAngle==before,"Pause holds the operating engine still");
            display.SetMode(MobileEngineDisplay.DisplayMode.InfoTimeline);display.SetTimeline(.15f);display.SetMode(MobileEngineDisplay.DisplayMode.OperatingEngine);Check(op.Paused,"Choosing Engine Motion waits for its play control");
            var play=display.GetComponentsInChildren<WorkshopCard>().First(c=>c.name=="PLAY TIMELAPSE card");play.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));yield return null;Check(!op.Paused,"Shared play card starts operating animation");
            display.GetComponentsInChildren<WorkshopCard>(true).First(c=>c.name=="INFO / TIMELINE card").OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));Check(display.Mode==MobileEngineDisplay.DisplayMode.InfoTimeline && !op.Active && Mathf.Abs(display.TimelinePosition-.15f)<.0001f,"Info tab restores the assembly timeline bookmark");
            Check(display.EngineRoot.GetComponentsInChildren<MeshCollider>().Any(c=>c.enabled),"Returning to the timeline restores its visible selection geometry");
            before=display.TimelinePosition;play.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));yield return new WaitForSecondsRealtime(.4f);Check(display.TimelinePlaying && display.TimelinePosition>before,"Shared play card starts and advances assembly timelapse after engine motion");play.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));Check(!display.TimelinePlaying,"Shared play card pauses assembly timelapse");
            Check(display.GetComponentInChildren<EngineDisplayCaption>() && !display.transform.Find("Engine display canvas/Safe area/Timeline dock/Part information card"),"Descriptions use assembly-style floating captions instead of a separate footer section");
            display.SetTimeline(1);display.SelectPart("908_Motor_Unit_GSwirl_01");yield return new WaitForSecondsRealtime(.35f);CaptureUI(display.ViewCamera,"display-info-caption");
            var speaker=display.GetComponent<NarrationSpeech>();display.SetTimeline(.15f);display.SetTimelineAudio(true);display.TogglePlayback();yield return new WaitForSecondsRealtime(.2f);
            Check(speaker.CueId==EngineLearningContent.Detail(steps[Mathf.FloorToInt(display.TimelinePosition*steps.Count)]).voiceId && speaker.CueId.StartsWith("detail-"),"Mobile auto assembly speaks part descriptions, never fitting commands");
            var audioCard=display.GetComponentsInChildren<WorkshopCard>(true).First(c=>c.name=="TIMELAPSE AUDIO card");audioCard.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));yield return null;Check(!display.TimelineAudioEnabled && !speaker.IsBusy,"Mobile audio card stops current and queued timeline speech");display.SetPlaybackSpeed(4);display.SetTimeline((50+.995f)/steps.Count);float silentStart=display.TimelinePosition;yield return new WaitForSecondsRealtime(.4f);Check(display.TimelinePosition>51f/steps.Count && display.TimelinePosition>silentStart,"Silent mobile timeline crosses part boundaries at higher speed");display.TogglePlayback();display.SetPlaybackSpeed(1);display.SetTimelineAudio(true);
            var operationCues=JsonUtility.FromJson<EngineOperationCatalogue>(Resources.Load<TextAsset>("EngineOperationNarration").text).cues;
            Check(operationCues.Length==5 && operationCues.All(c=>Resources.Load<NarrationLibrary>("NarrationLibrary").Find(c.id) && NarrationText.Find(c.id).words?.Length>0),"Engine explanation and all four strokes have generated speech and aligned captions");
            display.ExplainEngine();yield return new WaitForSecondsRealtime(.3f);Check(display.Explanation.Active && speaker.CueId=="engine-operation-introduction" && speaker.Source.isPlaying,"Mobile cycle tour plays its own explanation audio");display.SetTimelineAudio(false);Check(speaker.Source.isPlaying,"Timelapse mute preference does not interrupt the separate engine explanation");display.SetTimelineAudio(true);display.TogglePlay();float audioTime=speaker.Source.time;yield return new WaitForSecondsRealtime(.2f);Check(display.Explanation.Paused && Mathf.Abs(speaker.Source.time-audioTime)<.1f,"Pausing the tour also pauses its narration");display.TogglePlay();
            foreach(var cue in operationCues)
            {
                float timeout=Time.realtimeSinceStartup+4;while((speaker.CueId!=cue.id || !speaker.Source.isPlaying) && Time.realtimeSinceStartup<timeout)yield return null;
                Check(speaker.CueId==cue.id && speaker.Source.isPlaying,"Cycle tour speaks "+cue.id+" in order");speaker.Source.time=speaker.Source.clip.length*.5f;yield return null;yield return null;
                Check(Mathf.Abs(Mathf.DeltaAngle(display.Operation.CrankAngle,Mathf.Lerp(cue.startAngle,cue.endAngle,.5f)))<5,"Cycle movement follows the spoken stroke: "+cue.id);
                speaker.Source.time=speaker.Source.clip.length-.03f;timeout=Time.realtimeSinceStartup+4;while(display.Explanation.CurrentCueId==cue.id && Time.realtimeSinceStartup<timeout)yield return null;
            }
            Check(!display.Explanation.Active && !display.Operation.Paused,"Free engine motion resumes after the spoken cycle tour");display.ExplainEngine();yield return null;display.SetMode(MobileEngineDisplay.DisplayMode.InfoTimeline);Check(!display.Explanation.Active && !speaker.IsBusy,"Leaving the motion tab cancels tour audio");
            Check(!EngineLearningContent.Detail(display.Recipe.parts.First(p=>p.id=="908_Motor_Unit_GSwirl_01")).description.Contains("supplied model"),"Motor description omits the removed model explanation");display.SetMode(MobileEngineDisplay.DisplayMode.OperatingEngine);display.Operation.SetCrankAngle(75);yield return null;CaptureUI(display.ViewCamera,"display-motion-controls");
            yield return SceneManager.LoadSceneAsync("AssemblyWorkshop");yield return null;yield return null;var manager=FindAnyObjectByType<AssemblyManager>();while(manager.Recipe==null)yield return null;var pc=manager.GetComponent<PCDisplayChapter>();pc.EnsureChapter();manager.SelectChapter(manager.Recipe.chapters.FindIndex(c=>c.id==PCDisplayChapter.ChapterId));yield return null;
            Check(manager.GetComponent<AssemblyWorkshopUI>().GetComponentsInChildren<WorkshopCard>(true).Any(c=>c.name=="Section 0 card"),"PC chapter selector includes the Section zero card before the assembly chapters");
            yield return VerifyTutorial(manager.gameObject,"PC");
            pc.ToggleEngineAnimation();Check(pc.Operation.Active && pc.Operation.MovingCylinderCount==3,"PC display uses the same operating engine");
            VerifySpringTravel(pc.Operation,manager.AssemblyRoot);
            pc.Operation.SetCrankAngle(100);pc.SetMotionMode(false);Check(!pc.Operation.Active && pc.Timeline.Active,"PC info tab restores its independent assembly timeline");
            manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);var inspected=manager.FindPart("101_Cylinder_Block_01");var inspectionBounds=AssemblyHeldView.WorldBounds(inspected);Check(pc.Inspect(new Ray(inspectionBounds.center+Vector3.back*4,Vector3.forward)),"PC display can inspect a component independently from the current timeline step");speaker=pc.GetComponent<NarrationSpeech>();string inspectedVoice=speaker.CueId;speaker.Stop();pc.ReplayVoice();
            float replayDeadline=Time.realtimeSinceStartup+3;while(!speaker.Source.isPlaying && speaker.CueId==inspectedVoice && Time.realtimeSinceStartup<replayDeadline)yield return null;
            Check(speaker.CueId==inspectedVoice && speaker.Source.isPlaying,"Replay speaks the inspected part description instead of the previous timeline step; expected="+inspectedVoice+", actual="+speaker.CueId+", clip="+(speaker.Source.clip?speaker.Source.clip.loadState.ToString():"none")+", timeScale="+Time.timeScale);
            manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);pc.Timeline.SetValue(.15f);before=pc.Timeline.Position;pc.TogglePlay();yield return new WaitForSecondsRealtime(.4f);Check(pc.Timeline.Playing && pc.Timeline.Position>before,"PC display can play the assembly timelapse");pc.TogglePlay();Check(!pc.Timeline.Playing && manager.Recipe.parts.All(p=>manager.IsPlaced(p.id)),"PC presentation timeline preserves actual assembly progress");
            speaker=pc.GetComponent<NarrationSpeech>();pc.TogglePlay();yield return new WaitForSecondsRealtime(.2f);Check(speaker.CueId==EngineLearningContent.Detail(pc.Timeline.Current).voiceId && speaker.CueId.StartsWith("detail-"),"PC auto assembly speaks part descriptions without installation commands");pc.SetTimelineAudio(false);yield return null;Check(!speaker.IsBusy,"PC timeline audio toggle stops narration");pc.SetPlaybackSpeed(4);pc.Timeline.SetValue((50+.995f)/pc.Timeline.StepCount);yield return new WaitForSecondsRealtime(.4f);Check(pc.Timeline.Position>51f/pc.Timeline.StepCount,"Silent PC playback advances without narration waits");pc.TogglePlay();pc.SetPlaybackSpeed(1);pc.SetTimelineAudio(true);pc.ExplainEngine();yield return new WaitForSecondsRealtime(.2f);Check(pc.Explanation.Active && speaker.Source.isPlaying,"PC and VR display share the spoken four-stroke tour");
            pc.SetMotionMode(true);Check(pc.Operation.Active && !pc.Timeline.Active,"PC motion tab restores the complete engine before starting its mechanism");manager.SelectChapter(0);Check(!pc.Operation.Active && !pc.Timeline.Active,"Changing PC chapter stops and restores animation before resetting the workshop");
            var loose=manager.AssemblyParts.First(p=>p && p.Definition!=null && p.Definition.chapterId==manager.CurrentChapterId && !p.Definition.isBase && !p.IsSnapped);var tray=loose.transform.position;yield return null;Check(Vector3.Distance(tray,loose.transform.position)<.001f,"Operating view does not overwrite loose tray positions after leaving display");
            QuestVRSession.Instance.EnableVerification();yield return null;manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);manager.SelectChapter(manager.Recipe.chapters.FindIndex(c=>c.id==PCDisplayChapter.ChapterId));yield return null;pc.ToggleEngineAnimation();pc.Operation.Paused=true;pc.Operation.SetCrankAngle(450);yield return null;
            Check(pc.Operation.Active && FindAnyObjectByType<QuestVRRig>() && FindObjectsByType<WorkshopCard>(FindObjectsSortMode.None).Any(c=>c.name=="PLAY ENGINE" && c.GetComponentInParent<Canvas>().renderMode==RenderMode.WorldSpace),"VR display exposes the operating controls on its tracked canvas");
            yield return VerifyTutorial(manager.gameObject,"VR");
            yield return SceneManager.LoadSceneAsync("AssemblyWorkshop");for(int i=0;i<6;i++)yield return null;manager=FindAnyObjectByType<AssemblyManager>();while(manager.Recipe==null)yield return null;pc=manager.GetComponent<PCDisplayChapter>();pc.EnsureChapter();manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);manager.SelectChapter(manager.Recipe.chapters.FindIndex(c=>c.id==PCDisplayChapter.ChapterId));yield return null;pc.ToggleEngineAnimation();
            Check(pc.Operation.Active && manager.GetComponent<AssemblyWorkshopUI>().WorkshopCanvas.renderMode==RenderMode.WorldSpace,"A fresh VR workshop can create display controls after its canvas moves to the headset");Destroy(QuestVRSession.Instance.gameObject);yield return null;
            UnityEngine.Events.UnityAction<Scene,LoadSceneMode> prepare=(scene,mode)=>{if(scene.name!="MobileDisplayAR")return;var session=FindAnyObjectByType<MobileARSession>();session.Configure(session.gameObject.AddComponent<ARTestTrackingProvider>(),session.FallbackScene);};
            SceneManager.sceneLoaded+=prepare;yield return SceneManager.LoadSceneAsync("MobileDisplayAR");SceneManager.sceneLoaded-=prepare;yield return null;yield return null;display=FindAnyObjectByType<MobileEngineDisplay>();while(!display.Ready)yield return null;var ar=display.GetComponent<MobileARSession>();while(ar.State==MobileARSession.SessionState.Starting)yield return null;ar.Place();display.ToggleEngineAnimation();display.Operation.Paused=true;display.Operation.SetCrankAngle(450);yield return null;
            Check(ar.AllowsInteraction && display.Operation.Active && display.Operation.MovingValveCount==12,"Tracked AR display runs the same section and four-stroke animation");
        }
        IEnumerator VerifyTutorial(GameObject owner,string platform)
        {
            var manager=owner.GetComponent<AssemblyManager>();var chapter=manager?manager.CurrentChapterId:null;var count=manager?manager.ChapterInstalledCount:0;
            float timeScale=Time.timeScale;var tutorial=WorkshopTutorial.Open(owner);yield return null;yield return null;
            Check(tutorial.Platform==platform && WorkshopTutorial.IsOpen && Mathf.Approximately(Time.timeScale,timeScale),"Section zero selects "+platform+" controls without pausing the experience");
            Check(!WorkshopTutorial.BlocksInput && WorkshopTutorial.AllowsLocomotion,"Section zero permits practicing movement and world interaction");
            for(int i=0;i<tutorial.LessonCount;i++)
            {
                tutorial.Show(i);yield return null;yield return null;
                var cue=NarrationText.Find(tutorial.CurrentLessonId);Check(cue!=null && cue.words?.Length>0 && Resources.Load<NarrationLibrary>("NarrationLibrary").Find(cue.id),"Tutorial lesson has generated voice and timed captions: "+tutorial.CurrentLessonId);
            }
            tutorial.Show(0);float end=Time.realtimeSinceStartup+3;while(!tutorial.Speech.Source.isPlaying && Time.realtimeSinceStartup<end)yield return null;
            Check(tutorial.Speech.Source.isPlaying && !tutorial.Speech.PlayWhilePaused,"Tutorial narration plays while the workshop remains interactive");tutorial.Close();yield return null;yield return null;
            Check(!WorkshopTutorial.IsOpen && Mathf.Approximately(Time.timeScale,timeScale),"Closing Section zero restores the previous pause state");
            if(manager)Check(manager.CurrentChapterId==chapter && manager.ChapterInstalledCount==count,"Section zero preserves the selected chapter and installed parts");
        }
        void VerifySpringTravel(EngineOperatingAnimation operation,Transform root)
        {
            var list=(System.Collections.IEnumerable)typeof(EngineOperatingAnimation).GetField("valves",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(operation);
            int count=0;
            foreach(var valve in list)
            {
                var type=valve.GetType();var node=type.GetField("spring").GetValue(valve);var nodeType=node.GetType();var spring=(Transform)nodeType.GetField("transform").GetValue(node);
                var stemNode=type.GetField("stem").GetValue(valve);var stem=(Transform)stemNode.GetType().GetField("transform").GetValue(stemNode);
                var direction=(Vector3)type.GetField("direction").GetValue(valve);int axis=(int)type.GetField("springAxis").GetValue(valve);
                var mesh=spring.GetComponent<MeshFilter>().sharedMesh;var a=mesh.bounds.center;var b=a;var vector=Vector3.zero;vector[axis]=1;
                bool increasing=Vector3.Dot(Quaternion.Inverse(root.rotation)*spring.rotation*vector,direction)>0;a[axis]=increasing?mesh.bounds.max[axis]:mesh.bounds.min[axis];b[axis]=increasing?mesh.bounds.min[axis]:mesh.bounds.max[axis];
                operation.SetCrankAngle(0);var seated=root.InverseTransformPoint(spring.TransformPoint(a));var top=root.InverseTransformPoint(spring.TransformPoint(b));var stemStart=root.InverseTransformPoint(stem.position);
                float min=float.MaxValue,max=0,maxSeatError=0,maxTravelError=0;
                for(int angle=0;angle<720;angle+=15)
                {
                    operation.SetCrankAngle(angle);var seatNow=root.InverseTransformPoint(spring.TransformPoint(a));var topNow=root.InverseTransformPoint(spring.TransformPoint(b));
                    maxSeatError=Mathf.Max(maxSeatError,Vector3.Distance(seated,seatNow));maxTravelError=Mathf.Max(maxTravelError,Vector3.Distance(topNow-top,root.InverseTransformPoint(stem.position)-stemStart));
                    float length=Vector3.Distance(topNow,seatNow);min=Mathf.Min(min,length);max=Mathf.Max(max,length);
                }
                Check(maxSeatError<.00001f && maxTravelError<.00001f && min/max<.9f,"Spring visibly shortens with valve travel while its seated end stays fixed: "+spring.name);count++;
            }
            Check(count==12,"All twelve valve springs compress and recover through a complete four-stroke cycle");
        }
        void Capture(MobileEngineDisplay display,string name,bool overview)
        {
            var canvases=FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.enabled).ToArray();foreach(var canvas in canvases)canvas.enabled=false;
            var camera=display.ViewCamera;var rect=camera.rect;var position=camera.transform.position;var rotation=camera.transform.rotation;
            camera.rect=new Rect(0,0,1,1);
            if(overview){var b=new Bounds();bool first=true;foreach(var mesh in display.EngineRoot.GetComponentsInChildren<MeshFilter>()){if(!display.Recipe.parts.Any(p=>p.id==mesh.name))continue;var r=mesh.GetComponent<Renderer>();if(first){b=r.bounds;first=false;}else b.Encapsulate(r.bounds);}camera.transform.position=b.center+display.EngineRoot.TransformDirection(new Vector3(1,.18f,-.12f).normalized)*b.extents.magnitude*2.8f;camera.transform.LookAt(b.center);}
            WorkshopPresentationBuilder.Render(camera,"Tools/Verification/"+name+".png",1440,900);camera.rect=rect;camera.transform.SetPositionAndRotation(position,rotation);foreach(var canvas in canvases)canvas.enabled=true;
        }
        void CaptureUI(Camera camera,string name)
        {
            var canvases=FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas && c.renderMode!=RenderMode.WorldSpace).ToArray();var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();var rect=camera.rect;
            var scalers=canvases.Select(c=>c.GetComponent<UnityEngine.UI.CanvasScaler>()).ToArray();var scales=canvases.Select(c=>c.scaleFactor).ToArray();var target=new RenderTexture(1440,900,24);target.Create();var old=camera.targetTexture;camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);
            for(int i=0;i<canvases.Length;i++){if(scalers[i])scalers[i].enabled=false;canvases[i].scaleFactor=1;canvases[i].renderMode=RenderMode.ScreenSpaceCamera;canvases[i].worldCamera=camera;canvases[i].planeDistance=.5f;}
            Canvas.ForceUpdateCanvases();WorkshopPresentationBuilder.Render(camera,"Tools/Verification/"+name+".png",1440,900);
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].scaleFactor=scales[i];if(scalers[i])scalers[i].enabled=true;}camera.targetTexture=old;camera.rect=rect;target.Release();Destroy(target);Canvas.ForceUpdateCanvases();
        }
    }
}
