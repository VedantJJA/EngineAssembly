using UnityEngine;
using System.Collections.Generic;
namespace EngineAssembly
{
    // Runtime carrier: the original, individually serviceable parts stay intact as children.
    public class SubAssembly : MonoBehaviour
    {
        [SerializeField] string subAssemblyName;
        public string SubAssemblyName=>string.IsNullOrEmpty(subAssemblyName)?name:subAssemblyName;
        public AssemblyPart[] ChildParts=>GetComponentsInChildren<AssemblyPart>(true);
        public AssemblyPart Carrier=>GetComponent<AssemblyPart>();
        public bool Ready=>Carrier && Carrier.Manager && Carrier.Manager.GroupComplete(Carrier.Manager.CarrierDefinition(Carrier.PartId));
        readonly Dictionary<Collider,bool> surfaces=new Dictionary<Collider,bool>();
        readonly Dictionary<Rigidbody,bool> collisionStates=new Dictionary<Rigidbody,bool>();
        public bool UsesHandlingPhysics { get; private set; }
        public void RefreshPhysics()
        {
            var carrier=Carrier;if(!carrier || !carrier.Manager)return;
            var group=carrier.Manager.CarrierDefinition(carrier.PartId);if(group==null)return;
            bool detached=Ready && !carrier.IsSnapped;
            if(detached==UsesHandlingPhysics)return;
            UsesHandlingPhysics=detached;
            if(!UsesHandlingPhysics){RestorePhysics();return;}
            // The carrier's convex handling box is the single simulated body. Nested
            // kinematic member bodies must not collide against that box on release.
            foreach(var member in carrier.Manager.Members(group))
            {
                var body=member.Body;
                if(!collisionStates.ContainsKey(body))collisionStates.Add(body,body.detectCollisions);
                body.detectCollisions=false;
                foreach(var collider in member.GetComponentsInChildren<Collider>(true))
                {
                    if(collider.attachedRigidbody!=body)continue;
                    if(!surfaces.ContainsKey(collider))surfaces.Add(collider,collider.enabled);
                    collider.enabled=false;
                }
            }
            carrier.Body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
        }
        void RestorePhysics()
        {
            UsesHandlingPhysics=false;
            foreach(var pair in collisionStates)if(pair.Key)pair.Key.detectCollisions=pair.Value;
            foreach(var pair in surfaces)if(pair.Key)pair.Key.enabled=pair.Value;
            collisionStates.Clear();surfaces.Clear();
            if(Carrier && Carrier.Manager)
            {
                if(!Ready && !Carrier.IsSelected && !Carrier.IsBusy && !Carrier.IsSnapped){Carrier.Body.isKinematic=true;Carrier.Body.useGravity=false;}
                var group=Carrier.Manager.CarrierDefinition(Carrier.PartId);
                if(group!=null)foreach(var member in Carrier.Manager.Members(group))member.GetComponent<AssemblyPrecisionSurface>()?.RefreshPhysics();
            }
        }
        void LateUpdate()=>RefreshPhysics();
        void OnDisable()=>RestorePhysics();
    }
}

