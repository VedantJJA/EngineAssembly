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

namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class GuidancePickupVerification
    {
        static GuidancePickupVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch(){AssemblyAudioSetup.Run();SessionState.SetBool("GuidancePickup.Verify",true);EditorSceneManager.OpenScene("Assets/Scenes/AssemblyWorkshop.unity");EditorApplication.isPlaying=true;}
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("GuidancePickup.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Flip guidance and small pickup verification").AddComponent<GuidancePickupRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("GuidancePickup.Verify",false);EditorApplication.Exit(SessionState.GetBool("GuidancePickup.Passed",false)?0:1);}
        }
    }
    public sealed class GuidancePickupRunner:MonoBehaviour
    {
        [Serializable] sealed class Report{public bool passed;public string error;public string[] checks;}
        readonly List<string> checks=new List<string>(),errors=new List<string>();AssemblyManager manager;PlayerAssemblyController player;
        void Check(bool value,string text){if(!value)throw new Exception(text);checks.Add(text);}
        void OnEnable()=>Application.logMessageReceived+=Log;void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string text,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception)errors.Add(text+"\n"+stack);}
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";
            while(stack.Count>0){bool more=false;object next=null;try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception e){error=e.ToString();break;}if(!more){stack.Pop();continue;}if(next is IEnumerator nested)stack.Push(nested);else yield return next;}
            WorkshopSectionCompletion.VerificationEnabled=false;WorkshopGuidance.SetArrow(false);Time.timeScale=1;if(errors.Count>0)error+="\n"+string.Join("\n",errors);
            File.WriteAllText("Tools/Verification/guidance-pickup-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,error=error,checks=checks.ToArray()},true));SessionState.SetBool("GuidancePickup.Passed",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Fresh()
        {
            yield return SceneManager.LoadSceneAsync("AssemblyWorkshop");for(int i=0;i<5;i++)yield return null;manager=AssemblyManager.Instance;player=FindAnyObjectByType<PlayerAssemblyController>();manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);manager.SetDifficulty(SnapDifficulty.Easy,false);
        }
        IEnumerator Scenario()
        {
            for(int i=0;i<5;i++)yield return null;manager=AssemblyManager.Instance;player=FindAnyObjectByType<PlayerAssemblyController>();manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);manager.GetComponent<AssemblyNarrator>().enabled=false;manager.SetDifficulty(SnapDifficulty.Easy,false);
            manager.SelectChapter(manager.Recipe.chapters.FindIndex(c=>c.title.Contains("Pistons")));yield return null;yield return null;
            var part=manager.NextHintPart();var frame=manager.TargetFrame(part.Definition);foreach(var d in manager.Recipe.parts.Where(d=>d.chapterId==manager.CurrentChapterId && d.id.StartsWith("201_Piston_"))){var target=manager.TargetFrame(d);d.orientation=Vector3.Dot(target.up,Vector3.up)>0?AssemblyOrientation.UpsideDown:AssemblyOrientation.Upright;}
            var lift=manager.GetComponent<WorkpieceLift>();lift.Select(0);var correct=lift.FlipControlFor(frame);Check(correct && correct!=lift.StationFlip(0),"A piston step identifies its own lift, independently of the selected main lift");
            var arrow=manager.GetComponentInChildren<WorkshopQuestArrow>(true);WorkshopGuidance.SetArrow(true);player.ViewCamera.transform.SetPositionAndRotation(correct.AimPosition+Vector3.back*1.3f,Quaternion.LookRotation(correct.AimPosition-(correct.AimPosition+Vector3.back*1.3f)));yield return null;
            Check(arrow.PointsToFlip && arrow.TargetFlipControl==correct && !arrow.PointsToPlacement,"The quest arrow targets the required workpiece's flip button (tracking="+arrow.Tracking+", target="+arrow.TargetPart?.PartId+", expected="+part.PartId+")");Check(arrow.TargetOnScreen && arrow.ArrowDirection==Vector2.down,"On-screen flip guidance hovers vertically over the red button");
            player.PickUp(part);yield return null;Check(player.IsHoldingPart && arrow.PointsToFlip,"Flip guidance still targets the button while a part is carried");
            int station=Enumerable.Range(0,lift.StationCount).First(i=>lift.StationFrame(i)==frame);Check(lift.FlipStation(station),"The indicated subassembly lift can flip while a member is held");yield return null;Check(arrow.PointsToFlip,"The arrow stays at the controls while the lift is turning");
            float until=Time.realtimeSinceStartup+12;while(lift.IsTransitioning && Time.realtimeSinceStartup<until)yield return null;yield return null;Check(manager.OrientationReady(part.Definition) && !arrow.PointsToFlip && arrow.PointsToPlacement,"After turning, guidance returns to the held part's fitting position");player.DropHeldPart(false);
            part=manager.NextHintPart();frame=manager.TargetFrame(part.Definition);part.Definition.orientation=Vector3.Dot(frame.up,Vector3.up)>0?AssemblyOrientation.UpsideDown:AssemblyOrientation.Upright;
            QuestVRSession.Instance.EnableVerification();yield return null;yield return null;var rig=FindAnyObjectByType<QuestVRRig>();rig.enabled=false;var head=Camera.main;correct=lift.FlipControlFor(frame);head.transform.SetPositionAndRotation(correct.AimPosition+Vector3.back,Quaternion.LookRotation(Vector3.forward));yield return null;
            Check(arrow.PointsToFlip && arrow.TargetOnScreen && !float.IsNaN(arrow.rectTransform.anchoredPosition.x),"Quest arrow projection uses the active headset camera and world-space canvas");
            typeof(QuestVRSession).GetProperty("Active").SetValue(QuestVRSession.Instance,false);yield return Fresh();
            yield return Pickup();
        }
        AssemblyPart Add(string id,Vector3 position,Vector3 size,int order=1)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=id;go.transform.localScale=size;var part=go.AddComponent<AssemblyPart>();part.SetIdentity(id,id);var definition=new PartDefinition{id=id,displayName=id,chapterId="pickup-check",order=order,orientation=AssemblyOrientation.Any,geometryGroup=id,targetPosition=manager.AssemblyRoot.InverseTransformPoint(position+Vector3.forward*2),targetRotation=Quaternion.identity,trayPosition=manager.TrayFrame.InverseTransformPoint(position),trayRotation=Quaternion.identity};manager.Recipe.parts.Add(definition);part.Configure(definition,manager);typeof(AssemblyManager).GetMethod("RegisterRepairPart",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(manager,new object[]{part});part.TargetSocket=new GameObject(id+" socket").AddComponent<AssemblySocket>();part.TargetSocket.Configure(part);part.TargetSocket.transform.SetParent(manager.AssemblyRoot,false);part.TargetSocket.transform.SetLocalPositionAndRotation(definition.targetPosition,definition.targetRotation);return part;
        }
        IEnumerator Pickup()
        {
            manager.GetComponent<AssemblyNarrator>().enabled=false;manager.Recipe.chapters.Add(new ChapterDefinition{id="pickup-check",title="Pickup check"});var origin=new Vector3(30,1,30);var small=Add("tiny_01",origin,Vector3.one*.01f);var large=Add("large_01",origin+Vector3.right,Vector3.one*.3f);var later=Add("later_01",origin+Vector3.left,Vector3.one*.01f,2);manager.SelectChapter(manager.Recipe.chapters.Count-1);yield return null;
            var query=new GameObject("Small pickup fixture").AddComponent<AssemblyPickupQuery>();var camera=new GameObject("Pickup fixture camera").AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=60;camera.pixelRect=new Rect(0,0,1280,720);camera.transform.SetPositionAndRotation(origin+Vector3.back*2,Quaternion.identity);
            Ray Miss(AssemblyPart part,float margin)=>new Ray(new Vector3(part.transform.position.x+part.transform.lossyScale.x*.5f+margin,part.transform.position.y,part.transform.position.z-2),Vector3.forward);
            var ray=Miss(small,.012f);Check(!query.Raycast(ray,4,~0,out var exact,out _,out _) || exact!=small,"The precision ray misses the tiny part in the test fixture");Check(query.PickupRaycast(ray,4,~0,manager,camera,out var picked,out _,out _) && picked==small,"Easy aim assistance picks up a visible tiny part just beside the crosshair");
            foreach(var difficulty in new[]{SnapDifficulty.Medium,SnapDifficulty.Hard}){manager.SetDifficulty(difficulty,false);query.PickupRaycast(ray,4,~0,manager,camera,out picked,out _,out _);Check(picked!=small,difficulty+" retains precise pickup targeting");}manager.SetDifficulty(SnapDifficulty.Easy,false);
            query.PickupRaycast(Miss(large,.012f),4,~0,manager,camera,out picked,out _,out _);Check(picked!=large,"Large items do not receive the Easy pickup margin");query.PickupRaycast(new Ray(large.transform.position+Vector3.back*2,Vector3.forward),4,~0,manager,camera,out picked,out _,out _);Check(picked==large,"Large items remain selectable with a direct hit");
            query.PickupRaycast(Miss(small,.2f),4,~0,manager,camera,out picked,out _,out _);Check(picked!=small,"Aim assistance has a bounded margin");query.PickupRaycast(ray,1,~0,manager,camera,out picked,out _,out _);Check(picked!=small,"Tiny parts cannot be picked up beyond reach");query.PickupRaycast(Miss(later,.012f),4,~0,manager,camera,out picked,out _,out _);Check(picked!=later,"Aim assistance respects assembly order");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=small.transform.position+Vector3.back*.4f;wall.transform.localScale=new Vector3(.4f,.4f,.1f);query.PickupRaycast(ray,4,~0,manager,camera,out picked,out _,out _);Check(picked!=small,"World scenery blocks assisted pickup");Destroy(wall);var cover=Add("part-cover",small.transform.position+Vector3.back*.25f,new Vector3(.4f,.4f,.1f));cover.ResetLoose(manager.TrayFrame,cover.Definition.trayPosition,cover.Definition.trayRotation);query.PickupRaycast(ray,4,~0,manager,camera,out picked,out _,out _);Check(picked!=small,"Another visible engine part blocks assisted pickup");Destroy(cover.gameObject);yield return null;
            query.PickupRaycast(ray,4,~0,manager,camera,out picked,out _,out _,large);Check(picked!=small,"A mismatching inventory stack is never selected by aim assistance");Destroy(query.gameObject);Destroy(camera.gameObject);
        }
    }
}
