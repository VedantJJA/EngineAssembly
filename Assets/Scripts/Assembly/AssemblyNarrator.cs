using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace EngineAssembly
{
    [RequireComponent(typeof(AssemblyManager))]
    public sealed class AssemblyNarrator : MonoBehaviour
    {
        AssemblyManager manager;AudioSource source;NarrationLibrary library;
        string currentKey="";Coroutine loading;bool paused;
        readonly HashSet<string> spokenCues=new HashSet<string>();
        readonly HashSet<string> repeatGroups=new HashSet<string>();
        readonly Queue<string> pendingCues=new Queue<string>();
        int chapterVisit=-1;
        float nextCueAt;
        bool wasPlaying;
        public AudioSource Source=>source;
        public bool HasSpokenSharedCue(string id)=>spokenCues.Contains(id);
        public string CurrentCueId { get; private set; }
        public float LastCueFinishedAt {get;private set;}
        public float LastCueStartedAt {get;private set;}
        void Awake()
        {
            manager=GetComponent<AssemblyManager>();library=Resources.Load<NarrationLibrary>("NarrationLibrary");
            source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;source.spatialBlend=0;source.volume=1;source.priority=32;
            AudioListener.volume=WorkshopAudio.Volume;
        }
        public bool ReplayCurrent()
        {
            if(!manager || manager.Mode!=WorkshopMode.Assembly)return false;
            var cue=CurrentInstructionCue();
            if(string.IsNullOrEmpty(cue))return false;
            pendingCues.Clear();PlayCue(cue);return true;
        }
        string CurrentInstructionCue()
        {
            if(manager.RepairActive)return manager.Repair.Complete?"repair-complete":"repair-step-"+manager.Repair.CompletedSteps;
            if(manager.ChapterComplete)return "complete";
            if(manager.Task!=AssemblyTask.Assemble)return "";
            var next=manager.NextHintPart();if(!next)return "";
            if(manager.RequiresWorkpieceOrientation && !manager.OrientationReady(next.Definition))
                return next.Definition.orientation==AssemblyOrientation.UpsideDown?"flip-upside-down":"flip-upright";
            var instruction=EngineLearningContent.ForPart(next.Definition);
            return string.IsNullOrEmpty(instruction.voiceId)?instruction.id:instruction.voiceId;
        }
        static string RepeatGroup(string cue)=>cue.StartsWith("piston-")?"piston":cue.StartsWith("valve-")?"valve":cue;
        static string RepeatCue(string group)=>group=="piston"?"repeat-piston":group=="valve"?"repeat-valve":"repeat-matching";
        void Update()
        {
            if(manager.Recipe==null)return;
            if(chapterVisit!=manager.ChapterVisit){chapterVisit=manager.ChapterVisit;spokenCues.Clear();repeatGroups.Clear();pendingCues.Clear();StopCue();currentKey="";if(manager.RepairActive)pendingCues.Enqueue("repair-introduction");}
            var ui=manager.GetComponent<AssemblyWorkshopUI>();var mobile=manager.GetComponent<MobileAssemblyController>();
            bool suspend=WorkshopTutorial.IsOpen || manager.DisplayActive || manager.Mode!=WorkshopMode.Assembly || (ui && ui.IsOpen) || (mobile && mobile.ChapterMenuOpen) || !MobileARSession.CanInteract(gameObject);
            if(suspend){if(!paused){source.Pause();paused=true;}return;}
            if(paused){source.UnPause();paused=false;}
            if(wasPlaying && !source.isPlaying){LastCueFinishedAt=Time.unscaledTime;nextCueAt=LastCueFinishedAt+(CurrentCueId!=null && CurrentCueId.StartsWith("flip-")?1.2f:.75f);}
            wasPlaying=source.isPlaying;
            var cue=CurrentInstructionCue();string key=manager.CurrentChapterId+":"+manager.Task+":"+cue;
            if(key!=currentKey)
            {
                currentKey=key;
                if(string.IsNullOrEmpty(cue)){pendingCues.Clear();StopCue();return;}
                if(cue.StartsWith("flip-") || manager.RepairActive || manager.ChapterComplete || spokenCues.Add(cue))pendingCues.Enqueue(cue);
            }
            if(loading!=null || source.isPlaying || Time.unscaledTime<nextCueAt)return;
            // Finish each explanation even when the learner places a part early.
            // Skip obsolete flip prompts if the learner has already turned it.
            while(pendingCues.Count>0)
            {
                var pending=pendingCues.Dequeue();if(pending.StartsWith("flip-") && pending!=cue)continue;
                PlayCue(pending);return;
            }
            if(cue.StartsWith("flip-") || manager.RepairActive || manager.ChapterComplete || manager.Task!=AssemblyTask.Assemble)return;
            var active=EngineLearningContent.ForPart(manager.NextHintPart()?.Definition);
            // Shared piston and valve lessons intentionally remain silent on repeats.
        }
        void StopCue()
        {
            if(loading!=null){StopCoroutine(loading);loading=null;}
            source.Stop();paused=false;wasPlaying=false;
        }
        void PlayCue(string cue)
        {
            StopCue();CurrentCueId=cue;
            var clip=library?library.Find(cue):null;
            if(clip)loading=StartCoroutine(PlayLoaded(clip));
        }
        IEnumerator PlayLoaded(AudioClip clip)
        {
            // Always suspend once so StartCoroutine assigns its handle before
            // this routine clears it, including already-loaded replay clips.
            yield return null;
            if(clip.loadState==AudioDataLoadState.Unloaded)clip.LoadAudioData();
            while(clip.loadState==AudioDataLoadState.Loading)yield return null;
            if(clip.loadState!=AudioDataLoadState.Failed){source.clip=clip;source.Play();LastCueStartedAt=Time.unscaledTime;if(paused)source.Pause();}
            loading=null;
        }
        void OnDisable(){pendingCues.Clear();StopCue();currentKey="";}
    }
}
