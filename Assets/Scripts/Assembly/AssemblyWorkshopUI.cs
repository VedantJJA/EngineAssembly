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
        public enum Page { Closed, Chapters, Pause, Settings, Author, Library, Controls }
        public Page CurrentPage { get; private set; }=Page.Chapters;
        public bool IsOpen=>CurrentPage!=Page.Closed;
        public bool IsPaused=>IsOpen;
        public void MarkDirty()=>dirty=true;
        AssemblyManager manager;
        PlayerAssemblyController player;
        Canvas canvas;
        RectTransform surface,hud;
        TextMeshProUGUI hudTitle,hudStatus,hudHelp,notice;
        TextMeshProUGUI carryStatus;
        RectTransform inventoryPanel;
        UnityEngine.UI.RawImage inventoryIcon;
        TextMeshProUGUI inventoryCount,inventoryTitle;
        int browseChapterIndex;
        public int BrowsedChapterIndex=>browseChapterIndex;
        WorkshopQuestArrow questArrow;
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
            inventoryPanel=Box(hud,Ink,0,0,430,132);inventoryPanel.name="Held inventory";
            inventoryPanel.anchorMin=inventoryPanel.anchorMax=Vector2.zero;inventoryPanel.pivot=Vector2.zero;inventoryPanel.anchoredPosition=new Vector2(28,28);
            var icon=Box(inventoryPanel,Panel,12,12,108,108);icon.name="Inventory item card";
            var art=Rect("Inventory artwork",icon);Place(art,6,6,96,96);inventoryIcon=art.gameObject.AddComponent<UnityEngine.UI.RawImage>();inventoryIcon.raycastTarget=false;
            inventoryCount=Label(icon,"",20,White,44,70,62,36,true);inventoryCount.alignment=TextAlignmentOptions.Right;inventoryCount.textWrappingMode=TextWrappingModes.NoWrap;
            inventoryTitle=Label(inventoryPanel,"",19,White,140,17,276,58,true);
            carryStatus=Label(inventoryPanel,"",14,Mint,140,88,276,28);
            hudHelp=Label(hud,"",15,White,0,0,600,48);hudHelp.rectTransform.anchorMin=hudHelp.rectTransform.anchorMax=new Vector2(.5f,.5f);hudHelp.rectTransform.pivot=new Vector2(.5f,1);hudHelp.rectTransform.anchoredPosition=new Vector2(0,-28);hudHelp.alignment=TextAlignmentOptions.Center;
            AssemblyCaptionPresenter.Create(hud,manager,false);
            surface=Rect("Menu",root.transform);Stretch(surface);
            var bg=surface.gameObject.AddComponent<UnityEngine.UI.Image>();bg.color=Ink;bg.raycastTarget=true;
            savedTimeScale=Time.timeScale;Time.timeScale=0;
        }
        void Start() { if(!GetComponent<PCRepairChapter>())gameObject.AddComponent<PCRepairChapter>();player=FindAnyObjectByType<PlayerAssemblyController>();if(player){var arrow=Rect("Quest arrow",hud);questArrow=arrow.gameObject.AddComponent<WorkshopQuestArrow>();questArrow.Configure(hud,manager,player,bodyFont);}Rebuild(); }
        void Update()
        {
            if(InputHelper.IsKeyDown(KeyCode.F6))
            {
                ReleaseHeld();
                manager.SetMode(manager.Mode==WorkshopMode.Edit?WorkshopMode.Assembly:WorkshopMode.Edit);
                manager.SetInteraction(WorkshopInteraction.Build);Open(Page.Chapters);
            }
            if(InputHelper.IsKeyDown(KeyCode.Tab))Toggle();
            if(InputHelper.IsKeyDown(KeyCode.Escape)) { if(CurrentPage==Page.Controls)Open(Page.Settings);else if(CurrentPage==Page.Settings)Open(settingsBack);else if(CurrentPage==Page.Pause)SetOpen(false);else Open(Page.Pause); }
            if(CurrentPage==Page.Chapters){if(InputHelper.IsKeyDown(KeyCode.LeftArrow))BrowseChapter(-1);if(InputHelper.IsKeyDown(KeyCode.RightArrow))BrowseChapter(1);}
            if(CurrentPage==Page.Settings){if(InputHelper.IsKeyDown(KeyCode.LeftArrow))CycleDifficulty(-1);if(InputHelper.IsKeyDown(KeyCode.RightArrow))CycleDifficulty(1);}
            if(!IsOpen && manager.Mode==WorkshopMode.Edit && InputHelper.IsKeyDown(KeyCode.F7))Open(Page.Author);
            if(!IsOpen && InputHelper.IsKeyDown(KeyCode.H))manager.GetComponent<AssemblyHint>()?.Toggle();
            if(!IsOpen && InputHelper.IsKeyDown(KeyCode.V) && questArrow)questArrow.Toggle(!questArrow.Tracking);
            if(!IsOpen && manager.Mode==WorkshopMode.Assembly && manager.ChapterComplete && InputHelper.IsKeyDown(KeyCode.Return))manager.ContinueChapter();
            if(hudTitle)hudTitle.text=(manager.Mode==WorkshopMode.Edit?"EDITOR  ·  ":"CHAPTER "+(manager.CurrentChapterIndex+1)+"  ·  ")+(manager.Recipe==null?"Workshop":manager.Recipe.chapters[manager.CurrentChapterIndex].title);
            if(hudStatus)hudStatus.text=manager.ChapterInstalledCount+" / "+manager.ChapterPartCount+(manager.RepairActive?" steps":" parts")+"  ·  "+manager.Difficulty+(manager.ChapterComplete?"  ·  Chapter complete":"");
            int count=player && player.Inventory?player.Inventory.Count:0;inventoryPanel.gameObject.SetActive(count>0);
            if(count>0)
            {
                var part=player.CurrentHeldPart?player.CurrentHeldPart:player.Inventory.Items[0];
                inventoryTitle.text=AssemblyInventory.TypeName(part);inventoryCount.text="×"+count;
                inventoryIcon.texture=player.InventoryArtwork?player.InventoryArtwork.Find(part.PartId==PCRepairChapter.ReplacementId?PCRepairChapter.FaultyId:part.PartId):null;inventoryIcon.color=inventoryIcon.texture?Color.white:Color.clear;
                carryStatus.text=player.PlacementMode?"Placement · "+player.Tool:"Carry";
            }
            if(hudHelp)hudHelp.text=player?player.HoverMessage:"";
        }
        void ReleaseHeld() { if(!player)player=FindAnyObjectByType<PlayerAssemblyController>();if(player)player.DropHeldPart(); }
        public void Toggle()=>Open(IsOpen?Page.Closed:Page.Chapters);
        public void SetOpen(bool open)=>Open(open?Page.Chapters:Page.Closed);
        public void Open(Page page)
        {
            bool wasOpen=IsOpen;
            if(!wasOpen && page!=Page.Closed) { savedTimeScale=Time.timeScale;Time.timeScale=0; }
            if(wasOpen && page==Page.Closed)Time.timeScale=savedTimeScale;
            if(page==Page.Chapters && CurrentPage!=Page.Chapters)browseChapterIndex=manager.CurrentChapterIndex;
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
            string title=CurrentPage==Page.Chapters?"Choose your chapter":CurrentPage==Page.Pause?"Workshop paused":CurrentPage==Page.Settings?"Settings":CurrentPage==Page.Controls?"Controls":CurrentPage==Page.Author?"Chapter editor":"Recipe library";
            Label(surface,title,42,White,48,74,1120,60,true);
            Label(surface,CurrentPage==Page.Chapters?"Build an engine, one system at a time.":CurrentPage==Page.Author?"Arrange the sequence. Give equal priorities to interchangeable fasteners.":CurrentPage==Page.Settings?"Choose how precisely each part needs to be placed.":"Take your time. Your current assembly stays here.",18,Muted,50,139,1220,40);
            ActionCard(surface,"RETURN  /  ESC",()=>SetOpen(false),1190,80,200,46);
            if(CurrentPage==Page.Chapters)Chapters();
            else if(CurrentPage==Page.Pause)PauseMenu();
            else if(CurrentPage==Page.Settings)Settings();
            else if(CurrentPage==Page.Author)Author();
            else if(CurrentPage==Page.Library)Library();
            else if(CurrentPage==Page.Controls)Controls();
            var line=Box(surface,new Color(.15f,.2f,.24f),48,832,1344,1);line.name="Footer rule";
            notice=Label(surface,string.IsNullOrEmpty(message)?(dirty?"Unsaved recipe changes — use Save JSON in the chapter editor.":"TAB  Chapters     F6  "+(manager.Mode==WorkshopMode.Edit?"Switch to play":"Switch to editor")+"     ESC  Pause"):message,14,string.IsNullOrEmpty(message)?Muted:new Color(1,.66f,.4f),48,848,1344,38);
        }
        public void BrowseChapter(int delta)
        {
            if(manager.Recipe==null)return;
            browseChapterIndex=(browseChapterIndex+delta+manager.Recipe.chapters.Count)%manager.Recipe.chapters.Count;Rebuild();
        }
        void Chapters()
        {
            if(manager.Recipe==null){Label(surface,"Load an assembly recipe to begin.",22,White,50,230,900,60);return;}
            browseChapterIndex=Mathf.Clamp(browseChapterIndex,0,manager.Recipe.chapters.Count-1);
            var list=Scroll(surface,48,208,1344,500,3,428,236);list.name="Chapter card grid";
            for(int i=0;i<manager.Recipe.chapters.Count;i++)
            {
                int index=i;var chapter=manager.Recipe.chapters[i];bool repair=chapter.id==PCRepairChapter.ChapterId;
                if(repair && manager.Mode==WorkshopMode.Edit)continue;
                int count=manager.Recipe.parts.Count(p=>p.chapterId==chapter.id);
                var card=Card(list,428,236,()=>{ReleaseHeld();manager.SelectChapter(index);SetOpen(false);},repair || count>0 || manager.Mode==WorkshopMode.Edit);
                card.name="Chapter card · "+chapter.id;
                if(index==browseChapterIndex){card.normal=Hover;card.GetComponent<UnityEngine.UI.Image>().color=Hover;}
                var photo=Rect("Chapter image",card.transform);Place(photo,12,12,404,116);
                var raw=photo.gameObject.AddComponent<UnityEngine.UI.RawImage>();raw.texture=artwork?artwork.Find(manager.Recipe.title,repair?"chapter-5":chapter.id):null;raw.color=raw.texture?Color.white:Hover;raw.raycastTarget=false;
                if(!raw.texture)Label(photo,repair?"REPAIR":(index+1).ToString("00"),repair?40:64,Mint,20,16,364,80,true);
                Label(card.transform,repair?"BONUS · REPLACE AN IDLER BEARING":"CHAPTER "+(index+1).ToString("00")+" · "+(count==0?"DRAFT":count+" PARTS"),12,Mint,16,137,396,22,true);
                Label(card.transform,chapter.title,23,White,16,162,396,44,true);
                Label(card.transform,manager.Mode==WorkshopMode.Edit?"OPEN IN EDITOR →":repair || count>0?"PLAY CHAPTER →":"EMPTY DRAFT",13,Mint,16,211,396,22,true);
            }
            if(manager.Mode==WorkshopMode.Edit)
            {
                ActionCard(surface,"EDIT CHAPTERS",()=>Open(Page.Author),48,742,420,54,true);
                ActionCard(surface,"+ ADD CHAPTER",()=>{manager.AddChapter("Chapter "+(manager.Recipe.chapters.Count(c=>c.id!=PCRepairChapter.ChapterId)+1));browseChapterIndex=manager.CurrentChapterIndex;dirty=true;Open(Page.Author);},510,742,420,54);
                ActionCard(surface,"JSON LIBRARY",()=>Open(Page.Library),972,742,420,54);
            }
            else
            {
                if(!manager.RepairActive)ActionCard(surface,manager.Task==AssemblyTask.Assemble?"TASK: ASSEMBLE":"TASK: DISASSEMBLE",()=>{ReleaseHeld();manager.SetTask(manager.Task==AssemblyTask.Assemble?AssemblyTask.Disassemble:AssemblyTask.Assemble);Rebuild();},48,742,620,54);
                else Label(surface,"Repair follows its own removal and replacement steps.",16,Muted,48,753,620,42);
                ActionCard(surface,manager.ChapterComplete?"CONTINUE →":"RESUME CURRENT →",()=>{if(manager.ChapterComplete){ReleaseHeld();manager.ContinueChapter();}SetOpen(false);},772,742,620,54,true);
            }
        }
        void PauseMenu()
        {
            string[] labels={"CONTINUE","OPEN CHAPTERS","SETTINGS","MAIN MENU","QUIT GAME"};
            Action[] actions={()=>SetOpen(false),()=>Open(Page.Chapters),()=> { settingsBack=Page.Pause;Open(Page.Settings); },ExperienceMenu.GoHome,Quit};
            for(int i=0;i<labels.Length;i++)ActionCard(surface,labels[i],actions[i],440,218+i*111,560,78,i==0);
        }
        public void CycleDifficulty(int delta){manager.SetDifficulty((SnapDifficulty)(((int)manager.Difficulty+delta+3)%3));Rebuild();}
        void Settings()
        {
            var panel=Box(surface,Panel,220,240,1000,286);
            Label(panel,"SNAP DIFFICULTY",14,Mint,28,25,744,30,true);
            ActionCard(panel,"‹",()=>CycleDifficulty(-1),28,78,70,68);
            var value=Label(panel,manager.Difficulty.ToString(),34,White,120,86,744,54,true);value.alignment=TextAlignmentOptions.Center;
            ActionCard(panel,"›",()=>CycleDifficulty(1),902,78,70,68);
            string description=manager.Difficulty==SnapDifficulty.Hard?"Align position and rotation in placement mode, then click or release.":manager.Difficulty==SnapDifficulty.Medium?"Move the part to its ghost in placement mode. Rotation aligns automatically.":"Carry the part, look at its visible ghost and click. No orientation required.";
            Label(panel,description,20,Muted,28,186,944,70);
            ActionCard(surface,"CONTROLS",()=>Open(Page.Controls),220,570,1000,68);
            Label(surface,"Assembly order and required engine orientation apply at every difficulty.",17,Muted,220,674,1000,40);
            ActionCard(surface,"BACK",()=>Open(settingsBack),220,737,230,52);
        }
        void Controls()
        {
            var panel=Box(surface,Panel,160,204,1120,560);
            string[] keys={"W A S D / SHIFT","SPACE / CTRL or C","CLICK / E","Q","R + RIGHT MOUSE","SHIFT + RIGHT MOUSE","G / F","V / H / P","TAB / ESC / F6"};
            string[] descriptions={"Move / sprint","Jump / crouch","Pick up matching items; click an eligible ghost to place","Carry ↔ placement (Hard / Medium)","Toggle Move / Rotate, then drag with the right mouse button","Move held part nearer / farther when the Move tool is selected","Drop inventory / flip the active destination, even while carrying","Quest arrow / visual hints / instruction card","Chapter selector / pause / editor-play switch"};
            for(int i=0;i<keys.Length;i++){Label(panel,keys[i],16,Mint,28,24+i*57,310,42,true);Label(panel,descriptions[i],17,White,360,24+i*57,724,46);}
            ActionCard(surface,"BACK TO SETTINGS",()=>Open(Page.Settings),160,785,310,42);
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
            ActionCard(surface,"MOVE WORKPIECE",()=> { SetOpen(false);if(player)player.PickUpWorkpiece(); },673,754,250,48);
            ActionCard(surface,"VISIT CHAPTER",()=>SetOpen(false),943,754,215,48);
            ActionCard(surface,"FLIP BASE",()=> { SetOpen(false);manager.FlipWorkpiece(); },1178,754,214,48);
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
            var r=Rect("Panel",parent);Place(r,x,y,w,h);var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;WorkshopUIArt.Card(image);image.raycastTarget=false;return r;
        }
        TextMeshProUGUI Label(Transform parent,string text,float size,Color color,float x,float y,float w,float h,bool heading=false)
        {
            var r=Rect("Text",parent);Place(r,x,y,w,h);var label=r.gameObject.AddComponent<TextMeshProUGUI>();
            var font=heading?headingFont:bodyFont;if(font)label.font=font;
            label.text=text;label.fontSize=size;label.color=color;label.raycastTarget=false;label.textWrappingMode=TextWrappingModes.Normal;label.overflowMode=TextOverflowModes.Ellipsis;var shadow=r.gameObject.AddComponent<UnityEngine.UI.Shadow>();shadow.effectColor=new Color(0,0,0,.7f);shadow.effectDistance=new Vector2(1,-2);
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
            if(title=="‹" || title=="›")
            {
                var arrow=Label(card.transform,title,40,Mint,4,(h-54)/2,w-8,54,true);arrow.alignment=TextAlignmentOptions.Center;
            }
            else Label(card.transform,title,15,accent?Mint:White,18,(h-24)/2,w-36,26,true);
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

