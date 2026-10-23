using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace EngineAssembly
{
    // Samples the supplied lobe outline at the valve's actual axial station.
    // A flat tappet follows the intersection of that outline with the stem axis.
    public sealed class EngineCamContact
    {
        readonly float[] reach=new float[360];
        public float BaseReach{get;private set;}
        public float MaximumReach{get;private set;}
        public float PeakAngle{get;private set;}
        public EngineCamContact(Vector3[] vertices,Vector3 camAxis,Vector3 tip,Vector3 direction)
        {
            float nearest=vertices.Min(v=>Mathf.Abs(v.z-tip.z));
            var points=vertices.Where(v=>Mathf.Abs(v.z-tip.z)<=nearest+.0002f).Select(v=>new Vector2(v.x-camAxis.x,v.y-camAxis.y)).OrderBy(v=>v.x).ThenBy(v=>v.y).ToArray();
            var lower=new List<Vector2>();var upper=new List<Vector2>();
            foreach(var p in points){while(lower.Count>=2 && Cross(lower[lower.Count-1]-lower[lower.Count-2],p-lower[lower.Count-1])<=.00000001f)lower.RemoveAt(lower.Count-1);lower.Add(p);}
            foreach(var p in points.Reverse()){while(upper.Count>=2 && Cross(upper[upper.Count-1]-upper[upper.Count-2],p-upper[upper.Count-1])<=.00000001f)upper.RemoveAt(upper.Count-1);upper.Add(p);}
            var hull=lower.Take(lower.Count-1).Concat(upper.Take(upper.Count-1)).ToArray();
            var axis=new Vector2(direction.x,direction.y).normalized;var sideways=new Vector2(-axis.y,axis.x);float offset=Vector2.Dot(new Vector2(tip.x-camAxis.x,tip.y-camAxis.y),sideways);
            BaseReach=float.PositiveInfinity;
            for(int degree=0;degree<360;degree++)
            {
                float angle=degree*Mathf.Deg2Rad,s=Mathf.Sin(angle),c=Mathf.Cos(angle),contact=float.NegativeInfinity;
                for(int j=0;j<hull.Length;j++)
                {
                    var a=Rotate(hull[j],s,c);var b=Rotate(hull[(j+1)%hull.Length],s,c);float x=Vector2.Dot(a,sideways),next=Vector2.Dot(b,sideways);
                    if(offset<Mathf.Min(x,next) || offset>Mathf.Max(x,next) || Mathf.Abs(next-x)<.0000001f)continue;
                    contact=Mathf.Max(contact,Vector2.Dot(Vector2.Lerp(a,b,(offset-x)/(next-x)),axis));
                }
                if(float.IsNegativeInfinity(contact))throw new InvalidOperationException("Valve stem axis misses its cam lobe.");
                reach[degree]=contact;BaseReach=Mathf.Min(BaseReach,contact);if(contact>MaximumReach){MaximumReach=contact;PeakAngle=degree;}
            }
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static Vector2 Rotate(Vector2 p,float s,float c)=>new Vector2(c*p.x-s*p.y,s*p.x+c*p.y);
        public float Reach(float angle){float t=Mathf.Repeat(angle,360);int first=Mathf.FloorToInt(t);return Mathf.Lerp(reach[first],reach[(first+1)%360],t-first);}
    }
}
