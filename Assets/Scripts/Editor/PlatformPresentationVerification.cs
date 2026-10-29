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
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class PlatformPresentationVerification
    {
        const string Prefix="PlatformPresentation.";
        const string DifficultyKey="Assembly.SnapDifficulty",VersionKey="Assembly.EasyDefaultVersion";
        static PlatformPresentationVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch()
        {
            SessionState.SetBool("PlatformPresentation.PortraitOnly",false);
            SavePref(DifficultyKey);SavePref(VersionKey);PlayerPrefs.DeleteKey(DifficultyKey);PlayerPrefs.DeleteKey(VersionKey);
            SessionState.SetBool(Prefix+"Run",true);EditorSceneManager.OpenScene("Assets/Scenes/AssemblyWorkshop.unity");EditorApplication.isPlaying=true;
        }
        public static void RunMovementPortrait(){RunBatch();SessionState.SetBool("PlatformPresentation.PortraitOnly",true);}
        static void SavePref(string key){SessionState.SetBool(Prefix+key+".Exists",PlayerPrefs.HasKey(key));SessionState.SetInt(Prefix+key,PlayerPrefs.GetInt(key));}
        public static void RestorePrefs()
        {
            foreach(string key in new[]{DifficultyKey,VersionKey})if(SessionState.GetBool(Prefix+key+".Exists",false))PlayerPrefs.SetInt(key,SessionState.GetInt(Prefix+key,0));else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Prefix+"Run",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Platform presentation verification").AddComponent<PlatformPresentationRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){RestorePrefs();SessionState.SetBool(Prefix+"Run",false);EditorApplication.Exit(SessionState.GetBool(Prefix+"Passed",false)?0:1);}
        }
    }
    public sealed class PlatformPresentationRunner:MonoBehaviour
    {
        [Serializable] sealed class CaptureRecord { public string file;public int width,height,screenWidth,screenHeight; }
        [Serializable] sealed class Report { public bool passed;public string error;public string[] checks;public CaptureRecord[] captures; }
        readonly List<string> checks=new List<string>(),errors=new List<string>();readonly List<CaptureRecord> captures=new List<CaptureRecord>();
        readonly List<(object group,int customIndex)> temporarySizes=new List<(object,int)>();
        EditorWindow gameView;PropertyInfo selectedSize;int originalSize=-1;
#if ENABLE_INPUT_SYSTEM
        InputSettings previousInput;Keyboard keyboard;Mouse mouse;
#endif
        void OnEnable()=>Application.logMessageReceived+=Log;
        void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string stack,LogType type){if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert)errors.Add(message+"\n"+stack);}
        void Check(bool condition,string message){if(!condition)throw new Exception(message);checks.Add(message);}
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);Directory.CreateDirectory("Tools/Verification");var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";
            while(stack.Count>0){bool more=false;object value=null;try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception e){error=e.ToString();break;}if(!more){stack.Pop();continue;}if(value is IEnumerator routine)stack.Push(routine);else yield return value;}
            try{Cleanup();}catch(Exception e){error+="\nCleanup: "+e;}
            if(errors.Count>0)error+="\nRuntime errors: "+string.Join("\n",errors);
            File.WriteAllText("Tools/Verification/"+(SessionState.GetBool("PlatformPresentation.PortraitOnly",false)?"movement-portrait-report.json":"platform-presentation-report.json"),JsonUtility.ToJson(new Report{passed=error.Length==0,error=error,checks=checks.ToArray(),captures=captures.ToArray()},true));
            SessionState.SetBool("PlatformPresentation.Passed",error.Length==0);EditorApplication.isPlaying=false;
        }
        void Cleanup()
        {
#if ENABLE_INPUT_SYSTEM
            if(keyboard!=null && keyboard.added)InputSystem.RemoveDevice(keyboard);if(mouse!=null && mouse.added)InputSystem.RemoveDevice(mouse);if(previousInput)InputSystem.settings=previousInput;
#endif
            if(WorkshopTutorial.IsOpen)WorkshopTutorial.Active.Close();PlatformPresentationVerification.RestorePrefs();
            if(gameView && selectedSize!=null && originalSize>=0)selectedSize.SetValue(gameView,originalSize);
            for(int i=temporarySizes.Count-1;i>=0;i--){var item=temporarySizes[i];item.group.GetType().GetMethod("RemoveCustomSize",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(item.group,new object[]{item.customIndex});}
        }
        IEnumerator Load(string scene){yield return SceneManager.LoadSceneAsync(scene);for(int i=0;i<4;i++)yield return null;}
        IEnumerator ViewSize(int width,int height)
        {
            var assembly=typeof(EditorWindow).Assembly;var viewType=assembly.GetType("UnityEditor.GameView");var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
            gameView=EditorWindow.GetWindow(viewType,false,"Game",false);selectedSize=viewType.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(originalSize<0)originalSize=(int)selectedSize.GetValue(gameView);
            var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);var instance=singleton.GetProperty("instance",BindingFlags.Static|BindingFlags.Public).GetValue(null);
            var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;var getGroup=sizesType.GetMethod("GetGroup",flags);var groupType=getGroup.GetParameters()[0].ParameterType;
            var currentGroup=sizesType.GetProperty("currentGroupType",flags);object groupId=currentGroup!=null?currentGroup.GetValue(instance):Enum.Parse(groupType,EditorUserBuildSettings.activeBuildTarget==BuildTarget.Android?"Android":"Standalone");
            var group=getGroup.Invoke(instance,new[]{groupId});var sizeType=assembly.GetType("UnityEditor.GameViewSize");var kindType=assembly.GetType("UnityEditor.GameViewSizeType");
            var size=Activator.CreateInstance(sizeType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new[]{Enum.Parse(kindType,"FixedResolution"),(object)width,height,"Verification "+width+"x"+height},null);
            int count=(int)group.GetType().GetMethod("GetCustomCount",flags).Invoke(group,null),builtin=(int)group.GetType().GetMethod("GetBuiltinCount",flags).Invoke(group,null);
            group.GetType().GetMethod("AddCustomSize",flags).Invoke(group,new[]{size});temporarySizes.Add((group,builtin+count));selectedSize.SetValue(gameView,builtin+count);gameView.Repaint();
            Screen.SetResolution(width,height,FullScreenMode.Windowed);for(int i=0;i<12;i++)yield return null;
            Check((width>height)==(Screen.width>Screen.height),"Game view uses "+(width>height?"landscape":"portrait")+" presentation for "+width+" × "+height);
        }
        static Transform UIRoot(Component owner){if(owner is PCDisplayChapter){var ui=owner.GetComponent<AssemblyWorkshopUI>();if(ui && ui.WorkshopCanvas)return ui.WorkshopCanvas.transform;}return owner.transform;}
        static WorkshopCard Card(Component owner,string name,bool hidden=true)=>UIRoot(owner).GetComponentsInChildren<WorkshopCard>(hidden).First(c=>c.name==name);
        static void Click(WorkshopCard card)=>card.OnPointerClick(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left});
        static RectTransform FindRect(Component owner,string name)=>UIRoot(owner).GetComponentsInChildren<RectTransform>(true).First(r=>r.name==name);
        void FontCheck(Canvas canvas,string context)
        {
            var visible=canvas.GetComponentsInChildren<TextMeshProUGUI>(true).Where(t=>t.enabled && t.gameObject.activeInHierarchy).ToArray();Check(visible.Length>0,context+" includes visible labels");
            Canvas.ForceUpdateCanvases();
            foreach(var text in visible)
            {
                string content=text.text??"";Check(text.font && text.fontSize>=WorkshopTypography.VRMinimum,context+" readable font: "+text.name+" / "+content.Replace('\n',' '));if(text.enableAutoSizing)Check(text.fontSizeMin>=WorkshopTypography.VRMinimum,context+" does not shrink below caption size: "+text.name);
                if(string.IsNullOrWhiteSpace(content))continue;text.ForceMeshUpdate();float lineHeight=text.font.faceInfo.lineHeight*text.fontSize/Mathf.Max(1,text.font.faceInfo.pointSize);
                Check(text.rectTransform.rect.height+2>=lineHeight,context+" text box fits one readable line: "+content.Replace('\n',' '));
                Check(text.textInfo.characterCount>0 && text.textInfo.characterInfo.Take(text.textInfo.characterCount).Any(c=>c.isVisible),context+" label renders visible glyphs: "+content.Replace('\n',' '));
                if(text.GetComponentInParent<WorkshopCard>())Check(text.preferredHeight<=text.rectTransform.rect.height+2,context+" control label fits without vertical clipping: "+content.Replace('\n',' '));
            }
        }
        void Capture(string name,Camera camera,int width,int height)
        {
            var canvases=FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas && c.renderMode!=RenderMode.WorldSpace).ToArray();var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();var planes=canvases.Select(c=>c.planeDistance).ToArray();
            var scalers=canvases.Select(c=>c.GetComponent<UnityEngine.UI.CanvasScaler>()).ToArray();var scalerEnabled=scalers.Select(c=>c && c.enabled).ToArray();var scales=canvases.Select(c=>c.scaleFactor).ToArray();
            var target=new RenderTexture(width,height,24);target.Create();var oldTarget=camera.targetTexture;var oldRect=camera.rect;camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);
            try
            {
                for(int i=0;i<canvases.Length;i++){if(scalers[i])scalers[i].enabled=false;canvases[i].scaleFactor=1;canvases[i].renderMode=RenderMode.ScreenSpaceCamera;canvases[i].worldCamera=camera;canvases[i].planeDistance=.5f;}
                Canvas.ForceUpdateCanvases();string file="Tools/Verification/"+name+".png";WorkshopPresentationBuilder.Render(camera,file,width,height);captures.Add(new CaptureRecord{file=file,width=width,height=height,screenWidth=Screen.width,screenHeight=Screen.height});
            }
            finally
            {
                for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=planes[i];canvases[i].scaleFactor=scales[i];if(scalers[i])scalers[i].enabled=scalerEnabled[i];}
                camera.targetTexture=oldTarget;camera.rect=oldRect;target.Release();Destroy(target);Canvas.ForceUpdateCanvases();
            }
        }
        IEnumerator Scenario()
        {
            if(SessionState.GetBool("PlatformPresentation.PortraitOnly",false)){yield return MovementPortrait();yield break;}
            yield return null;yield return null;Time.timeScale=1;
            Check(WorkshopDifficulty.Saved==SnapDifficulty.Easy,"A new installation defaults to Easy");
            PlayerPrefs.SetInt("Assembly.SnapDifficulty",(int)SnapDifficulty.Hard);PlayerPrefs.DeleteKey("Assembly.EasyDefaultVersion");
            Check(WorkshopDifficulty.Saved==SnapDifficulty.Easy && PlayerPrefs.GetInt("Assembly.EasyDefaultVersion")==1,"Old implicit Hard preference migrates to Easy once");
            foreach(var explicitChoice in new[]{SnapDifficulty.Medium,SnapDifficulty.Hard,SnapDifficulty.Easy}){PlayerPrefs.SetInt("Assembly.SnapDifficulty",(int)explicitChoice);Check(WorkshopDifficulty.Saved==explicitChoice,"An explicit "+explicitChoice+" choice is preserved after migration");}
            yield return ViewSize(1440,900);var manager=AssemblyManager.Instance;while(!manager || manager.Recipe==null){yield return null;manager=AssemblyManager.Instance;}
            var ui=manager.GetComponent<AssemblyWorkshopUI>();ui.SetOpen(false);manager.SelectChapter(0);Check(manager.Difficulty==SnapDifficulty.Easy,"PC assembly starts on Easy");
            var player=FindAnyObjectByType<PlayerAssemblyController>();yield return VerifyTutorialMovement(manager,player);Capture("platform-pc-section-zero",player.ViewCamera,1440,900);WorkshopTutorial.Active.Close();yield return null;yield return null;
            manager.SelectChapter(manager.Recipe.chapters.FindIndex(c=>c.id==PCDisplayChapter.ChapterId));yield return null;yield return null;var display=manager.GetComponent<PCDisplayChapter>();
            var controls=FindRect(display,"Engine operating controls");var settings=FindRect(display,"Display settings");Check(controls.gameObject.activeInHierarchy && controls.rect.height<=112,"PC display keeps a compact playback dock");Check(!settings.gameObject.activeInHierarchy,"PC display settings start closed");
            var next=ui.WorkshopCanvas.GetComponentsInChildren<WorkshopCard>(true).First(c=>c.GetComponentsInChildren<TextMeshProUGUI>(true).Any(t=>t.text.StartsWith("NEXT STEP")));Check(!next.gameObject.activeInHierarchy,"Display hides assembly Next Step control");
            var header=FindRect(ui,"Workshop header");Check(header.rect.height<=76,"PC display header is compact");Check(!FindRect(display,"Display captions").gameObject.activeInHierarchy,"PC complete-engine view has no oversized instruction card");Capture("platform-pc-display-clear",player.ViewCamera,1440,900);
            Click(Card(display,"DISPLAY SETTINGS"));yield return null;Check(settings.gameObject.activeInHierarchy,"PC display settings open through their card");Capture("platform-pc-display-settings",player.ViewCamera,1440,900);Click(Card(display,"DONE"));yield return null;Check(!settings.gameObject.activeInHierarchy,"PC display settings close through Done");
            yield return ViewSize(1920,1080);yield return Load("MobileEngineDisplay");var phone=FindAnyObjectByType<MobileEngineDisplay>();while(!phone || !phone.Ready){yield return null;phone=FindAnyObjectByType<MobileEngineDisplay>();}yield return null;
            yield return VerifyPhoneDisplay(phone,"landscape",1920,1080);
            yield return ViewSize(1080,1920);yield return null;yield return VerifyPhoneDisplay(phone,"portrait",1080,1920);
            yield return Load("MobileAssembly");var assembly=FindAnyObjectByType<MobileAssemblyController>();while(!assembly || !assembly.Ready){yield return null;assembly=FindAnyObjectByType<MobileAssemblyController>();}Check(assembly.GetComponent<AssemblyManager>().Difficulty==SnapDifficulty.Easy,"Phone assembly starts on Easy");
            yield return ViewSize(1440,900);yield return Load("AssemblyWorkshop");manager=AssemblyManager.Instance;while(!manager || manager.Recipe==null){yield return null;manager=AssemblyManager.Instance;}ui=manager.GetComponent<AssemblyWorkshopUI>();ui.SetOpen(false);manager.SelectChapter(0);
            QuestVRSession.Instance.EnableVerification();yield return null;yield return null;var rig=FindAnyObjectByType<QuestVRRig>();Check(manager.Difficulty==SnapDifficulty.Easy,"Quest assembly starts on Easy");FontCheck(ui.WorkshopCanvas,"VR workshop");
            manager.SelectChapter(manager.Recipe.chapters.FindIndex(c=>c.id==PCDisplayChapter.ChapterId));yield return null;yield return null;display=manager.GetComponent<PCDisplayChapter>();FontCheck(ui.WorkshopCanvas,"New VR display controls");Check(!FindRect(display,"Display settings").gameObject.activeInHierarchy,"VR display settings start closed");Capture("platform-vr-display-clear",rig.HeadCamera,1440,900);
            Click(Card(display,"DISPLAY SETTINGS"));yield return null;FontCheck(ui.WorkshopCanvas,"VR display settings");Capture("platform-vr-display-settings",rig.HeadCamera,1440,900);Click(Card(display,"DONE"));yield return null;
            var guide=WorkshopTutorial.Open(manager.gameObject);yield return null;rig.BindUI();yield return null;Check(guide.Platform=="VR" && guide.GetComponent<Canvas>().renderMode==RenderMode.WorldSpace,"New Section 0 uses tracked VR UI");FontCheck(guide.GetComponent<Canvas>(),"VR Section 0");
            var keys=(TextMeshProUGUI)typeof(WorkshopTutorial).GetField("controls",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(guide);
            var progress=(TextMeshProUGUI)typeof(WorkshopTutorial).GetField("progress",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(guide);
            for(int i=0;i<guide.LessonCount;i++){guide.Show(i);yield return null;Canvas.ForceUpdateCanvases();keys.ForceMeshUpdate();Check(keys.preferredHeight<=keys.rectTransform.rect.height+2,"VR controls reference fits without clipping for "+guide.CurrentLessonId);Check(-keys.rectTransform.anchoredPosition.y+keys.rectTransform.rect.height<-progress.rectTransform.anchoredPosition.y,"VR controls stay above guide footer for "+guide.CurrentLessonId);}
            guide.Show(0);yield return null;Check(!WorkshopTutorial.BlocksInput && Time.timeScale>0,"VR Section 0 permits locomotion and interaction");Capture("platform-vr-section-zero",rig.HeadCamera,1440,900);guide.Close();yield return null;Destroy(QuestVRSession.Instance.gameObject);yield return null;
        }
        static Rect ScreenRect(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);var first=RectTransformUtility.WorldToScreenPoint(null,corners[0]);var last=RectTransformUtility.WorldToScreenPoint(null,corners[2]);return Rect.MinMaxRect(first.x,first.y,last.x,last.y);
        }
        IEnumerator MovementPortrait()
        {
            yield return ViewSize(1080,1920);yield return Load("MobileAssembly");var mobile=FindAnyObjectByType<MobileAssemblyController>();while(!mobile.Ready)yield return null;
            var guide=WorkshopTutorial.Open(mobile.gameObject);yield return null;yield return null;var panel=guide.transform.Find("Guide card") as RectTransform;var area=ScreenRect(panel);
            Check(area.xMin>=0 && area.xMax<=Screen.width && area.yMin>=0 && area.yMax<=Screen.height,"Portrait phone movement card fits entirely inside the screen");
            Check(guide.Platform=="Mobile" && guide.CurrentLessonId=="tutorial-mobile-navigation","Portrait phone view teaches phone gestures");Capture("teaching-phone-movement-portrait",mobile.ViewCamera,1080,1920);guide.Close();yield return null;
            UnityEngine.Events.UnityAction<Scene,LoadSceneMode> setup=(scene,mode)=>{if(scene.name=="MobileAssemblyAR"){var session=FindAnyObjectByType<MobileARSession>();session.Configure(session.gameObject.AddComponent<ARTestTrackingProvider>(),session.FallbackScene);}};
            SceneManager.sceneLoaded+=setup;yield return Load("MobileAssemblyAR");SceneManager.sceneLoaded-=setup;mobile=FindAnyObjectByType<MobileAssemblyController>();while(!mobile.Ready)yield return null;
            guide=WorkshopTutorial.Open(mobile.gameObject);yield return null;yield return null;panel=guide.transform.Find("Guide card") as RectTransform;area=ScreenRect(panel);
            var placement=mobile.GetComponent<MobileAROverlay>().Canvas.GetComponentsInChildren<WorkshopCard>(true).First(c=>c.name=="PLACE ENGINE");
            Check(!area.Overlaps(ScreenRect(placement.transform as RectTransform)),"Portrait AR movement card leaves the Place Engine button accessible");
            Check(area.xMin>=0 && area.xMax<=Screen.width && area.yMin>=0 && area.yMax<=Screen.height,"Portrait AR movement card fits inside the screen");
            Check(guide.Platform=="AR" && guide.CurrentLessonId=="tutorial-ar-place","Portrait AR teaches surface placement before assembly");Capture("teaching-ar-movement-portrait",mobile.ViewCamera,1080,1920);guide.Close();yield return null;
        }
        IEnumerator VerifyPhoneDisplay(MobileEngineDisplay phone,string orientation,int width,int height)
        {
            phone.SetTimeline(1);yield return null;yield return null;Canvas.ForceUpdateCanvases();var footer=FindRect(phone,"Timeline dock");var settings=FindRect(phone,"Display settings");
            Check(footer.rect.height<=140,"Phone "+orientation+" display keeps a compact playback dock");Check(!settings.gameObject.activeInHierarchy,"Phone "+orientation+" settings start closed");
            Check(!FindRect(phone,"Display captions").gameObject.activeInHierarchy,"Phone "+orientation+" overview leaves the engine unobstructed");
            var visible=phone.GetComponentsInChildren<WorkshopCard>().ToArray();Check(visible.Length<=4,"Phone "+orientation+" overview shows only primary navigation and playback cards");Capture("platform-mobile-"+orientation+"-clear",phone.ViewCamera,width,height);
            Click(Card(phone,"DISPLAY SETTINGS card"));yield return null;Check(settings.gameObject.activeInHierarchy,"Phone "+orientation+" settings open");Check(settings.rect.width>=750 && settings.rect.height>=630,"Phone "+orientation+" settings retain readable control spacing");Capture("platform-mobile-"+orientation+"-settings",phone.ViewCamera,width,height);
            Click(Card(phone,"DONE card"));yield return null;Check(!settings.gameObject.activeInHierarchy,"Phone "+orientation+" settings close");
        }
        IEnumerator VerifyTutorialMovement(AssemblyManager manager,PlayerAssemblyController player)
        {
            var guide=WorkshopTutorial.Open(manager.gameObject);yield return null;yield return null;Check(guide.Platform=="PC" && Time.timeScale>0 && !WorkshopTutorial.BlocksInput,"PC Section 0 is nonmodal and leaves simulation running");
            Check(!guide.GetComponent<UnityEngine.UI.Image>(),"Section 0 has no full-screen raycast blocker");var panel=FindRect(guide,"Guide card");Check(panel.rect.width<700 && panel.rect.height<550,"Section 0 occupies a caption-sized corner card");
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=new Vector2(40,Screen.height*.5f)},hits);Check(hits.All(h=>!h.gameObject.GetComponentInParent<WorkshopTutorial>()),"Pointer rays outside the guide remain available to the world");
#if ENABLE_INPUT_SYSTEM
            previousInput=InputSystem.settings;InputSystem.settings=Instantiate(previousInput);InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();var controller=player.GetComponent<CharacterController>();var oldPosition=player.transform.position;var oldRotation=player.transform.rotation;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Section zero movement verification floor";floor.transform.position=new Vector3(40,-.15f,40);floor.transform.localScale=new Vector3(8,.3f,8);controller.enabled=false;player.transform.SetPositionAndRotation(new Vector3(40,.02f,40),Quaternion.identity);controller.enabled=true;Physics.SyncTransforms();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();var start=player.transform.position;yield return new WaitForSecondsRealtime(.3f);var delta=player.transform.position-start;
            Check(new Vector2(delta.x,delta.z).magnitude>.2f,"W advances the real player controller while Section 0 is open");InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();controller.enabled=false;player.transform.SetPositionAndRotation(oldPosition,oldRotation);controller.enabled=true;floor.SetActive(false);Destroy(floor);Physics.SyncTransforms();InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);keyboard=null;mouse=null;InputSystem.settings=previousInput;previousInput=null;
#else
            Check(false,"The presentation verification requires the project's Input System for movement simulation");
#endif
        }
    }
}

