using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.UI;
namespace EngineAssembly
{
    public sealed class QuestVRRig : MonoBehaviour
    {
        sealed class Hand
        {public string name;public Transform grip,aim,visual;public bool tracked,leverGrip;public XRRayInteractor ray;public LineRenderer line;public AssemblyPart held;public LiftControlHandle lever,pressedButton;public float leverY;public Vector3 contact;public Quaternion rotation;public InputAction trackedAction,position,rotationAction,aimPosition,aimRotation,trigger,gripAction,stick,primary;}
        QuestVRSession session;Hand left,right;Camera head;CharacterController controller;AssemblyPickupQuery pickup;HeldPlayerCollisionGuard guard;AssemblyManager manager;AssemblyWorkshopUI ui;
        readonly HashSet<Canvas> bound=new HashSet<Canvas>();Material rayMaterial;LineRenderer arc;Transform marker;float falling;bool turnLatched,teleporting,teleportValid;Vector3 teleportPoint,spawn;
        EventSystem events;XRUIInputModule inputModule;int canvasRevision=-1;InputAction headPosition,headRotation,menu;readonly Vector3[] teleportPoints=new Vector3[29];readonly Collider[] teleportObstacles=new Collider[64];
        public Camera HeadCamera=>head;public bool TeleportValid=>teleportValid;
        public void Configure(QuestVRSession owner)
        {
            session=owner;headPosition=session.Input("Head/Position");headRotation=session.Input("Head/Rotation");menu=session.Input("Left/Menu");manager=FindAnyObjectByType<AssemblyManager>();ui=FindAnyObjectByType<AssemblyWorkshopUI>();var player=FindAnyObjectByType<PlayerAssemblyController>();
            spawn=player?player.transform.position:Vector3.zero;transform.position=spawn;
            foreach(var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None)){if(camera.GetComponent<AudioListener>())camera.GetComponent<AudioListener>().enabled=false;camera.enabled=false;}
            if(player){player.DropHeldPart(false);player.enabled=false;player.GetComponent<CharacterController>().enabled=false;}
            controller=gameObject.AddComponent<CharacterController>();controller.radius=.23f;controller.height=1.7f;controller.center=Vector3.up*.85f;controller.stepOffset=.22f;
            head=new GameObject("Tracked Quest head",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();head.transform.SetParent(transform,false);head.tag="MainCamera";head.nearClipPlane=.04f;head.farClipPlane=35;head.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            head.transform.localPosition=Vector3.up*1.65f;
            if(!FindAnyObjectByType<XRInteractionManager>())gameObject.AddComponent<XRInteractionManager>();
            pickup=gameObject.AddComponent<AssemblyPickupQuery>();guard=gameObject.AddComponent<HeldPlayerCollisionGuard>();
            rayMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));rayMaterial.SetColor("_BaseColor",new Color(.15f,.78f,.83f));
            left=CreateHand("Left");right=CreateHand("Right");arc=Line("Teleport arc",transform,.012f);marker=GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;marker.name="Teleport landing marker";Destroy(marker.GetComponent<Collider>());marker.localScale=new Vector3(.45f,.006f,.45f);marker.GetComponent<Renderer>().sharedMaterial=rayMaterial;marker.gameObject.SetActive(false);BindUI();
        }
        Hand CreateHand(string name)
        {
            var hand=new Hand{name=name,trackedAction=session.Input(name+"/Tracked"),position=session.Input(name+"/Position"),rotationAction=session.Input(name+"/Rotation"),aimPosition=session.Input(name+"/AimPosition"),aimRotation=session.Input(name+"/AimRotation"),trigger=session.Input(name+"/Trigger"),gripAction=session.Input(name+"/Grip"),stick=session.Input(name+"/Stick"),primary=session.Input(name+"/Primary")};hand.grip=new GameObject(name+" tracked controller").transform;hand.grip.SetParent(transform,false);hand.aim=new GameObject(name+" controller UI ray").transform;hand.aim.SetParent(transform,false);
            var dot=GameObject.CreatePrimitive(PrimitiveType.Sphere);dot.name="Controller pointer origin";dot.transform.SetParent(hand.grip,false);dot.transform.localScale=Vector3.one*.025f;Destroy(dot.GetComponent<Collider>());dot.GetComponent<Renderer>().sharedMaterial=rayMaterial;hand.visual=dot.transform;
            hand.ray=hand.aim.gameObject.AddComponent<XRRayInteractor>();hand.ray.maxRaycastDistance=8;hand.ray.enableUIInteraction=true;
            hand.ray.selectInput.inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue;
            hand.ray.activateInput.inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue;
            hand.ray.uiPressInput=new XRInputButtonReader{inputSourceMode=XRInputButtonReader.InputSourceMode.InputActionReference,inputActionReferencePerformed=InputActionReference.Create(session.Input(name+"/Trigger")),inputActionReferenceValue=InputActionReference.Create(session.Input(name+"/Trigger"))};
            hand.ray.uiScrollInput=new XRInputValueReader<Vector2>{inputSourceMode=XRInputValueReader.InputSourceMode.InputActionReference,inputActionReference=InputActionReference.Create(session.Input(name+"/Stick"))};
            hand.line=Line(name+" pointer",hand.aim,.004f);return hand;
        }
        LineRenderer Line(string name,Transform parent,float width){var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(parent,false);line.useWorldSpace=true;line.widthMultiplier=width;line.sharedMaterial=rayMaterial;line.positionCount=2;return line;}
        public void BindUI()
        {
            if(inputModule && canvasRevision==WorkshopCanvasRegistration.Revision)return;
            if(!events)events=FindAnyObjectByType<EventSystem>();if(!events)events=new GameObject("VR Event System").AddComponent<EventSystem>();
            if(!inputModule)
            {
                foreach(var old in events.GetComponents<BaseInputModule>())if(!(old is XRUIInputModule)){old.enabled=false;Destroy(old);}
                inputModule=events.GetComponent<XRUIInputModule>();if(!inputModule)inputModule=events.gameObject.AddComponent<XRUIInputModule>();inputModule.enableXRInput=true;
            }
            canvasRevision=WorkshopCanvasRegistration.Revision;bound.RemoveWhere(c=>!c);
            foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if(bound.Contains(canvas) || canvas.name.Contains("AR status"))continue;bound.Add(canvas);
                canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=head;var rect=(RectTransform)canvas.transform;rect.SetParent(head.transform,false);rect.sizeDelta=new Vector2(1440,900);rect.localScale=Vector3.one*.0012f;rect.localPosition=new Vector3(0,0,1.8f);rect.localRotation=Quaternion.identity;
                var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();if(scaler)scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
                if(!canvas.GetComponent<TrackedDeviceGraphicRaycaster>())canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
                foreach(var cross in canvas.GetComponentsInChildren<InvertingCrosshair>(true))cross.gameObject.SetActive(false);
            }
        }
        void Update()
        {
            if(!session || !session.Active)return;BindUI();
            var hp=headPosition.ReadValue<Vector3>();var hr=headRotation.ReadValue<Quaternion>();if(Quaternion.Dot(hr,hr)>.1f){head.transform.localPosition=hp+(session.FloorTracking?Vector3.zero:Vector3.up*1.65f);head.transform.localRotation=hr;}
            PoseHand(left);PoseHand(right);guard.Refresh();
            if(menu.WasPressedThisFrame()){if(WorkshopTutorial.IsOpen)WorkshopTutorial.Active.Close();else if(ui)ui.Open(ui.IsOpen?AssemblyWorkshopUI.Page.Closed:AssemblyWorkshopUI.Page.Pause);}
            if(Time.timeScale<=0 || ui && ui.IsOpen){ReleaseLever(left);ReleaseLever(right);StopTeleport();return;}
            if(!manager)return;
            if(transform.position.y<-8)Teleport(spawn);
            Move(left.stick.ReadValue<Vector2>(),Time.deltaTime);
            var stick=right.stick.ReadValue<Vector2>();if(Mathf.Abs(stick.x)>.7f && !turnLatched){transform.RotateAround(head.transform.position,Vector3.up,Mathf.Sign(stick.x)*30);turnLatched=true;}else if(Mathf.Abs(stick.x)<.3f)turnLatched=false;
            if(stick.y>.65f){teleporting=true;PreviewTeleport(right.aim.position,right.aim.forward);}else if(teleporting && stick.y<.3f){if(teleportValid)Teleport(teleportPoint);StopTeleport();}
            Interact(left,right);Interact(right,left);
        }
        void PoseHand(Hand hand)
        {
            bool tracked=hand.trackedAction.IsPressed();hand.tracked=tracked;hand.visual.gameObject.SetActive(tracked);hand.aim.gameObject.SetActive(tracked);if(!tracked){ReleaseLever(hand);return;}
            hand.grip.localPosition=hand.position.ReadValue<Vector3>()+(session.FloorTracking?Vector3.zero:Vector3.up*1.65f);var rotation=hand.rotationAction.ReadValue<Quaternion>();if(Quaternion.Dot(rotation,rotation)>.1f)hand.grip.localRotation=rotation;
            var aimRotation=hand.aimRotation.ReadValue<Quaternion>();hand.aim.SetPositionAndRotation(hand.grip.position,hand.grip.rotation);if(Quaternion.Dot(aimRotation,aimRotation)>.1f){hand.aim.localPosition=hand.aimPosition.ReadValue<Vector3>()+(session.FloorTracking?Vector3.zero:Vector3.up*1.65f);hand.aim.localRotation=aimRotation;}
            Vector3 end=hand.aim.position+hand.aim.forward*5;if(hand.ray.TryGetCurrentUIRaycastResult(out var uiHit))end=uiHit.worldPosition;else if(Physics.Raycast(hand.aim.position,hand.aim.forward,out var hit,5))end=hit.point;
            hand.line.SetPosition(0,hand.aim.position);hand.line.SetPosition(1,end);
            if(hand.held && hand.held.IsSelected){var r=hand.grip.rotation*hand.rotation;hand.held.MoveHeld(hand.grip.TransformPoint(new Vector3(0,-.035f,.18f))-r*Vector3.Scale(hand.contact,hand.held.transform.lossyScale),r);}else hand.held=null;
        }
        void Interact(Hand hand,Hand other)
        {
            if(!hand.tracked)return;var trigger=hand.trigger;var grip=hand.gripAction;var ray=new Ray(hand.aim.position,hand.aim.forward);
            if(manager && manager.DisplayActive){if(!hand.ray.TryGetCurrentUIRaycastResult(out _) && trigger.WasPressedThisFrame())manager.GetComponent<PCDisplayChapter>()?.Inspect(ray);return;}
            if(hand.lever){if(!hand.lever.Held || !(hand.leverGrip?grip:trigger).IsPressed())ReleaseLever(hand);else hand.lever.SetValue((hand.grip.position.y-hand.leverY)/.1f);return;}
            // Physical controls use controller proximity and grip/touch. Distant
            // ray interaction remains available, but never selects a different lift.
            if(hand.pressedButton && hand.pressedButton.DistanceTo(hand.grip.position)>.10f)hand.pressedButton=null;
            var button=LiftControlHandle.Nearest(hand.grip.position,.08f,LiftControlHandle.ControlKind.Flip);
            if(button && hand.pressedButton!=button)
            {
                bool inFront=Vector3.Dot(hand.grip.position-button.transform.position,-button.transform.parent.forward)>-.012f;
                bool touch=!hand.held && button.DistanceTo(hand.grip.position)<.018f;
                if(inFront && (touch || trigger.WasPressedThisFrame()))
                {hand.pressedButton=button;button.Begin();return;}
            }
            if(!hand.held && grip.WasPressedThisFrame())
            {
                var lever=LiftControlHandle.Nearest(hand.grip.position,.09f,LiftControlHandle.ControlKind.Lever);
                if(lever && lever.Begin()){hand.lever=lever;hand.leverGrip=true;hand.leverY=hand.grip.position.y;return;}
            }
            if(hand.ray.TryGetCurrentUIRaycastResult(out _) && !hand.held)return;
            if(trigger.WasPressedThisFrame())
            {
                if(hand.held && manager)
                {var guide=manager.GetComponent<AssemblyEasyGuide>();if(guide && guide.Aim(ray,6,head)==hand.held && hand.held.GuidedSnap(hand.held.TargetSocket)){hand.held=null;return;}}
                var control=LiftControlHandle.RayControl(ray,4);
                {if(control && (!hand.held || control.Kind==LiftControlHandle.ControlKind.Flip) && control.Begin()){if(control.Kind==LiftControlHandle.ControlKind.Lever){hand.lever=control;hand.leverGrip=false;hand.leverY=hand.grip.position.y;}return;}}
            }
            if(grip.WasPressedThisFrame() && !hand.held && manager && pickup.Raycast(ray,4,~0,out var part,out _,out _))
            {
                part=manager.InteractionPart(part);if(part && part!=other.held && !part.IsSelected && part.BeginGrab())
                {hand.held=part;hand.contact=part.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(part).center);hand.rotation=Quaternion.Inverse(hand.grip.rotation)*part.transform.rotation;guard.Track(part.transform,part,null);part.transform.SetParent(hand.grip,true);}
            }
            if(hand.held && !grip.IsPressed())ReleasePart(hand);
            if(hand.primary.WasPressedThisFrame())manager?.GetComponent<AssemblyStepAssist>()?.NextStep();
        }
        void ReleaseLever(Hand hand){if(hand.lever)hand.lever.Release();hand.lever=null;}
        void ReleasePart(Hand hand){if(hand.held){hand.held.transform.SetParent(null,true);hand.held.Release(false);hand.held=null;}}
        public void Move(Vector2 input,float dt)
        {
            float height=Mathf.Clamp(head.transform.localPosition.y,.8f,2.1f);controller.height=height;controller.center=new Vector3(head.transform.localPosition.x,height*.5f,head.transform.localPosition.z);
            var forward=Vector3.ProjectOnPlane(head.transform.forward,Vector3.up).normalized;var rightAxis=Vector3.Cross(Vector3.up,forward);var motion=input.magnitude>.15f?Vector3.ClampMagnitude(rightAxis*input.x+forward*input.y,1)*1.6f:Vector3.zero;
            if(controller.isGrounded && falling<0)falling=-1;falling-=9.81f*dt;controller.Move((motion+Vector3.up*falling)*dt);
        }
        public bool CanTeleport(Vector3 point,Vector3 normal)
        {
            float floor=manager && manager.GetComponent<WorkpieceLift>()?manager.GetComponent<WorkpieceLift>().GroundHeight:0;
            if(normal.y<.7f || Mathf.Abs(point.y-floor)>.18f)return false;float h=Mathf.Clamp(head.transform.localPosition.y,1.1f,2.1f);
            int count=Physics.OverlapCapsuleNonAlloc(point+Vector3.up*(controller.radius+.025f),point+Vector3.up*(h-controller.radius),controller.radius,teleportObstacles,~0,QueryTriggerInteraction.Ignore);
            if(count==teleportObstacles.Length)return false;
            for(int i=0;i<count;i++)if(!teleportObstacles[i].transform.IsChildOf(transform))return false;return true;
        }
        public void Teleport(Vector3 point){var offset=Vector3.ProjectOnPlane(head.transform.position-transform.position,Vector3.up);controller.enabled=false;transform.position=point-offset;controller.enabled=true;falling=0;}
        void PreviewTeleport(Vector3 start,Vector3 direction)
        {
            int count=1;teleportPoints[0]=start;var position=start;var velocity=direction*6+Vector3.up*2;teleportValid=false;
            for(int i=0;i<28;i++){var next=position+velocity*.06f;velocity+=Vector3.down*9.81f*.06f;if(Physics.Linecast(position,next,out var hit,~0,QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform)){teleportPoints[count++]=hit.point;teleportPoint=hit.point;teleportValid=CanTeleport(hit.point,hit.normal);break;}teleportPoints[count++]=next;position=next;}
            arc.enabled=true;arc.positionCount=count;for(int i=0;i<count;i++)arc.SetPosition(i,teleportPoints[i]);arc.startColor=arc.endColor=teleportValid?Color.cyan:Color.red;marker.gameObject.SetActive(teleportValid);if(teleportValid)marker.position=teleportPoint+Vector3.up*.012f;
        }
        void StopTeleport(){teleporting=false;teleportValid=false;if(arc)arc.enabled=false;if(marker)marker.gameObject.SetActive(false);}
        void OnDestroy(){if(left!=null){ReleasePart(left);ReleaseLever(left);}if(right!=null){ReleasePart(right);ReleaseLever(right);}if(marker)Destroy(marker.gameObject);if(rayMaterial)Destroy(rayMaterial);}
    }
}
