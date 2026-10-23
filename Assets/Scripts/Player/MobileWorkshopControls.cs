using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EngineAssembly
{
    // Touch layer for the full workshop. World taps use the touched pixel, never
    // the desktop crosshair, while UI pointers are reserved for the touch HUD.
    [DefaultExecutionOrder(-50)]
    public sealed class MobileWorkshopControls : MonoBehaviour
    {
        public Vector2 Movement { get; private set; }
        public bool CrouchHeld { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool JumpQueued { get; private set; }
        public bool HasTouchHud=>canvas;
        public bool HasLandscapeLayout=>canvas && scaler && scaler.referenceResolution.x>scaler.referenceResolution.y;
        Canvas canvas;
        UnityEngine.UI.CanvasScaler scaler;
        RectTransform stickArea,stickKnob,crouchArea,sprintArea,jumpArea,dropArea,menuArea;
        Sprite disc;
        int lookId=-1,leverId=-1;
        Vector2 lookStart,lookLast,lookDelta,leverStart,tapPosition;
        bool lookDragged,tapQueued;
        LiftControlHandle heldLever;
        AssemblyWorkshopUI workshopUI;

        void Awake()
        {
            var root=new GameObject("Mobile workshop touch HUD",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(transform,false);
            canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=110;
            scaler=root.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1440,900);scaler.matchWidthOrHeight=.5f;
            disc=MakeDisc();
            stickArea=Control(root.transform,"Move joystick",new Vector2(0,0),new Vector2(195,155),176,new Color(.12f,.23f,.27f,.62f),"",TouchControl.Kind.Stick);
            stickKnob=Disc(stickArea,"Joystick grip",Vector2.zero,78,new Color(.1f,.68f,.66f,.95f));stickKnob.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            crouchArea=Control(root.transform,"Crouch",new Vector2(1,0),new Vector2(-304,122),98,new Color(.11f,.43f,.47f,.88f),"CROUCH",TouchControl.Kind.Crouch);
            sprintArea=Control(root.transform,"Sprint",new Vector2(1,0),new Vector2(-170,190),106,new Color(.11f,.43f,.47f,.88f),"SPRINT",TouchControl.Kind.Sprint);
            jumpArea=Control(root.transform,"Jump",new Vector2(1,0),new Vector2(-92,82),96,new Color(.11f,.43f,.47f,.88f),"JUMP",TouchControl.Kind.Jump);
            dropArea=Control(root.transform,"Drop held item",new Vector2(1,0),new Vector2(-425,85),84,new Color(.34f,.4f,.44f,.85f),"DROP",TouchControl.Kind.Drop);
            menuArea=Control(root.transform,"Pause",new Vector2(1,1),new Vector2(-70,-68),76,new Color(.11f,.43f,.47f,.88f),"Ⅱ",TouchControl.Kind.Menu);
        }
        void Start()
        {
            workshopUI=FindAnyObjectByType<AssemblyWorkshopUI>();
            // Directly tapping a visible ghost is the phone's placement gesture.
            AssemblyManager.Instance?.SetDifficulty(SnapDifficulty.Easy,false);
        }
        void Update()
        {
            if(!workshopUI)workshopUI=FindAnyObjectByType<AssemblyWorkshopUI>();
            bool playing=workshopUI && !workshopUI.IsOpen;
            if(canvas.enabled!=playing)canvas.enabled=playing;
            if(!playing){ReleaseLever();lookId=-1;lookDelta=Vector2.zero;Movement=Vector2.zero;CrouchHeld=SprintHeld=false;tapQueued=false;return;}
#if ENABLE_INPUT_SYSTEM
            if(Touchscreen.current==null)return;
            foreach(var touch in Touchscreen.current.touches)
            {
                int id=touch.touchId.ReadValue();var point=touch.position.ReadValue();
                if(touch.press.wasPressedThisFrame)
                {
                    if(IsControl(point) || IsWorkshopCard(point))continue;
                    var camera=GetComponent<PlayerAssemblyController>()?.ViewCamera;
                    var control=camera?LiftControlHandle.RayControl(camera.ScreenPointToRay(point),5):null;
                    if(control && control.Begin())
                    {
                        if(control.Kind==LiftControlHandle.ControlKind.Lever){heldLever=control;leverId=id;leverStart=point;}
                        continue;
                    }
                    if(lookId<0){lookId=id;lookStart=lookLast=point;lookDragged=false;}
                }
                if(id==leverId)
                {
                    if(touch.press.isPressed && heldLever)heldLever.SetValue(Mathf.Clamp((point.y-leverStart.y)/85f,-1,1));
                    if(touch.press.wasReleasedThisFrame)ReleaseLever();
                    continue;
                }
                if(id!=lookId)continue;
                if(touch.press.isPressed)
                {
                    var delta=point-lookLast;lookLast=point;
                    if((point-lookStart).sqrMagnitude>20*20)lookDragged=true;
                    if(lookDragged)lookDelta+=delta;
                }
                if(touch.press.wasReleasedThisFrame)
                {
                    if(!lookDragged){tapPosition=point;tapQueued=true;}
                    lookId=-1;
                }
            }
#endif
        }
        bool IsControl(Vector2 point)=>In(stickArea,point)||In(crouchArea,point)||In(sprintArea,point)||In(jumpArea,point)||In(dropArea,point)||In(menuArea,point);
        static bool In(RectTransform rect,Vector2 point)=>rect && RectTransformUtility.RectangleContainsScreenPoint(rect,point);
        static bool IsWorkshopCard(Vector2 point)
        {
            if(!EventSystem.current)return false;
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
            foreach(var hit in hits)if(hit.gameObject.GetComponentInParent<WorkshopCard>())return true;
            return false;
        }
        public Vector2 ConsumeLook(){var result=lookDelta;lookDelta=Vector2.zero;return result;}
        public bool ConsumeTap(out Vector2 position){position=tapPosition;bool result=tapQueued;tapQueued=false;return result;}
        public bool ConsumeJump(){bool result=JumpQueued;JumpQueued=false;return result;}
        void ReleaseLever(){if(heldLever)heldLever.Release();heldLever=null;leverId=-1;}
        public void SetStick(Vector2 point)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(stickArea,point,null,out var local);
            Movement=Vector2.ClampMagnitude(local/(stickArea.rect.width*.43f),1);
            stickKnob.anchoredPosition=Movement*48;
        }
        public void ReleaseStick(){Movement=Vector2.zero;if(stickKnob)stickKnob.anchoredPosition=Vector2.zero;}
        public void SetButton(TouchControl.Kind kind,bool pressed)
        {
            if(kind==TouchControl.Kind.Crouch)CrouchHeld=pressed;
            else if(kind==TouchControl.Kind.Sprint)SprintHeld=pressed;
            else if(kind==TouchControl.Kind.Jump && pressed)JumpQueued=true;
            else if(kind==TouchControl.Kind.Drop && pressed)GetComponent<PlayerAssemblyController>()?.DropHeldPart(false);
            else if(kind==TouchControl.Kind.Menu && pressed)workshopUI?.Open(AssemblyWorkshopUI.Page.Pause);
        }
        RectTransform Control(Transform parent,string name,Vector2 anchor,Vector2 offset,float diameter,Color color,string label,TouchControl.Kind kind)
        {
            var rect=Disc(parent,name,offset,diameter,color);rect.anchorMin=rect.anchorMax=anchor;
            var control=rect.gameObject.AddComponent<TouchControl>();control.Configure(this,kind);
            if(label.Length>0)
            {
                var text=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();text.transform.SetParent(rect,false);
                var r=text.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
                text.font=Resources.Load<TMP_FontAsset>("WorkshopHeading");text.fontSize=kind==TouchControl.Kind.Menu?42:16;text.alignment=TextAlignmentOptions.Center;text.color=Color.white;text.raycastTarget=false;text.text=label;
            }
            return rect;
        }
        RectTransform Disc(Transform parent,string name,Vector2 offset,float diameter,Color color)
        {
            var rect=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=offset;rect.sizeDelta=Vector2.one*diameter;
            var image=rect.GetComponent<UnityEngine.UI.Image>();image.sprite=disc;image.color=color;return rect;
        }
        static Sprite MakeDisc()
        {
            const int size=64;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Mobile touch circle",filterMode=FilterMode.Bilinear};
            var pixels=new Color[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++)pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01((31.5f-Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f)))*2));
            texture.SetPixels(pixels);texture.Apply(false,true);return Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f));
        }
        void OnDestroy(){ReleaseLever();if(disc){var texture=disc.texture;Destroy(disc);if(texture)Destroy(texture);}}
    }

    public sealed class TouchControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public enum Kind { Stick, Crouch, Sprint, Jump, Drop, Menu }
        MobileWorkshopControls owner;Kind kind;int pointer=-1;
        public void Configure(MobileWorkshopControls controls,Kind action){owner=controls;kind=action;}
        public void OnPointerDown(PointerEventData eventData){pointer=eventData.pointerId;if(kind==Kind.Stick)owner.SetStick(eventData.position);else owner.SetButton(kind,true);}
        public void OnDrag(PointerEventData eventData){if(pointer==eventData.pointerId && kind==Kind.Stick)owner.SetStick(eventData.position);}
        public void OnPointerUp(PointerEventData eventData){if(pointer!=eventData.pointerId)return;if(kind==Kind.Stick)owner.ReleaseStick();else owner.SetButton(kind,false);pointer=-1;}
        void OnDisable(){if(pointer<0 || !owner)return;if(kind==Kind.Stick)owner.ReleaseStick();else owner.SetButton(kind,false);pointer=-1;}
    }
}
