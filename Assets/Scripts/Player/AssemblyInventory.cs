using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace EngineAssembly
{
    // One stack retains the actual CAD identities. Placement uses the selected socket's
    // own part, never assigns a screw or spring to a different recipe ID.
    public sealed class AssemblyInventory : MonoBehaviour
    {
        readonly List<AssemblyPart> items=new List<AssemblyPart>();
        readonly Dictionary<Renderer,bool> hidden=new Dictionary<Renderer,bool>();
        static readonly Dictionary<AssemblyPart,float[]> shapes=new Dictionary<AssemblyPart,float[]>();
        public IReadOnlyList<AssemblyPart> Items=>items;
        public int Count=>items.Count;
        public bool Contains(AssemblyPart part)=>items.Contains(part);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset()=>shapes.Clear();
        public static string TypeName(AssemblyPart part)
        {
            var name=part.PartId ?? part.name;
            name=Regex.Replace(name,@"^\d{3}_","");
            name=Regex.Replace(name,@"_Cyl\d+.*$","");
            // A fastener's last number before its instance suffix is its size,
            // e.g. M8_x_35. Never strip that specification as an instance number.
            if(Regex.IsMatch(name,@"_(Fasteners|Valves)_\d+$"))name=Regex.Replace(name,@"_(Fasteners|Valves)_\d+$","");
            else name=Regex.Replace(name,@"_\d+$","");
            return name.Replace('_',' ');
        }
        public static bool Matches(AssemblyPart a,AssemblyPart b)
        {
            if(!a || !b || a.Manager!=b.Manager || TypeName(a)!=TypeName(b))return false;
            if(a.Manager && (a.Manager.CarrierDefinition(a.PartId)!=null || a.Manager.CarrierDefinition(b.PartId)!=null))return a==b;
            var x=Shape(a);var y=Shape(b);if(x.Length!=y.Length || x.Length==0)return false;
            for(int i=0;i<x.Length;i++)if(Mathf.Abs(x[i]-y[i])>Mathf.Max(.00002f,Mathf.Max(x[i],y[i])*.002f))return false;
            return true;
        }
        static float[] Shape(AssemblyPart part)
        {
            if(shapes.TryGetValue(part,out var saved))return saved;
            var points=new List<Vector3>();var edges=new List<float>();float area=0;
            foreach(var f in part.GetComponentsInChildren<MeshFilter>(true))
                if(f.sharedMesh && (part.Manager?part.Manager.OwnsMesh(part,f):f.GetComponentInParent<AssemblyPart>()==part))
                {
                    var vertices=f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v)-part.transform.position).ToArray();points.AddRange(vertices);
                    var triangles=f.sharedMesh.triangles;
                    for(int i=0;i+2<triangles.Length;i+=3)
                    {
                        var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];
                        edges.Add((a-b).magnitude);edges.Add((b-c).magnitude);edges.Add((c-a).magnitude);
                        area+=Vector3.Cross(b-a,c-a).magnitude*.5f;
                    }
                }
            if(points.Count==0)return shapes[part]=Array.Empty<float>();
            var center=points.Aggregate(Vector3.zero,(sum,p)=>sum+p)/points.Count;
            var radii=points.Select(p=>(p-center).magnitude).OrderBy(v=>v).ToArray();
            // Radial extent alone can confuse different shapes. Include surface area
            // and edge distribution, retaining invariance under CAD instance flips.
            var result=new float[131];result[0]=radii.Average();
            for(int i=0;i<64;i++)result[i+1]=radii[Mathf.RoundToInt(i*(radii.Length-1)/63f)];
            result[65]=Mathf.Sqrt(area);
            if(edges.Count>0){edges.Sort();result[66]=edges.Average();for(int i=0;i<64;i++)result[67+i]=edges[Mathf.RoundToInt(i*(edges.Count-1)/63f)];}
            return shapes[part]=result;
        }
        public bool Collect(AssemblyPart part)
        {
            if(!part || Contains(part))return false;
            bool grabbed=items.Count==0?part.BeginGrab():part.BeginInventoryGrab(items[0]);
            if(!grabbed)return false;items.Add(part);return true;
        }
        public void Hide(AssemblyPart part)
        {
            foreach(var r in part.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mesh=r.GetComponent<MeshFilter>();if(!mesh || part.Manager && !part.Manager.OwnsMesh(part,mesh))continue;
                if(!hidden.ContainsKey(r))hidden[r]=r.enabled;r.enabled=false;
            }
        }
        public void Reveal(AssemblyPart part)
        {
            foreach(var r in part.GetComponentsInChildren<MeshRenderer>(true))if(hidden.TryGetValue(r,out var visible)){r.enabled=visible;hidden.Remove(r);}
        }
        public void Remove(AssemblyPart part){Reveal(part);items.Remove(part);}
        public void Prune(){foreach(var p in items.ToArray())if(!p || !p.IsSelected)Remove(p);}
        public void Clear(){foreach(var p in items.ToArray())Remove(p);}
    }
}
