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
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class TeachingGuidanceVerification
    {
        static readonly string[] keys={WorkshopGuidance.HintsKey,WorkshopGuidance.ArrowKey,"Assembly.SnapDifficulty","Assembly.EasyDefaultVersion"};
        static TeachingGuidanceVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch()
        {
            AssemblyAudioSetup.Run();
            foreach(var key in keys){SessionState.SetBool("Teaching.Exists."+key,PlayerPrefs.HasKey(key));SessionState.SetInt("Teaching.Pref."+key,PlayerPrefs.GetInt(key));PlayerPrefs.DeleteKey(key);}
            SessionState.SetBool("Teaching.Run",true);EditorSceneManager.OpenScene("Assets/Scenes/AssemblyWorkshop.unity");EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("Teaching.Run",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Movement and contextual teaching checks").AddComponent<TeachingGuidanceRunner>();
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                foreach(var key in keys)if(SessionState.GetBool("Teaching.Exists."+key,false))PlayerPrefs.SetInt(key,SessionState.GetInt("Teaching.Pref."+key,0));else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();SessionState.SetBool("Teaching.Run",false);EditorApplication.Exit(SessionState.GetBool("Teaching.Passed",false)?0:1);
            }
        }
    }
    public sealed class TeachingGuidanceRunner:MonoBehaviour
    {
        [Serializable] sealed class Report{public bool passed;public string error;public string[] checks;}
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        void Check(bool condition,string message){if(!condition)throw new Exception(message);checks.Add(message);}
        void OnEnable()=>Application.logMessageReceived+=Log;void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception)errors.Add(message+"\n"+stack);}
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);var work=new Stack<IEnumerator>();work.Push(Scenario());string error="";
            while(work.Count>0){bool more=false;object next=null;try{more=work.Peek().MoveNext();if(more)next=work.Peek().Current;}catch(Exception e){error=e.ToString();break;}if(!more){work.Pop();continue;}if(next is IEnumerator nested)work.Push(nested);else yield return next;}
            WorkshopTeachingDirector.VerificationEnabled=false;if(WorkshopTutorial.IsOpen)WorkshopTutorial.Active.Close();
            if(errors.Count>0)error+="\n"+string.Join("\n",errors);
            Directory.CreateDirectory("Tools/Verification");File.WriteAllText("Tools/Verification/teaching-guidance-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,error=error,checks=checks.ToArray()},true));SessionState.SetBool("Teaching.Passed",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator CloseTip(){WorkshopTutorial.Active.Close();yield return null;yield return null;}
        IEnumerator Load(string scene){WorkshopTeachingDirector.VerificationEnabled=false;yield return SceneManager.LoadSceneAsync(scene);for(int i=0;i<5;i++)yield return null;}
        WorkshopTeachingDirector Director(AssemblyManager manager)
        {var director=manager.GetComponent<WorkshopTeachingDirector>();director.enabled=false;var narrator=manager.GetComponent<AssemblyNarrator>();narrator.enabled=false;narrator.Source.Stop();WorkshopTeachingDirector.VerificationEnabled=true;return director;}
        static WorkshopCard FindCard(Component owner,string prefix)=>owner.GetComponentsInChildren<WorkshopCard>(true).First(c=>c.gameObject.activeInHierarchy && c.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>(t.text??"").StartsWith(prefix)));
        static void Click(WorkshopCard card)=>card.OnPointerClick(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left});
        void Capture(GameObject owner,Camera camera,string name)
        {
            var canvas=owner.GetComponentsInChildren<Canvas>().First(c=>c.isRootCanvas);var mode=canvas.renderMode;var previousCamera=canvas.worldCamera;var plane=canvas.planeDistance;var target=new RenderTexture(1440,900,24);target.Create();var oldTarget=camera.targetTexture;var oldRect=camera.rect;
            try{camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.4f;Canvas.ForceUpdateCanvases();WorkshopPresentationBuilder.Render(camera,"Tools/Verification/"+name+".png",1440,900);}
            finally{canvas.renderMode=mode;canvas.worldCamera=previousCamera;canvas.planeDistance=plane;camera.targetTexture=oldTarget;camera.rect=oldRect;target.Release();Destroy(target);}
        }
        IEnumerator Scenario()
        {
            yield return null;yield return null;
            foreach(var platform in new[]{"PC","Touch","Mobile","AR","VR"})
            {
                var lessons=WorkshopTutorial.LessonsFor(platform);Check(lessons.Length==(platform=="AR"?2:1),platform+" has a separate movement-only introduction");
                Check(lessons.All(l=>WorkshopTutorial.IsMovement(l.id)),platform+" Section 0 excludes pickup, placement, lift and help commands");
                foreach(var lesson in lessons)Check(Resources.Load<NarrationLibrary>("NarrationLibrary").Find(lesson.id) && NarrationText.Find(lesson.id)?.words?.Length>0,"Movement voice and timed captions exist: "+lesson.id);
            }
            Check(WorkshopGuidance.Hints && !WorkshopGuidance.Arrow,"Fresh guidance defaults are hints on and arrow off");Check(WorkshopDifficulty.Saved==SnapDifficulty.Easy,"Fresh snap difficulty defaults to Easy");
            var manager=AssemblyManager.Instance;var ui=manager.GetComponent<AssemblyWorkshopUI>();ui.SetOpen(false);var player=FindAnyObjectByType<PlayerAssemblyController>();player.enabled=false;var director=Director(manager);
            director.Tick();Check(WorkshopTutorial.IsOpen && !WorkshopTutorial.Active.IsContextual && WorkshopTutorial.Active.CurrentLessonId=="tutorial-pc-navigation","Entering PC play automatically opens movement Section 0");
            Check(Time.timeScale>0 && !WorkshopTutorial.BlocksInput,"The movement introduction leaves gameplay running");Capture(manager.gameObject,player.ViewCamera,"teaching-pc-movement");yield return CloseTip();
            director.Tick();Check(!WorkshopTutorial.IsOpen,"Other commands do not appear before they are needed");
            player.PickUp(manager.NextHintPart());Check(player.IsHoldingPart,"A real model component is carried for contextual teaching");director.Tick();Check(WorkshopTutorial.Active && WorkshopTutorial.Active.IsContextual && WorkshopTutorial.Active.CurrentLessonId=="tutorial-pc-pickup","Pickup controls are taught on first pickup");yield return CloseTip();
            director.Tick();Check(WorkshopTutorial.Active && WorkshopTutorial.Active.CurrentLessonId=="tutorial-pc-placement","Easy placement is taught when a component is held");yield return CloseTip();
            director.Tick();Check(!WorkshopTutorial.IsOpen,"Completed control tips are not repeated every frame");player.DropHeldPart(false);
            manager.SelectChapter(0);yield return null;yield return null;var first=manager.NextHintPart();first.InstallImmediate(first.TargetSocket);manager.AssemblyRoot.rotation=Quaternion.identity;director.Tick();
            Check(WorkshopTutorial.Active && WorkshopTutorial.Active.CurrentLessonId=="tutorial-pc-lift","Lift and flip controls appear at the first required turn");yield return CloseTip();
            var next=manager.NextHintPart();manager.TargetFrame(next.Definition).rotation=next.Definition.orientation==AssemblyOrientation.UpsideDown?Quaternion.Euler(180,0,0):Quaternion.identity;player.PickUp(next);manager.SetDifficulty(SnapDifficulty.Hard,false);director.Tick();
            Check(WorkshopTutorial.Active && WorkshopTutorial.Active.CurrentLessonId=="tutorial-pc-placement-advanced","Q and rotation controls are introduced only when advanced placement is needed");yield return CloseTip();
            manager.SetDifficulty(SnapDifficulty.Easy,false);ui.Open(AssemblyWorkshopUI.Page.Settings);Click(FindCard(ui,"GHOST HINTS"));Check(!WorkshopGuidance.Hints && !manager.GetComponent<AssemblyHint>().Enabled,"Settings disables ghost hints");
            Click(FindCard(ui,"DIRECTION ARROW"));var arrow=ui.WorkshopCanvas.GetComponentInChildren<WorkshopQuestArrow>(true);Check(WorkshopGuidance.Arrow && arrow.Tracking,"Settings enables an arrow that was previously disabled");Capture(manager.gameObject,player.ViewCamera,"teaching-pc-guidance-settings");
            ui.SetOpen(false);var guide=manager.GetComponent<AssemblyEasyGuide>();guide.RefreshTargets();var ghost=FindObjectsByType<AssemblyVisual>(FindObjectsSortMode.None).Where(v=>v.name=="Easy placement preview").ToArray();
            Check(ghost.All(v=>(GameObject)typeof(AssemblyVisual).GetField("visualRoot",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(v)==null),"Turning hints off removes all placement ghost visuals");player.DropHeldPart(false);
            yield return Load("MobileAssembly");var mobile=FindAnyObjectByType<MobileAssemblyController>();while(!mobile.Ready)yield return null;manager=mobile.GetComponent<AssemblyManager>();
            Check(WorkshopTutorial.DetectPlatform(mobile.gameObject)=="Mobile","Ordinary phone mode is distinct from AR");Check(mobile.QuestArrow.Tracking && !manager.GetComponent<AssemblyHint>().Enabled,"Guidance choices carry across experience changes");
            mobile.GuidanceSettings.SetOpen(true);yield return null;yield return null;Check(FindCard(mobile.GuidanceSettings,"GHOST HINTS") && FindCard(mobile.GuidanceSettings,"DIRECTION ARROW"),"Phone Settings exposes both guidance toggles");Capture(mobile.gameObject,mobile.ViewCamera,"teaching-phone-guidance-settings");
            Click(FindCard(mobile.GuidanceSettings,"GHOST HINTS"));Click(FindCard(mobile.GuidanceSettings,"DIRECTION ARROW"));Check(WorkshopGuidance.Hints && !WorkshopGuidance.Arrow && !mobile.QuestArrow.Tracking,"Phone settings restore hints and disable the arrow immediately");mobile.GuidanceSettings.SetOpen(false);
            var intro=WorkshopTutorial.Open(mobile.gameObject);Check(intro.CurrentLessonId=="tutorial-mobile-navigation" && intro.LessonCount==1,"Phone movement introduction uses gestures without AR scanning commands");yield return CloseTip();
            UnityEngine.Events.UnityAction<Scene,LoadSceneMode> setup=(scene,mode)=>{if(scene.name=="MobileAssemblyAR"){var session=FindAnyObjectByType<MobileARSession>();session.Configure(session.gameObject.AddComponent<ARTestTrackingProvider>(),session.FallbackScene);}};
            SceneManager.sceneLoaded+=setup;yield return Load("MobileAssemblyAR");SceneManager.sceneLoaded-=setup;mobile=FindAnyObjectByType<MobileAssemblyController>();while(!mobile.Ready)yield return null;manager=mobile.GetComponent<AssemblyManager>();director=Director(manager);director.Tick();
            Check(WorkshopTutorial.Active && WorkshopTutorial.Active.Platform=="AR" && WorkshopTutorial.Active.CurrentLessonId=="tutorial-ar-place" && WorkshopTutorial.Active.LessonCount==2,"Supported AR starts with surface placement and active-build gestures; detected="+WorkshopTutorial.DetectPlatform(mobile.gameObject)+", state="+mobile.GetComponent<MobileARSession>().State+", lesson="+(WorkshopTutorial.Active?WorkshopTutorial.Active.CurrentLessonId:"none")+", timeScale="+Time.timeScale);yield return CloseTip();
            yield return Load("AssemblyWorkshop");manager=AssemblyManager.Instance;ui=manager.GetComponent<AssemblyWorkshopUI>();ui.SetOpen(false);QuestVRSession.Instance.EnableVerification();yield return null;yield return null;director=Director(manager);director.Tick();
            Check(WorkshopTutorial.Active && WorkshopTutorial.Active.Platform=="VR" && WorkshopTutorial.Active.CurrentLessonId=="tutorial-vr-navigation" && WorkshopTutorial.Active.LessonCount==1,"VR starts with its own walking, snap-turn and teleport lesson");yield return CloseTip();
            var rig=FindAnyObjectByType<QuestVRRig>();rig.HeadCamera.transform.position=AssemblyHeldView.WorldBounds(manager.NextHintPart()).center+Vector3.back*1.5f;director.Tick();
            Check(WorkshopTutorial.Active && WorkshopTutorial.Active.CurrentLessonId=="tutorial-vr-parts","VR grip and fitting controls appear when approaching a component");
            var text=WorkshopTutorial.FindLesson("tutorial-vr-parts").text;Check(text.Contains("preview") && text.IndexOf("release the grip",StringComparison.OrdinalIgnoreCase)>=0 && text.Contains("Move your hand away") && text.Contains("trigger"),"VR lesson covers reversible grip snapping, release installation and trigger placement");yield return CloseTip();
            WorkshopTeachingDirector.VerificationEnabled=false;Destroy(QuestVRSession.Instance.gameObject);yield return null;
        }
    }
}
