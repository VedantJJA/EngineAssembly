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
        [SerializeField] float jumpHeight=1.2f,crouchHeight=1.05f,crouchSpeed=2.2f,zoomFieldOfView=35;
        [SerializeField] LayerMask pickupLayerMask=~0;
        AssemblyPart heldPart;
        PickupablePlatform heldPlatform;
        AssemblyVisual hover;
        AssemblyPart hoveredPart;
        bool hoveredAllowed;
        AssemblyWorkshopUI workshopUI;
        CharacterController controller;
        AssemblyPickupQuery pickupQuery;
        float pitch,fallSpeed,holdDistance=1.5f;
        Vector3 contactLocal;
        Quaternion heldViewRotation;
        float standingHeight,standingEye,normalFov;
        bool crouched,platformWasFlipping;
        public AssemblyPart CurrentHeldPart=>heldPart;
        public bool IsHoldingPart=>heldPart;
        public bool IsInEditMode=>AssemblyManager.Instance && AssemblyManager.Instance.Mode==WorkshopMode.Edit;
        public bool IsEditModeActive=>IsInEditMode;
        public Camera ViewCamera=>playerCamera;
        public string HoverMessage { get; private set; }="";
        Transform Held=>heldPart?heldPart.transform:heldPlatform?heldPlatform.transform:null;
        void Awake()
        {
            controller=GetComponent<CharacterController>();if(!playerCamera)playerCamera=GetComponentInChildren<Camera>();
            if(!playerCamera)playerCamera=Camera.main;
            standingHeight=controller.height;standingEye=playerCamera?playerCamera.transform.localPosition.y:1.65f;normalFov=playerCamera?playerCamera.fieldOfView:60;
            hover=gameObject.AddComponent<AssemblyVisual>();
            pickupQuery=gameObject.AddComponent<AssemblyPickupQuery>();
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
            bool menu=ui && ui.IsOpen;
            Cursor.lockState=menu?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=menu;
            if(menu) { ClearHover();return; }
            Vector2 delta=MouseDelta();
            bool rotating=Held && MouseHeld(1);
            playerCamera.fieldOfView=Mathf.Lerp(playerCamera.fieldOfView,!Held && MouseHeld(1)?zoomFieldOfView:normalFov,12*Time.deltaTime);
            if(!rotating)
            {
                transform.Rotate(Vector3.up,delta.x*mouseSensitivity);
                pitch=Mathf.Clamp(pitch-delta.y*mouseSensitivity,-85,85);playerCamera.transform.localRotation=Quaternion.Euler(pitch,0,0);
            }
            float x=(InputHelper.IsKeyHeld(KeyCode.D)?1:0)-(InputHelper.IsKeyHeld(KeyCode.A)?1:0);
            float z=(InputHelper.IsKeyHeld(KeyCode.W)?1:0)-(InputHelper.IsKeyHeld(KeyCode.S)?1:0);
            UpdateCrouch(InputHelper.IsKeyHeld(KeyCode.LeftControl)||InputHelper.IsKeyHeld(KeyCode.C));
            if(controller.isGrounded && fallSpeed<0)fallSpeed=-2;
            if(controller.isGrounded && !crouched && InputHelper.IsKeyDown(KeyCode.Space))fallSpeed=Mathf.Sqrt(jumpHeight*-2*gravity);
            fallSpeed+=gravity*Time.deltaTime;
            Vector3 direction=Vector3.ClampMagnitude(transform.right*x+transform.forward*z,1)*(crouched?crouchSpeed:InputHelper.IsKeyHeld(KeyCode.LeftShift)?sprintSpeed:walkSpeed);
            if(controller.enabled)controller.Move((direction+Vector3.up*fallSpeed)*Time.deltaTime);
            if(InputHelper.IsKeyDown(KeyCode.F))
            {
                if(heldPlatform)heldPlatform.Flip();
                else if(!heldPart && AssemblyManager.Instance)AssemblyManager.Instance.AssemblyRoot.GetComponent<PickupablePlatform>().Flip();
            }
            if(Held)
            {
                if(InputHelper.IsKeyDown(KeyCode.G)) { DropHeldPart();return; }
                if(heldPlatform && heldPlatform.IsFlipping) { heldViewRotation=Quaternion.Inverse(playerCamera.transform.rotation)*Held.rotation;platformWasFlipping=true;return; }
                if(platformWasFlipping) { heldViewRotation=Quaternion.Inverse(playerCamera.transform.rotation)*Held.rotation;platformWasFlipping=false; }
                holdDistance=Mathf.Clamp(holdDistance+MouseScroll()*.15f,minHoldDistance,maxHoldDistance);
                Quaternion rotation=playerCamera.transform.rotation*heldViewRotation;
                if(rotating)rotation=Quaternion.AngleAxis(-delta.x*.5f,playerCamera.transform.up)*Quaternion.AngleAxis(delta.y*.5f,playerCamera.transform.right)*rotation;
                heldViewRotation=Quaternion.Inverse(playerCamera.transform.rotation)*rotation;
                Vector3 position=playerCamera.transform.position+playerCamera.transform.forward*holdDistance-rotation*Vector3.Scale(contactLocal,Held.lossyScale);
                if(heldPart)heldPart.MoveHeld(position,rotation);else heldPlatform.MoveHeld(position,rotation);
                if(heldPart && heldPart.Manager && heldPart.Manager.Difficulty==SnapDifficulty.Easy && heldPart.Manager.Task==AssemblyTask.Assemble && heldPart.Manager.Interaction==WorkshopInteraction.Build && !IsInEditMode)
                {
                    var guide=heldPart.Manager.GetComponent<AssemblyEasyGuide>();var aim=new Ray(playerCamera.transform.position,playerCamera.transform.forward);
                    if(guide) { guide.Aim(aim,pickupDistance);if(MouseDown(0) && guide.Place(aim,pickupDistance))heldPart=null; }
                    if(InputHelper.IsKeyDown(KeyCode.E))DropHeldPart();
                }
                else if(MouseDown(0)||InputHelper.IsKeyDown(KeyCode.E))DropHeldPart();
                return;
            }
            var ray=new Ray(playerCamera.transform.position,playerCamera.transform.forward);
            var manager=AssemblyManager.Instance;
            if(pickupQuery.Raycast(ray,pickupDistance,pickupLayerMask,out var part,out var contact,out var hit))
            {
                if(part && part.Manager)part=part.Manager.InteractionPart(part);
                var platform=hit.collider?hit.collider.GetComponentInParent<PickupablePlatform>():null;
                if(part)
                {
                    string reason="";bool allowed=!part.Manager||part.Manager.CanPickUp(part,out reason);
                    HoverMessage=allowed?part.PartDisplayName:part.PartDisplayName+" — "+reason;
                    if(hoveredPart!=part || hoveredAllowed!=allowed) { hover.Hover(part,allowed);hoveredPart=part;hoveredAllowed=allowed; }
                    if(MouseDown(0)||InputHelper.IsKeyDown(KeyCode.E))
                    {
                        bool move=manager && manager.Interaction==WorkshopInteraction.Move;
                        if(move && part.Manager && (part.Definition.isBase || part.IsSnapped || part.Manager.CarrierDefinition(part.PartId)!=null || part.ChapterId!=manager.CurrentChapterId)) GrabPlatform(part.Manager.MovableFrame(part).GetComponent<PickupablePlatform>(),contact);
                        else if(part.BeginGrab()) { heldPart=part;CaptureContact(contact);ClearHover(); }
                    }
                }
                else { ClearHover();if(platform && manager && manager.Interaction==WorkshopInteraction.Move && (MouseDown(0)||InputHelper.IsKeyDown(KeyCode.E)))GrabPlatform(platform,hit.point); }
            }
            else ClearHover();
        }
        void ClearHover() { if(hoveredPart && hover)hover.Clear();hoveredPart=null;HoverMessage=""; }
        void CaptureContact(Vector3 point) { holdDistance=Mathf.Clamp(Vector3.Distance(playerCamera.transform.position,point),minHoldDistance,maxHoldDistance);contactLocal=Held.InverseTransformPoint(point);heldViewRotation=Quaternion.Inverse(playerCamera.transform.rotation)*Held.rotation; }
        void UpdateCrouch(bool requested)
        {
            if(!requested && crouched)
            {
                var bottom=transform.position+Vector3.up*controller.radius;
                var top=transform.position+Vector3.up*(standingHeight-controller.radius);
                foreach(var collider in Physics.OverlapCapsule(bottom,top,controller.radius*.95f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                    if(collider!=controller && !collider.transform.IsChildOf(transform)) { requested=true;break; }
            }
            crouched=requested;controller.height=crouched?crouchHeight:standingHeight;
            var center=controller.center;center.y=controller.height*.5f;controller.center=center;
            var eye=playerCamera.transform.localPosition;eye.y=Mathf.Lerp(eye.y,crouched?standingEye-(standingHeight-crouchHeight):standingEye,15*Time.deltaTime);playerCamera.transform.localPosition=eye;
        }
        void GrabPlatform(PickupablePlatform platform,Vector3 point) { if(platform && platform.BeginGrab()) { heldPlatform=platform;CaptureContact(point);ClearHover(); } }
        public void PickUp(AssemblyPart part)
        {
            if(part && part.Manager)part=part.Manager.InteractionPart(part);
            if(Held || !part)return;
            if(part.Manager && part.Manager.Interaction==WorkshopInteraction.Move && (part.IsSnapped || part.Definition.isBase || part.Manager.CarrierDefinition(part.PartId)!=null || part.ChapterId!=part.Manager.CurrentChapterId)) { GrabPlatform(part.Manager.MovableFrame(part).GetComponent<PickupablePlatform>(),part.transform.position);return; }
            if(!part.BeginGrab())return;heldPart=part;CaptureContact(part.transform.position);
        }
        public void PickUpWorkpiece()
        {
            if(!AssemblyManager.Instance || !playerCamera)return;
            DropHeldPart();var root=AssemblyManager.Instance.AssemblyRoot;
            GrabPlatform(root.GetComponent<PickupablePlatform>(),root.position);
        }
        public void DropHeldPart()
        {
            if(heldPart)
            {
                var manager=heldPart.Manager;
                if(manager && manager.Mode==WorkshopMode.Edit && manager.Interaction==WorkshopInteraction.Move)
                {
                    heldPart.Release(false);
                    manager.CaptureTray(heldPart);heldPart.Body.isKinematic=true;heldPart.Body.interpolation=RigidbodyInterpolation.None;
                }
                else heldPart.Release();
            }
            if(heldPlatform)heldPlatform.Release();heldPart=null;heldPlatform=null;
        }
        void OnDisable() { DropHeldPart();Cursor.lockState=CursorLockMode.None;Cursor.visible=true; }
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

