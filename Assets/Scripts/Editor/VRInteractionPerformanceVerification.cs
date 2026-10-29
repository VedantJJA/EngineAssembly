using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class VRInteractionPerformanceVerification
    {
        static VRInteractionPerformanceVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void Before()=>Run("before");public static void After()=>Run("after");
        static void Run(string label){SessionState.SetString("VRPerformance.Label",label);SessionState.SetBool("VRPerformance.Run",true);EditorSceneManager.OpenScene("Assets/Scenes/AssemblyWorkshop.unity");EditorApplication.isPlaying=true;}
        static void Changed(PlayModeStateChange state){if(!SessionState.GetBool("VRPerformance.Run",false))return;if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("VR display comparison").AddComponent<VRInteractionPerformanceRunner>();if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("VRPerformance.Run",false);EditorApplication.Exit(SessionState.GetBool("VRPerformance.Passed",false)?0:1);}}
    }
    public sealed class VRInteractionPerformanceRunner:MonoBehaviour
    {
        [Serializable] sealed class Sample{public string operation;public double medianMs,p95Ms;}
        [Serializable] sealed class Report{public bool passed;public string error,environment;public int displayPhysicalColliders,sectionMaterials,engineContacts;public double displayEntryMs,motionEntryMs;public Sample[] samples;}
        readonly List<Sample> samples=new List<Sample>();readonly List<string> errors=new List<string>();Report report=new Report();
        void OnEnable()=>Application.logMessageReceived+=Log;void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception)errors.Add(message);}
        IEnumerator Start()
        {
            QualitySettings.SetQualityLevel(0,true);var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";
            while(stack.Count>0){bool more=false;object next=null;try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception e){error=e.ToString();break;}if(!more){stack.Pop();continue;}if(next is IEnumerator routine)stack.Push(routine);else yield return next;}
            if(errors.Count>0)error+="\n"+string.Join("\n",errors);report.passed=error.Length==0;report.error=error;report.environment="Editor D3D11, Mobile quality, simulated Quest input. CPU call comparison only, not hardware FPS.";report.samples=samples.ToArray();Directory.CreateDirectory("Tools/Verification");File.WriteAllText("Tools/Verification/vr-interaction-performance-"+SessionState.GetString("VRPerformance.Label","after")+".json",JsonUtility.ToJson(report,true));SessionState.SetBool("VRPerformance.Passed",report.passed);EditorApplication.isPlaying=false;
        }
        IEnumerator Measure(string name,Action action)
        {
            action();yield return null;var values=new double[30];var timer=new Stopwatch();
            for(int i=0;i<values.Length;i++){timer.Restart();action();timer.Stop();values[i]=timer.Elapsed.TotalMilliseconds;yield return null;}Array.Sort(values);samples.Add(new Sample{operation=name,medianMs=values[values.Length/2],p95Ms=values[28]});
        }
        IEnumerator Scenario()
        {
            var manager=AssemblyManager.Instance;while(!manager || manager.Recipe==null){yield return null;manager=AssemblyManager.Instance;}manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);manager.SetDifficulty(SnapDifficulty.Easy,false);manager.SelectChapter(manager.Recipe.chapters.FindLastIndex(c=>manager.Recipe.parts.Any(p=>p.chapterId==c.id)));QuestVRSession.Instance.EnableVerification();yield return null;yield return null;
            var rig=FindAnyObjectByType<QuestVRRig>();var controller=rig.GetComponent<CharacterController>();var probe=rig.gameObject.AddComponent<PerformanceContactProbe>();
            var block=manager.FindPart("101_Cylinder_Block_01").GetComponent<Renderer>().bounds;var origin=new Vector3(block.center.x+block.extents.x*.3f,block.center.y-controller.height*.5f,block.min.z-controller.radius-.035f);
            yield return Measure("Player contact against engine",()=>{controller.enabled=false;rig.transform.position=origin;controller.enabled=true;Physics.SyncTransforms();controller.Move(new Vector3(0,-.02f,.08f));});report.engineContacts=probe.engineContacts;
            var timer=Stopwatch.StartNew();manager.SelectChapter(manager.Recipe.chapters.FindIndex(c=>c.id==PCDisplayChapter.ChapterId));timer.Stop();report.displayEntryMs=timer.Elapsed.TotalMilliseconds;yield return null;yield return null;
            var display=manager.GetComponent<PCDisplayChapter>();timer.Restart();display.SetMotionMode(true);timer.Stop();report.motionEntryMs=timer.Elapsed.TotalMilliseconds;display.Operation.Paused=true;yield return null;
            report.displayPhysicalColliders=manager.AssemblyRoot.GetComponentsInChildren<Collider>(true).Count(c=>c.enabled && c.gameObject.activeInHierarchy && !(c.gameObject.layer==2));
            report.sectionMaterials=Resources.FindObjectsOfTypeAll<Material>().Count(m=>m.name.Contains("display section") || m.name.Contains("Display section") || m.name.Contains("shared section"));
            float angle=0;yield return Measure("VR engine motion",()=>display.Operation.SetCrankAngle(angle+=3));
            yield return Measure("VR display UI tick",()=>display.SendMessage("Update"));
            Destroy(QuestVRSession.Instance.gameObject);
        }
    }
    public sealed class PerformanceContactProbe:MonoBehaviour
    {
        public int engineContacts;
        void OnControllerColliderHit(ControllerColliderHit hit){if(hit.collider.GetComponentInParent<AssemblyPart>())engineContacts++;}
    }
}
