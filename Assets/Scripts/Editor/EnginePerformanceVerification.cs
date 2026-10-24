using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class EnginePerformanceVerification
    {
        static EnginePerformanceVerification(){EditorApplication.playModeStateChanged+=Changed;}
        public static void Baseline(){SessionState.SetString("Performance.Label","baseline");Run();}
        public static void After(){WorkshopPerformanceSetup.Configure();SessionState.SetString("Performance.Label","after");Run();}
        static void Run(){SessionState.SetBool("Performance.Run",true);EditorSceneManager.OpenScene("Assets/Scenes/MobileAssembly.unity");EditorApplication.isPlaying=true;}
        static void Changed(PlayModeStateChange state){if(!SessionState.GetBool("Performance.Run",false))return;if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Performance comparison").AddComponent<EnginePerformanceRunner>();if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("Performance.Run",false);EditorApplication.Exit(SessionState.GetBool("Performance.Passed",false)?0:1);}}
    }
    public sealed class EnginePerformanceRunner:MonoBehaviour
    {
        [Serializable] sealed class Sample{public string operation;public double medianMs,p95Ms,bytesPerCall;}
        [Serializable] sealed class Report{public bool passed;public string environment,error;public Sample[] samples;public int renderedTriangles,enabledColliders;public string[] checks;}
        readonly List<string> checks=new List<string>();
        void Check(bool result,string message){if(!result)throw new Exception(message);checks.Add(message);}
        readonly List<Sample> samples=new List<Sample>();readonly List<string> errors=new List<string>();
        void OnEnable()=>Application.logMessageReceived+=Log;
        void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors.Add(message);}
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;QualitySettings.SetQualityLevel(0,true);
            var work=new Stack<IEnumerator>();work.Push(Scenario());string error="";
            while(work.Count>0){bool more=false;object next=null;try{more=work.Peek().MoveNext();if(more)next=work.Peek().Current;}catch(Exception e){error=e.ToString();break;}if(!more){work.Pop();continue;}if(next is IEnumerator nested)work.Push(nested);else yield return next;}
            if(errors.Count>0)error+="\n"+string.Join("\n",errors);
            var meshes=FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);int triangles=0;foreach(var mesh in meshes)if(mesh.sharedMesh && mesh.TryGetComponent<Renderer>(out var r) && r.enabled)triangles+=mesh.sharedMesh.triangles.Length/3;
            var report=new Report{passed=error.Length==0,error=error,environment="Unity Editor comparison, D3D11, Mobile quality. Not phone or headset FPS.",samples=samples.ToArray(),renderedTriangles=triangles,enabledColliders=FindObjectsByType<Collider>(FindObjectsSortMode.None).Count(c=>c.enabled),checks=checks.ToArray()};
            Directory.CreateDirectory("Tools/Verification");File.WriteAllText("Tools/Verification/performance-"+SessionState.GetString("Performance.Label","after")+".json",JsonUtility.ToJson(report,true));SessionState.SetBool("Performance.Passed",report.passed);EditorApplication.isPlaying=false;
        }
        IEnumerator Measure(string name,Action action,int count=20)
        {
            action();action();yield return null;var times=new double[count];long allocated=0;var watch=new Stopwatch();
            for(int i=0;i<count;i++){long before=GC.GetAllocatedBytesForCurrentThread();watch.Restart();action();watch.Stop();allocated+=GC.GetAllocatedBytesForCurrentThread()-before;times[i]=watch.Elapsed.TotalMilliseconds;yield return null;}
            Array.Sort(times);samples.Add(new Sample{operation=name,medianMs=times[count/2],p95Ms=times[Math.Min(count-1,(int)(count*.95))],bytesPerCall=(double)allocated/count});
        }
        IEnumerator Scenario()
        {
            var phone=FindAnyObjectByType<MobileAssemblyController>();while(!phone || !phone.Ready){yield return null;phone=FindAnyObjectByType<MobileAssemblyController>();}
            var manager=phone.GetComponent<AssemblyManager>();int chapter=manager.Recipe.chapters.FindLastIndex(c=>manager.Recipe.parts.Any(p=>p.chapterId==c.id));phone.ChooseChapter(chapter);yield return null;
            phone.SelectPart(manager.NextHintPart().PartId);var guide=manager.GetComponent<AssemblyEasyGuide>();
            yield return Measure("Phone ghost refresh, late chapter",guide.RefreshTargets);
            yield return Measure("Next assembly step, late chapter",()=>manager.NextHintPart());
            if(SessionState.GetString("Performance.Label","")=="after")
            {
                var selected=manager.AssemblyParts.First(p=>p.IsSelected);phone.RotateEngine(new Vector2(16,12));phone.PanModel(new Vector2(8,5));guide.RefreshTargets();
                var queryScene=(Scene)typeof(AssemblyEasyGuide).GetField("queryScene",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(guide);
                var expected=selected.GetTargetPose(selected.TargetSocket);
                Check(queryScene.GetRootGameObjects().Any(g=>g.activeSelf && Vector3.Distance(g.transform.position,expected.position)<.00001f && Quaternion.Angle(g.transform.rotation,expected.rotation)<.01f),"Ghost queries follow the active build immediately after phone rotation and pan");
                typeof(MobileAssemblyController).GetMethod("CancelSelection",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(phone,null);guide.RefreshTargets();Check(queryScene.GetRootGameObjects().All(g=>!g.activeSelf),"No ghost physics remains active when no item is selected");
                for(int c=0;c<manager.Recipe.chapters.Count;c++)
                {
                    if(!manager.Recipe.parts.Any(p=>p.chapterId==manager.Recipe.chapters[c].id))continue;phone.ChooseChapter(c);
                    var candidates=manager.RecommendedParts(manager.AssemblyParts.Where(p=>p && p.gameObject.activeInHierarchy && p.ChapterId==manager.CurrentChapterId && !p.Definition.isBase && !p.IsBusy && !p.IsSnapped)).ToArray();
                    Check(manager.NextHintPart()==(candidates.FirstOrDefault(p=>p.TargetSocket && manager.CanInstall(p,p.TargetSocket,out _))??candidates.FirstOrDefault()),"Optimized recommendation preserves chapter order "+c);
                }
                var profiles=Resources.Load<WorkshopPerformanceProfiles>("WorkshopPerformanceProfiles");Check(profiles && profiles.phone && profiles.quest && !profiles.phone.supportsHDR && !profiles.quest.supportsHDR && profiles.quest.msaaSampleCount==2,"Phone and Quest have valid lightweight render profiles");
            }
            yield return SceneManager.LoadSceneAsync("MobileEngineDisplay");yield return null;var display=FindAnyObjectByType<MobileEngineDisplay>();while(!display.Ready)yield return null;
            display.SetTimelineAudio(false);display.SetTimeline(50.4f/display.StepCount);float position=50.4f/display.StepCount;
            yield return Measure("Phone timeline, within one part",()=>{position+=.00001f;display.SetTimeline(position,false);});
            display.SetMode(MobileEngineDisplay.DisplayMode.OperatingEngine);display.Operation.Paused=true;float angle=0;
            yield return Measure("Engine motion and section",()=>display.Operation.SetCrankAngle(angle+=3));
            yield return SceneManager.LoadSceneAsync("AssemblyWorkshop");for(int i=0;i<6;i++)yield return null;manager=FindAnyObjectByType<AssemblyManager>();manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);manager.SelectChapter(1);yield return null;
            QuestVRSession.Instance.EnableVerification();yield return null;var rig=FindAnyObjectByType<QuestVRRig>();
            yield return Measure("Quest UI binding, unchanged scene",rig.BindUI);
            yield return Measure("Quest ghost refresh",manager.GetComponent<AssemblyEasyGuide>().RefreshTargets);
            if(SessionState.GetString("Performance.Label","")=="after")
            {
                var tutorial=WorkshopTutorial.Open(manager.gameObject);yield return null;rig.BindUI();
                Check(tutorial.GetComponent<Canvas>().renderMode==RenderMode.WorldSpace && tutorial.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>(),"New VR tutorial binds to controller rays after the original canvases were cached");tutorial.Close();
            }
            Destroy(QuestVRSession.Instance.gameObject);
        }
    }
}
