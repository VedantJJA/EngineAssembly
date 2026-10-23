using UnityEngine;
using System.Collections.Generic;
using System.Linq;
namespace EngineAssembly
{
    public sealed class LiftControlHandle : MonoBehaviour
    {
        public enum ControlKind { Lever, Flip }
        public ControlKind Kind { get; private set; }
        public float Value { get; private set; }
        public bool Held { get; private set; }
        WorkpieceLift lift;int station;Transform pivot;Vector3 restPosition;float pressedUntil;
        static LiftControlHandle desktop;
        public static bool DesktopHolding=>desktop;
        static readonly HashSet<LiftControlHandle> controls=new HashSet<LiftControlHandle>();
        Collider[] contactColliders;
        Collider visibleTarget;
        public Vector3 GrabPosition=>Kind==ControlKind.Lever && pivot?pivot.TransformPoint(new Vector3(0,0,-.145f)):transform.position;
        public Vector3 AimPosition=>visibleTarget?visibleTarget.bounds.center:GrabPosition;
        public float DistanceTo(Vector3 point){float best=float.PositiveInfinity;foreach(var c in contactColliders??GetComponentsInChildren<Collider>())if(c && c.enabled)best=Mathf.Min(best,Vector3.Distance(point,c.ClosestPoint(point)));return best;}
        public static LiftControlHandle RayControl(Ray ray,float reach)
        {
            Physics.SyncTransforms();
            // Select only the rendered red surface, never a projected point or
            // the larger invisible volume used for physical VR grabbing.
            LiftControlHandle direct=null;float directDistance=reach;
            foreach(var control in controls)
            {
                if(!control || !control.isActiveAndEnabled || !control.visibleTarget ||
                   !control.visibleTarget.enabled || !control.visibleTarget.gameObject.activeInHierarchy)continue;
                if(control.visibleTarget.Raycast(ray,out var hit,reach) && hit.distance<directDistance)
                {direct=control;directDistance=hit.distance;}
            }
            if(direct)
            {
                // Only scenery in front of the red surface can hide it. The
                // panel's enlarged grab colliders must never block its own red
                // controls, and a neighboring control cannot steal this click.
                bool blocked=Physics.RaycastAll(ray,Mathf.Max(0,directDistance-.005f),~0,QueryTriggerInteraction.Ignore)
                    .Any(h=>BlocksControl(h.collider));
                if(!blocked)return direct;
            }
            return null;
        }
        static bool BlocksControl(Collider collider)
        {
            // A standing camera can sit above its capsule. Its downward ray then
            // crosses its own body; crouching happened to hide this self-hit.
            if(collider is CharacterController || collider.GetComponentInParent<PlayerAssemblyController>())return false;
            var part=collider.GetComponentInParent<AssemblyPart>();if(part && part.IsSelected)return false;
            // Invisible VR grab volumes are not scenery. Actual panel housing
            // and shafts still occlude a red surface when viewed from behind.
            if(collider.GetComponentInParent<LiftControlHandle>() && !collider.GetComponent<Renderer>())return false;
            return true;
        }
        public static LiftControlHandle Nearest(Vector3 point,float reach,ControlKind kind)
        {
            LiftControlHandle nearest=null;float distance=reach;
            foreach(var control in controls)
            {
                if(!control || !control.isActiveAndEnabled || control.Kind!=kind)continue;
                float next=control.DistanceTo(point);if(next<distance){nearest=control;distance=next;}
            }
            return nearest;
        }
        public void Configure(WorkpieceLift owner,int index,ControlKind kind,Transform movingPivot=null,Collider redCollider=null){lift=owner;station=index;Kind=kind;pivot=movingPivot;visibleTarget=redCollider;restPosition=transform.localPosition;RefreshColliders();}
        public void RefreshColliders()=>contactColliders=GetComponentsInChildren<Collider>();
        public bool Begin()
        {
            if(!lift || Held || !lift.CanOperate(station))return false;
            if(Kind==ControlKind.Flip){bool started=lift.FlipStation(station);if(started)pressedUntil=Time.unscaledTime+.2f;return started;}
            Held=true;Value=0;return true;
        }
        public void SetValue(float value){if(Held)Value=Mathf.Clamp(value,-1,1);}
        public void Release(){Held=false;Value=0;}
        void Update()
        {
            if(Held && Time.timeScale>0)lift.MoveStationFromInput(station,Value);
            if(pivot)pivot.localRotation=Quaternion.Slerp(pivot.localRotation,Quaternion.Euler(Value*35,0,0),1-Mathf.Exp(-18*Time.unscaledDeltaTime));
            if(Kind==ControlKind.Flip)transform.localPosition=Vector3.Lerp(transform.localPosition,restPosition+Vector3.forward*(Time.unscaledTime<pressedUntil?.012f:0),1-Mathf.Exp(-25*Time.unscaledDeltaTime));
        }
        public static bool ProcessDesktop(Camera camera,bool down,bool held,float deltaY,float distance)
        {
            if(desktop)
            {
                if(!held){desktop.Release();desktop=null;return true;}
                desktop.SetValue(desktop.Value+deltaY*.035f);return true;
            }
            if(!down || !camera)return false;
            var control=RayControl(camera.ViewportPointToRay(new Vector3(.5f,.5f,0)),distance);if(!control)return false;
            if(control.Begin() && control.Kind==ControlKind.Lever)desktop=control;
            return true;
        }
        void OnEnable()=>controls.Add(this);
        void OnDisable(){controls.Remove(this);Release();if(desktop==this)desktop=null;}
    }
}
