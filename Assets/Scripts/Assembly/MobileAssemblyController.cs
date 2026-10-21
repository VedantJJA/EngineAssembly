using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace EngineAssembly
{
    [RequireComponent(typeof(AssemblyManager))]
    public sealed class MobileAssemblyController : MonoBehaviour
    {
        [SerializeField] Camera viewCamera;
        [SerializeField] float engineRadius=1.7f;
        [SerializeField] Vector3 engineCenter;
        [SerializeField] MobilePartArtwork artwork;
        AssemblyManager manager;
        AssemblyEasyGuide guide;
        AssemblyPart selected;
        AssemblyVisual idlePreview;
        readonly Dictionary<string,WorkshopCard> cards=new Dictionary<string,WorkshopCard>();
        readonly Dictionary<AssemblyPart,Renderer[]> renderers=new Dictionary<AssemblyPart,Renderer[]>();
        readonly Dictionary<AssemblyPart,Collider[]> colliders=new Dictionary<AssemblyPart,Collider[]>();
        readonly Dictionary<AssemblyPart,Vector3> sourceScales=new Dictionary<AssemblyPart,Vector3>();
        readonly Dictionary<string,Vector3> targetScales=new Dictionary<string,Vector3>();
        RectTransform safe,cardContent,chapterPanel;
        Canvas canvas;
        TextMeshProUGUI title,status,actionText,selectionText;
        WorkshopCard next;
        WorkshopCard stepAssistCard;
        WorkshopQuestArrow questArrow;
        public WorkshopQuestArrow QuestArrow=>questArrow;
        TMP_FontAsset font,heading;
        Quaternion initialRotation;
        Vector3 initialPosition,authoredEngineScale;
        SubAssemblyDefinition fixture;
        Quaternion fixtureRotation;
        Vector3 fixturePosition,lastPanPointer;
        bool multiTouch,panAllowed,mousePanning;
        Transform fixtureStage;
        readonly Dictionary<string,Transform> workspaces=new Dictionary<string,Transform>();
        readonly Dictionary<string,Pose> workspaceHome=new Dictionary<string,Pose>();
        float fixtureRadius=1,zoomFactor=1,lastPinchDistance;
        Transform Workpiece=>fixture==null?manager.AssemblyRoot:fixtureStage;
        Vector3 FocusCenter=>fixture==null?engineCenter:Vector3.zero;
        float FocusRadius=>fixture==null?engineRadius*(manager?manager.EngineScale:1):fixtureRadius;
        public Transform ActiveWorkpiece=>Workpiece;
        public bool Ready=>ready;
        public Vector3 WorldFocus=>fixture!=null?AssemblyPresentationBounds.World(Workpiece,manager).center:Workpiece.TransformPoint(FocusCenter);
        public float PresentationRadius=>FocusRadius;
        public string ActiveWorkspaceId=>fixture?.id??"engine";
        public Transform WorkspaceFor(string id)=>workspaces.TryGetValue(id,out var stage)?stage:null;
        bool ready,refreshPending,dragging,pointerActive,overUI;
        Vector2 startPointer,lastPointer;
        Rect lastSafeArea;
        Vector2Int lastScreen;
        public AssemblyPart SelectedPart=>selected;
        public IReadOnlyCollection<string> VisiblePartIds=>cards.Keys;
        public Camera ViewCamera=>viewCamera;
        public bool ChapterMenuOpen=>chapterPanel && chapterPanel.gameObject.activeSelf;
        static readonly Color Background=WorkshopTheme.Background,Panel=WorkshopTheme.Surface,Mint=WorkshopTheme.Accent,TextColor=WorkshopTheme.Text,Muted=WorkshopTheme.Muted;
        SpatialExperienceRig spatialRig;

        public void Configure(Camera camera,float radius,MobilePartArtwork images,Vector3 center=default) { viewCamera=camera;engineRadius=radius;artwork=images;engineCenter=center; }
        IEnumerator Start()
        {
            manager=GetComponent<AssemblyManager>();
            while(manager.Recipe==null)yield return null;
            if(!viewCamera)viewCamera=Camera.main;
            spatialRig=SpatialExperienceRig.Ensure(gameObject,manager.AssemblyRoot,viewCamera);
            spatialRig.CameraChanged+=SetPresentationCamera;
            viewCamera.backgroundColor=Background;
            manager.SetDifficulty(SnapDifficulty.Easy,false);
            manager.SetInteraction(WorkshopInteraction.Build);
            manager.SetTask(AssemblyTask.Assemble);
            guide=manager.GetComponent<AssemblyEasyGuide>();
            if(!guide)guide=gameObject.AddComponent<AssemblyEasyGuide>();
            initialRotation=manager.AssemblyRoot.localRotation;
            initialPosition=manager.AssemblyRoot.localPosition;
            authoredEngineScale=manager.AssemblyRoot.lossyScale;
            idlePreview=new GameObject("Floating assembly preview").AddComponent<AssemblyVisual>();idlePreview.transform.SetParent(transform,false);
            foreach(var part in manager.AssemblyParts)
            {
                sourceScales[part]=part.transform.lossyScale;
                if(part.Definition!=null)targetScales[part.PartId]=manager.TargetFrame(part.Definition).lossyScale;
                renderers[part]=part.GetComponentsInChildren<Renderer>(true).Where(r=>r.GetComponentInParent<AssemblyPart>(true)==part).ToArray();
                // Preserve author-disabled colliders: the CoACD bake replaces the old broad hull.
                colliders[part]=part.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled && c.GetComponentInParent<AssemblyPart>(true)==part).ToArray();
            }
            CreateUI();
            AssemblyCaptionPresenter.Create(safe,manager,true);
            manager.Changed+=OnChanged;manager.onPartSnapped.AddListener(OnInstalled);
            ready=true;PrepareFixtureStage();RefreshChapter();UpdateViewport();
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        void OnChanged()=>refreshPending=true;
        public void AttachPresentationSources()
        {
            if(!ready || !spatialRig.IsTracked)return;
            foreach(var part in manager.AssemblyParts)
            {
                if(part.Definition==null || part.IsSnapped || part.IsBusy)continue;
                if(manager.CarrierDefinition(part.PartId)!=null && part.transform.parent && part.transform.parent.name.StartsWith("Workspace"))continue;
                if(!part.transform.IsChildOf(spatialRig.ContentAnchor))part.transform.SetParent(spatialRig.ContentAnchor,true);
                SetTargetScale(part);
            }
        }
        static Vector3 Divide(Vector3 a,Vector3 b)=>new Vector3(a.x/b.x,a.y/b.y,a.z/b.z);
        void SetTargetScale(AssemblyPart part)
        {
            if(!spatialRig.IsTracked || !targetScales.TryGetValue(part.PartId,out var authored))return;
            var frame=manager.TargetFrame(part.Definition);
            var world=Vector3.Scale(sourceScales[part],Divide(frame.lossyScale,authored));
            part.transform.localScale=part.transform.parent?Divide(world,part.transform.parent.lossyScale):world;
        }
        void RestoreChapterScales()
        {
            if(!spatialRig.IsTracked)return;
            var ratio=Divide(manager.AssemblyRoot.lossyScale,authoredEngineScale);
            // Reset independently zoomed station pieces before the manager
            // installs previous chapters into the engine's reference frame.
            foreach(var pair in sourceScales.OrderBy(p=>p.Key.transform.GetComponentsInParent<Transform>(true).Length))
            {
                var world=Vector3.Scale(pair.Value,ratio);var part=pair.Key;
                part.transform.localScale=part.transform.parent?Divide(world,part.transform.parent.lossyScale):world;
            }
        }
        void OnInstalled(AssemblyPart part)
        {
            if(part==selected)selected=null;
            if(spatialRig.IsTracked)
            {
                var group=manager.MemberGroup(part);
                var frame=group!=null?WorkspaceFor(group.id):manager.AssemblyRoot;
                // New geometry can extend below an initially empty fixture.
                // Raise only its destination build, keeping every socket attached.
                GetComponent<MobileARSession>().KeepAboveWorkbench(frame);
            }
            refreshPending=true;
        }
        void OnDestroy()
        {
            if(spatialRig)spatialRig.CameraChanged-=SetPresentationCamera;
            if(manager) { manager.Changed-=OnChanged;manager.onPartSnapped.RemoveListener(OnInstalled); }
            foreach(var stage in workspaces.Values)if(stage)Destroy(stage.gameObject);
        }
        void Update()
        {
            if(!ready)return;
            if(stepAssistCard){var assist=manager.GetComponent<AssemblyStepAssist>();stepAssistCard.interactable=assist && assist.Available;}
            if(refreshPending) { refreshPending=false;RefreshChapter(); }
            if(lastScreen!=new Vector2Int(Screen.width,Screen.height) || lastSafeArea!=Screen.safeArea)UpdateViewport();
            ReadPointer();
        }
        public bool SelectPart(string id)
        {
            if(!ready || !MobileARSession.CanInteract(gameObject) || ChapterMenuOpen || manager.AssemblyParts.AnyBusy())return false;
            var part=manager.FindPart(id);
            if(!part || part.IsSnapped || part.IsBusy || part.ChapterId!=manager.CurrentChapterId)return false;
            if(!manager.CanInstall(part,part.TargetSocket,out var reason)) { status.text=reason;return false; }
            if(selected==part)return true;
            if(selected && selected.IsBusy)return false;
            CancelSelection();
            SetTargetScale(part);
            if(!part.BeginGrab())return false;
            if(spatialRig.IsTracked)part.transform.SetParent(Workpiece,true);
            selected=part;guide.RefreshTargets();
            idlePreview.Clear();
            RefreshCards();
            selectionText.text=part.PartDisplayName;
            status.text="Tap the glowing target to install";
            return true;
        }
        public bool PrepareAssistedStep(AssemblyPart part)
        {
            if(!ready || ChapterMenuOpen)return false;
            CancelSelection();
            SetTargetScale(part);
            if(!part.BeginGrab())return false;if(spatialRig.IsTracked)part.transform.SetParent(Workpiece,true);selected=part;
            guide.RefreshTargets();selectionText.text=part.PartDisplayName;status.text="Next Step · Place this component";return true;
        }
        public bool TapTarget(Vector2 screenPosition)
        {
            if(!ready || !MobileARSession.CanInteract(gameObject) || ChapterMenuOpen || !selected || selected.IsBusy || !viewCamera.pixelRect.Contains(screenPosition))return false;
            var ray=viewCamera.ScreenPointToRay(screenPosition);
            if(guide.Aim(ray,100,viewCamera)!=selected){status.text="Tap the visible ghost. Drag to turn the engine.";return false;}
            var part=selected;
            bool result=part.GuidedSnap(part.TargetSocket);
            status.text=result?"Component placed":"Tap the visible ghost. Drag to turn the engine.";
            if(result)RefreshChapter();
            return result;
        }
        public void RotateEngine(Vector2 delta)
        {
            if(!ready || ChapterMenuOpen || !MobileARSession.CanInteract(gameObject))return;
            var pivot=WorldFocus;
            Workpiece.RotateAround(pivot,viewCamera.transform.right,delta.y*.3f);
            Workpiece.RotateAround(pivot,viewCamera.transform.up,-delta.x*.3f);
            if(spatialRig.IsTracked)GetComponent<MobileARSession>().KeepAboveWorkbench(Workpiece);
            RefreshCards();
            if(selected && !selected.IsBusy)
            {
                guide.RefreshTargets();
                status.text=manager.CanInstall(selected,selected.TargetSocket,out var reason)?"Tap the glowing target to install":reason;
            }
        }
        public void PanModel(Vector2 pixelDelta)
        {
            if(!ready || ChapterMenuOpen || !MobileARSession.CanInteract(gameObject))return;
            float depth=Vector3.Dot(WorldFocus-viewCamera.transform.position,viewCamera.transform.forward);
            var center=viewCamera.pixelRect.center;
            var from=viewCamera.ScreenToWorldPoint(new Vector3(center.x,center.y,depth));
            var to=viewCamera.ScreenToWorldPoint(new Vector3(center.x+pixelDelta.x,center.y+pixelDelta.y,depth));
            Workpiece.position+=to-from;
            if(spatialRig.IsTracked)GetComponent<MobileARSession>().KeepAboveWorkbench(Workpiece);
        }
        public void ZoomModel(float ratio)
        {
            if(!ready || ChapterMenuOpen || !MobileARSession.CanInteract(gameObject) || ratio<=0)return;
            if(spatialRig.IsTracked){GetComponent<MobileARSession>().Zoom(ratio,WorldFocus);return;}
            float next=Mathf.Clamp(zoomFactor*ratio,.4f,3.5f);float applied=next/zoomFactor;zoomFactor=next;
            var center=Workpiece.TransformPoint(FocusCenter);
            viewCamera.transform.position=center+(viewCamera.transform.position-center)/applied;
        }
        void PrepareFixtureStage()
        {
            foreach(var stage in workspaces.Values)if(stage)Destroy(stage.gameObject);
            workspaces.Clear();workspaceHome.Clear();fixtureStage=null;
            var groups=manager.Recipe.subAssemblies.Where(g=>manager.FindPart(g.rootPartId).ChapterId==manager.CurrentChapterId).OrderBy(g=>manager.FindPart(g.previewPartId).OrderIndex).ToArray();
            float spacing=groups.Length==0?0:Mathf.Max(.8f,groups.Max(g=>g.focusRadius)*2.4f);
            for(int i=0;i<groups.Length;i++)
            {
                var g=groups[i];var root=manager.FindPart(g.rootPartId);
                var stage=new GameObject("Workspace · "+root.PartDisplayName).transform;
                stage.SetParent(spatialRig.ContentAnchor,false);
                // Keep separate workspaces near the content anchor so a future
                // tracked AR camera does not have to follow offscreen storage.
                stage.SetPositionAndRotation(manager.AssemblyRoot.TransformPoint(engineCenter)+manager.AssemblyRoot.right*((i-(groups.Length-1)*.5f)*spacing*manager.AssemblyRoot.lossyScale.x),manager.AssemblyRoot.rotation);
                workspaces[g.id]=stage;workspaceHome[g.id]=new Pose(stage.localPosition,stage.localRotation);
                if(root.IsSnapped)continue;
                root.transform.SetParent(stage,false);root.transform.localScale=sourceScales[root];root.transform.localRotation=AssemblyManager.InitialFixtureRotation(g);
                root.transform.localPosition=-(root.transform.localRotation*Vector3.Scale(g.focusCenter,root.transform.localScale));
            }
            if(spatialRig.IsTracked){AttachPresentationSources();ArrangeTrackedWorkspaces();}
        }
        public void ArrangeTrackedWorkspaces()
        {
            var session=GetComponent<MobileARSession>();if(!session || !session.WorkspaceSurface)return;
            var stages=workspaces.Values.Where(s=>s).ToArray();
            for(int i=0;i<stages.Length;i++)
            {
                // Separate stations along the right side; the main engine stays on the left.
                float spacing=Mathf.Min(.3f,.8f/Mathf.Max(1,stages.Length-1));
                session.SeatBuild(stages[i],new Vector3(.5f,0,(i-(stages.Length-1)*.5f)*spacing));
                session.RememberHome(stages[i]);
                var id=workspaces.First(pair=>pair.Value==stages[i]).Key;
                workspaceHome[id]=new Pose(stages[i].localPosition,stages[i].localRotation);
            }
        }
        void CancelSelection()
        {
            if(!selected)return;
            if(selected.IsBusy)selected.CancelSnap();
            else selected.Release(false);
            var d=selected.Definition;
            if(manager.CarrierDefinition(selected.PartId)==null)selected.ResetLoose(manager.TrayFrame,d.trayPosition,d.trayRotation);
            else
            {
                selected.ResetLoose(manager.TrayFrame,d.trayPosition,d.trayRotation);var g=manager.CarrierDefinition(selected.PartId);
                var stage=WorkspaceFor(g.id);
                if(stage){selected.transform.SetParent(stage,false);selected.transform.localScale=sourceScales[selected];selected.transform.SetLocalPositionAndRotation(-Vector3.Scale(g.focusCenter,selected.transform.localScale),Quaternion.identity);}
                else selected.transform.SetPositionAndRotation(manager.AssemblyRoot.TransformPoint(g.benchPosition),manager.AssemblyRoot.rotation);
            }
            selected=null;
            if(ready)RefreshChapter();
        }
        public void ChooseChapter(int index)
        {
            if(!ready)return;CancelSelection();
            fixture=null;
            RestoreChapterScales();manager.SelectChapter(index);chapterPanel.gameObject.SetActive(false);
            zoomFactor=1;PrepareFixtureStage();
            cardContent.GetComponentInParent<UnityEngine.UI.ScrollRect>().horizontalNormalizedPosition=0;
            RefreshChapter();
        }
        void RefreshChapter()
        {
            AttachPresentationSources();
            var nextPart=selected?selected:manager.NextHintPart();
            var nextFixture=manager.MemberGroup(nextPart);
            bool focusChanged=fixture!=nextFixture;
            if(focusChanged)
            {
                fixture=nextFixture;
                if(fixture!=null)
                {
                    fixtureStage=WorkspaceFor(fixture.id);fixtureRadius=fixture.focusRadius;
                    var home=workspaceHome[fixture.id];fixturePosition=home.position;fixtureRotation=home.rotation;
                }
            }
            foreach(var part in manager.AssemblyParts)
            {
                bool visible=part.IsSnapped;
                var group=manager.MemberGroup(part);
                if(group!=null)visible=part.IsSnapped && (spatialRig.IsTracked || fixture!=null && group.id==fixture.id || fixture==null && manager.IsPlaced(group.rootPartId));
                else if(fixture!=null && !spatialRig.IsTracked)visible=false;
                bool moving=selected && selected.IsBusy && (part==selected || manager.MemberGroup(part)?.rootPartId==selected.PartId);
                visible|=moving;
                foreach(var renderer in renderers[part])if(renderer)renderer.enabled=visible;
                foreach(var collider in colliders[part])if(collider)collider.enabled=visible && !moving && !manager.IsCarriedMember(part);
            }
            title.text=manager.Recipe.chapters[manager.CurrentChapterIndex].title;
            actionText.text=manager.ChapterInstalledCount+" / "+manager.ChapterPartCount+" INSTALLED";
            selectionText.text=selected?selected.PartDisplayName:manager.ChapterComplete?"Section complete":"Choose a part below";
            status.text=manager.ChapterComplete?"Continue to the next chapter":selected?"Tap a visible target to install":fixture!=null?manager.FindPart(fixture.rootPartId).PartDisplayName+" · Separate workspace":"1 finger: rotate · 2 fingers: move / pinch to zoom";
            next.gameObject.SetActive(manager.ChapterComplete);
            var preview=manager.NextHintPart();
            idlePreview.Clear();
            RefreshCards();
            if(focusChanged) { zoomFactor=1;UpdateViewport();cardContent.GetComponentInParent<UnityEngine.UI.ScrollRect>().horizontalNormalizedPosition=0; }
        }
        void RefreshCards()
        {
            var parts=manager.Recipe.parts.Where(d=>d.chapterId==manager.CurrentChapterId && !manager.IsPlaced(d.id)
                && (fixture!=null?d.subAssemblyId==fixture.id:string.IsNullOrEmpty(d.subAssemblyId))).OrderBy(d=>d.order).ToArray();
            var ids=new HashSet<string>(parts.Select(d=>d.id));
            foreach(var id in cards.Keys.Where(id=>!ids.Contains(id)).ToArray()) { cards[id].gameObject.SetActive(false);Destroy(cards[id].gameObject);cards.Remove(id); }
            for(int i=0;i<parts.Length;i++)
            {
                var d=parts[i];var part=manager.FindPart(d.id);
                if(!cards.TryGetValue(d.id,out var card))
                {
                    string id=d.id;card=MakeCard(cardContent,()=>SelectPart(id));cards.Add(id,card);
                    var layout=card.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();layout.preferredWidth=218;layout.preferredHeight=236;
                    var photo=NewRect("Part image",card.transform);Anchor(photo,new Vector2(0,1),new Vector2(1,1),new Vector2(10,-145),new Vector2(-10,-10));
                    var image=photo.gameObject.AddComponent<UnityEngine.UI.RawImage>();var carrier=manager.CarrierDefinition(id);image.texture=artwork?artwork.Find(id)??artwork.Find(carrier?.previewPartId):null;image.color=image.texture?Color.white:new Color(.12f,.2f,.23f);image.raycastTarget=false;
                    Label(card.transform,d.order.ToString("000"),17,Mint,new Vector2(12,-148),new Vector2(190,24));
                    Label(card.transform,d.displayName,20,TextColor,new Vector2(12,-174),new Vector2(194,56));
                }
                card.transform.SetSiblingIndex(i);
                bool eligible=manager.CanInstall(part,part.TargetSocket,out _);
                card.normal=selected==part?new Color(.78f,.94f,.93f):Panel;card.highlight=new Color(.86f,.96f,.96f);
                card.GetComponent<UnityEngine.UI.Image>().color=card.normal;
                var group=card.GetComponent<CanvasGroup>();if(!group)group=card.gameObject.AddComponent<CanvasGroup>();group.alpha=eligible?1:.68f;
                // Future cards remain tappable so a useful ordering/orientation explanation can be shown.
            }
        }
        void CreateUI()
        {
            font=Resources.Load<TMP_FontAsset>("WorkshopBody");heading=Resources.Load<TMP_FontAsset>("WorkshopHeading");
            var root=NewRect("Mobile Assembly Canvas",transform);
            canvas=root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scaler=root.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;
            root.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            if(!FindAnyObjectByType<EventSystem>())
            {
                var events=new GameObject("Mobile Event System",typeof(EventSystem));events.transform.SetParent(transform,false);
#if ENABLE_INPUT_SYSTEM
                events.AddComponent<InputSystemUIInputModule>();
#else
                events.AddComponent<StandaloneInputModule>();
#endif
            }
            safe=NewRect("Safe area",root);Stretch(safe);
            var header=Box(safe,Panel);Anchor(header,new Vector2(0,1),Vector2.one,new Vector2(0,-184),Vector2.zero);
            Label(header,"ENGINE / LAB",22,Mint,new Vector2(28,-20),new Vector2(700,32));
            title=Label(header,"",36,TextColor,new Vector2(28,-62),new Vector2(770,84),true);
            title.rectTransform.anchorMax=Vector2.one;title.rectTransform.sizeDelta=new Vector2(-278,84);
            actionText=Label(header,"",19,Muted,new Vector2(28,-147),new Vector2(700,28));
            var chapters=MakeCard(header,ToggleChapters);Anchor((RectTransform)chapters.transform,Vector2.one,Vector2.one,new Vector2(-224,-66),new Vector2(-24,-16));CenterLabel(chapters.transform,"CHAPTERS",20);
            var home=MakeCard(header,ExperienceMenu.GoHome);Anchor((RectTransform)home.transform,Vector2.one,Vector2.one,new Vector2(-224,-128),new Vector2(-24,-78));CenterLabel(home.transform,"MAIN MENU",18);
            var rotate=MakeCard(safe,ResetView);
            var arrowLabel=(TextMeshProUGUI)null;
            var arrowToggle=MakeCard(safe,()=>{questArrow.Toggle(!questArrow.Tracking);arrowLabel.text=questArrow.Tracking?"ARROW ON":"ARROW OFF";});arrowToggle.name="Quest arrow toggle";
            Anchor((RectTransform)arrowToggle.transform,Vector2.up,Vector2.up,new Vector2(24,-248),new Vector2(244,-194));CenterLabel(arrowToggle.transform,"ARROW OFF",18);arrowLabel=arrowToggle.GetComponentInChildren<TextMeshProUGUI>();
            questArrow=NewRect("Quest arrow",safe).gameObject.AddComponent<WorkshopQuestArrow>();questArrow.ConfigureMobile(safe,manager,this,font);
            Anchor((RectTransform)rotate.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-212,348),new Vector2(-24,400));CenterLabel(rotate.transform,"RESET VIEW",18);
            var flip=MakeCard(safe,()=>RotateEngine(new Vector2(0,600)));
            Anchor((RectTransform)flip.transform,Vector2.zero,Vector2.zero,new Vector2(24,348),new Vector2(194,400));CenterLabel(flip.transform,"FLIP",18);
            var assist=MakeCard(safe,()=>manager.GetComponent<AssemblyStepAssist>()?.NextStep());assist.name="Next Step";
            stepAssistCard=assist;
            Anchor((RectTransform)assist.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-164,348),new Vector2(164,400));CenterLabel(assist.transform,"NEXT STEP  →",20);
            var footer=Box(safe,Panel);Anchor(footer,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,330));
            selectionText=Label(footer,"",25,TextColor,new Vector2(24,-14),new Vector2(970,40),true);
            status=Label(footer,"",18,Muted,new Vector2(24,-54),new Vector2(1000,30));
            var scrollRoot=NewRect("Part cards",footer);Anchor(scrollRoot,Vector2.zero,Vector2.one,new Vector2(16,8),new Vector2(-16,-90));
            var scroll=scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();scroll.horizontal=true;scroll.vertical=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            var viewport=NewRect("Viewport",scrollRoot);Stretch(viewport);viewport.gameObject.AddComponent<UnityEngine.UI.Image>();viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
            cardContent=NewRect("Ordered parts",viewport);cardContent.anchorMin=Vector2.zero;cardContent.anchorMax=new Vector2(0,1);cardContent.pivot=new Vector2(0,.5f);cardContent.sizeDelta=Vector2.zero;
            var layout=cardContent.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();layout.spacing=14;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=false;layout.childForceExpandHeight=true;
            cardContent.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().horizontalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport;scroll.content=cardContent;
            next=MakeCard(safe,()=> { RestoreChapterScales();if(!manager.ContinueChapter())ToggleChapters();else { fixture=null;zoomFactor=1;PrepareFixtureStage();RefreshChapter();UpdateViewport(); } });
            Anchor((RectTransform)next.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-220,430),new Vector2(220,498));CenterLabel(next.transform,"CONTINUE",25);
            chapterPanel=Box(safe,Background);Stretch(chapterPanel);
            Label(chapterPanel,"CHOOSE A CHAPTER",34,Mint,new Vector2(28,-26),new Vector2(820,58),true);
            var close=MakeCard(chapterPanel,()=>chapterPanel.gameObject.SetActive(false));Anchor((RectTransform)close.transform,Vector2.one,Vector2.one,new Vector2(-140,-82),new Vector2(-24,-22));CenterLabel(close.transform,"BACK",20);
            var list=NewRect("Chapter list",chapterPanel);Anchor(list,Vector2.zero,Vector2.one,new Vector2(24,24),new Vector2(-24,-118));
            var chapterScroll=list.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();chapterScroll.horizontal=false;
            var vp=NewRect("Viewport",list);Stretch(vp);vp.gameObject.AddComponent<UnityEngine.UI.Image>();vp.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
            var content=NewRect("Chapters",vp);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
            var vl=content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();vl.spacing=16;vl.childControlHeight=true;vl.childControlWidth=true;vl.childForceExpandHeight=false;
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;chapterScroll.viewport=vp;chapterScroll.content=content;
            for(int i=0;i<manager.Recipe.chapters.Count;i++)
            {
                int index=i;var chapter=manager.Recipe.chapters[i];var card=MakeCard(content,()=>ChooseChapter(index));
                card.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=106;CenterLabel(card.transform,(i+1).ToString("00")+"   "+chapter.title,27);
                card.interactable=manager.Recipe.parts.Any(p=>p.chapterId==chapter.id);
            }
            chapterPanel.gameObject.SetActive(false);
        }
        void ToggleChapters() { chapterPanel.gameObject.SetActive(!chapterPanel.gameObject.activeSelf);pointerActive=false; }
        void SetPresentationCamera(Camera camera){viewCamera=camera;if(ready)UpdateViewport();}
        public void ResetView(){if(spatialRig.IsTracked){GetComponent<MobileARSession>().ResetActiveBuild();return;}zoomFactor=1;Workpiece.SetLocalPositionAndRotation(fixture==null?initialPosition:fixturePosition,fixture==null?initialRotation:fixtureRotation);RotateEngine(Vector2.zero);UpdateViewport();}
        void UpdateViewport()
        {
            lastSafeArea=Screen.safeArea;lastScreen=new Vector2Int(Screen.width,Screen.height);
            safe.anchorMin=lastSafeArea.min/new Vector2(Screen.width,Screen.height);safe.anchorMax=lastSafeArea.max/new Vector2(Screen.width,Screen.height);safe.offsetMin=safe.offsetMax=Vector2.zero;
            Canvas.ForceUpdateCanvases();
            canvas.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution=Screen.width>Screen.height?new Vector2(1920,1080):new Vector2(1080,1920);
            Canvas.ForceUpdateCanvases();
            if(spatialRig.IsTracked)return;
            float bottom=lastSafeArea.yMin+410*canvas.scaleFactor,top=lastSafeArea.yMax-350*canvas.scaleFactor;
            viewCamera.pixelRect=new Rect(lastSafeArea.xMin,bottom,lastSafeArea.width,Mathf.Max(100,top-bottom));
            float vertical=viewCamera.fieldOfView*Mathf.Deg2Rad*.5f;
            float horizontal=Mathf.Atan(Mathf.Tan(vertical)*viewCamera.aspect);
            float distance=FocusRadius/Mathf.Sin(Mathf.Min(vertical,horizontal))*1.12f/zoomFactor;
            viewCamera.transform.SetPositionAndRotation(WorldFocus+new Vector3(0,0,-distance),Quaternion.identity);
        }
        void ReadPointer()
        {
            if(!MobileARSession.CanInteract(gameObject)){pointerActive=multiTouch=dragging=false;return;}
            if(ChapterMenuOpen)return;
            Vector2 position=Vector2.zero;bool down=false,pressed=false,up=false;
#if ENABLE_INPUT_SYSTEM
            if(Touchscreen.current!=null)
            {
                var touches=Touchscreen.current.touches.Where(t=>t.press.isPressed).ToArray();
                if(touches.Length>=2)
                {
                    Vector2 center=(touches[0].position.ReadValue()+touches[1].position.ReadValue())*.5f;
                    float span=Vector2.Distance(touches[0].position.ReadValue(),touches[1].position.ReadValue());
                    if(!multiTouch)panAllowed=touches.Length==2 && touches.All(t=>viewCamera.pixelRect.Contains(t.position.ReadValue()) && !PointerOverUI(t.position.ReadValue()));
                    else if(panAllowed && touches.Length==2) { PanModel(center-(Vector2)lastPanPointer);if(lastPinchDistance>10)ZoomModel(span/lastPinchDistance); }
                    if(touches.Length>2)panAllowed=false;
                    multiTouch=true;pointerActive=false;lastPanPointer=center;lastPinchDistance=span;return;
                }
                if(multiTouch) { if(touches.Length==0)multiTouch=false;pointerActive=false;return; }
            }
            if(Mouse.current!=null && viewCamera.pixelRect.Contains(Mouse.current.position.ReadValue()) && !PointerOverUI(Mouse.current.position.ReadValue()))ZoomModel(Mathf.Exp(Mouse.current.scroll.ReadValue().y/120*.12f));
            if(Mouse.current!=null && (Mouse.current.middleButton.isPressed || Mouse.current.middleButton.wasReleasedThisFrame))
            {
                var point=Mouse.current.position.ReadValue();
                if(Mouse.current.middleButton.wasPressedThisFrame){mousePanning=viewCamera.pixelRect.Contains(point)&&!PointerOverUI(point);lastPanPointer=point;}
                else if(mousePanning && Mouse.current.middleButton.isPressed)PanModel(point-(Vector2)lastPanPointer);
                lastPanPointer=point;if(Mouse.current.middleButton.wasReleasedThisFrame)mousePanning=false;pointerActive=false;return;
            }
            if(Touchscreen.current!=null && Touchscreen.current.primaryTouch.phase.ReadValue()!=UnityEngine.InputSystem.TouchPhase.None)
            {
                var touch=Touchscreen.current.primaryTouch;var phase=touch.phase.ReadValue();position=touch.position.ReadValue();down=phase==UnityEngine.InputSystem.TouchPhase.Began;pressed=touch.press.isPressed;up=phase==UnityEngine.InputSystem.TouchPhase.Ended;
                if(phase==UnityEngine.InputSystem.TouchPhase.Canceled){pointerActive=false;return;}
                if(Touchscreen.current.touches.Count(t=>t.press.isPressed)>1) { pointerActive=false;return; }
            }
            else if(Mouse.current!=null) { position=Mouse.current.position.ReadValue();down=Mouse.current.leftButton.wasPressedThisFrame;pressed=Mouse.current.leftButton.isPressed;up=Mouse.current.leftButton.wasReleasedThisFrame; }
#elif ENABLE_LEGACY_INPUT_MANAGER
            position=Input.mousePosition;down=Input.GetMouseButtonDown(0);pressed=Input.GetMouseButton(0);up=Input.GetMouseButtonUp(0);
#endif
            if(down)
            {
                overUI=PointerOverUI(position);pointerActive=!overUI && viewCamera.pixelRect.Contains(position);dragging=false;startPointer=lastPointer=position;
            }
            if(!pointerActive)return;
            if(pressed)
            {
                if(Vector2.Distance(position,startPointer)>12*canvas.scaleFactor)dragging=true;
                if(dragging)RotateEngine((position-lastPointer)/canvas.scaleFactor);
                lastPointer=position;
            }
            if(up) { if(!dragging && !PointerOverUI(position))TapTarget(position);pointerActive=false; }
        }
        bool PointerOverUI(Vector2 point)
        {
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
            return hits.Any(hit=>hit.module is UnityEngine.UI.GraphicRaycaster);
        }
        RectTransform NewRect(string name,Transform parent) { var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform; }
        static void Anchor(RectTransform r,Vector2 min,Vector2 max,Vector2 low,Vector2 high) { r.anchorMin=min;r.anchorMax=max;r.offsetMin=low;r.offsetMax=high; }
        static void Stretch(RectTransform r)=>Anchor(r,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        RectTransform Box(Transform parent,Color color) { var r=NewRect("Panel",parent);var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=true;WorkshopUIArt.Card(image);return r; }
        WorkshopCard MakeCard(Transform parent,System.Action action)
        {
            var r=Box(parent,WorkshopTheme.AccentWash);r.name="Mobile card";var card=r.gameObject.AddComponent<WorkshopCard>();card.normal=WorkshopTheme.AccentWash;card.highlight=WorkshopTheme.Hover;card.clicked=action;return card;
        }
        TextMeshProUGUI Label(Transform parent,string text,float size,Color color,Vector2 position,Vector2 dimensions,bool bold=false)
        {
            var r=NewRect("Label",parent);r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=position;r.sizeDelta=dimensions;
            var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=bold?heading:font;t.fontSize=size;t.color=color;t.text=text;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Ellipsis;return t;
        }
        void CenterLabel(Transform parent,string text,float size) { var t=Label(parent,text,size,TextColor,Vector2.zero,Vector2.one,true);Stretch(t.rectTransform);t.margin=new Vector4(12,4,12,4);t.alignment=TextAlignmentOptions.Center; }
    }
}

