using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace EngineAssembly
{
    [RequireComponent(typeof(AssemblyManager))]
    public sealed class AssemblyNarrator : MonoBehaviour
    {
        AssemblyManager manager;AudioSource source;NarrationLibrary library;
        string currentKey="";Coroutine loading;bool paused,inventoryAnnouncementPending,inventoryAnnouncementPlayed;
        readonly HashSet<string> spokenCues=new HashSet<string>();
        readonly HashSet<string> repeatGroups=new HashSet<string>();
        int chapterVisit=-1;
        public AudioSource Source=>source;
        public bool HasSpokenSharedCue(string id)=>spokenCues.Contains(id);
        public string CurrentCueId { get; private set; }
        void Awake()
        {
            manager=GetComponent<AssemblyManager>();library=Resources.Load<NarrationLibrary>("NarrationLibrary");
            source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;source.spatialBlend=0;source.volume=1;source.priority=32;
            AudioListener.volume=WorkshopAudio.Volume;
        }
        public void NotifyInventoryStack(int count)
        {
            if(count>1 && !inventoryAnnouncementPlayed){inventoryAnnouncementPlayed=true;inventoryAnnouncementPending=true;}
        }
        public bool ReplayCurrent()
        {
            if(!manager || manager.Mode!=WorkshopMode.Assembly)return false;
            var cue=CurrentCueId;
            if(string.IsNullOrEmpty(cue) || !library || !library.Find(cue))cue=CurrentInstructionCue();
            if(string.IsNullOrEmpty(cue))return false;
            PlayCue(cue);return true;
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
            if(chapterVisit!=manager.ChapterVisit){chapterVisit=manager.ChapterVisit;spokenCues.Clear();repeatGroups.Clear();currentKey="";}
            var ui=manager.GetComponent<AssemblyWorkshopUI>();var mobile=manager.GetComponent<MobileAssemblyController>();
            bool suspend=manager.Mode!=WorkshopMode.Assembly || (ui && ui.IsOpen) || (mobile && mobile.ChapterMenuOpen) || !MobileARSession.CanInteract(gameObject);
            if(suspend){if(!paused){source.Pause();paused=true;}return;}
            var cue=CurrentInstructionCue();string key=manager.CurrentChapterId+":"+manager.Task+":"+cue;
            if(key!=currentKey)
            {
                currentKey=key;CurrentCueId=cue;
                if(string.IsNullOrEmpty(cue)){StopCue();return;}
                if(cue.StartsWith("flip-") || manager.RepairActive || manager.ChapterComplete || spokenCues.Add(cue))PlayCue(cue);
                else
                {
                    var instruction=EngineLearningContent.ForPart(manager.NextHintPart()?.Definition);
                    if((instruction.partIds.Count>1 || !string.IsNullOrEmpty(instruction.voiceId)) && repeatGroups.Add(RepeatGroup(cue)))PlayCue(RepeatCue(RepeatGroup(cue)));
                    else StopCue();
                }
            }
            else if(paused){source.UnPause();paused=false;}
            if(loading!=null || source.isPlaying)return;
            if(inventoryAnnouncementPending){inventoryAnnouncementPending=false;PlayCue("inventory-stack");return;}
            if(cue.StartsWith("flip-") || manager.RepairActive || manager.ChapterComplete || manager.Task!=AssemblyTask.Assemble)return;
            var active=EngineLearningContent.ForPart(manager.NextHintPart()?.Definition);
            if(active.partIds.Count<=1 || !spokenCues.Contains(cue))return;
            int installed=0;foreach(var id in active.partIds)if(manager.IsPlaced(id))installed++;
            if(installed>0 && installed<active.partIds.Count && repeatGroups.Add(RepeatGroup(cue)))PlayCue(RepeatCue(RepeatGroup(cue)));
        }
        void StopCue()
        {
            if(loading!=null){StopCoroutine(loading);loading=null;}
            source.Stop();paused=false;
        }
        void PlayCue(string cue)
        {
            StopCue();CurrentCueId=cue;
            var clip=library?library.Find(cue):null;
            if(clip)loading=StartCoroutine(PlayLoaded(clip));
        }
        IEnumerator PlayLoaded(AudioClip clip)
        {
            if(clip.loadState==AudioDataLoadState.Unloaded)clip.LoadAudioData();
            while(clip.loadState==AudioDataLoadState.Loading)yield return null;
            if(clip.loadState!=AudioDataLoadState.Failed){source.clip=clip;source.Play();if(paused)source.Pause();}
            loading=null;
        }
        void OnDisable(){StopCue();currentKey="";}
    }
}
