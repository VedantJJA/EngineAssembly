using UnityEngine;
#if ENGINE_VUFORIA && (UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR)
using Vuforia;
#endif

namespace EngineAssembly
{
    // Scene-owned adapter around Vuforia's automatic Ground Plane flow.
    [DisallowMultipleComponent]
    public sealed class VuforiaGroundPlaneProvider : ARTrackingProvider
    {
        [SerializeField] bool allowEditorCameraPreview;
        public override bool CanRunHere=>Application.isMobilePlatform || Application.isEditor && allowEditorCameraPreview;
#if ENGINE_VUFORIA && (UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR)
        public const int CurrentSceneVersion=2;
        [Header("Vuforia Ground Plane scene")]
        [SerializeField] int sceneVersion;
        [SerializeField] Transform groundPlaneRoot;
        [SerializeField] Camera arCamera;
        [SerializeField] PlaneFinderBehaviour finder;
        [SerializeField] ContentPositioningBehaviour positioning;
        [SerializeField] AnchorBehaviour groundPlaneStage;
        [SerializeField] GameObject reticle;
        [SerializeField,Min(.1f)] float surfaceLifetime=.6f;
        VuforiaBehaviour engine;
        HitTestResult latestHit;
        float lastHitTime=float.NegativeInfinity;
        bool activated,started,stopping,searching=true,placed,readyReported;
        int placementVersion,surfaceHitCount;
        public Transform GroundPlaneRoot=>groundPlaneRoot;
        public PlaneFinderBehaviour PlaneFinder=>finder;
        public AnchorBehaviour GroundPlaneStage=>groundPlaneStage;
        public ContentPositioningBehaviour ContentPositioning=>positioning;
        public int SurfaceHitCount=>surfaceHitCount;
        public bool SceneConfigured=>sceneVersion==CurrentSceneVersion && groundPlaneRoot && arCamera && finder && positioning && groundPlaneStage && reticle;
        public override Camera TrackingCamera=>arCamera;
        public static bool HasTrackedPose(Status status)=>status==Status.TRACKED || status==Status.EXTENDED_TRACKED;
        public override bool TrackingReliable=>started && engine && engine.DevicePoseBehaviour && HasTrackedPose(engine.DevicePoseBehaviour.TargetStatus.Status);
        public string TrackingStatus=>engine && engine.DevicePoseBehaviour?engine.DevicePoseBehaviour.TargetStatus.Status+" / "+engine.DevicePoseBehaviour.TargetStatus.StatusInfo:started?"Starting device tracking":"Not started";
        public override Vector3? SurfacePosition=>HasSurface?(Vector3?)(latestHit.Position*VuforiaConfiguration.Instance.Vuforia.VirtualSceneScaleFactor):null;
        public override bool HasSurface=>searching && TrackingReliable && SurfaceIsCurrent(Time.unscaledTime);
        bool SurfaceIsCurrent(float now)
        {
            if(latestHit==null || now-lastHitTime>surfaceLifetime || !arCamera)return false;
            var point=arCamera.WorldToViewportPoint(latestHit.Position*VuforiaConfiguration.Instance.Vuforia.VirtualSceneScaleFactor);
            return point.z>arCamera.nearClipPlane && point.x>=0 && point.x<=1 && point.y>=0 && point.y<=1;
        }
        public override string TrackingGuidance
        {
            get
            {
                if(!engine || !engine.DevicePoseBehaviour)return "Move your phone slowly sideways with the floor in view.";
                switch(engine.DevicePoseBehaviour.TargetStatus.StatusInfo)
                {
                    case StatusInfo.INSUFFICIENT_LIGHT:return "Use a brighter area. Keep a textured floor or tabletop in view.";
                    case StatusInfo.EXCESSIVE_MOTION:return "Move more slowly. Gently sweep the phone from side to side.";
                    case StatusInfo.INSUFFICIENT_FEATURES:return "Aim at a surface with visible details. Avoid plain walls, glass and reflections.";
                    case StatusInfo.RELOCALIZING:return "Point back toward the area you scanned and move slowly to recover tracking.";
                    default:return "Aim down at a well-lit floor or tabletop and slowly move sideways. A blue square will mark the detected surface.";
                }
            }
        }
        public void Configure(Transform root,Camera camera,PlaneFinderBehaviour planeFinder,ContentPositioningBehaviour content,AnchorBehaviour stage,GameObject indicator)
        {
            groundPlaneRoot=root;arCamera=camera;finder=planeFinder;positioning=content;groundPlaneStage=stage;reticle=indicator;sceneVersion=CurrentSceneVersion;
        }
        public void EnsureSceneObjects()
        {
            if(!SceneConfigured)throw new System.InvalidOperationException("Rebuild the AR scenes using Tools > Engine Assembly > AR > Rebuild Vuforia AR Scenes From Scratch.");
        }
        public override void Begin()
        {
            if(activated || stopping)return;
            EnsureSceneObjects();
            if(string.IsNullOrWhiteSpace(VuforiaConfiguration.Instance.Vuforia.LicenseKey)){ReportFailure(ARFailure.Configuration);return;}
            engine=arCamera.GetComponent<VuforiaBehaviour>();
            if(!engine || !finder.gameObject.activeInHierarchy || !arCamera.gameObject.activeInHierarchy){ReportFailure(ARFailure.Configuration);return;}
            var app=VuforiaApplication.Instance;
            app.OnVuforiaInitialized+=Initialized;app.OnVuforiaStarted+=Started;app.OnVuforiaStopped+=Stopped;app.OnVuforiaPaused+=Paused;app.OnVuforiaError+=EngineError;
            activated=true;
            // All SDK behaviours already ran Awake in the saved scene. Enabling
            // the camera cannot outrun PlaneFinder's startup listener.
            finder.enabled=true;arCamera.enabled=true;
            var listener=arCamera.GetComponent<AudioListener>();if(listener)listener.enabled=true;
            engine.enabled=true;
            if(!app.IsInitialized)app.Initialize(FusionProviderOption.PREFER_PLATFORM_FUSION_PROVIDER);
            if(app.IsRunning)Started();
        }
        void Initialized(VuforiaInitError error)
        {
            if(stopping || error==VuforiaInitError.NONE)return;
            string code=error.ToString();
            ReportFailure(code.Contains("DEVICE_NOT_SUPPORTED")?ARFailure.UnsupportedDevice:code.Contains("CAMERA_ACCESS")?ARFailure.CameraDenied:code.Contains("LICENSE")?ARFailure.Configuration:ARFailure.Initialization);
        }
        void Started()
        {
            if(stopping || started || !engine)return;
            if(!engine.World.AnchorsSupported){ReportFailure(ARFailure.UnsupportedDevice);return;}
            if(!engine.DevicePoseBehaviour){ReportFailure(ARFailure.Initialization);return;}
            engine.DevicePoseBehaviour.enabled=true;
            engine.SetWorldCenter(WorldCenterMode.DEVICE,null);
            engine.CameraDevice.SetFocusMode(FocusMode.FOCUS_MODE_CONTINUOUSAUTO);
            started=true;ClearSurface();
            if(!readyReported){readyReported=true;ReportReady();}
        }
        void Stopped(){started=false;ClearSurface();}
        void Paused(bool paused){if(paused)ClearSurface();}
        void EngineError(VuforiaEngineError error){if(!stopping)ReportFailure(ARFailure.Initialization);}
        public void HandleSurfaceHit(HitTestResult hit)
        {
            if(!started || stopping || !searching || hit==null)return;
            latestHit=hit;lastHitTime=Time.unscaledTime;surfaceHitCount++;
        }
        public void HandleContentPlaced(GameObject content)
        {
            if(content!=groundPlaneStage.gameObject)return;
            placed=true;placementVersion++;
        }
        public override Transform PlaceAnchor()
        {
            if(!HasSurface)return null;
            int before=placementVersion;
            positioning.PositionContentAtPlaneAnchor(latestHit);
            if(placementVersion==before)return null;
            searching=false;finder.enabled=false;ClearSurface();
            return groundPlaneStage.transform;
        }
        void ClearSurface(){latestHit=null;lastHitTime=float.NegativeInfinity;}
        public override void BeginRepositioning(){ClearSurface();searching=true;if(finder)finder.enabled=true;}
        public override void RestartSurfaceSearch()
        {
            // A manual retry may reset an unanchored tracker. Never reset an
            // existing workspace or keep restarting a phone that is scanning.
            if(started && !placed && engine.DevicePoseBehaviour)engine.DevicePoseBehaviour.Reset();
            BeginRepositioning();
        }
        public override void Shutdown()
        {
            if(stopping)return;stopping=true;started=false;ClearSurface();
            if(!activated)return;
            var app=VuforiaApplication.Instance;
            app.OnVuforiaInitialized-=Initialized;app.OnVuforiaStarted-=Started;app.OnVuforiaStopped-=Stopped;app.OnVuforiaPaused-=Paused;app.OnVuforiaError-=EngineError;
            if(finder)finder.enabled=false;if(engine)engine.enabled=false;
            // The SDK owns application initialization. Scene unload disposes
            // its observers; Deinit here races the next AR scene's camera.
        }
#else
        public override Camera TrackingCamera=>null;
        public override bool HasSurface=>false;
        public override bool TrackingReliable=>false;
        public override void Begin()=>ReportFailure(ARFailure.Configuration);
        public override Transform PlaceAnchor()=>null;
        public override void BeginRepositioning(){}
        public override void Shutdown(){}
#endif
        void OnDestroy()=>Shutdown();
    }
}