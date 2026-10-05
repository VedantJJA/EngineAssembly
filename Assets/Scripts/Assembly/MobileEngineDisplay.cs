using System;
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
    public sealed class MobileEngineDisplay : MonoBehaviour
    {
        [SerializeField] Transform engineRoot;
        [SerializeField] Camera viewCamera;
        [SerializeField] TextAsset recipeAsset;
        sealed class Visual { public PartDefinition definition;public Transform transform;public MeshRenderer renderer;public MeshCollider collider; }
        readonly Dictionary<string,Visual> visuals=new Dictionary<string,Visual>();
        readonly Dictionary<string,Transform> groups=new Dictionary<string,Transform>();
        readonly Dictionary<string,int> indices=new Dictionary<string,int>();
        List<PartDefinition> timeline;
        AssemblyRecipe recipe;
        RectTransform safe,footer,info;
        Canvas canvas;
        TextMeshProUGUI progress,title,description,partMeta,playLabel;
        AssemblyTimelineScrubber scrubber;
        TMP_FontAsset heading,body;
        Quaternion initialRotation;
        Vector3 focus,pan;
        float radius,zoom=1,position;
        bool playing,ready,pointerActive,dragging,multiTouch,panAllowed;
        Vector2 lastPointer,startPointer,multiCenter;
        float lastSpan;
        Vector2Int lastSize;Rect lastSafe;
        string selectedId;
        public bool Ready=>ready;
        public float TimelinePosition=>position;
        public int StepCount=>timeline?.Count??0;
        public int VisiblePartCount=>visuals.Values.Count(v=>v.renderer.enabled);
        public string SelectedPartId=>selectedId;
        public Camera ViewCamera=>viewCamera;
        public Transform EngineRoot=>engineRoot;
        public AssemblyRecipe Recipe=>recipe;
        static readonly Color Ink=new Color(.035f,.052f,.075f),Panel=new Color(.065f,.105f,.14f),Mint=new Color(.45f,.94f,.74f),Muted=new Color(.61f,.71f,.79f);
        public void Configure(Transform root,Camera camera,TextAsset asset){engineRoot=root;viewCamera=camera;recipeAsset=asset;}
        void Start()
        {
            recipe=AssemblyRecipeStore.Parse(recipeAsset.text);timeline=EngineLearningContent.Timeline(recipe);
            for(int i=0;i<timeline.Count;i++)indices[timeline[i].id]=i;
            foreach(var group in recipe.subAssemblies)
            {
                var t=new GameObject(group.rootPartId+" display fixture").transform;t.SetParent(engineRoot,false);groups[group.id]=t;
            }
            foreach(var filter in engineRoot.GetComponentsInChildren<MeshFilter>().ToArray())
            {
                var d=recipe.parts.FirstOrDefault(p=>p.id==filter.name);if(d==null){filter.gameObject.SetActive(false);continue;}
                var renderer=filter.GetComponent<MeshRenderer>();if(!renderer)continue;
                // Preserve CAD scale while changing only the reference frame.
                var scale=filter.transform.lossyScale;filter.transform.SetParent(string.IsNullOrEmpty(d.subAssemblyId)?engineRoot:groups[d.subAssemblyId],true);
                filter.transform.localScale=new Vector3(scale.x/engineRoot.lossyScale.x,scale.y/engineRoot.lossyScale.y,scale.z/engineRoot.lossyScale.z);
                filter.transform.SetLocalPositionAndRotation(d.targetPosition,d.targetRotation);
                var collider=filter.GetComponent<MeshCollider>();if(!collider)collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;
                visuals[d.id]=new Visual{definition=d,transform=filter.transform,renderer=renderer,collider=collider};
            }
            if(visuals.Count!=recipe.parts.Count-recipe.subAssemblies.Count)throw new InvalidOperationException("Display recipe does not cover the visual model.");
            initialRotation=engineRoot.rotation;CreateUI();ready=true;SetTimeline(1);
            if(VisiblePartCount>0){var bounds=BoundsOfVisible();focus=engineRoot.InverseTransformPoint(bounds.center);radius=Mathf.Max(.25f,bounds.extents.magnitude);}
            UpdateViewport();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        public float PartProgress(string id)
        {
            if(!indices.TryGetValue(id,out int index))return recipe.parts.Any(p=>p.id==id && p.isBase)?1:0;
            return Mathf.Clamp01(position*timeline.Count-index);
        }
        public void SetTimeline(float value)
        {
            if(!ready)return;position=Mathf.Clamp01(value);scrubber.SetValue(position);
            foreach(var group in recipe.subAssemblies)
            {
                var carrier=recipe.parts.First(p=>p.id==group.rootPartId);float t=Mathf.SmoothStep(0,1,PartProgress(carrier.id));
                var transform=groups[group.id];transform.localPosition=Vector3.Lerp(group.benchPosition,carrier.targetPosition,t);
                transform.localRotation=Quaternion.Slerp(Quaternion.identity,carrier.targetRotation,t);
            }
            foreach(var visual in visuals.Values)
            {
                float t=PartProgress(visual.definition.id);bool visible=t>0;
                visual.renderer.enabled=visible;visual.collider.enabled=visible;
                var approach=visual.definition.orientation==AssemblyOrientation.UpsideDown?Vector3.down:Vector3.up;
                visual.transform.localPosition=visual.definition.targetPosition+approach*(1-Mathf.SmoothStep(0,1,t))*.5f;
            }
            int done=Mathf.FloorToInt(position*timeline.Count+.00001f);int index=Mathf.Clamp(done,0,timeline.Count-1);var current=timeline[index];
            progress.text=done+" / "+timeline.Count+"   ·   "+recipe.chapters.First(c=>c.id==current.chapterId).title;
            if(selectedId!=null && (!visuals.TryGetValue(selectedId,out var selected) || !selected.renderer.enabled))selectedId=null;
            if(selectedId==null)ShowInstruction(current);
            if(VisiblePartCount>0){var bounds=BoundsOfVisible();focus=engineRoot.InverseTransformPoint(bounds.center);radius=Mathf.Max(.25f,bounds.extents.magnitude);}
            UpdatePlaybackLabel();UpdateCamera();Physics.SyncTransforms();
        }
        void ShowInstruction(PartDefinition current)
        {
            title.text=position>=1?"Complete engine":"ASSEMBLY SEQUENCE";
            description.text=position>=1?"Tap a visible component to learn what it does. Drag the timeline backwards to take the engine apart.":EngineLearningContent.ForPart(current).caption;
            partMeta.text=position>=1?(recipe.parts.Count-recipe.subAssemblies.Count)+" physical components · "+recipe.subAssemblies.Count+" completed subassemblies":current.displayName;
        }
        public bool SelectPart(string id)
        {
            if(!ready || !visuals.TryGetValue(id,out var visual) || !visual.renderer.enabled)return false;
            selectedId=id;playing=false;UpdatePlaybackLabel();var detail=EngineLearningContent.Detail(visual.definition);
            title.text=detail.title;description.text=detail.description;
            partMeta.text=recipe.chapters.First(c=>c.id==visual.definition.chapterId).title+"   ·   Assembly step "+visual.definition.order.ToString("000");return true;
        }
        public bool TapPart(Vector2 point)
        {
            if(!ready || !viewCamera.pixelRect.Contains(point) || OverUI(point))return false;
            if(Physics.Raycast(viewCamera.ScreenPointToRay(point),out var hit,100,~0,QueryTriggerInteraction.Ignore))
            {
                var selected=visuals.FirstOrDefault(pair=>pair.Value.collider==hit.collider);if(selected.Key!=null)return SelectPart(selected.Key);
            }
            selectedId=null;SetTimeline(position);return false;
        }
        public void RotateModel(Vector2 delta)
        {
            if(!ready)return;Vector3 center=engineRoot.TransformPoint(focus);
            engineRoot.RotateAround(center,viewCamera.transform.up,-delta.x*.25f);engineRoot.RotateAround(center,viewCamera.transform.right,delta.y*.25f);
        }
        public void PanModel(Vector2 delta)
        {
            if(!ready)return;float distance=Vector3.Distance(viewCamera.transform.position,engineRoot.TransformPoint(focus));
            float metres=2*distance*Mathf.Tan(viewCamera.fieldOfView*Mathf.Deg2Rad*.5f)/Mathf.Max(1,viewCamera.pixelHeight);
            var shift=(viewCamera.transform.right*delta.x+viewCamera.transform.up*delta.y)*metres;
            if((pan+shift).magnitude>radius*3)return;pan+=shift;engineRoot.position+=shift;
        }
        public void ZoomModel(float ratio){if(!ready)return;zoom=Mathf.Clamp(zoom*ratio,.45f,5);UpdateCamera();}
        public void ResetView(){engineRoot.SetPositionAndRotation(Vector3.zero,initialRotation);pan=Vector3.zero;zoom=1;UpdateCamera();}
        public void TogglePlayback(){playing=!playing;if(playing && position>=1)SetTimeline(0);UpdatePlaybackLabel();}
        void UpdatePlaybackLabel(){if(playLabel)playLabel.text=playing?"PAUSE":"PLAY TIMELAPSE";}
        void Update()
        {
            if(!ready)return;
            if(lastSize!=new Vector2Int(Screen.width,Screen.height) || lastSafe!=Screen.safeArea)UpdateViewport();
            if(playing)SetTimeline(position+Time.unscaledDeltaTime/90);if(position>=1 && playing){playing=false;UpdatePlaybackLabel();}
            ReadPointer();if(InputHelper.IsKeyDown(KeyCode.Escape))ExperienceMenu.GoHome();
        }
        Bounds BoundsOfVisible(){var rs=visuals.Values.Where(v=>v.renderer.enabled).Select(v=>v.renderer).ToArray();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
        void UpdateViewport()
        {
            lastSize=new Vector2Int(Screen.width,Screen.height);lastSafe=Screen.safeArea;
            canvas.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution=Screen.width>Screen.height?new Vector2(1920,1080):new Vector2(1080,1920);
            safe.anchorMin=lastSafe.min/new Vector2(Screen.width,Screen.height);safe.anchorMax=lastSafe.max/new Vector2(Screen.width,Screen.height);safe.offsetMin=safe.offsetMax=Vector2.zero;Canvas.ForceUpdateCanvases();
            float bottom=lastSafe.yMin+370*canvas.scaleFactor,top=lastSafe.yMax-150*canvas.scaleFactor;
            viewCamera.pixelRect=new Rect(lastSafe.xMin,bottom,lastSafe.width,Mathf.Max(100,top-bottom));UpdateCamera();
        }
        void UpdateCamera()
        {
            float vertical=viewCamera.fieldOfView*Mathf.Deg2Rad*.5f,horizontal=Mathf.Atan(Mathf.Tan(vertical)*viewCamera.aspect);
            float distance=radius/Mathf.Sin(Mathf.Min(vertical,horizontal))*1.18f/zoom;
            viewCamera.transform.SetPositionAndRotation(engineRoot.TransformPoint(focus)-pan+Vector3.back*distance,Quaternion.identity);
        }
        void ReadPointer()
        {
            Vector2 point=default;bool down=false,pressed=false,up=false;
#if ENABLE_INPUT_SYSTEM
            if(Touchscreen.current!=null)
            {
                var touches=Touchscreen.current.touches.Where(t=>t.press.isPressed).ToArray();
                if(touches.Length>=2)
                {
                    Vector2 center=(touches[0].position.ReadValue()+touches[1].position.ReadValue())*.5f;float span=Vector2.Distance(touches[0].position.ReadValue(),touches[1].position.ReadValue());
                    if(!multiTouch)panAllowed=touches.Length==2 && touches.All(t=>viewCamera.pixelRect.Contains(t.position.ReadValue()) && !OverUI(t.position.ReadValue()));
                    else if(panAllowed && touches.Length==2){PanModel(center-multiCenter);if(lastSpan>10)ZoomModel(span/lastSpan);}
                    if(touches.Length>2)panAllowed=false;multiCenter=center;lastSpan=span;multiTouch=true;pointerActive=false;return;
                }
                if(multiTouch){if(touches.Length==0)multiTouch=false;pointerActive=false;return;}
                var touch=Touchscreen.current.primaryTouch;var phase=touch.phase.ReadValue();
                if(phase!=UnityEngine.InputSystem.TouchPhase.None){point=touch.position.ReadValue();down=phase==UnityEngine.InputSystem.TouchPhase.Began;pressed=touch.press.isPressed;up=phase==UnityEngine.InputSystem.TouchPhase.Ended;if(phase==UnityEngine.InputSystem.TouchPhase.Canceled){pointerActive=false;return;}}
                else if(Mouse.current!=null){point=Mouse.current.position.ReadValue();down=Mouse.current.leftButton.wasPressedThisFrame;pressed=Mouse.current.leftButton.isPressed;up=Mouse.current.leftButton.wasReleasedThisFrame;}
            }
            else if(Mouse.current!=null){point=Mouse.current.position.ReadValue();down=Mouse.current.leftButton.wasPressedThisFrame;pressed=Mouse.current.leftButton.isPressed;up=Mouse.current.leftButton.wasReleasedThisFrame;}
            if(Mouse.current!=null && viewCamera.pixelRect.Contains(point) && !OverUI(point))
            {
                ZoomModel(Mathf.Exp(Mouse.current.scroll.ReadValue().y/120*.12f));
                if(Mouse.current.middleButton.isPressed){PanModel(Mouse.current.delta.ReadValue());pointerActive=false;return;}
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            point=Input.mousePosition;down=Input.GetMouseButtonDown(0);pressed=Input.GetMouseButton(0);up=Input.GetMouseButtonUp(0);
#endif
            if(down){pointerActive=viewCamera.pixelRect.Contains(point) && !OverUI(point);startPointer=lastPointer=point;dragging=false;}
            if(!pointerActive)return;
            if(pressed){if(Vector2.Distance(point,startPointer)>12*canvas.scaleFactor)dragging=true;if(dragging)RotateModel((point-lastPointer)/canvas.scaleFactor);lastPointer=point;}
            if(up){if(!dragging)TapPart(point);pointerActive=false;}
        }
        bool OverUI(Vector2 point){if(!EventSystem.current)return false;var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);return hits.Any(h=>h.module is UnityEngine.UI.GraphicRaycaster);}
        void CreateUI()
        {
            heading=Resources.Load<TMP_FontAsset>("WorkshopHeading");body=Resources.Load<TMP_FontAsset>("WorkshopBody");
            var root=Rect("Engine display canvas",transform);canvas=root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scaler=root.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;root.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            if(!FindAnyObjectByType<EventSystem>()){
                var events=new GameObject("Display event system",typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                events.AddComponent<InputSystemUIInputModule>();
#else
                events.AddComponent<StandaloneInputModule>();
#endif
            }
            safe=Rect("Safe area",root);Anchor(safe,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var header=Box("Display header",safe,Panel);Anchor(header,Vector2.up,Vector2.one,new Vector2(0,-140),Vector2.zero);
            Label(header,"ENGINE / LAB  ·  DISPLAY",30,true,new Vector2(24,60),new Vector2(-250,-20));
            var gestureHelp=Label(header,"1 finger rotate  ·  2 fingers move  ·  pinch to zoom",19,false,new Vector2(24,18),new Vector2(-24,55));
            gestureHelp.rectTransform.anchorMax=new Vector2(1,0);gestureHelp.rectTransform.offsetMin=new Vector2(24,18);gestureHelp.rectTransform.offsetMax=new Vector2(-24,55);
            Card(header,"MAIN MENU",ExperienceMenu.GoHome,Vector2.one,Vector2.one,new Vector2(-224,-70),new Vector2(-24,-18));
            footer=Box("Timeline dock",safe,Ink);Anchor(footer,Vector2.zero,Vector2.right,Vector2.zero,new Vector2(0,360));
            info=Box("Part information card",footer,Panel);Anchor(info,Vector2.zero,Vector2.one,new Vector2(20,140),new Vector2(-20,-12));
            title=Label(info,"",27,true,new Vector2(20,140),new Vector2(-80,-14));
            description=Label(info,"",22,false,new Vector2(20,44),new Vector2(-20,-60));description.enableAutoSizing=true;description.fontSizeMin=18;description.fontSizeMax=22;
            partMeta=Label(info,"",15,false,new Vector2(20,8),new Vector2(-20,39));partMeta.rectTransform.anchorMax=new Vector2(1,0);partMeta.rectTransform.offsetMin=new Vector2(20,8);partMeta.rectTransform.offsetMax=new Vector2(-20,39);partMeta.color=Muted;partMeta.textWrappingMode=TextWrappingModes.NoWrap;partMeta.overflowMode=TextOverflowModes.Ellipsis;
            Card(info,"×",()=>{selectedId=null;SetTimeline(position);},Vector2.one,Vector2.one,new Vector2(-64,-53),new Vector2(-12,-8));
            progress=Label(footer,"",18,false,new Vector2(24,94),new Vector2(-24,130));progress.rectTransform.anchorMax=new Vector2(1,0);progress.rectTransform.offsetMin=new Vector2(24,94);progress.rectTransform.offsetMax=new Vector2(-24,130);progress.color=Mint;
            var rail=Box("Assembly timeline slider",footer,new Color(.12f,.19f,.24f));Anchor(rail,Vector2.zero,Vector2.right,new Vector2(32,62),new Vector2(-32,86));
            var fill=Box("Timeline fill",rail,Mint);Anchor(fill,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);fill.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            var thumb=Box("Timeline handle",rail,Color.white);Anchor(thumb,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(-15,-21),new Vector2(15,21));thumb.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            // Expand only the rail's hit area using transparent graphic padding.
            rail.GetComponent<UnityEngine.UI.Image>().raycastPadding=new Vector4(-8,-14,-8,-14);
            scrubber=rail.gameObject.AddComponent<AssemblyTimelineScrubber>();scrubber.Configure(fill,thumb);scrubber.changed=value=>{playing=false;SetTimeline(value);};
            Card(footer,"START",()=>{playing=false;SetTimeline(0);},Vector2.zero,Vector2.zero,new Vector2(24,8),new Vector2(164,48));
            var play=Card(footer,"PLAY TIMELAPSE",TogglePlayback,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-160,8),new Vector2(160,48));playLabel=play.GetComponentInChildren<TextMeshProUGUI>();
            Card(footer,"COMPLETE",()=>{playing=false;SetTimeline(1);},Vector2.right,Vector2.right,new Vector2(-194,8),new Vector2(-24,48));
            Card(safe,"RESET VIEW",ResetView,Vector2.one,Vector2.one,new Vector2(-208,-206),new Vector2(-24,-158));
        }
        RectTransform Card(Transform parent,string text,Action action,Vector2 min,Vector2 max,Vector2 low,Vector2 high)
        {
            var rect=Box(text+" card",parent,Panel);Anchor(rect,min,max,low,high);var card=rect.gameObject.AddComponent<WorkshopCard>();card.normal=Panel;card.highlight=new Color(.1f,.24f,.25f);card.clicked=action;
            var label=Label(rect,text,17,true,new Vector2(4,2),new Vector2(-4,-2));label.alignment=TextAlignmentOptions.Center;return rect;
        }
        TextMeshProUGUI Label(Transform parent,string text,float size,bool bold,Vector2 low,Vector2 high)
        {
            var rect=Rect("Label",parent);Anchor(rect,Vector2.zero,Vector2.one,low,high);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();label.font=bold?heading:body;label.text=text;label.fontSize=size;label.color=Color.white;label.raycastTarget=false;label.textWrappingMode=TextWrappingModes.Normal;return label;
        }
        static RectTransform Rect(string name,Transform parent){var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);return rect;}
        static RectTransform Box(string name,Transform parent,Color color){var rect=Rect(name,parent);rect.gameObject.AddComponent<UnityEngine.UI.Image>().color=color;return rect;}
        static void Anchor(RectTransform rect,Vector2 min,Vector2 max,Vector2 low,Vector2 high){rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=low;rect.offsetMax=high;}
    }
}
