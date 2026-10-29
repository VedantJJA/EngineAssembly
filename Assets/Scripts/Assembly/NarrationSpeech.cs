using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace EngineAssembly
{
    [Serializable] public class SpokenWord { public string word; public float start,end; }
    [Serializable] public class SpokenCue { public string id,text; public SpokenWord[] words; }
    [Serializable] public class SpokenCatalogue { public SpokenCue[] cues; }
    public static class NarrationText
    {
        static Dictionary<string,SpokenCue> cues;
        public static SpokenCue Find(string id)
        {
            if(cues==null){var asset=Resources.Load<TextAsset>("NarrationText");cues=asset?JsonUtility.FromJson<SpokenCatalogue>(asset.text).cues.ToDictionary(c=>c.id):new Dictionary<string,SpokenCue>();}
            return id!=null && cues.TryGetValue(id,out var cue)?cue:null;
        }
    }
    public sealed class NarrationSpeech : MonoBehaviour
    {
        public AudioSource Source {get;private set;}
        public string CueId {get;private set;}
        public bool PlayWhilePaused {get;set;}
        Coroutine loading;
        bool paused,manualPause;
        bool started,finished,appPaused;
        public event Action<string> Completed;
        public bool IsBusy=>loading!=null || Source && (Source.isPlaying || paused);
        void Awake(){Source=gameObject.AddComponent<AudioSource>();Source.playOnAwake=false;Source.spatialBlend=0;Source.bypassReverbZones=true;Source.dopplerLevel=0;Source.priority=32;}
        public void Speak(string id){if(loading!=null)StopCoroutine(loading);Source.Stop();Source.clip=null;paused=false;manualPause=false;started=false;finished=false;CueId=id;loading=StartCoroutine(Load(id));}
        IEnumerator Load(string id){yield return null;var library=Resources.Load<NarrationLibrary>("NarrationLibrary");var clip=library?library.Find(id):null;if(clip){if(clip.loadState==AudioDataLoadState.Unloaded)clip.LoadAudioData();while(clip.loadState==AudioDataLoadState.Loading)yield return null;if(clip.loadState==AudioDataLoadState.Loaded){Source.clip=clip;Source.Play();started=true;}}loading=null;}
        public void Stop(){if(loading!=null)StopCoroutine(loading);loading=null;paused=false;manualPause=false;started=false;Source.Stop();}
        public void SetPaused(bool value){manualPause=value;ApplyPause();}
        void ApplyPause(){bool shouldPause=appPaused || Time.timeScale==0 && !PlayWhilePaused || manualPause;if(shouldPause && Source.isPlaying){Source.Pause();paused=true;}else if(!shouldPause && paused){Source.UnPause();paused=false;}}
        void Update(){ApplyPause();if(started && !finished && loading==null && !paused && !manualPause && !appPaused && (Time.timeScale>0 || PlayWhilePaused) && !Source.isPlaying){finished=true;Completed?.Invoke(CueId);}}
        void OnApplicationPause(bool paused)=>appPaused=paused;
    }
    [DefaultExecutionOrder(300)]
    public sealed class LiveNarrationCaption : MonoBehaviour
    {
        public TextMeshProUGUI Label;
        public AssemblyNarrator Assembly;
        public NarrationSpeech Speaker;
        string lastText,lastId,tokenText;int lastWord=-2,lastLimit=-1;string[] tokens;SpokenCue tokenCue;float tokenWeight;readonly System.Text.StringBuilder builder=new System.Text.StringBuilder(1024);
        void LateUpdate()
        {
            if(!Assembly && !Speaker){var manager=GetComponentInParent<AssemblyManager>();if(manager)Assembly=manager.GetComponent<AssemblyNarrator>();}
            var audio=Assembly?Assembly.Source:Speaker?Speaker.Source:null;
            string id=Assembly?Assembly.CurrentCueId:Speaker?Speaker.CueId:null;
            if(!Label || !Label.enabled || !audio || !audio.clip || !audio.isPlaying)return;
            var cue=NarrationText.Find(id);if(cue==null)return;
            if(tokenCue!=cue || tokenText!=cue.text){tokenCue=cue;tokenText=cue.text;tokens=cue.text.Split(new[]{' ','\n','\r'},StringSplitOptions.RemoveEmptyEntries);tokenWeight=0;foreach(var token in tokens)tokenWeight+=Mathf.Max(2,token.Length);lastWord=-2;}
            int active=0;
            if(cue.words!=null && cue.words.Length==tokens.Length){active=-1;while(active+1<cue.words.Length && audio.time>=cue.words[active+1].start)active++;}
            else {float target=tokenWeight*audio.time/Mathf.Max(.1f,audio.clip.length);float total=0;while(active<tokens.Length-1 && total+Mathf.Max(2,tokens[active].Length)<target)total+=Mathf.Max(2,tokens[active++].Length);}
            // A rolling phrase keeps long educational explanations readable.
            int limit=Mathf.Min(tokens.Length,Assembly?Assembly.CaptionWordLimit:tokens.Length);if(limit<=0){Label.text="";return;}active=Mathf.Min(active,limit-1);
            int first=Mathf.Max(0,(active/28)*28),last=Mathf.Min(limit,first+28);
            if(lastId==id && lastWord==active && lastLimit==limit && Label.text==lastText)return;
            string dark=ColorUtility.ToHtmlStringRGB(WorkshopTheme.Text),muted="788797";
            builder.Clear();for(int i=first;i<last;i++){if(i>first)builder.Append(' ');builder.Append("<color=#").Append(i<=active?dark:muted).Append('>').Append(tokens[i]).Append("</color>");}
            lastText=builder.ToString();Label.text=lastText;lastId=id;lastWord=active;lastLimit=limit;
        }
    }
}
