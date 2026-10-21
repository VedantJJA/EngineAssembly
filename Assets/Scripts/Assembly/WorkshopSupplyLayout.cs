using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EngineAssembly
{
    // Scene-owned PC supply surfaces. Recipe socket coordinates and mobile staging
    // remain independent of the furniture used by the walking workshop.
    [DisallowMultipleComponent]
    public sealed class WorkshopSupplyLayout : MonoBehaviour
    {
        [Serializable] public struct Surface { public Vector3 center; public Vector2 size; }
        [SerializeField] Surface[] surfaces = Array.Empty<Surface>();
        readonly List<AssemblyPart> repairParts = new List<AssemblyPart>();
        readonly List<Surface> localSurfaces=new List<Surface>();
        public IReadOnlyList<Surface> Surfaces => surfaces;
        public void ClearLocalSurfaces()=>localSurfaces.Clear();
        public void Configure(Surface[] value) => surfaces = value;
        public bool Contains(Bounds bounds) => surfaces.Concat(localSurfaces).Any(s => bounds.min.x >= s.center.x-s.size.x*.5f-.005f && bounds.max.x <= s.center.x+s.size.x*.5f+.005f
            && bounds.min.z >= s.center.z-s.size.y*.5f-.005f && bounds.max.z <= s.center.z+s.size.y*.5f+.005f && Mathf.Abs(bounds.min.y-s.center.y-.025f)<.01f);
        public Vector3 RepairDestination => surfaces.Length>0 ? surfaces[0].center+Vector3.up*.2f : transform.position;
        public void StageChapter(AssemblyManager manager)
        {
            repairParts.Clear();localSurfaces.Clear();
            var carriers=manager.Recipe.subAssemblies.Select(s=>s.rootPartId).ToHashSet();
            Stage(manager.AssemblyParts.Where(p=>p && p.gameObject.activeSelf && !p.IsSnapped && p.Definition!=null && !p.Definition.isBase && !carriers.Contains(p.PartId)));
        }
        public void StageRepairPart(AssemblyPart part)
        {
            if(!repairParts.Contains(part))repairParts.Add(part);
            Stage(repairParts.Where(p=>p && p.gameObject.activeSelf && !p.IsSnapped && !p.IsSelected));
        }
        IEnumerable<AssemblyPart> Members(AssemblyManager manager,SubAssemblyDefinition group)=>manager.AssemblyParts.Where(p=>p && p.gameObject.activeSelf && !p.IsSnapped && !p.IsSelected && p.Definition!=null && p.Definition.subAssemblyId==group.id);
        public Vector2 MeasureSubassembly(AssemblyManager manager,SubAssemblyDefinition group,float width)
        {
            var parts=Members(manager,group).ToArray();if(parts.Length==0)return Vector2.zero;
            float x=0,z=0,row=0;
            Vector2 Size(AssemblyPart p){var b=AssemblyHeldView.WorldBounds(p).size;if(b.z>b.x && b.z>.8f)(b.x,b.z)=(b.z,b.x);return new Vector2(Mathf.Max(.085f,b.x)+.045f,Mathf.Max(.085f,b.z)+.045f);}
            width=Mathf.Max(width,parts.Max(p=>Size(p).x)+.02f);
            foreach(var batch in parts.GroupBy(p=>string.IsNullOrEmpty(p.GeometryGroupId)?AssemblyInventory.TypeName(p):p.GeometryGroupId).OrderBy(g=>g.Min(p=>p.Definition.order)))
            {
                float groupWidth=batch.Sum(p=>Size(p).x);if(x>0 && batch.Count()>1 && groupWidth>width-x && groupWidth<=width){x=0;z+=row+.04f;row=0;}
                foreach(var p in batch.OrderBy(p=>p.Definition.order).ThenBy(p=>p.PartId,StringComparer.Ordinal)){var size=Size(p);if(x+size.x>width){x=0;z+=row+.04f;row=0;}x+=size.x;row=Mathf.Max(row,size.y);}
            }
            return new Vector2(width,Mathf.Max(.38f,z+row+.02f));
        }
        public void StageSubassembly(AssemblyManager manager,SubAssemblyDefinition group,Surface surface){localSurfaces.Add(surface);Stage(Members(manager,group),new[]{surface});}
        void Stage(IEnumerable<AssemblyPart> input)=>Stage(input,surfaces);
        void Stage(IEnumerable<AssemblyPart> input,Surface[] destinations)

        {
            if(destinations.Length==0)return;
            int surface=0;float x=0,z=0,rowDepth=0;
            // Consecutive slots for a geometry group keep repeated fasteners together.
            var groups=input.GroupBy(p=>string.IsNullOrEmpty(p.GeometryGroupId)?AssemblyInventory.TypeName(p):p.GeometryGroupId).OrderBy(g=>g.Min(p=>p.Definition.order));
            foreach(var group in groups)
            {
                float groupWidth=group.Sum(p=>Mathf.Max(.085f,AssemblyHeldView.WorldBounds(p).size.x)+.045f);
                if(surface<destinations.Length && x>0 && group.Count()>1 && groupWidth>destinations[surface].size.x-x && groupWidth<=destinations[surface].size.x)
                {x=0;z+=rowDepth+.04f;rowDepth=0;}
                foreach(var part in group.OrderBy(p=>p.Definition.order).ThenBy(p=>p.PartId,StringComparer.Ordinal))
                {
                    var bounds=AssemblyHeldView.WorldBounds(part);
                    // Long parts lie across the bench, rather than projecting into an aisle.
                    if(bounds.size.z>bounds.size.x && bounds.size.z>.8f){part.transform.rotation=Quaternion.AngleAxis(90,Vector3.up)*part.transform.rotation;bounds=AssemblyHeldView.WorldBounds(part);}
                    float width=Mathf.Max(.085f,bounds.size.x)+.045f,depth=Mathf.Max(.085f,bounds.size.z)+.045f;
                    bool fits=false;
                    while(surface<destinations.Length)
                    {
                        var s=destinations[surface];
                        if(x+width>s.size.x){x=0;z+=rowDepth+.04f;rowDepth=0;}
                        if(width<=s.size.x && z+depth<=s.size.y){fits=true;break;}
                        surface++;x=z=rowDepth=0;
                    }
                    if(!fits)throw new InvalidOperationException("Supply benches need more space for "+part.PartDisplayName+" ("+width+" x "+depth+").");
                    var slot=destinations[surface];
                    var bottom=new Vector3(slot.center.x-slot.size.x*.5f+x+width*.5f,slot.center.y+.025f,slot.center.z-slot.size.y*.5f+z+depth*.5f);
                    var position=part.transform.position+bottom+Vector3.up*bounds.extents.y-bounds.center;
                    part.ResetLoose(part.Manager.TrayFrame,part.Manager.TrayFrame.InverseTransformPoint(position),Quaternion.Inverse(part.Manager.TrayFrame.rotation)*part.transform.rotation);
                    x+=width;rowDepth=Mathf.Max(rowDepth,depth);
                }
            }
        }
    }
}
