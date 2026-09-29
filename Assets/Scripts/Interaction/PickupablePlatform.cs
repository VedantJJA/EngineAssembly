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
        struct Passenger { public Rigidbody body; public Transform parent; public bool kinematic, gravity; }
        Quaternion flipStart,flipEnd;
        float flipTime;
        void Awake() { body=GetComponent<Rigidbody>(); body.isKinematic=lockWhenReleased; }
        public bool BeginGrab()
        {
            if(IsHeld || IsFlipping) return false;
            IsHeld=true;body.isKinematic=true;
            if(carryRestingObjects)
                foreach(var other in resting)
                {
                    if(!other || other==body || other.transform.IsChildOf(transform) || transform.IsChildOf(other.transform)) continue;
                    var part=other.GetComponent<AssemblyPart>(); if(part && (part.IsSnapped || part.IsSelected || part.IsBusy)) continue;
                    passengers.Add(new Passenger { body=other,parent=other.transform.parent,kinematic=other.isKinematic,gravity=other.useGravity });
                    other.isKinematic=true;other.transform.SetParent(transform,true);
                }
            onPickedUp.Invoke();return true;
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
                passenger.body.transform.SetParent(passenger.parent,true);passenger.body.isKinematic=passenger.kinematic;passenger.body.useGravity=passenger.gravity;
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

