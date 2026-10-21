using UnityEngine;

namespace EngineAssembly
{
    public static class AssemblyPresentationBounds
    {
        // Include installed meshes while the AR content is hidden for placement.
        // Exclude source pieces, generated collision hulls and carried members.
        public static Bounds World(Transform frame,AssemblyManager manager=null)
        {
            bool first=true;var local=new Bounds(Vector3.zero,Vector3.zero);
            foreach(var mesh in frame.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!mesh.sharedMesh || !mesh.GetComponent<MeshRenderer>() || mesh.GetComponentInParent<GeneratedColliderGroup>() || mesh.gameObject.hideFlags!=HideFlags.None)continue;
                var part=mesh.GetComponentInParent<AssemblyPart>();
                if(manager && (!part || !part.IsSnapped || manager.IsCarriedMember(part)))continue;
                var b=mesh.sharedMesh.bounds;var matrix=frame.worldToLocalMatrix*mesh.transform.localToWorldMatrix;
                for(int i=0;i<8;i++){var point=matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(first){local=new Bounds(point,Vector3.zero);first=false;}else local.Encapsulate(point);}
            }
            if(first && manager)
            {
                var carrier=frame.GetComponentsInChildren<AssemblyPart>(true);
                foreach(var part in carrier)if(manager.CarrierDefinition(part.PartId)!=null)return manager.WorkpieceBounds(part.transform);
                return manager.WorkpieceBounds(frame);
            }
            if(first)return new Bounds(frame.position,Vector3.zero);
            var world=new Bounds(frame.TransformPoint(local.center),Vector3.zero);
            for(int i=0;i<8;i++)world.Encapsulate(frame.TransformPoint(local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
            return world;
        }
    }
}
