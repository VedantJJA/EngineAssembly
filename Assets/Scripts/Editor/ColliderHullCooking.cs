using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace EngineAssembly.Editor
{
    public static class ColliderHullCooking
    {
        public const MeshColliderCookingOptions Options=MeshColliderCookingOptions.CookForFasterSimulation|MeshColliderCookingOptions.EnableMeshCleaning|MeshColliderCookingOptions.WeldColocatedVertices|MeshColliderCookingOptions.UseFastMidphase;
        public static ColliderHullSet.Hull Prepare(Mesh source,string name)
        {
            var vertices=source.vertices;var indices=source.triangles;
            if(vertices.Length<4 || indices.Length<12 || indices.Length/3>255)throw new InvalidOperationException("Invalid hull size: "+name);
            var bounds=source.bounds;float scale=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            if(!float.IsFinite(scale) || scale<=0)throw new InvalidOperationException("Zero-volume hull: "+name);
            var unique=new List<Vector3>();var map=new Dictionary<Vector3,int>();var remap=new int[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                var point=(vertices[i]-bounds.center)/scale;
                if(!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z))throw new InvalidOperationException("Invalid hull coordinates: "+name);
                if(!map.TryGetValue(point,out int index)){index=unique.Count;map.Add(point,index);unique.Add(point);}remap[i]=index;
            }
            var triangles=new List<int>();
            if(!HasVolume(unique))return null; // backend remeshing sometimes emits purely planar scraps
            for(int i=0;i<indices.Length;i+=3)
            {
                if(indices[i]<0 || indices[i+1]<0 || indices[i+2]<0 || indices[i]>=vertices.Length || indices[i+1]>=vertices.Length || indices[i+2]>=vertices.Length)throw new InvalidOperationException("Invalid hull indices: "+name);
                int a=remap[indices[i]],b=remap[indices[i+1]],c=remap[indices[i+2]];
                if(a==b || b==c || a==c || Vector3.Cross(unique[b]-unique[a],unique[c]-unique[a]).sqrMagnitude<1e-18f)continue;
                triangles.Add(a);triangles.Add(b);triangles.Add(c);
            }
            if(unique.Count<4 || triangles.Count<12)throw new InvalidOperationException("Hull has no solid surface: "+name);
            var mesh=new Mesh{name=name,vertices=unique.ToArray(),triangles=triangles.ToArray()};mesh.RecalculateBounds();mesh.RecalculateNormals();
            string error="";Application.LogCallback capture=(message,stack,type)=>{if(type==LogType.Error || type==LogType.Exception)error+=message+"\n";};
            Application.logMessageReceived+=capture;
            try{Physics.BakeMesh(mesh.GetEntityId(),true,Options);}
            finally{Application.logMessageReceived-=capture;}
            if(error.Length>0){UnityEngine.Object.DestroyImmediate(mesh);throw new InvalidOperationException("PhysX rejected normalized hull "+name+": "+error);}
            return new ColliderHullSet.Hull{mesh=mesh,center=bounds.center,scale=scale};
        }
        static bool HasVolume(List<Vector3> points)
        {
            if(points.Count<4)return false;var a=points[0];var b=points.OrderByDescending(p=>(p-a).sqrMagnitude).First();
            var axis=b-a;var c=points.OrderByDescending(p=>Vector3.Cross(axis,p-a).sqrMagnitude).First();var normal=Vector3.Cross(axis,c-a).normalized;
            return normal.sqrMagnitude>0 && points.Max(p=>Mathf.Abs(Vector3.Dot(normal,p-a)))>1e-6f;
        }
        public static MeshCollider Add(Transform parent,ColliderHullSet.Hull hull,bool undo=false)
        {
            var child=new GameObject(hull.mesh.name);if(undo)UnityEditor.Undo.RegisterCreatedObjectUndo(child,"Create validated hull");
            child.transform.SetParent(parent,false);child.transform.localPosition=hull.center;child.transform.localScale=Vector3.one*hull.scale;
            var collider=undo?UnityEditor.Undo.AddComponent<MeshCollider>(child):child.AddComponent<MeshCollider>();
            collider.cookingOptions=Options;collider.convex=true;collider.sharedMesh=hull.mesh;collider.contactOffset=.001f;return collider;
        }
    }
}
