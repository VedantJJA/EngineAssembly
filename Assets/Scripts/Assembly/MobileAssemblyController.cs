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
        RectTransform safe,cardContent,chapterPanel;
        Canvas canvas;
        TextMeshProUGUI title,status,actionText,selectionText;
        WorkshopCard next;
        TMP_FontAsset font,heading;
        Quaternion initialRotation;
        Vector3 initialPosition;
        SubAssemblyDefinition fixture;
        Quaternion fixtureRotation;
        Vector3 fixturePosition,lastPanPointer;
        bool multiTouch,panAllowed,mousePanning;
        Transform Workpiece=>fixture==null?manager.AssemblyRoot:manager.FindPart(fixture.rootPartId).transform;
        Vector3 FocusCenter=>fixture==null?engineCenter:fixture.focusCenter;
        float FocusRadius=>fixture==null?engineRadius:fixture.focusRadius;
        bool ready,refreshPending,dragging,pointerActive,overUI;
        Vector2 startPointer,lastPointer;
        Rect lastSafeArea;
        Vector2Int lastScreen;
        public AssemblyPart SelectedPart=>selected;
        public IReadOnlyCollection<string> VisiblePartIds=>cards.Keys;
        public Camera ViewCamera=>viewCamera;
        public bool ChapterMenuOpen=>chapterPanel && chapterPanel.gameObject.activeSelf;
        static readonly Color Background=new Color(.035f,.052f,.075f),Panel=new Color(.075f,.105f,.14f),Mint=new Color(.45f,.94f,.74f),TextColor=new Color(.92f,.96f,.98f),Muted=new Color(.57f,.67f,.75f);

        public void Configure(Camera camera,float radius,MobilePartArtwork images,Vector3 center=default) { viewCamera=camera;engineRadius=radius;artwork=images;engineCenter=center; }
        IEnumerator Start()
        {
            manager=GetComponent<AssemblyManager>();
            while(manager.Recipe==null)yield return null;
            if(!viewCamera)viewCamera=Camera.main;
            manager.SetDifficulty(SnapDifficulty.Easy,false);
            manager.SetInteraction(WorkshopInteraction.Build);
            manager.SetTask(AssemblyTask.Assemble);
            guide=manager.GetComponent<AssemblyEasyGuide>();
            if(!guide)guide=gameObject.AddComponent<AssemblyEasyGuide>();
            initialRotation=manager.AssemblyRoot.rotation;
            initialPosition=manager.AssemblyRoot.position;
            idlePreview=new GameObject("Floating assembly preview").AddComponent<AssemblyVisual>();idlePreview.transform.SetParent(transform,false);
            foreach(var part in manager.AssemblyParts)
            {
                renderers[part]=part.GetComponentsInChildren<Renderer>(true).Where(r=>r.GetComponentInParent<AssemblyPart>()==part).ToArray();
                // Preserve author-disabled colliders: the CoACD bake replaces the old broad hull.
                colliders[part]=part.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled && c.GetComponentInParent<AssemblyPart>()==part).ToArray();
            }
            CreateUI();
            manager.Changed+=OnChanged;manager.onPartSnapped.AddListener(OnInstalled);
            ready=true;RefreshChapter();UpdateViewport();
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        void OnChanged()=>refreshPending=true;
        void OnInstalled(AssemblyPart part) { if(part==selected)selected=null;refreshPending=true; }
        void OnDestroy()
        {
            if(manager) { manager.Changed-=OnChanged;manager.onPartSnapped.RemoveListener(OnInstalled); }
        }
        void Update()
        {
            if(!ready)return;
            if(refreshPending) { refreshPending=false;RefreshChapter(); }
            if(lastScreen!=new Vector2Int(Screen.width,Screen.height) || lastSafeArea!=Screen.safeArea)UpdateViewport();
            ReadPointer();
        }
        public bool SelectPart(string id)
        {
            if(!ready || ChapterMenuOpen)return false;
            var part=manager.FindPart(id);
            if(!part || part.IsSnapped || part.IsBusy || part.ChapterId!=manager.CurrentChapterId)return false;
            if(!manager.CanInstall(part,part.TargetSocket,out var reason)) { status.text=reason;return false; }
            if(selected==part)return true;
            if(selected && selected.IsBusy)return false;
            CancelSelection();
            if(!part.BeginGrab())return false;
            selected=part;guide.RefreshTargets();
            idlePreview.Clear();
            RefreshCards();
            selectionText.text=part.PartDisplayName;
            status.text="Tap the glowing target to install";
            return true;
        }
        public bool TapTarget(Vector2 screenPosition)
        {
            if(!ready || ChapterMenuOpen || !selected || selected.IsBusy || !viewCamera.pixelRect.Contains(screenPosition))return false;
            bool result=guide.Place(viewCamera.ScreenPointToRay(screenPosition),100);
            status.text=result?"Installing...":"Tap the visible ghost. Drag to turn the engine.";
            return result;
        }
        public void RotateEngine(Vector2 delta)
        {
            if(!ready || ChapterMenuOpen)return;
            var pivot=Workpiece.TransformPoint(FocusCenter);
            Workpiece.RotateAround(pivot,viewCamera.transform.right,delta.y*.3f);
            Workpiece.RotateAround(pivot,viewCamera.transform.up,-delta.x*.3f);
            RefreshCards();
            if(selected && !selected.IsBusy)
            {
                guide.RefreshTargets();
                status.text=manager.CanInstall(selected,selected.TargetSocket,out var reason)?"Tap the glowing target to install":reason;
            }
        }
        public void PanModel(Vector2 pixelDelta)
        {
            if(!ready || ChapterMenuOpen)return;
            float depth=Vector3.Dot(Workpiece.TransformPoint(FocusCenter)-viewCamera.transform.position,viewCamera.transform.forward);
            var center=viewCamera.pixelRect.center;
            var from=viewCamera.ScreenToWorldPoint(new Vector3(center.x,center.y,depth));
            var to=viewCamera.ScreenToWorldPoint(new Vector3(center.x+pixelDelta.x,center.y+pixelDelta.y,depth));
            Workpiece.position+=to-from;
        }
        void CancelSelection()
        {
            if(!selected)return;
            if(selected.IsBusy)selected.CancelSnap();
            else selected.Release(false);
            var d=selected.Definition;
            if(manager.CarrierDefinition(selected.PartId)==null)selected.ResetLoose(manager.TrayFrame,d.trayPosition,d.trayRotation);
            else { selected.ResetLoose(manager.TrayFrame,d.trayPosition,d.trayRotation);var g=manager.CarrierDefinition(selected.PartId);selected.transform.SetPositionAndRotation(manager.AssemblyRoot.TransformPoint(g.benchPosition),manager.AssemblyRoot.rotation); }
            selected=null;
        }
        public void ChooseChapter(int index)
        {
            if(!ready)return;CancelSelection();
            fixture=null;
            manager.SelectChapter(index);chapterPanel.gameObject.SetActive(false);
            cardContent.GetComponentInParent<UnityEngine.UI.ScrollRect>().horizontalNormalizedPosition=0;
            RefreshChapter();
        }
        void RefreshChapter()
        {
            var nextPart=selected?selected:manager.NextHintPart();
            var nextFixture=manager.MemberGroup(nextPart);
            bool focusChanged=fixture!=nextFixture;
            if(focusChanged)
            {
                fixture=nextFixture;
                if(fixture!=null)
                {
                    var root=manager.FindPart(fixture.rootPartId).transform;
                    root.rotation=manager.AssemblyRoot.rotation;
                    root.position=manager.AssemblyRoot.TransformPoint(engineCenter)+Vector3.right*5-root.TransformVector(fixture.focusCenter);
                    fixturePosition=root.position;fixtureRotation=root.rotation;
                }
            }
            foreach(var part in manager.AssemblyParts)
            {
                bool visible=part.IsSnapped;
                var group=manager.MemberGroup(part);
                if(group!=null)visible=part.IsSnapped && (group==fixture || (fixture==null && manager.IsPlaced(group.rootPartId)));
                else if(fixture!=null)visible=false;
                foreach(var renderer in renderers[part])if(renderer)renderer.enabled=visible;
                foreach(var collider in colliders[part])if(collider)collider.enabled=visible && !manager.IsCarriedMember(part);
            }
            title.text=manager.Recipe.chapters[manager.CurrentChapterIndex].title;
            actionText.text=manager.ChapterInstalledCount+" / "+manager.ChapterPartCount+" INSTALLED";
            selectionText.text=selected?selected.PartDisplayName:manager.ChapterComplete?"Section complete":"Choose a part below";
            status.text=manager.ChapterComplete?"Continue to the next chapter":selected?"Tap a visible target to install":fixture!=null?"BUILD SUBASSEMBLY · Select a part below":"1 finger: rotate · 2 fingers: move · Tap a card";
            next.gameObject.SetActive(manager.ChapterComplete);
            var preview=manager.NextHintPart();
            idlePreview.Clear();
            RefreshCards();
            if(focusChanged)UpdateViewport();
        }
        void RefreshCards()
        {
            var parts=manager.Recipe.parts.Where(d=>d.chapterId==manager.CurrentChapterId && !manager.IsPlaced(d.id)).OrderBy(d=>d.order).ThenBy(d=>d.displayName).ToArray();
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
                card.normal=selected==part?new Color(.10f,.31f,.26f):Panel;card.highlight=new Color(.12f,.25f,.28f);
                card.GetComponent<UnityEngine.UI.Image>().color=card.normal;
                var group=card.GetComponent<CanvasGroup>();if(!group)group=card.gameObject.AddComponent<CanvasGroup>();group.alpha=eligible?1:.43f;
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
            actionText=Label(header,"",19,Muted,new Vector2(28,-147),new Vector2(700,28));
            var chapters=MakeCard(header,ToggleChapters);Anchor((RectTransform)chapters.transform,Vector2.one,Vector2.one,new Vector2(-224,-66),new Vector2(-24,-16));CenterLabel(chapters.transform,"CHAPTERS",20);
            var rotate=MakeCard(safe,()=> { Workpiece.SetPositionAndRotation(fixture==null?initialPosition:fixturePosition,fixture==null?initialRotation:fixtureRotation);RotateEngine(Vector2.zero);UpdateViewport(); });
            Anchor((RectTransform)rotate.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-212,348),new Vector2(-24,400));CenterLabel(rotate.transform,"RESET VIEW",18);
            var flip=MakeCard(safe,()=>RotateEngine(new Vector2(0,600)));
            Anchor((RectTransform)flip.transform,Vector2.zero,Vector2.zero,new Vector2(24,348),new Vector2(194,400));CenterLabel(flip.transform,"FLIP",18);
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
            next=MakeCard(safe,()=> { if(!manager.ContinueChapter())ToggleChapters();else RefreshChapter(); });
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
        void UpdateViewport()
        {
            lastSafeArea=Screen.safeArea;lastScreen=new Vector2Int(Screen.width,Screen.height);
            safe.anchorMin=lastSafeArea.min/new Vector2(Screen.width,Screen.height);safe.anchorMax=lastSafeArea.max/new Vector2(Screen.width,Screen.height);safe.offsetMin=safe.offsetMax=Vector2.zero;
            Canvas.ForceUpdateCanvases();
            float bottom=lastSafeArea.yMin+410*canvas.scaleFactor,top=lastSafeArea.yMax-200*canvas.scaleFactor;
            viewCamera.pixelRect=new Rect(lastSafeArea.xMin,bottom,lastSafeArea.width,Mathf.Max(100,top-bottom));
            float vertical=viewCamera.fieldOfView*Mathf.Deg2Rad*.5f;
            float horizontal=Mathf.Atan(Mathf.Tan(vertical)*viewCamera.aspect);
            float distance=FocusRadius/Mathf.Sin(Mathf.Min(vertical,horizontal))*1.12f;
            viewCamera.transform.SetPositionAndRotation(Workpiece.TransformPoint(FocusCenter)+new Vector3(0,0,-distance),Quaternion.identity);
        }
        void ReadPointer()
        {
            if(ChapterMenuOpen)return;
            Vector2 position=Vector2.zero;bool down=false,pressed=false,up=false;
#if ENABLE_INPUT_SYSTEM
            if(Touchscreen.current!=null)
            {
                var touches=Touchscreen.current.touches.Where(t=>t.press.isPressed).ToArray();
                if(touches.Length>=2)
                {
                    Vector2 center=(touches[0].position.ReadValue()+touches[1].position.ReadValue())*.5f;
                    if(!multiTouch)panAllowed=touches.Length==2 && touches.All(t=>viewCamera.pixelRect.Contains(t.position.ReadValue()) && !PointerOverUI(t.position.ReadValue()));
                    else if(panAllowed && touches.Length==2)PanModel(center-(Vector2)lastPanPointer);
                    if(touches.Length>2)panAllowed=false;
                    multiTouch=true;pointerActive=false;lastPanPointer=center;return;
                }
                if(multiTouch) { if(touches.Length==0)multiTouch=false;pointerActive=false;return; }
            }
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
        RectTransform Box(Transform parent,Color color) { var r=NewRect("Panel",parent);var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=true;return r; }
        WorkshopCard MakeCard(Transform parent,System.Action action)
        {
            var r=Box(parent,Panel);r.name="Mobile card";var card=r.gameObject.AddComponent<WorkshopCard>();card.normal=Panel;card.highlight=new Color(.12f,.25f,.28f);card.clicked=action;return card;
        }
        TextMeshProUGUI Label(Transform parent,string text,float size,Color color,Vector2 position,Vector2 dimensions,bool bold=false)
        {
            var r=NewRect("Label",parent);r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=position;r.sizeDelta=dimensions;
            var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=bold?heading:font;t.fontSize=size;t.color=color;t.text=text;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Ellipsis;return t;
        }
        void CenterLabel(Transform parent,string text,float size) { var t=Label(parent,text,size,TextColor,Vector2.zero,Vector2.one,true);Stretch(t.rectTransform);t.margin=new Vector4(12,4,12,4);t.alignment=TextAlignmentOptions.Center; }
    }
}

