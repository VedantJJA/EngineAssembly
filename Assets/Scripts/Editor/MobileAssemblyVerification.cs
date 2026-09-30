using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace EngineAssembly.Editor {
[InitializeOnLoad] public static class MobileAssemblyVerification {
 static MobileAssemblyVerification()=>EditorApplication.playModeStateChanged+=Changed;
 public static void RunBatch(){
 SessionState.SetBool("Mobile.Verify",true);
 EditorSceneManager.OpenScene(MobileAssemblyBuilder.ScenePath,OpenSceneMode.Single);
 var m=UnityEngine.Object.FindAnyObjectByType<AssemblyManager>();
 var renderers=m.AssemblyParts.SelectMany(p=>p.GetComponentsInChildren<MeshRenderer>()).ToArray();
 var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
 m.GetComponent<MobileAssemblyController>().Configure(Camera.main,bounds.extents.magnitude,AssetDatabase.LoadAssetAtPath<MobilePartArtwork>("Assets/UI/MobilePartArtwork.asset"),bounds.center);
 EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
 EditorApplication.isPlaying=true;
 }
 static void Changed(PlayModeStateChange s){
 if(!SessionState.GetBool("Mobile.Verify",false))return;
 if(s==PlayModeStateChange.EnteredPlayMode)new GameObject("Mobile verification").AddComponent<MobileAssemblyVerificationRunner>();
 if(s==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("Mobile.Verify",false);EditorApplication.Exit(SessionState.GetBool("Mobile.Result",false)?0:1);}
 }
}
public sealed class MobileAssemblyVerificationRunner:MonoBehaviour {
 [Serializable] class Report{public bool passed;public string[] checks;public string error;}
 List<string> checks=new List<string>();
 IEnumerator Start(){
 var scenario=Scenario();string error="";
 while(true){bool more=false;object step=null;try{more=scenario.MoveNext();if(more)step=scenario.Current;}catch(Exception e){error=e.ToString();}
 if(!more||error.Length>0)break;yield return step;}
 Directory.CreateDirectory("Tools/Verification");
 File.WriteAllText("Tools/Verification/mobile-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,checks=checks.ToArray(),error=error},true));
 SessionState.SetBool("Mobile.Result",error.Length==0);EditorApplication.isPlaying=false;
 }
 IEnumerator Scenario(){
 yield return null;yield return null;yield return null;
 var m=FindAnyObjectByType<AssemblyManager>();var ui=m.GetComponent<MobileAssemblyController>();
 Assert(ui && m.Difficulty==SnapDifficulty.Easy,"Mobile uses Easy placement");
 Assert(!FindAnyObjectByType<PlayerAssemblyController>() && !m.GetComponent<AssemblyWorkshopUI>(),"No walking controller or desktop interface");
 Assert(FindObjectsByType<EventSystem>().Length==1,"One touch EventSystem");
 Assert(!FindAnyObjectByType<Button>(),"Custom cards without stock buttons");
 Assert(ui.VisiblePartIds.Count==m.Recipe.parts.Count(p=>p.chapterId==m.CurrentChapterId&&!m.IsPlaced(p.id)),"Cards cover every uninstalled current chapter part");
 var ordered=ui.VisiblePartIds.Select(id=>m.FindPart(id).Definition.order).ToArray();
 Assert(ordered.SequenceEqual(ordered.OrderBy(x=>x)),"Cards sorted by assembly priority");
 var art=AssetDatabase.LoadAssetAtPath<MobilePartArtwork>("Assets/UI/MobilePartArtwork.asset");
 Assert(m.AssemblyParts.All(p=>art.Find(p.PartId) || art.Find(m.CarrierDefinition(p.PartId)?.previewPartId)),"Every component and subassembly has thumbnail artwork");
 Assert(!ui.TapTarget(ui.ViewCamera.pixelRect.center),"Target tap requires card selection");
 var blocked=m.AssemblyParts.FirstOrDefault(p=>p.ChapterId==m.CurrentChapterId&&!p.IsSnapped&&!m.CanInstall(p,p.TargetSocket,out _));
 if(blocked)Assert(!ui.SelectPart(blocked.PartId),"Later priority card cannot bypass assembly order");
 var part=m.NextHintPart();Assert(ui.SelectPart(part.PartId)&&part.IsSelected,"Selecting a card picks up its actual component");
 var initial=m.AssemblyRoot.rotation;ui.RotateEngine(new Vector2(90,50));yield return null;
 Assert(Quaternion.Angle(initial,m.AssemblyRoot.rotation)>5,"Engine rotates with selected ghost");
 bool placed=false;
 for(int turn=0;turn<16&&!placed;turn++){
 var rect=ui.ViewCamera.pixelRect;
 for(int y=1;y<36&&!placed;y++)for(int x=1;x<36&&!placed;x++)placed=ui.TapTarget(new Vector2(rect.xMin+rect.width*x/36,rect.yMin+rect.height*y/36));
 if(!placed){ui.RotateEngine(new Vector2(75,turn%4==3?90:0));yield return null;}
 }
 Assert(placed,"Tapping visible imported CAD ghost starts installation after rotation");
 yield return new WaitForSeconds(.5f);yield return null;
 Assert(part.IsSnapped&&!ui.VisiblePartIds.Contains(part.PartId),"Installation removes its card");
 Assert(Vector3.Distance(part.transform.position,part.GetTargetPose(part.TargetSocket).position)<.002f,"Installed component matches rotated socket");
 ui.ChooseChapter(3);yield return null;
 Assert(m.Recipe.parts.Where(p=>m.Recipe.chapters.FindIndex(c=>c.id==p.chapterId)<3).All(p=>m.IsPlaced(p.id)),"Chapter selection assembles all earlier sections");
 Assert(ui.SelectedPart==null && ui.VisiblePartIds.All(id=>m.FindPart(id).ChapterId==m.CurrentChapterId),"Changing chapters clears selection and replaces card strip");
 yield return Capture(ui,"mobile-portrait",1080,1920);
 yield return Capture(ui,"mobile-landscape",1920,1080);
 }
 IEnumerator Capture(MobileAssemblyController ui,string name,int width,int height){
 var canvas=ui.GetComponentInChildren<Canvas>();var camera=ui.ViewCamera;
 var rect=camera.rect;var mode=canvas.renderMode;var mask=camera.cullingMask;var position=camera.transform.position;
 float scale=Mathf.Sqrt(width/1080f*height/1920f);int bottom=Mathf.RoundToInt(410*scale),top=height-Mathf.RoundToInt(200*scale);
 var world=new RenderTexture(width,top-bottom,24);world.Create();camera.targetTexture=world;camera.rect=new Rect(0,0,1,1);
 var settings=new SerializedObject(ui);float radius=settings.FindProperty("engineRadius").floatValue;
 float vertical=camera.fieldOfView*Mathf.Deg2Rad*.5f,horizontal=Mathf.Atan(Mathf.Tan(vertical)*width/(top-bottom));
 var center=ui.GetComponent<AssemblyManager>().AssemblyRoot.TransformPoint(settings.FindProperty("engineCenter").vector3Value);
 camera.transform.position=center+Vector3.back*(radius/Mathf.Sin(Mathf.Min(vertical,horizontal))*1.12f);
 UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=world});
 var background=new GameObject("Capture world",typeof(RectTransform),typeof(RawImage));background.transform.SetParent(canvas.transform,false);background.transform.SetAsFirstSibling();
 var image=background.GetComponent<RawImage>();image.texture=world;image.raycastTarget=false;
 var r=(RectTransform)background.transform;r.anchorMin=new Vector2(0,(float)bottom/height);r.anchorMax=new Vector2(1,(float)top/height);r.offsetMin=r.offsetMax=Vector2.zero;
 var layers=canvas.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>t.gameObject.layer);foreach(var t in layers.Keys)t.gameObject.layer=31;
 var rt=new RenderTexture(width,height,24);rt.Create();camera.targetTexture=rt;camera.cullingMask=1<<31;
 canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.15f;
 Canvas.ForceUpdateCanvases();
 foreach(var text in canvas.GetComponentsInChildren<TMPro.TextMeshProUGUI>())text.ForceMeshUpdate();
 yield return null;
 Canvas.ForceUpdateCanvases();
 WorkshopPresentationBuilder.Render(camera,"Tools/Verification/"+name+".png",width,height);
 foreach(var pair in layers)pair.Key.gameObject.layer=pair.Value;DestroyImmediate(background);
 camera.targetTexture=null;camera.rect=rect;camera.cullingMask=mask;camera.transform.position=position;canvas.renderMode=mode;rt.Release();Destroy(rt);world.Release();Destroy(world);Canvas.ForceUpdateCanvases();
 }
 void Assert(bool v,string message){if(!v)throw new Exception(message);checks.Add(message);}
}
}
