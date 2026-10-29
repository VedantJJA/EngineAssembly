using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class ExperienceReworkVerification
    {
        static ExperienceReworkVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch(){AssemblyAudioSetup.Run();SessionState.SetBool("Rework.Run",true);EditorSceneManager.OpenScene("Assets/Scenes/AssemblyWorkshop.unity");EditorApplication.isPlaying=true;}
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("Rework.Run",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Experience rework verification").AddComponent<ExperienceReworkRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("Rework.Run",false);EditorApplication.Exit(SessionState.GetBool("Rework.Passed",false)?0:1);}
        }
    }
    public sealed class ExperienceReworkRunner:MonoBehaviour
    {
        [Serializable] sealed class Report{public bool passed;public string error;public string[] checks;}
        readonly List<string> checks=new List<string>(),errors=new List<string>();AssemblyManager manager;PlayerAssemblyController player;
        void Check(bool value,string text){if(!value)throw new Exception(text);checks.Add(text);}
        void OnEnable()=>Application.logMessageReceived+=Log;void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string text,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception)errors.Add(text+"\n"+stack);}
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";
            while(stack.Count>0){bool more=false;object next=null;try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception e){error=e.ToString();break;}if(!more){stack.Pop();continue;}if(next is IEnumerator nested)stack.Push(nested);else yield return next;}
            Time.timeScale=1;WorkshopGuidance.SetCaptions(true);WorkshopPractice.VerificationEnabled=false;WorkshopSectionCompletion.VerificationEnabled=false;
            if(errors.Count>0)error+="\n"+string.Join("\n",errors);Directory.CreateDirectory("Tools/Verification");File.WriteAllText("Tools/Verification/experience-rework-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,error=error,checks=checks.ToArray()},true));SessionState.SetBool("Rework.Passed",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Load(string scene){if(WorkshopTutorial.IsOpen)WorkshopTutorial.Active.Close(false);yield return SceneManager.LoadSceneAsync(scene);for(int i=0;i<5;i++)yield return null;manager=AssemblyManager.Instance;var ui=manager?manager.GetComponent<AssemblyWorkshopUI>():null;if(ui)ui.SetOpen(false);}
        IEnumerator FinishVoice(NarrationSpeech speech)
        {
            float end=Time.realtimeSinceStartup+8;while(!speech.Source.isPlaying && Time.realtimeSinceStartup<end)yield return null;Check(speech.Source.isPlaying,"Voice starts: "+speech.CueId);speech.Source.time=Mathf.Max(0,speech.Source.clip.length-.05f);yield return new WaitForSecondsRealtime(.2f);
        }
        Ray PlacementRay(AssemblyPart part,Camera camera)
        {
            var guide=part.Manager.GetComponent<AssemblyEasyGuide>();var pose=part.GetTargetPose(part.TargetSocket);var center=pose.position+pose.rotation*Vector3.Scale(part.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(part).center),part.transform.lossyScale);
            var extent=AssemblyMotion.LocalBounds(part).extents;var size=Vector3.Scale(extent,part.transform.lossyScale)*.7f;guide.RefreshTargets();
            for(int i=0;i<144;i++)
            {
                float a=(i%24)*Mathf.PI/12;var origin=center+new Vector3(Mathf.Cos(a)*(i<72?.7f:1.6f),i%72<24?1.2f:i%72<48?.15f:-1.2f,Mathf.Sin(a)*(i<72?.7f:1.6f));
                foreach(var offset in new[]{Vector3.zero,Vector3.right*size.x,Vector3.left*size.x,Vector3.up*size.y,Vector3.down*size.y,Vector3.forward*size.z,Vector3.back*size.z})
                {var ray=new Ray(origin,center+pose.rotation*offset-origin);camera.transform.SetPositionAndRotation(origin,Quaternion.LookRotation(ray.direction));if(guide.Aim(ray,30,camera)==part)return ray;}
            }
            throw new Exception("No visible placement for "+part.PartId);
        }
        void Capture(Camera camera,GameObject owner,string name)
        {
            var canvases=owner.GetComponentsInChildren<Canvas>().Where(c=>c.isRootCanvas).ToArray();var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();var planes=canvases.Select(c=>c.planeDistance).ToArray();var rect=camera.rect;
            try{camera.rect=new Rect(0,0,1,1);foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.4f;}Canvas.ForceUpdateCanvases();WorkshopPresentationBuilder.Render(camera,"Tools/Verification/"+name+".png",1440,900);}
            finally{for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=planes[i];}camera.rect=rect;}
        }
        IEnumerator Scenario()
        {
            yield return null;yield return null;manager=AssemblyManager.Instance;manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);player=FindAnyObjectByType<PlayerAssemblyController>();player.enabled=false;
            WorkshopPractice.VerificationEnabled=true;WorkshopPractice.Ensure(manager);yield return null;yield return null;
            var practice=manager.GetComponent<WorkshopPractice>();Check(practice && practice.Active && manager.CurrentChapterIndex==0,"Assembly starts in actual chapter zero");
            Check(manager.Recipe.Validate().Count==0 && practice.ShapeCount==5,"Chapter zero uses five valid demo shapes");Check(manager.AssemblyParts.Where(p=>p.Definition!=null && p.ChapterId!=WorkshopPractice.ChapterId).All(p=>!p.gameObject.activeSelf),"Engine components remain hidden during practice");
            foreach(var platform in new[]{"PC","Touch","Mobile","AR","VR","Hands"})Check(WorkshopTutorial.AllControlsFor(platform).Length>=3,platform+" has a full controls sequence at the beginning");
            Check(WorkshopTutorial.Active && WorkshopTutorial.Active.IsCompleteGuide,"Practice opens all controls without contextual popups later");Capture(player.ViewCamera,manager.gameObject,"rework-practice-pc");
            var cubes=manager.AssemblyParts.Where(p=>p.PartId.StartsWith("practice-cube")).ToArray();foreach(var part in cubes)player.PickUp(part);Check(player.Inventory.Count==3,"Demo cubes practise repeated pickup");player.DropHeldPart(false);
            Check(cubes.All(p=>!p.Body.isKinematic && p.Body.useGravity) && cubes.Select(p=>p.transform.position).Distinct().Count()==3,"Dropping a stack releases three real physical cubes");
            var start=cubes.Select(p=>p.transform.position.y).ToArray();yield return new WaitForSecondsRealtime(.15f);Check(cubes.Select((p,i)=>p.transform.position.y<start[i]-.015f).All(v=>v),"Dropped cubes fall instead of returning to their supply positions");
            foreach(var part in manager.AssemblyParts.Where(p=>p.ChapterId==WorkshopPractice.ChapterId && !p.Definition.isBase).ToArray())
            {player.PickUp(part);var ray=PlacementRay(part,player.ViewCamera);Check(manager.GetComponent<AssemblyEasyGuide>().Place(ray,30,player.ViewCamera,part),"Practice shape fits its visible target: "+part.PartId);player.DropHeldPart(false);}
            Check(!manager.ChapterComplete,"Finishing shapes waits for the controls sequence");var guideCard=WorkshopTutorial.Active;guideCard.Show(guideCard.LessonCount-1);WorkshopSectionCompletion.VerificationEnabled=true;yield return FinishVoice(guideCard.Speech);
            yield return new WaitForSecondsRealtime(1.8f);Check(!WorkshopTutorial.IsOpen && manager.ChapterComplete,"Final control narration closes after the practice action and pacing pause");yield return new WaitForSecondsRealtime(5.6f);WorkshopSectionCompletion.VerificationEnabled=false;
            Check(manager.CurrentChapterIndex==1 && WorkshopPractice.ChapterNumber(manager)==1 && !practice.Active,"Practice advances to engine chapter one");Check(manager.AssemblyParts.Where(p=>p.ChapterId==WorkshopPractice.ChapterId).All(p=>!p.gameObject.activeSelf),"Practice props disappear from the engine workspace");
            var saved=AssemblyRecipeStore.Parse(JsonUtility.ToJson(manager.Recipe));WorkshopPractice.Strip(saved);Check(saved.parts.All(p=>p.chapterId!=WorkshopPractice.ChapterId) && saved.Validate().Count==0,"Authored recipes exclude temporary demo data");
            yield return DesktopRepair();
            ExperienceMenu.MobileRepairRequested=true;yield return Load("MobileAssembly");var mobile=FindAnyObjectByType<MobileAssemblyController>();while(!mobile.Ready)yield return null;Check(!manager.GetComponent<WorkshopPractice>(),"Repair opens without chapter zero");yield return TouchRepair(mobile);
            ExperienceMenu.MobileRepairRequested=false;yield return Load("MobileAssembly");mobile=FindAnyObjectByType<MobileAssemblyController>();while(!mobile.Ready)yield return null;WorkshopPractice.Ensure(manager);mobile.ChooseChapter(0);yield return null;yield return null;Check(manager.CurrentChapterId==WorkshopPractice.ChapterId,"Phone assembly offers the same demo practice chapter");if(WorkshopTutorial.IsOpen)WorkshopTutorial.Active.Close(false);
            var pistonChapter=manager.Recipe.chapters.FindIndex(c=>c.title.Contains("Pistons"));mobile.ChooseChapter(pistonChapter);yield return null;yield return null;var hint=manager.GetComponent<AssemblyHint>();var source=hint.transform.Find("Next part hint").GetComponent<AssemblyVisual>();
            var piston=manager.Recipe.subAssemblies.First(g=>g.rootPartId.StartsWith("230_Piston"));foreach(var group in manager.Recipe.subAssemblies.Where(g=>g.rootPartId.StartsWith("230_Piston")))foreach(var member in manager.Members(group).OrderBy(p=>p.OrderIndex).ToArray())member.InstallImmediate(member.TargetSocket);yield return null;yield return null;
            Check(mobile.SelectPart(piston.rootPartId),"Completed piston can be selected from the phone hotbar");yield return null;yield return null;
            Check((GameObject)typeof(AssemblyVisual).GetField("visualRoot",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(source)==null,"Phone and AR do not draw a second pickup ghost");
            yield return TrackedARChecks();
            yield return DisplayChecks();
        }
        IEnumerator TrackedARChecks()
        {
            UnityEngine.Events.UnityAction<Scene,LoadSceneMode> setup=(scene,mode)=>{if(scene.name=="MobileAssemblyAR"){var session=FindAnyObjectByType<MobileARSession>();session.Configure(session.gameObject.AddComponent<ARTestTrackingProvider>(),session.FallbackScene);}};
            SceneManager.sceneLoaded+=setup;yield return Load("MobileAssemblyAR");SceneManager.sceneLoaded-=setup;
            var mobile=FindAnyObjectByType<MobileAssemblyController>();while(!mobile.Ready)yield return null;var ar=mobile.GetComponent<MobileARSession>();float deadline=Time.realtimeSinceStartup+5;while(ar.State!=MobileARSession.SessionState.Scanning && Time.realtimeSinceStartup<deadline)yield return null;
            if(WorkshopTutorial.IsOpen)WorkshopTutorial.Active.Close(false);ar.Place();yield return null;yield return null;Check(ar.State==MobileARSession.SessionState.Placed && manager.CurrentChapterId==WorkshopPractice.ChapterId,"Supported AR starts with placed practice shapes");
            mobile.ChooseChapter(manager.Recipe.chapters.FindIndex(c=>c.title.Contains("Pistons")));yield return null;yield return null;
            var groups=manager.Recipe.subAssemblies.Where(g=>g.rootPartId.StartsWith("230_Piston")).ToArray();foreach(var group in groups)foreach(var part in manager.Members(group).OrderBy(p=>p.OrderIndex).ToArray())part.InstallImmediate(part.TargetSocket);
            yield return null;yield return null;Check(mobile.SelectPart(groups[0].rootPartId),"Tracked AR can carry a completed piston from its hotbar");yield return null;yield return null;
            var source=manager.transform.Find("Next part hint").GetComponent<AssemblyVisual>();Check((GameObject)typeof(AssemblyVisual).GetField("visualRoot",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(source)==null,"Tracked AR completed piston has no pickup-position ghost");Capture(mobile.ViewCamera,mobile.gameObject,"rework-ar-piston-ghost");
        }
        IEnumerator DesktopRepair()
        {
            manager.SelectChapter(manager.Recipe.chapters.FindIndex(c=>c.id==PCRepairChapter.ChapterId));yield return null;yield return null;var repair=manager.Repair;var lift=manager.GetComponent<WorkpieceLift>();
            Check(lift.StationCount==2 && lift.TryRepairSurface(out _),"Repair lift exists before the idler is removed");lift.TryRepairSurface(out var surface);var replacement=AssemblyHeldView.WorldBounds(repair.Replacement);
            Check(surface.Contains(new Vector3(replacement.center.x,surface.center.y,replacement.center.z)) && Mathf.Abs(replacement.min.y-surface.max.y)<.02f,"Correct replacement bearing starts on the repair workbench");
            Check(PCRepairChapter.BrokenColour.g>.5f && PCRepairChapter.NewColour.r>.5f,"Broken and replacement indicators use light red and green");Capture(player.ViewCamera,manager.gameObject,"rework-repair-start");
            int safety=0;while(!repair.Complete && safety++<16)
            {
                var part=repair.NextPart;var step=repair.CurrentStep;int before=repair.CompletedSteps;player.PickUp(part);Check(player.CurrentHeldPart==part,"Repair pickup: "+part.PartId);
                if(step.install){var ray=PlacementRay(part,player.ViewCamera);Check(manager.GetComponent<AssemblyEasyGuide>().Place(ray,30,player.ViewCamera,part),"Repair fit: "+part.PartId);player.DropHeldPart(false);}
                else
                {
                    if(part.PartId==PCRepairChapter.UnitId)
                    {
                        var b=AssemblyHeldView.WorldBounds(part);float radius=Mathf.Max(.02f,b.extents.magnitude);var controller=player.GetComponent<CharacterController>();
                        player.ViewCamera.transform.SetPositionAndRotation(surface.center+Vector3.up*.7f-Vector3.forward*(radius+controller.radius+.7f),Quaternion.identity);
                    }
                    player.DropHeldPart(false);if(part.PartId!=PCRepairChapter.UnitId)Check(!part.Body.isKinematic,"Repair removal drops physically: "+part.PartId);
                }
                float end=Time.realtimeSinceStartup+3;while(repair.CompletedSteps==before && Time.realtimeSinceStartup<end)yield return null;
                Check(repair.CompletedSteps==before+1,"Repair advances one step: "+step.partId);
                if(step.partId==PCRepairChapter.UnitId && !step.install){Check(part.Body.isKinematic && lift.CanOperate(1),"Idler docks on the real repair lift for service");Capture(player.ViewCamera,manager.gameObject,"rework-repair-docked");}
            }
            Check(repair.Complete && repair.Replacement.IsSnapped && repair.InventoryPartIds.Contains(PCRepairChapter.FaultyId),"All repair operations finish and retain the broken bearing");
        }
        IEnumerator TouchRepair(MobileAssemblyController mobile)
        {
            var repair=manager.Repair;Check(repair.Active,"Phone repair starts assembled");int safety=0;
            while(!repair.Complete && safety++<16)
            {
                var part=repair.NextPart;var step=repair.CurrentStep;int before=repair.CompletedSteps;
                if(step.install)
                {
                    Check(mobile.SelectPart(part.PartId),"Hotbar selects the correct repair part: "+part.PartId);var ray=PlacementRay(part,mobile.ViewCamera);var screen=mobile.ViewCamera.WorldToScreenPoint(ray.GetPoint(1));Check(mobile.TapTarget(screen),"Touch installs repair part: "+part.PartId);
                }
                else
                {
                    Check(TapRemoval(mobile,part),"Touch removes the visible part into the hotbar: "+part.PartId);
                }
                yield return null;yield return null;Check(repair.CompletedSteps==before+1,"Touch repair advances exactly once");
                if(!step.install)Check(mobile.VisiblePartIds.Contains(part.PartId),"Removed part remains in the hotbar: "+part.PartId);
            }
            Check(repair.Complete && mobile.VisiblePartIds.Contains(PCRepairChapter.FaultyId) && repair.Replacement.IsSnapped,"Phone keeps the broken bearing after fitting its replacement");Capture(mobile.ViewCamera,mobile.gameObject,"rework-repair-phone");
        }
        bool TapRemoval(MobileAssemblyController mobile,AssemblyPart part)
        {
            var query=mobile.GetComponent<AssemblyPickupQuery>()??mobile.gameObject.AddComponent<AssemblyPickupQuery>();var center=AssemblyHeldView.WorldBounds(part).center;var extent=AssemblyHeldView.WorldBounds(part).extents*.7f;
            for(int i=0;i<144;i++)
            {
                float a=(i%24)*Mathf.PI/12;var origin=center+new Vector3(Mathf.Cos(a)*.8f,i%72<24?.7f:i%72<48?.08f:-.7f,Mathf.Sin(a)*.8f);
                foreach(var offset in new[]{Vector3.zero,Vector3.right*extent.x,Vector3.left*extent.x,Vector3.up*extent.y,Vector3.down*extent.y,Vector3.forward*extent.z,Vector3.back*extent.z})
                {
                    var ray=new Ray(origin,center+offset-origin);mobile.ViewCamera.transform.SetPositionAndRotation(origin,Quaternion.LookRotation(ray.direction));
                    if(query.Raycast(ray,30,~0,out var hit,out _,out _) && hit && manager.InteractionPart(hit)==part && mobile.TapTarget(mobile.ViewCamera.WorldToScreenPoint(center+offset)))return true;
                }
            }return false;
        }
        IEnumerator DisplayChecks()
        {
            yield return Load("MobileEngineDisplay");var display=FindAnyObjectByType<MobileEngineDisplay>();while(!display.Ready)yield return null;yield return null;
            Check(!WorkshopTutorial.IsOpen && !display.GetComponentsInChildren<TextMeshProUGUI>(true).Any(t=>t.text=="SECTION 0" || t.text=="SECTION ON" || t.text=="SECTION OFF"),"Display has neither practice nor fixed-section controls");
            WorkshopGuidance.SetCaptions(false);display.SetTimeline(.1f);yield return null;yield return null;Check(display.GetComponentsInChildren<EngineDisplayCaption>(true).All(c=>!c.Text.enabled && !c.Heading.enabled),"Settings can hide display captions while narration remains independent");WorkshopGuidance.SetCaptions(true);
            var steps=EngineLearningContent.Timeline(display.Recipe);int upside=steps.FindIndex(p=>p.orientation==AssemblyOrientation.UpsideDown);display.SetTimeline((upside+.3f)/steps.Count);Check(Quaternion.Angle(display.PresentationOrientation,Quaternion.Euler(180,0,0))<.1f,"Phone timelapse turns the workpiece over when assembly needs it");
            display.SetTimeline(1);display.ExplainEngine();yield return null;var tour=display.Explanation;var speaker=display.GetComponent<NarrationSpeech>();
            while(tour.CurrentCueId!=null && !tour.CurrentCueId.StartsWith("engine-stroke-"))
            {
                if(tour.CurrentCueId.StartsWith("engine-identify-"))Check(display.Operation.HighlightedDescriptionParts.Count>0 && display.Operation.Paused,"Narrator highlights the named stationary parts: "+tour.CurrentCueId);
                yield return FinishVoice(speaker);yield return new WaitForSecondsRealtime(.75f);
            }
            Check(tour.HasIntroduced && display.Operation.HighlightedDescriptionParts.Count==0,"Highlighting stops before the narrated engine strokes begin");tour.Cancel();display.Operation.Paused=true;display.Operation.SetCrankAngle(90);
            foreach(var stroke in new[]{(90f,"Power"),(270f,"Exhaust"),(450f,"Intake"),(630f,"Compression")}){display.Operation.SetCrankAngle(display.Operation.CrankForCycle(stroke.Item1));Check(display.Operation.StrokeName(0)==stroke.Item2,"Narrated stroke matches the actual cylinder phase: "+stroke.Item2);}
            var shell=display.EngineRoot.GetComponentsInChildren<Renderer>(true).First(r=>r.name=="101_Cylinder_Block_01");var block=new MaterialPropertyBlock();shell.GetPropertyBlock(block);Check(block.GetFloat("_SectionEnabled")==0,"Engine motion never uses the broken fixed clipping plane");Capture(display.ViewCamera,display.gameObject,"rework-engine-tour");
            display.SetMode(MobileEngineDisplay.DisplayMode.InfoTimeline);display.SetTimeline(1);yield return null;
            ExperienceMenu.RequestedChapter=PCDisplayChapter.ChapterId;yield return Load("AssemblyWorkshop");manager.SelectChapter(manager.Recipe.chapters.FindIndex(c=>c.id==PCDisplayChapter.ChapterId));var ui=manager.GetComponent<AssemblyWorkshopUI>();ui.SetOpen(false);yield return null;yield return null;ui.Open(AssemblyWorkshopUI.Page.Chapters);Check(!ui.IsOpen && !manager.GetComponent<WorkshopPractice>(),"Display mode cannot open the assembly chapter selector or practice");
        }
    }
}
