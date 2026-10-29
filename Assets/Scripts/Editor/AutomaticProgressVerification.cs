using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class AutomaticProgressVerification
    {
        static AutomaticProgressVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch(){AssemblyAudioSetup.Run();SessionState.SetBool("Automatic.Run",true);EditorSceneManager.OpenScene("Assets/Scenes/AssemblyWorkshop.unity");EditorApplication.isPlaying=true;}
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("Automatic.Run",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Automatic progress verification").AddComponent<AutomaticProgressRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("Automatic.Run",false);EditorApplication.Exit(SessionState.GetBool("Automatic.Passed",false)?0:1);}
        }
    }
    public sealed class AutomaticProgressRunner:MonoBehaviour
    {
        [Serializable] sealed class Report{public bool passed;public string error;public string[] checks;}
        readonly List<string> checks=new List<string>();
        void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";
            while(stack.Count>0){bool more=false;object next=null;try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception e){error=e.ToString();break;}if(!more){stack.Pop();continue;}if(next is IEnumerator nested)stack.Push(nested);else yield return next;}
            Time.timeScale=1;WorkshopSectionCompletion.VerificationEnabled=false;
            Directory.CreateDirectory("Tools/Verification");File.WriteAllText("Tools/Verification/automatic-progress-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,error=error,checks=checks.ToArray()},true));SessionState.SetBool("Automatic.Passed",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Loaded(NarrationSpeech speech){float until=Time.realtimeSinceStartup+8;while(!speech.Source.isPlaying && Time.realtimeSinceStartup<until)yield return null;Check(speech.Source.isPlaying,"Narration starts: "+speech.CueId);}
        IEnumerator Finish(NarrationSpeech speech){yield return Loaded(speech);speech.Source.time=Mathf.Max(0,speech.Source.clip.length-.12f);yield return new WaitForSecondsRealtime(.4f);}
        void Complete(AssemblyManager manager)
        {
            var pending=manager.Recipe.parts.Where(d=>d.chapterId==manager.CurrentChapterId && !manager.IsPlaced(d.id)).ToList();
            while(pending.Count>0){var d=pending.FirstOrDefault(p=>AssemblyRecipe.Dependencies(p).All(manager.IsPlaced));Check(d!=null,"Completion installs a dependency-ready component");var part=manager.FindPart(d.id);part.InstallImmediate(part.TargetSocket);pending.Remove(d);}
            Check(manager.ChapterComplete,"Real chapter reaches completion");
        }
        IEnumerator Scenario()
        {
            yield return null;yield return null;var manager=AssemblyManager.Instance;manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);
            var player=FindAnyObjectByType<PlayerAssemblyController>();player.enabled=false;
            var tutorial=WorkshopTutorial.Open(manager.gameObject);yield return Loaded(tutorial.Speech);
            Time.timeScale=0;tutorial.Speech.Source.time=tutorial.Speech.Source.clip.length-.15f;yield return new WaitForSecondsRealtime(.4f);
            Check(WorkshopTutorial.IsOpen,"Paused control narration keeps its card open");Time.timeScale=1;yield return new WaitForSecondsRealtime(.45f);
            Check(!WorkshopTutorial.IsOpen,"Movement card closes after natural narration completion");
            tutorial=WorkshopTutorial.OpenLesson(manager.gameObject,"tutorial-pc-pickup");yield return Finish(tutorial.Speech);Check(!WorkshopTutorial.IsOpen,"Contextual control card closes without a click");
            foreach(var id in new[]{"tutorial-vr-navigation","tutorial-vr-parts","tutorial-vr-lift","tutorial-vr-help"})Check(WorkshopTutorial.FindLesson(id).text.Contains("Quest 2"),"Quest 2 controls are explicit in "+id);
            var completed=manager.GetComponent<WorkshopSectionCompletion>();WorkshopSectionCompletion.VerificationEnabled=true;
            tutorial=WorkshopTutorial.Open(manager.gameObject);int visit=manager.ChapterVisit,index=manager.CurrentChapterIndex;Complete(manager);
            Check(completed.IsPresenting && completed.Celebration.isPlaying,"Completion plays the celebration sound");Check(!WorkshopTutorial.IsOpen,"Completion closes any remaining control card");
            yield return new WaitForSecondsRealtime(1);Check(completed.Speech.CueId=="complete","Celebration is followed by a completion voice");
            float remaining=completed.SecondsRemaining;Time.timeScale=0;yield return new WaitForSecondsRealtime(.7f);Check(Mathf.Abs(completed.SecondsRemaining-remaining)<.1f && manager.ChapterVisit==visit,"Pause freezes completion countdown");Time.timeScale=1;
            yield return new WaitForSecondsRealtime(1);Check(manager.ChapterVisit==visit && completed.SecondsRemaining>0,"Section does not change before five active seconds");
            yield return new WaitForSecondsRealtime(3.8f);Check(manager.CurrentChapterIndex==index+1 && manager.ChapterVisit==visit+1,"PC automatically advances after the celebration and voice");
            yield return new WaitForSecondsRealtime(.5f);Check(manager.ChapterVisit==visit+1,"Completion advances exactly once");
            Complete(manager);manager.SelectChapter(0);yield return null;Check(!completed.IsPresenting,"Manual chapter selection cancels a pending automatic transition");
            yield return SceneManager.LoadSceneAsync("MobileAssembly");yield return null;yield return null;var mobile=FindAnyObjectByType<MobileAssemblyController>();while(!mobile.Ready)yield return null;manager=mobile.GetComponent<AssemblyManager>();
            WorkshopSectionCompletion.VerificationEnabled=true;mobile.ChooseChapter(0);yield return null;yield return null;Complete(manager);yield return new WaitForSecondsRealtime(5.8f);
            Check(manager.CurrentChapterIndex==1 && mobile.Ready,"Phone automatically advances and refreshes its assembly workspace");
            tutorial=WorkshopTutorial.OpenLesson(manager.gameObject,"tutorial-vr-lift");yield return Finish(tutorial.Speech);Check(!WorkshopTutorial.IsOpen,"Quest 2 lift control narration dismisses its card");
            UnityEngine.Events.UnityAction<Scene,LoadSceneMode> setup=(scene,mode)=>{if(scene.name=="MobileAssemblyAR"){var session=FindAnyObjectByType<MobileARSession>();session.Configure(session.gameObject.AddComponent<ARTestTrackingProvider>(),session.FallbackScene);}};
            SceneManager.sceneLoaded+=setup;yield return SceneManager.LoadSceneAsync("MobileAssemblyAR");SceneManager.sceneLoaded-=setup;yield return null;yield return null;
            mobile=FindAnyObjectByType<MobileAssemblyController>();while(!mobile.Ready)yield return null;
            tutorial=WorkshopTutorial.Open(mobile.gameObject);Check(tutorial.LessonCount==2,"AR controls have placement and gesture narration cards");yield return Finish(tutorial.Speech);
            Check(WorkshopTutorial.IsOpen && tutorial.CurrentLessonId=="tutorial-ar-gestures","AR placement narration automatically continues to the gesture card");yield return Finish(tutorial.Speech);
            Check(!WorkshopTutorial.IsOpen,"The final AR control card closes after narration");
        }
    }
}
