using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EngineAssembly
{
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterController))]
    public class PlayerAssemblyController : MonoBehaviour
    {
        [SerializeField] Camera playerCamera;
        [SerializeField] float walkSpeed=4.5f, sprintSpeed=7, gravity=-18, mouseSensitivity=.12f;
        [SerializeField] float pickupDistance=5, minHoldDistance=.3f,maxHoldDistance=5;
        [SerializeField] LayerMask pickupLayerMask=~0;
        AssemblyPart heldPart;
        PickupablePlatform heldPlatform;
        AssemblyVisual hover;
        AssemblyPart hoveredPart;
        bool hoveredAllowed;
        AssemblyWorkshopUI workshopUI;
        CharacterController controller;
        float pitch,fallSpeed,holdDistance=1.5f;
        Vector3 contactLocal;
        public AssemblyPart CurrentHeldPart=>heldPart;
        public bool IsHoldingPart=>heldPart;
        public bool IsInEditMode=>AssemblyManager.Instance && AssemblyManager.Instance.Mode==WorkshopMode.Edit;
        public bool IsEditModeActive=>IsInEditMode;
        public Camera ViewCamera=>playerCamera;
        Transform Held=>heldPart?heldPart.transform:heldPlatform?heldPlatform.transform:null;
        void Awake()
        {
            controller=GetComponent<CharacterController>();if(!playerCamera)playerCamera=GetComponentInChildren<Camera>();
            if(!playerCamera)playerCamera=Camera.main;
            hover=gameObject.AddComponent<AssemblyVisual>();
        }
        void Start()
        {
            if(!AssemblyManager.Instance && !FindAnyObjectByType<AssemblyManager>()) new GameObject("Assembly Workshop").AddComponent<AssemblyManager>();
        }
        void Update()
        {
            if(!playerCamera)return;
            if(heldPart && !heldPart.IsSelected)heldPart=null;
            if(!workshopUI)workshopUI=FindAnyObjectByType<AssemblyWorkshopUI>();
            var ui=workshopUI;
            if(InputHelper.IsKeyDown(KeyCode.Tab) && ui)ui.Toggle();
            bool menu=ui && ui.IsOpen;
            if(InputHelper.IsKeyDown(KeyCode.Escape) && ui) { ui.SetOpen(true);menu=true; }
            Cursor.lockState=menu?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=menu;
            if(menu) { ClearHover();return; }
            Vector2 delta=MouseDelta();
            bool rotating=Held && MouseHeld(1);
            if(!rotating)
            {
                transform.Rotate(Vector3.up,delta.x*mouseSensitivity);
                pitch=Mathf.Clamp(pitch-delta.y*mouseSensitivity,-85,85);playerCamera.transform.localRotation=Quaternion.Euler(pitch,0,0);
            }
            float x=(InputHelper.IsKeyHeld(KeyCode.D)?1:0)-(InputHelper.IsKeyHeld(KeyCode.A)?1:0);
            float z=(InputHelper.IsKeyHeld(KeyCode.W)?1:0)-(InputHelper.IsKeyHeld(KeyCode.S)?1:0);
            if(controller.isGrounded && fallSpeed<0)fallSpeed=-2;fallSpeed+=gravity*Time.deltaTime;
            Vector3 direction=Vector3.ClampMagnitude(transform.right*x+transform.forward*z,1)*(InputHelper.IsKeyHeld(KeyCode.LeftShift)?sprintSpeed:walkSpeed);
            if(controller.enabled)controller.Move((direction+Vector3.up*fallSpeed)*Time.deltaTime);
            if(InputHelper.IsKeyDown(KeyCode.F))
            {
                if(heldPlatform)heldPlatform.Flip();
                else if(!heldPart && AssemblyManager.Instance)AssemblyManager.Instance.AssemblyRoot.GetComponent<PickupablePlatform>().Flip();
            }
            if(Held)
            {
                if(InputHelper.IsKeyDown(KeyCode.G)) { DropHeldPart();return; }
                if(heldPlatform && heldPlatform.IsFlipping)return;
                holdDistance=Mathf.Clamp(holdDistance+MouseScroll()*.15f,minHoldDistance,maxHoldDistance);
                Quaternion rotation=Held.rotation;
                if(rotating)rotation=Quaternion.AngleAxis(-delta.x*.5f,playerCamera.transform.up)*Quaternion.AngleAxis(delta.y*.5f,playerCamera.transform.right)*rotation;
                Vector3 position=playerCamera.transform.position+playerCamera.transform.forward*holdDistance-rotation*Vector3.Scale(contactLocal,Held.lossyScale);
                if(heldPart)heldPart.MoveHeld(position,rotation);else heldPlatform.MoveHeld(position,rotation);
                if(MouseDown(0)||InputHelper.IsKeyDown(KeyCode.E))DropHeldPart();
                return;
            }
            var ray=new Ray(playerCamera.transform.position,playerCamera.transform.forward);
            if(Physics.Raycast(ray,out var hit,pickupDistance,pickupLayerMask,QueryTriggerInteraction.Ignore))
            {
                var part=hit.collider.GetComponentInParent<AssemblyPart>();
                var platform=hit.collider.GetComponentInParent<PickupablePlatform>();
                if(part)
                {
                    bool allowed=!part.Manager||part.Manager.CanPickUp(part,out _);
                    if(hoveredPart!=part || hoveredAllowed!=allowed) { hover.Hover(part,allowed);hoveredPart=part;hoveredAllowed=allowed; }
                    if(MouseDown(0)||InputHelper.IsKeyDown(KeyCode.E))
                    {
                        if(part.Definition!=null && part.Definition.isBase && part.Manager) GrabPlatform(part.Manager.AssemblyRoot.GetComponent<PickupablePlatform>(),hit.point);
                        else if(part.BeginGrab()) { heldPart=part;CaptureContact(hit.point);ClearHover(); }
                    }
                }
                else { ClearHover();if(platform && (MouseDown(0)||InputHelper.IsKeyDown(KeyCode.E)))GrabPlatform(platform,hit.point); }
            }
            else ClearHover();
        }
        void ClearHover() { if(hoveredPart && hover)hover.Clear();hoveredPart=null; }
        void CaptureContact(Vector3 point) { holdDistance=Mathf.Clamp(Vector3.Distance(playerCamera.transform.position,point),minHoldDistance,maxHoldDistance);contactLocal=Held.InverseTransformPoint(point); }
        void GrabPlatform(PickupablePlatform platform,Vector3 point) { if(platform && platform.BeginGrab()) { heldPlatform=platform;CaptureContact(point);ClearHover(); } }
        public void PickUp(AssemblyPart part) { if(Held || !part || !part.BeginGrab())return;heldPart=part;CaptureContact(part.transform.position); }
        public void PickUpWorkpiece()
        {
            if(!AssemblyManager.Instance || !playerCamera)return;
            DropHeldPart();var root=AssemblyManager.Instance.AssemblyRoot;
            GrabPlatform(root.GetComponent<PickupablePlatform>(),root.position);
        }
        public void DropHeldPart()
        {
            if(heldPart)heldPart.Release();if(heldPlatform)heldPlatform.Release();heldPart=null;heldPlatform=null;
        }
        void OnDisable() { DropHeldPart();Cursor.lockState=CursorLockMode.None;Cursor.visible=true; }
        void OnGUI()
        {
            if(Cursor.lockState!=CursorLockMode.Locked)return;
            GUI.Label(new Rect(Screen.width/2-5,Screen.height/2-10,20,20),"+");
            GUI.Box(new Rect(16,Screen.height-75,Mathf.Min(680,Screen.width-32),60),heldPart?heldPart.PartDisplayName+"\n"+heldPart.RejectionReason:"E / Click: pick up or release   RMB: rotate   Wheel: distance   F: flip base   Tab: workshop");
        }
        static Vector2 MouseDelta()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current!=null?Mouse.current.delta.ReadValue():Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxis("Mouse X"),Input.GetAxis("Mouse Y"))*10;
#else
            return Vector2.zero;
#endif
        }
        static bool MouseDown(int button)
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current!=null && (button==0?Mouse.current.leftButton.wasPressedThisFrame:Mouse.current.rightButton.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(button);
#else
            return false;
#endif
        }
        static bool MouseHeld(int button)
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current!=null && (button==0?Mouse.current.leftButton.isPressed:Mouse.current.rightButton.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(button);
#else
            return false;
#endif
        }
        static float MouseScroll()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current!=null?Mouse.current.scroll.ReadValue().y/120:0;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#else
            return 0;
#endif
        }
    }
}

