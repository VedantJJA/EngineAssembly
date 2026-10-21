using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace EngineAssembly
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public class PickupablePlatform : MonoBehaviour
    {
        [SerializeField] bool lockWhenReleased=true;
        [SerializeField] bool allowPickup=true;
        [SerializeField] bool carryRestingObjects=true;
        [SerializeField] float flipDuration=.45f;
        public UnityEvent onPickedUp=new UnityEvent(),onReleased=new UnityEvent();
        public bool IsHeld { get; private set; }
        public bool IsFlipping { get; private set; }
        public enum TurnPhase { None, Lowering, Turning, Raising }
        public TurnPhase Phase { get; private set; }
        public bool CanPickUp=>allowPickup;
        public void SetPickupAllowed(bool allowed){if(!allowed)Release();allowPickup=allowed;}
        Rigidbody body;
        readonly HashSet<Rigidbody> resting=new HashSet<Rigidbody>();
        readonly List<Passenger> passengers=new List<Passenger>();
        struct Passenger { public Rigidbody body; public Transform parent; public bool kinematic, gravity; public RigidbodyInterpolation interpolation; }
        Quaternion flipStart,flipEnd;
        Vector3 flipCenter,flipLocalCenter;
        float flipTime;
        bool groundFlip;
        Vector3 undergroundCenter,returnCenter;
        readonly Dictionary<Collider,bool> flipColliders=new Dictionary<Collider,bool>();
        readonly Dictionary<Rigidbody,bool> flipBodies=new Dictionary<Rigidbody,bool>();
        const float TravelDuration=.55f,TurnDuration=.65f;
        void Awake() { body=GetComponent<Rigidbody>(); body.isKinematic=lockWhenReleased; }
        public bool BeginGrab()
        {
            if(!allowPickup || IsHeld || IsFlipping) return false;
            if(carryRestingObjects)FindStagedPassengers();
            IsHeld=true;body.isKinematic=true;
            if(carryRestingObjects)
                foreach(var other in resting)
                {
                    if(!other || other==body || other.transform.IsChildOf(transform) || transform.IsChildOf(other.transform)) continue;
                    var part=other.GetComponent<AssemblyPart>(); if(part && (part.IsSnapped || part.IsSelected || part.IsBusy)) continue;
                    passengers.Add(new Passenger { body=other,parent=other.transform.parent,kinematic=other.isKinematic,gravity=other.useGravity,interpolation=other.interpolation });
                    other.isKinematic=true;other.interpolation=RigidbodyInterpolation.None;other.transform.SetParent(transform,true);
                }
            onPickedUp.Invoke();return true;
        }
        void FindStagedPassengers()
        {
            // Staged kinematic parts do not produce collision-stay callbacks against a kinematic bench.
            Physics.SyncTransforms();
            foreach(var surface in GetComponentsInChildren<Collider>())
            {
                if(surface.isTrigger || surface.attachedRigidbody!=body)continue;
                var bounds=surface.bounds;var center=new Vector3(bounds.center.x,bounds.max.y+.025f,bounds.center.z);
                foreach(var hit in Physics.OverlapBox(center,new Vector3(bounds.extents.x,.06f,bounds.extents.z),Quaternion.identity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                {
                    var other=hit.attachedRigidbody;
                    if(other && other!=body && Mathf.Abs(hit.bounds.min.y-bounds.max.y)<.075f)resting.Add(other);
                }
            }
        }
        public void MoveHeld(Vector3 position,Quaternion rotation)
        {
            if(!IsHeld || IsFlipping)return;
            transform.SetPositionAndRotation(position,rotation);
        }
        public void Release()
        {
            if(!IsHeld)return;
            IsHeld=false;
            foreach(var passenger in passengers)
            {
                if(!passenger.body)continue;
                passenger.body.transform.SetParent(passenger.parent,true);passenger.body.isKinematic=passenger.kinematic;passenger.body.useGravity=passenger.gravity;passenger.body.interpolation=passenger.interpolation;
            }
            passengers.Clear();body.isKinematic=lockWhenReleased;body.useGravity=!lockWhenReleased;onReleased.Invoke();
        }
        public void Flip()
        {
            Flip(GeometryCenter());
        }
        public void Flip(Vector3 center)
        {
            if(IsFlipping)return;
            flipCenter=center;flipLocalCenter=transform.InverseTransformPoint(flipCenter);
            body.isKinematic=true;flipStart=transform.rotation;flipEnd=Quaternion.AngleAxis(180,transform.right)*flipStart;
            flipTime=0;IsFlipping=true;
            groundFlip=false;Phase=TurnPhase.Turning;
        }
        public bool FlipThroughGround(Bounds bounds,float ground,float returnHeightOffset=0)
        {
            if(IsFlipping || IsHeld)return false;
            Flip(bounds.center);returnCenter=flipCenter+Vector3.up*returnHeightOffset;groundFlip=true;Phase=TurnPhase.Lowering;
            // A bounding sphere contains the complete workpiece through every
            // intermediate rotation, not just its initial and final bounds.
            undergroundCenter=new Vector3(flipCenter.x,ground-bounds.extents.magnitude-.12f,flipCenter.z);
            foreach(var collider in GetComponentsInChildren<Collider>(true)){flipColliders[collider]=collider.enabled;collider.enabled=false;}
            foreach(var other in GetComponentsInChildren<Rigidbody>(true)){flipBodies[other]=other.detectCollisions;other.detectCollisions=false;}
            return true;
        }
        public void CancelFlip()
        {
            if(!IsFlipping)return;
            transform.rotation=flipStart;transform.position+=flipCenter-transform.TransformPoint(flipLocalCenter);
            FinishFlip();
        }
        void FinishFlip()
        {
            IsFlipping=false;Phase=TurnPhase.None;groundFlip=false;
            foreach(var pair in flipBodies)if(pair.Key)pair.Key.detectCollisions=pair.Value;
            foreach(var pair in flipColliders)if(pair.Key)pair.Key.enabled=pair.Value;
            flipBodies.Clear();flipColliders.Clear();body.isKinematic=IsHeld||lockWhenReleased;
            foreach(var surface in GetComponentsInChildren<AssemblyPrecisionSurface>(true))surface.RefreshPhysics();
            Physics.SyncTransforms();
        }
        void Update()
        {
            if(!IsFlipping)return;
            if(groundFlip)
            {
                flipTime+=Time.deltaTime;Vector3 center;Quaternion rotation;
                if(flipTime<TravelDuration)
                {Phase=TurnPhase.Lowering;center=Vector3.Lerp(flipCenter,undergroundCenter,Smooth(flipTime/TravelDuration));rotation=flipStart;}
                else if(flipTime<TravelDuration+TurnDuration)
                {Phase=TurnPhase.Turning;center=undergroundCenter;rotation=Quaternion.Slerp(flipStart,flipEnd,Smooth((flipTime-TravelDuration)/TurnDuration));}
                else
                {Phase=TurnPhase.Raising;center=Vector3.Lerp(undergroundCenter,returnCenter,Smooth((flipTime-TravelDuration-TurnDuration)/TravelDuration));rotation=flipEnd;}
                transform.rotation=rotation;transform.position+=center-transform.TransformPoint(flipLocalCenter);
                if(flipTime>=TravelDuration*2+TurnDuration)FinishFlip();return;
            }
            flipTime+=Time.deltaTime;float t=Mathf.Clamp01(flipTime/Mathf.Max(.01f,flipDuration));
            transform.rotation=Quaternion.Slerp(flipStart,flipEnd,Mathf.SmoothStep(0,1,t));
            transform.position+=flipCenter-transform.TransformPoint(flipLocalCenter);
            if(t>=1)FinishFlip();
        }
        static float Smooth(float t){t=Mathf.Clamp01(t);return t*t*t*(t*(t*6-15)+10);}
        public Vector3 GeometryCenter()
        {
            TryGeometryBounds(out var bounds);return bounds.center;
        }
        public bool TryGeometryBounds(out Bounds bounds)
        {
            bool first=true;bounds=new Bounds(transform.position,Vector3.zero);
            foreach(var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.GetComponentInParent<GeneratedColliderGroup>() || renderer.gameObject.hideFlags!=HideFlags.None)continue;
                var part=renderer.GetComponentInParent<AssemblyPart>();if(part && !part.IsSnapped && part.transform!=transform)continue;
                if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);
            }
            return !first;
        }
        void OnCollisionStay(Collision collision)
        {
            var other=collision.rigidbody;
            if(!other || IsHeld)return;
            bool above=false;
            foreach(var contact in collision.contacts) if(Vector3.Dot(contact.normal,transform.up)<-.5f) { above=true;break; }
            if(above)resting.Add(other);else resting.Remove(other);
        }
        void OnCollisionExit(Collision collision) { if(collision.rigidbody)resting.Remove(collision.rigidbody); }
        void OnDisable() { Release();CancelFlip();resting.Clear(); }
    }
}

