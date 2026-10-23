using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class BeginnerNarrationVerification
    {
        static BeginnerNarrationVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch(){SessionState.SetBool("BeginnerAudio.Verify",true);EditorSceneManager.OpenScene("Assets/Scenes/AssemblyWorkshop.unity");EditorApplication.isPlaying=true;}
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("BeginnerAudio.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Beginner narration verification").AddComponent<BeginnerNarrationVerificationRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("BeginnerAudio.Verify",false);EditorApplication.Exit(SessionState.GetBool("BeginnerAudio.Result",false)?0:1);}
        }
    }
    public sealed class BeginnerNarrationVerificationRunner:MonoBehaviour
    {
        [Serializable] class Report{public bool passed;public string[] checks;public string error;}
        readonly List<string> checks=new List<string>();
        void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
        IEnumerator Start()
        {
            var test=Scenario();string error="";
            while(true){bool more=false;object value=null;try{more=test.MoveNext();if(more)value=test.Current;}catch(Exception ex){error=ex.ToString();}if(error.Length>0 || !more)break;yield return value;}
            File.WriteAllText("Tools/Verification/beginner-narration-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,checks=checks.ToArray(),error=error},true));SessionState.SetBool("BeginnerAudio.Result",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Scenario()
        {
            yield return null;yield return null;yield return null;
            var m=AssemblyManager.Instance;var ui=m.GetComponent<AssemblyWorkshopUI>();ui.SetOpen(false);FindAnyObjectByType<PlayerAssemblyController>().enabled=false;
            var data=EngineLearningContent.Data;
            Check(data.steps.Count==183 && data.details.Count==325,"Reviewed catalogue keeps 183 steps and all 325 item descriptions");
            var ids=data.steps.SelectMany(s=>s.partIds).ToArray();
            Check(ids.Distinct().Count()==m.Recipe.parts.Count && ids.Length==m.Recipe.parts.Count,"Every recipe item has exactly one lesson");
            Check(data.steps.All(s=>!string.IsNullOrEmpty(s.purpose) && s.narration.Contains(s.purpose) && s.narration.Contains(s.caption)),"Every lesson teaches purpose and fitting directions");
            Check(data.steps.Where(s=>!string.IsNullOrEmpty(s.voiceId)).GroupBy(s=>s.voiceId).All(g=>g.Select(s=>s.narration).Distinct().Count()==1),"Repeated piston and valve lessons share identical narration");
            // Short in-memory clips exercise playback timing independently of TTS
            // generation speed and the duration of the exported voice lessons.
            var library=ScriptableObject.CreateInstance<NarrationLibrary>();
            var cueIds=data.steps.Select(s=>string.IsNullOrEmpty(s.voiceId)?s.id:s.voiceId).Concat(new[]{"flip-upright","flip-upside-down","complete","repeat-matching","repeat-piston","repeat-valve"}).Distinct();
            library.cues=cueIds.Select(id=>new NarrationLibrary.Cue{id=id,clip=AudioClip.Create(id,24000,1,24000,false)}).ToArray();
            var narrator=m.GetComponent<AssemblyNarrator>();narrator.enabled=false;
            typeof(AssemblyNarrator).GetField("library",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(narrator,library);
            foreach(var part in m.Recipe.parts)part.orientation=AssemblyOrientation.Any;
            m.SelectChapter(0);ui.SetOpen(false);narrator.enabled=true;
            yield return null;yield return null;yield return null;
            Check(narrator.Source.isPlaying && narrator.CurrentCueId=="chapter-1-cue-001","First beginner lesson starts with a loaded clip");
            var first=m.NextHintPart();first.InstallImmediate(first.TargetSocket);yield return null;yield return null;
            Check(narrator.CurrentCueId=="chapter-1-cue-001" && narrator.Source.isPlaying,"Installing early does not cut off the current explanation");
            float deadline=Time.realtimeSinceStartup+3;
            while(narrator.CurrentCueId=="chapter-1-cue-001" && Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;yield return null;
            Check(narrator.CurrentCueId=="chapter-1-cue-002" && narrator.Source.isPlaying,"Next queued lesson starts after the previous audio finishes");
            ui.Open(AssemblyWorkshopUI.Page.Pause);yield return null;yield return null;Check(!narrator.Source.isPlaying,"Pause menu pauses a lesson");
            ui.SetOpen(false);yield return null;yield return null;Check(narrator.Source.isPlaying,"Returning to the workshop resumes the lesson");
            Check(narrator.ReplayCurrent(),"Replay accepts the current fitting instruction");yield return null;yield return null;
            Check(narrator.CurrentCueId=="chapter-1-cue-002" && narrator.Source.isPlaying,"Replay works for an already-loaded clip without leaving a stuck coroutine");
        }
    }
}
