using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace EngineAssembly
{
    // Presentation poses only. Assembly progress, recipe data and source meshes remain unchanged.
    public sealed class PCDisplayTimeline:MonoBehaviour
    {
        sealed class Visual
        {
            public PartDefinition definition;public Transform transform;public MeshRenderer renderer;
            public Vector3 localPosition,position,offset;public Quaternion localRotation,rotation;public bool enabled;public int depth;
        }
        AssemblyManager manager;NarrationSpeech speech;Transform root;List<PartDefinition> steps;Visual[] visuals;
        Dictionary<string,int> indices;
        readonly Dictionary<string,Vector3> stageOffsets=new Dictionary<string,Vector3>();
        readonly HashSet<Renderer> faded=new HashSet<Renderer>();EngineSectionView treatment;
        public bool Active{get;private set;}
        public bool Playing{get;set;}
        public float Position{get;private set;}=1;
        public PartDefinition Current{get;private set;}
        public int StepCount=>steps?.Count??0;
        public bool AudioEnabled=>DisplayPlaybackSettings.AudioEnabled;
        public float PlaybackSpeed=>DisplayPlaybackSettings.Speed;
        public Action<PartDefinition> StepChanged;
        public void Configure(AssemblyManager manager,NarrationSpeech speech){this.manager=manager;this.speech=speech;root=manager.AssemblyRoot;steps=EngineLearningContent.Timeline(manager.Recipe);indices=steps.Select((p,i)=>(p.id,i)).ToDictionary(p=>p.id,p=>p.i);treatment=manager.GetComponent<EngineSectionView>()??manager.gameObject.AddComponent<EngineSectionView>();}
        public bool IsFaded(Renderer renderer)=>faded.Contains(renderer);
        void Cache()
        {
            var list=new List<Visual>();
            foreach(var mesh in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var d=manager.Recipe.parts.FirstOrDefault(p=>p.id==mesh.name);if(d==null || !mesh.TryGetComponent<MeshRenderer>(out var renderer))continue;
                var t=mesh.transform;int depth=0;for(var parent=t.parent;parent;parent=parent.parent)depth++;
                var scale=new Vector3(t.lossyScale.x/root.lossyScale.x,t.lossyScale.y/root.lossyScale.y,t.lossyScale.z/root.lossyScale.z);
                var group=manager.Recipe.subAssemblies.FirstOrDefault(g=>g.id==d.subAssemblyId);var frame=group!=null?manager.Recipe.parts.First(p=>p.id==group.rootPartId).targetRotation:Quaternion.identity;
                list.Add(new Visual{definition=d,transform=t,renderer=renderer,localPosition=t.localPosition,localRotation=t.localRotation,position=root.InverseTransformPoint(t.position),rotation=Quaternion.Inverse(root.rotation)*t.rotation,enabled=renderer.enabled,depth=depth,
                    offset=frame*AssemblyMotion.DisplayInsertionOffset(d,mesh.sharedMesh.bounds,scale)});
            }
            visuals=list.OrderBy(v=>v.depth).ToArray();stageOffsets.Clear();
            var all=BoundsFor(visuals);
            foreach(var group in manager.Recipe.subAssemblies)
            {
                var members=visuals.Where(v=>v.definition.subAssemblyId==group.id).ToArray();if(members.Length==0)continue;var bounds=BoundsFor(members);
                stageOffsets[group.id]=new Vector3(all.center.x-bounds.center.x,all.min.y-bounds.min.y,all.center.z-bounds.center.z);
            }
        }
        Bounds BoundsFor(IEnumerable<Visual> set)
        {
            bool first=true;var bounds=new Bounds();foreach(var v in set){var b=v.renderer.bounds;for(int i=0;i<8;i++){var p=root.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}}return bounds;
        }
        float Progress(string id)=>indices.TryGetValue(id,out int index)?Mathf.Clamp01(Position*steps.Count-index):1;
        public void SetValue(float value)
        {
            if(!Active){Cache();Active=true;}Position=Mathf.Clamp01(value);var next=steps[Mathf.Clamp(Mathf.FloorToInt(Position*steps.Count+.00001f),0,steps.Count-1)];bool changed=Current!=next;Current=next;
            string workspace=Position>=1?null:Current.subAssemblyId;
            foreach(var visual in visuals)
            {
                var d=visual.definition;float fraction=Progress(d.id);bool visible=fraction>0;
                if(!string.IsNullOrEmpty(workspace))visible&=d.subAssemblyId==workspace;
                else if(!string.IsNullOrEmpty(d.subAssemblyId)){var carrier=manager.Recipe.subAssemblies.First(g=>g.id==d.subAssemblyId).rootPartId;visible&=Progress(carrier)>0;}
                var offset=string.IsNullOrEmpty(workspace)?Vector3.zero:stageOffsets[workspace];float t=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.76f,fraction));
                visual.renderer.enabled=visible;visual.transform.SetPositionAndRotation(root.TransformPoint(visual.position+offset+visual.offset*(1-t)),root.rotation*visual.rotation);
            }
            faded.Clear();var fitting=visuals.FirstOrDefault(v=>v.definition.id==Current.id);
            if(Position<1 && fitting!=null)foreach(var housing in visuals.Where(v=>v.renderer.enabled && v!=fitting && new[]{"101_","401_","701_","501_Front_","601_"}.Any(v.definition.id.StartsWith)))if(housing.renderer.bounds.Intersects(fitting.renderer.bounds))faded.Add(housing.renderer);
            treatment.SetContext(faded);Physics.SyncTransforms();if(changed)StepChanged?.Invoke(Current);
        }
        public void TogglePlay(){if(!Active || Position>=1){Current=null;Playing=true;SetValue(0);}else {Playing=!Playing;if(Playing)StepChanged?.Invoke(Current);}}
        static string Voice(PartDefinition part)=>DisplayPlaybackSettings.Description(part).voiceId;
        void Update()
        {
            if(!Active || !Playing || Time.timeScale<=0)return;int index=Mathf.Clamp(Mathf.FloorToInt(Position*steps.Count),0,steps.Count-1);
            var part=steps[index];float seconds=manager.Recipe.subAssemblies.Any(g=>g.rootPartId==part.id)?5:new[]{"Bolt","Screw","Nut"}.Any(part.id.Contains)?2.6f:3.8f;
            float next=Position+Mathf.Min(Time.unscaledDeltaTime,.1f)*PlaybackSpeed/(steps.Count*seconds);
            if(AudioEnabled && next*steps.Count>=index+1 && speech.IsBusy && (index+1>=steps.Count || Voice(steps[index+1])!=Voice(part)))next=(index+.9999f)/steps.Count;
            SetValue(next);if(Position>=1)Playing=false;
        }
        public void Stop(bool reset=false)
        {
            Playing=false;if(reset){Position=1;Current=null;}if(!Active)return;Active=false;
            foreach(var visual in visuals)if(visual.transform){visual.transform.SetLocalPositionAndRotation(visual.localPosition,visual.localRotation);visual.renderer.enabled=visual.enabled;}
            faded.Clear();treatment.SetContext(faded);Physics.SyncTransforms();
        }
        void OnDisable()=>Stop();
        void OnDestroy()=>Stop();
    }
}
