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
        static readonly HashSet<LiftControlHandle> controls=new HashSet<LiftControlHandle>();
        Collider[] contactColliders;
        Collider visibleTarget;
        public Vector3 GrabPosition=>Kind==ControlKind.Lever && pivot?pivot.TransformPoint(new Vector3(0,0,-.145f)):transform.position;
        public Vector3 AimPosition=>Kind==ControlKind.Lever?GrabPosition:transform.position-transform.parent.forward*.035f;
        public float DistanceTo(Vector3 point){float best=float.PositiveInfinity;foreach(var c in contactColliders??GetComponentsInChildren<Collider>())if(c && c.enabled)best=Mathf.Min(best,Vector3.Distance(point,c.ClosestPoint(point)));return best;}
        public static LiftControlHandle RayControl(Ray ray,float reach)
        {
            Physics.SyncTransforms();
            // The cursor is centered on the rendered red control. Test its own
            // collider first: the projected aim point below is only a fallback
            // for near misses and can be hidden by the panel at some angles.
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
                    .Any(h=>!h.collider.transform.IsChildOf(direct.transform.parent) &&
                        !controls.Any(c=>c && c.transform.parent==direct.transform.parent &&
                            c.contactColliders!=null && c.contactColliders.Contains(h.collider)));
                if(!blocked)return direct;
            }
            // Aim at the visible control, rather than the edge of a thin mesh
            // collider. This remains usable when the panel is seen obliquely.
            var candidates=controls.Where(c=>c && c.isActiveAndEnabled)
                .Select(c=>new {control=c,along=Vector3.Dot(c.AimPosition-ray.origin,ray.direction),
                    offset=Vector3.Cross(ray.direction,c.AimPosition-ray.origin).magnitude})
                .Where(c=>c.along>0 && c.along<=reach && c.offset<=Mathf.Min(.2f,.11f+c.along*.03f))
                .OrderBy(c=>c.offset).ThenBy(c=>c.along);
            foreach(var target in candidates)
            {
                var control=target.control;var delta=control.AimPosition-ray.origin;
                bool blocked=Physics.RaycastAll(ray.origin,delta.normalized,Mathf.Max(0,delta.magnitude-.045f),~0,QueryTriggerInteraction.Ignore)
                    .Any(h=>!h.collider.transform.IsChildOf(control.transform.parent));
                if(!blocked)return control;
            }
            return null;
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
            var control=RayControl(new Ray(camera.transform.position,camera.transform.forward),distance);if(!control)return false;
            if(control.Begin() && control.Kind==ControlKind.Lever)desktop=control;
            return true;
        }
        void OnEnable()=>controls.Add(this);
        void OnDisable(){controls.Remove(this);Release();if(desktop==this)desktop=null;}
    }
}
