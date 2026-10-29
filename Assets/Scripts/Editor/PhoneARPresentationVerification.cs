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
using TMPro;

namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class PhoneARPresentationVerification
    {
        static PhoneARPresentationVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch(){VuforiaSceneSetup.Create();SessionState.SetBool("PhoneAR.Verify",true);EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");EditorApplication.isPlaying=true;}
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("PhoneAR.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Phone AR presentation verification").AddComponent<PhoneARPresentationRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("PhoneAR.Verify",false);EditorApplication.Exit(SessionState.GetBool("PhoneAR.Passed",false)?0:1);}
        }
    }
    public sealed class PhoneARPresentationRunner:MonoBehaviour
    {
        [Serializable] sealed class Report{public bool passed;public string error;public string[] checks;}
        readonly List<string> checks=new List<string>();ARTestTrackingProvider tracking;EditorWindow gameView;PropertyInfo sizeProperty;int originalSize=-1;
        readonly List<(object group,int index)> temporarySizes=new List<(object,int)>();
        void Check(bool valid,string text){if(!valid)throw new Exception(text);checks.Add(text);}
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";
            while(stack.Count>0){bool more=false;object value=null;try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();break;}if(!more){stack.Pop();continue;}if(value is IEnumerator nested)stack.Push(nested);else yield return value;}
            SceneManager.sceneLoaded-=Fake;WorkshopPractice.VerificationEnabled=false;if(gameView && sizeProperty!=null && originalSize>=0)sizeProperty.SetValue(gameView,originalSize);
            foreach(var item in temporarySizes.AsEnumerable().Reverse())item.group.GetType().GetMethod("RemoveCustomSize",BindingFlags.Public|BindingFlags.Instance).Invoke(item.group,new object[]{item.index});
            File.WriteAllText("Tools/Verification/phone-ar-presentation-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,error=error,checks=checks.ToArray()},true));SessionState.SetBool("PhoneAR.Passed",error.Length==0);EditorApplication.isPlaying=false;
        }
        void Fake(Scene scene,LoadSceneMode mode)
        {
            var session=FindAnyObjectByType<MobileARSession>();if(session){tracking=session.gameObject.AddComponent<ARTestTrackingProvider>();session.Configure(tracking,session.FallbackScene);}
        }
        IEnumerator Wait(Func<bool> condition,string text)
        {float end=Time.realtimeSinceStartup+25;while(!condition() && Time.realtimeSinceStartup<end)yield return null;Check(condition(),text);}
        IEnumerator Size(int width,int height)
        {
            var editor=typeof(EditorWindow).Assembly;var viewType=editor.GetType("UnityEditor.GameView");var sizesType=editor.GetType("UnityEditor.GameViewSizes");const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
            gameView=EditorWindow.GetWindow(viewType,false,"Game",false);sizeProperty=viewType.GetProperty("selectedSizeIndex",flags);if(originalSize<0)originalSize=(int)sizeProperty.GetValue(gameView);
            var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);var sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
            var groupType=sizesType.GetMethod("GetGroup",flags).GetParameters()[0].ParameterType;var group=sizesType.GetMethod("GetGroup",flags).Invoke(sizes,new[]{Enum.Parse(groupType,EditorUserBuildSettings.activeBuildTarget==BuildTarget.Android?"Android":"Standalone")});
            int custom=(int)group.GetType().GetMethod("GetCustomCount",flags).Invoke(group,null),builtin=(int)group.GetType().GetMethod("GetBuiltinCount",flags).Invoke(group,null);
            var size=Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize"),flags,null,new[]{Enum.Parse(editor.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),(object)width,height,"Phone AR "+width+"x"+height},null);
            group.GetType().GetMethod("AddCustomSize",flags).Invoke(group,new[]{size});temporarySizes.Add((group,builtin+custom));sizeProperty.SetValue(gameView,builtin+custom);gameView.Repaint();Screen.SetResolution(width,height,FullScreenMode.Windowed);
            for(int i=0;i<14;i++)yield return null;Check(Screen.width==width && Screen.height==height,"Runtime uses "+width+" x "+height);
        }
        static RectTransform RectOf(Component root,string name)=>root.GetComponentsInChildren<RectTransform>(true).First(r=>r.name==name);
        static Rect ScreenRect(RectTransform transform)
        {var corners=new Vector3[4];transform.GetWorldCorners(corners);var a=RectTransformUtility.WorldToScreenPoint(null,corners[0]);var b=RectTransformUtility.WorldToScreenPoint(null,corners[2]);return Rect.MinMaxRect(a.x,a.y,b.x,b.y);}
        void Inside(RectTransform rect,string name){var area=ScreenRect(rect);Check(area.width>0 && area.height>0 && area.xMin>=Screen.safeArea.xMin-2 && area.xMax<=Screen.safeArea.xMax+2 && area.yMin>=Screen.safeArea.yMin-2 && area.yMax<=Screen.safeArea.yMax+2,name+" fits the safe area");}
        void Separate(RectTransform a,RectTransform b,string name)=>Check(!ScreenRect(a).Overlaps(ScreenRect(b)),name+" do not overlap");
        void NoPractice(GameObject host,string name)
        {var manager=host.GetComponent<AssemblyManager>();Check(!host.GetComponent<WorkshopPractice>() && (!manager || manager.CurrentChapterId!="practice-controls" && manager.Recipe.chapters.All(c=>c.id!=WorkshopPractice.ChapterId)) && !WorkshopTutorial.IsOpen,name+" skips phone Chapter 0");Check(WorkshopTutorial.OpenAllControls(host)==null,name+" cannot reopen phone practice");}
        void Capture(string name,Camera camera)
        {
            var canvases=FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas && c.renderMode!=RenderMode.WorldSpace).ToArray();var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();var planes=canvases.Select(c=>c.planeDistance).ToArray();
            var target=new RenderTexture(Screen.width,Screen.height,24);target.Create();var oldTarget=camera.targetTexture;var oldRect=camera.rect;camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceCamera;canvases[i].worldCamera=camera;canvases[i].planeDistance=.5f;}Canvas.ForceUpdateCanvases();
            WorkshopPresentationBuilder.Render(camera,"Tools/Verification/"+name+".png",Screen.width,Screen.height);
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=planes[i];}camera.targetTexture=oldTarget;camera.rect=oldRect;target.Release();Destroy(target);Canvas.ForceUpdateCanvases();
        }
        IEnumerator Scenario()
        {
            WorkshopPractice.VerificationEnabled=true;SceneManager.sceneLoaded+=Fake;WorkshopGuidance.SetCaptions(true);yield return Size(1080,1920);
            foreach(string scene in new[]{"MobileAssemblyAR","MobileDisplayAR"})
            {
                ExperienceMenu.Launch(scene);yield return Wait(()=>SceneManager.GetActiveScene().name==scene && FindAnyObjectByType<MobileARSession>()?.State==MobileARSession.SessionState.Scanning,scene+" starts scanning");
                var session=FindAnyObjectByType<MobileARSession>();var overlay=session.GetComponent<MobileAROverlay>();NoPractice(session.gameObject,scene);tracking.surface=false;yield return null;yield return null;
#if ENGINE_VUFORIA
                var saved=session.GetComponent<VuforiaGroundPlaneProvider>();Check(saved.GroundPlaneRoot && saved.PlaneFinder && saved.GroundPlaneStage && saved.ContentPositioning,"Saved AR scene includes the real Vuforia Ground Plane objects");Check(saved.PlaneFinder.transform.parent==saved.GroundPlaneRoot && saved.GroundPlaneStage.transform.parent==saved.GroundPlaneRoot && saved.TrackingCamera.transform.parent==saved.GroundPlaneRoot,"AR Camera, Plane Finder and Ground Plane Stage are visible together in the saved hierarchy");Check(saved.ContentPositioning.AnchorStage==saved.GroundPlaneStage && !saved.ContentPositioning.DuplicateStage,"Vuforia Content Positioning is linked to the reusable Ground Plane Stage");Check(saved.PlaneFinder.OnInteractiveHitTest.GetPersistentEventCount()>0 && saved.ContentPositioning.OnContentPlaced.GetPersistentEventCount()>0,"Ground Plane hit and content-placement events are serialized in the scene");Check(saved.PlaneFinder.HitTestMode==Vuforia.HitTestMode.AUTOMATIC,"The SDK automatically detects the surface at the camera centre");Check(saved.TrackingCamera.gameObject.activeInHierarchy && saved.PlaneFinder.gameObject.activeInHierarchy && saved.PlaneFinder.enabled,"Ground Plane components load together before engine startup");Check(!saved.TrackingCamera.GetComponent<Vuforia.VuforiaBehaviour>().enabled && !saved.TrackingCamera.enabled,"Vuforia camera remains stopped until camera permission is granted");Check(!saved.PlaneFinder.GetComponent<Vuforia.AnchorInputListenerBehaviour>(),"Raw screen taps cannot bypass PLACE ENGINE or activate through UI");
#endif
                Check(!overlay.PlacementMarkerVisible && !overlay.PlacementReady,"No square or placement is shown until Vuforia detects a real surface");Check(overlay.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text=="Move your phone"),"Scanning explains how to move the phone");session.Place();Check(!session.AllowsInteraction,"The scanning guide cannot place content without a valid surface");Inside(RectOf(overlay,"AR status"),"Scanning card");Capture(scene+"-portrait-scan",tracking.TrackingCamera);
                session.Rescan();yield return null;Check(tracking.rescans==1 && !overlay.PlacementMarkerVisible,"Rescan retries the provider and clears the placement target");
                tracking.surface=true;yield return null;yield return null;Check(overlay.PlacementReady && overlay.PlacementMarkerVisible,"A real surface enables the blue placement square");Canvas.ForceUpdateCanvases();var square=overlay.GetComponentInChildren<ARPlacementRing>();Check(square.canvasRenderer && square.canvasRenderer.GetMesh() && square.canvasRenderer.GetMesh().vertexCount==32,"The detected square has a rendered four-sided outline");Inside(RectOf(overlay,"Detected surface square"),"Detected square");Separate(RectOf(overlay,"AR status"),RectOf(overlay,"Detected surface square"),"Surface square and controls");Capture(scene+"-surface-found",tracking.TrackingCamera);session.Place();yield return null;yield return null;
                Check(session.AllowsInteraction && !overlay.PlacementCircleVisible,"Placement hides the scan marker and enables the engine");
                foreach(var size in new[]{new Vector2Int(1080,1920),new Vector2Int(1080,2400),new Vector2Int(1920,1080)})
                {
                    yield return Size(size.x,size.y);Canvas.ForceUpdateCanvases();var mobile=session.GetComponent<MobileAssemblyController>();Component host=mobile?(Component)mobile:session.GetComponent<MobileEngineDisplay>();string header=mobile?"Assembly header":"Display header",caption=mobile?"Assembly captions":"Display captions";
                    Inside(RectOf(host,header),"Phone header");Inside(RectOf(host,caption),"Phone caption");Check(!RectOf(overlay,"AR status").gameObject.activeInHierarchy,"Stable AR hides the redundant status toolbar; reposition and phone view live in settings");Separate(RectOf(host,header),RectOf(host,caption),"Header and captions");
                    if(mobile){Separate(RectOf(host,"Chapter menu action"),RectOf(host,"Settings action"),"Chapter and settings actions");Check(!RectOf(host,"Home action").gameObject.activeInHierarchy,"Main menu is tucked into phone settings");Check(mobile.GetComponentsInChildren<WorkshopCard>().Count(c=>c.transform.parent==RectOf(host,header))==2,"Phone assembly header shows only two primary controls");}
                    Check(tracking.TrackingCamera.rect==new Rect(0,0,1,1),"AR camera stays full screen in "+size.x+" x "+size.y);Capture(scene+"-"+size.x+"x"+size.y,tracking.TrackingCamera);
                }
                session.Reposition();tracking.surface=false;yield return null;Check(!overlay.PlacementMarkerVisible && !session.AllowsInteraction,"Reposition returns to movement guidance until another surface is detected");
                yield return Size(1080,1920);
            }
            ExperienceMenu.Launch("MobileAssemblyARFallback");yield return Wait(()=>FindAnyObjectByType<MobileAssemblyController>()?.Ready==true,"Standard phone assembly opens");NoPractice(FindAnyObjectByType<MobileAssemblyController>().gameObject,"Phone fallback");
            ExperienceMenu.LaunchMobileWorkshop();yield return Wait(()=>AssemblyManager.Instance && FindAnyObjectByType<PlayerAssemblyController>()!=null,"Walkable phone workshop opens");yield return null;yield return null;NoPractice(AssemblyManager.Instance.gameObject,"Walkable phone workshop");
            ExperienceMenu.GoHome();yield return null;ExperienceMenu.Launch("AssemblyWorkshop");yield return Wait(()=>AssemblyManager.Instance?.GetComponent<WorkshopPractice>()?.Active==true,"PC retains Chapter 0 practice");
        }
    }
}
