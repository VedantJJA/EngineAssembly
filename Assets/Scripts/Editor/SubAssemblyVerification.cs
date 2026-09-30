using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif
namespace EngineAssembly.Editor {
[InitializeOnLoad] public static class SubAssemblyVerification {
 static SubAssemblyVerification()=>EditorApplication.playModeStateChanged+=Changed;
 public static void RunBatch(){
 if(!Application.isBatchMode)throw new Exception("Isolated batch verification only.");
 SessionState.SetBool("SubAssembly.Verify",true);
 EditorSceneManager.OpenScene(MobileAssemblyBuilder.ScenePath,OpenSceneMode.Single);EditorApplication.isPlaying=true;
 }
 static void Changed(PlayModeStateChange s){
 if(!SessionState.GetBool("SubAssembly.Verify",false))return;
 if(s==PlayModeStateChange.EnteredPlayMode)new GameObject("Subassembly tests").AddComponent<SubAssemblyVerificationRunner>();
 if(s==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("SubAssembly.Verify",false);EditorApplication.Exit(SessionState.GetBool("SubAssembly.Result",false)?0:1);}
 }
}
public class SubAssemblyVerificationRunner:MonoBehaviour {
 [Serializable] class Report{public bool passed;public string[] checks;public string error;}
 List<string> checks=new List<string>();AssemblyManager m;MobileAssemblyController ui;
 IEnumerator Start(){
 var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";
 while(stack.Count>0){
 bool more=false;object step=null;
 try{more=stack.Peek().MoveNext();if(more)step=stack.Peek().Current;}catch(Exception e){error=e.ToString();break;}
 if(!more){stack.Pop();continue;}if(step is IEnumerator nested){stack.Push(nested);continue;}yield return step;
 }
 Directory.CreateDirectory("Tools/Verification");
 File.WriteAllText("Tools/Verification/subassembly-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,checks=checks.ToArray(),error=error},true));
 SessionState.SetBool("SubAssembly.Result",error.Length==0);EditorApplication.isPlaying=false;
 }
 IEnumerator Scenario(){
 yield return null;yield return null;yield return null;
 m=FindAnyObjectByType<AssemblyManager>();ui=m.GetComponent<MobileAssemblyController>();
 Assert(m.Recipe!=null && m.Recipe.Validate().Count==0,"Version 2 recipe validates");
 Assert(m.AssemblyParts.Count==325 && m.Recipe.subAssemblies.Count==7,"318 original components plus seven generated carriers");
 foreach(var p in m.AssemblyParts){var s=new SerializedObject(p);s.FindProperty("snapDuration").floatValue=0;s.ApplyModifiedPropertiesWithoutUndo();}
 var hint=m.GetComponent<AssemblyHint>();yield return null;
 Assert(hint.Enabled && hint.HighlightedPart,"Hints active by default");
 Assert(Resources.FindObjectsOfTypeAll<MeshRenderer>().Any(r=>r.gameObject.activeInHierarchy&&r.sharedMaterial&&r.sharedMaterial.HasProperty("_ZTest")&&r.sharedMaterial.GetInt("_ZTest")==8),"Default hints render through walls");
 // Run every step through runtime pickup and proximity snapping, not the editor bypass.
 var original=AssemblyRecipeStore.Load("ModelReview/three-cylinder-before-subassemblies.json");
 foreach(var difficulty in new[]{SnapDifficulty.Hard,SnapDifficulty.Medium,SnapDifficulty.Easy}){
 m.SetDifficulty(difficulty,false);
 int start=difficulty==SnapDifficulty.Hard?0:1,end=difficulty==SnapDifficulty.Hard?m.Recipe.chapters.Count:2;
 for(int chapter=start;chapter<end;chapter++){
 ui.ChooseChapter(chapter);yield return null;
 var ordered=m.Recipe.parts.Where(p=>p.chapterId==m.CurrentChapterId&&!p.isBase).OrderBy(p=>p.order).ThenBy(p=>p.id).ToArray();
 var late=m.FindPart(ordered.Last().id);
 Assert(!m.CanInstall(late,late.TargetSocket,out _)&&!late.BeginGrab(),difficulty+" chapter "+chapter+" blocks later-step pickup and snap");
 foreach(var d in ordered){
 if(d.orientation!=AssemblyOrientation.Any)m.AssemblyRoot.rotation=d.orientation==AssemblyOrientation.Upright?Quaternion.identity:Quaternion.Euler(180,0,0);
 var p=m.FindPart(d.id);
 if(!m.CanInstall(p,p.TargetSocket,out var reason))throw new Exception(d.id+" cannot install: "+reason);
 var group=m.CarrierDefinition(d.id);
 if(group!=null){
 if(!m.GroupComplete(group))throw new Exception("Incomplete carrier became eligible: "+d.id);
 if(!m.Members(group).All(member=>member.transform.IsChildOf(p.transform)))throw new Exception("Group children detached: "+d.id);
 }
 if(!p.BeginGrab())throw new Exception("Pickup rejected: "+d.id);
 var pose=p.GetTargetPose(p.TargetSocket);p.MoveHeld(pose.position,pose.rotation);
 bool snapped=p.SnapToSocket(p.TargetSocket);
 if(!snapped || !p.IsSnapped)throw new Exception("Snap failed: "+d.id+" "+p.RejectionReason);
 yield return null;
 if(group!=null){
 foreach(var member in m.Members(group)){
 var baseline=original.parts.First(x=>x.id==member.PartId);
 if(Vector3.Distance(member.transform.position,m.AssemblyRoot.TransformPoint(baseline.targetPosition))>.002f)throw new Exception("CAD pose changed: "+member.PartId);
 }
 }
 }
 Assert(m.ChapterComplete,difficulty+" completes chapter "+chapter+" without a deadlock");
 }
 }
 // Carrier cannot be acquired incomplete, and placement order still applies to UI.
 ui.ChooseChapter(1);m.SetDifficulty(SnapDifficulty.Easy,false);yield return null;
 var g=m.Recipe.subAssemblies.First(s=>s.id.Contains("Cyl01"));var carrier=m.FindPart(g.rootPartId);
 Assert(!ui.SelectPart(carrier.PartId),"Mobile cannot select incomplete piston unit");
 Assert(!ui.SelectPart("206_Connecting_Rod_Big_End_Cap_Cyl01_01"),"Rod cap cannot precede piston-unit installation");
 var first=m.NextHintPart();Assert(ui.SelectPart(first.PartId),"Mobile selects first bench component");
 yield return null;
 var camera=ui.ViewCamera;var guide=m.GetComponent<AssemblyEasyGuide>();
 Ray visible=new Ray();bool found=false;
 var rect=camera.pixelRect;
 for(int y=1;y<36&&!found;y++)for(int x=1;x<36&&!found;x++){
 var ray=camera.ScreenPointToRay(new Vector2(rect.xMin+rect.width*x/36,rect.yMin+rect.height*y/36));
 if(guide.Aim(ray,100)==first){visible=ray;found=true;}
 }
 Assert(found,"Visible CAD bench ghost can be targeted");
 var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
 float distance=Vector3.Distance(camera.transform.position,first.TargetSocket.SnapPosition)*.5f;
 wall.transform.position=visible.GetPoint(distance);wall.transform.rotation=Quaternion.LookRotation(visible.direction);wall.transform.localScale=new Vector3(5,5,.1f);Physics.SyncTransforms();
 Assert(!guide.Place(visible,100),"X-ray ghost cannot be placed through an occluder");
 wall.SetActive(false);Physics.SyncTransforms();Destroy(wall);
 Assert(guide.Place(visible,100),"Line-of-sight placement succeeds after removing occluder");
 yield return null;
 Assert(!ui.VisiblePartIds.Contains(first.PartId),"Placed bench component card disappears");
 // Pan is parallel to the camera's face plane and retains socket-relative poses.
 var target=m.NextHintPart();var frame=m.TargetFrame(target.Definition);var before=frame.position;var rotation=frame.rotation;
 ui.PanModel(new Vector2(50,35));var delta=frame.position-before;
 Assert(delta.magnitude>.001f&&Mathf.Abs(Vector3.Dot(delta,camera.transform.forward))<.0001f&&Quaternion.Angle(rotation,frame.rotation)<.001f,"Pan translates active subassembly in camera plane without rotating");
 Assert(Vector3.Distance(target.TargetSocket.SnapPosition,frame.TransformPoint(target.Definition.targetPosition))<.001f,"Panning keeps ghost sockets attached");
 var old=frame.rotation;ui.RotateEngine(new Vector2(40,25));Assert(Quaternion.Angle(old,frame.rotation)>1,"One-finger rotation rotates active subassembly");
#if ENABLE_INPUT_SYSTEM
 var touch=InputSystem.AddDevice<Touchscreen>();var center=camera.pixelRect.center;
 Action<int,UnityEngine.InputSystem.TouchPhase,Vector2> queue=(id,phase,point)=>InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=phase,position=point});
 Action update=()=>{InputSystem.Update();ui.SendMessage("Update");};
 var p1=center+Vector2.left*40;var p2=center+Vector2.right*40;
 queue(1,UnityEngine.InputSystem.TouchPhase.Began,p1);update();
 queue(2,UnityEngine.InputSystem.TouchPhase.Began,p2);update();
 before=frame.position;rotation=frame.rotation;int installed=m.ChapterInstalledCount;
 queue(1,UnityEngine.InputSystem.TouchPhase.Moved,p1+new Vector2(35,25));queue(2,UnityEngine.InputSystem.TouchPhase.Moved,p2+new Vector2(35,25));update();
 Assert(Vector3.Distance(before,frame.position)>.001f&&Quaternion.Angle(rotation,frame.rotation)<.001f,"Two actual touch contacts pan instead of rotating");
 queue(2,UnityEngine.InputSystem.TouchPhase.Ended,p2+new Vector2(35,25));update();
 before=frame.position;rotation=frame.rotation;
 queue(1,UnityEngine.InputSystem.TouchPhase.Moved,p1+new Vector2(90,45));update();
 queue(1,UnityEngine.InputSystem.TouchPhase.Ended,p1+new Vector2(90,45));update();
 Assert(Vector3.Distance(before,frame.position)<.001f&&Quaternion.Angle(rotation,frame.rotation)<.001f&&m.ChapterInstalledCount==installed,"Two-to-one contact transition cannot rotate or accidentally place");
 yield return null;
 queue(3,UnityEngine.InputSystem.TouchPhase.Began,center);update();yield return null;rotation=frame.rotation;
 queue(3,UnityEngine.InputSystem.TouchPhase.Moved,center+new Vector2(65,30));update();yield return null;queue(3,UnityEngine.InputSystem.TouchPhase.Ended,center+new Vector2(65,30));update();yield return null;
 Assert(Quaternion.Angle(rotation,frame.rotation)>1,"Single actual touch contact rotates the model");
 queue(4,UnityEngine.InputSystem.TouchPhase.Began,new Vector2(20,20));queue(5,UnityEngine.InputSystem.TouchPhase.Began,new Vector2(80,20));update();before=frame.position;
 queue(4,UnityEngine.InputSystem.TouchPhase.Moved,new Vector2(50,60));queue(5,UnityEngine.InputSystem.TouchPhase.Moved,new Vector2(110,60));update();
 Assert(Vector3.Distance(before,frame.position)<.001f,"Gestures beginning on card UI do not pan the model");
 queue(4,UnityEngine.InputSystem.TouchPhase.Ended,new Vector2(50,60));queue(5,UnityEngine.InputSystem.TouchPhase.Ended,new Vector2(110,60));update();InputSystem.RemoveDevice(touch);
#endif
 var playerObject=new GameObject("PC integration controls");
 var eye=new GameObject("PC test camera").AddComponent<Camera>();eye.transform.SetParent(playerObject.transform,false);eye.CopyFrom(camera);eye.transform.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);eye.enabled=false;
 var player=playerObject.AddComponent<PlayerAssemblyController>();player.enabled=false;playerObject.GetComponent<CharacterController>().enabled=false;
 foreach(var difficulty in new[]{SnapDifficulty.Hard,SnapDifficulty.Medium})
 {
     ui.ChooseChapter(1);m.SetDifficulty(difficulty,false);yield return null;eye.transform.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);
     var firstPart=m.NextHintPart();player.PickUp(m.FindPart("206_Connecting_Rod_Big_End_Cap_Cyl01_01"));Assert(!player.CurrentHeldPart,"PC "+difficulty+" controller refuses out-of-order pickup");
     player.PickUp(firstPart);Assert(player.CurrentHeldPart==firstPart,"PC "+difficulty+" can pick up eligible bench part");
     var pose=firstPart.GetTargetPose(firstPart.TargetSocket);firstPart.MoveHeld(pose.position,pose.rotation);
     var barrier=GameObject.CreatePrimitive(PrimitiveType.Cube);barrier.transform.position=Vector3.Lerp(eye.transform.position,pose.position,.5f);barrier.transform.rotation=eye.transform.rotation;barrier.transform.localScale=new Vector3(5,5,.15f);Physics.SyncTransforms();
     Assert(!firstPart.SnapToSocket(firstPart.TargetSocket),"PC "+difficulty+" refuses snapping behind an occluder");
     barrier.SetActive(false);Physics.SyncTransforms();Destroy(barrier);
     Assert(firstPart.SnapToSocket(firstPart.TargetSocket),"PC "+difficulty+" snaps when its target is visible: "+firstPart.RejectionReason);player.DropHeldPart();
 }
 ui.ChooseChapter(1);yield return null;
 foreach(var member in m.Recipe.parts.Where(p=>p.chapterId==m.CurrentChapterId&&!string.IsNullOrEmpty(p.subAssemblyId)).OrderBy(p=>p.order))m.FindPart(member.id).InstallImmediate(m.FindPart(member.id).TargetSocket);
 yield return null;
 var rootPart=m.FindPart(g.rootPartId);m.AssemblyRoot.rotation=Quaternion.identity;
 player.PickUp(m.Members(g).First());Assert(player.CurrentHeldPart==rootPart,"PC picks a completed unit through any of its original members");
 var local=m.Members(g).ToDictionary(p=>p,p=>rootPart.transform.InverseTransformPoint(p.transform.position));
 rootPart.MoveHeld(rootPart.transform.position+Vector3.right,Quaternion.Euler(15,30,45));
 Assert(local.All(pair=>Vector3.Distance(pair.Key.transform.position,rootPart.transform.TransformPoint(pair.Value))<.001f),"Carrying and rotating the unit preserves every member's relative pose");player.DropHeldPart();
 m.SetInteraction(WorkshopInteraction.Move);player.PickUp(rootPart);Assert(rootPart.GetComponent<PickupablePlatform>().IsHeld,"PC Move mode manipulates loose subassembly fixture instead of engine");player.DropHeldPart();m.SetInteraction(WorkshopInteraction.Build);Destroy(playerObject);yield return null;
 // Prior chapter reconstruction includes all carriers and preserves full geometry.
 ui.ChooseChapter(8);yield return null;
 Assert(m.Recipe.parts.Where(p=>p.chapterId!="chapter-9").All(p=>m.IsPlaced(p.id)),"Chapter jump reconstructs earlier subassemblies completely");
 var physical=m.AssemblyParts.Where(p=>m.CarrierDefinition(p.PartId)==null).ToArray();
 if(physical.Any(p=>p.GetComponentInChildren<GeneratedColliderGroup>(true))) {
 Assert(physical.All(p=>p.GetComponentInChildren<GeneratedColliderGroup>(true)),"Every physical component retains its CoACD hull group");
 Assert(physical.All(p=>p.GetComponents<Collider>().All(c=>!c.enabled || c.isTrigger)),"Mobile chapter changes never re-enable replaced broad colliders");
 Assert(physical.All(p=>p.GetComponent<MeshFilter>().sharedMesh.vertexCount>0),"Every physical component has a renderable mesh, including CAD wiring");
 }
 ui.ChooseChapter(1);m.SetTask(AssemblyTask.Disassemble);yield return null;
 int safety=0;
 while(!m.ChapterComplete && safety++<60){
 var p=m.NextHintPart();if(!p || !p.BeginGrab())throw new Exception("Disassembly blocked at "+p?.PartId);
 p.Release(false);yield return null;
 }
 Assert(m.ChapterComplete,"Reverse disassembly removes caps, units, then individual members");
 // Invalid edits are rejected before mutating the scene.
 var bad=AssemblyRecipeStore.Parse(JsonUtility.ToJson(m.Recipe));bad.parts.First(p=>p.id==g.rootPartId).prerequisites.Clear();
 Assert(bad.Validate().Count>0,"Incomplete carrier definitions rejected by validation");
 var badOrder=AssemblyRecipeStore.Parse(JsonUtility.ToJson(m.Recipe));badOrder.parts.First(p=>p.id=="203_Piston_Wrist_Pin_Cyl01_01").order=200;
 Assert(badOrder.Validate().Count>0,"Invalid pin-before-rod authoring order rejected");
 }
 void Assert(bool v,string s){if(!v)throw new Exception(s);checks.Add(s);}
}
}

