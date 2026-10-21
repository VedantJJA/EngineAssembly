using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EngineAssembly
{
    // A separate physics scene tests the actual ghost meshes without adding physical obstacles.
    public sealed class AssemblyEasyGuide : MonoBehaviour
    {
        sealed class Target { public AssemblyPart part; public AssemblyVisual visual; public GameObject query; public bool isGhost,placeable; public readonly List<SurfaceSample> samples=new List<SurfaceSample>(); }
        struct SurfaceSample { public Transform frame;public Vector3 point; }
        readonly List<Target> targets = new List<Target>();
        readonly Dictionary<Collider,Target> hits = new Dictionary<Collider,Target>();
        Scene queryScene;
        PhysicsScene queryPhysics;
        AssemblyManager manager;
        readonly RaycastHit[] rayHits=new RaycastHit[512];
        public AssemblyPart LookTarget { get; private set; }
        void Awake() { manager=GetComponent<AssemblyManager>(); }
        public void RefreshTargets()
        {
            if (!manager || manager.Recipe==null) return;
            if (!queryScene.IsValid()) { queryScene=SceneManager.CreateScene("Assembly ghost queries "+GetEntityId(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));queryPhysics=queryScene.GetPhysicsScene(); }
            foreach(var obsolete in targets.Where(t=>!t.part || !manager.AssemblyParts.Contains(t.part)).ToArray())
            {
                foreach(var collider in obsolete.query.GetComponentsInChildren<Collider>(true))hits.Remove(collider);
                obsolete.query.SetActive(false);Destroy(obsolete.query);obsolete.visual.Clear();Destroy(obsolete.visual.gameObject);targets.Remove(obsolete);
            }
            bool active=manager.Interaction==WorkshopInteraction.Build && manager.Mode==WorkshopMode.Assembly && manager.Task==AssemblyTask.Assemble;
            foreach (var part in manager.AssemblyParts)
            {
                bool eligible=active && part && part.gameObject.activeInHierarchy && part.IsSelected && part.TargetSocket;
                if(eligible && manager.TargetFrame(part.Definition).GetComponentInParent<PickupablePlatform>() is PickupablePlatform platform && platform.IsFlipping)eligible=false;
                var target=targets.Find(t=>t.part==part);
                bool occluder=active && part && part.gameObject.activeInHierarchy && part.IsSnapped && !manager.IsCarriedMember(part) && HasVisibleGeometry(part);
                if (!eligible && !occluder) { if(target!=null) { target.query.SetActive(false);target.visual.Clear(); } continue; }
                if(target==null)
                {
                    target=new Target { part=part,query=new GameObject("Ghost ray geometry"),visual=new GameObject("Easy placement preview").AddComponent<AssemblyVisual>() };
                    SceneManager.MoveGameObjectToScene(target.query,queryScene);
                    foreach(var filter in part.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if(!filter.sharedMesh || !manager.OwnsMesh(part,filter))continue;
                        var child=new GameObject("Mesh query");child.transform.SetParent(target.query.transform,false);
                        var matrix=part.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                        child.transform.localPosition=matrix.GetColumn(3);child.transform.localRotation=matrix.rotation;child.transform.localScale=matrix.lossyScale;
                        var collider=AssemblyQueryGeometry.Add(child.transform,filter.sharedMesh);hits[collider]=target;
                        var vertices=filter.sharedMesh.vertices;var triangles=filter.sharedMesh.triangles;
                        int stride=Mathf.Max(3,(triangles.Length/256/3)*3);
                        for(int i=0;i+2<triangles.Length;i+=stride)target.samples.Add(new SurfaceSample{frame=child.transform,point=(vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3});
                    }
                    targets.Add(target);
                }
                target.isGhost=eligible;target.placeable=eligible && manager.CanInstall(part,part.TargetSocket,out _);target.query.SetActive(true);var pose=eligible?part.GetTargetPose(part.TargetSocket):new Pose(part.transform.position,part.transform.rotation);
                target.query.transform.SetPositionAndRotation(pose.position,pose.rotation);target.query.transform.localScale=part.transform.lossyScale;
                if(eligible)target.visual.ShowGhost(part,part.TargetSocket,target.placeable && LookTarget==part?AssemblyVisual.Ready:AssemblyVisual.Waiting,true);else target.visual.Clear();
            }
            Physics.SyncTransforms();
        }
        public AssemblyPart Aim(Ray ray,float distance,Camera camera=null)
        {
            LookTarget=null;
            if(!manager || manager.Interaction!=WorkshopInteraction.Build || manager.Mode!=WorkshopMode.Assembly)return null;
            RefreshTargets();
            return TargetWithTolerance(ray,distance,camera);
        }
        AssemblyPart TargetWithTolerance(Ray ray,float distance,Camera camera)
        {
            // CAD imports can contain inward-wound faces. Treat both sides as surfaces in
            // these synchronous selection queries, and restore the project's physics setting.
            bool backfaces=Physics.queriesHitBackfaces;
            try
            {
                Physics.queriesHitBackfaces=true;
                var direct=RayTarget(ray,distance);if(direct)return direct;
                foreach(var target in targets.Where(t=>t.isGhost && t.placeable && t.query.activeSelf))
                {
                    var colliders=target.query.GetComponentsInChildren<Collider>();if(colliders.Length==0)continue;
                    var bounds=colliders[0].bounds;foreach(var c in colliders.Skip(1))bounds.Encapsulate(c.bounds);
                    float size=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
                    // Thin seals and keeper halves need a usable aim area. Large parts still
                    // require the pointer on their visible surface.
                    if(size>.25f)continue;
                    float depth=Vector3.Dot(bounds.center-ray.origin,ray.direction.normalized);
                    if(depth<=0 || depth>distance)continue;
                    float padding=Mathf.Lerp(.025f,.006f,Mathf.Clamp01(size/.25f));
                    if(camera)
                    {
                        float pixels=Mathf.Lerp(16,5,Mathf.Clamp01(size/.25f));
                        float units=camera.orthographic?2*camera.orthographicSize/camera.pixelHeight:2*depth*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)/camera.pixelHeight;
                        padding=Mathf.Min(.035f,Mathf.Max(padding,pixels*units));
                    }
                    var expanded=bounds;expanded.Expand(padding*2);
                    if(!expanded.IntersectRay(ray,out _))continue;
                    foreach(var sample in target.samples)
                    {
                        var point=sample.frame.TransformPoint(sample.point);var delta=point-ray.origin;float along=Vector3.Dot(delta,ray.direction.normalized);
                        if(along<=0 || along>distance || (delta-ray.direction.normalized*along).sqrMagnitude>padding*padding)continue;
                        if(RayTarget(new Ray(ray.origin,delta),distance)==target.part)return LookTarget;
                    }
                    var forward=ray.direction.normalized;var right=Vector3.Cross(forward,Vector3.up).normalized;
                    if(right.sqrMagnitude<.1f)right=Vector3.Cross(forward,Vector3.right).normalized;
                    var up=Vector3.Cross(right,forward);var aim=ray.GetPoint(depth);
                    // Only accept a nearby real ghost surface that has its own clear ray.
                    // An inflated collider alone would allow placement behind a wall.
                    for(int y=-3;y<=3;y++)for(int x=-3;x<=3;x++)
                    {
                        if(x*x+y*y>10)continue;
                        var point=aim+(right*x+up*y)*(padding/3);
                        if(RayTarget(new Ray(ray.origin,point-ray.origin),distance)==target.part)return LookTarget;
                    }
                }
                LookTarget=null;return null;
            }
            finally { Physics.queriesHitBackfaces=backfaces; }
        }
        AssemblyPart RayTarget(Ray ray,float distance)
        {
            LookTarget=null;
            if(!queryPhysics.IsValid())return null;
            int count=queryPhysics.Raycast(ray.origin,ray.direction,rayHits,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            Target target=null;RaycastHit hit=default;float nearest=distance;
            for(int i=0;i<count;i++)if(rayHits[i].distance<=nearest && hits.TryGetValue(rayHits[i].collider,out var candidate) && candidate.isGhost && candidate.placeable){target=candidate;hit=rayHits[i];nearest=hit.distance;}
            if(target!=null)
            {
                // Installed CAD parts use exact meshes above; convex physics hulls can fill visible holes.
                bool blocked=false;
                var bounds=hit.collider.bounds;float size=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
                float contactTolerance=size<=.1f?Mathf.Min(.0125f,size*.4f):.001f;
                for(int i=0;i<count;i++)
                {
                    var obstacle=rayHits[i];if(obstacle.distance>=hit.distance-.0005f || !hits.TryGetValue(obstacle.collider,out var entry) || entry.isGhost)continue;
                    // Small rings/keepers overlap their mating surfaces in CAD. Permit only
                    // that shallow contact with prerequisites, never unrelated geometry.
                    if(hit.distance-obstacle.distance<=contactTolerance && IsMatingPart(target.part,entry.part,new HashSet<string>()))continue;
                    blocked=true;break;
                }
                foreach(var obstacle in Physics.RaycastAll(ray,Mathf.Max(0,hit.distance-.001f),Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                {
                    // Player and carried inventory are never world occluders. Standing and
                    // crouching must use the same visible-surface test.
                    if(obstacle.collider.GetComponentInParent<PlayerAssemblyController>())continue;
                    var installed=obstacle.collider.GetComponentInParent<AssemblyPart>();
                    if(installed && !HasVisibleGeometry(installed))continue;
                    if(installed && (installed.IsSelected || installed.IsBusy || manager.IsCarriedMember(installed)))continue;
                    if(!installed || !installed.IsSnapped) { blocked=true;break; }
                }
                if(!blocked)LookTarget=target.part;
            }
            return LookTarget;
        }
        bool IsMatingPart(AssemblyPart part,AssemblyPart obstacle,HashSet<string> visited)
        {
            if(part.Definition==null || obstacle.Definition==null || part.Definition.subAssemblyId!=obstacle.Definition.subAssemblyId || !visited.Add(part.PartId))return false;
            foreach(var id in AssemblyRecipe.Dependencies(part.Definition))
            {
                if(id==obstacle.PartId)return true;
                var prerequisite=manager.FindPart(id);if(prerequisite && IsMatingPart(prerequisite,obstacle,visited))return true;
            }
            return false;
        }
        public bool VisibleFrom(AssemblyPart part,Camera camera)
        {
            if(!part || !camera)return false;
            RefreshTargets();var target=targets.Find(t=>t.part==part && t.isGhost);
            if(target==null)return false;
            return VisibleTarget(target,part,camera);
        }
        bool VisibleTarget(Target target,AssemblyPart part,Camera camera)
        {
            // Sample projected exact-mesh bounds; accepting any visible surface avoids requiring
            // the CAD origin (which can lie outside the mesh or inside a bore) to be visible.
            foreach(var collider in target.query.GetComponentsInChildren<Collider>())
            {
                var bounds=collider.bounds;var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
                for(int i=0;i<8;i++)
                {
                    var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));var point=camera.WorldToScreenPoint(corner);
                    if(point.z<=camera.nearClipPlane)continue;min=Vector2.Min(min,point);max=Vector2.Max(max,point);
                }
                if(float.IsInfinity(min.x))continue;
                min=Vector2.Max(min,camera.pixelRect.min);max=Vector2.Min(max,camera.pixelRect.max);
                if(min.x>=max.x || min.y>=max.y)continue;
                for(int y=0;y<9;y++)for(int x=0;x<9;x++)
                {
                    var point=new Vector2(Mathf.Lerp(min.x,max.x,(x+.5f)/9),Mathf.Lerp(min.y,max.y,(y+.5f)/9));
                    if(TargetWithTolerance(camera.ScreenPointToRay(point),camera.farClipPlane,camera)==part)return true;
                }
            }
            return false;
        }
        public bool Place(Ray ray,float distance,Camera camera=null) { var part=Aim(ray,distance,camera);return part && part.IsSelected && (manager.Difficulty==SnapDifficulty.Easy?part.GuidedSnap(part.TargetSocket):part.SnapToSocket(part.TargetSocket)); }
        static bool HasVisibleGeometry(AssemblyPart part)=>part.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.enabled && r.gameObject.activeInHierarchy);
        void LateUpdate() { RefreshTargets(); }
        void OnDisable(){foreach(var t in targets){if(t.visual)t.visual.Clear();if(t.query)t.query.SetActive(false);}LookTarget=null;}
        void OnDestroy()
        {
            foreach(var t in targets)if(t.visual)Destroy(t.visual.gameObject);
            if(queryScene.IsValid() && queryScene.isLoaded)SceneManager.UnloadSceneAsync(queryScene);
        }
    }
}

