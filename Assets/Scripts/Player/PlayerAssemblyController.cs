using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EngineAssembly
{
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterController))]
    public class PlayerAssemblyController : MonoBehaviour
    {
        public enum PlacementTool { Rotate, Move }
        [SerializeField] Camera playerCamera;
        [SerializeField] float walkSpeed=4.5f,sprintSpeed=7,gravity=-18,mouseSensitivity=.12f;
        [SerializeField] float pickupDistance=5,minHoldDistance=.3f,maxHoldDistance=5;
        [SerializeField] float jumpHeight=1.2f,crouchHeight=1.05f,crouchSpeed=2.2f,zoomFieldOfView=35;
        [SerializeField] LayerMask pickupLayerMask=~0;
        [SerializeField] MobilePartArtwork inventoryArtwork;
        public MobilePartArtwork InventoryArtwork=>inventoryArtwork;
        [SerializeField] float voidHeight=-12;
        AssemblyPart heldPart,hoveredPart;
        PickupablePlatform heldPlatform;
        AssemblyInventory inventory;
        AssemblyVisual hover;
        AssemblyWorkshopUI workshopUI;
        CharacterController controller;
        AssemblyPickupQuery pickupQuery;
        HeldPlayerCollisionGuard collisionGuard;
        Vector3 spawnPosition,contactLocal,placementOffset;
        Quaternion spawnRotation,heldViewRotation;
        bool placementMode,crouched,platformWasFlipping,hoveredAllowed;
        float pitch,fallSpeed,holdDistance=1.5f,standingHeight,standingEye,normalFov;
        public AssemblyPart CurrentHeldPart=>heldPart;
        public AssemblyInventory Inventory=>inventory;
        public bool IsHoldingPart=>heldPart;
        public bool Holds(AssemblyPart part)=>inventory && inventory.Contains(part);
        public bool IsInEditMode=>AssemblyManager.Instance && AssemblyManager.Instance.Mode==WorkshopMode.Edit;
        public bool IsEditModeActive=>IsInEditMode;
        public Camera ViewCamera=>playerCamera;
        public string HoverMessage { get; private set; }="";
        public PlacementTool Tool { get; private set; }
        public bool PlacementMode=>heldPart && placementMode && (!heldPart.Manager || heldPart.Manager.Difficulty!=SnapDifficulty.Easy);
        public bool CarryMode=>heldPart && !PlacementMode;
        Transform Held=>heldPart?heldPart.transform:heldPlatform?heldPlatform.transform:null;
        void Awake()
        {
            controller=GetComponent<CharacterController>();if(!playerCamera)playerCamera=GetComponentInChildren<Camera>();if(!playerCamera)playerCamera=Camera.main;
            standingHeight=controller.height;standingEye=playerCamera?playerCamera.transform.localPosition.y:1.65f;normalFov=playerCamera?playerCamera.fieldOfView:60;
            hover=gameObject.AddComponent<AssemblyVisual>();pickupQuery=gameObject.AddComponent<AssemblyPickupQuery>();collisionGuard=gameObject.AddComponent<HeldPlayerCollisionGuard>();inventory=gameObject.AddComponent<AssemblyInventory>();
            spawnPosition=transform.position;spawnRotation=transform.rotation;
        }
        void Start(){if(!AssemblyManager.Instance && !FindAnyObjectByType<AssemblyManager>())new GameObject("Assembly Workshop").AddComponent<AssemblyManager>();}
        void Update()
        {
            if(!playerCamera)return;
            if(transform.position.y<voidHeight)Respawn();
            collisionGuard.Refresh();inventory.Prune();
            if(heldPart && !inventory.Contains(heldPart))heldPart=null;
            if(!heldPart && inventory.Count>0)Activate(inventory.Items.First());
            if(!workshopUI)workshopUI=FindAnyObjectByType<AssemblyWorkshopUI>();
            bool menu=workshopUI && workshopUI.IsOpen;
            Cursor.lockState=menu?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=menu;
            if(menu){ClearHover();return;}
            if(InputHelper.IsKeyDown(KeyCode.F))FlipWorkpiece();
            if(InputHelper.IsKeyDown(KeyCode.Q))TogglePlacementMode();
            if((PlacementMode || heldPlatform) && InputHelper.IsKeyDown(KeyCode.R))TogglePlacementTool();
            var delta=MouseDelta();bool manipulating=(PlacementMode || heldPlatform) && MouseHeld(1);
            playerCamera.fieldOfView=Mathf.Lerp(playerCamera.fieldOfView,!manipulating && MouseHeld(1)?zoomFieldOfView:normalFov,12*Time.deltaTime);
            if(!manipulating){transform.Rotate(Vector3.up,delta.x*mouseSensitivity);pitch=Mathf.Clamp(pitch-delta.y*mouseSensitivity,-85,85);playerCamera.transform.localRotation=Quaternion.Euler(pitch,0,0);}
            UpdateCrouch(InputHelper.IsKeyHeld(KeyCode.LeftControl)||InputHelper.IsKeyHeld(KeyCode.C));
            float x=(InputHelper.IsKeyHeld(KeyCode.D)?1:0)-(InputHelper.IsKeyHeld(KeyCode.A)?1:0),z=(InputHelper.IsKeyHeld(KeyCode.W)?1:0)-(InputHelper.IsKeyHeld(KeyCode.S)?1:0);
            if(controller.isGrounded && fallSpeed<0)fallSpeed=-2;
            if(controller.isGrounded && !crouched && InputHelper.IsKeyDown(KeyCode.Space))fallSpeed=Mathf.Sqrt(jumpHeight*-2*gravity);
            fallSpeed+=gravity*Time.deltaTime;
            var direction=Vector3.ClampMagnitude(transform.right*x+transform.forward*z,1)*(crouched?crouchSpeed:InputHelper.IsKeyHeld(KeyCode.LeftShift)?sprintSpeed:walkSpeed);
            if(controller.enabled)controller.Move((direction+Vector3.up*fallSpeed)*Time.deltaTime);
            if(Held && InputHelper.IsKeyDown(KeyCode.G)){DropHeldPart(false);return;}
            if(Held)
            {
                if(heldPlatform && heldPlatform.IsFlipping){heldViewRotation=Quaternion.Inverse(playerCamera.transform.rotation)*Held.rotation;platformWasFlipping=true;return;}
                if(platformWasFlipping){heldViewRotation=Quaternion.Inverse(playerCamera.transform.rotation)*Held.rotation;platformWasFlipping=false;}
                var rotation=playerCamera.transform.rotation*heldViewRotation;
                if(CarryMode)
                {
                    float distance=Mathf.Max(1,AssemblyHeldView.WorldBounds(heldPart).extents.magnitude*.9f);
                    heldPart.MoveHeld(playerCamera.transform.TransformPoint(new Vector3(.35f,-.2f,distance))-rotation*Vector3.Scale(contactLocal,Held.lossyScale),rotation);
                }
                else
                {
                    if(manipulating)Manipulate(delta,ref rotation);
                    heldViewRotation=Quaternion.Inverse(playerCamera.transform.rotation)*rotation;
                    var position=playerCamera.transform.TransformPoint(new Vector3(placementOffset.x,placementOffset.y,holdDistance))-rotation*Vector3.Scale(contactLocal,Held.lossyScale);
                    if(heldPart)heldPart.MoveHeld(position,rotation);else heldPlatform.MoveHeld(position,rotation);
                }
                foreach(var item in inventory.Items)if(item!=heldPart){inventory.Hide(item);item.MoveHeld(new Vector3(0,-1000,0),item.transform.rotation);}
                var ray=new Ray(playerCamera.transform.position,playerCamera.transform.forward);
                var manager=AssemblyManager.Instance;var guide=manager?manager.GetComponent<AssemblyEasyGuide>():null;
                bool click=MouseDown(0)||InputHelper.IsKeyDown(KeyCode.E);
                if(heldPart && manager && manager.Mode==WorkshopMode.Assembly && manager.Task==AssemblyTask.Assemble)
                {
                    var target=guide?guide.Aim(ray,pickupDistance,playerCamera):null;
                    if(click && target && (PlacementMode || manager.Difficulty==SnapDifficulty.Easy))
                    {
                        TryPlace(ray);return;
                    }
                }
                if(heldPart && CarryMode){HoverForCollection(ray,click);return;}
                if(click)DropHeldPart();return;
            }
            HoverForCollection(new Ray(playerCamera.transform.position,playerCamera.transform.forward),MouseDown(0)||InputHelper.IsKeyDown(KeyCode.E));
        }
        public void Manipulate(Vector2 delta,ref Quaternion rotation)
        {
            if(Tool==PlacementTool.Rotate)rotation=Quaternion.AngleAxis(-delta.x*.5f,playerCamera.transform.up)*Quaternion.AngleAxis(delta.y*.5f,playerCamera.transform.right)*rotation;
            else if(InputHelper.IsKeyHeld(KeyCode.LeftShift))holdDistance=Mathf.Clamp(holdDistance+delta.y*.008f,minHoldDistance,maxHoldDistance);
            else placementOffset+=new Vector3(delta.x,delta.y,0)*.003f;
        }
        void HoverForCollection(Ray ray,bool click)
        {
            if(!pickupQuery.Raycast(ray,pickupDistance,pickupLayerMask,out var part,out var contact,out var hit)){ClearHover();return;}
            if(part && part.Manager)part=part.Manager.InteractionPart(part);
            var platform=hit.collider?hit.collider.GetComponentInParent<PickupablePlatform>():null;
            if(!part){ClearHover();if(!Held && platform && click)GrabPlatform(platform,hit.point);return;}
            string reason="";bool allowed=heldPart?part.Manager && part.Manager.CanCollect(part,heldPart,out reason):!part.Manager || part.Manager.CanPickUp(part,out reason);
            HoverMessage=allowed?part.PartDisplayName:part.PartDisplayName+" — "+reason;
            if(hoveredPart!=part || hoveredAllowed!=allowed){hover.Hover(part,allowed);hoveredPart=part;hoveredAllowed=allowed;}
            if(!click)return;
            if(!Held && part.Definition!=null && part.Definition.isBase){GrabPlatform(part.Manager.MovableFrame(part).GetComponent<PickupablePlatform>(),contact);return;}
            if(allowed)Collect(part,contact);
        }
        void Collect(AssemblyPart part,Vector3 contact)
        {
            bool first=inventory.Count==0;if(!inventory.Collect(part))return;
            if(first){heldPart=part;CaptureContact(contact);}else {inventory.Hide(part);part.MoveHeld(new Vector3(0,-1000,0),part.transform.rotation);collisionGuard.Track(part.transform,part,null);}
            ClearHover();
        }
        public bool TryPlace(Ray ray)
        {
            var manager=heldPart?heldPart.Manager:null;var guide=manager?manager.GetComponent<AssemblyEasyGuide>():null;
            if(!guide)return false;var target=guide.Aim(ray,pickupDistance,playerCamera);
            if(!target || !inventory.Contains(target))return false;
            if(target!=heldPart)Activate(target,true);
            bool snapped=guide.Place(ray,pickupDistance,playerCamera);
            if(snapped){inventory.Remove(target);if(inventory.Count>0)Activate(inventory.Items.OrderBy(p=>p.Definition.order).First(),true);else heldPart=null;}
            return snapped;
        }
        void Activate(AssemblyPart part,bool transferPose=false)
        {
            var previous=heldPart;var center=previous?AssemblyHeldView.WorldBounds(previous).center:Vector3.zero;
            var rotation=previous?previous.transform.rotation:part.transform.rotation;
            if(transferPose && previous && previous.TargetSocket && part.TargetSocket)
                rotation=previous.transform.rotation*Quaternion.Inverse(previous.GetTargetPose(previous.TargetSocket).rotation)*part.GetTargetPose(part.TargetSocket).rotation;
            if(previous && previous!=part && inventory.Contains(previous))inventory.Hide(previous);
            heldPart=part;inventory.Reveal(part);
            var localCenter=part.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(part).center);
            if(transferPose)part.MoveHeld(center-rotation*Vector3.Scale(localCenter,part.transform.lossyScale),rotation);
            contactLocal=localCenter;heldViewRotation=Quaternion.Inverse(playerCamera.transform.rotation)*part.transform.rotation;
            collisionGuard.Track(part.transform,part,null);
        }
        void CaptureContact(Vector3 point){holdDistance=Mathf.Clamp(Vector3.Distance(playerCamera.transform.position,point),minHoldDistance,maxHoldDistance);contactLocal=Held.InverseTransformPoint(point);heldViewRotation=Quaternion.Inverse(playerCamera.transform.rotation)*Held.rotation;placementMode=false;placementOffset=Vector3.zero;collisionGuard.Track(Held,heldPart,heldPlatform);}
        void ClearHover(){if(hoveredPart && hover)hover.Clear();hoveredPart=null;HoverMessage="";}
        public void FlipWorkpiece(){if(heldPlatform)heldPlatform.Flip();else AssemblyManager.Instance?.FlipWorkpiece();}
        public void TogglePlacementTool()=>Tool=Tool==PlacementTool.Move?PlacementTool.Rotate:PlacementTool.Move;
        public void TogglePlacementMode(){if(!heldPart || heldPart.Manager && heldPart.Manager.Difficulty==SnapDifficulty.Easy)return;placementMode=!placementMode;placementOffset=Vector3.zero;if(placementMode)holdDistance=Mathf.Clamp(1.5f,minHoldDistance,maxHoldDistance);}
        public void PickUp(AssemblyPart part){if(part && part.Manager)part=part.Manager.InteractionPart(part);if(heldPlatform || !part)return;Collect(part,AssemblyHeldView.WorldBounds(part).center);}
        void GrabPlatform(PickupablePlatform platform,Vector3 point){if(platform && platform.BeginGrab()){heldPlatform=platform;CaptureContact(point);ClearHover();}}
        public void PickUpWorkpiece(){if(Held || !AssemblyManager.Instance)return;var root=AssemblyManager.Instance.AssemblyRoot;GrabPlatform(root.GetComponent<PickupablePlatform>(),root.position);}
        public void DropHeldPart(bool attemptSnap=true)
        {
            bool carrying=CarryMode;int index=0;
            foreach(var part in inventory.Items.ToArray())
            {
                inventory.Reveal(part);
                bool primary=part==heldPart;
                if((carrying || !primary) && playerCamera)
                {
                    float radius=AssemblyHeldView.WorldBounds(part).extents.magnitude;
                    var centerLocal=part.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(part).center);
                    var center=playerCamera.transform.TransformPoint(new Vector3((index++%4)*.12f,0,radius+controller.radius+.7f));
                    part.MoveHeld(center-part.transform.rotation*Vector3.Scale(centerLocal,part.transform.lossyScale),part.transform.rotation);
                }
                part.Release(attemptSnap && primary && !carrying);
                if(IsInEditMode && !part.IsSnapped){part.Manager.CaptureTray(part);part.Body.isKinematic=true;part.Body.interpolation=RigidbodyInterpolation.None;}
                inventory.Remove(part);
            }
            if(heldPlatform)heldPlatform.Release();heldPart=null;heldPlatform=null;collisionGuard?.Refresh();ClearHover();
        }
        public void Respawn(){DropHeldPart(false);bool enabled=controller.enabled;controller.enabled=false;transform.SetPositionAndRotation(spawnPosition,spawnRotation);fallSpeed=0;pitch=0;if(playerCamera)playerCamera.transform.localRotation=Quaternion.identity;controller.enabled=enabled;Physics.SyncTransforms();}
        void UpdateCrouch(bool requested)
        {
            if(!requested && crouched)
            {
                var bottom=transform.position+Vector3.up*controller.radius;var top=transform.position+Vector3.up*(standingHeight-controller.radius);
                foreach(var collider in Physics.OverlapCapsule(bottom,top,controller.radius*.95f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                    if(collider!=controller && !collider.transform.IsChildOf(transform) && !Physics.GetIgnoreCollision(controller,collider)){requested=true;break;}
            }
            crouched=requested;controller.height=crouched?crouchHeight:standingHeight;var center=controller.center;center.y=controller.height*.5f;controller.center=center;
            var eye=playerCamera.transform.localPosition;eye.y=Mathf.Lerp(eye.y,crouched?standingEye-(standingHeight-crouchHeight):standingEye,15*Time.deltaTime);playerCamera.transform.localPosition=eye;
        }
        void OnDisable(){DropHeldPart(false);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        static Vector2 MouseDelta(){
#if ENABLE_INPUT_SYSTEM
            return Mouse.current!=null?Mouse.current.delta.ReadValue():Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxis("Mouse X"),Input.GetAxis("Mouse Y"))*10;
#else
            return Vector2.zero;
#endif
        }
        static bool MouseDown(int button){
#if ENABLE_INPUT_SYSTEM
            return Mouse.current!=null && (button==0?Mouse.current.leftButton.wasPressedThisFrame:Mouse.current.rightButton.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(button);
#else
            return false;
#endif
        }
        static bool MouseHeld(int button){
#if ENABLE_INPUT_SYSTEM
            return Mouse.current!=null && (button==0?Mouse.current.leftButton.isPressed:Mouse.current.rightButton.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(button);
#else
            return false;
#endif
        }
    }
}
