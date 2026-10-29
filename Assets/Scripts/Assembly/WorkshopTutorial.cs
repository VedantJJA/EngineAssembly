using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace EngineAssembly
{
    // The guide is a caption overlay: learners can try every control while it is open.
    public sealed class WorkshopTutorial : MonoBehaviour
    {
        [Serializable] public sealed class PracticeAction { public string id,label;public float target; }
        [Serializable] public sealed class Lesson { public string id,platform,title,controls,text,difficulty;public int practiceOrder;public PracticeAction[] actions; }
        [Serializable] sealed class Catalogue { public Lesson[] lessons; }
        public static WorkshopTutorial Active { get; private set; }
        public static bool IsOpen=>Active;
        static int closedFrame=-1;
        public static bool BlocksInput=>closedFrame==Time.frameCount;
        public static bool AllowsLocomotion=>IsOpen;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetState(){Active=null;closedFrame=-1;}
        public string Platform { get; private set; }
        public int LessonIndex { get; private set; }
        public int LessonCount=>lessons.Length;
        public string CurrentLessonId=>lessons[LessonIndex].id;
        public bool IsContextual {get;private set;}
        public bool IsCompleteGuide {get;private set;}
        public GameObject Owner=>owner;
        public NarrationSpeech Speech=>speech;
        Lesson[] lessons;NarrationSpeech speech;RectTransform panel;
        TextMeshProUGUI title,body,controls,progress,next,tasks;WorkshopCard nextCard;
        float fallbackDeadline,readyAt=-1;bool narrationFinished;GameObject owner;WorkshopPractice practice;
        SnapDifficulty guideDifficulty;
        public bool ActionComplete=>!IsCompleteGuide || lessons[LessonIndex].actions==null || practice && lessons[LessonIndex].actions.All(a=>practice.Progress(a.id)>=a.target);
        public bool ReadyToContinue=>narrationFinished && ActionComplete;
        public static string DetectPlatform(GameObject owner)
        {
            if(QuestVRSession.Instance && QuestVRSession.Instance.Active || QuestVRSession.IsQuestModel(SystemInfo.deviceModel)){var rig=FindAnyObjectByType<QuestVRRig>();return rig && rig.HandTrackingActive?"Hands":"VR";}
            if(owner.GetComponent<MobileAssemblyController>() || owner.GetComponent<MobileEngineDisplay>())
            {var session=owner.GetComponent<MobileARSession>();return session && session.State!=MobileARSession.SessionState.Fallback?"AR":"Mobile";}
            return ExperienceMenu.MobileWorkshopRequested?"Touch":"PC";
        }
        public static WorkshopTutorial Open(GameObject owner)
        {
            if(WorkshopPractice.SkipOnPhone(owner))return null;if(Active)return Active;
            var tutorial=new GameObject("Section 0 controls",typeof(RectTransform)).AddComponent<WorkshopTutorial>();
            tutorial.transform.SetParent(owner.transform,false);tutorial.Build(owner);return tutorial;
        }
        public static Lesson[] LessonsFor(string platform,bool scanning=true)
        {
            var data=Resources.Load<TextAsset>("WorkshopTutorial");
            return JsonUtility.FromJson<Catalogue>(data.text).lessons.Where(l=>l.platform==platform && IsMovement(l.id) && (scanning || l.id!="tutorial-ar-place")).ToArray();
        }
        public static bool IsMovement(string id)=>id.EndsWith("-navigation") || id=="tutorial-ar-place" || id=="tutorial-ar-gestures";
        public static Lesson FindLesson(string id)=>JsonUtility.FromJson<Catalogue>(Resources.Load<TextAsset>("WorkshopTutorial").text).lessons.FirstOrDefault(l=>l.id==id);
        public static Lesson[] AllControlsFor(string platform,bool scanning=true,SnapDifficulty difficulty=SnapDifficulty.Easy)
        {
            var all=JsonUtility.FromJson<Catalogue>(Resources.Load<TextAsset>("WorkshopTutorial").text).lessons;
            return all.Where(l=>(l.platform==platform || platform=="Mobile" && (l.id=="tutorial-ar-assemble" || l.id=="tutorial-ar-help")) && l.id!="tutorial-display" && (scanning || l.id!="tutorial-ar-place") && MatchesDifficulty(l,difficulty)).OrderBy(l=>l.practiceOrder).ToArray();
        }
        static bool MatchesDifficulty(Lesson lesson,SnapDifficulty difficulty)=>string.IsNullOrEmpty(lesson.difficulty) || lesson.difficulty==difficulty.ToString() || lesson.difficulty=="Manual" && difficulty!=SnapDifficulty.Easy;
        public static WorkshopTutorial OpenAllControls(GameObject owner)
        {
            if(WorkshopPractice.SkipOnPhone(owner))return null;if(Active)return Active;var tutorial=new GameObject("Chapter 0 control practice",typeof(RectTransform)).AddComponent<WorkshopTutorial>();tutorial.transform.SetParent(owner.transform,false);tutorial.Build(owner,null,true);return tutorial;
        }
        public static WorkshopTutorial OpenLesson(GameObject owner,string id)
        {
            if(Active)return Active;if(FindLesson(id)==null)return null;
            var tutorial=new GameObject("Contextual control tip",typeof(RectTransform)).AddComponent<WorkshopTutorial>();tutorial.transform.SetParent(owner.transform,false);tutorial.Build(owner,id);return tutorial;
        }
        void Build(GameObject owner,string lessonId=null,bool allControls=false)
        {
            this.owner=owner;practice=owner.GetComponent<WorkshopPractice>();Active=this;Platform=DetectPlatform(owner);var ar=owner.GetComponent<MobileARSession>();
            guideDifficulty=owner.GetComponent<AssemblyManager>()?owner.GetComponent<AssemblyManager>().Difficulty:SnapDifficulty.Easy;
            IsCompleteGuide=allControls;IsContextual=!string.IsNullOrEmpty(lessonId);lessons=IsContextual?new[]{FindLesson(lessonId)}:allControls?AllControlsFor(Platform,ar && ar.State!=MobileARSession.SessionState.Fallback,guideDifficulty):LessonsFor(Platform,ar && ar.State!=MobileARSession.SessionState.Fallback);
            speech=gameObject.AddComponent<NarrationSpeech>();speech.Completed+=NarrationCompleted;
            var canvas=gameObject.AddComponent<Canvas>();gameObject.AddComponent<WorkshopCanvasRegistration>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=500;
            var scaler=gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.matchWidthOrHeight=.5f;
            bool vr=Platform=="VR" || Platform=="Hands";bool portrait=!vr && Screen.height>Screen.width;scaler.referenceResolution=portrait?new Vector2(1080,1920):new Vector2(1440,900);
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            panel=Rect("Guide card",transform);panel.anchorMin=panel.anchorMax=Vector2.one;panel.pivot=Vector2.one;panel.sizeDelta=portrait?new Vector2(1000,592):new Vector2(594,vr?690:592);panel.anchoredPosition=new Vector2(portrait?-40:-28,portrait?(Platform=="AR" && !IsContextual?-500:-132):-144);
            var image=panel.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=WorkshopTheme.Surface;WorkshopUIArt.Card(image);
            float width=panel.sizeDelta.x;
            Label(panel,allControls?"CHAPTER 0  /  PRACTICE CONTROLS":IsContextual?"CONTROLS  /  TRY IT NOW":"MOVEMENT CONTROLS",18,WorkshopTheme.Accent,22,16,width-120,28);
            ActionCard(IsCompleteGuide?"Ⅱ":"×",()=>{if(IsCompleteGuide)PausePractice();else Close();},width-66,12,48,40);
            title=Label(panel,"",27,WorkshopTheme.Text,22,58,width-44,42);
            body=Label(panel,"",23,WorkshopTheme.Text,22,110,width-44,158);body.fontStyle=FontStyles.Bold;body.overflowMode=TextOverflowModes.Ellipsis;
            var live=body.gameObject.AddComponent<LiveNarrationCaption>();live.Speaker=speech;live.Label=body;
            controls=Label(panel,"",23,WorkshopTheme.Text,22,278,width-44,vr?166:106);controls.lineSpacing=vr?3:6;
            controls.enableAutoSizing=true;controls.fontSizeMin=21;controls.fontSizeMax=23;
            tasks=Label(panel,"",21,WorkshopTheme.Text,22,vr?450:388,width-44,vr?132:106);tasks.lineSpacing=4;
            float progressTop=vr?594:500,actionsTop=vr?636:540;
            progress=Label(panel,"",17,WorkshopTheme.Muted,22,progressTop,width-44,28);progress.textWrappingMode=TextWrappingModes.NoWrap;progress.overflowMode=TextOverflowModes.Ellipsis;
            ActionCard("BACK",()=>Show(LessonIndex-1),22,actionsTop,112,38);
            ActionCard(Platform=="PC"?"REPLAY / R":"REPLAY",()=>Show(LessonIndex),148,actionsTop,154,38);
            next=ActionCard("NEXT",Advance,width-180,actionsTop,158,38);nextCard=next.transform.parent.GetComponent<WorkshopCard>();
            Show(0);if(vr)WorkshopTypography.ApplyVR(canvas);
        }
        public void Show(int index)
        {
            LessonIndex=Mathf.Clamp(index,0,lessons.Length-1);var lesson=lessons[LessonIndex];
            narrationFinished=false;readyAt=-1;
            title.text=lesson.title;var words=lesson.text.Split(new[]{' ','\n','\r'},StringSplitOptions.RemoveEmptyEntries);body.text=string.Join(" ",words.Take(28))+(words.Length>28?"…":"");controls.text=lesson.controls;
            RefreshProgress();speech.Speak(lesson.id);
            fallbackDeadline=Time.unscaledTime+Mathf.Max(6,words.Length*.4f);
        }
        void NarrationCompleted(string id){if(Active==this && id==CurrentLessonId)narrationFinished=true;}
        public void Advance(){if(!ReadyToContinue)return;if(LessonIndex+1<lessons.Length)Show(LessonIndex+1);else Close(true);}
        void PausePractice(){var ui=owner.GetComponent<AssemblyWorkshopUI>();if(ui)ui.Open(AssemblyWorkshopUI.Page.Pause);else owner.GetComponent<MobileAssemblyController>()?.GuidanceSettings.SetOpen(true);}
        void RefreshProgress()
        {
            string prefix=IsContextual?"TRY THIS CONTROL":(IsCompleteGuide?"CONTROLS ":"MOVEMENT ")+(LessonIndex+1).ToString("00")+" / "+lessons.Length.ToString("00");
            progress.text=prefix+(Platform=="PC"?"   ·   Hold ALT for guide buttons":"");
            tasks.text=IsCompleteGuide && lessons[LessonIndex].actions!=null?string.Join("\n",lessons[LessonIndex].actions.Select(a=>(practice && practice.Progress(a.id)>=a.target?"<color=#356EB0>✓ ":"<color=#566474>○ ")+a.label+"</color>")):"Try the controls at your own pace.";
            next.text=ReadyToContinue?(LessonIndex+1==lessons.Length?"FINISH":"NEXT →"):narrationFinished?"TRY IT NOW":"LISTEN";
            if(nextCard)nextCard.interactable=ReadyToContinue;
        }
        void Update()
        {
            body.enabled=WorkshopGuidance.Captions;
            var ui=owner.GetComponent<AssemblyWorkshopUI>();var mobile=owner.GetComponent<MobileAssemblyController>();bool menu=ui && ui.IsOpen || mobile && mobile.ChapterMenuOpen;
            speech.SetPaused(menu);panel.gameObject.SetActive(!menu);if(menu){readyAt=-1;return;}
            var manager=owner.GetComponent<AssemblyManager>();
            if(IsCompleteGuide && manager && guideDifficulty!=manager.Difficulty)
            {
                guideDifficulty=manager.Difficulty;var old=lessons[LessonIndex];var ar=owner.GetComponent<MobileARSession>();lessons=AllControlsFor(Platform,ar && ar.State!=MobileARSession.SessionState.Fallback,guideDifficulty);
                bool needsPlacement=Platform=="PC" && guideDifficulty!=SnapDifficulty.Easy && practice && (practice.Progress("placement-mode")<1 || guideDifficulty==SnapDifficulty.Hard && practice.Progress("manipulate-rotate")<5);
                int order=old.practiceOrder>=4 && (old.practiceOrder<=5 || needsPlacement)?4:old.practiceOrder;if(needsPlacement && order==4)practice.EnsureLooseCube();int index=Array.FindIndex(lessons,l=>l.practiceOrder>=order);Show(index<0?lessons.Length-1:index);
            }
            if(InputHelper.IsKeyDown(KeyCode.R))Show(LessonIndex);
            if(InputHelper.IsKeyDown(KeyCode.PageDown))Advance();
            if(InputHelper.IsKeyDown(KeyCode.PageUp))Show(LessonIndex-1);
            if(!ui && InputHelper.IsKeyDown(KeyCode.Escape)){if(IsCompleteGuide)PausePractice();else Close();}
            if(Active==this && Time.timeScale>0 && !speech.IsBusy && !speech.Source.clip && Time.unscaledTime>=fallbackDeadline)NarrationCompleted(CurrentLessonId);
            RefreshProgress();
            if(!ReadyToContinue || Time.timeScale<=0){readyAt=-1;return;}
            if(readyAt<0)readyAt=Time.unscaledTime+1.6f;
            if(Time.unscaledTime>=readyAt)Advance();
        }
        public void Close()=>Close(!IsCompleteGuide);
        public void Close(bool completed){if(Active!=this)return;Active=null;closedFrame=Time.frameCount;speech.Stop();if(completed && IsCompleteGuide && practice)practice.FinishControls();Destroy(gameObject);}
        void OnDestroy(){if(Active==this)Active=null;}
        TextMeshProUGUI ActionCard(string text,Action action,float x,float y,float width,float height)
        {
            var r=Rect(text,panel);Place(r,x,y,width,height);var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=WorkshopTheme.AccentWash;WorkshopUIArt.Card(image);
            var card=r.gameObject.AddComponent<WorkshopCard>();card.normal=WorkshopTheme.AccentWash;card.highlight=WorkshopTheme.Hover;card.clicked=action;
            var label=Label(r,text,20,WorkshopTheme.Accent,8,3,width-16,height-6);label.alignment=TextAlignmentOptions.Center;return label;
        }
        static RectTransform Rect(string name,Transform parent){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);return r;}
        static void Place(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=Vector2.up;r.pivot=Vector2.up;r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
        static TextMeshProUGUI Label(Transform parent,string text,float size,Color colour,float x,float y,float w,float h)
        {var r=Rect(text,parent);Place(r,x,y,w,h);var label=r.gameObject.AddComponent<TextMeshProUGUI>();label.font=Resources.Load<TMP_FontAsset>("WorkshopBody");label.text=text;label.fontSize=size;label.color=colour;label.raycastTarget=false;return label;}
    }
}
