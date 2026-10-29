using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class PracticePacingVerification
    {
        static PracticePacingVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch(){AssemblyAudioSetup.Run();SessionState.SetBool("Pacing.Verify",true);EditorSceneManager.OpenScene("Assets/Scenes/AssemblyWorkshop.unity");EditorApplication.isPlaying=true;}
        public static void RunLive()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.isDirty || EditorApplication.isPlaying)throw new InvalidOperationException("Live verification needs a saved scene outside Play mode.");
            SessionState.SetString("Pacing.ReturnScene",scene.path);SessionState.SetBool("Pacing.Live",true);SessionState.SetBool("Pacing.Verify",true);EditorSceneManager.OpenScene("Assets/Scenes/AssemblyWorkshop.unity");EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("Pacing.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Practice pacing verification").AddComponent<PracticePacingRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("Pacing.Verify",false);if(SessionState.GetBool("Pacing.Live",false)){SessionState.SetBool("Pacing.Live",false);EditorApplication.delayCall+=()=>EditorSceneManager.OpenScene(SessionState.GetString("Pacing.ReturnScene","Assets/Scenes/MainMenu.unity"));}else EditorApplication.Exit(SessionState.GetBool("Pacing.Passed",false)?0:1);}
        }
    }
    public sealed class PracticePacingRunner:MonoBehaviour
    {
        [Serializable] sealed class Report{public bool passed;public string error;public string[] checks;}
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
        void OnEnable()=>Application.logMessageReceived+=Log;void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception)errors.Add(message+"\n"+stack);}
        IEnumerator Start()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";
            while(stack.Count>0){bool more=false;object next=null;try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception e){error=e.ToString();break;}if(!more){stack.Pop();continue;}if(next is IEnumerator nested)stack.Push(nested);else yield return next;}
            Time.timeScale=1;WorkshopPractice.VerificationEnabled=false;if(errors.Count>0)error+="\n"+string.Join("\n",errors);
            File.WriteAllText("Tools/Verification/practice-pacing-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,error=error,checks=checks.ToArray()},true));SessionState.SetBool("Pacing.Passed",error.Length==0);EditorApplication.isPlaying=false;
        }
        static void FinishVoice(WorkshopTutorial guide){guide.Speech.Stop();typeof(WorkshopTutorial).GetMethod("NarrationCompleted",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(guide,new object[]{guide.CurrentLessonId});}
        static int Index(WorkshopTutorial guide,string id)=>Array.FindIndex(WorkshopTutorial.AllControlsFor(guide.Platform,true,AssemblyManager.Instance.Difficulty),l=>l.id==id);
        IEnumerator Scenario()
        {
            yield return null;yield return null;var manager=AssemblyManager.Instance;var ui=manager.GetComponent<AssemblyWorkshopUI>();ui.SetOpen(false);manager.SetDifficulty(SnapDifficulty.Easy,false);WorkshopPractice.VerificationEnabled=true;WorkshopPractice.Ensure(manager);yield return null;yield return null;
            var practice=manager.GetComponent<WorkshopPractice>();var guide=WorkshopTutorial.Active;var player=FindAnyObjectByType<PlayerAssemblyController>();
            Check(guide && guide.IsCompleteGuide && practice.Active,"Assembly starts in action-gated practice");
            foreach(var platform in new[]{"PC","Touch","Mobile","AR","VR","Hands"})
            {
                var lessons=WorkshopTutorial.AllControlsFor(platform);Check(lessons.All(l=>l.actions!=null && l.actions.Length>0),platform+" lessons all wait for a practice action");
                Check(lessons.Last().actions.Any(a=>a.id=="place" && a.target==5),platform+" finishes after fitting all five shapes");
            }
            foreach(var difficulty in new[]{SnapDifficulty.Easy,SnapDifficulty.Medium,SnapDifficulty.Hard})
            {
                var lessons=WorkshopTutorial.AllControlsFor("PC",true,difficulty);Check(lessons.Any(l=>l.id=="tutorial-pc-placement-advanced")== (difficulty==SnapDifficulty.Hard),difficulty+" only includes the matching Hard lesson");
                Check(lessons.Any(l=>l.id=="tutorial-pc-placement-medium")== (difficulty==SnapDifficulty.Medium),difficulty+" only includes the matching Medium lesson");
                if(difficulty==SnapDifficulty.Easy)Check(lessons.All(l=>!l.controls.StartsWith("Q ")),"Easy never teaches Q or T placement modes");
            }
            guide.Show(0);FinishVoice(guide);yield return new WaitForSecondsRealtime(2.1f);Check(guide.LessonIndex==0 && !guide.ReadyToContinue,"Narration finishing never advances an untried movement task");guide.Advance();Check(guide.LessonIndex==0,"Next cannot bypass the practice action");
            WorkshopPractice.Record("walk",2);WorkshopPractice.Record("look",45);yield return new WaitForSecondsRealtime(.8f);Check(guide.LessonIndex==0,"A breathing pause follows successful practice");yield return new WaitForSecondsRealtime(1.1f);Check(guide.CurrentLessonId=="tutorial-pc-stance","Successful practice advances automatically after the pause");
            guide.Show(Index(guide,"tutorial-pc-pickup"));var cubes=manager.AssemblyParts.Where(p=>p.PartId.StartsWith("practice-cube")).ToArray();player.PickUp(cubes[0]);Check(!guide.ActionComplete,"Picking up one cube does not complete matching-copy practice");player.PickUp(cubes[1]);Check(guide.ActionComplete && player.Inventory.Count==2,"Two real matching pickups complete the stack task");Check(!guide.ReadyToContinue,"Trying the action early still waits for its narration");
            guide.Show(Index(guide,"tutorial-pc-drop"));FinishVoice(guide);player.DropHeldPart(false);Check(practice.Progress("drop")==2 && cubes.Take(2).All(p=>!p.Body.isKinematic),"Dropping copies records each physical body");Check(!guide.ActionComplete,"Drop lesson waits for the learner to pick a cube back up");player.PickUp(cubes[0]);Check(guide.ActionComplete,"Picking up a dropped cube completes the drop task");
            player.TogglePlacementMode();Check(!player.PlacementMode && practice.Progress("placement-mode")==0,"Easy cannot accidentally enter Q placement mode");
            ui.Open(AssemblyWorkshopUI.Page.Settings);yield return null;Check(WorkshopTutorial.Active==guide && !guide.transform.Find("Guide card").gameObject.activeSelf,"Settings suspends the card without dismissing the lesson");manager.SetDifficulty(SnapDifficulty.Medium,false);ui.SetOpen(false);yield return null;guide.Show(Index(guide,"tutorial-pc-placement-medium"));player.TogglePlacementMode();player.TogglePlacementTool();var rotation=cubes[0].transform.rotation;player.Manipulate(Vector2.one*10,ref rotation);Check(guide.ActionComplete && player.PlacementMode,"Medium practice tracks real Q, T and mouse adjustment actions");
            manager.SetDifficulty(SnapDifficulty.Hard,false);yield return null;Check(guide.CurrentLessonId=="tutorial-pc-placement-advanced","Changing difficulty in practice selects its matching controls");Check(!guide.ActionComplete,"Hard also waits for rotation practice");player.TogglePlacementTool();player.Manipulate(Vector2.one*10,ref rotation);Check(guide.ActionComplete,"Trying both adjustment tools completes Hard controls");
            guide.Show(Index(guide,"tutorial-pc-lift"));var lift=manager.GetComponent<WorkpieceLift>();Check(lift.Move(.15f) && guide.ActionComplete,"Actual lift travel completes height practice");guide.Show(Index(guide,"tutorial-pc-flip"));Check(lift.FlipActive(),"Practice board can be flipped");Check(!guide.ActionComplete,"Flip practice waits for the hatch animation to finish");float timeout=Time.realtimeSinceStartup+12;while(practice.Progress("flip")==0 && Time.realtimeSinceStartup<timeout)yield return null;Check(guide.ActionComplete,"Completed lift animation completes flip practice");
            yield return null;Capture(player.ViewCamera,manager.gameObject,"paced-practice");guide.Close();Check(!practice.ControlsFinished,"Dismissing a guide never silently completes practice");player.DropHeldPart(false);
            manager.SelectChapter(manager.Recipe.chapters.FindIndex(c=>c.id==PCDisplayChapter.ChapterId));ui.SetOpen(false);yield return null;yield return null;yield return null;
            var display=manager.GetComponent<PCDisplayChapter>();var station=display.Station;Check(station && station.ModeSwitch.Available && !station.ExplanationButton.Available,"Display starts with a fixed Info/Motion selector");
            var mode=station.ModeSwitch;var ray=new Ray(mode.AimPosition-station.FaceForward*.65f,station.FaceForward);Check(DisplaySceneControl.RayControl(ray,2)==mode,"A mouse ray directly targets the rendered physical selector");Check(mode.Activate() && display.MotionMode && display.Operation.Active && !display.Operation.Paused,"Physical selector starts engine motion");yield return null;Check(station.ExplanationButton.Available,"Explanation button becomes available in motion mode");
            var explain=station.ExplanationButton;var buttonRay=new Ray(explain.AimPosition-station.FaceForward*.55f,station.FaceForward);Check(DisplaySceneControl.RayControl(buttonRay,2)==explain,"Explanation button is selectable on its visible surface");
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=buttonRay.GetPoint(.2f);blocker.transform.localScale=Vector3.one*.08f;Check(!DisplaySceneControl.RayControl(buttonRay,.56f),"Scenery blocks physical controls through walls");Destroy(blocker);yield return null;
            Check(DisplaySceneControl.Nearest(explain.AimPosition-station.FaceForward*.06f,.1f)==explain,"Quest hands can find the explanation button at close range");Check(explain.Activate() && display.Explanation.Active && display.Operation.Paused,"Physical explanation starts the stationary narrated introduction");var position=station.transform.position;var rotationAtStart=station.transform.rotation;lift.Move(.1f);yield return null;Check(station.transform.position==position && station.transform.rotation==rotationAtStart,"Display console stays fixed when the engine lift moves");
            mode.Activate();yield return null;Check(!display.MotionMode && !display.Operation.Active && !display.Explanation.Active && !explain.Available,"Selecting Info stops motion and hides the explanation button");
            QuestVRSession.Instance.EnableVerification();yield return null;yield return null;var rig=FindAnyObjectByType<QuestVRRig>();rig.enabled=false;
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;var hand=typeof(QuestVRRig).GetField("right",flags).GetValue(rig);var other=typeof(QuestVRRig).GetField("left",flags).GetValue(rig);var type=hand.GetType();type.GetField("tracked").SetValue(hand,true);type.GetField("optical").SetValue(hand,false);var aim=(Transform)type.GetField("aim").GetValue(hand);var grip=(Transform)type.GetField("grip").GetValue(hand);grip.position=ray.origin;aim.SetPositionAndRotation(ray.origin,Quaternion.LookRotation(ray.direction));var buttons=(QuestHandButtons)type.GetField("buttons").GetValue(hand);buttons.Sample(true,false,false,false);buttons.Sample(true,false,false,true);typeof(QuestVRRig).GetMethod("Interact",flags).Invoke(rig,new[]{hand,other});Check(display.MotionMode,"Quest trigger ray operates the physical mode selector");yield return null;
            grip.position=explain.AimPosition-station.FaceForward*.012f;buttons.Sample(true,false,false,false);typeof(QuestVRRig).GetMethod("Interact",flags).Invoke(rig,new[]{hand,other});Check(display.Explanation.Active,"Quest close hand touch starts the explanation");
            Capture(player.ViewCamera,manager.gameObject,"physical-display-console");
            manager.SelectChapter(0);practice.OpenGuide();yield return null;rig.BindUI();var vrGuide=WorkshopTutorial.Active;
            Check(vrGuide.Owner==manager.gameObject && vrGuide.transform.parent!=manager.transform,"Quest keeps tutorial ownership after reparenting its canvas");vrGuide.Close(true);Check(practice.ControlsFinished,"Quest guide completion reaches its original practice chapter");
            practice.OpenGuide();rig.BindUI();manager.SelectChapter(1);Check(!WorkshopTutorial.IsOpen,"Leaving practice closes a reparented Quest guide");
        }
        void Capture(Camera camera,GameObject owner,string name)
        {
            var station=owner.GetComponent<PCDisplayChapter>()?.Station;if(station){var position=station.transform.position-station.transform.forward*1.3f+Vector3.up*1.2f;camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(station.transform.position+Vector3.up*1.05f-position));}
            var target=new RenderTexture(1440,900,24);camera.targetTexture=target;camera.enabled=true;var canvases=station?FindObjectsByType<Canvas>(FindObjectsInactive.Include):owner.GetComponentsInChildren<Canvas>(true);var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();
            var enabled=canvases.Select(c=>c.enabled).ToArray();if(station)foreach(var canvas in canvases)canvas.enabled=false;
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceCamera;canvases[i].worldCamera=camera;canvases[i].planeDistance=.4f;}Canvas.ForceUpdateCanvases();camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1440,900),0,0);texture.Apply();File.WriteAllBytes("Tools/Verification/"+name+".png",texture.EncodeToPNG());
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].enabled=enabled[i];}camera.targetTexture=null;RenderTexture.active=previous;Destroy(texture);target.Release();Destroy(target);
        }
    }
}
