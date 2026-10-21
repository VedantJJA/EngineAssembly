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
        public enum Page { Home, Experiences, Options }
        public Page CurrentPage { get; private set; }
        RectTransform safe,content;
        TMP_FontAsset body,heading;
        Vector2Int size;
        [SerializeField] Transform enginePreview;
        static readonly Color Ink=WorkshopTheme.Background,Panel=WorkshopTheme.Surface,Mint=WorkshopTheme.Accent,White=WorkshopTheme.Text,Muted=WorkshopTheme.Muted;
        void Awake()
        {
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
            safe=Rect("Safe area",canvasObject.transform);Stretch(safe);content=Rect("Menu content",safe);Stretch(content);Show(Page.Home);
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
            Text(content,"ENGINE / LAB",20,Mint,48,30,width-96,30,true);
            Text(content,page==Page.Home?"Your engine.\nYour experience.":page==Page.Experiences?"Select experience":"Make it your workshop",page==Page.Home?52:44,White,48,90,width-96,page==Page.Home?170:66,true);
            if(page==Page.Home)
            {
                Text(content,"Build, explore and repair a three-cylinder engine.",20,Muted,50,270,width-100,56);
                float w=Mathf.Min(580,width-96);
                ActionCard("SELECT EXPERIENCE","Choose your platform and activity",()=>Show(Page.Experiences),48,335,w,92,true);
                ActionCard("OPTIONS","Snap difficulty and sound",()=>Show(Page.Options),48,449,w,92);
                ActionCard("QUIT GAME","Leave Engine / Lab",Quit,48,563,w,92);
            }
            else if(page==Page.Experiences)
            {
                Text(content,"One engine. Four ways to explore it.",20,Muted,50,165,width-96,35);
                float w=(width-116)/2;
                ExperienceCard("01 / WINDOWS","Engine assembly","Carry components, build subassemblies and complete each chapter.","PLAY WORKSHOP  →",()=>Launch("AssemblyWorkshop"),48,230,w,true);
                ExperienceCard("02 / ANDROID · AR","Engine display","Place the engine on a real surface. Explore its timeline and components. Phone view is available when AR cannot run.","EXPLORE ENGINE  →",()=>Launch("MobileDisplayAR"),68+w,230,w,true);
                ExperienceCard("03 / ANDROID · AR","Engine assembly","Scan a surface, place the engine and build with the part cards. Phone view is available when AR cannot run.","BUILD ENGINE  →",()=>Launch("MobileAssemblyAR"),48,495,w,true);
                ExperienceCard("04 / META QUEST 2 · VR","Engine repair","Walk around the engine or use the sticks to move and teleport. Grab components and operate the lift controls.","REPAIR ENGINE  →",()=>Launch("RepairVRFallback"),68+w,495,w,true);
            }
            else
            {
                Text(content,"Assembly snap difficulty",23,White,50,220,width-96,42,true);
                string[] labels={"HARD","MEDIUM","EASY"};string[] descriptions={"Position and orientation","Position only","Carry, aim at the ghost and click"};
                int selected=PlayerPrefs.GetInt("Assembly.SnapDifficulty",0);float w=(width-136)/3;
                for(int i=0;i<3;i++){int index=i;ActionCard(labels[i]+(i==selected?"  /  ACTIVE":""),descriptions[i],()=>{PlayerPrefs.SetInt("Assembly.SnapDifficulty",index);PlayerPrefs.Save();Show(Page.Options);},48+i*(w+20),280,w,130,i==selected);}
                WorkshopVolumeSlider.Create(content,50,465,width-100);
            }
            if(page!=Page.Home)ActionCard("← MAIN MENU","",()=>Show(Page.Home),48,785,260,64);
            Text(content,"ENGINEERING · LEARN BY BUILDING",12,Muted,48,content.rect.height-40,width-96,25);
        }
        void ExperienceCard(string platform,string title,string description,string action,Action click,float x,float y,float w,bool available)
        {
            var card=Card(content,x,y,w,240,click);card.interactable=available;
            var accent=Rect("Accent",card.transform);Place(accent,0,0,w,3);accent.gameObject.AddComponent<UnityEngine.UI.Image>().color=available?Mint:new Color(.95f,.65f,.32f);
            Text(card.transform,platform,14,available?Mint:Muted,22,24,w-44,25,true);
            Text(card.transform,title,28,White,22,61,w-44,62,true);
            Text(card.transform,description,17,Muted,22,126,w-44,66);
            Text(card.transform,action,13,available?Mint:Muted,22,206,w-44,23,true);
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
        public static void Launch(string scene){Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;SceneManager.LoadScene(scene);if(scene=="RepairVRFallback" && QuestVRSession.Instance && !QuestVRSession.Instance.Active)QuestVRSession.Instance.Request();}
        public static void GoHome()=>Launch("MainMenu");
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
