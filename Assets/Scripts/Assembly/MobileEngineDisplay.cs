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
        sealed class Visual { public PartDefinition definition;public Transform transform;public MeshRenderer renderer;public MeshCollider collider;public Vector3 insertionOffset;public Vector3[] samples; }
        readonly Dictionary<string,Visual> visuals=new Dictionary<string,Visual>();
        readonly Dictionary<Collider,Visual> colliderVisuals=new Dictionary<Collider,Visual>();
        readonly HashSet<Visual> cutaway=new HashSet<Visual>();
        readonly Dictionary<string,Transform> groups=new Dictionary<string,Transform>();
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
        Vector3 focus,pan,initialPosition;
        float radius,zoom=1,position;
        bool playing,ready,pointerActive,dragging,multiTouch,panAllowed;
        Vector2 lastPointer,startPointer,multiCenter;
        float lastSpan;
        Vector2Int lastSize;Rect lastSafe;
        string selectedId;
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
        SpatialExperienceRig spatialRig;
        public void Configure(Transform root,Camera camera,TextAsset asset){engineRoot=root;viewCamera=camera;recipeAsset=asset;}
        void Start()
        {
            speech=gameObject.AddComponent<NarrationSpeech>();section=gameObject.AddComponent<EngineSectionView>();operation=gameObject.AddComponent<EngineOperatingAnimation>();explanation=gameObject.AddComponent<EngineOperationNarrator>();SetPlaybackSpeed(DisplayPlaybackSettings.Speed);
            engineRoot.localScale*=.6f;
            spatialRig=SpatialExperienceRig.Ensure(gameObject,engineRoot,viewCamera);viewCamera.backgroundColor=Ink;
            spatialRig.CameraChanged+=SetPresentationCamera;
            recipe=AssemblyRecipeStore.Parse(recipeAsset.text);timeline=EngineLearningContent.Timeline(recipe);
            for(int i=0;i<timeline.Count;i++)indices[timeline[i].id]=i;
            foreach(var group in recipe.subAssemblies)
            {
                var t=new GameObject(group.rootPartId+" display fixture").transform;t.SetParent(engineRoot,false);groups[group.id]=t;
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
                    insertionOffset=AssemblyMotion.DisplayInsertionOffset(d,filter.sharedMesh.bounds,filter.transform.localScale)};
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
            initialRotation=engineRoot.localRotation;initialPosition=engineRoot.localPosition;CreateUI();ready=true;SetTimeline(1);operation.Configure(engineRoot,recipe,viewCamera,section);
            explanation.Configure(operation,speech,(heading,text)=>{title.text=heading;description.text=text;});
            UpdateViewport();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        public float PartProgress(string id)
        {
            if(!indices.TryGetValue(id,out int index))return recipe.parts.Any(p=>p.id==id && p.isBase)?1:0;
            return Mathf.Clamp01(position*timeline.Count-index);
        }
        public void SetTimeline(float value)
        {
            if(!ready)return;if(operation.Active){explanation?.Cancel();operation.Stop();}Mode=DisplayMode.InfoTimeline;position=Mathf.Clamp01(value);scrubber.SetValue(position);
            int done=Mathf.FloorToInt(position*timeline.Count+.00001f),index=Mathf.Clamp(done,0,timeline.Count-1);
            currentStep=timeline[index];var workspace=recipe.subAssemblies.FirstOrDefault(g=>g.id==currentStep.subAssemblyId);
            if(workspace!=displayWorkspace){displayWorkspace=workspace;pan=Vector3.zero;selectedId=null;plannedStep=-1;}
            foreach(var group in recipe.subAssemblies)
            {
                var carrier=recipe.parts.First(p=>p.id==group.rootPartId);float t=InsertionProgress(PartProgress(carrier.id));
                var transform=groups[group.id];
                // Separate display stage: only the unit being built occupies this area.
                // Complete units rejoin the engine when their carrier installation begins.
                transform.localPosition=group==displayWorkspace?Vector3.zero:carrier.targetPosition+Vector3.up*Mathf.Clamp(group.focusRadius*.3f,.025f,.18f)*(t>0?1-t:0);
                transform.localRotation=group==displayWorkspace?Quaternion.identity:carrier.targetRotation;
            }
            foreach(var visual in visuals.Values)
            {
                float t=PartProgress(visual.definition.id);bool visible=t>0;
                if(displayWorkspace!=null)visible&=visual.definition.subAssemblyId==displayWorkspace.id;
                else if(!string.IsNullOrEmpty(visual.definition.subAssemblyId))
                    visible&=PartProgress(recipe.subAssemblies.First(g=>g.id==visual.definition.subAssemblyId).rootPartId)>0;
                visual.renderer.enabled=visible;visual.collider.enabled=visible;
                visual.transform.localPosition=visual.definition.targetPosition+visual.insertionOffset*(1-InsertionProgress(t));
                visual.transform.localRotation=visual.definition.targetRotation;
            }
            progress.text=done+" / "+timeline.Count+"   ·   "+recipe.chapters.First(c=>c.id==currentStep.chapterId).title;
            if(selectedId!=null && (!visuals.TryGetValue(selectedId,out var selected) || !selected.renderer.enabled))selectedId=null;
            var bounds=WorkspaceBounds();var fitting=FittingBounds();
            float frameRadius=position>=1?bounds.extents.magnitude:Mathf.Max(fitting.extents.magnitude*1.6f,bounds.extents.magnitude*.32f);
            focus=engineRoot.InverseTransformPoint(position>=1?bounds.center:Vector3.Lerp(bounds.center,fitting.center,.78f));radius=Mathf.Max(.045f,frameRadius);
            Physics.SyncTransforms();
            if(plannedStep!=index || position>=1){PlanInsertionView();plannedStep=index;}
            section.SetContext(cutaway.Select(v=>(Renderer)v.renderer));
            foreach(var faded in cutaway)faded.collider.enabled=false;
            if(selectedId==null)ShowInstruction(currentStep);
            UpdatePlaybackLabel();UpdateCamera();Physics.SyncTransforms();
        }
        static float InsertionProgress(float fraction)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.76f,fraction));
        public float CurrentStepSeconds=>recipe.subAssemblies.Any(g=>g.rootPartId==currentStep?.id)?5:currentStep!=null && (currentStep.id.Contains("Bolt") || currentStep.id.Contains("Screw") || currentStep.id.Contains("Nut"))?2.6f:3.8f;
        Bounds FittingBounds()
        {
            var carrier=recipe.subAssemblies.FirstOrDefault(g=>g.rootPartId==currentStep.id);
            var fitting=visuals.Values.Where(v=>v.definition.id==currentStep.id || carrier!=null && v.definition.subAssemblyId==carrier.id).ToArray();
            if(fitting.Length==0)return WorkspaceBounds();var bounds=fitting[0].renderer.bounds;foreach(var visual in fitting.Skip(1))bounds.Encapsulate(visual.renderer.bounds);return bounds;
        }
        Bounds WorkspaceBounds()
        {
            var context=visuals.Values.Where(v=>displayWorkspace==null || v.definition.subAssemblyId==displayWorkspace.id).ToArray();
            var bounds=context[0].renderer.bounds;foreach(var visual in context.Skip(1))bounds.Encapsulate(visual.renderer.bounds);return bounds;
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
            var detail=DisplayPlaybackSettings.Description(current);
            string voice=detail.voiceId;if(playing && TimelineAudioEnabled && spokenStep!=voice){spokenStep=voice;speech.Speak(voice);}
            title.text=position>=1?"Complete engine":displayWorkspace!=null?"SUBASSEMBLY · "+recipe.parts.First(p=>p.id==displayWorkspace.rootPartId).displayName:"ASSEMBLY SEQUENCE";
            if(!speech.IsBusy)description.text=position>=1?"Tap a component to hear its purpose. Play the timeline to watch the engine being assembled.":detail.description;
            partMeta.text=position>=1?(recipe.parts.Count-recipe.subAssemblies.Count)+" physical components · "+recipe.subAssemblies.Count+" completed subassemblies":current.displayName;
            if(cutaway.Count>0 && position<1)partMeta.text+=" · See-through casing";
        }
        public bool SelectPart(string id)
        {
            if(!ready || !visuals.TryGetValue(id,out var visual) || !visual.renderer.enabled)return false;
            selectedId=id;playing=false;explanation.Cancel();if(operation.Active)operation.Paused=true;UpdatePlaybackLabel();var detail=EngineLearningContent.Detail(visual.definition);
            title.text=detail.title;description.text=detail.description;
            speech.Speak(detail.voiceId);
            partMeta.text=recipe.chapters.First(c=>c.id==visual.definition.chapterId).title+"   ·   Assembly step "+visual.definition.order.ToString("000");return true;
        }
        public bool TapPart(Vector2 point)
        {
            if(!ready || !viewCamera.pixelRect.Contains(point) || OverUI(point))return false;
            foreach(var hit in Physics.RaycastAll(viewCamera.ScreenPointToRay(point),100,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            {
                var selected=visuals.FirstOrDefault(pair=>pair.Value.collider==hit.collider);if(selected.Key!=null && !operation.IsClipped(selected.Value.renderer,hit.point))return SelectPart(selected.Key);
            }
            selectedId=null;if(operation.Active)ShowOperatingInstruction();else SetTimeline(position);return false;
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
            if(!ready)return;if(Mode==mode){if(mode==DisplayMode.OperatingEngine && !operation.Active){operation.Play();operation.Paused=true;operation.SetCrankAngle(engineBookmark);}return;}playing=false;selectedId=null;explanation.Cancel();speech.Stop();spokenStep=null;
            if(mode==DisplayMode.InfoTimeline){engineBookmark=operation.CrankAngle;operation.Stop();Mode=mode;SetTimeline(timelineBookmark);}
            else {timelineBookmark=position;SetTimeline(1);Mode=mode;operation.Play();operation.Paused=true;operation.SetCrankAngle(engineBookmark);cameraDirection=engineRoot.TransformDirection(new Vector3(1,.18f,-.12f).normalized);zoom=1.35f;UpdateCamera();ShowOperatingInstruction();}
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
            if(sectionLabel){sectionLabel.text=motion?(operation.SectionEnabled?"SECTION ON":"SECTION OFF"):"REPLAY VOICE";}
            if(explainLabel)explainLabel.text=motion?"EXPLAIN CYCLE":"AUDIO · "+(TimelineAudioEnabled?"ON":"OFF");
            if(infoModeCard){infoModeCard.color=motion?WorkshopTheme.Surface:WorkshopTheme.AccentWash;infoModeCard.GetComponent<WorkshopCard>().normal=infoModeCard.color;}if(engineModeCard){engineModeCard.color=motion?WorkshopTheme.AccentWash:WorkshopTheme.Surface;engineModeCard.GetComponent<WorkshopCard>().normal=engineModeCard.color;}
        }
        void Update()
        {
            if(!ready || WorkshopTutorial.BlocksInput)return;
            if(lastSize!=new Vector2Int(Screen.width,Screen.height) || lastSafe!=Screen.safeArea)UpdateViewport();
            if(playing && MobileARSession.CanInteract(gameObject))
            {
                float next=position+Mathf.Min(Time.unscaledDeltaTime,.1f)*playbackSpeed/(timeline.Count*CurrentStepSeconds);
                int index=Mathf.Min(timeline.Count-1,Mathf.FloorToInt(position*timeline.Count));
                if(TimelineAudioEnabled && next*timeline.Count>=index+1 && speech.IsBusy)
                {
                    string nextVoice=index+1<timeline.Count?DisplayPlaybackSettings.Description(timeline[index+1]).voiceId:null;
                    if(nextVoice==null || nextVoice!=spokenStep)next=(index+.9999f)/timeline.Count;
                }
                SetTimeline(next);
            }
            if(position>=1 && playing){playing=false;UpdatePlaybackLabel();}
            if(operation.Active){ShowOperatingInstruction();UpdatePlaybackLabel();}
            ReadPointer();if(InputHelper.IsKeyDown(KeyCode.Escape))ExperienceMenu.GoHome();
        }
        void UpdateViewport()
        {
            lastSize=new Vector2Int(Screen.width,Screen.height);lastSafe=Screen.safeArea;
            canvas.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution=Screen.width>Screen.height?new Vector2(1920,1080):new Vector2(1080,1920);
            safe.anchorMin=lastSafe.min/new Vector2(Screen.width,Screen.height);safe.anchorMax=lastSafe.max/new Vector2(Screen.width,Screen.height);safe.offsetMin=safe.offsetMax=Vector2.zero;Canvas.ForceUpdateCanvases();
            if(spatialRig.IsTracked)return;
            float bottom=lastSafe.yMin+208*canvas.scaleFactor,top=lastSafe.yMax-126*canvas.scaleFactor;
            float inset=Screen.width>Screen.height?330*canvas.scaleFactor:0;
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
                var touches=Touchscreen.current.touches.Where(t=>t.press.isPressed).ToArray();
                if(touches.Length>=2)
                {
                    Vector2 center=(touches[0].position.ReadValue()+touches[1].position.ReadValue())*.5f;float span=Vector2.Distance(touches[0].position.ReadValue(),touches[1].position.ReadValue());
                    if(!multiTouch)panAllowed=touches.Length==2 && touches.All(t=>viewCamera.pixelRect.Contains(t.position.ReadValue()) && !OverUI(t.position.ReadValue()));
                    else if(panAllowed && touches.Length==2){PanModel(center-multiCenter);if(lastSpan>10)ZoomModel(span/lastSpan);}
                    if(touches.Length>2)panAllowed=false;multiCenter=center;lastSpan=span;multiTouch=true;pointerActive=false;return;
                }
                if(multiTouch){if(touches.Length==0)multiTouch=false;pointerActive=false;return;}
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
        bool OverUI(Vector2 point){if(!EventSystem.current)return false;var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);return hits.Any(h=>h.module is UnityEngine.UI.GraphicRaycaster);}
        void CreateUI()
        {
            heading=Resources.Load<TMP_FontAsset>("WorkshopHeading");body=Resources.Load<TMP_FontAsset>("WorkshopBody");
            var root=Rect("Engine display canvas",transform);canvas=root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
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
            var header=Box("Display header",safe,Panel);Anchor(header,Vector2.up,Vector2.one,new Vector2(0,-110),Vector2.zero);
            Label(header,"ENGINE / LAB  ·  DISPLAY",30,true,new Vector2(24,45),new Vector2(-250,-20));
            var gestureHelp=Label(header,"1 finger rotate  ·  2 fingers move  ·  pinch to zoom",19,false,new Vector2(24,18),new Vector2(-24,55));
            gestureHelp.rectTransform.anchorMax=new Vector2(1,0);gestureHelp.rectTransform.offsetMin=new Vector2(24,18);gestureHelp.rectTransform.offsetMax=new Vector2(-24,55);
            Card(header,"MAIN MENU",ExperienceMenu.GoHome,Vector2.one,Vector2.one,new Vector2(-224,-70),new Vector2(-24,-18));
            Card(header,"SECTION 0",()=>WorkshopTutorial.Open(gameObject),Vector2.one,Vector2.one,new Vector2(-448,-70),new Vector2(-240,-18));
            footer=Box("Timeline dock",safe,Ink);Anchor(footer,Vector2.zero,Vector2.right,Vector2.zero,new Vector2(0,198));
            var captions=EngineDisplayCaption.Create(safe,speech,true);info=(RectTransform)captions.transform;title=captions.Heading;description=captions.Text;partMeta=captions.Context;
            infoModeCard=Card(footer,"INFO / TIMELINE",()=>SetMode(DisplayMode.InfoTimeline),new Vector2(0,1),new Vector2(.5f,1),new Vector2(24,-52),new Vector2(-8,-8)).GetComponent<UnityEngine.UI.Image>();
            engineModeCard=Card(footer,"ENGINE MOTION",()=>SetMode(DisplayMode.OperatingEngine),new Vector2(.5f,1),Vector2.one,new Vector2(8,-52),new Vector2(-24,-8)).GetComponent<UnityEngine.UI.Image>();
            progress=Label(footer,"",18,false,new Vector2(24,108),new Vector2(-24,136));progress.rectTransform.anchorMax=new Vector2(1,0);progress.rectTransform.offsetMin=new Vector2(24,108);progress.rectTransform.offsetMax=new Vector2(-24,136);progress.color=Mint;
            var rail=Box("Assembly timeline slider",footer,WorkshopTheme.Border);Anchor(rail,Vector2.zero,Vector2.right,new Vector2(32,82),new Vector2(-32,100));
            var fill=Box("Timeline fill",rail,Mint);Anchor(fill,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);fill.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            var thumb=Box("Timeline handle",rail,Color.white);Anchor(thumb,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(-15,-21),new Vector2(15,21));thumb.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            // Expand only the rail's hit area using transparent graphic padding.
            rail.GetComponent<UnityEngine.UI.Image>().raycastPadding=new Vector4(-8,-14,-8,-14);
            scrubber=rail.gameObject.AddComponent<AssemblyTimelineScrubber>();scrubber.Configure(fill,thumb);scrubber.changed=value=>{playing=false;selectedId=null;explanation.Cancel();speech.Stop();if(operation.Active){operation.Paused=true;operation.SetCrankAngle(value*720);}else SetTimeline(value);};
            var play=Card(footer,"PLAY TIMELAPSE",TogglePlay,new Vector2(.02f,0),new Vector2(.42f,0),new Vector2(0,8),new Vector2(0,56));playLabel=play.GetComponentInChildren<TextMeshProUGUI>();
            explainLabel=Card(footer,"TIMELAPSE AUDIO",()=>{if(Mode==DisplayMode.OperatingEngine)ExplainEngine();else SetTimelineAudio(!TimelineAudioEnabled);},new Vector2(.44f,0),new Vector2(.72f,0),new Vector2(0,8),new Vector2(0,56)).GetComponentInChildren<TextMeshProUGUI>();
            var sectionButton=Card(footer,"REPLAY VOICE",()=>{if(Mode==DisplayMode.OperatingEngine)ToggleSection();else ReplayVoice();},new Vector2(.74f,0),new Vector2(.98f,0),new Vector2(0,8),new Vector2(0,56));sectionLabel=sectionButton.GetComponentInChildren<TextMeshProUGUI>();
            Card(safe,"RESET VIEW",ResetView,Vector2.one,Vector2.one,new Vector2(-208,-206),new Vector2(-24,-158));
            Card(safe,"SETTINGS",()=>settingsPanel.gameObject.SetActive(!settingsPanel.gameObject.activeSelf),Vector2.one,Vector2.one,new Vector2(-410,-206),new Vector2(-226,-158));
            settingsPanel=Box("Display settings",safe,Panel);Anchor(settingsPanel,new Vector2(.1f,.5f),new Vector2(.9f,.5f),new Vector2(0,-220),new Vector2(0,220));
            speedLabel=Label(settingsPanel,"",24,true,new Vector2(24,370),new Vector2(-24,-20));
            var speedRail=Box("Playback speed slider",settingsPanel,WorkshopTheme.Border);Anchor(speedRail,Vector2.zero,Vector2.right,new Vector2(28,335),new Vector2(-28,355));
            speedRail.GetComponent<UnityEngine.UI.Image>().raycastPadding=new Vector4(-8,-16,-8,-16);
            var speedFill=Box("Speed fill",speedRail,Mint);Anchor(speedFill,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var speedThumb=Box("Speed handle",speedRail,WorkshopTheme.Text);Anchor(speedThumb,Vector2.zero,Vector2.zero,new Vector2(-12,-12),new Vector2(12,32));
            var speedControl=speedRail.gameObject.AddComponent<AssemblyTimelineScrubber>();speedControl.Configure(speedFill,speedThumb);speedControl.changed=v=>SetPlaybackSpeed(.1f+v*3.9f);speedControl.SetValue((playbackSpeed-.1f)/3.9f);
            engineSpeedLabel=Label(settingsPanel,"Engine movement · 12 rpm (teaching speed)",22,true,new Vector2(24,240),new Vector2(-24,-140));
            var engineRail=Box("Engine movement speed",settingsPanel,WorkshopTheme.Border);Anchor(engineRail,Vector2.zero,Vector2.right,new Vector2(28,205),new Vector2(-28,225));
            var engineFill=Box("Movement fill",engineRail,Mint);Anchor(engineFill,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);var engineThumb=Box("Movement handle",engineRail,WorkshopTheme.Text);Anchor(engineThumb,Vector2.zero,Vector2.zero,new Vector2(-12,-12),new Vector2(12,32));
            var engineSpeed=engineRail.gameObject.AddComponent<AssemblyTimelineScrubber>();engineSpeed.Configure(engineFill,engineThumb);engineSpeed.changed=v=>{operation.SetSpeed(4+v*26);engineSpeedLabel.text="Engine movement · "+operation.TeachingRPM.ToString("0")+" rpm (teaching speed)";};engineSpeed.SetValue(8f/26);
            audioLabel=Card(settingsPanel,"TIMELAPSE AUDIO · "+(TimelineAudioEnabled?"ON":"OFF"),()=>SetTimelineAudio(!TimelineAudioEnabled),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-240,110),new Vector2(240,160)).GetComponentInChildren<TextMeshProUGUI>();
            Card(settingsPanel,"DONE",()=>settingsPanel.gameObject.SetActive(false),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-100,18),new Vector2(100,65));
            SetPlaybackSpeed(playbackSpeed);settingsPanel.gameObject.SetActive(false);
        }
        RectTransform Card(Transform parent,string text,Action action,Vector2 min,Vector2 max,Vector2 low,Vector2 high)
        {
            var rect=Box(text+" card",parent,WorkshopTheme.AccentWash);Anchor(rect,min,max,low,high);var card=rect.gameObject.AddComponent<WorkshopCard>();card.normal=WorkshopTheme.AccentWash;card.highlight=WorkshopTheme.Hover;card.clicked=action;
            var label=Label(rect,text,17,true,new Vector2(4,2),new Vector2(-4,-2));label.alignment=TextAlignmentOptions.Center;return rect;
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
