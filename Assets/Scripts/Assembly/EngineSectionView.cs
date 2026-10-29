using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EngineAssembly
{
    // Renderer-only treatments. Mesh assets, vertices and gameplay colliders are untouched.
    public sealed class EngineSectionView : MonoBehaviour
    {
        sealed class Entry
        {
            public Renderer renderer;public Material[] originals,display;
            public MaterialPropertyBlock originalBlock,block;
            public ShadowCastingMode shadow;public float opacity=1;public bool section,camPose;
            public bool applied;public Vector4 appliedPlane,camAxis;public Vector3 camOffsets;public Matrix4x4 camFrame;
            public float appliedEdge,appliedOpacity;public bool appliedSection;
        }
        readonly Dictionary<Renderer,Entry> entries=new Dictionary<Renderer,Entry>();
        readonly Dictionary<Material,Material> sectionMaterials=new Dictionary<Material,Material>();
        readonly HashSet<Renderer> selection=new HashSet<Renderer>(),contextSet=new HashSet<Renderer>();
        Vector4 plane;float edge=.001f;
        public int SharedSectionMaterialCount=>sectionMaterials.Count;
        public int CachedRendererCount=>entries.Count;
        public float ContextVisibility(Renderer renderer)=>entries.TryGetValue(renderer,out var entry)?entry.opacity:1;
        Entry Find(Renderer renderer)
        {
            if(entries.TryGetValue(renderer,out var entry))return entry;
            entry=new Entry{renderer=renderer,originals=renderer.sharedMaterials,originalBlock=new MaterialPropertyBlock(),block=new MaterialPropertyBlock(),shadow=renderer.shadowCastingMode};
            renderer.GetPropertyBlock(entry.originalBlock);renderer.GetPropertyBlock(entry.block);
            entries[renderer]=entry;return entry;
        }
        void Apply(Entry entry,bool force=false)
        {
            if(!entry.renderer)return;
            if(!force && entry.applied && entry.appliedSection==entry.section && entry.appliedOpacity==entry.opacity && (!entry.section || entry.appliedPlane==plane && entry.appliedEdge==edge))return;
            if(!entry.applied || entry.appliedSection!=entry.section)
            {
                if(entry.section && entry.display==null)
                {
                    entry.display=new Material[entry.originals.Length];
                    for(int i=0;i<entry.originals.Length;i++)
                    {
                        var original=entry.originals[i];
                        if(!original || !original.HasProperty("_Cull")){entry.display[i]=original;continue;}
                        if(!sectionMaterials.TryGetValue(original,out var material))
                        {material=new Material(original){name=original.name+" shared section"};material.SetFloat("_Cull",0);sectionMaterials.Add(original,material);}
                        entry.display[i]=material;
                    }
                }
                entry.renderer.sharedMaterials=entry.section?entry.display:entry.originals;
            }
            entry.block.SetFloat("_ContextOpacity",entry.opacity);entry.block.SetFloat("_SectionEnabled",entry.section?1:0);
            entry.block.SetVector("_SectionPlane",plane);entry.block.SetFloat("_SectionEdgeWidth",edge);entry.renderer.SetPropertyBlock(entry.block);
            entry.renderer.shadowCastingMode=entry.section || entry.camPose || entry.opacity<1?ShadowCastingMode.Off:entry.shadow;
            entry.applied=true;entry.appliedSection=entry.section;entry.appliedOpacity=entry.opacity;entry.appliedPlane=plane;entry.appliedEdge=edge;
        }
        public void SetCamPose(Renderer renderer,Transform frame,Vector3 axis,float firstStation,float secondStation,Vector3 offsets,bool enabled)
        {
            if(!renderer)return;var entry=Find(renderer);bool active=enabled && frame;var matrix=frame?frame.localToWorldMatrix:Matrix4x4.identity;var stations=new Vector4(axis.x,axis.y,firstStation,secondStation);
            if(entry.applied && entry.camPose==active && entry.camFrame==matrix && entry.camAxis==stations && entry.camOffsets==offsets)return;
            entry.camPose=active;entry.camFrame=matrix;entry.camAxis=stations;entry.camOffsets=offsets;entry.block.SetFloat("_CamPoseWarp",active?1:0);if(frame){entry.block.SetMatrix("_EngineToWorld",matrix);entry.block.SetMatrix("_WorldToEngine",frame.worldToLocalMatrix);}
            entry.block.SetVector("_CamAxisStations",stations);entry.block.SetVector("_CamPhaseOffsets",offsets);Apply(entry,true);
        }
        public void SetContext(IEnumerable<Renderer> context,float visibility=.28f)
        {
            contextSet.Clear();foreach(var renderer in context)if(renderer){contextSet.Add(renderer);Find(renderer);}
            foreach(var entry in entries.Values){float opacity=contextSet.Contains(entry.renderer) ? Mathf.Clamp01(visibility) : 1;if(entry.opacity==opacity)continue;entry.opacity=opacity;Apply(entry);}
        }
        public void SetSection(IEnumerable<Renderer> shells,bool enabled,Vector3 point,Vector3 normal,float width)
        {
            normal.Normalize();plane=new Vector4(normal.x,normal.y,normal.z,-Vector3.Dot(normal,point));edge=width;
            selection.Clear();if(enabled)foreach(var renderer in shells)if(renderer){selection.Add(renderer);Find(renderer);}
            foreach(var entry in entries.Values){entry.section=selection.Contains(entry.renderer);Apply(entry);}
        }
        public bool IsClipped(Renderer renderer,Vector3 point)=>entries.TryGetValue(renderer,out var entry) && entry.section && Vector3.Dot(new Vector3(plane.x,plane.y,plane.z),point)+plane.w>0;
        public void Restore()
        {
            foreach(var entry in entries.Values)
            {
                if(entry.renderer){entry.renderer.sharedMaterials=entry.originals;entry.renderer.SetPropertyBlock(entry.originalBlock);entry.renderer.shadowCastingMode=entry.shadow;}
            }
            foreach(var material in sectionMaterials.Values)if(material)Destroy(material);
            sectionMaterials.Clear();entries.Clear();
        }
        void OnDestroy()=>Restore();
    }
}
