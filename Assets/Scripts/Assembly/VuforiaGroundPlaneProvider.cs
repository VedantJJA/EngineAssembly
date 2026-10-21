using UnityEngine;
#if ENGINE_VUFORIA && (UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR)
using Vuforia;
#endif

namespace EngineAssembly
{
    [DisallowMultipleComponent]
    public sealed class VuforiaGroundPlaneProvider : ARTrackingProvider
    {
        [SerializeField] bool allowEditorCameraPreview;
        public override bool CanRunHere=>Application.isMobilePlatform || Application.isEditor && allowEditorCameraPreview;
#if ENGINE_VUFORIA && (UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR)
        Camera arCamera;
        VuforiaBehaviour engine;
        PlaneFinderBehaviour finder;
        AnchorBehaviour anchor;
        HitTestResult latestHit;
        float lastHitTime;
        GameObject reticle;
        Material reticleMaterial;
        bool started,stopping,activated,searching=true;
        public override Camera TrackingCamera=>arCamera;
        public override bool TrackingReliable=>started && engine && engine.DevicePoseBehaviour &&
            engine.DevicePoseBehaviour.TargetStatus.Status==Status.TRACKED && engine.DevicePoseBehaviour.TargetStatus.StatusInfo==StatusInfo.NORMAL &&
            (!anchor || anchor.TargetStatus.Status==Status.TRACKED || anchor.TargetStatus.Status==Status.EXTENDED_TRACKED);
        public override bool HasSurface=>searching && TrackingReliable && latestHit!=null && Time.unscaledTime-lastHitTime<.5f;
        public override void Begin()
        {
            if(activated || stopping)return;
            if(string.IsNullOrWhiteSpace(VuforiaConfiguration.Instance.Vuforia.LicenseKey)){ReportFailure(ARFailure.Configuration);return;}
            var app=VuforiaApplication.Instance;
            activated=true;
            app.OnVuforiaInitialized+=Initialized;app.OnVuforiaStarted+=Started;app.OnVuforiaError+=EngineError;
            var cameraObject=new GameObject("Vuforia AR Camera");cameraObject.SetActive(false);
            arCamera=cameraObject.AddComponent<Camera>();arCamera.tag="MainCamera";arCamera.rect=new Rect(0,0,1,1);
            arCamera.nearClipPlane=.01f;arCamera.farClipPlane=20;arCamera.clearFlags=CameraClearFlags.SolidColor;arCamera.backgroundColor=Color.black;
            cameraObject.AddComponent<AudioListener>();
            var data=cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();data.renderPostProcessing=false;
            engine=cameraObject.AddComponent<VuforiaBehaviour>();
            // Initialization is delayed in the saved Vuforia configuration, so
            // no desktop experience starts a camera or consumes a licence.
            cameraObject.SetActive(true);
            if(!app.IsInitialized)app.Initialize();else Initialized(VuforiaInitError.NONE);
            if(app.IsRunning && !started)Started();
        }
        void Initialized(VuforiaInitError error)
        {
            if(stopping || error==VuforiaInitError.NONE)return;
            string name=error.ToString();
            ReportFailure(name.Contains("DEVICE_NOT_SUPPORTED")?ARFailure.UnsupportedDevice:name.Contains("CAMERA_ACCESS")?ARFailure.CameraDenied:name.Contains("LICENSE")?ARFailure.Configuration:ARFailure.Initialization);
        }
        void EngineError(VuforiaEngineError error){if(!stopping)ReportFailure(ARFailure.Initialization);}
        void Started()
        {
            if(stopping || started)return;
            try{SetupTracking();}catch(System.Exception error){Debug.LogWarning("Vuforia surface setup failed: "+error.GetType().Name);ReportFailure(ARFailure.Initialization);}
        }
        void SetupTracking()
        {
            if(!engine || !engine.World.AnchorsSupported){ReportFailure(ARFailure.UnsupportedDevice);return;}
            if(!engine.DevicePoseBehaviour){ReportFailure(ARFailure.Initialization);return;}
            engine.DevicePoseBehaviour.enabled=true;
            reticle=new GameObject("Ground Plane placement indicator");
            var line=reticle.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=false;line.positionCount=66;line.widthMultiplier=.003f;
            reticleMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")){color=new Color(.02f,.55f,.5f)};line.sharedMaterial=reticleMaterial;
            for(int i=0;i<65;i++){float angle=i*Mathf.PI*2/64;line.SetPosition(i,new Vector3(Mathf.Sin(angle)*.1f,.002f,Mathf.Cos(angle)*.1f));}
            line.SetPosition(65,new Vector3(0,.002f,.14f));reticle.SetActive(false);
            finder=CreatePlaneFinder(reticle,FoundSurface);
            // Switch off the fallback camera before the finder starts its
            // screen-space queries and camera-relative placement indicator.
            started=true;ReportReady();if(!stopping)finder.gameObject.SetActive(true);
        }
        static PlaneFinderBehaviour CreatePlaneFinder(GameObject indicator,UnityEngine.Events.UnityAction<HitTestResult> onHit)
        {
            var finderObject=new GameObject("Vuforia Ground Plane Finder");finderObject.SetActive(false);
            var configured=finderObject.AddComponent<PlaneFinderBehaviour>();
            // These UnityEvents are serialized in Vuforia's prefab, but are
            // null on a component added at runtime. Initialize both before
            // subscribing or starting hit tests.
            configured.OnAutomaticHitTest=new HitTestEvent();configured.OnInteractiveHitTest=new HitTestEvent();
            configured.PlaneIndicator=indicator;configured.HitTestMode=HitTestMode.AUTOMATIC;configured.OnAutomaticHitTest.AddListener(onHit);
            return configured;
        }
        void FoundSurface(HitTestResult hit)
        {
            if(!searching || !TrackingReliable)return;
            latestHit=hit;lastHitTime=Time.unscaledTime;
            reticle.transform.SetPositionAndRotation(hit.Position,hit.Rotation);
        }
        void Update(){if(reticle)reticle.SetActive(HasSurface);}
        public override Transform PlaceAnchor()
        {
            if(!HasSurface)return null;
            var next=engine.ObserverFactory.CreateAnchorBehaviour("Engine anchor "+Time.frameCount,latestHit);
            if(!next)return null;
            var old=anchor;anchor=next;searching=false;finder.enabled=false;reticle.SetActive(false);
            // The caller reparents the content before the previous anchor is
            // destroyed at the end of this frame.
            if(old)Destroy(old.gameObject);
            return anchor.transform;
        }
        public override void BeginRepositioning(){searching=true;latestHit=null;if(finder)finder.enabled=true;}
        public override void Shutdown()
        {
            if(stopping)return;stopping=true;started=false;
            if(!activated)return;
            var app=VuforiaApplication.Instance;app.OnVuforiaInitialized-=Initialized;app.OnVuforiaStarted-=Started;app.OnVuforiaError-=EngineError;
            // Detach content in the session before destroying this anchor.
            if(anchor)Destroy(anchor.gameObject);if(finder)Destroy(finder.gameObject);if(reticle)Destroy(reticle);if(reticleMaterial)Destroy(reticleMaterial);
            if(engine)engine.enabled=false;app.Deinit();if(arCamera)Destroy(arCamera.gameObject);
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
