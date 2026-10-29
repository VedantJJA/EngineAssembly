using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace EngineAssembly
{
    [Serializable] public sealed class EngineOperationCue
    {
        public string id,title,text;public string[] partPrefixes;
        public float startAngle,endAngle;
    }
    [Serializable] public sealed class EngineOperationCatalogue { public EngineOperationCue[] cues;public string[] tourOrder; }

    // A separate, optional tour. The normal engine animation remains freely playable.
    public sealed class EngineOperationNarrator : MonoBehaviour
    {
        EngineOperatingAnimation operation;
        NarrationSpeech speech;
        Action<string,string> present;
        Coroutine routine;
        string ownCue;
        public bool Active=>routine!=null;
        public bool Paused{get;private set;}
        public string CurrentCueId=>ownCue;
        public bool HasIntroduced {get;private set;}
        public void Configure(EngineOperatingAnimation animation,NarrationSpeech speaker,Action<string,string> caption)
        {operation=animation;speech=speaker;present=caption;}
        public void Play()
        {
            Cancel();if(!operation.Active)operation.Play();operation.Paused=true;Paused=false;routine=StartCoroutine(Tour());
        }
        IEnumerator Tour()
        {
            var asset=Resources.Load<TextAsset>("EngineOperationNarration");
            var data=asset?JsonUtility.FromJson<EngineOperationCatalogue>(asset.text):new EngineOperationCatalogue{cues=Array.Empty<EngineOperationCue>()};
            var cues=data.tourOrder?.Select(id=>data.cues.First(c=>c.id==id)).ToArray()??data.cues;
            foreach(var cue in cues)
            {
                bool identifying=Mathf.Approximately(cue.startAngle,cue.endAngle);if(identifying)operation.HighlightDescription(cue.partPrefixes);else {HasIntroduced=true;operation.ClearDescriptionHighlight();}
                float startAngle=operation.CrankForCycle(cue.startAngle),endAngle=operation.CrankForCycle(cue.endAngle);
                ownCue=cue.id;operation.SetCrankAngle(startAngle);present?.Invoke(cue.title,cue.text);speech.Speak(cue.id);
                // Speak loads asynchronously. Include that interval before reading the clip clock.
                yield return null;
                float elapsed=0,fallback=Mathf.Max(4,cue.text.Split(' ').Length/2.3f);
                do
                {
                    if(Paused || Time.timeScale<=0){yield return null;continue;}
                    if(speech.CueId!=ownCue){Cancel();yield break;}
                    float duration=speech.Source.clip?speech.Source.clip.length:fallback;
                    elapsed=speech.Source.isPlaying?speech.Source.time:elapsed+Time.unscaledDeltaTime;
                    operation.SetCrankAngle(Mathf.Lerp(startAngle,endAngle,Mathf.Clamp01(elapsed/duration)));
                    yield return null;
                }while(speech.IsBusy || (!speech.Source.clip && elapsed<fallback));
                operation.SetCrankAngle(endAngle);
                float gap=0;while(gap<.65f){if(!Paused && Time.timeScale>0)gap+=Time.unscaledDeltaTime;yield return null;}
            }
            operation.ClearDescriptionHighlight();ownCue=null;routine=null;Paused=false;operation.Paused=false;
        }
        public void SetPaused(bool paused){if(!Active)return;Paused=paused;speech.SetPaused(paused);}
        public void Cancel()
        {
            if(routine!=null)StopCoroutine(routine);routine=null;Paused=false;
            if(speech && speech.CueId==ownCue)speech.Stop();ownCue=null;
            if(operation)operation.ClearDescriptionHighlight();
        }
        void OnDisable()=>Cancel();
    }
}
