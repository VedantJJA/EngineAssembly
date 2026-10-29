using System.Collections.Generic;
using UnityEngine;

namespace EngineAssembly
{
    public sealed class DisplaySceneControl:MonoBehaviour
    {
        static readonly HashSet<DisplaySceneControl> controls=new HashSet<DisplaySceneControl>();
        EngineDisplayStation station;Collider surface;bool explanation;
        public Vector3 AimPosition=>surface.bounds.center;
        public bool Available=>isActiveAndEnabled && gameObject.activeInHierarchy && station && station.Available && (!explanation || station.MotionMode);
        public void Configure(EngineDisplayStation owner,Collider visibleSurface,bool explain){station=owner;surface=visibleSurface;explanation=explain;}
        public bool Activate(){if(!Available)return false;station.Activate(explanation);return true;}
        public float DistanceTo(Vector3 point)=>surface?Vector3.Distance(point,surface.ClosestPoint(point)):float.PositiveInfinity;
        public bool InFront(Vector3 point)=>Vector3.Dot(point-transform.position,-station.FaceForward)>-.012f;
        public static DisplaySceneControl Nearest(Vector3 point,float reach)
        {
            DisplaySceneControl best=null;foreach(var control in controls)if(control && control.Available && control.InFront(point)){float distance=control.DistanceTo(point);if(distance<reach){best=control;reach=distance;}}return best;
        }
        public static DisplaySceneControl RayControl(Ray ray,float reach)
        {
            Physics.SyncTransforms();DisplaySceneControl best=null;
            foreach(var control in controls)
            {
                if(!control || !control.Available || !control.surface || !control.InFront(ray.origin) || !control.surface.Raycast(ray,out var hit,reach))continue;
                bool blocked=false;foreach(var obstacle in Physics.RaycastAll(ray,Mathf.Max(0,hit.distance-.004f),~0,QueryTriggerInteraction.Ignore))
                {
                    if(obstacle.collider is CharacterController || obstacle.collider.GetComponentInParent<PlayerAssemblyController>() || obstacle.collider.GetComponentInParent<QuestVRRig>() || obstacle.collider.transform.IsChildOf(control.station.transform))continue;
                    blocked=true;break;
                }
                if(!blocked){best=control;reach=hit.distance;}
            }
            return best;
        }
        void OnEnable()=>controls.Add(this);
        void OnDisable()=>controls.Remove(this);
    }
}
