using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EngineAssembly
{
    // A separate physics scene tests the actual ghost meshes without adding physical obstacles.
    public sealed class AssemblyEasyGuide : MonoBehaviour
    {
        sealed class Target { public AssemblyPart part; public AssemblyVisual visual; public GameObject query; public bool isGhost; }
        readonly List<Target> targets = new List<Target>();
        readonly Dictionary<Collider,Target> hits = new Dictionary<Collider,Target>();
        Scene queryScene;
        PhysicsScene queryPhysics;
        AssemblyManager manager;
        public AssemblyPart LookTarget { get; private set; }
        void Awake() { manager=GetComponent<AssemblyManager>(); }
        public void RefreshTargets()
        {
            if (!manager || manager.Recipe==null) return;
            if (!queryScene.IsValid()) { queryScene=SceneManager.CreateScene("Assembly ghost queries "+GetEntityId(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));queryPhysics=queryScene.GetPhysicsScene(); }
            bool active=manager.Interaction==WorkshopInteraction.Build && manager.Mode==WorkshopMode.Assembly && manager.Task==AssemblyTask.Assemble;
            foreach (var part in manager.AssemblyParts)
            {
                bool eligible=active && part && part.gameObject.activeInHierarchy && part.IsSelected && part.TargetSocket && manager.CanInstall(part,part.TargetSocket,out _);
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
                        var collider=child.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;hits[collider]=target;
                    }
                    targets.Add(target);
                }
                target.isGhost=eligible;target.query.SetActive(true);var pose=eligible?part.GetTargetPose(part.TargetSocket):new Pose(part.transform.position,part.transform.rotation);
                target.query.transform.SetPositionAndRotation(pose.position,pose.rotation);target.query.transform.localScale=part.transform.lossyScale;
                if(eligible && manager.Difficulty==SnapDifficulty.Easy)target.visual.ShowGhost(part,part.TargetSocket,LookTarget==part?AssemblyVisual.Ready:AssemblyVisual.Waiting,true);else target.visual.Clear();
            }
            Physics.SyncTransforms();
        }
        public AssemblyPart Aim(Ray ray,float distance)
        {
            LookTarget=null;
            if(!manager || manager.Interaction!=WorkshopInteraction.Build || manager.Mode!=WorkshopMode.Assembly)return null;
            RefreshTargets();
            return RayTarget(ray,distance);
        }
        AssemblyPart RayTarget(Ray ray,float distance)
        {
            LookTarget=null;
            if(queryPhysics.IsValid() && queryPhysics.Raycast(ray.origin,ray.direction,out var hit,distance) && hits.TryGetValue(hit.collider,out var target) && target.isGhost)
            {
                // Installed CAD parts use exact meshes above; convex physics hulls can fill visible holes.
                bool blocked=false;
                foreach(var obstacle in Physics.RaycastAll(ray,Mathf.Max(0,hit.distance-.025f),Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                {
                    var installed=obstacle.collider.GetComponentInParent<AssemblyPart>();
                    if(installed && !HasVisibleGeometry(installed))continue;
                    if(installed && (installed==target.part && installed.IsSelected || manager.IsCarriedMember(installed)))continue;
                    if(!installed || !installed.IsSnapped) { blocked=true;break; }
                }
                if(!blocked)LookTarget=target.part;
            }
            return LookTarget;
        }
        public bool VisibleFrom(AssemblyPart part,Camera camera)
        {
            if(!part || !camera)return false;
            RefreshTargets();var target=targets.Find(t=>t.part==part && t.isGhost);
            if(target==null)return false;
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
                    if(RayTarget(camera.ScreenPointToRay(point),camera.farClipPlane)==part)return true;
                }
            }
            return false;
        }
        public bool Place(Ray ray,float distance) { var part=Aim(ray,distance);return part && part.IsSelected && part.GuidedSnap(part.TargetSocket); }
        static bool HasVisibleGeometry(AssemblyPart part)=>part.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.enabled && r.gameObject.activeInHierarchy);
        void LateUpdate() { RefreshTargets(); }
        void OnDestroy()
        {
            foreach(var t in targets)if(t.visual)Destroy(t.visual.gameObject);
            if(queryScene.IsValid() && queryScene.isLoaded)SceneManager.UnloadSceneAsync(queryScene);
        }
    }
}

