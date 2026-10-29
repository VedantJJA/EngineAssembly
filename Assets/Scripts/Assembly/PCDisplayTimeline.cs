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
            public Vector3 localPosition,position,offset;public Quaternion localRotation,rotation;public bool enabled,housing;public int depth;public string carrier;
        }
        AssemblyManager manager;NarrationSpeech speech;Transform root;List<PartDefinition> steps;Visual[] visuals;
        Dictionary<string,int> indices;
        readonly Dictionary<string,Vector3> stageOffsets=new Dictionary<string,Vector3>();
        readonly Dictionary<string,Visual> byId=new Dictionary<string,Visual>();
        readonly Dictionary<string,float> stepSeconds=new Dictionary<string,float>();
        readonly Dictionary<string,string> voices=new Dictionary<string,string>();
        readonly HashSet<Renderer> faded=new HashSet<Renderer>();EngineSectionView treatment;Quaternion orientation=Quaternion.identity,wantedOrientation=Quaternion.identity;Vector3 mainPivot;readonly Dictionary<string,Vector3> pivots=new Dictionary<string,Vector3>();
        public Quaternion PresentationOrientation=>orientation;
        public bool IsTurning=>Quaternion.Angle(orientation,wantedOrientation)>.1f;
        public bool Active{get;private set;}
        public bool Playing{get;set;}
        public float Position{get;private set;}=1;
        public PartDefinition Current{get;private set;}
        public int StepCount=>steps?.Count??0;
        public bool AudioEnabled=>DisplayPlaybackSettings.AudioEnabled;
        public float PlaybackSpeed=>DisplayPlaybackSettings.Speed;
        public Action<PartDefinition> StepChanged;
        public void Configure(AssemblyManager manager,NarrationSpeech speech)
        {
            this.manager=manager;this.speech=speech;root=manager.AssemblyRoot;steps=EngineLearningContent.Timeline(manager.Recipe);indices=steps.Select((p,i)=>(p.id,i)).ToDictionary(p=>p.id,p=>p.i);treatment=manager.GetComponent<EngineSectionView>()??manager.gameObject.AddComponent<EngineSectionView>();
            foreach(var part in steps){stepSeconds[part.id]=manager.Recipe.subAssemblies.Exists(g=>g.rootPartId==part.id)?5:part.id.Contains("Bolt") || part.id.Contains("Screw") || part.id.Contains("Nut")?2.6f:3.8f;voices[part.id]=DisplayPlaybackSettings.Description(part).voiceId;}
        }
        public bool IsFaded(Renderer renderer)=>faded.Contains(renderer);
        void Cache()
        {
            var list=new List<Visual>();byId.Clear();
            var definitions=manager.Recipe.parts.ToDictionary(p=>p.id);var groups=manager.Recipe.subAssemblies.ToDictionary(g=>g.id);
            foreach(var mesh in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!definitions.TryGetValue(mesh.name,out var d) || !mesh.TryGetComponent<MeshRenderer>(out var renderer))continue;
                var t=mesh.transform;int depth=0;for(var parent=t.parent;parent;parent=parent.parent)depth++;
                var scale=new Vector3(t.lossyScale.x/root.lossyScale.x,t.lossyScale.y/root.lossyScale.y,t.lossyScale.z/root.lossyScale.z);
                SubAssemblyDefinition group=null;if(!string.IsNullOrEmpty(d.subAssemblyId))groups.TryGetValue(d.subAssemblyId,out group);var frame=group!=null?definitions[group.rootPartId].targetRotation:Quaternion.identity;
                var visual=new Visual{definition=d,transform=t,renderer=renderer,localPosition=t.localPosition,localRotation=t.localRotation,position=root.InverseTransformPoint(t.position),rotation=Quaternion.Inverse(root.rotation)*t.rotation,enabled=renderer.enabled,depth=depth,
                    offset=frame*AssemblyMotion.DisplayInsertionOffset(d,mesh.sharedMesh.bounds,scale),carrier=group?.rootPartId,housing=d.id.StartsWith("101_") || d.id.StartsWith("401_") || d.id.StartsWith("701_") || d.id.StartsWith("501_Front_") || d.id.StartsWith("601_")};
                list.Add(visual);byId[d.id]=visual;
            }
            visuals=list.OrderBy(v=>v.depth).ToArray();stageOffsets.Clear();
            var all=BoundsFor(visuals);mainPivot=all.center;pivots.Clear();
            foreach(var group in manager.Recipe.subAssemblies)
            {
                var members=visuals.Where(v=>v.definition.subAssemblyId==group.id).ToArray();if(members.Length==0)continue;var bounds=BoundsFor(members);
                stageOffsets[group.id]=new Vector3(all.center.x-bounds.center.x,all.min.y-bounds.min.y,all.center.z-bounds.center.z);
                pivots[group.id]=bounds.center+stageOffsets[group.id];
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
            string workspace=Position>=1?null:Current.subAssemblyId;var frameOffset=string.IsNullOrEmpty(workspace)?Vector3.zero:stageOffsets[workspace];
            wantedOrientation=Position>=1?Quaternion.identity:DisplayWorkpieceOrientation.For(manager.Recipe,steps,Mathf.FloorToInt(Position*steps.Count),workspace);if(!Playing)orientation=wantedOrientation;
            var pivot=string.IsNullOrEmpty(workspace)?mainPivot:pivots[workspace];
            foreach(var visual in visuals)
            {
                var d=visual.definition;float fraction=Progress(d.id);bool visible=fraction>0;
                if(!string.IsNullOrEmpty(workspace))visible&=d.subAssemblyId==workspace;
                else if(visual.carrier!=null)visible&=Progress(visual.carrier)>0;
                if(visual.renderer.enabled!=visible)visual.renderer.enabled=visible;
                // Only the entering part moves. Unchanged parts do not dirty the transform hierarchy.
                float t=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.76f,fraction));
                var position=root.TransformPoint(pivot+orientation*(visual.position+frameOffset+visual.offset*(1-t)-pivot));var rotation=root.rotation*orientation*visual.rotation;
                if(visual.transform.position!=position || visual.transform.rotation!=rotation)visual.transform.SetPositionAndRotation(position,rotation);
            }
            faded.Clear();
            if(Position<1 && byId.TryGetValue(Current.id,out var fitting))
                foreach(var housing in visuals)if(housing.housing && housing.renderer.enabled && housing!=fitting && housing.renderer.bounds.Intersects(fitting.renderer.bounds))faded.Add(housing.renderer);
            treatment.SetContext(faded);if(changed)StepChanged?.Invoke(Current);
        }
        public void TogglePlay(){if(!Active || Position>=1){Current=null;Playing=true;SetValue(0);}else {Playing=!Playing;if(Playing)StepChanged?.Invoke(Current);}}
        void Update()
        {
            if(!Active || !Playing || Time.timeScale<=0)return;int index=Mathf.Clamp(Mathf.FloorToInt(Position*steps.Count),0,steps.Count-1);
            if(IsTurning){orientation=Quaternion.RotateTowards(orientation,wantedOrientation,Mathf.Min(Time.unscaledDeltaTime,.1f)*225);SetValue(Position);return;}
            var part=steps[index];float next=Position+Mathf.Min(Time.unscaledDeltaTime,.1f)*PlaybackSpeed/(steps.Count*stepSeconds[part.id]);
            if(AudioEnabled && next*steps.Count>=index+1 && speech.IsBusy && (index+1>=steps.Count || voices[steps[index+1].id]!=voices[part.id]))next=(index+.9999f)/steps.Count;
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
