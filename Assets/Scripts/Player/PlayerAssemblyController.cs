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
        MobileWorkshopControls mobileControls;
        Vector3 spawnPosition,contactLocal,placementOffset;
        Quaternion spawnRotation,heldViewRotation;
        bool placementMode,crouched,platformWasFlipping,hoveredAllowed;
        float pitch,fallSpeed,holdDistance=1.5f,standingHeight,standingEye,normalFov;
        public AssemblyPart CurrentHeldPart=>heldPart;
        public AssemblyPart HoveredPart=>hoveredPart;
        public AssemblyInventory Inventory=>inventory;
        public bool IsHoldingPart=>heldPart;
        public bool Holds(AssemblyPart part)=>inventory && inventory.Contains(part);
        public void DetachForAssistance(AssemblyPart part)
        {
            if(!inventory.Contains(part))return;
            inventory.Reveal(part);inventory.Remove(part);
            if(heldPart==part){heldPart=null;if(inventory.Count>0)Activate(inventory.Items.First());}
            ClearHover();
        }
        public bool IsInEditMode=>AssemblyManager.Instance && AssemblyManager.Instance.Mode==WorkshopMode.Edit;
        public bool IsEditModeActive=>IsInEditMode;
        public Camera ViewCamera=>playerCamera;
        public Ray InteractionRay=>playerCamera.ViewportPointToRay(new Vector3(.5f,.5f,0));
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
            AssemblyPlayerCollision.Attach(controller);
            spawnPosition=transform.position;spawnRotation=transform.rotation;
            if(ExperienceMenu.MobileWorkshopRequested)mobileControls=gameObject.AddComponent<MobileWorkshopControls>();
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
            bool menu=workshopUI && workshopUI.IsOpen || WorkshopTutorial.BlocksInput;
            bool displayMode=AssemblyManager.Instance && AssemblyManager.Instance.DisplayActive;
            bool hudPointer=InputHelper.IsKeyHeld(KeyCode.LeftAlt) || displayMode && AssemblyManager.Instance.GetComponent<PCDisplayChapter>() is PCDisplayChapter display && display.SettingsOpen;
            var desiredLock=menu || mobileControls || hudPointer?CursorLockMode.None:CursorLockMode.Locked;
            if(Cursor.lockState!=desiredLock)Cursor.lockState=desiredLock;
            if(Cursor.visible!=(menu || hudPointer))Cursor.visible=menu || hudPointer;
            if(menu || hudPointer){ClearHover();return;}
            if(mobileControls){UpdateMobile();return;}
            if(!WorkshopTutorial.IsOpen && InputHelper.IsKeyDown(KeyCode.R))AssemblyManager.Instance?.GetComponent<AssemblyNarrator>()?.ReplayCurrent();
            if(LiftControlHandle.DesktopHolding && LiftControlHandle.ProcessDesktop(playerCamera,false,MouseHeld(0),MouseDelta().y,pickupDistance)){ClearHover();HoverMessage="Hold and drag up / down · Release to stop";return;}
            var lift=AssemblyManager.Instance?AssemblyManager.Instance.GetComponent<WorkpieceLift>():null;
            if(lift && InputHelper.IsKeyDown(KeyCode.LeftArrow))lift.Cycle(-1);
            if(lift && InputHelper.IsKeyDown(KeyCode.RightArrow))lift.Cycle(1);
            if(lift)lift.MoveFromInput((InputHelper.IsKeyHeld(KeyCode.UpArrow)?1:0)-(InputHelper.IsKeyHeld(KeyCode.DownArrow)?1:0));
            if(InputHelper.IsKeyDown(KeyCode.F))FlipWorkpiece();
            if(InputHelper.IsKeyDown(KeyCode.Q))TogglePlacementMode();
            if((PlacementMode || heldPlatform) && InputHelper.IsKeyDown(KeyCode.T))TogglePlacementTool();
            var delta=MouseDelta();bool manipulating=(PlacementMode || heldPlatform) && MouseHeld(1);
            playerCamera.fieldOfView=Mathf.Lerp(playerCamera.fieldOfView,!displayMode && !manipulating && MouseHeld(1)?zoomFieldOfView:normalFov,12*Time.deltaTime);
            if(!manipulating){transform.Rotate(Vector3.up,delta.x*mouseSensitivity);pitch=Mathf.Clamp(pitch-delta.y*mouseSensitivity,-85,85);playerCamera.transform.localRotation=Quaternion.Euler(pitch,0,0);}
            UpdateCrouch(InputHelper.IsKeyHeld(KeyCode.LeftControl)||InputHelper.IsKeyHeld(KeyCode.C));
            float x=(InputHelper.IsKeyHeld(KeyCode.D)?1:0)-(InputHelper.IsKeyHeld(KeyCode.A)?1:0),z=(InputHelper.IsKeyHeld(KeyCode.W)?1:0)-(InputHelper.IsKeyHeld(KeyCode.S)?1:0);
            if(controller.isGrounded && fallSpeed<0)fallSpeed=-2;
            if(controller.isGrounded && !crouched && InputHelper.IsKeyDown(KeyCode.Space)){fallSpeed=Mathf.Sqrt(jumpHeight*-2*gravity);WorkshopPractice.Record("jump");}
            fallSpeed+=gravity*Time.deltaTime;
            var direction=Vector3.ClampMagnitude(transform.right*x+transform.forward*z,1)*(crouched?crouchSpeed:InputHelper.IsKeyHeld(KeyCode.LeftShift)?sprintSpeed:walkSpeed);
            if(direction.sqrMagnitude>.01f && InputHelper.IsKeyHeld(KeyCode.LeftShift))WorkshopPractice.Record("sprint",Time.deltaTime);
            if(controller.enabled)controller.Move((direction+Vector3.up*fallSpeed)*Time.deltaTime);
            if(displayMode)
            {
                if(MouseDown(0) && (!UnityEngine.EventSystems.EventSystem.current || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))
                {var ray=InteractionRay;var control=DisplaySceneControl.RayControl(ray,pickupDistance);if(control)control.Activate();else AssemblyManager.Instance.GetComponent<PCDisplayChapter>()?.Inspect(ray);}
                HoverMessage="Aim + click to inspect · Hold ALT for playback controls";return;
            }
            var aimedControl=LiftControlHandle.RayControl(playerCamera.ViewportPointToRay(new Vector3(.5f,.5f,0)),pickupDistance);
            if(aimedControl && MouseDown(0))
            {
                ShowControlPrompt(aimedControl);
                LiftControlHandle.ProcessDesktop(playerCamera,MouseDown(0),MouseHeld(0),0,pickupDistance);return;
            }
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
                foreach(var item in inventory.Items)if(item!=heldPart){inventory.Hide(item);item.MoveHeld(heldPart.transform.position,heldPart.transform.rotation);}
                if(aimedControl){ShowControlPrompt(aimedControl);return;}
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
            if(aimedControl){ShowControlPrompt(aimedControl);return;}
            HoverForCollection(new Ray(playerCamera.transform.position,playerCamera.transform.forward),MouseDown(0)||InputHelper.IsKeyDown(KeyCode.E));
        }
        void ShowControlPrompt(LiftControlHandle control)
        {ClearHover();HoverMessage=control.Kind==LiftControlHandle.ControlKind.Lever?"Hold click + drag up / down to move this lift":"Click to turn this workpiece over";}
        void UpdateMobile()
        {
            var movement=mobileControls.Movement;
            var look=mobileControls.ConsumeLook();
            transform.Rotate(Vector3.up,look.x*mouseSensitivity);
            pitch=Mathf.Clamp(pitch-look.y*mouseSensitivity,-85,85);
            playerCamera.transform.localRotation=Quaternion.Euler(pitch,0,0);
            UpdateCrouch(mobileControls.CrouchHeld);
            if(controller.isGrounded && fallSpeed<0)fallSpeed=-2;
            if(controller.isGrounded && !crouched && mobileControls.ConsumeJump()){fallSpeed=Mathf.Sqrt(jumpHeight*-2*gravity);WorkshopPractice.Record("jump");}
            fallSpeed+=gravity*Time.deltaTime;
            var direction=Vector3.ClampMagnitude(transform.right*movement.x+transform.forward*movement.y,1)*(crouched?crouchSpeed:mobileControls.SprintHeld?sprintSpeed:walkSpeed);
            if(direction.sqrMagnitude>.01f && mobileControls.SprintHeld)WorkshopPractice.Record("sprint",Time.deltaTime);
            if(controller.enabled)controller.Move((direction+Vector3.up*fallSpeed)*Time.deltaTime);
            if(Held)
            {
                var rotation=playerCamera.transform.rotation*heldViewRotation;
                if(heldPart)
                {
                    float distance=Mathf.Max(1,AssemblyHeldView.WorldBounds(heldPart).extents.magnitude*.9f);
                    heldPart.MoveHeld(playerCamera.transform.TransformPoint(new Vector3(.35f,-.2f,distance))-rotation*Vector3.Scale(contactLocal,Held.lossyScale),rotation);
                }
                else heldPlatform.MoveHeld(playerCamera.transform.TransformPoint(new Vector3(0,0,holdDistance))-rotation*Vector3.Scale(contactLocal,Held.lossyScale),rotation);
            }
            if(!mobileControls.ConsumeTap(out var screen))return;
            var ray=playerCamera.ScreenPointToRay(screen);
            if(AssemblyManager.Instance && AssemblyManager.Instance.DisplayActive){var control=DisplaySceneControl.RayControl(ray,pickupDistance);if(control)control.Activate();else AssemblyManager.Instance.GetComponent<PCDisplayChapter>()?.Inspect(ray);return;}
            if(Held)
            {
                var manager=AssemblyManager.Instance;
                if(heldPart && manager && manager.Mode==WorkshopMode.Assembly && manager.Task==AssemblyTask.Assemble && TryPlace(ray))return;
                if(heldPart){HoverForCollection(ray,true);return;}
                return;
            }
            HoverForCollection(ray,true);
        }
        public void Manipulate(Vector2 delta,ref Quaternion rotation)
        {
            WorkshopPractice.Record("manipulate",delta.magnitude);
            WorkshopPractice.Record(Tool==PlacementTool.Move?"manipulate-move":"manipulate-rotate",delta.magnitude);
            if(Tool==PlacementTool.Rotate)rotation=Quaternion.AngleAxis(-delta.x*.5f,playerCamera.transform.up)*Quaternion.AngleAxis(delta.y*.5f,playerCamera.transform.right)*rotation;
            else if(InputHelper.IsKeyHeld(KeyCode.LeftShift))holdDistance=Mathf.Clamp(holdDistance+delta.y*.008f,minHoldDistance,maxHoldDistance);
            else placementOffset+=new Vector3(delta.x,delta.y,0)*.003f;
        }
        void HoverForCollection(Ray ray,bool click)
        {
            if(!pickupQuery.PickupRaycast(ray,pickupDistance,pickupLayerMask,AssemblyManager.Instance,playerCamera,out var part,out var contact,out var hit,heldPart)){ClearHover();return;}
            if(part && part.Manager)part=part.Manager.InteractionPart(part);
            var platform=hit.collider?hit.collider.GetComponentInParent<PickupablePlatform>():null;
            if(!part){ClearHover();if(!Held && platform && click)GrabPlatform(platform,hit.point);return;}
            string reason="";bool allowed=heldPart?part.Manager && part.Manager.CanCollect(part,heldPart,out reason):!part.Manager || part.Manager.CanPickUp(part,out reason);
            HoverMessage=allowed?part.PartDisplayName:part.PartDisplayName+" - "+reason;
            if(hoveredPart!=part || hoveredAllowed!=allowed){hover.Hover(part,allowed);hoveredPart=part;hoveredAllowed=allowed;}
            if(!click)return;
            if(!Held && part.Definition!=null && part.Definition.isBase){GrabPlatform(part.Manager.MovableFrame(part).GetComponent<PickupablePlatform>(),contact);return;}
            if(allowed)Collect(part,contact);
        }
        void Collect(AssemblyPart part,Vector3 contact)
        {
            bool first=inventory.Count==0;if(!inventory.Collect(part))return;
            if(first){heldPart=part;CaptureContact(contact);}else {inventory.Hide(part);part.MoveHeld(heldPart.transform.position,part.transform.rotation);collisionGuard.Track(part.transform,part,null);}
            ClearHover();
        }
        public bool TryPlace(Ray ray)
        {
            var manager=heldPart?heldPart.Manager:null;var guide=manager?manager.GetComponent<AssemblyEasyGuide>():null;
            if(!guide)return false;var target=guide.Aim(ray,pickupDistance,playerCamera);
            if(!target)return false;
            var consumed=inventory.Contains(target)?target:inventory.Items.FirstOrDefault(p=>AssemblyInventory.Matches(p,target));
            if(!consumed)return false;
            if(consumed!=heldPart)Activate(consumed,true);
            bool snapped=guide.Place(ray,pickupDistance,playerCamera,consumed);
            if(snapped){inventory.Remove(consumed);if(inventory.Count>0)Activate(inventory.Items.OrderBy(p=>p.Definition.order).First(),true);else heldPart=null;}
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
        void CaptureContact(Vector3 point){if(heldPart && heldPart.GetComponent<SubAssembly>())point=AssemblyHeldView.WorldBounds(heldPart).center;holdDistance=Mathf.Clamp(Vector3.Distance(playerCamera.transform.position,point),minHoldDistance,maxHoldDistance);contactLocal=Held.InverseTransformPoint(point);heldViewRotation=Quaternion.Inverse(playerCamera.transform.rotation)*Held.rotation;placementMode=false;placementOffset=Vector3.zero;collisionGuard.Track(Held,heldPart,heldPlatform);}
        void ClearHover(){if(hoveredPart && hover)hover.Clear();hoveredPart=null;HoverMessage="";}
        public void FlipWorkpiece(){var manager=AssemblyManager.Instance;if(heldPlatform)heldPlatform.Flip(manager?manager.WorkpieceBounds(heldPlatform.transform).center:heldPlatform.GeometryCenter());else manager?.FlipWorkpiece();}
        public void TogglePlacementTool(){Tool=Tool==PlacementTool.Move?PlacementTool.Rotate:PlacementTool.Move;WorkshopPractice.Record("placement-tool");}
        public void TogglePlacementMode(){if(!heldPart || heldPart.Manager && heldPart.Manager.Difficulty==SnapDifficulty.Easy)return;placementMode=!placementMode;placementOffset=Vector3.zero;if(placementMode){holdDistance=Mathf.Clamp(1.5f,minHoldDistance,maxHoldDistance);WorkshopPractice.Record("placement-mode");}}
        public void PickUp(AssemblyPart part){if(part && part.Manager)part=part.Manager.InteractionPart(part);if(heldPlatform || !part)return;Collect(part,AssemblyHeldView.WorldBounds(part).center);}
        void GrabPlatform(PickupablePlatform platform,Vector3 point){if(platform && platform.BeginGrab()){heldPlatform=platform;CaptureContact(point);ClearHover();}}
        public void PickUpWorkpiece(){if(Held || !AssemblyManager.Instance)return;var root=AssemblyManager.Instance.AssemblyRoot;GrabPlatform(root.GetComponent<PickupablePlatform>(),root.position);}
        public void DropHeldPart(bool attemptSnap=true)
        {
            if(!inventory)return;
            bool carrying=CarryMode;int index=0;
            var dropped=inventory.Items.ToArray();
            // Every inventory entry is a real CAD part. Space the entire stack by
            // its actual bounds instead of reusing four fixed release positions.
            // The old modulo-four layout put the fifth valve on top of the first.
            float span=.04f,radius=.02f;
            foreach(var part in dropped)
            {
                if(!part)continue;inventory.Reveal(part,true);
                var bounds=AssemblyHeldView.WorldBounds(part);
                span=Mathf.Max(span,bounds.size.magnitude+.035f);radius=Mathf.Max(radius,bounds.extents.magnitude);
            }
            int columns=Mathf.Max(1,Mathf.CeilToInt(Mathf.Sqrt(dropped.Length)));
            var forward=playerCamera?Vector3.ProjectOnPlane(playerCamera.transform.forward,Vector3.up).normalized:transform.forward;
            if(forward.sqrMagnitude<.1f)forward=transform.forward;
            var right=Vector3.Cross(Vector3.up,forward);
            var origin=(playerCamera?playerCamera.transform.position:transform.position)+forward*(radius+(controller?controller.radius:.25f)+.7f);
            foreach(var part in dropped)
            {
                if(!part)continue;
                bool primary=part==heldPart;
                if(carrying || !primary)
                {
                    var centerLocal=part.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(part).center);
                    var center=origin+right*((index%columns-(columns-1)*.5f)*span)+forward*(index/columns*span);
                    index++;
                    part.MoveHeld(center-part.transform.rotation*Vector3.Scale(centerLocal,part.transform.lossyScale),part.transform.rotation);
                }
                part.transform.SetParent(null,true);
                if(part.Body){part.Body.position=part.transform.position;part.Body.rotation=part.transform.rotation;part.Body.detectCollisions=true;}
                part.Release(attemptSnap && primary && !carrying);
                if(part.Body && !part.Body.isKinematic){part.Body.linearVelocity=Vector3.zero;part.Body.angularVelocity=Vector3.zero;part.Body.WakeUp();}
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
                foreach(var collider in Physics.OverlapCapsule(bottom,top,controller.radius*.95f,~0,QueryTriggerInteraction.Ignore))
                    if(collider!=controller && !collider.transform.IsChildOf(transform) && AssemblyPlayerCollision.CanContact(controller,collider) && !Physics.GetIgnoreCollision(controller,collider)){requested=true;break;}
            }
            if(requested && !crouched)WorkshopPractice.Record("crouch");crouched=requested;controller.height=crouched?crouchHeight:standingHeight;var center=controller.center;center.y=controller.height*.5f;controller.center=center;
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
