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
    [InitializeOnLoad] public static class ExperienceMenuVerification
    {
        static ExperienceMenuVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch(){SessionState.SetBool("Menu.Verify",true);EditorSceneManager.OpenScene(ExperienceMenuBuilder.ScenePath,OpenSceneMode.Single);EditorApplication.isPlaying=true;}
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("Menu.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){var go=new GameObject("Menu verification");UnityEngine.Object.DontDestroyOnLoad(go);go.AddComponent<ExperienceMenuVerificationRunner>();}
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("Menu.Verify",false);EditorApplication.Exit(SessionState.GetBool("Menu.Result",false)?0:1);}
        }
    }
    public sealed class ExperienceMenuVerificationRunner:MonoBehaviour
    {
        [Serializable] class Report{public bool passed;public string[] checks;public string error;}
        readonly List<string> checks=new List<string>();
        IEnumerator Start()
        {
            int difficulty=PlayerPrefs.GetInt("Assembly.SnapDifficulty",0);float volume=AudioListener.volume;
            var scenario=Scenario();string error="";
            while(true){bool more=false;object step=null;try{more=scenario.MoveNext();if(more)step=scenario.Current;}catch(Exception e){error=e.ToString();}if(!more || error.Length>0)break;yield return step;}
            PlayerPrefs.SetInt("Assembly.SnapDifficulty",difficulty);AudioListener.volume=volume;
            File.WriteAllText("Tools/Verification/menu-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,checks=checks.ToArray(),error=error},true));SessionState.SetBool("Menu.Result",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Scenario()
        {
            yield return null;yield return null;yield return null;
            var menu=FindAnyObjectByType<ExperienceMenu>();
            Assert(menu && menu.CurrentPage==ExperienceMenu.Page.Home,"Main menu opens on its home page");
            Assert(menu.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length==0,"Menu uses custom interactive cards");
            Assert(menu.GetComponentsInChildren<TextMeshProUGUI>().All(t=>t.font),"Main menu text uses the custom font assets");
            Capture(menu,"main-menu");
            menu.Show(ExperienceMenu.Page.Experiences);yield return null;
            Capture(menu,"experience-cards");
            Assert(menu.GetComponentsInChildren<WorkshopCard>().Count(c=>c.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text.Contains(" / ")))==4,"All four experience cards are displayed");
            Assert(Card(menu,"04 / META QUEST 2").interactable,"Quest repair fallback card is available");
            Card(menu,"01 / WINDOWS").clicked();yield return null;yield return null;yield return null;
            Assert(FindAnyObjectByType<PlayerAssemblyController>() && AssemblyManager.Instance.Recipe.parts.All(d=>AssemblyManager.Instance.FindPart(d.id)?.Definition!=null),"Windows card loads every playable component and carrier in the desktop workshop");
            ExperienceMenu.GoHome();yield return null;yield return null;
            menu=FindAnyObjectByType<ExperienceMenu>();menu.Show(ExperienceMenu.Page.Experiences);yield return null;
            var phoneCard=menu.GetComponentsInChildren<WorkshopCard>().Single(c=>c.name=="Play workshop on phone");
            ExecuteEvents.ExecuteHierarchy(phoneCard.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
            yield return null;yield return null;yield return null;
            var phonePlayer=FindAnyObjectByType<PlayerAssemblyController>();var touch=FindAnyObjectByType<MobileWorkshopControls>();
            Assert(ExperienceMenu.MobileWorkshopRequested && phonePlayer && touch && touch.HasTouchHud && touch.HasLandscapeLayout,"First experience phone action opens the full workshop with a landscape touch HUD");
            Assert(AssemblyManager.Instance.Difficulty==SnapDifficulty.Easy,"Phone workshop enables direct ghost placement");
            AssemblyManager.Instance.GetComponent<AssemblyWorkshopUI>().SetOpen(false);yield return null;Canvas.ForceUpdateCanvases();
            var stick=touch.transform.Find("Mobile workshop touch HUD/Move joystick");var stickCenter=RectTransformUtility.WorldToScreenPoint(null,stick.position);
            touch.SetStick(stickCenter+Vector2.right*80);Assert(touch.Movement.x>.3f,"Phone joystick provides walking input");touch.ReleaseStick();
            touch.SetButton(TouchControl.Kind.Crouch,true);touch.SetButton(TouchControl.Kind.Sprint,true);
            Assert(touch.CrouchHeld && touch.SprintHeld && touch.Movement==Vector2.zero,"Phone crouch and sprint buttons provide independent hold input");
            ExperienceMenu.GoHome();yield return null;yield return null;
            menu=FindAnyObjectByType<ExperienceMenu>();menu.Show(ExperienceMenu.Page.Experiences);yield return null;
            Card(menu,"03 / ANDROID").clicked();yield return null;yield return null;yield return null;
            Assert(FindAnyObjectByType<MobileAssemblyController>() && !FindAnyObjectByType<PlayerAssemblyController>(),"Android assembly card opens the mobile prototype without walking");
            ExperienceMenu.GoHome();yield return null;yield return null;
            menu=FindAnyObjectByType<ExperienceMenu>();menu.Show(ExperienceMenu.Page.Experiences);yield return null;
            Card(menu,"02 / ANDROID").clicked();yield return null;yield return null;yield return null;
            Assert(FindAnyObjectByType<MobileEngineDisplay>(),"Android display card routes to its separate timeline scene");
            ExperienceMenu.GoHome();yield return null;yield return null;
            menu=FindAnyObjectByType<ExperienceMenu>();menu.Show(ExperienceMenu.Page.Experiences);yield return null;
            Card(menu,"04 / META QUEST 2").clicked();yield return null;yield return null;yield return null;yield return null;
            Assert(AssemblyManager.Instance.RepairActive && FindAnyObjectByType<RepairVRFallback>().Ready,"Quest card opens the playable repair fallback scene");
            ExperienceMenu.GoHome();yield return null;yield return null;
            menu=FindAnyObjectByType<ExperienceMenu>();menu.Show(ExperienceMenu.Page.Options);yield return null;
            Card(menu,"EASY").clicked();yield return null;Assert(PlayerPrefs.GetInt("Assembly.SnapDifficulty")==2,"Main menu options save snap difficulty");
        }
        WorkshopCard Card(ExperienceMenu menu,string prefix)=>menu.GetComponentsInChildren<WorkshopCard>().Single(c=>c.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text.StartsWith(prefix)));
        void Capture(ExperienceMenu menu,string name)
        {
            var canvas=menu.GetComponentInChildren<Canvas>();var camera=Camera.main;var mode=canvas.renderMode;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.5f;
            var rt=new RenderTexture(1440,900,24);rt.Create();camera.targetTexture=rt;Canvas.ForceUpdateCanvases();
            foreach(var text in canvas.GetComponentsInChildren<TextMeshProUGUI>())text.ForceMeshUpdate();
            WorkshopPresentationBuilder.Render(camera,"Tools/Verification/"+name+".png",1440,900);
            camera.targetTexture=null;rt.Release();Destroy(rt);canvas.renderMode=mode;Canvas.ForceUpdateCanvases();
        }
        void Assert(bool value,string description){if(!value)throw new Exception(description);checks.Add(description);}
    }
}
