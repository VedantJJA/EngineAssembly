using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace EngineAssembly
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public class PickupablePlatform : MonoBehaviour
    {
        [SerializeField] bool lockWhenReleased=true;
        [SerializeField] bool carryRestingObjects=true;
        [SerializeField] float flipDuration=.45f;
        public UnityEvent onPickedUp=new UnityEvent(),onReleased=new UnityEvent();
        public bool IsHeld { get; private set; }
        public bool IsFlipping { get; private set; }
        Rigidbody body;
        readonly HashSet<Rigidbody> resting=new HashSet<Rigidbody>();
        readonly List<Passenger> passengers=new List<Passenger>();
        struct Passenger { public Rigidbody body; public Transform parent; public bool kinematic, gravity; public RigidbodyInterpolation interpolation; }
        Quaternion flipStart,flipEnd;
        float flipTime;
        void Awake() { body=GetComponent<Rigidbody>(); body.isKinematic=lockWhenReleased; }
        public bool BeginGrab()
        {
            if(IsHeld || IsFlipping) return false;
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
            if(IsFlipping)return;
            body.isKinematic=true;flipStart=transform.rotation;flipEnd=Quaternion.AngleAxis(180,transform.right)*flipStart;
            flipTime=0;IsFlipping=true;
        }
        void Update()
        {
            if(!IsFlipping)return;
            flipTime+=Time.deltaTime;float t=Mathf.Clamp01(flipTime/Mathf.Max(.01f,flipDuration));
            transform.rotation=Quaternion.Slerp(flipStart,flipEnd,Mathf.SmoothStep(0,1,t));
            if(t>=1) { IsFlipping=false;body.isKinematic=IsHeld||lockWhenReleased; }
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
        void OnDisable() { Release();IsFlipping=false;resting.Clear(); }
    }
}

