using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EngineAssembly
{
    // Selection follows visible CAD surfaces, independently of convex simulation hulls.
    public sealed class AssemblyPickupQuery : MonoBehaviour
    {
        sealed class Entry { public MeshFilter source; public Renderer renderer; public MeshCollider query; public AssemblyPart part; }
        readonly List<Entry> entries=new List<Entry>();
        readonly HashSet<MeshFilter> known=new HashSet<MeshFilter>();
        readonly HashSet<AssemblyPart> scanned=new HashSet<AssemblyPart>();
        readonly Dictionary<Collider,Entry> lookup=new Dictionary<Collider,Entry>();
        Scene scene;
        public bool Raycast(Ray ray,float distance,int mask,out AssemblyPart part,out Vector3 point,out RaycastHit environment)
        {
            part=null;point=default;environment=default;
            if(!scene.IsValid())scene=SceneManager.CreateScene("Part selection "+GetEntityId(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            foreach(var candidate in AssemblyPart.AllParts)
            {
                if(!candidate || !scanned.Add(candidate))continue;
                foreach(var filter in candidate.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(!filter.sharedMesh || filter.GetComponentInParent<AssemblyPart>()!=candidate || !known.Add(filter))continue;
                    var go=new GameObject("Selection geometry");SceneManager.MoveGameObjectToScene(go,scene);
                    var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;
                    var entry=new Entry{source=filter,renderer=filter.GetComponent<Renderer>(),query=collider,part=candidate};entries.Add(entry);lookup[collider]=entry;
                }
            }
            foreach(var entry in entries)
            {
                bool visible=entry.source && entry.part && !entry.part.IsSelected && entry.renderer && entry.renderer.enabled && entry.source.gameObject.activeInHierarchy && (mask&(1<<entry.source.gameObject.layer))!=0;
                entry.query.gameObject.SetActive(visible);if(!visible)continue;
                entry.query.transform.SetPositionAndRotation(entry.source.transform.position,entry.source.transform.rotation);
                entry.query.transform.localScale=entry.source.transform.lossyScale;
            }
            Physics.SyncTransforms();
            float nearest=distance;
            if(scene.GetPhysicsScene().Raycast(ray.origin,ray.direction,out var meshHit,distance) && lookup.TryGetValue(meshHit.collider,out var selected))
            { part=selected.part;point=meshHit.point;nearest=meshHit.distance; }
            foreach(var hit in Physics.RaycastAll(ray,distance,mask,QueryTriggerInteraction.Ignore))
            {
                if(hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<AssemblyPart>())continue;
                if(hit.distance>=nearest)continue;
                nearest=hit.distance;environment=hit;part=null;point=hit.point;
            }
            return part || environment.collider;
        }
        void OnDestroy() { if(scene.IsValid() && scene.isLoaded)SceneManager.UnloadSceneAsync(scene); }
    }
}
