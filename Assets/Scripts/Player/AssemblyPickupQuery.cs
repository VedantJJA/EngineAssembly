using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EngineAssembly
{
    // Selection follows visible CAD surfaces, independently of convex simulation hulls.
    public sealed class AssemblyPickupQuery : MonoBehaviour
    {
        sealed class Entry { public MeshFilter source; public Renderer renderer; public MeshCollider query; public Transform frame;public AssemblyPart part; }
        readonly List<Entry> entries=new List<Entry>();
        readonly HashSet<MeshFilter> known=new HashSet<MeshFilter>();
        readonly HashSet<AssemblyPart> scanned=new HashSet<AssemblyPart>();
        readonly Dictionary<Collider,Entry> lookup=new Dictionary<Collider,Entry>();
        readonly Dictionary<AssemblyPart,Bounds> visibleBounds=new Dictionary<AssemblyPart,Bounds>();
        readonly List<SmallCandidate> smallCandidates=new List<SmallCandidate>();
        struct SmallCandidate{public AssemblyPart part;public Bounds bounds;public float margin,score;}
        public const float SmallPartSize=.14f;
        Scene scene;
        public bool Raycast(Ray ray,float distance,int mask,out AssemblyPart part,out Vector3 point,out RaycastHit environment)
        {
            Prepare(mask);return CastPrepared(ray,distance,mask,out part,out point,out environment);
        }
        void Prepare(int mask)
        {
            if(!scene.IsValid())scene=SceneManager.CreateScene("Part selection "+GetEntityId(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            foreach(var candidate in AssemblyPart.AllParts)
            {
                if(!candidate || !scanned.Add(candidate))continue;
                foreach(var filter in candidate.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(!filter.sharedMesh || filter.GetComponentInParent<AssemblyPart>()!=candidate || !known.Add(filter))continue;
                    var go=new GameObject("Selection geometry");SceneManager.MoveGameObjectToScene(go,scene);
                    var collider=AssemblyQueryGeometry.Add(go.transform,filter.sharedMesh);
                    var entry=new Entry{source=filter,renderer=filter.GetComponent<Renderer>(),query=collider,frame=go.transform,part=candidate};entries.Add(entry);lookup[collider]=entry;
                }
            }
            visibleBounds.Clear();foreach(var entry in entries)
            {
                bool visible=entry.source && entry.part && !entry.part.IsSelected && entry.renderer && entry.renderer.enabled && !entry.renderer.forceRenderingOff && entry.source.gameObject.activeInHierarchy && (mask&(1<<entry.source.gameObject.layer))!=0;
                entry.frame.gameObject.SetActive(visible);if(!visible)continue;
                entry.frame.SetPositionAndRotation(entry.source.transform.position,entry.source.transform.rotation);
                entry.frame.localScale=entry.source.transform.lossyScale;
                var bounds=entry.renderer.bounds;if(visibleBounds.TryGetValue(entry.part,out var previous)){previous.Encapsulate(bounds);visibleBounds[entry.part]=previous;}else visibleBounds.Add(entry.part,bounds);
            }
            Physics.SyncTransforms();
        }
        bool CastPrepared(Ray ray,float distance,int mask,out AssemblyPart part,out Vector3 point,out RaycastHit environment)
        {
            part=null;point=default;environment=default;
            float nearest=distance;
            bool backfaces=Physics.queriesHitBackfaces;
            try
            {
                Physics.queriesHitBackfaces=true;
                if(scene.GetPhysicsScene().Raycast(ray.origin,ray.direction,out var meshHit,distance) && lookup.TryGetValue(meshHit.collider,out var selected))
                { part=selected.part;point=meshHit.point;nearest=meshHit.distance; }
            }
            finally{Physics.queriesHitBackfaces=backfaces;}
            foreach(var hit in Physics.RaycastAll(ray,distance,mask,QueryTriggerInteraction.Ignore))
            {
                if(hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<AssemblyPart>())continue;
                if(hit.distance>=nearest)continue;
                nearest=hit.distance;environment=hit;part=null;point=hit.point;
            }
            return part || environment.collider;
        }
        public bool PickupRaycast(Ray ray,float distance,int mask,AssemblyManager manager,Camera camera,out AssemblyPart part,out Vector3 point,out RaycastHit environment,AssemblyPart stack=null)
        {
            Prepare(mask);bool direct=CastPrepared(ray,distance,mask,out part,out point,out environment);
            if(!manager || manager.Mode!=WorkshopMode.Assembly || manager.DisplayActive || manager.Difficulty!=SnapDifficulty.Easy)return direct;
            if(part && Eligible(manager,manager.InteractionPart(part),stack))return direct;
            smallCandidates.Clear();float pixelAngle=camera?2*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)/Mathf.Max(1,camera.pixelHeight)*(ExperienceMenu.MobileWorkshopRequested?22:14):.009f;
            foreach(var pair in visibleBounds)
            {
                var bounds=pair.Value;if(Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z)>SmallPartSize)continue;
                float depth=Vector3.Dot(bounds.center-ray.origin,ray.direction);if(depth<0 || depth>distance)continue;
                float margin=Mathf.Clamp(depth*pixelAngle,.008f,.04f);var projected=ray.GetPoint(depth);float miss=(bounds.ClosestPoint(projected)-projected).sqrMagnitude;if(miss>margin*margin)continue;
                var candidate=manager.InteractionPart(pair.Key);if(candidate!=pair.Key || !Eligible(manager,candidate,stack) || manager.CarrierDefinition(candidate.PartId)!=null)continue;
                smallCandidates.Add(new SmallCandidate{part=candidate,bounds=bounds,margin=margin,score=miss/(margin*margin)+depth*.0001f});
            }
            smallCandidates.Sort((a,b)=>a.score.CompareTo(b.score));
            for(int i=0;i<Mathf.Min(4,smallCandidates.Count);i++)
            {
                var candidate=smallCandidates[i];var b=candidate.bounds;var nearest=b.ClosestPoint(ray.GetPoint(Vector3.Dot(b.center-ray.origin,ray.direction)));
                for(int sample=0;sample<7;sample++)
                {
                    var aim=sample==0?nearest:sample==1?b.center:b.center+(sample==2?Vector3.right*b.extents.x:sample==3?Vector3.left*b.extents.x:sample==4?Vector3.up*b.extents.y:sample==5?Vector3.down*b.extents.y:Vector3.forward*b.extents.z)*.7f;
                    var direction=aim-ray.origin;if(direction.sqrMagnitude<.000001f)continue;
                    if(!CastPrepared(new Ray(ray.origin,direction.normalized),distance,mask,out var hit,out var contact,out _) || hit!=candidate.part)continue;
                    float along=Vector3.Dot(contact-ray.origin,ray.direction);if(along<0 || Vector3.Distance(ray.GetPoint(along),contact)>candidate.margin)continue;
                    part=hit;point=contact;environment=default;return true;
                }
            }
            return direct;
        }
        static bool Eligible(AssemblyManager manager,AssemblyPart part,AssemblyPart stack)=>part && part.Manager==manager && !part.IsSelected && !part.IsBusy && (stack?manager.CanCollect(part,stack,out _):manager.CanPickUp(part,out _));
        void OnDestroy() { if(scene.IsValid() && scene.isLoaded)SceneManager.UnloadSceneAsync(scene); }
    }
}
