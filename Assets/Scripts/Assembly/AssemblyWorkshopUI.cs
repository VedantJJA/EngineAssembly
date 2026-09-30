using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace EngineAssembly
{
    [DefaultExecutionOrder(-100), RequireComponent(typeof(AssemblyManager))]
    public class AssemblyWorkshopUI : MonoBehaviour
    {
        public enum Page { Closed, Chapters, Pause, Settings, Author, Library }
        public Page CurrentPage { get; private set; }=Page.Chapters;
        public bool IsOpen=>CurrentPage!=Page.Closed;
        public bool IsPaused=>IsOpen;
        public void MarkDirty()=>dirty=true;
        AssemblyManager manager;
        PlayerAssemblyController player;
        Canvas canvas;
        RectTransform surface,hud,moveBadge;
        TextMeshProUGUI hudTitle,hudStatus,hudHelp,notice;
        TMP_FontAsset bodyFont,headingFont;
        AssemblyChapterArtwork artwork;
        string selectedId="",filename="three-cylinder",message="";
        bool showAllParts,dirty;
        float savedTimeScale=1;
        Page settingsBack=Page.Pause;
        static readonly Color Ink=new Color(.032f,.046f,.065f,.98f),Panel=new Color(.065f,.089f,.12f),Muted=new Color(.58f,.66f,.73f),White=new Color(.92f,.95f,.97f),Mint=new Color(.46f,.92f,.72f),Hover=new Color(.105f,.17f,.21f);
        void Awake()
        {
            manager=GetComponent<AssemblyManager>();bodyFont=Resources.Load<TMP_FontAsset>("WorkshopBody");headingFont=Resources.Load<TMP_FontAsset>("WorkshopHeading");
            artwork=Resources.Load<AssemblyChapterArtwork>("AssemblyChapterArtwork");
            var root=new GameObject("Workshop Canvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(transform,false);canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scaler=root.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1440,900);scaler.matchWidthOrHeight=.5f;
            if(!FindAnyObjectByType<EventSystem>())
            {
                var events=new GameObject("Workshop Event System",typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                events.AddComponent<InputSystemUIInputModule>();
#else
                events.AddComponent<StandaloneInputModule>();
#endif
                events.transform.SetParent(transform,false);
            }
            hud=Rect("Workshop HUD",root.transform);Stretch(hud);
            hudTitle=Label(hud,"",24,White,38,28,1020,38);
            hudStatus=Label(hud,"",17,Mint,38,68,1150,50);
            var crosshair=Label(hud,"+",24,White,0,0,32,32);
            crosshair.alignment=TextAlignmentOptions.Center;
            crosshair.rectTransform.anchorMin=crosshair.rectTransform.anchorMax=new Vector2(.5f,.5f);
            crosshair.rectTransform.pivot=new Vector2(.5f,.5f);crosshair.rectTransform.anchoredPosition=Vector2.zero;
            moveBadge=Box(hud,new Color(.10f,.25f,.23f),0,0,260,58);
            moveBadge.name="Move mode indicator";moveBadge.anchorMin=moveBadge.anchorMax=Vector2.one;
            moveBadge.pivot=Vector2.one;moveBadge.anchoredPosition=new Vector2(-24,-24);
            Label(moveBadge,"MOVE MODE ACTIVE",17,Mint,18,17,230,28,true);
            moveBadge.gameObject.SetActive(false);
            hudHelp=Label(hud,"",16,Muted,38,800,1340,75);
            surface=Rect("Menu",root.transform);Stretch(surface);
            var bg=surface.gameObject.AddComponent<UnityEngine.UI.Image>();bg.color=Ink;bg.raycastTarget=true;
            savedTimeScale=Time.timeScale;Time.timeScale=0;
        }
        void Start() { player=FindAnyObjectByType<PlayerAssemblyController>();Rebuild(); }
        void Update()
        {
            if(InputHelper.IsKeyDown(KeyCode.F6))
            {
                ReleaseHeld();
                manager.SetMode(manager.Mode==WorkshopMode.Edit?WorkshopMode.Assembly:WorkshopMode.Edit);
                manager.SetInteraction(WorkshopInteraction.Build);Open(Page.Chapters);
            }
            if(InputHelper.IsKeyDown(KeyCode.Tab))Toggle();
            if(InputHelper.IsKeyDown(KeyCode.Escape)) { if(CurrentPage==Page.Settings)Open(settingsBack);else if(CurrentPage==Page.Pause)SetOpen(false);else Open(Page.Pause); }
            if(!IsOpen && InputHelper.IsKeyDown(KeyCode.M)) { ReleaseHeld();manager.SetInteraction(manager.Interaction==WorkshopInteraction.Build?WorkshopInteraction.Move:WorkshopInteraction.Build); }
            if(!IsOpen && manager.Mode==WorkshopMode.Edit && InputHelper.IsKeyDown(KeyCode.F7))Open(Page.Author);
            if(!IsOpen && InputHelper.IsKeyDown(KeyCode.H))manager.GetComponent<AssemblyHint>()?.Toggle();
            if(!IsOpen && manager.Mode==WorkshopMode.Assembly && manager.ChapterComplete && InputHelper.IsKeyDown(KeyCode.Return))manager.ContinueChapter();
            if(hudTitle)hudTitle.text=(manager.Mode==WorkshopMode.Edit?"AUTHORING":manager.Task==AssemblyTask.Assemble?"ASSEMBLY":"DISASSEMBLY")+"  /  "+(manager.Recipe==null?"Workshop":manager.Recipe.chapters[manager.CurrentChapterIndex].title);
            if(hudStatus)hudStatus.text=manager.Interaction.ToString().ToUpper()+"  ·  "+manager.Difficulty+"  ·  "+manager.ChapterInstalledCount+" / "+manager.ChapterPartCount+" parts   "+(manager.ChapterComplete && manager.Mode==WorkshopMode.Assembly?"CHAPTER COMPLETE — ENTER to continue":manager.Status);
            if(moveBadge)moveBadge.gameObject.SetActive(manager.Interaction==WorkshopInteraction.Move);
            if(hudHelp)hudHelp.text=(player && player.CurrentHeldPart?player.CurrentHeldPart.PartDisplayName+"   "+player.CurrentHeldPart.RejectionReason+"\n":player && !string.IsNullOrEmpty(player.HoverMessage)?player.HoverMessage+"\n":"")+
                (manager.Difficulty==SnapDifficulty.Easy && manager.Task==AssemblyTask.Assemble && manager.Interaction==WorkshopInteraction.Build?"Pick up a part, then aim at its ghost + click   ·   E / G  Drop":"E / Click  Pick up & place   ·   RMB  Rotate held item / zoom   ·   Wheel  Distance")+
                "   ·   SPACE  Jump   ·   CTRL / C  Crouch   ·   H  Toggle hints"+
                "\nM  "+(manager.Interaction==WorkshopInteraction.Build?"Move mode":"Build mode")+"   ·   F  Flip base   ·   TAB  Chapters   ·   ESC  Pause   ·   F6  "+(manager.Mode==WorkshopMode.Edit?"Play mode   ·   F7  Chapter editor":"Edit mode");
        }
        void ReleaseHeld() { if(!player)player=FindAnyObjectByType<PlayerAssemblyController>();if(player)player.DropHeldPart(); }
        public void Toggle()=>Open(IsOpen?Page.Closed:Page.Chapters);
        public void SetOpen(bool open)=>Open(open?Page.Chapters:Page.Closed);
        public void Open(Page page)
        {
            bool wasOpen=IsOpen;
            if(!wasOpen && page!=Page.Closed) { ReleaseHeld();savedTimeScale=Time.timeScale;Time.timeScale=0; }
            if(wasOpen && page==Page.Closed)Time.timeScale=savedTimeScale;
            CurrentPage=page;surface.gameObject.SetActive(IsOpen);hud.gameObject.SetActive(!IsOpen);
            Cursor.lockState=IsOpen?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=IsOpen;
            if(IsOpen)Rebuild();
        }
        void OnDisable() { if(IsOpen)Time.timeScale=savedTimeScale;Cursor.lockState=CursorLockMode.None;Cursor.visible=true; }
        void Run(Action action) { try { action();message=""; }catch(Exception ex) { message=ex.Message; } if(IsOpen)Rebuild(); }
        void Rebuild()
        {
            if(!surface)return;
            foreach(Transform child in surface) { child.gameObject.SetActive(false);Destroy(child.gameObject); }
            hud.gameObject.SetActive(!IsOpen);
            Label(surface,"ENGINE / LAB",18,Mint,48,30,420,28,true);
            Label(surface,manager.Mode==WorkshopMode.Edit?"EDITOR WORKSPACE":"THREE-CYLINDER WORKSHOP",13,Muted,935,34,450,24);
            string title=CurrentPage==Page.Chapters?"Choose your chapter":CurrentPage==Page.Pause?"Workshop paused":CurrentPage==Page.Settings?"Make it your workshop":CurrentPage==Page.Author?"Chapter editor":"Recipe library";
            Label(surface,title,42,White,48,74,1120,60,true);
            Label(surface,CurrentPage==Page.Chapters?"Build an engine, one system at a time.":CurrentPage==Page.Author?"Arrange the sequence. Give equal priorities to interchangeable fasteners.":CurrentPage==Page.Settings?"Choose how precisely each part needs to be placed.":"Take your time. Your current assembly stays here.",18,Muted,50,139,1220,40);
            ActionCard(surface,"RETURN  /  ESC",()=>SetOpen(false),1190,80,200,46);
            if(CurrentPage==Page.Chapters)Chapters();
            else if(CurrentPage==Page.Pause)PauseMenu();
            else if(CurrentPage==Page.Settings)Settings();
            else if(CurrentPage==Page.Author)Author();
            else if(CurrentPage==Page.Library)Library();
            var line=Box(surface,new Color(.15f,.2f,.24f),48,832,1344,1);line.name="Footer rule";
            notice=Label(surface,string.IsNullOrEmpty(message)?(dirty?"Unsaved recipe changes — use Save JSON in the chapter editor.":"TAB  Chapters     F6  "+(manager.Mode==WorkshopMode.Edit?"Switch to play":"Switch to editor")+"     ESC  Pause"):message,14,string.IsNullOrEmpty(message)?Muted:new Color(1,.66f,.4f),48,848,1344,38);
        }
        void Chapters()
        {
            if(manager.Recipe==null) { Label(surface,"Load an assembly recipe to begin.",22,White,50,230,900,60);return; }
            var content=Scroll(surface,48,208,1010,595,3,316,264);
            for(int i=0;i<manager.Recipe.chapters.Count;i++)
            {
                int index=i;var chapter=manager.Recipe.chapters[i];int count=manager.Recipe.parts.Count(p=>p.chapterId==chapter.id);
                var card=Card(content,316,264,()=> { ReleaseHeld();manager.SelectChapter(index);SetOpen(false); },count>0 || manager.Mode==WorkshopMode.Edit);
                if(i==manager.CurrentChapterIndex)Box(card.transform,Mint,0,0,316,3);
                var photo=Rect("Chapter image",card.transform);Place(photo,12,12,292,134);
                var raw=photo.gameObject.AddComponent<UnityEngine.UI.RawImage>();raw.texture=artwork?artwork.Find(manager.Recipe.title,chapter.id):null;raw.color=raw.texture?Color.white:new Color(.105f,.15f,.18f);raw.raycastTarget=false;
                if(!raw.texture)
                {
                    Label(photo,(i+1).ToString("00"),76,new Color(.25f,.4f,.44f),15,-4,160,110,true);
                    Label(photo,"ENGINE\nASSEMBLY",14,Mint,171,70,112,54,true);
                }
                Label(card.transform,"CHAPTER "+(i+1).ToString("00")+"   /   "+(count==0?"DRAFT":count+" PARTS"),11,Mint,16,153,285,19,true);
                Label(card.transform,chapter.title,21,White,16,177,284,56,true);
                Label(card.transform,i==manager.CurrentChapterIndex?"CURRENT CHAPTER  →":manager.Mode==WorkshopMode.Edit?"OPEN IN EDITOR  →":"START ASSEMBLY  →",11,Muted,16,237,285,21);
            }
            var side=Box(surface,Panel,1080,208,312,595);
            Label(side,"YOUR WORKSHOP",13,Mint,24,24,264,24,true);
            Label(side,manager.Recipe.title,28,White,24,66,264,112,true);
            Label(side,manager.Mode==WorkshopMode.Edit?"Visit any chapter. Earlier chapters are assembled automatically.":"Select a chapter to practice, or complete your current chapter and continue.",17,Muted,24,193,264,126);
            ActionCard(side,manager.Interaction==WorkshopInteraction.Build?"BUILD MODE  /  M":"MOVE MODE  /  M",()=> { manager.SetInteraction(manager.Interaction==WorkshopInteraction.Build?WorkshopInteraction.Move:WorkshopInteraction.Build);Rebuild(); },24,335,264,50);
            if(manager.Mode==WorkshopMode.Edit)
            {
                ActionCard(side,"EDIT CHAPTERS",()=>Open(Page.Author),24,403,264,50,true);
                ActionCard(side,"+ ADD CHAPTER",()=> { manager.AddChapter("Chapter "+(manager.Recipe.chapters.Count+1));dirty=true;Open(Page.Author); },24,467,264,50);
                ActionCard(side,"JSON LIBRARY",()=>Open(Page.Library),24,531,264,42);
            }
            else
            {
                ActionCard(side,manager.Task==AssemblyTask.Assemble?"TASK: ASSEMBLE":"TASK: DISASSEMBLE",()=> { ReleaseHeld();manager.SetTask(manager.Task==AssemblyTask.Assemble?AssemblyTask.Disassemble:AssemblyTask.Assemble);Rebuild(); },24,397,264,46);
                ActionCard(side,manager.ChapterComplete?"CONTINUE  →":"RESUME CURRENT  →",()=> { if(manager.ChapterComplete)manager.ContinueChapter();SetOpen(false); },24,461,264,58,true);
                Label(side,"Scroll to explore all "+manager.Recipe.chapters.Count+" chapters",14,Muted,24,535,264,40);
            }
        }
        void PauseMenu()
        {
            string[] labels={"CONTINUE","OPEN CHAPTERS","SETTINGS","QUIT GAME"};
            Action[] actions={()=>SetOpen(false),()=>Open(Page.Chapters),()=> { settingsBack=Page.Pause;Open(Page.Settings); },Quit};
            for(int i=0;i<labels.Length;i++)ActionCard(surface,labels[i],actions[i],440,240+i*115,560,86,i==0);
        }
        void Settings()
        {
            string[] titles={"Hard","Medium","Easy"};
            string[] descriptions={"POSITION + ROTATION\n\nAlign the part with its ghost, including its orientation, then release.",
                "POSITION ONLY\n\nPlace the part in its ghost. Rotation aligns automatically when you release.",
                "PICK UP + CLICK\n\nPick up a part first. Then look directly at its visible ghost and click to place it."};
            for(int i=0;i<3;i++)
            {
                int difficulty=i;var card=Card(surface,420,385,()=> { manager.SetDifficulty((SnapDifficulty)difficulty);Rebuild(); });Place((RectTransform)card.transform,48+i*456,248,420,385);
                Label(card.transform,"0"+(i+1),55,new Color(.22f,.39f,.4f),28,22,320,75,true);
                Label(card.transform,titles[i],34,White,28,107,360,52,true);
                Label(card.transform,descriptions[i],18,Muted,28,183,360,146);
                Label(card.transform,(int)manager.Difficulty==i?"●  SELECTED":"SELECT DIFFICULTY  →",15,Mint,28,342,360,28,true);
            }
            Label(surface,"Assembly order and required base orientation apply at every difficulty.",17,Muted,50,674,1240,36);
            ActionCard(surface,"BACK",()=>Open(settingsBack),50,737,230,52);
        }
        void Author()
        {
            if(manager.Mode!=WorkshopMode.Edit) { Open(Page.Chapters);return; }
            if(manager.Recipe==null)return;
            var chapter=manager.Recipe.chapters[manager.CurrentChapterIndex];
            Field(surface,chapter.title,50,200,625,48,value=> { chapter.title=value;dirty=true; });
            ActionCard(surface,"+ CHAPTER",()=> { manager.AddChapter("New chapter");dirty=true;Rebuild(); },692,200,195,48);
            ActionCard(surface,"CHAPTER CARDS",()=>Open(Page.Chapters),907,200,220,48);
            ActionCard(surface,"JSON LIBRARY",()=>Open(Page.Library),1147,200,245,48);
            Field(surface,chapter.description,50,261,840,46,value=> { chapter.description=value;dirty=true; });
            ActionCard(surface,showAllParts?"ALL PARTS":"THIS CHAPTER",()=> { showAllParts=!showAllParts;Rebuild(); },910,261,225,46);
            ActionCard(surface,"PREVIEW",()=>Run(()=> { manager.LoadRecipe(manager.Recipe);manager.SetMode(WorkshopMode.Edit); }),1155,261,237,46);
            var list=Scroll(surface,50,328,800,400,1,774,60);
            foreach(var p in manager.Recipe.parts.Where(p=>showAllParts || p.chapterId==chapter.id).OrderBy(p=>p.order).ThenBy(p=>p.displayName))
            {
                var d=p;var row=Card(list,774,60,()=> { selectedId=d.id;Rebuild(); });
                Label(row.transform,d.order.ToString("000"),20,Mint,18,16,70,29,true);
                Label(row.transform,d.displayName,17,White,100,13,650,36);
            }
            var panel=Box(surface,Panel,876,328,516,400);
            var selected=manager.Recipe.parts.FirstOrDefault(p=>p.id==selectedId);
            if(selected==null)Label(panel,"Select a part to edit its order.\n\nUse ALL PARTS to move a part into a new chapter.",21,Muted,26,30,460,190);
            else
            {
                Label(panel,selected.displayName,23,White,24,22,468,65,true);
                Label(panel,"ASSEMBLY PRIORITY",12,Mint,24,99,244,22);
                Field(panel,selected.order.ToString(),286,89,206,44,value=> { if(int.TryParse(value,out int n)) { selected.order=Mathf.Max(0,n);dirty=true; } });
                ActionCard(panel,"MOVE TO THIS CHAPTER",()=>Run(()=> { var copy=AssemblyRecipeStore.Parse(JsonUtility.ToJson(manager.Recipe));copy.parts.First(p=>p.id==selected.id).chapterId=chapter.id;manager.LoadRecipe(copy);dirty=true; }),24,151,468,45);
                ActionCard(panel,"BASE: "+selected.orientation,()=> { selected.orientation=(AssemblyOrientation)(((int)selected.orientation+1)%3);dirty=true;Rebuild(); },24,209,468,45);
                ActionCard(panel,"CAPTURE TARGET POSE",()=>Run(()=> { manager.CaptureTarget(manager.FindPart(selected.id));dirty=true; }),24,267,468,45);
                ActionCard(panel,"CAPTURE TRAY POSE",()=>Run(()=> { manager.CaptureTray(manager.FindPart(selected.id));dirty=true; }),24,325,468,45);
            }
            Field(surface,filename,50,754,380,48,value=>filename=value);
            ActionCard(surface,"SAVE JSON",()=>Run(()=> { manager.Save(filename);dirty=false;message="Saved "+filename+".json"; }),448,754,205,48,true);
            ActionCard(surface,"MOVE WORKPIECE",()=> { manager.SetInteraction(WorkshopInteraction.Move);SetOpen(false);if(player)player.PickUpWorkpiece(); },673,754,250,48);
            ActionCard(surface,"VISIT CHAPTER",()=>SetOpen(false),943,754,215,48);
            ActionCard(surface,"FLIP BASE",()=> { SetOpen(false);manager.AssemblyRoot.GetComponent<PickupablePlatform>().Flip(); },1178,754,214,48);
        }
        void Library()
        {
            if(manager.Mode!=WorkshopMode.Edit) { Open(Page.Chapters);return; }
            var list=Scroll(surface,48,216,1344,500,1,1310,62);
            foreach(var path in AssemblyRecipeStore.Files())
            {
                var p=path;ActionCard(list,Path.GetFileName(p),()=>Run(()=> { manager.LoadRecipe(AssemblyRecipeStore.Load(p));filename=Path.GetFileNameWithoutExtension(p);dirty=false;Open(Page.Author); }),0,0,1310,62);
            }
            var catalog=Resources.Load<AssemblyRecipeCatalog>("AssemblyRecipeCatalog");
            if(!Application.isEditor && catalog)foreach(var asset in catalog.recipes)
            {
                var a=asset;if(a)ActionCard(list,"BUILT IN  /  "+a.name,()=>Run(()=> { manager.LoadRecipe(AssemblyRecipeStore.Parse(a.text));dirty=false;Open(Page.Author); }),0,0,1310,62);
            }
            Label(surface,"JSON files: "+AssemblyRecipeStore.DirectoryPath,14,Muted,48,737,1344,60);
        }
        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        RectTransform Rect(string name,Transform parent) { var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform; }
        static void Stretch(RectTransform r) { r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero; }
        static void Place(RectTransform r,float x,float y,float w,float h) { r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h); }
        RectTransform Box(Transform parent,Color color,float x,float y,float w,float h)
        {
            var r=Rect("Panel",parent);Place(r,x,y,w,h);var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return r;
        }
        TextMeshProUGUI Label(Transform parent,string text,float size,Color color,float x,float y,float w,float h,bool heading=false)
        {
            var r=Rect("Text",parent);Place(r,x,y,w,h);var label=r.gameObject.AddComponent<TextMeshProUGUI>();
            var font=heading?headingFont:bodyFont;if(font)label.font=font;
            label.text=text;label.fontSize=size;label.color=color;label.raycastTarget=false;label.textWrappingMode=TextWrappingModes.Normal;label.overflowMode=TextOverflowModes.Ellipsis;
            return label;
        }
        WorkshopCard Card(Transform parent,float w,float h,Action action,bool enabled=true)
        {
            var r=Box(parent,Panel,0,0,w,h);r.name="Custom Card";var image=r.GetComponent<UnityEngine.UI.Image>();image.raycastTarget=true;
            var card=r.gameObject.AddComponent<WorkshopCard>();card.normal=Panel;card.highlight=Hover;card.clicked=action;card.interactable=enabled;
            if(!enabled) { var group=r.gameObject.AddComponent<CanvasGroup>();group.alpha=.45f; }
            return card;
        }
        void ActionCard(Transform parent,string title,Action action,float x,float y,float w,float h,bool accent=false)
        {
            var card=Card(parent,w,h,action);Place((RectTransform)card.transform,x,y,w,h);
            if(accent) { card.normal=new Color(.10f,.25f,.23f);card.GetComponent<UnityEngine.UI.Image>().color=card.normal; }
            Label(card.transform,title,15,accent?Mint:White,18,(h-24)/2,w-36,26,true);
        }
        void Field(Transform parent,string value,float x,float y,float w,float h,Action<string> changed)
        {
            var rect=Box(parent,new Color(.09f,.13f,.17f),x,y,w,h);rect.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            var text=Label(rect,value,18,White,14,8,w-28,h-12);
            var input=rect.gameObject.AddComponent<TMP_InputField>();input.textViewport=rect;input.textComponent=text;input.targetGraphic=rect.GetComponent<UnityEngine.UI.Image>();input.text=value;input.caretColor=Mint;input.customCaretColor=true;
            input.onEndEdit.AddListener(s=>changed(s));
        }
        RectTransform Scroll(Transform parent,float x,float y,float w,float h,int columns,float cellW,float cellH)
        {
            var root=Rect("Scroll",parent);Place(root,x,y,w,h);var scroll=root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();scroll.horizontal=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            var viewport=Rect("Viewport",root);Stretch(viewport);var image=viewport.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=Color.white;viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
            var content=Rect("Content",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(0,1);content.sizeDelta=new Vector2(0,0);
            var grid=content.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();grid.cellSize=new Vector2(cellW,cellH);grid.spacing=new Vector2(20,20);grid.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=columns;
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport;scroll.content=content;return content;
        }
    }
}

