using UnityEngine;

namespace EngineAssembly
{
    /// <summary>Use the CAD surface while fixed or carried; convex hulls when loose and dynamic.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class AssemblyPrecisionSurface : MonoBehaviour
    {
        [SerializeField] MeshCollider surface;
        GeneratedColliderGroup group;
        Collider[] hulls;
        MeshRenderer source;
        Rigidbody body;
        AssemblyPart part;
        bool hasState,lastVisible,lastPrecise;
        public void Configure(MeshCollider collider) { surface=collider;source=GetComponent<MeshRenderer>();Refresh(); }
        void Awake() { source=GetComponent<MeshRenderer>();Refresh(); }
        void OnEnable()=>Refresh();
        void LateUpdate()=>Refresh();
        public void RefreshPhysics()=>Refresh();
        public void PreparePhysics(bool kinematic)
        {
            if(!kinematic && surface)surface.enabled=false;
        }
        void Refresh()
        {
            if(!surface)return;
            if(!source)source=GetComponent<MeshRenderer>();
            if(!part)part=GetComponentInParent<AssemblyPart>();
            body=GetComponentInParent<Rigidbody>();
            if(!group)
            {
                group=GetComponentInChildren<GeneratedColliderGroup>(true);
                hulls=group?group.GetComponentsInChildren<Collider>(true):new Collider[0];
            }
            // The display has independent exact selection meshes. Keeping the
            // simulation surfaces active while hundreds of parts move makes
            // PhysX rebuild their broadphase unnecessarily on standalone XR.
            if(part && part.Manager && part.Manager.DisplayActive)
            {
                if(surface.enabled)surface.enabled=false;
                if(hulls!=null)foreach(var hull in hulls)if(hull && hull.enabled)hull.enabled=false;
                hasState=false;return;
            }
            bool visible=source && source.enabled && source.gameObject.activeInHierarchy && (!body || body.detectCollisions);
            bool precise=visible && (!body || body.isKinematic);
            if(hasState && lastVisible==visible && lastPrecise==precise && surface.enabled==precise)return;
            if(surface.enabled!=precise)surface.enabled=precise;
            if(hulls!=null)foreach(var hull in hulls)if(hull && hull.enabled!=(visible && !precise))hull.enabled=visible && !precise;
            hasState=true;lastVisible=visible;lastPrecise=precise;
        }
    }
}
