using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
namespace EngineAssembly
{
    public sealed class ExperienceMenu : MonoBehaviour
    {
        public static bool MobileWorkshopRequested { get; private set; }
        public static string RequestedChapter="";
        public static bool MobileRepairRequested;
        int deviceMode;
        public enum Page { Home, Experiences, Options }
        public Page CurrentPage { get; private set; }
        RectTransform safe,content;
        TMP_FontAsset body,heading;
        Vector2Int size;
        [SerializeField] Transform enginePreview;
        static readonly Color Ink=WorkshopTheme.Background,Panel=WorkshopTheme.Surface,Mint=WorkshopTheme.Accent,White=WorkshopTheme.Text,Muted=WorkshopTheme.Muted;
        void Awake()
        {
            deviceMode=QuestVRSession.IsQuestModel(SystemInfo.deviceModel)?2:0;
            Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            body=Resources.Load<TMP_FontAsset>("WorkshopBody");heading=Resources.Load<TMP_FontAsset>("WorkshopHeading");
            AudioListener.volume=PlayerPrefs.GetFloat("Assembly.Volume",1);
            var canvasObject=new GameObject("Experience menu canvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));canvasObject.transform.SetParent(transform,false);
            canvasObject.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1440,900);scaler.matchWidthOrHeight=.5f;
            if(!FindAnyObjectByType<EventSystem>())
            {
                var events=new GameObject("Menu event system",typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                events.AddComponent<InputSystemUIInputModule>();
#else
                events.AddComponent<StandaloneInputModule>();
#endif
            }
            safe=Rect("Safe area",canvasObject.transform);Stretch(safe);
            var grid=Rect("Drafting grid",safe);Stretch(grid);var texture=grid.gameObject.AddComponent<UnityEngine.UI.RawImage>();texture.texture=WorkshopUIArt.DraftGrid;texture.uvRect=new Rect(0,0,16,10);texture.raycastTarget=false;
            content=Rect("Menu content",safe);Stretch(content);Show(Page.Home);
            if(Camera.main){Camera.main.backgroundColor=Ink;Camera.main.clearFlags=CameraClearFlags.SolidColor;}
        }
        void Update()
        {
            if(enginePreview)enginePreview.Rotate(Vector3.up,7*Time.unscaledDeltaTime,Space.World);
            if(size!=new Vector2Int(Screen.width,Screen.height)){size=new Vector2Int(Screen.width,Screen.height);var area=Screen.safeArea;safe.anchorMin=area.min/new Vector2(Screen.width,Screen.height);safe.anchorMax=area.max/new Vector2(Screen.width,Screen.height);safe.offsetMin=safe.offsetMax=Vector2.zero;Canvas.ForceUpdateCanvases();Show(CurrentPage);}
            if(InputHelper.IsKeyDown(KeyCode.Escape) && CurrentPage!=Page.Home)Show(Page.Home);
        }
        public void Show(Page page)
        {
            CurrentPage=page;foreach(Transform child in content){child.gameObject.SetActive(false);Destroy(child.gameObject);}Canvas.ForceUpdateCanvases();
            if(enginePreview)enginePreview.gameObject.SetActive(page==Page.Home);
            float width=content.rect.width;
            Text(content,"ENGINE / LAB",20,Mint,40,26,width-80,32,true);
            Text(content,page==Page.Home?"Learn the engine. Build it yourself.":page==Page.Experiences?"Choose your experience":"Settings",page==Page.Home?42:38,White,40,84,width-80,96,true);
            if(page==Page.Home)
            {
                Text(content,"A guided workshop in assembly, repair and engine systems.",20,Muted,42,188,width-84,64);
                float w=Mathf.Min(550,width-80);
                ActionCard("ENTER WORKSHOP","Choose assembly, repair or engine display",()=>Show(Page.Experiences),40,290,w,100,true);
                ActionCard("SETTINGS","Placement difficulty and sound",()=>Show(Page.Options),40,410,w,82);
                ActionCard("QUIT","",Quit,40,510,w,66);
            }
            else if(page==Page.Experiences)
            {
                Text(content,"Choose where to work, then choose an activity.",19,Muted,42,175,width-84,40);
                float tab=(width-104)/3;
                string[] modes={Application.isMobilePlatform?"PHONE WORKSHOP":"PC WORKSHOP","AUGMENTED REALITY","VIRTUAL REALITY"};
                for(int i=0;i<3;i++){int index=i;ActionCard(modes[i],"",()=>{deviceMode=index;Show(Page.Experiences);},40+i*(tab+12),232,tab,56,i==deviceMode);}
                bool narrow=width<1000;float w=narrow?width-80:(width-112)/3;
                string[] names={"Engine assembly","Engine repair","Engine display"};
                string[] descriptions={"Build the engine one chapter at a time. Learn why every part is needed.","Open the timing drive, replace a worn bearing and put the system back together.","Select components to hear how they work. Explore the complete engine."};
                for(int i=0;i<3;i++){int activity=i;float x=narrow?40:40+i*(w+16),y=narrow?322+i*184:338;
                    ActivityCard(i,names[i],descriptions[i],()=>LaunchActivity(activity),x,y,w,narrow);}
            }
            else
            {
                Text(content,"Placement difficulty",20,White,42,230,width-84,36,true);
                string[] labels={"Hard","Medium","Easy"};string[] descriptions={"Position and orient each component.","Position each component; rotation is assisted.","Carry a component and select its visible target."};
                int selected=PlayerPrefs.GetInt("Assembly.SnapDifficulty",0);float w=Mathf.Min(170,(width-104)/3);
                for(int i=0;i<3;i++){int index=i;ActionCard(labels[i],"",()=>{PlayerPrefs.SetInt("Assembly.SnapDifficulty",index);Show(Page.Options);},40+i*(w+12),290,w,56,i==selected);}
                Text(content,descriptions[Mathf.Clamp(selected,0,2)],18,Muted,42,370,width-84,64);
                WorkshopVolumeSlider.Create(content,42,475,Mathf.Min(880,width-84));
            }
            if(page!=Page.Home)ActionCard("BACK","",()=>Show(Page.Home),40,content.rect.height-105,180,58);
            Text(content,"EXPLORE  /  UNDERSTAND  /  BUILD",12,Muted,40,content.rect.height-35,width-80,24);
        }
        void LaunchActivity(int activity)
        {
            if(deviceMode==1){MobileRepairRequested=activity==1;Launch(activity==2?"MobileDisplayAR":"MobileAssemblyAR");return;}
            RequestedChapter=activity==1?PCRepairChapter.ChapterId:activity==2?PCDisplayChapter.ChapterId:"";
            if(deviceMode==2){MobileWorkshopRequested=false;Launch("AssemblyWorkshop");if(QuestVRSession.Instance && QuestVRSession.IsQuestModel(SystemInfo.deviceModel))QuestVRSession.Instance.Request();}
            else LaunchDesktopWorkshop();
        }
        void ActivityCard(int index,string title,string description,Action click,float x,float y,float w,bool compact)
        {
            var card=Card(content,x,y,w,compact?168:350,click);
            var imageFrame=Rect("Experience image frame",card.transform);Place(imageFrame,12,12,compact?132:w-24,compact?144:144);imageFrame.gameObject.AddComponent<UnityEngine.UI.Image>();imageFrame.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
            var picture=Rect("Experience artwork",imageFrame);Stretch(picture);var image=picture.gameObject.AddComponent<UnityEngine.UI.RawImage>();var art=Resources.Load<AssemblyChapterArtwork>("AssemblyChapterArtwork");image.texture=art?art.PresentationImage(EngineLearningContent.Data.recipeTitle,index==0?"chapter-1":index==1?"chapter-5":"chapter-9"):null;image.raycastTarget=false;picture.gameObject.AddComponent<CroppedArtwork>();
            float left=compact?162:22,top=compact?18:174,width=compact?w-184:w-44;
            Text(card.transform,(index+1).ToString("00")+" / "+title,23,White,left,top,width,38,true);
            Text(card.transform,description,17,Muted,left,top+47,width,compact?62:78);
            Text(card.transform,index==2?"EXPLORE ENGINE  →":"ENTER WORKSHOP  →",14,Mint,left,compact?132:308,width,26,true);
        }
        void ExperienceCard(string platform,string title,string description,string action,Action click,float x,float y,float w,bool available,Action phoneClick=null)
        {
            var card=Card(content,x,y,w,240,click);card.interactable=available;
            var accent=Rect("Accent",card.transform);Place(accent,0,0,w,3);accent.gameObject.AddComponent<UnityEngine.UI.Image>().color=WorkshopTheme.Brass;
            Text(card.transform,platform,14,available?Mint:Muted,22,24,w-44,25,true);
            Text(card.transform,title,28,White,22,61,w-44,62,true);
            Text(card.transform,description,17,Muted,22,126,w-44,66);
            Text(card.transform,action,13,available?Mint:Muted,22,206,phoneClick==null?w-44:(w-44)*.44f,23,true);
            if(phoneClick!=null)
            {
                var mobile=Card(card.transform,w-190,197,170,36,phoneClick);mobile.name="Play workshop on phone";
                mobile.normal=WorkshopTheme.AccentWash;mobile.GetComponent<UnityEngine.UI.Image>().color=mobile.normal;
                Text(mobile.transform,"PLAY ON PHONE  →",12,Mint,10,8,150,24,true);
            }
        }
        void ActionCard(string title,string description,Action click,float x,float y,float w,float h,bool accent=false)
        {
            var card=Card(content,x,y,w,h,click);if(accent){card.normal=WorkshopTheme.AccentWash;card.GetComponent<UnityEngine.UI.Image>().color=card.normal;}
            Text(card.transform,title,19,accent?Mint:White,22,description.Length>0?18:(h-28)/2,w-44,34,true);
            if(description.Length>0)Text(card.transform,description,15,Muted,22,53,w-44,h-59);
        }
        WorkshopCard Card(Transform parent,float x,float y,float w,float h,Action click)
        {
            var r=Rect("Experience card",parent);Place(r,x,y,w,h);var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=Panel;WorkshopUIArt.Card(image);
            var card=r.gameObject.AddComponent<WorkshopCard>();card.normal=Panel;card.highlight=WorkshopTheme.Hover;card.clicked=click;return card;
        }
        void Text(Transform parent,string value,float fontSize,Color color,float x,float y,float w,float h,bool bold=false)
        { var r=Rect("Label",parent);Place(r,x,y,w,h);var label=r.gameObject.AddComponent<TextMeshProUGUI>();label.font=bold?heading:body;label.text=value;label.fontSize=fontSize;label.color=color;label.raycastTarget=false;label.textWrappingMode=TextWrappingModes.Normal;label.overflowMode=TextOverflowModes.Ellipsis; }
        static RectTransform Rect(string name,Transform parent){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);return r;}
        static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        static void Place(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=Vector2.up;r.pivot=Vector2.up;r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
        public static void LaunchMobileWorkshop()
        {
            MobileWorkshopRequested=true;
            if(Application.isMobilePlatform)Screen.orientation=ScreenOrientation.LandscapeLeft;
            Launch("AssemblyWorkshop");
        }
        public static void LaunchDesktopWorkshop(){if(Application.isMobilePlatform && !QuestVRSession.IsQuestModel(SystemInfo.deviceModel)){LaunchMobileWorkshop();return;}MobileWorkshopRequested=false;Launch("AssemblyWorkshop");}
        public static void Launch(string scene){if(scene!="AssemblyWorkshop")MobileWorkshopRequested=false;if(scene=="MainMenu" && Application.isMobilePlatform)Screen.orientation=ScreenOrientation.Portrait;Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;SceneManager.LoadScene(scene);if(scene=="RepairVRFallback" && QuestVRSession.Instance && !QuestVRSession.Instance.Active)QuestVRSession.Instance.Request();}
        public static void GoHome(){RequestedChapter="";MobileRepairRequested=false;Launch("MainMenu");}
        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
    }
}
