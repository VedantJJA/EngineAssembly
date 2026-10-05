using UnityEngine;
namespace EngineAssembly
{
    public static class AssemblyHeldView
    {
        public static Bounds WorldBounds(AssemblyPart part)
        {
            bool first=true;var result=new Bounds(part.transform.position,Vector3.zero);
            foreach(var filter in part.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.sharedMesh || !(part.Manager?part.Manager.OwnsMesh(part,filter):filter.GetComponentInParent<AssemblyPart>()==part))continue;
                var b=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++) { var p=filter.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p); }
            }
            return result;
        }
    }
}

