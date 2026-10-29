using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EngineAssembly
{
    // A separate physics scene tests the actual ghost meshes without adding physical obstacles.
    public sealed class AssemblyEasyGuide : MonoBehaviour
    {
        sealed class Target { public AssemblyPart part; public AssemblyVisual visual; public GameObject query; public Collider[] colliders;public bool isGhost,placeable; public readonly List<SurfaceSample> samples=new List<SurfaceSample>(); }
        struct SurfaceSample { public Transform frame;public Vector3 point; }
        readonly List<Target> targets = new List<Target>();
        readonly List<AssemblyPart> carried=new List<AssemblyPart>();
        readonly Dictionary<Collider,Target> hits = new Dictionary<Collider,Target>();
        readonly Dictionary<AssemblyPart,Target> byPart=new Dictionary<AssemblyPart,Target>();
        readonly Dictionary<AssemblyPart,MeshRenderer[]> visibleGeometry=new Dictionary<AssemblyPart,MeshRenderer[]>();
        bool queriesActive;int knownCount=-1,knownVisit=-1;AssemblyRecipe knownRecipe;
        Scene queryScene;
        PhysicsScene queryPhysics;
        AssemblyManager manager;
        readonly RaycastHit[] rayHits=new RaycastHit[512],worldHits=new RaycastHit[512];
        readonly HashSet<string> matingVisited=new HashSet<string>();
        public AssemblyPart LookTarget { get; private set; }
        void Awake() { manager=GetComponent<AssemblyManager>(); }
        public void RefreshTargets()
        {
            if (!manager || manager.Recipe==null) return;
            bool active=manager.Interaction==WorkshopInteraction.Build && manager.Mode==WorkshopMode.Assembly && manager.Task==AssemblyTask.Assemble && !manager.DisplayActive;
            carried.Clear();if(active)foreach(var part in manager.AssemblyParts)if(part && part.IsSelected)carried.Add(part);
            if(carried.Count==0)
            {
                if(queriesActive){foreach(var target in targets){if(target.query)target.query.SetActive(false);target.visual.Clear();}queriesActive=false;}
                LookTarget=null;return;
            }
            queriesActive=true;
            if (!queryScene.IsValid()) { queryScene=SceneManager.CreateScene("Assembly ghost queries "+GetEntityId(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));queryPhysics=queryScene.GetPhysicsScene(); }
            if(knownCount!=manager.AssemblyParts.Count || knownVisit!=manager.ChapterVisit || knownRecipe!=manager.Recipe)for(int i=targets.Count-1;i>=0;i--)
            {
                var obsolete=targets[i];if(obsolete.part && manager.AssemblyParts.Contains(obsolete.part))continue;
                foreach(var collider in obsolete.colliders)hits.Remove(collider);
                byPart.Remove(obsolete.part);visibleGeometry.Remove(obsolete.part);obsolete.query.SetActive(false);Destroy(obsolete.query);obsolete.visual.Clear();Destroy(obsolete.visual.gameObject);targets.RemoveAt(i);
            }
            knownCount=manager.AssemblyParts.Count;knownVisit=manager.ChapterVisit;knownRecipe=manager.Recipe;bool sync=false;
            foreach (var part in manager.AssemblyParts)
            {
                bool eligible=active && part && part.gameObject.activeInHierarchy && (manager.RepairActive || part.ChapterId==manager.CurrentChapterId) && !part.IsSnapped && !part.IsBusy
                    && part.TargetSocket && part.TargetSocket.isActiveAndEnabled && MatchingHeldPart(part)
                    && (part.IsSelected || manager.CanInstall(part,part.TargetSocket,out _));
                if(eligible && manager.TargetFrame(part.Definition).GetComponentInParent<PickupablePlatform>() is PickupablePlatform platform && platform.IsFlipping)eligible=false;
                if(!part)continue;byPart.TryGetValue(part,out var target);
                bool occluder=active && part && part.gameObject.activeInHierarchy && part.IsSnapped && !manager.IsCarriedMember(part) && HasVisibleGeometry(part);
                if (!eligible && !occluder) { if(target!=null) { if(target.query.activeSelf){target.query.SetActive(false);sync=true;}target.visual.Clear(); } continue; }
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
                    target.colliders=target.query.GetComponentsInChildren<Collider>(true);targets.Add(target);byPart[part]=target;sync=true;
                }
                target.isGhost=eligible;target.placeable=eligible && manager.CanInstall(part,part.TargetSocket,out _);if(!target.query.activeSelf){target.query.SetActive(true);sync=true;}var pose=eligible?part.GetTargetPose(part.TargetSocket):new Pose(part.transform.position,part.transform.rotation);
                var frame=target.query.transform;if(frame.position!=pose.position || frame.rotation!=pose.rotation){frame.SetPositionAndRotation(pose.position,pose.rotation);sync=true;}var scale=part.transform.lossyScale;if(frame.localScale!=scale){frame.localScale=scale;sync=true;}
                if(eligible)target.visual.ShowGhost(part,part.TargetSocket,target.placeable && LookTarget==part?AssemblyVisual.Ready:AssemblyVisual.Waiting,true);else target.visual.Clear();
            }
            if(sync)Physics.SyncTransforms();
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
                foreach(var target in targets)
                {
                    if(!target.isGhost || !target.placeable || !target.query.activeSelf)continue;
                    var colliders=target.colliders;if(colliders.Length==0)continue;
                    var bounds=colliders[0].bounds;for(int i=1;i<colliders.Length;i++)bounds.Encapsulate(colliders[i].bounds);
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
                    if(hit.distance-obstacle.distance<=contactTolerance){matingVisited.Clear();if(IsMatingPart(target.part,entry.part,matingVisited))continue;}
                    blocked=true;break;
                }
                int worldCount=Physics.RaycastNonAlloc(ray,worldHits,Mathf.Max(0,hit.distance-.001f),Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                var obstacles=worldCount==worldHits.Length?Physics.RaycastAll(ray,Mathf.Max(0,hit.distance-.001f),Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore):worldHits;
                if(obstacles!=worldHits)worldCount=obstacles.Length;
                for(int i=0;i<worldCount;i++)
                {
                    var obstacle=obstacles[i];
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
            RefreshTargets();byPart.TryGetValue(part,out var target);
            if(target==null || !target.isGhost)return false;
            return VisibleTarget(target,part,camera);
        }
        bool VisibleTarget(Target target,AssemblyPart part,Camera camera)
        {
            // Sample projected exact-mesh bounds; accepting any visible surface avoids requiring
            // the CAD origin (which can lie outside the mesh or inside a bore) to be visible.
            foreach(var collider in target.colliders)
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
        public AssemblyPart MatchingHeldPart(AssemblyPart target)
        {
            if(target && target.IsSelected)return target;
            if(!target || manager.RepairActive)return null;
            foreach(var item in carried)if(item && item.IsSelected && AssemblyInventory.Matches(item,target))return item;
            return null;
        }
        public bool Place(Ray ray,float distance,Camera camera=null,AssemblyPart held=null)
        {
            var target=Aim(ray,distance,camera);if(!target)return false;
            return AssemblyEquivalentPlacement.Place(held?held:MatchingHeldPart(target),target);
        }
        bool HasVisibleGeometry(AssemblyPart part)
        {
            if(!visibleGeometry.TryGetValue(part,out var meshes)){meshes=part.GetComponentsInChildren<MeshRenderer>(true);visibleGeometry[part]=meshes;}
            foreach(var mesh in meshes)if(mesh && mesh.enabled && mesh.gameObject.activeInHierarchy)return true;return false;
        }
        void LateUpdate() { RefreshTargets(); }
        void OnDisable(){foreach(var t in targets){if(t.visual)t.visual.Clear();if(t.query)t.query.SetActive(false);}LookTarget=null;queriesActive=false;}
        void OnDestroy()
        {
            foreach(var t in targets)if(t.visual)Destroy(t.visual.gameObject);
            if(queryScene.IsValid() && queryScene.isLoaded)SceneManager.UnloadSceneAsync(queryScene);
        }
    }
}

