using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace EngineAssembly.Editor
{
    [InitializeOnLoad]
    public static class WorkshopUIVerification
    {
        static WorkshopUIVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Batch verification only.");
            SessionState.SetBool("Workshop.UIVerify",true);
            EditorSceneManager.OpenScene(AssemblyWorkshopBuilder.ScenePath,OpenSceneMode.Single);EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("Workshop.UIVerify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("UI verification").AddComponent<WorkshopUIVerificationRunner>();
            if(state==PlayModeStateChange.EnteredEditMode) { SessionState.SetBool("Workshop.UIVerify",false);EditorApplication.Exit(SessionState.GetBool("Workshop.UIResult",false)?0:1); }
        }
    }
    public sealed class WorkshopUIVerificationRunner : MonoBehaviour
    {
        [Serializable] class Report { public bool passed;public string[] checks;public string error; }
        readonly List<string> checks=new List<string>();
        IEnumerator Start()
        {
            int originalDifficulty=PlayerPrefs.GetInt("Assembly.SnapDifficulty",0);
            var scenario=Scenario();string error="";
            while(true)
            {
                bool next=false;object current=null;
                try { next=scenario.MoveNext();if(next)current=scenario.Current; }catch(Exception ex) { error=ex.ToString(); }
                if(!next || error.Length>0)break;yield return current;
            }
            PlayerPrefs.SetInt("Assembly.SnapDifficulty",originalDifficulty);PlayerPrefs.Save();
            File.WriteAllText("Tools/Verification/ui-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,checks=checks.ToArray(),error=error},true));
            SessionState.SetBool("Workshop.UIResult",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Scenario()
        {
            yield return null;yield return null;
            var manager=FindAnyObjectByType<AssemblyManager>();var ui=manager.GetComponent<AssemblyWorkshopUI>();
            Assert(ui && ui.IsOpen && Time.timeScale==0,"Chapter screen pauses workshop on start");
            Assert(FindObjectsByType<EventSystem>().Length==1,"One EventSystem handles custom card input");
            Assert(ui.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length==0,"Runtime UI uses custom cards without stock buttons");
            var cards=ui.GetComponentsInChildren<WorkshopCard>();Assert(cards.Count(c=>c.name.StartsWith("Chapter card · "))==manager.Recipe.chapters.Count,"Chapter selector shows a grid of cards, including the PC repair bonus");
            Assert(ui.GetComponentsInChildren<TextMeshProUGUI>().All(t=>t.font!=null),"All text has a font");
            Capture(ui,"chapter-menu");
            var first=cards.First(c=>c.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text.Contains("CHAPTER 01")));
            ExecuteEvents.Execute<IPointerClickHandler>(first.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},(h,e)=>h.OnPointerClick((PointerEventData)e));
            Assert(!ui.IsOpen && Time.timeScale==1 && manager.CurrentChapterIndex==0,"Chapter card click enters gameplay and restores time");
            var tracker=ui.GetComponentInChildren<WorkshopQuestArrow>(true);
            Assert(tracker.GetComponent<CanvasRenderer>(),"Quest arrow has its required CanvasRenderer");
#if ENABLE_INPUT_SYSTEM
            InputSystem.settings=Instantiate(InputSystem.settings);
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard=InputSystem.AddDevice<Keyboard>();var oldInputMode=InputSystem.settings.updateMode;
            InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;ui.enabled=false;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.V));InputSystem.Update();ui.SendMessage("Update");
            Assert(tracker.Tracking,"Pressing V enables the quest arrow");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();InputSystem.settings.updateMode=oldInputMode;InputSystem.RemoveDevice(keyboard);ui.enabled=true;
#else
            tracker.Toggle(true);
#endif
            yield return null;Canvas.ForceUpdateCanvases();Capture(ui,"quest-arrow-pickup");
            Assert(tracker.canvasRenderer.GetMesh() && tracker.canvasRenderer.GetMesh().vertexCount>=7,"Quest arrow produces visible graphic geometry");
            Assert(tracker.TargetPart==manager.NextHintPart() && !tracker.PointsToPlacement,"Quest arrow initially points to the next pickup");
            var arrowPlayer=FindAnyObjectByType<PlayerAssemblyController>();arrowPlayer.enabled=false;
            var savedRotation=arrowPlayer.ViewCamera.transform.rotation;
            arrowPlayer.ViewCamera.transform.LookAt(AssemblyHeldView.WorldBounds(tracker.TargetPart).center);yield return null;
            Assert(tracker.Tracking && !tracker.ArrowVisible && tracker.canvasRenderer.GetAlpha()==0,"Onscreen pickup hides the border arrow without turning tracking off");
            arrowPlayer.ViewCamera.transform.Rotate(0,180,0);yield return null;
            Assert(tracker.ArrowVisible,"Arrow returns automatically when the target leaves the screen");
            arrowPlayer.ViewCamera.transform.rotation=savedRotation;arrowPlayer.enabled=true;
            var controls=FindAnyObjectByType<PlayerAssemblyController>();controls.PickUp(manager.NextHintPart());yield return null;
            Capture(ui,"quest-arrow-placement");
            Assert(tracker.TargetPart==controls.CurrentHeldPart && tracker.PointsToPlacement,"Quest arrow switches to the held component's placement target");controls.DropHeldPart(false);tracker.Toggle(false);
            var reticle=ui.GetComponentsInChildren<TextMeshProUGUI>().Single(t=>t.text=="+");
            Assert(reticle.rectTransform.anchorMin==new Vector2(.5f,.5f) && reticle.rectTransform.anchorMax==new Vector2(.5f,.5f) && reticle.rectTransform.anchoredPosition==Vector2.zero,"Crosshair is centred independently of resolution");
            Assert(!ui.GetComponentsInChildren<TextMeshProUGUI>(true).Any(t=>t.text.Contains("MOVE MODE ACTIVE") || t.text.Contains("CAPS LOCK")),"Old Move mode and Caps Lock HUD controls are removed");
            ui.SetOpen(true);yield return null;
            var task=ui.GetComponentsInChildren<WorkshopCard>().First(c=>c.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text.StartsWith("TASK:")));
            task.clicked();yield return null;
            Assert(manager.Task==AssemblyTask.Disassemble && manager.ChapterInstalledCount==manager.ChapterPartCount,"Task card starts the selected section assembled for disassembly");
            task=ui.GetComponentsInChildren<WorkshopCard>().First(c=>c.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text.StartsWith("TASK:")));
            task.clicked();yield return null;
            Assert(manager.Task==AssemblyTask.Assemble && manager.ChapterInstalledCount==0,"Task card switches back to a loose assembly section");
            ui.Open(AssemblyWorkshopUI.Page.Pause);yield return null;Capture(ui,"pause-menu");
            Assert(Time.timeScale==0,"Escape pause state stops simulation");
            ui.Open(AssemblyWorkshopUI.Page.Settings);yield return null;Capture(ui,"settings-menu");
            manager.SetDifficulty(SnapDifficulty.Hard,false);ui.CycleDifficulty(1);yield return null;Assert(manager.Difficulty==SnapDifficulty.Medium,"Difficulty arrow changes snap behavior");
            manager.SetMode(WorkshopMode.Edit);manager.SelectChapter(3);ui.Open(AssemblyWorkshopUI.Page.Author);yield return null;Capture(ui,"editor-menu");
            Assert(manager.Recipe.parts.Where(p=>manager.Recipe.chapters.FindIndex(c=>c.id==p.chapterId)<3).All(p=>manager.IsPlaced(p.id)),"Editor chapter jump prepares every earlier chapter");
            manager.AddChapter("New authored chapter");ui.Open(AssemblyWorkshopUI.Page.Chapters);yield return null;
            Assert(ui.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text=="New authored chapter"),"New draft appears immediately as a chapter card");
            manager.SetMode(WorkshopMode.Assembly);ui.Open(AssemblyWorkshopUI.Page.Chapters);yield return null;
            ui.BrowseChapter(manager.Recipe.chapters.Count-1-ui.BrowsedChapterIndex);yield return null;
            Assert(!ui.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text=="EDIT CHAPTERS" || t.text=="+ ADD CHAPTER" || t.text=="JSON LIBRARY"),"Play chapter menu has no authoring controls");
            Assert(ui.GetComponentsInChildren<WorkshopCard>().Any(c=>!c.interactable),"Empty draft cannot be launched in play mode");
            ui.SetOpen(false);Assert(Time.timeScale==1,"Leaving menus restores simulation after multiple pages");manager.SetDifficulty(SnapDifficulty.Hard);
        }
        void Capture(AssemblyWorkshopUI ui,string name)
        {
            var canvas=ui.GetComponentInChildren<Canvas>();var camera=Camera.main;
            var mode=canvas.renderMode;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.5f;
            var rt=new RenderTexture(1440,900,24);rt.Create();camera.targetTexture=rt;Canvas.ForceUpdateCanvases();
            WorkshopPresentationBuilder.Render(camera,"Tools/Verification/"+name+".png",1440,900);
            camera.targetTexture=null;rt.Release();Destroy(rt);canvas.renderMode=mode;Canvas.ForceUpdateCanvases();
        }
        void Assert(bool condition,string message) { if(!condition)throw new Exception(message);checks.Add(message); }
    }
}
