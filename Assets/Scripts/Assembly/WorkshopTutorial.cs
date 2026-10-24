using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace EngineAssembly
{
    // Section zero never changes recipe order or resets the player's build.
    public sealed class WorkshopTutorial : MonoBehaviour
    {
        [Serializable] public sealed class Lesson { public string id,platform,title,controls,text; }
        [Serializable] sealed class Catalogue { public Lesson[] lessons; }
        public static WorkshopTutorial Active { get; private set; }
        public static bool IsOpen=>Active;
        static int closedFrame=-1;
        public static bool BlocksInput=>IsOpen || closedFrame==Time.frameCount;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetState(){Active=null;closedFrame=-1;}
        public string Platform { get; private set; }
        public int LessonIndex { get; private set; }
        public int LessonCount=>lessons.Length;
        public string CurrentLessonId=>lessons[LessonIndex].id;
        public NarrationSpeech Speech=>speech;
        Lesson[] lessons;NarrationSpeech speech;RectTransform panel;
        TextMeshProUGUI title,body,controls,progress,next;
        float previousScale;CursorLockMode previousLock;bool previousCursor;
        public static string DetectPlatform(GameObject owner)
        {
            if(QuestVRSession.Instance && QuestVRSession.Instance.Active || QuestVRSession.IsQuestModel(SystemInfo.deviceModel))return "VR";
            if(owner.GetComponent<MobileAssemblyController>() || owner.GetComponent<MobileEngineDisplay>())return "AR";
            return ExperienceMenu.MobileWorkshopRequested?"Touch":"PC";
        }
        public static WorkshopTutorial Open(GameObject owner)
        {
            if(Active)return Active;
            var tutorial=new GameObject("Section 0 controls",typeof(RectTransform)).AddComponent<WorkshopTutorial>();
            tutorial.transform.SetParent(owner.transform,false);tutorial.Build(owner);return tutorial;
        }
        public static Lesson[] LessonsFor(string platform,bool scanning=true)
        {
            var data=Resources.Load<TextAsset>("WorkshopTutorial");
            return JsonUtility.FromJson<Catalogue>(data.text).lessons.Where(l=>(l.platform==platform || l.platform=="Display") && (scanning || l.id!="tutorial-ar-place")).ToArray();
        }
        void Build(GameObject owner)
        {
            Active=this;Platform=DetectPlatform(owner);var ar=owner.GetComponent<MobileARSession>();
            lessons=LessonsFor(Platform,ar && ar.State!=MobileARSession.SessionState.Fallback);
            if(owner.GetComponent<MobileEngineDisplay>())lessons=lessons.Where(l=>l.id!="tutorial-ar-assemble" && l.id!="tutorial-ar-help").ToArray();
            previousScale=Time.timeScale;previousLock=Cursor.lockState;previousCursor=Cursor.visible;
            Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            speech=gameObject.AddComponent<NarrationSpeech>();speech.PlayWhilePaused=true;
            var canvas=gameObject.AddComponent<Canvas>();gameObject.AddComponent<WorkshopCanvasRegistration>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=500;
            var scaler=gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.matchWidthOrHeight=.5f;
            bool portrait=Platform!="VR" && Screen.height>Screen.width;scaler.referenceResolution=portrait?new Vector2(1080,1600):new Vector2(1440,900);
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var dim=gameObject.AddComponent<UnityEngine.UI.Image>();dim.color=WorkshopTheme.Overlay;dim.raycastTarget=true;
            panel=Rect("Guide card",transform);panel.anchorMin=panel.anchorMax=Vector2.one*.5f;panel.pivot=Vector2.one*.5f;panel.sizeDelta=portrait?new Vector2(984,1040):new Vector2(1100,700);
            var image=panel.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=WorkshopTheme.Surface;WorkshopUIArt.Card(image);
            Label(panel,"SECTION 0  /  WORKSHOP GUIDE",17,WorkshopTheme.Accent,32,26,760,30);
            Label(panel,Platform=="AR"?"PHONE / AR":Platform=="Touch"?"PHONE / GARAGE":Platform,15,WorkshopTheme.Muted,portrait?792:924,26,130,30);
            title=Label(panel,"",34,WorkshopTheme.Text,32,80,portrait?676:780,70);
            body=Label(panel,"",26,WorkshopTheme.Text,32,portrait?180:170,portrait?920:620,portrait?280:310);
            body.fontStyle=FontStyles.Bold;body.enableAutoSizing=true;body.fontSizeMin=22;body.fontSizeMax=26;
            var live=body.gameObject.AddComponent<LiveNarrationCaption>();live.Speaker=speech;live.Label=body;
            var keyPanel=Rect("Controls reference",panel);Place(keyPanel,portrait?32:700,portrait?480:170,portrait?920:368,portrait?280:310);
            var keyImage=keyPanel.gameObject.AddComponent<UnityEngine.UI.Image>();keyImage.color=WorkshopTheme.AccentWash;keyImage.raycastTarget=false;WorkshopUIArt.Card(keyImage);
            Label(keyPanel,"CONTROLS REFERENCE",14,WorkshopTheme.Accent,22,22,portrait?876:324,30);
            controls=Label(keyPanel,"",21,WorkshopTheme.Text,22,68,portrait?876:324,portrait?186:220);controls.lineSpacing=12;controls.enableAutoSizing=true;controls.fontSizeMin=16;controls.fontSizeMax=21;
            float bottom=portrait?824:526;
            progress=Label(panel,"",16,WorkshopTheme.Muted,32,bottom,portrait?920:1036,30);
            ActionCard("Previous",()=>Show(LessonIndex-1),32,bottom+70,190);
            ActionCard("Replay voice / R",()=>Show(LessonIndex),244,bottom+70,250);
            next=ActionCard("Next",()=>{if(LessonIndex+1==lessons.Length)Close();else Show(LessonIndex+1);},portrait?738:854,bottom+70,214);
            ActionCard("Close guide",Close,portrait?738:854,80,214);Show(0);
        }
        public void Show(int index)
        {
            LessonIndex=Mathf.Clamp(index,0,lessons.Length-1);var lesson=lessons[LessonIndex];
            title.text=lesson.title;body.text=lesson.text;controls.text=lesson.controls;
            progress.text="LESSON "+(LessonIndex+1).ToString("00")+" / "+lessons.Length.ToString("00")+"   ·   Your assembly is paused";
            next.text=LessonIndex+1==lessons.Length?"RETURN TO EXPERIENCE":"NEXT →";speech.Speak(lesson.id);
        }
        void Update(){if(InputHelper.IsKeyDown(KeyCode.R))Show(LessonIndex);if(InputHelper.IsKeyDown(KeyCode.Escape))Close();}
        void LateUpdate(){if(speech && !speech.IsBusy && body)body.text=lessons[LessonIndex].text;}
        public void Close(){if(Active!=this)return;Active=null;closedFrame=Time.frameCount;speech.Stop();Time.timeScale=previousScale;Cursor.lockState=previousLock;Cursor.visible=previousCursor;Destroy(gameObject);}
        void OnDestroy(){if(Active==this){Active=null;Time.timeScale=previousScale;}}
        TextMeshProUGUI ActionCard(string text,Action action,float x,float y,float width)
        {
            var r=Rect(text,panel);Place(r,x,y,width,56);var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=WorkshopTheme.AccentWash;WorkshopUIArt.Card(image);
            var card=r.gameObject.AddComponent<WorkshopCard>();card.normal=WorkshopTheme.AccentWash;card.highlight=WorkshopTheme.Hover;card.clicked=action;
            var label=Label(r,text,17,WorkshopTheme.Accent,14,12,width-28,32);label.alignment=TextAlignmentOptions.Center;return label;
        }
        static RectTransform Rect(string name,Transform parent){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);return r;}
        static void Place(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=Vector2.up;r.pivot=Vector2.up;r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
        static TextMeshProUGUI Label(Transform parent,string text,float size,Color colour,float x,float y,float w,float h)
        {var r=Rect(text,parent);Place(r,x,y,w,h);var label=r.gameObject.AddComponent<TextMeshProUGUI>();label.font=Resources.Load<TMP_FontAsset>("WorkshopBody");label.text=text;label.fontSize=size;label.color=colour;label.raycastTarget=false;return label;}
    }
}
