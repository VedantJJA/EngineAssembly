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
            var scenario=Scenario();string error="";
            while(true)
            {
                bool next=false;object current=null;
                try { next=scenario.MoveNext();if(next)current=scenario.Current; }catch(Exception ex) { error=ex.ToString(); }
                if(!next || error.Length>0)break;yield return current;
            }
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
            var cards=ui.GetComponentsInChildren<WorkshopCard>();Assert(cards.Length>=9,"Nine chapter cards are rendered");
            Assert(ui.GetComponentsInChildren<TextMeshProUGUI>().All(t=>t.font!=null),"All text has a font");
            Capture(ui,"chapter-menu");
            var first=cards.First(c=>c.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text.Contains("CHAPTER 01")));
            ExecuteEvents.Execute<IPointerClickHandler>(first.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},(h,e)=>h.OnPointerClick((PointerEventData)e));
            Assert(!ui.IsOpen && Time.timeScale==1 && manager.CurrentChapterIndex==0,"Chapter card click enters gameplay and restores time");
            var reticle=ui.GetComponentsInChildren<TextMeshProUGUI>().Single(t=>t.text=="+");
            Assert(reticle.rectTransform.anchorMin==new Vector2(.5f,.5f) && reticle.rectTransform.anchorMax==new Vector2(.5f,.5f) && reticle.rectTransform.anchoredPosition==Vector2.zero,"Crosshair is centred independently of resolution");
            manager.SetInteraction(WorkshopInteraction.Move);yield return null;
            Assert(ui.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text=="MOVE MODE ACTIVE"),"Move mode shows a visible corner badge");
            manager.SetInteraction(WorkshopInteraction.Build);yield return null;
            Assert(!ui.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text=="MOVE MODE ACTIVE"),"Build mode hides the Move badge");
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
            var medium=ui.GetComponentsInChildren<WorkshopCard>().First(c=>c.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text=="Medium"));
            medium.clicked();yield return null;Assert(manager.Difficulty==SnapDifficulty.Medium,"Difficulty card changes snap behavior");
            manager.SetMode(WorkshopMode.Edit);manager.SelectChapter(3);ui.Open(AssemblyWorkshopUI.Page.Author);yield return null;Capture(ui,"editor-menu");
            Assert(manager.Recipe.parts.Where(p=>manager.Recipe.chapters.FindIndex(c=>c.id==p.chapterId)<3).All(p=>manager.IsPlaced(p.id)),"Editor chapter jump prepares every earlier chapter");
            manager.AddChapter("New authored chapter");ui.Open(AssemblyWorkshopUI.Page.Chapters);yield return null;
            Assert(ui.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text=="New authored chapter"),"New draft appears immediately as a chapter card");
            manager.SetMode(WorkshopMode.Assembly);ui.Open(AssemblyWorkshopUI.Page.Chapters);yield return null;
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
