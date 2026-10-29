using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EngineAssembly
{
    [RequireComponent(typeof(AssemblyManager))]
    public sealed class AssemblyNarrator:MonoBehaviour
    {
        sealed class Lesson {public string id;public AssemblyInstruction step;public AssemblyPart pickedPart;public EnginePartDetail purpose;public float purposeEnd;public int purposeWords;}
        AssemblyManager manager;AudioSource source;NarrationLibrary library;Coroutine loading;
        readonly Queue<Lesson> pending=new Queue<Lesson>();readonly HashSet<string> spokenCues=new HashSet<string>();
        Lesson current;AssemblyPart pickedPart;int chapterVisit=-1;string currentKey="";bool active,paused;float nextCueAt;
        public AudioSource Source=>source;
        public string CurrentCueId {get;private set;}
        public float LastCueStartedAt {get;private set;}
        public float LastCueFinishedAt {get;private set;}
        public bool HasSpokenSharedCue(string id)=>spokenCues.Contains(id);
        public bool HasDescriptions=>active && current?.step!=null || pending.Any(l=>l.step!=null);
        public EnginePartDetail CurrentDescription=>active && current?.step!=null && LessonComplete(current)?current.purpose:null;
        public int CaptionWordLimit=>CurrentDescription!=null?current.purposeWords:int.MaxValue;
        void Awake()
        {
            manager=GetComponent<AssemblyManager>();library=Resources.Load<NarrationLibrary>("NarrationLibrary");source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;source.spatialBlend=0;source.bypassReverbZones=true;source.dopplerLevel=0;source.volume=1;source.priority=32;AudioListener.volume=WorkshopAudio.Volume;
            manager.onPartSnapped.AddListener(PartInstalled);
        }
        void EnsureVisit()
        {
            if(chapterVisit==manager.ChapterVisit)return;chapterVisit=manager.ChapterVisit;pending.Clear();spokenCues.Clear();pickedPart=null;StopCue();currentKey="";nextCueAt=0;if(manager.RepairActive)pending.Enqueue(new Lesson{id="repair-introduction"});
        }
        bool InstructionComplete(AssemblyInstruction step)
        {
            if(step==null || step.partIds.Count==0)return false;bool found=false;foreach(var id in step.partIds){if(!manager.FindPart(id))continue;found=true;if(!manager.IsPlaced(id))return false;}return found;
        }
        bool LessonComplete(Lesson lesson)=>lesson.pickedPart?manager.IsPlaced(lesson.pickedPart.PartId):InstructionComplete(lesson.step);
        AssemblyPart InstructionPart()=>pickedPart && pickedPart.ChapterId==manager.CurrentChapterId && !manager.IsPlaced(pickedPart.PartId)?pickedPart:manager.NextHintPart();
        bool CanNarrate(AssemblyPart part)=>enabled && !manager.IsPreparing && manager.Mode==WorkshopMode.Assembly && !manager.DisplayActive && !manager.RepairActive && manager.CurrentChapterId!=WorkshopPractice.ChapterId && part && part.ChapterId==manager.CurrentChapterId;
        public void PartPickedUp(AssemblyPart part)
        {
            if(!CanNarrate(part) || manager.Task!=AssemblyTask.Assemble)return;EnsureVisit();pickedPart=part;
            var lesson=ForStep(EngineLearningContent.ForPart(part.Definition));lesson.pickedPart=part;
            // Unheard queued lessons may be requested later, but must not linger
            // behind the newly selected part. Already heard cues stay suppressed.
            foreach(var queued in pending)if(queued.id!=current?.id)spokenCues.Remove(queued.id);
            pending.Clear();currentKey="";nextCueAt=0;
            if(current?.id==lesson.id)
            {
                current.step=lesson.step;current.pickedPart=part;current.purpose=lesson.purpose;current.purposeWords=lesson.purposeWords;current.purposeEnd=lesson.purposeEnd;return;
            }
            StopCue();if(spokenCues.Add(lesson.id))pending.Enqueue(lesson);
        }
        Lesson ForStep(AssemblyInstruction step)
        {
            string id=string.IsNullOrEmpty(step.voiceId)?step.id:step.voiceId;int words=step.purpose?.Split(new[]{' ','\n','\r'},StringSplitOptions.RemoveEmptyEntries).Length??0;
            var cue=NarrationText.Find(id);float end=cue?.words!=null && cue.words.Length>=words && words>0?cue.words[words-1].end+.045f:0;
            var definition=step.partIds.Select(manager.FindPart).FirstOrDefault(p=>p)?.Definition;
            return new Lesson{id=id,step=step,purposeWords=words,purposeEnd=end,purpose=new EnginePartDetail{voiceId=id,title=definition?.displayName??"How this part works",description=step.purpose}};
        }
        void EnqueueStep(AssemblyInstruction step,bool installed=false)
        {
            var lesson=ForStep(step);if(spokenCues.Add(lesson.id)){pending.Enqueue(lesson);return;}
            // Shared piston/valve speech follows the copy the learner actually
            // built, rather than an unstarted canonical copy in another slot.
            if(!installed || !InstructionComplete(step))return;var bound=current?.id==lesson.id?current:pending.FirstOrDefault(l=>l.id==lesson.id);
            if(bound==null || InstructionComplete(bound.step))return;bound.step=step;bound.purpose=lesson.purpose;bound.purposeWords=lesson.purposeWords;bound.purposeEnd=lesson.purposeEnd;
        }
        void PartInstalled(AssemblyPart part)
        {
            if(!CanNarrate(part))return;
            EnsureVisit();EnqueueStep(EngineLearningContent.ForPart(part.Definition),true);
        }
        string FlipCue(AssemblyOrientation orientation)=>(orientation==AssemblyOrientation.UpsideDown?"flip-upside-down":"flip-upright")+(WorkshopTutorial.DetectPlatform(gameObject)=="PC"?"":"-spatial");
        string CurrentInstructionCue()
        {
            if(manager.RepairActive)return manager.Repair.Complete?"repair-complete":"repair-step-"+manager.Repair.CompletedSteps;
            if(manager.ChapterComplete)return "complete";if(manager.Task!=AssemblyTask.Assemble)return "";var next=InstructionPart();if(!next)return "";
            if(manager.RequiresWorkpieceOrientation && !manager.OrientationReady(next.Definition))return FlipCue(next.Definition.orientation);
            var step=EngineLearningContent.ForPart(next.Definition);return string.IsNullOrEmpty(step.voiceId)?step.id:step.voiceId;
        }
        public bool ReplayCurrent()
        {
            if(!manager || manager.Mode!=WorkshopMode.Assembly)return false;
            if(current!=null){Play(current);return true;}
            string id=CurrentInstructionCue();if(string.IsNullOrEmpty(id))return false;
            var part=InstructionPart();var lesson=!manager.RepairActive && !manager.ChapterComplete && !id.StartsWith("flip-") && part?ForStep(EngineLearningContent.ForPart(part.Definition)):new Lesson{id=id};if(part==pickedPart)lesson.pickedPart=part;Play(lesson);return true;
        }
        public void SilenceForCompletion()
        {
            // Preserve original purposes in queue order, without appending the
            // separate display-description recordings after installation.
            var remaining=pending.Where(l=>l.step!=null).ToArray();pending.Clear();foreach(var lesson in remaining)pending.Enqueue(lesson);
            if(current?.step==null)StopCue();
        }
        void Update()
        {
            if(manager.Recipe==null)return;EnsureVisit();var ui=manager.GetComponent<AssemblyWorkshopUI>();var mobile=manager.GetComponent<MobileAssemblyController>();var completion=manager.GetComponent<WorkshopSectionCompletion>();bool completing=completion && completion.HandlesCompletion;
            bool suspend=manager.CurrentChapterId==WorkshopPractice.ChapterId || WorkshopTutorial.IsOpen || completing && !HasDescriptions || manager.DisplayActive || manager.Mode!=WorkshopMode.Assembly || ui && ui.IsOpen || mobile && mobile.ChapterMenuOpen || !MobileARSession.CanInteract(gameObject) || Time.timeScale<=0;
            if(suspend){if(!paused){source.Pause();paused=true;}return;}if(paused){source.UnPause();paused=false;}
            string cue=CurrentInstructionCue();string key=manager.CurrentChapterId+":"+manager.Task+":"+cue;
            if(!completing && key!=currentKey)
            {
                currentKey=key;
                if(!string.IsNullOrEmpty(cue))
                {
                    if(cue.StartsWith("flip-") || manager.RepairActive || manager.ChapterComplete)pending.Enqueue(new Lesson{id=cue});
                    else {var next=InstructionPart();if(next)EnqueueStep(EngineLearningContent.ForPart(next.Definition));}
                }
            }
            if(active && loading==null)
            {
                // Continue a purpose already in progress, omitting only the
                // placement tail once its logical fitting step is complete.
                bool trimmed=current.step!=null && LessonComplete(current) && source.time>=PurposeEnd(current);
                bool obsoleteRepair=manager.RepairActive && current.id.StartsWith("repair-step-") && current.id!=cue;
                if(trimmed || obsoleteRepair || !source.isPlaying)Finish();
            }
            if(active || loading!=null || Time.unscaledTime<nextCueAt)return;
            while(pending.Count>0)
            {
                var lesson=pending.Dequeue();if(lesson.id.StartsWith("flip-") && lesson.id!=cue || manager.RepairActive && lesson.id.StartsWith("repair-step-") && lesson.id!=cue)continue;
                if(completing && lesson.step==null)continue;Play(lesson);return;
            }
        }
        float PurposeEnd(Lesson lesson)
        {
            if(lesson.purposeEnd>0)return Mathf.Min(source.clip?source.clip.length:lesson.purposeEnd,lesson.purposeEnd);
            if(lesson.purposeWords==0)return 0;var text=NarrationText.Find(lesson.id)?.text??lesson.step.narration;int total=Mathf.Max(1,text.Split(new[]{' ','\n','\r'},StringSplitOptions.RemoveEmptyEntries).Length);return source.clip?source.clip.length*lesson.purposeWords/total:0;
        }
        public float CurrentPurposeEnd=>current?.step!=null?PurposeEnd(current):0;
        void Finish(){source.Stop();active=false;LastCueFinishedAt=Time.unscaledTime;nextCueAt=LastCueFinishedAt+(current.id.StartsWith("flip-")?1.2f:.75f);}
        void StopCue(){if(loading!=null)StopCoroutine(loading);loading=null;source.Stop();active=false;paused=false;current=null;CurrentCueId=null;}
        void Play(Lesson lesson)
        {
            StopCue();current=lesson;CurrentCueId=lesson.id;var clip=library?library.Find(lesson.id):null;if(clip){active=true;loading=StartCoroutine(PlayLoaded(clip));}
        }
        IEnumerator PlayLoaded(AudioClip clip)
        {
            yield return null;if(clip.loadState==AudioDataLoadState.Unloaded)clip.LoadAudioData();while(clip.loadState==AudioDataLoadState.Loading)yield return null;
            if(clip.loadState==AudioDataLoadState.Failed){active=false;loading=null;yield break;}source.clip=clip;source.time=0;source.Play();LastCueStartedAt=Time.unscaledTime;if(paused)source.Pause();loading=null;
        }
        void OnDisable(){pending.Clear();StopCue();currentKey="";}
        void OnDestroy(){if(manager)manager.onPartSnapped.RemoveListener(PartInstalled);}
    }
}
