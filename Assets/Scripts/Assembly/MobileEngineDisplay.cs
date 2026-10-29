using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace EngineAssembly
{
    public sealed class MobileEngineDisplay : MonoBehaviour
    {
        public enum DisplayMode { InfoTimeline, OperatingEngine }
        public DisplayMode Mode{get;private set;}
        public bool TimelinePlaying=>playing;
        [SerializeField] Transform engineRoot;
        [SerializeField] Camera viewCamera;
        [SerializeField] TextAsset recipeAsset;
        sealed class Visual { public PartDefinition definition;public Transform transform;public MeshRenderer renderer;public MeshCollider collider;public Vector3 insertionOffset;public Vector3[] samples;public string carrierId; }
        readonly Dictionary<string,Visual> visuals=new Dictionary<string,Visual>();
        readonly Dictionary<Collider,Visual> colliderVisuals=new Dictionary<Collider,Visual>();
        readonly HashSet<Visual> cutaway=new HashSet<Visual>();
        readonly Dictionary<string,Transform> groups=new Dictionary<string,Transform>();
        readonly Dictionary<string,SubAssemblyDefinition> groupDefinitions=new Dictionary<string,SubAssemblyDefinition>(),carrierDefinitions=new Dictionary<string,SubAssemblyDefinition>();
        readonly HashSet<string> bases=new HashSet<string>();
        readonly Dictionary<string,EnginePartDetail> details=new Dictionary<string,EnginePartDetail>();
        readonly List<Renderer> contextRenderers=new List<Renderer>();
        readonly Dictionary<string,int> indices=new Dictionary<string,int>();
        List<PartDefinition> timeline;
        AssemblyRecipe recipe;
        RectTransform safe,footer,info;
        Canvas canvas;
        TextMeshProUGUI progress,title,description,partMeta,playLabel;
        AssemblyTimelineScrubber scrubber;
        TMP_FontAsset heading,body;
        NarrationSpeech speech;RectTransform settingsPanel;TextMeshProUGUI speedLabel,sectionLabel,engineSpeedLabel,audioLabel,explainLabel;
        UnityEngine.UI.Image infoModeCard,engineModeCard;float timelineBookmark=1,engineBookmark;string spokenStep;
        EngineSectionView section;EngineOperatingAnimation operation;EngineOperationNarrator explanation;
        public EngineOperatingAnimation Operation=>operation;
        public EngineOperationNarrator Explanation=>explanation;
        float playbackSpeed=1;Vector3 cameraDirection=new Vector3(1,.7f,-1).normalized;
        public float PlaybackSpeed=>playbackSpeed;
        public bool TimelineAudioEnabled=>DisplayPlaybackSettings.AudioEnabled;
        public void SetTimelineAudio(bool enabled){DisplayPlaybackSettings.AudioEnabled=enabled;if(!enabled && Mode==DisplayMode.InfoTimeline)speech.Stop();spokenStep=null;if(audioLabel)audioLabel.text="TIMELAPSE AUDIO · "+(enabled?"ON":"OFF");if(enabled && playing)ShowInstruction(currentStep);}
        public void SetPlaybackSpeed(float value){playbackSpeed=Mathf.Clamp(value,.1f,4);DisplayPlaybackSettings.Speed=playbackSpeed;if(speedLabel)speedLabel.text="Assembly speed · "+playbackSpeed.ToString("0.00")+"×";}
        Quaternion initialRotation;
        Quaternion presentationOrientation=Quaternion.identity,wantedPresentationOrientation=Quaternion.identity;Vector3 presentationPivot;readonly Dictionary<string,Vector3> groupPivots=new Dictionary<string,Vector3>();
        public Quaternion PresentationOrientation=>presentationOrientation;
        Vector3 focus,pan,initialPosition;
        float radius,zoom=1,position;
        bool playing,ready,pointerActive,dragging,multiTouch,panAllowed;
        Vector2 lastPointer,startPointer,multiCenter;
        float lastSpan;
        Vector2Int lastSize;Rect lastSafe;
        string selectedId;string instructionKey;bool instructionComplete,instructionBusy;int progressIndex=-1;
        SubAssemblyDefinition displayWorkspace;
        PartDefinition currentStep;
        int plannedStep=-1;
        Vector3 insertionView=new Vector3(1,.7f,-1).normalized;
        public string CurrentWorkspaceId=>displayWorkspace?.id??"engine";
        public string CurrentStepId=>currentStep?.id;
        public bool IsPartVisible(string id)=>visuals.TryGetValue(id,out var visual) && visual.renderer.enabled;
        public bool CutawayActive=>cutaway.Count>0;
        public bool Ready=>ready;
        public Vector3 WorldFocus=>engineRoot.TransformPoint(focus);
        public float PresentationRadius=>radius;
        public float TimelinePosition=>position;
        public int StepCount=>timeline?.Count??0;
        public int VisiblePartCount=>visuals.Values.Count(v=>v.renderer.enabled);
        public string SelectedPartId=>selectedId;
        public Camera ViewCamera=>viewCamera;
        public Transform EngineRoot=>engineRoot;
        public AssemblyRecipe Recipe=>recipe;
        static readonly Color Ink=WorkshopTheme.Background,Panel=WorkshopTheme.Surface,Mint=WorkshopTheme.Accent,Muted=WorkshopTheme.Muted;
        SpatialExperienceRig spatialRig;readonly List<RaycastResult> pointerHits=new List<RaycastResult>(12);PointerEventData pointerEvent;EventSystem pointerEvents;
        public void Configure(Transform root,Camera camera,TextAsset asset){engineRoot=root;viewCamera=camera;recipeAsset=asset;}
        void Start()
        {
            EngineSurfaceLibrary.Apply(engineRoot);
            speech=gameObject.AddComponent<NarrationSpeech>();section=gameObject.AddComponent<EngineSectionView>();operation=gameObject.AddComponent<EngineOperatingAnimation>();explanation=gameObject.AddComponent<EngineOperationNarrator>();SetPlaybackSpeed(DisplayPlaybackSettings.Speed);
            engineRoot.localScale*=.6f;
            spatialRig=SpatialExperienceRig.Ensure(gameObject,engineRoot,viewCamera);viewCamera.backgroundColor=Ink;
            spatialRig.CameraChanged+=SetPresentationCamera;
            recipe=AssemblyRecipeStore.Parse(recipeAsset.text);timeline=EngineLearningContent.Timeline(recipe);
            for(int i=0;i<timeline.Count;i++)indices[timeline[i].id]=i;
            foreach(var part in recipe.parts){if(part.isBase)bases.Add(part.id);details[part.id]=DisplayPlaybackSettings.Description(part);}
            foreach(var group in recipe.subAssemblies)
            {
                groupDefinitions[group.id]=group;carrierDefinitions[group.rootPartId]=group;var t=new GameObject(group.rootPartId+" display fixture").transform;t.SetParent(engineRoot,false);groups[group.id]=t;
            }
            foreach(var filter in engineRoot.GetComponentsInChildren<MeshFilter>().ToArray())
            {
                var d=recipe.parts.FirstOrDefault(p=>p.id==filter.name);if(d==null){filter.gameObject.SetActive(false);continue;}
                var renderer=filter.GetComponent<MeshRenderer>();if(!renderer)continue;
                // Preserve CAD scale while changing only the reference frame.
                var scale=filter.transform.lossyScale;filter.transform.SetParent(string.IsNullOrEmpty(d.subAssemblyId)?engineRoot:groups[d.subAssemblyId],true);
                filter.transform.localScale=new Vector3(scale.x/engineRoot.lossyScale.x,scale.y/engineRoot.lossyScale.y,scale.z/engineRoot.lossyScale.z);
                filter.transform.SetLocalPositionAndRotation(d.targetPosition,d.targetRotation);
                var collider=filter.GetComponent<MeshCollider>();if(!collider)collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;
                visuals[d.id]=new Visual{definition=d,transform=filter.transform,renderer=renderer,collider=collider,
                    insertionOffset=AssemblyMotion.DisplayInsertionOffset(d,filter.sharedMesh.bounds,filter.transform.localScale),carrierId=string.IsNullOrEmpty(d.subAssemblyId)?null:groupDefinitions[d.subAssemblyId].rootPartId};
                colliderVisuals[collider]=visuals[d.id];
            }
            if(visuals.Count!=recipe.parts.Count-recipe.subAssemblies.Count)throw new InvalidOperationException("Display recipe does not cover the visual model.");
            foreach(var visual in visuals.Values.Where(v=>v.definition.order==403))
            {
                string station=visual.definition.displayName.Split('·').Last().Trim().Replace(" IN"," intake valve ").Replace(" EX"," exhaust valve ");
                var valve=visuals.Values.FirstOrDefault(v=>v.definition.order==402 && v.definition.displayName.Equals(station,StringComparison.OrdinalIgnoreCase));
                if(valve==null)continue;
                var shape=valve.transform.GetComponent<MeshFilter>().sharedMesh.bounds;
                var axis=valve.definition.targetRotation*AssemblyMotion.ShapeAxis(shape,valve.transform.localScale,false);if(Vector3.Dot(axis,Vector3.up)<0)axis=-axis;
                if(visual.definition.id.Contains("Keeper"))
                {
                    var offset=visual.definition.targetPosition-valve.definition.targetPosition;var radial=offset-Vector3.Project(offset,axis);
                    if(radial.sqrMagnitude>.000001f)axis=radial.normalized;
                }
                visual.insertionOffset=axis.normalized*visual.insertionOffset.magnitude;
            }
            initialRotation=engineRoot.localRotation;initialPosition=engineRoot.localPosition;CreateUI();ready=true;SetTimeline(1);CachePresentationPivots();operation.Configure(engineRoot,recipe,viewCamera,section);WorkshopTeachingDirector.Ensure(gameObject);
            explanation.Configure(operation,speech,(heading,text)=>{title.text=heading;description.text=text;});
            UpdateViewport();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        public float PartProgress(string id)
        {
            if(!indices.TryGetValue(id,out int index))return bases.Contains(id)?1:0;
            return Mathf.Clamp01(position*timeline.Count-index);
        }
        public void SetTimeline(float value,bool synchronizePhysics=true)
        {
            if(!ready)return;if(operation.Active){explanation?.Cancel();operation.Stop();}Mode=DisplayMode.InfoTimeline;position=Mathf.Clamp01(value);scrubber.SetValue(position);
            int done=Mathf.FloorToInt(position*timeline.Count+.00001f),index=Mathf.Clamp(done,0,timeline.Count-1);
            currentStep=timeline[index];SubAssemblyDefinition workspace=null;if(!string.IsNullOrEmpty(currentStep.subAssemblyId))groupDefinitions.TryGetValue(currentStep.subAssemblyId,out workspace);
            wantedPresentationOrientation=position>=1?Quaternion.identity:DisplayWorkpieceOrientation.For(recipe,timeline,index,workspace?.id);if(!playing)presentationOrientation=wantedPresentationOrientation;
            if(workspace!=displayWorkspace){displayWorkspace=workspace;pan=Vector3.zero;selectedId=null;plannedStep=-1;}
            foreach(var group in recipe.subAssemblies)
            {
                var carrier=recipe.parts.First(p=>p.id==group.rootPartId);float t=InsertionProgress(PartProgress(carrier.id));
                var transform=groups[group.id];
                // Separate display stage: only the unit being built occupies this area.
                // Complete units rejoin the engine when their carrier installation begins.
                var pos=group==displayWorkspace?Vector3.zero:carrier.targetPosition+Vector3.up*Mathf.Clamp(group.focusRadius*.3f,.025f,.18f)*(t>0?1-t:0);
                var rotation=group==displayWorkspace?presentationOrientation:carrier.targetRotation;
                if(group==displayWorkspace && groupPivots.TryGetValue(group.id,out var pivot))pos+=pivot-presentationOrientation*pivot;
                else if(displayWorkspace==null){pos=presentationPivot+presentationOrientation*(pos-presentationPivot);rotation=presentationOrientation*rotation;}
                if(transform.localPosition!=pos || transform.localRotation!=rotation)transform.SetLocalPositionAndRotation(pos,rotation);
            }
            foreach(var visual in visuals.Values)
            {
                float t=PartProgress(visual.definition.id);bool visible=t>0;
                if(displayWorkspace!=null)visible&=visual.definition.subAssemblyId==displayWorkspace.id;
                else if(!string.IsNullOrEmpty(visual.definition.subAssemblyId))
                    visible&=PartProgress(visual.carrierId)>0;
                if(visual.renderer.enabled!=visible)visual.renderer.enabled=visible;
                bool queryVisible=visible && !cutaway.Contains(visual);if(visual.collider.enabled!=queryVisible)visual.collider.enabled=queryVisible;
                var pos=visual.definition.targetPosition+visual.insertionOffset*(1-InsertionProgress(t));var rotation=visual.definition.targetRotation;
                if(displayWorkspace==null && string.IsNullOrEmpty(visual.definition.subAssemblyId)){pos=presentationPivot+presentationOrientation*(pos-presentationPivot);rotation=presentationOrientation*rotation;}
                if(visual.transform.localPosition!=pos || visual.transform.localRotation!=rotation)visual.transform.SetLocalPositionAndRotation(pos,rotation);
            }
            if(progressIndex!=done){progressIndex=done;progress.text=done+" / "+timeline.Count+"   ·   "+recipe.chapters.First(c=>c.id==currentStep.chapterId).title;}
            if(selectedId!=null && (!visuals.TryGetValue(selectedId,out var selected) || !selected.renderer.enabled))selectedId=null;
            var bounds=WorkspaceBounds();var fitting=FittingBounds();
            float frameRadius=position>=1?bounds.extents.magnitude:Mathf.Max(fitting.extents.magnitude*1.6f,bounds.extents.magnitude*.32f);
            focus=engineRoot.InverseTransformPoint(position>=1?bounds.center:Vector3.Lerp(bounds.center,fitting.center,.78f));radius=Mathf.Max(.045f,frameRadius);
            if(plannedStep!=index || position>=1)
            {
                // Restore previous cutaways before choosing the next view. Only a new
                // step needs physics queries; ordinary playback moves renderers directly.
                foreach(var faded in cutaway)faded.collider.enabled=faded.renderer.enabled;
                Physics.SyncTransforms();PlanInsertionView();plannedStep=index;
                contextRenderers.Clear();foreach(var faded in cutaway){faded.collider.enabled=false;contextRenderers.Add(faded.renderer);}
                section.SetContext(contextRenderers);instructionKey=null;
            }
            if(selectedId==null)ShowInstruction(currentStep);
            UpdatePlaybackLabel();UpdateCamera();if(synchronizePhysics)Physics.SyncTransforms();
        }
        static float InsertionProgress(float fraction)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.76f,fraction));
        void CachePresentationPivots()
        {
            presentationPivot=engineRoot.InverseTransformPoint(WorkspaceBounds().center);
            foreach(var group in recipe.subAssemblies)
            {
                bool first=true;Bounds bounds=default;foreach(var visual in visuals.Values.Where(v=>v.definition.subAssemblyId==group.id))
                {
                    var mesh=visual.transform.GetComponent<MeshFilter>();var matrix=groups[group.id].worldToLocalMatrix*visual.transform.localToWorldMatrix;var b=mesh.sharedMesh.bounds;
                    for(int i=0;i<8;i++){var point=matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);}
                }groupPivots[group.id]=bounds.center;
            }
        }
        public float CurrentStepSeconds=>recipe.subAssemblies.Any(g=>g.rootPartId==currentStep?.id)?5:currentStep!=null && (currentStep.id.Contains("Bolt") || currentStep.id.Contains("Screw") || currentStep.id.Contains("Nut"))?2.6f:3.8f;
        Bounds FittingBounds()
        {
            carrierDefinitions.TryGetValue(currentStep.id,out var carrier);Bounds bounds=default;bool found=false;
            foreach(var visual in visuals.Values)
            {
                if(visual.definition.id!=currentStep.id && (carrier==null || visual.definition.subAssemblyId!=carrier.id))continue;
                if(!found){bounds=visual.renderer.bounds;found=true;}else bounds.Encapsulate(visual.renderer.bounds);
            }
            return found?bounds:WorkspaceBounds();
        }
        Bounds WorkspaceBounds()
        {
            Bounds bounds=default;bool found=false;
            foreach(var visual in visuals.Values)
            {
                if(displayWorkspace!=null && visual.definition.subAssemblyId!=displayWorkspace.id)continue;
                if(!found){bounds=visual.renderer.bounds;found=true;}else bounds.Encapsulate(visual.renderer.bounds);
            }
            return bounds;
        }
        IEnumerable<Vector3> SurfaceSamples(Visual visual)
        {
            if(visual.samples==null)
            {
                var mesh=visual.transform.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;
                int stride=Mathf.Max(3,triangles.Length/48/3*3);var samples=new List<Vector3>();
                for(int i=0;i+2<triangles.Length;i+=stride)samples.Add((vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3);
                visual.samples=samples.ToArray();
            }
            return visual.samples.Select(visual.transform.TransformPoint);
        }
        void PlanInsertionView()
        {
            cutaway.Clear();
            if(position>=1){cameraDirection=new Vector3(1,.7f,-1).normalized;return;}
            var carrier=recipe.subAssemblies.FirstOrDefault(g=>g.rootPartId==currentStep.id);
            var fitting=visuals.Values.Where(v=>v.definition.id==currentStep.id || carrier!=null && v.definition.subAssemblyId==carrier.id).ToHashSet();
            if(fitting.Count==0)return;
            var candidates=new List<Vector3>{engineRoot.InverseTransformDirection(cameraDirection).normalized,new Vector3(1,.7f,-1).normalized};
            foreach(float height in new[]{.7f,-.7f,1.6f,-1.6f})foreach(float x in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})candidates.Add(new Vector3(x,height,z).normalized);
            foreach(var side in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right})candidates.Add((side+Vector3.up*.45f).normalized);
            float best=float.PositiveInfinity;var hidden=new HashSet<Visual>();var previousDirection=cameraDirection;
            bool backfaces=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;
            try { foreach(var localDirection in candidates)
            {
                var direction=engineRoot.TransformDirection(localDirection).normalized;var eye=WorldFocus-pan+direction*FramingDistance();
                var blockers=new HashSet<Visual>();int blockedSamples=0;
                foreach(var visual in fitting.OrderByDescending(v=>v.renderer.bounds.size.sqrMagnitude).Take(3))
                {
                    var remaining=carrier!=null?engineRoot.TransformPoint(recipe.parts.First(p=>p.id==carrier.rootPartId).targetPosition)-groups[carrier.id].position
                        :visual.transform.parent.TransformPoint(visual.definition.targetPosition)-visual.transform.position;
                    // Check the approach and the seated pose, so the last frames
                    // cannot disappear into a housing after the view was chosen.
                    var approach=SurfaceSamples(visual).OrderBy(p=>(p-eye).sqrMagnitude).Take(3).ToArray();
                    foreach(var point in approach.Concat(approach.Select(p=>p+remaining)))
                    {
                        var delta=point-eye;bool blocked=false;
                        foreach(var hit in Physics.RaycastAll(eye,delta.normalized,Mathf.Max(0,delta.magnitude-.0001f),~0,QueryTriggerInteraction.Ignore))
                            if(colliderVisuals.TryGetValue(hit.collider,out var other) && other.renderer.enabled && !fitting.Contains(other)){blockers.Add(other);blocked=true;}
                        if(blocked)blockedSamples++;
                    }
                }
                float cost=blockedSamples*10+blockers.Count*.5f+Vector3.Angle(direction,previousDirection)/360;
                if(cost>=best)continue;best=cost;insertionView=localDirection;cameraDirection=direction;hidden=blockers;
            }} finally { Physics.queriesHitBackfaces=backfaces; }
            // Inherently enclosed fittings need a cutaway after choosing the clearest angle.
            // The context returns automatically at the next step and at the complete engine.
            foreach(var visual in hidden)cutaway.Add(visual);
        }
        void ShowInstruction(PartDefinition current)
        {
            var detail=details[current.id];
            string voice=detail.voiceId;if(playing && TimelineAudioEnabled && spokenStep!=voice){spokenStep=voice;speech.Speak(voice);}
            bool complete=position>=1,busy=speech.IsBusy;
            if(instructionKey==current.id && instructionComplete==complete && instructionBusy==busy)return;
            instructionKey=current.id;instructionComplete=complete;instructionBusy=busy;
            title.text=position>=1?"Complete engine":displayWorkspace!=null?"SUBASSEMBLY · "+recipe.parts.First(p=>p.id==displayWorkspace.rootPartId).displayName:"ASSEMBLY SEQUENCE";
            if(!speech.IsBusy)description.text=position>=1?"Tap a component to hear its purpose. Play the timeline to watch the engine being assembled.":detail.description;
            partMeta.text=position>=1?(recipe.parts.Count-recipe.subAssemblies.Count)+" physical components · "+recipe.subAssemblies.Count+" completed subassemblies":current.displayName;
            if(cutaway.Count>0 && position<1)partMeta.text+=" · See-through casing";
        }
        public bool SelectPart(string id)
        {
            if(!ready || !visuals.TryGetValue(id,out var visual) || !visual.renderer.enabled)return false;
            selectedId=id;instructionKey=null;playing=false;explanation.Cancel();if(operation.Active)operation.Paused=true;UpdatePlaybackLabel();var detail=EngineLearningContent.Detail(visual.definition);
            title.text=detail.title;description.text=detail.description;
            speech.Speak(detail.voiceId);
            partMeta.text="";return true;
        }
        public bool TapPart(Vector2 point)
        {
            if(!ready || !viewCamera.pixelRect.Contains(point) || OverUI(point))return false;
            // Moving CAD surfaces need no simulation contacts in a display. Cook
            // once, then expose them to physics only for an actual inspection tap.
            bool temporary=operation.Active;
            var ray=viewCamera.ScreenPointToRay(point);
            if(temporary)SetInspectionPhysics(true,ray);
            try
            {
                Physics.SyncTransforms();
                foreach(var hit in Physics.RaycastAll(ray,100,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
                {
                    if(colliderVisuals.TryGetValue(hit.collider,out var selected) && !operation.IsClipped(selected.renderer,hit.point))return SelectPart(selected.definition.id);
                }
                selectedId=null;if(operation.Active)ShowOperatingInstruction();else SetTimeline(position);return false;
            }
            finally {if(temporary)SetInspectionPhysics(false);}
        }
        void SetInspectionPhysics(bool enabled,Ray? ray=null)
        {
            foreach(var visual in visuals.Values)
            {
                bool active=enabled && visual.renderer.enabled && !cutaway.Contains(visual) && (!ray.HasValue || visual.renderer.bounds.IntersectRay(ray.Value));
                if(visual.collider.enabled!=active)visual.collider.enabled=active;
            }
        }
        public void RotateModel(Vector2 delta)
        {
            if(!ready || !MobileARSession.CanInteract(gameObject))return;Vector3 center=engineRoot.TransformPoint(focus);
            engineRoot.RotateAround(center,viewCamera.transform.up,-delta.x*.25f);engineRoot.RotateAround(center,viewCamera.transform.right,delta.y*.25f);
            if(spatialRig.IsTracked)GetComponent<MobileARSession>().KeepAboveWorkbench(engineRoot);
        }
        public void PanModel(Vector2 delta)
        {
            if(!ready || !MobileARSession.CanInteract(gameObject))return;float distance=Vector3.Distance(viewCamera.transform.position,engineRoot.TransformPoint(focus));
            float metres=2*distance*Mathf.Tan(viewCamera.fieldOfView*Mathf.Deg2Rad*.5f)/Mathf.Max(1,viewCamera.pixelHeight);
            var shift=(viewCamera.transform.right*delta.x+viewCamera.transform.up*delta.y)*metres;
            if((pan+shift).magnitude>radius*3)return;pan+=shift;engineRoot.position+=shift;
            if(spatialRig.IsTracked)GetComponent<MobileARSession>().KeepAboveWorkbench(engineRoot);
        }
        public void ZoomModel(float ratio){if(!ready || !MobileARSession.CanInteract(gameObject))return;if(spatialRig.IsTracked){GetComponent<MobileARSession>().Zoom(ratio,WorldFocus);return;}zoom=Mathf.Clamp(zoom*ratio,.45f,5);UpdateCamera();}
        public void ResetView(){if(spatialRig.IsTracked)GetComponent<MobileARSession>().ResetActiveBuild();else engineRoot.SetLocalPositionAndRotation(initialPosition,initialRotation);pan=Vector3.zero;zoom=1;plannedStep=-1;if(operation.Active)UpdateCamera();else SetTimeline(position);}
        void SetPresentationCamera(Camera camera){viewCamera=camera;if(operation)operation.SetCamera(camera);if(ready)UpdateViewport();}
        void OnDestroy(){if(spatialRig)spatialRig.CameraChanged-=SetPresentationCamera;}
        public void SetMode(DisplayMode mode)
        {
            if(!ready)return;if(Mode==mode){if(mode==DisplayMode.OperatingEngine && !operation.Active){operation.Play();operation.Paused=true;operation.SetCrankAngle(engineBookmark);SetInspectionPhysics(false);}return;}playing=false;selectedId=null;explanation.Cancel();speech.Stop();spokenStep=null;
            if(mode==DisplayMode.InfoTimeline){engineBookmark=operation.CrankAngle;operation.Stop();Mode=mode;SetTimeline(timelineBookmark);}
            else {timelineBookmark=position;SetTimeline(1);Mode=mode;operation.Play();operation.Paused=true;operation.SetCrankAngle(engineBookmark);SetInspectionPhysics(false);cameraDirection=engineRoot.TransformDirection(new Vector3(1,.18f,-.12f).normalized);zoom=1.35f;UpdateCamera();ShowOperatingInstruction();}
            UpdatePlaybackLabel();
        }
        public void TogglePlay()
        {
            if(Mode==DisplayMode.OperatingEngine && explanation.Active){explanation.SetPaused(!explanation.Paused);UpdatePlaybackLabel();return;}
            selectedId=null;speech.Stop();
            if(Mode==DisplayMode.OperatingEngine)operation.Paused=!operation.Paused;
            else {playing=!playing;spokenStep=null;plannedStep=-1;if(playing && position>=1)SetTimeline(0);else if(playing)ShowInstruction(currentStep);}
            UpdatePlaybackLabel();
        }
        public void TogglePlayback(){SetMode(DisplayMode.InfoTimeline);TogglePlay();}
        public void ToggleEngineAnimation()
        {
            SetMode(DisplayMode.OperatingEngine);TogglePlay();ShowOperatingInstruction();
        }
        public void ExplainEngine(){SetMode(DisplayMode.OperatingEngine);selectedId=null;explanation.Play();UpdatePlaybackLabel();}
        public void ToggleSection(){if(!operation.Active){ToggleEngineAnimation();operation.Paused=true;}else operation.SectionEnabled=!operation.SectionEnabled;UpdatePlaybackLabel();}
        void ReplayVoice(){if(selectedId!=null)speech.Speak(EngineLearningContent.Detail(visuals[selectedId].definition).voiceId);else if(currentStep!=null && (!playing || TimelineAudioEnabled))speech.Speak(DisplayPlaybackSettings.Description(currentStep).voiceId);}
        void ShowOperatingInstruction()
        {
            if(!operation.Active)return;progress.text=operation.CycleSummary;scrubber.SetValue(operation.CrankAngle/720);
            if(selectedId==null){if(!explanation.Active){title.text="INSIDE THE RUNNING ENGINE";description.text=operation.Explanation;}partMeta.text=explanation.Active?"Four-stroke tour · Follow cylinder 1": "Slowed teaching animation · Crankshaft "+operation.TeachingRPM.ToString("0")+" rpm · Camshafts "+(operation.TeachingRPM*.5f).ToString("0.#")+" rpm";}
        }
        void UpdatePlaybackLabel()
        {
            bool motion=Mode==DisplayMode.OperatingEngine;
            if(playLabel)playLabel.text=motion?(explanation.Active?(explanation.Paused?"RESUME EXPLANATION":"PAUSE EXPLANATION"):operation.Paused?"PLAY ENGINE":"PAUSE ENGINE"):(playing?"PAUSE TIMELAPSE":"PLAY TIMELAPSE");
            if(sectionLabel){sectionLabel.text="CAPTIONS · "+(WorkshopGuidance.Captions?"ON":"OFF");}
            if(explainLabel)explainLabel.text="EXPLAIN ENGINE CYCLE";
            if(infoModeCard){infoModeCard.color=motion?WorkshopTheme.Surface:WorkshopTheme.AccentWash;infoModeCard.GetComponent<WorkshopCard>().normal=infoModeCard.color;}if(engineModeCard){engineModeCard.color=motion?WorkshopTheme.AccentWash:WorkshopTheme.Surface;engineModeCard.GetComponent<WorkshopCard>().normal=engineModeCard.color;}
        }
        void Update()
        {
            if(!ready || WorkshopTutorial.BlocksInput)return;
            if(info)info.gameObject.SetActive(!WorkshopTutorial.IsOpen && (selectedId!=null || position<1 || operation.Active));
            if(lastSize!=new Vector2Int(Screen.width,Screen.height) || lastSafe!=Screen.safeArea)UpdateViewport();
            if(playing && MobileARSession.CanInteract(gameObject))
            {
                if(Quaternion.Angle(presentationOrientation,wantedPresentationOrientation)>.1f){presentationOrientation=Quaternion.RotateTowards(presentationOrientation,wantedPresentationOrientation,Mathf.Min(Time.unscaledDeltaTime,.1f)*225);SetTimeline(position,false);ReadPointer();return;}
                float next=position+Mathf.Min(Time.unscaledDeltaTime,.1f)*playbackSpeed/(timeline.Count*CurrentStepSeconds);
                int index=Mathf.Min(timeline.Count-1,Mathf.FloorToInt(position*timeline.Count));
                if(TimelineAudioEnabled && next*timeline.Count>=index+1 && speech.IsBusy)
                {
                    string nextVoice=index+1<timeline.Count?DisplayPlaybackSettings.Description(timeline[index+1]).voiceId:null;
                    if(nextVoice==null || nextVoice!=spokenStep)next=(index+.9999f)/timeline.Count;
                }
                SetTimeline(next,false);
            }
            if(position>=1 && playing){playing=false;UpdatePlaybackLabel();}
            if(operation.Active){ShowOperatingInstruction();UpdatePlaybackLabel();}
            ReadPointer();if(InputHelper.IsKeyDown(KeyCode.Escape))ExperienceMenu.GoHome();
        }
        void UpdateViewport()
        {
            lastSize=new Vector2Int(Screen.width,Screen.height);lastSafe=Screen.safeArea;
            canvas.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution=Screen.width>Screen.height?new Vector2(1920,1080):new Vector2(1080,1920);
            canvas.GetComponent<UnityEngine.UI.CanvasScaler>().matchWidthOrHeight=Screen.width>Screen.height?.5f:0;
            safe.anchorMin=lastSafe.min/new Vector2(Screen.width,Screen.height);safe.anchorMax=lastSafe.max/new Vector2(Screen.width,Screen.height);safe.offsetMin=safe.offsetMax=Vector2.zero;Canvas.ForceUpdateCanvases();
            if(spatialRig.IsTracked)return;
            float bottom=lastSafe.yMin+142*canvas.scaleFactor,top=lastSafe.yMax-(Screen.width>Screen.height?100:320)*canvas.scaleFactor;
            float inset=Screen.width>Screen.height?lastSafe.width*.32f:0;
            viewCamera.pixelRect=new Rect(lastSafe.xMin+inset,bottom,lastSafe.width-inset,Mathf.Max(100,top-bottom));plannedStep=-1;if(operation.Active)UpdateCamera();else SetTimeline(position);
        }
        void UpdateCamera()
        {
            if(spatialRig && spatialRig.IsTracked)
            {
                if(position<1 && !operation.Active)
                {
                    var pivot=WorldFocus;var eye=(viewCamera.transform.position-pivot).normalized;
                    var correction=Quaternion.FromToRotation(engineRoot.TransformDirection(insertionView),eye);
                    var turn=Quaternion.Slerp(Quaternion.identity,correction,playing?1-Mathf.Exp(-6*Time.unscaledDeltaTime):1);
                    engineRoot.SetPositionAndRotation(pivot+turn*(engineRoot.position-pivot),turn*engineRoot.rotation);
                }
                return;
            }
            float distance=FramingDistance();
            var target=engineRoot.TransformPoint(focus)-pan;var desired=target+cameraDirection*distance;
            var rotation=Quaternion.LookRotation(target-desired,Mathf.Abs(Vector3.Dot(cameraDirection,Vector3.up))>.95f?Vector3.forward:Vector3.up);
            float blend=playing?1-Mathf.Exp(-3.5f*Time.unscaledDeltaTime):1;
            viewCamera.transform.SetPositionAndRotation(Vector3.Lerp(viewCamera.transform.position,desired,blend),Quaternion.Slerp(viewCamera.transform.rotation,rotation,blend));
        }
        float FramingDistance()
        {
            if(spatialRig && spatialRig.IsTracked)return Vector3.Distance(viewCamera.transform.position,WorldFocus);
            float vertical=viewCamera.fieldOfView*Mathf.Deg2Rad*.5f,horizontal=Mathf.Atan(Mathf.Tan(vertical)*viewCamera.aspect);
            return radius/Mathf.Sin(Mathf.Min(vertical,horizontal))*1.18f/zoom;
        }
        void ReadPointer()
        {
            if(!MobileARSession.CanInteract(gameObject)){pointerActive=multiTouch=dragging=false;return;}
            Vector2 point=default;bool down=false,pressed=false,up=false;
#if ENABLE_INPUT_SYSTEM
            if(Touchscreen.current!=null)
            {
                UnityEngine.InputSystem.Controls.TouchControl firstTouch=null,secondTouch=null;int touchCount=0;
                foreach(var candidate in Touchscreen.current.touches)if(candidate.press.isPressed){if(touchCount==0)firstTouch=candidate;else if(touchCount==1)secondTouch=candidate;touchCount++;}
                if(touchCount>=2)
                {
                    var first=firstTouch.position.ReadValue();var second=secondTouch.position.ReadValue();Vector2 center=(first+second)*.5f;float span=Vector2.Distance(first,second);
                    if(!multiTouch)panAllowed=touchCount==2 && viewCamera.pixelRect.Contains(first) && viewCamera.pixelRect.Contains(second) && !OverUI(first) && !OverUI(second);
                    else if(panAllowed && touchCount==2){PanModel(center-multiCenter);if(lastSpan>10)ZoomModel(span/lastSpan);}
                    if(touchCount>2)panAllowed=false;multiCenter=center;lastSpan=span;multiTouch=true;pointerActive=false;return;
                }
                if(multiTouch){if(touchCount==0)multiTouch=false;pointerActive=false;return;}
                var touch=Touchscreen.current.primaryTouch;var phase=touch.phase.ReadValue();
                if(phase!=UnityEngine.InputSystem.TouchPhase.None){point=touch.position.ReadValue();down=phase==UnityEngine.InputSystem.TouchPhase.Began;pressed=touch.press.isPressed;up=phase==UnityEngine.InputSystem.TouchPhase.Ended;if(phase==UnityEngine.InputSystem.TouchPhase.Canceled){pointerActive=false;return;}}
                else if(Mouse.current!=null){point=Mouse.current.position.ReadValue();down=Mouse.current.leftButton.wasPressedThisFrame;pressed=Mouse.current.leftButton.isPressed;up=Mouse.current.leftButton.wasReleasedThisFrame;}
            }
            else if(Mouse.current!=null){point=Mouse.current.position.ReadValue();down=Mouse.current.leftButton.wasPressedThisFrame;pressed=Mouse.current.leftButton.isPressed;up=Mouse.current.leftButton.wasReleasedThisFrame;}
            if(Mouse.current!=null && viewCamera.pixelRect.Contains(point) && !OverUI(point))
            {
                ZoomModel(Mathf.Exp(Mouse.current.scroll.ReadValue().y/120*.12f));
                if(Mouse.current.middleButton.isPressed){PanModel(Mouse.current.delta.ReadValue());pointerActive=false;return;}
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            point=Input.mousePosition;down=Input.GetMouseButtonDown(0);pressed=Input.GetMouseButton(0);up=Input.GetMouseButtonUp(0);
#endif
            if(down){pointerActive=viewCamera.pixelRect.Contains(point) && !OverUI(point);startPointer=lastPointer=point;dragging=false;}
            if(!pointerActive)return;
            if(pressed){if(Vector2.Distance(point,startPointer)>12*canvas.scaleFactor)dragging=true;if(dragging)RotateModel((point-lastPointer)/canvas.scaleFactor);lastPointer=point;}
            if(up){if(!dragging)TapPart(point);pointerActive=false;}
        }
        bool OverUI(Vector2 point)
        {
            var events=EventSystem.current;if(!events)return false;if(pointerEvents!=events || pointerEvent==null){pointerEvents=events;pointerEvent=new PointerEventData(events);}
            pointerEvent.Reset();pointerEvent.position=point;pointerHits.Clear();events.RaycastAll(pointerEvent,pointerHits);
            foreach(var hit in pointerHits)if(hit.module is UnityEngine.UI.GraphicRaycaster)return true;return false;
        }
        void CreateUI()
        {
            heading=Resources.Load<TMP_FontAsset>("WorkshopHeading");body=Resources.Load<TMP_FontAsset>("WorkshopBody");
            var root=Rect("Engine display canvas",transform);canvas=root.gameObject.AddComponent<Canvas>();root.gameObject.AddComponent<WorkshopCanvasRegistration>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scaler=root.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;root.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            if(!FindAnyObjectByType<EventSystem>()){
                var events=new GameObject("Display event system",typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                events.AddComponent<InputSystemUIInputModule>();
#else
                events.AddComponent<StandaloneInputModule>();
#endif
            }
            safe=Rect("Safe area",root);Anchor(safe,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var header=Box("Display header",safe,Panel);Anchor(header,Vector2.up,Vector2.one,new Vector2(0,-90),Vector2.zero);
            Label(header,"ENGINE DISPLAY",25,true,new Vector2(24,26),new Vector2(-250,-12));
            var gestureHelp=Label(header,"1 finger rotate  ·  2 fingers move  ·  pinch to zoom",19,false,new Vector2(24,8),new Vector2(-470,36));
            gestureHelp.rectTransform.anchorMax=new Vector2(1,0);gestureHelp.rectTransform.offsetMin=new Vector2(24,8);gestureHelp.rectTransform.offsetMax=new Vector2(-24,36);
            gestureHelp.gameObject.SetActive(false);
            Card(header,"MAIN MENU",ExperienceMenu.GoHome,Vector2.one,Vector2.one,new Vector2(-224,-70),new Vector2(-24,-18));
            footer=Box("Timeline dock",safe,Ink);Anchor(footer,Vector2.zero,Vector2.right,Vector2.zero,new Vector2(0,132));
            var captions=EngineDisplayCaption.Create(safe,speech,true);info=(RectTransform)captions.transform;title=captions.Heading;description=captions.Text;partMeta=captions.Context;
            progress=Label(footer,"",19,false,new Vector2(24,100),new Vector2(-24,128));progress.rectTransform.anchorMax=new Vector2(1,0);progress.rectTransform.offsetMin=new Vector2(24,100);progress.rectTransform.offsetMax=new Vector2(-24,128);progress.color=Mint;
            var rail=Box("Assembly timeline slider",footer,WorkshopTheme.Border);Anchor(rail,Vector2.zero,Vector2.right,new Vector2(32,76),new Vector2(-32,90));
            var fill=Box("Timeline fill",rail,Mint);Anchor(fill,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);fill.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            var thumb=Box("Timeline handle",rail,WorkshopTheme.Text);Anchor(thumb,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(-10,-15),new Vector2(10,15));thumb.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            rail.GetComponent<UnityEngine.UI.Image>().raycastPadding=new Vector4(-8,-14,-8,-14);
            scrubber=rail.gameObject.AddComponent<AssemblyTimelineScrubber>();scrubber.Configure(fill,thumb);scrubber.changed=value=>{playing=false;selectedId=null;explanation.Cancel();speech.Stop();if(operation.Active){operation.Paused=true;operation.SetCrankAngle(value*720);}else SetTimeline(value);};
            var play=Card(footer,"PLAY TIMELAPSE",TogglePlay,new Vector2(.02f,0),new Vector2(.44f,0),new Vector2(0,10),new Vector2(0,58));playLabel=play.GetComponentInChildren<TextMeshProUGUI>();
            Card(footer,"DISPLAY SETTINGS",()=>settingsPanel.gameObject.SetActive(!settingsPanel.gameObject.activeSelf),new Vector2(.64f,0),new Vector2(.98f,0),new Vector2(0,10),new Vector2(0,58)).GetComponentInChildren<TextMeshProUGUI>().text="SETTINGS";
            settingsPanel=Box("Display settings",safe,Panel);settingsPanel.anchorMin=settingsPanel.anchorMax=new Vector2(.5f,.5f);settingsPanel.pivot=new Vector2(.5f,.5f);settingsPanel.sizeDelta=new Vector2(800,670);
            Label(settingsPanel,"ENGINE DISPLAY",28,true,new Vector2(24,610),new Vector2(-180,-18));
            Card(settingsPanel,"DONE",()=>settingsPanel.gameObject.SetActive(false),Vector2.one,Vector2.one,new Vector2(-156,-62),new Vector2(-24,-16));
            infoModeCard=Card(settingsPanel,"INFO / TIMELINE",()=>SetMode(DisplayMode.InfoTimeline),new Vector2(0,0),new Vector2(.5f,0),new Vector2(24,536),new Vector2(-10,588)).GetComponent<UnityEngine.UI.Image>();
            engineModeCard=Card(settingsPanel,"ENGINE MOTION",()=>SetMode(DisplayMode.OperatingEngine),new Vector2(.5f,0),new Vector2(1,0),new Vector2(10,536),new Vector2(-24,588)).GetComponent<UnityEngine.UI.Image>();
            audioLabel=Card(settingsPanel,"TIMELAPSE AUDIO",()=>SetTimelineAudio(!TimelineAudioEnabled),new Vector2(0,0),new Vector2(.5f,0),new Vector2(24,460),new Vector2(-10,510)).GetComponentInChildren<TextMeshProUGUI>();
            audioLabel.text="TIMELAPSE AUDIO · "+(TimelineAudioEnabled?"ON":"OFF");
            sectionLabel=Card(settingsPanel,"CAPTIONS",()=>WorkshopGuidance.SetCaptions(!WorkshopGuidance.Captions),new Vector2(.5f,0),new Vector2(1,0),new Vector2(10,460),new Vector2(-24,510)).GetComponentInChildren<TextMeshProUGUI>();
            speedLabel=Label(settingsPanel,"",23,true,new Vector2(24,394),new Vector2(-24,-238));
            var speedRail=Box("Playback speed slider",settingsPanel,WorkshopTheme.Border);Anchor(speedRail,Vector2.zero,Vector2.right,new Vector2(28,366),new Vector2(-28,380));speedRail.GetComponent<UnityEngine.UI.Image>().raycastPadding=new Vector4(-8,-16,-8,-16);
            var speedFill=Box("Speed fill",speedRail,Mint);Anchor(speedFill,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);speedFill.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            var speedThumb=Box("Speed handle",speedRail,WorkshopTheme.Text);Anchor(speedThumb,Vector2.zero,Vector2.zero,new Vector2(-10,-8),new Vector2(10,22));speedThumb.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            var speedControl=speedRail.gameObject.AddComponent<AssemblyTimelineScrubber>();speedControl.Configure(speedFill,speedThumb);speedControl.changed=v=>SetPlaybackSpeed(.1f+v*3.9f);speedControl.SetValue((playbackSpeed-.1f)/3.9f);
            engineSpeedLabel=Label(settingsPanel,"Engine movement · 12 rpm",23,true,new Vector2(24,294),new Vector2(-24,-338));
            var engineRail=Box("Engine movement speed",settingsPanel,WorkshopTheme.Border);Anchor(engineRail,Vector2.zero,Vector2.right,new Vector2(28,266),new Vector2(-28,280));engineRail.GetComponent<UnityEngine.UI.Image>().raycastPadding=new Vector4(-8,-16,-8,-16);
            var engineFill=Box("Movement fill",engineRail,Mint);Anchor(engineFill,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);engineFill.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;var engineThumb=Box("Movement handle",engineRail,WorkshopTheme.Text);Anchor(engineThumb,Vector2.zero,Vector2.zero,new Vector2(-10,-8),new Vector2(10,22));engineThumb.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            var engineSpeed=engineRail.gameObject.AddComponent<AssemblyTimelineScrubber>();engineSpeed.Configure(engineFill,engineThumb);engineSpeed.changed=v=>{operation.SetSpeed(4+v*26);engineSpeedLabel.text="Engine movement · "+operation.TeachingRPM.ToString("0")+" rpm";};engineSpeed.SetValue(8f/26);
            explainLabel=Card(settingsPanel,"EXPLAIN ENGINE CYCLE",ExplainEngine,new Vector2(0,0),new Vector2(.5f,0),new Vector2(24,184),new Vector2(-10,232)).GetComponentInChildren<TextMeshProUGUI>();
            Card(settingsPanel,"RESET VIEW",ResetView,new Vector2(.5f,0),new Vector2(1,0),new Vector2(10,184),new Vector2(-24,232));
            Card(settingsPanel,"ASSEMBLED VIEW",()=>{playing=false;selectedId=null;explanation.Cancel();speech.Stop();SetTimeline(1);settingsPanel.gameObject.SetActive(false);},Vector2.zero,Vector2.right,new Vector2(24,110),new Vector2(-24,158));
            if(GetComponent<MobileARSession>() is MobileARSession ar)
            {
                Card(settingsPanel,"REPOSITION",()=>{settingsPanel.gameObject.SetActive(false);ar.Reposition();},Vector2.zero,new Vector2(.5f,0),new Vector2(24,28),new Vector2(-10,88));
                Card(settingsPanel,"PHONE VIEW",ar.UsePhoneView,new Vector2(.5f,0),Vector2.right,new Vector2(10,28),new Vector2(-24,88));
            }
            else Label(settingsPanel,"Choose a mode here. Play and the timeline remain below the engine.",23,false,new Vector2(24,24),new Vector2(-24,-592));
            SetPlaybackSpeed(playbackSpeed);settingsPanel.gameObject.SetActive(false);if(QuestVRSession.Instance && QuestVRSession.Instance.Active)WorkshopTypography.ApplyVR(canvas);
        }
        RectTransform Card(Transform parent,string text,Action action,Vector2 min,Vector2 max,Vector2 low,Vector2 high)
        {
            var rect=Box(text+" card",parent,WorkshopTheme.AccentWash);Anchor(rect,min,max,low,high);var card=rect.gameObject.AddComponent<WorkshopCard>();card.normal=WorkshopTheme.AccentWash;card.highlight=WorkshopTheme.Hover;card.clicked=action;
            var label=Label(rect,text,22,true,new Vector2(4,2),new Vector2(-4,-2));label.alignment=TextAlignmentOptions.Center;return rect;
        }
        TextMeshProUGUI Label(Transform parent,string text,float size,bool bold,Vector2 low,Vector2 high)
        {
            var rect=Rect("Label",parent);Anchor(rect,Vector2.zero,Vector2.one,low,high);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();label.font=bold?heading:body;label.text=text;label.fontSize=size;label.color=new Color(.08f,.14f,.2f);label.raycastTarget=false;label.textWrappingMode=TextWrappingModes.Normal;return label;
        }
        static RectTransform Rect(string name,Transform parent){var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);return rect;}
        static RectTransform Box(string name,Transform parent,Color color){var rect=Rect(name,parent);var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;WorkshopUIArt.Card(image);return rect;}
        static void Anchor(RectTransform rect,Vector2 min,Vector2 max,Vector2 low,Vector2 high){rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=low;rect.offsetMax=high;}
    }
}
