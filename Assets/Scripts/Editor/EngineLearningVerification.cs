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
using UnityEngine.EventSystems;

namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class EngineLearningVerification
    {
        static EngineLearningVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch(){SessionState.SetBool("Learning.Verify",true);EditorSceneManager.OpenScene(MobileEngineDisplayBuilder.ScenePath,OpenSceneMode.Single);EditorApplication.isPlaying=true;}
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("Learning.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){var go=new GameObject("Learning verification");UnityEngine.Object.DontDestroyOnLoad(go);go.AddComponent<EngineLearningVerificationRunner>();}
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("Learning.Verify",false);EditorApplication.Exit(SessionState.GetBool("Learning.Result",false)?0:1);}
        }
    }
    public sealed class EngineLearningVerificationRunner:MonoBehaviour
    {
        [Serializable] class Report {public bool passed;public string[] checks;public string error;}
        readonly List<string> checks=new List<string>();
        IEnumerator Start()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";
            while(stack.Count>0){bool more=false;object step=null;try{more=stack.Peek().MoveNext();if(more)step=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();break;}if(!more){stack.Pop();continue;}if(step is IEnumerator nested){stack.Push(nested);continue;}yield return step;}
            Directory.CreateDirectory("Tools/Verification");File.WriteAllText("Tools/Verification/learning-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,checks=checks.ToArray(),error=error},true));
            SessionState.SetBool("Learning.Result",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Scenario()
        {
            yield return null;yield return null;
            var display=FindAnyObjectByType<MobileEngineDisplay>();Assert(display && display.Ready,"Display scene starts with its complete engine");
            var recipe=display.Recipe;var cues=EngineLearningContent.Data.steps;
            var coverage=cues.SelectMany(c=>c.partIds).ToArray();
            Assert(coverage.Length==recipe.parts.Count && coverage.Distinct().Count()==recipe.parts.Count && recipe.parts.All(p=>coverage.Contains(p.id)),"Narration cues cover every recipe item exactly once");
            Assert(EngineLearningContent.Data.details.Count==recipe.parts.Count && EngineLearningContent.Data.details.All(d=>!string.IsNullOrEmpty(d.description)),"Every component and carrier has a part description");
            Assert(!FindAnyObjectByType<PlayerAssemblyController>() && !FindAnyObjectByType<AssemblyManager>(),"Mobile display is independent of workshop movement and physics");
            Assert(display.VisiblePartCount==recipe.parts.Count-recipe.subAssemblies.Count && display.StepCount==recipe.parts.Count,"Timeline includes every playable physical mesh and carrier operation");
            var order=EngineLearningContent.Timeline(recipe);var indices=order.Select((p,i)=>(p.id,i)).ToDictionary(p=>p.id,p=>p.i);
            Assert(order.All(p=>AssemblyRecipe.Dependencies(p).All(id=>recipe.parts.First(d=>d.id==id).isBase || indices[id]<indices[p.id])),"Timeline follows every assembly dependency");
            var end=display.EngineRoot.GetComponentsInChildren<MeshFilter>().Where(f=>recipe.parts.Any(p=>p.id==f.name)).ToDictionary(f=>f.name,f=>display.EngineRoot.InverseTransformPoint(f.transform.position));
            display.SetTimeline(0);Assert(display.VisiblePartCount==0,"Timeline start has no installed components");
            display.SetTimeline(.42f);var midway=display.EngineRoot.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).Select(r=>r.name).OrderBy(s=>s).ToArray();
            Assert(midway.Length>1 && midway.Length<recipe.parts.Count-recipe.subAssemblies.Count,"Scrubbing assembles only the parts reached by the timeline");
            display.SetTimeline(.91f);display.SetTimeline(.42f);Assert(midway.SequenceEqual(display.EngineRoot.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).Select(r=>r.name).OrderBy(s=>s)),"Reverse scrubbing reconstructs the same component state deterministically");
            Assert(InView(display),"All intermediate assembly geometry fits the engine viewport");
            var piston=recipe.subAssemblies.First(g=>g.id.Contains("Cyl01"));int carrierIndex=indices[piston.rootPartId];
            display.SetTimeline((carrierIndex-.02f)/display.StepCount);
            var fixture=display.EngineRoot.Find(piston.rootPartId+" display fixture");Assert(Vector3.Distance(fixture.localPosition,piston.benchPosition)<.001f,"Completed piston remains on the bench before its carrier step");
            display.SetTimeline((carrierIndex+.5f)/display.StepCount);var carrier=recipe.parts.First(p=>p.id==piston.rootPartId);
            Assert(Vector3.Distance(fixture.localPosition,piston.benchPosition)>.01f && Vector3.Distance(fixture.localPosition,carrier.targetPosition)>.01f,"Carrier step smoothly moves the whole piston unit into the engine");
            display.SetTimeline(1);Assert(end.All(pair=>Vector3.Distance(pair.Value,display.EngineRoot.InverseTransformPoint(display.EngineRoot.GetComponentsInChildren<MeshFilter>().First(f=>f.name==pair.Key).transform.position))<.001f),"Full scrub restores every original assembled pose");
            var scrubber=display.GetComponentInChildren<AssemblyTimelineScrubber>();
            var rail=(RectTransform)scrubber.transform;var point=RectTransformUtility.WorldToScreenPoint(null,rail.TransformPoint(new Vector3(Mathf.Lerp(rail.rect.xMin,rail.rect.xMax,.25f),0,0)));
            scrubber.OnPointerDown(new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left});
            Assert(Mathf.Abs(display.TimelinePosition-.25f)<.02f,"Custom slider receives pointer input and changes the assembly");
            scrubber.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=Vector2.up});Assert(display.TimelinePosition>.25f,"Timeline accepts scroll wheel input");
            display.SetTimeline(0);display.TogglePlayback();yield return null;yield return null;Assert(display.TimelinePosition>0,"Timelapse playback advances the assembly");display.TogglePlayback();
            display.SetTimeline(1);var beforeRotation=display.EngineRoot.rotation;display.RotateModel(new Vector2(55,20));Assert(Quaternion.Angle(beforeRotation,display.EngineRoot.rotation)>1,"One-finger gesture path rotates the display model");
            var before=display.EngineRoot.position;display.PanModel(new Vector2(25,15));Assert(Vector3.Distance(before,display.EngineRoot.position)>.001f,"Two-finger gesture path pans in the camera plane");
            float fov=display.ViewCamera.fieldOfView,dist=Vector3.Distance(display.ViewCamera.transform.position,display.EngineRoot.position);display.ZoomModel(1.5f);Assert(Vector3.Distance(display.ViewCamera.transform.position,display.EngineRoot.position)<dist && display.ViewCamera.fieldOfView==fov,"Pinch zoom moves the display camera closer");
            display.ResetView();display.SetTimeline(1);Physics.SyncTransforms();
            var camera=display.ViewCamera;bool tapped=false;
            for(int x=2;x<9 && !tapped;x++)for(int y=2;y<9 && !tapped;y++){var sample=new Vector2(Mathf.Lerp(camera.pixelRect.xMin,camera.pixelRect.xMax,x/10f),Mathf.Lerp(camera.pixelRect.yMin,camera.pixelRect.yMax,y/10f));tapped=display.TapPart(sample);}
            Assert(tapped && !string.IsNullOrEmpty(display.SelectedPartId),"Tapping visible CAD geometry opens a part card");
            var info=display.GetComponentsInChildren<RectTransform>().First(r=>r.name=="Part information card");var corners=new Vector3[4];info.GetWorldCorners(corners);
            Assert(corners.Max(p=>p.y)<=camera.pixelRect.yMin+1,"Part card is outside the reserved engine viewport");
            Assert(display.GetComponentsInChildren<UnityEngine.UI.Button>().Length==0 && display.GetComponentsInChildren<UnityEngine.UI.Slider>().Length==0,"Display controls use custom cards and custom timeline graphics");
            Capture(display.GetComponentInChildren<Canvas>(),camera,"engine-display",1080,1920);
            display.SetTimeline(.57f);Capture(display.GetComponentInChildren<Canvas>(),camera,"engine-display-timeline",1080,1920);
            SceneManager.LoadScene("AssemblyWorkshop");yield return null;yield return null;yield return null;
            var manager=AssemblyManager.Instance;manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);yield return null;
            var captions=manager.GetComponentInChildren<AssemblyCaptionPresenter>();Assert(captions,"PC workshop creates caption presenter");
            manager.SelectChapter(2);manager.AssemblyRoot.rotation=Quaternion.Euler(180,0,0);
            foreach(var p in manager.Recipe.parts.Where(p=>p.chapterId==manager.CurrentChapterId && p.order<303))manager.FindPart(p.id).InstallImmediate(manager.FindPart(p.id).TargetSocket);
            yield return null;yield return null;string cueId=captions.CurrentCueId;Assert(captions.CaptionText.Contains("bedplate bolts"),"PC shows a single instruction for the bedplate bolt group");
            var bolt=manager.NextHintPart();bolt.InstallImmediate(bolt.TargetSocket);yield return null;yield return null;
            Assert(captions.CurrentCueId==cueId,"Grouped fastener caption stays until the group is complete");
            foreach(var p in manager.Recipe.parts.Where(p=>p.chapterId==manager.CurrentChapterId && p.order==303 && !manager.IsPlaced(p.id)))manager.FindPart(p.id).InstallImmediate(manager.FindPart(p.id).TargetSocket);
            yield return null;yield return null;Assert(captions.CurrentCueId!=cueId && captions.CaptionText.Contains("oil sump"),"Caption advances to the sump bench build after the complete fastener group");
            manager.SelectChapter(3);var head=manager.FindPart("401_Cylinder_Head_01");head.InstallImmediate(head.TargetSocket);yield return null;yield return null;
            Assert(captions.CaptionText.Contains("exhaust valves") && captions.CaptionText.Contains("combustion-chamber side"),"Head caption identifies the current valve family and insertion side before spring stacks");
            manager.GetComponent<AssemblyHint>().Toggle();yield return null;
            Assert(!manager.GetComponent<AssemblyHint>().Enabled && captions.CaptionText.Contains("exhaust valves"),"Assembly instructions remain available with visual hints disabled");
            var texts=captions.GetComponentsInChildren<TextMeshProUGUI>();Assert(texts.Any(t=>(t.fontStyle&FontStyles.Bold)!=0) && texts.All(t=>t.GetComponent<UnityEngine.UI.Shadow>() && !t.raycastTarget),"Video captions have bold text, shadows and do not block interaction");
            Capture(manager.GetComponentInChildren<Canvas>(),Camera.main,"assembly-captions",1440,900);
            SceneManager.LoadScene("MobileAssembly");yield return null;yield return null;yield return null;yield return null;
            manager=AssemblyManager.Instance;var mobile=manager.GetComponent<MobileAssemblyController>();captions=manager.GetComponentInChildren<AssemblyCaptionPresenter>();
            Assert(captions && captions.CaptionText.Length>0,"Mobile assembly uses the same narration captions");
            var captionRect=(RectTransform)captions.transform;captionRect.GetWorldCorners(corners);
            Assert(corners.Min(p=>p.y)>=mobile.ViewCamera.pixelRect.yMax-1,"Mobile captions stay above the engine viewport");
            Capture(manager.GetComponentInChildren<Canvas>(),mobile.ViewCamera,"mobile-assembly-captions",1080,1920);
        }
        static void Capture(Canvas canvas,Camera camera,string name,int width,int height)
        {
            var mode=canvas.renderMode;var oldRect=camera.rect;var oldTarget=camera.targetTexture;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.5f;
            var rt=new RenderTexture(width,height,24);rt.Create();camera.targetTexture=rt;camera.rect=new Rect(0,0,1,1);Canvas.ForceUpdateCanvases();
            var display=UnityEngine.Object.FindAnyObjectByType<MobileEngineDisplay>();if(display)display.SendMessage("UpdateCamera");
            foreach(var t in canvas.GetComponentsInChildren<TextMeshProUGUI>())t.ForceMeshUpdate();
            WorkshopPresentationBuilder.Render(camera,"Tools/Verification/"+name+".png",width,height);
            camera.targetTexture=oldTarget;camera.rect=oldRect;rt.Release();UnityEngine.Object.Destroy(rt);canvas.renderMode=mode;Canvas.ForceUpdateCanvases();
            if(display)display.SendMessage("UpdateCamera");
        }
        static bool InView(MobileEngineDisplay display)
        {
            foreach(var renderer in display.EngineRoot.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled))
            {
                var b=renderer.bounds;
                for(int i=0;i<8;i++){var p=display.ViewCamera.WorldToScreenPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(p.z<=0 || !display.ViewCamera.pixelRect.Contains(p))return false;}
            }
            return true;
        }
        void Assert(bool result,string text){if(!result)throw new Exception(text);checks.Add(text);}
    }
}
