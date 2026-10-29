using UnityEngine;

namespace EngineAssembly
{
    [DisallowMultipleComponent]
    public sealed class WorkshopSectionCompletion:MonoBehaviour
    {
        public const float AdvanceDelay=5;
        public static bool VerificationEnabled {get;set;}
        AssemblyManager manager;NarrationSpeech speech;AudioSource celebration;AudioClip chime;
        int visit=-1,handledVisit=-1;float elapsed;bool voiceRequested,appPaused;
        public bool IsPresenting {get;private set;}
        public bool HandlesCompletion=>manager && handledVisit==manager.ChapterVisit && manager.ChapterComplete;
        public NarrationSpeech Speech=>speech;
        public AudioSource Celebration=>celebration;
        public float SecondsRemaining=>Mathf.Max(0,AdvanceDelay-elapsed);
        public int NextChapterIndex=>manager?.Recipe?.chapters.FindIndex(manager.CurrentChapterIndex+1,c=>c.id!=PCDisplayChapter.ChapterId && manager.IsPlayableChapter(c.id))??-1;
        public string StatusText=>manager && manager.GetComponent<AssemblyNarrator>() is AssemblyNarrator narrator && narrator.HasDescriptions?"Finishing the part description…":NextChapterIndex<0?"All sections completed.":SecondsRemaining>0?"Next section in "+Mathf.CeilToInt(SecondsRemaining)+" seconds":"Finishing the completion message…";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset()=>VerificationEnabled=false;
        void Awake()
        {
            manager=GetComponent<AssemblyManager>();chime=Resources.Load<AudioClip>("WorkshopSectionComplete");
            celebration=gameObject.AddComponent<AudioSource>();celebration.playOnAwake=false;celebration.spatialBlend=0;celebration.priority=64;celebration.volume=.7f;
            var voice=new GameObject("Section completion narration");voice.transform.SetParent(transform,false);speech=voice.AddComponent<NarrationSpeech>();
            manager.onChapterCompleted.AddListener(Begin);
        }
        public void Begin()
        {
            if(Application.isBatchMode && !VerificationEnabled || !manager || manager.IsPreparing || manager.Mode!=WorkshopMode.Assembly || manager.DisplayActive || !manager.ChapterComplete)return;
            if(handledVisit==manager.ChapterVisit)return;
            visit=handledVisit=manager.ChapterVisit;elapsed=0;voiceRequested=false;IsPresenting=true;
            if(WorkshopTutorial.Active && WorkshopTutorial.Active.Owner==gameObject)WorkshopTutorial.Active.Close(false);
            manager.GetComponent<AssemblyNarrator>()?.SilenceForCompletion();
            if(chime)celebration.PlayOneShot(chime);
            manager.Report(NextChapterIndex<0?"All sections completed.":"Section complete. The next section starts automatically.");
        }
        void Update()
        {
            if(!IsPresenting)return;
            if(!manager || manager.ChapterVisit!=visit || manager.Mode!=WorkshopMode.Assembly || manager.DisplayActive || !manager.ChapterComplete){Cancel();return;}
            var ui=manager.GetComponent<AssemblyWorkshopUI>();var mobile=manager.GetComponent<MobileAssemblyController>();
            bool paused=appPaused || Time.timeScale<=0 || ui && ui.IsOpen || mobile && mobile.ChapterMenuOpen || !MobileARSession.CanInteract(gameObject);
            speech.SetPaused(paused);if(paused){celebration.Pause();return;}celebration.UnPause();elapsed+=Time.unscaledDeltaTime;
            if(manager.GetComponent<AssemblyNarrator>() is AssemblyNarrator narrator && narrator.HasDescriptions){speech.SetPaused(true);elapsed=0;return;}
            // The short celebration leads into the spoken completion, rather than
            // competing with the explanation that was playing on the last part.
            if(!voiceRequested && elapsed>=.75f){voiceRequested=true;speech.Speak(manager.RepairActive?"repair-complete":"complete");}
            if(elapsed<AdvanceDelay || !voiceRequested || speech.IsBusy)return;
            int next=NextChapterIndex;IsPresenting=false;
            if(next<0)return;
            var player=FindAnyObjectByType<PlayerAssemblyController>();if(player)player.DropHeldPart(false);
            var vr=FindAnyObjectByType<QuestVRRig>();if(vr)vr.ReleaseForChapterChange();
            if(mobile)mobile.ChooseChapter(next);else manager.ContinueChapter();
        }
        void Cancel(){IsPresenting=false;speech.Stop();celebration.Stop();if(manager && !manager.ChapterComplete)handledVisit=-1;}
        void OnApplicationPause(bool paused)=>appPaused=paused;
        void OnDestroy(){if(manager)manager.onChapterCompleted.RemoveListener(Begin);}
    }
}
