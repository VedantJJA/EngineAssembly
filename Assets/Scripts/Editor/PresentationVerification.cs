using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class PresentationVerification
    {
        static PresentationVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch()
        {
            if(!Application.isBatchMode || !Application.dataPath.Replace('\\','/').EndsWith("/Tools/Verification/UnityProject/Assets"))throw new InvalidOperationException("Use the isolated verification project.");
            foreach(var path in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/UI/ChapterImages/Light"}).Select(AssetDatabase.GUIDToAssetPath))AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            SessionState.SetBool("Presentation.Verify",true);EditorSceneManager.OpenScene(ExperienceMenuBuilder.ScenePath,OpenSceneMode.Single);EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("Presentation.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Presentation verification").AddComponent<PresentationVerificationRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("Presentation.Verify",false);EditorApplication.Exit(SessionState.GetBool("Presentation.Result",false)?0:1);}
        }
    }
    public sealed class PresentationVerificationRunner:MonoBehaviour
    {
        [Serializable] sealed class Report{public bool passed;public string[] checks;public string error;}
        readonly List<string> checks=new List<string>();
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";
            while(stack.Count>0){bool more=false;object item=null;try{more=stack.Peek().MoveNext();if(more)item=stack.Peek().Current;}catch(Exception e){error=e.ToString();break;}if(!more){stack.Pop();continue;}if(item is IEnumerator nested)stack.Push(nested);else yield return item;}
            File.WriteAllText("Tools/Verification/presentation-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,checks=checks.ToArray(),error=error},true));SessionState.SetBool("Presentation.Result",error.Length==0);EditorApplication.isPlaying=false;
        }
        void Check(bool value,string description){if(!value)throw new Exception(description);checks.Add(description);}
        void CheckMaterials()
        {
            var physical=FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(r=>r.name.Length>4 && char.IsDigit(r.name[0]) && r.name[3]=='_' && r.GetComponent<MeshFilter>()).ToArray();
            Check(physical.Length>0 && physical.All(r=>r.sharedMaterials.All(m=>m && m.shader.name=="EngineAssembly/Surface Triplanar" && m.GetTexture("_BaseMap") && m.GetTexture("_RoughnessMap"))),"Every labelled model renderer receives the textured material palette");
        }
        IEnumerator Scenario()
        {
            yield return null;yield return null;
            var menu=FindAnyObjectByType<ExperienceMenu>();
            Check(menu && Camera.main.backgroundColor.r<.75f && Camera.main.backgroundColor.r>.3f,"Main menu uses a subdued garage-grey background");
            Check(menu.GetComponentsInChildren<WorkshopCard>().All(c=>c.normal.r<.9f && c.GetComponent<UnityEngine.UI.Image>().sprite),"Custom cards use the subdued garage palette");
            Capture(menu.GetComponentInChildren<Canvas>(),Camera.main,"main-menu-light");menu.Show(ExperienceMenu.Page.Experiences);yield return null;
            Capture(menu.GetComponentInChildren<Canvas>(),Camera.main,"experiences-light");menu.Show(ExperienceMenu.Page.Options);yield return null;
            Capture(menu.GetComponentInChildren<Canvas>(),Camera.main,"options-light");CheckMaterials();
            yield return SceneManager.LoadSceneAsync("AssemblyWorkshop");yield return null;yield return null;
            var manager=AssemblyManager.Instance;var ui=manager.GetComponent<AssemblyWorkshopUI>();
            var images=ui.GetComponentsInChildren<CroppedArtwork>().Select(c=>c.GetComponent<UnityEngine.UI.RawImage>()).ToArray();
            Check(images.Length>=9 && images.All(i=>i.texture && i.texture.width==960),"PC chapter cards show the new complete chapter photographs");
            foreach(var image in images)
            {
                var size=image.rectTransform.rect.size;float sx=size.x/(image.texture.width*image.uvRect.width),sy=size.y/(image.texture.height*image.uvRect.height);
                Check(Mathf.Abs(sx-sy)<.005f,"Chapter artwork retains its source proportions in the visible frame");
            }
            Check(images.All(i=>i.GetComponentInParent<UnityEngine.UI.Mask>() && i.uvRect.width<=1 && i.uvRect.height<=1),"Chapter photos are cropped inside rounded masks");
            Capture(ui.GetComponentInChildren<Canvas>(),Camera.main,"chapters-light");ui.Open(AssemblyWorkshopUI.Page.Settings);yield return null;Capture(ui.GetComponentInChildren<Canvas>(),Camera.main,"settings-light");ui.Open(AssemblyWorkshopUI.Page.Pause);yield return null;Capture(ui.GetComponentInChildren<Canvas>(),Camera.main,"pause-light");
            Check(ui.GetComponentsInChildren<UnityEngine.UI.RawImage>().Any(i=>i.transform.parent && i.transform.parent.name=="Chapter photograph" && i.texture),"Pause screen shows the current chapter photograph");
            ui.SetOpen(false);FindAnyObjectByType<PlayerAssemblyController>().enabled=false;
            var subtitle=ui.GetComponentInChildren<AssemblyCaptionPresenter>();
            Check(subtitle && !subtitle.GetComponent<UnityEngine.UI.Image>() && subtitle.GetComponentsInChildren<TextMeshProUGUI>().All(t=>t.outlineWidth>0),"Assembly directions render as outlined subtitles without a backing panel");
            CheckLessonFit(subtitle);
            yield return null;Capture(ui.GetComponentInChildren<Canvas>(),Camera.main,"workshop-subtitles");
            var next=manager.NextHintPart();var assist=manager.GetComponent<AssemblyStepAssist>();Check(assist.NextStep() && next.IsSnapped && !next.IsBusy,"PC assistance finishes installation in the same call");yield return null;CheckMaterials();
            yield return SceneManager.LoadSceneAsync("MobileAssemblyARFallback");yield return null;yield return null;yield return null;
            manager=AssemblyManager.Instance;var phone=manager.GetComponent<MobileAssemblyController>();next=manager.NextHintPart();Check(!phone.TapTarget(phone.ViewCamera.pixelRect.center),"Mobile placement still requires selecting a component");Check(manager.GetComponent<AssemblyStepAssist>().NextStep() && next.IsSnapped && !next.IsBusy,"Mobile assistance places immediately");yield return null;yield return null;Check(!phone.VisiblePartIds.Contains(next.PartId),"Mobile card is removed as soon as its part is installed");CheckMaterials();
            Check(!phone.GetComponentInChildren<AssemblyCaptionPresenter>().GetComponent<UnityEngine.UI.Image>(),"Mobile directions do not cover the model with a panel");
            CheckLessonFit(phone.GetComponentInChildren<AssemblyCaptionPresenter>());
            Capture(phone.GetComponentInChildren<Canvas>(),phone.ViewCamera,"mobile-assembly-redesign");
            yield return SceneManager.LoadSceneAsync("MobileDisplayARFallback");yield return null;yield return null;
            var display=FindAnyObjectByType<MobileEngineDisplay>();var timeline=EngineLearningContent.Timeline(display.Recipe);CheckMaterials();
            Check(display.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="Timeline dock" && r.rect.height<320),"Display timeline uses a compact information dock");
            var wanted=new[]{"Crankshaft","Wrist_Pin","Stem_Seal","Keeper","Spring_Seat","Cylinder_Head","Bolt"};
            foreach(var keyword in wanted)
            {
                int index=timeline.FindIndex(p=>p.id.Contains(keyword) && !display.Recipe.subAssemblies.Any(g=>g.rootPartId==p.id));if(index<0)continue;
                var part=timeline[index];var target=display.EngineRoot.GetComponentsInChildren<MeshFilter>().First(f=>f.name==part.id).transform;
                display.SetTimeline((index+.01f)/timeline.Count);var start=target.localPosition;var rotation=target.localRotation;
                Check((start-part.targetPosition).magnitude>.001f && (start-part.targetPosition).magnitude<=.161f,"Display component appears just outside its mounting position");
                display.SetTimeline((index+.5f)/timeline.Count);Check(Vector3.Distance(target.localPosition,part.targetPosition)<Vector3.Distance(start,part.targetPosition),"Display component follows a short insertion stroke");
                Check(Quaternion.Angle(rotation,target.localRotation)<.01f,"Display component arrives already oriented");
                display.SetTimeline((index+1f)/timeline.Count);Check(Vector3.Distance(target.localPosition,part.targetPosition)<.0001f,"Display stroke ends at the authored CAD pose");
            }
            var group=display.Recipe.subAssemblies.First();int carrierIndex=timeline.FindIndex(p=>p.id==group.rootPartId);var carrier=timeline[carrierIndex];
            display.SetTimeline((carrierIndex+.01f)/timeline.Count);var fixture=display.EngineRoot.GetComponentsInChildren<Transform>().First(t=>t.name==group.rootPartId+" display fixture");
            Check(Vector3.Distance(fixture.localPosition,carrier.targetPosition)<=.181f,"Completed display subassembly appears beside the engine instead of travelling from its bench");
            display.SetTimeline(1);Capture(display.GetComponentInChildren<Canvas>(),display.ViewCamera,"display-materials-light");
            Check(display.VisiblePartCount==display.Recipe.parts.Count-display.Recipe.subAssemblies.Count,"Complete timelapse retains every physical component");
            display.SetTimeline(0);Check(display.VisiblePartCount==display.Recipe.parts.Count(p=>p.isBase && !display.Recipe.subAssemblies.Any(g=>g.rootPartId==p.id)),"Reversing the timeline hides every uninstalled component");
        }
        void CheckLessonFit(AssemblyCaptionPresenter presenter)
        {
            var caption=presenter.transform.Find("Caption").GetComponent<TextMeshProUGUI>();
            string previous=caption.text;
            foreach(var lesson in EngineLearningContent.Data.steps)
            {
                Check(!string.IsNullOrWhiteSpace(lesson.purpose),"Lesson explains purpose: "+lesson.id);
                caption.text=lesson.caption;caption.ForceMeshUpdate();
                Check(!caption.isTextOverflowing,"Instruction fits its subtitle area: "+lesson.id);
            }
            caption.text=previous;caption.ForceMeshUpdate();
        }
        void Capture(Canvas canvas,Camera camera,string name)
        {
            var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;var oldPlane=canvas.planeDistance;var rect=camera.rect;var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();var enabled=scaler.enabled;var factor=canvas.scaleFactor;var oldTarget=camera.targetTexture;
            var target=new RenderTexture(1440,900,24);target.Create();camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);scaler.enabled=false;canvas.scaleFactor=1;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=Mathf.Max(.5f,camera.nearClipPlane+.1f);Canvas.ForceUpdateCanvases();
            WorkshopPresentationBuilder.Render(camera,"Tools/Verification/"+name+".png",1440,900);
            canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldPlane;camera.targetTexture=oldTarget;camera.rect=rect;canvas.scaleFactor=factor;scaler.enabled=enabled;target.Release();Destroy(target);Canvas.ForceUpdateCanvases();
        }
    }
}
