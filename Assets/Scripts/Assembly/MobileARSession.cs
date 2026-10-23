using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace EngineAssembly
{
    [DisallowMultipleComponent,RequireComponent(typeof(SpatialExperienceRig))]
    public sealed class MobileARSession : MonoBehaviour
    {
        public enum SessionState { Starting, Scanning, WorkbenchPlaced, Placed, TrackingLost, Fallback }
        [SerializeField] ARTrackingProvider provider;
        [SerializeField] string fallbackScene="MobileAssemblyARFallback";
        [SerializeField,Range(.05f,1)] float tabletopScale=.25f;
        [SerializeField] float workspaceHeight=.85f;
        [SerializeField,Min(1)] float initializationTimeout=30;
        float initializationDeadline;
        SpatialExperienceRig rig;
        MobileAssemblyController assembly;
        MobileEngineDisplay display;
        MobileAROverlay overlay;
        Transform storage,storageParent,anchorParent;
        Vector3 storagePosition,storageScale,initialAnchorPosition,initialAnchorScale;
        Quaternion storageRotation,initialAnchorRotation;
        CanvasGroup uiGroup;
        UnityEngine.UI.GraphicRaycaster gameRaycaster;
        bool ready,placedOnce,closed,waitingForCameraPermission;
        Transform workspaceSurface;
        readonly Dictionary<Transform,Pose> homes=new Dictionary<Transform,Pose>();
        readonly Dictionary<Transform,Vector3> homeScales=new Dictionary<Transform,Vector3>();
        public Transform WorkspaceSurface=>workspaceSurface;
        public Transform ActiveBuild=>assembly?assembly.ActiveWorkpiece:display.EngineRoot;
        readonly Dictionary<Renderer,bool> hiddenRenderers=new Dictionary<Renderer,bool>();
        public SessionState State { get; private set; }=SessionState.Starting;
        public bool AllowsInteraction=>State==SessionState.Placed;
        public string FallbackScene=>fallbackScene;
        public ARTrackingProvider Provider=>provider;
        public void Configure(ARTrackingProvider tracking,string fallback){provider=tracking;fallbackScene=fallback;}
        public static bool CanInteract(GameObject host){if(WorkshopTutorial.IsOpen)return false;var session=host.GetComponent<MobileARSession>();return !session || session.AllowsInteraction;}
        IEnumerator Start()
        {
            rig=GetComponent<SpatialExperienceRig>();assembly=GetComponent<MobileAssemblyController>();display=GetComponent<MobileEngineDisplay>();
            while((assembly && !assembly.Ready) || (display && !display.Ready))yield return null;
            var canvas=assembly?assembly.GetComponentInChildren<Canvas>():display.GetComponentInChildren<Canvas>();
            uiGroup=canvas.gameObject.AddComponent<CanvasGroup>();gameRaycaster=canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            anchorParent=rig.ContentAnchor.parent;initialAnchorPosition=rig.ContentAnchor.localPosition;initialAnchorRotation=rig.ContentAnchor.localRotation;initialAnchorScale=rig.ContentAnchor.localScale;
            if(assembly)
            {
                storage=GetComponent<AssemblyManager>().TrayFrame;storageParent=storage.parent;storagePosition=storage.localPosition;storageRotation=storage.localRotation;storageScale=storage.localScale;
            }
            overlay=gameObject.AddComponent<MobileAROverlay>();overlay.CreateSession(Place,Reposition,UsePhoneView);
            ready=true;ShowContent(false);SetInteraction(false);
            initializationDeadline=Time.realtimeSinceStartup+initializationTimeout;
            if(!provider || !provider.CanRunHere){Fail(ARFailure.UnsupportedPlatform);yield break;}
            provider.Ready+=ProviderReady;provider.Failed+=Fail;
            if(provider.RequiresCameraPermission)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if(!Permission.HasUserAuthorizedPermission(Permission.Camera))
                {
                    overlay.SetStatus("Camera permission", "Allow camera access to start AR, or choose Phone view.",false,false);
                    waitingForCameraPermission=true;
                    bool answered=false;var callbacks=new PermissionCallbacks();
                    callbacks.PermissionGranted+=_=>answered=true;callbacks.PermissionDenied+=_=>answered=true;
                    callbacks.PermissionDeniedAndDontAskAgain+=_=>answered=true;
                    Permission.RequestUserPermission(Permission.Camera,callbacks);
                    initializationDeadline=Time.realtimeSinceStartup+90;
                    while(!answered && !Permission.HasUserAuthorizedPermission(Permission.Camera) && Time.realtimeSinceStartup<initializationDeadline && !closed)yield return null;
                    waitingForCameraPermission=false;
                }
                if(!Permission.HasUserAuthorizedPermission(Permission.Camera)){Fail(ARFailure.CameraDenied);yield break;}
#elif UNITY_IOS && !UNITY_EDITOR
                if(!Application.HasUserAuthorization(UserAuthorization.WebCam))yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
                if(!Application.HasUserAuthorization(UserAuthorization.WebCam)){Fail(ARFailure.CameraDenied);yield break;}
#endif
            }
            if(closed)yield break;
            initializationDeadline=Time.realtimeSinceStartup+initializationTimeout;
            overlay.SetStatus("Starting AR", "Opening the camera and starting surface tracking. Phone view is available below.",false,false);
            try{provider.Begin();}catch(System.Exception error){Debug.LogWarning("AR provider startup failed: "+error.GetType().Name);Fail(ARFailure.Initialization);}
            while(State==SessionState.Starting && Time.realtimeSinceStartup<initializationDeadline && !closed)yield return null;
            if(State==SessionState.Starting && !closed)Fail(ARFailure.Initialization);
        }
        void ProviderReady()
        {
            if(closed || !provider.TrackingCamera)return;
            try{rig.BeginTrackedSession(provider.TrackingCamera,new Pose(Vector3.zero,Quaternion.identity));}
            catch(System.Exception error){Debug.LogWarning("AR camera setup failed: "+error.GetType().Name);Fail(ARFailure.Initialization);return;}
            State=SessionState.Scanning;overlay.SetPlacementLabel("PLACE ENGINE");overlay.SetStatus("Find a surface", "Move your phone slowly over a clear, well-lit floor. The engine will float at working height.",false,false);
        }
        void Update()
        {
            if(!ready || closed || !provider)return;
            // This watchdog also runs if an SDK callback interrupts Start's
            // coroutine, so the checking screen cannot remain indefinitely.
            if(State==SessionState.Starting){if(!waitingForCameraPermission && Time.realtimeSinceStartup>=initializationDeadline)Fail(ARFailure.Initialization);return;}
            if(State==SessionState.Scanning)
                overlay.SetStatus(provider.HasSurface?"Surface found":"Find a surface",provider.HasSurface?"Place the floating engine here. Subassemblies appear beside it.":"Move slowly over a textured floor in good light.",provider.HasSurface,false);
            else if(State==SessionState.Placed && !provider.TrackingReliable)
            {State=SessionState.TrackingLost;SetInteraction(false);overlay.SetStatus("Tracking paused","Move your phone slowly back toward the engine to recover tracking.",false,true);}
            else if(State==SessionState.TrackingLost && provider.TrackingReliable)
            {State=SessionState.Placed;SetInteraction(true);overlay.SetStatus("Engine anchored","Rotate, move and pinch the model, or walk around it.",false,true);}
        }
        public void Place()
        {
            if(State!=SessionState.Scanning || !provider.HasSurface)return;
            var anchor=provider.PlaceAnchor();if(!anchor)return;
            if(!workspaceSurface)
            {
                workspaceSurface=new GameObject("AR elevated workspace frame").transform;
                workspaceSurface.SetParent(anchor,false);workspaceSurface.localPosition=Vector3.up*workspaceHeight;
                if(storage){storage.SetParent(rig.ContentAnchor,false);storage.SetLocalPositionAndRotation(storagePosition,storageRotation);storage.localScale=storageScale;}
                rig.ContentAnchor.SetParent(workspaceSurface,false);rig.ContentAnchor.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);rig.ContentAnchor.localScale=Vector3.one*tabletopScale;
                var engine=assembly?GetComponent<AssemblyManager>().AssemblyRoot:display.EngineRoot;
                SeatBuild(engine,new Vector3(assembly?-.36f:0,0,0));
                RememberHome(engine);
                if(assembly){assembly.AttachPresentationSources();assembly.ArrangeTrackedWorkspaces();}
                placedOnce=true;
                State=SessionState.Placed;ShowContent(true);SetInteraction(true);AnchoredStatus();Physics.SyncTransforms();return;
            }
            workspaceSurface.SetParent(anchor,false);workspaceSurface.SetLocalPositionAndRotation(Vector3.up*workspaceHeight,Quaternion.identity);
            State=SessionState.Placed;ShowContent(true);SetInteraction(true);AnchoredStatus();Physics.SyncTransforms();
        }
        void AnchoredStatus()=>overlay.SetStatus("Engine anchored","Gestures control only the active build: 1 finger rotates; 2 fingers move; pinch changes size.",false,true);
        public void SeatBuild(Transform build,Vector3 surfacePoint)
        {
            if(!workspaceSurface || !build)return;
            var bounds=AssemblyPresentationBounds.World(build,assembly?GetComponent<AssemblyManager>():null);
            var local=workspaceSurface.InverseTransformPoint(bounds.center);
            build.position+=workspaceSurface.TransformVector(new Vector3(surfacePoint.x-local.x,0,surfacePoint.z-local.z));
            build.position+=Vector3.up*(workspaceSurface.position.y+.02f-bounds.min.y);
        }
        public void KeepAboveWorkbench(Transform build)
        {
            if(!workspaceSurface || !build)return;
            var bounds=AssemblyPresentationBounds.World(build,assembly?GetComponent<AssemblyManager>():null);
            float floor=workspaceSurface.position.y+.02f;
            if(bounds.min.y<floor)build.position+=Vector3.up*(floor-bounds.min.y);
        }
        public void RememberHome(Transform build){homes[build]=new Pose(build.localPosition,build.localRotation);homeScales[build]=build.localScale;}
        public void ResetActiveBuild()
        {
            if(!AllowsInteraction || !homes.TryGetValue(ActiveBuild,out var home))return;
            ActiveBuild.SetLocalPositionAndRotation(home.position,home.rotation);ActiveBuild.localScale=homeScales[ActiveBuild];
            if(assembly)assembly.AttachPresentationSources();Physics.SyncTransforms();
        }
        public void Reposition()
        {
            if(State!=SessionState.Placed && State!=SessionState.TrackingLost || assembly && GetComponent<AssemblyManager>().AssemblyParts.AnyBusy())return;
            provider.BeginRepositioning();State=SessionState.Scanning;ShowContent(false);SetInteraction(false);
        }
        void ShowContent(bool show)
        {
            if(rig && rig.ContentAnchor)
            {
                // Disabling the root cancels held parts through OnDisable.
                // Once placed, hide only renderers while choosing a new anchor.
                if(!placedOnce)rig.ContentAnchor.gameObject.SetActive(show);
                else if(!show)
                {
                    foreach(var renderer in rig.ContentAnchor.GetComponentsInChildren<Renderer>(true))
                    {hiddenRenderers[renderer]=renderer.enabled;renderer.enabled=false;}
                }
                else
                {
                    rig.ContentAnchor.gameObject.SetActive(true);
                    foreach(var pair in hiddenRenderers)if(pair.Key)pair.Key.enabled=pair.Value;hiddenRenderers.Clear();
                }
            }
            if(assembly){var hint=GetComponent<AssemblyHint>();if(hint)hint.enabled=show;var guide=GetComponent<AssemblyEasyGuide>();if(guide)guide.enabled=show;}
        }
        void SetInteraction(bool allowed)
        {
            if(uiGroup){uiGroup.alpha=placedOnce?1:0;uiGroup.interactable=allowed;uiGroup.blocksRaycasts=allowed;}
            if(gameRaycaster)gameRaycaster.enabled=allowed;
        }
        public void UsePhoneView()=>Fail(ARFailure.UnsupportedPlatform);
        public void Zoom(float ratio,Vector3 focus)
        {
            if(!AllowsInteraction || ratio<=0)return;
            var build=ActiveBuild;if(!homeScales.ContainsKey(build))RememberHome(build);var baseline=homeScales[build];
            var localFocus=build.InverseTransformPoint(focus);
            float factor=Mathf.Clamp(build.localScale.x/baseline.x*ratio,.5f,3);
            build.localScale=baseline*factor;build.position+=focus-build.TransformPoint(localFocus);
            KeepAboveWorkbench(build);if(assembly)assembly.AttachPresentationSources();
            Physics.SyncTransforms();
        }
        public void Fail(ARFailure reason)
        {
            if(closed)return;closed=true;State=SessionState.Fallback;
            // A missing licence or a denied camera is not an unsupported phone.
            string message=reason==ARFailure.UnsupportedDevice?"Your device does not support AR. Opening the standard phone view.":reason==ARFailure.CameraDenied?"Camera access is required for AR. Opening the standard phone view.":reason==ARFailure.UnsupportedPlatform?"Opening the standard phone view.":"AR is unavailable right now. Opening the standard phone view.";
            MobileAROverlay.QueueFallback(fallbackScene,message);RestoreContent();ShutdownProvider();
            ExperienceMenu.Launch(fallbackScene);
        }
        void RestoreContent()
        {
            if(!ready)return;
            if(storage){storage.SetParent(storageParent,false);storage.SetLocalPositionAndRotation(storagePosition,storageRotation);storage.localScale=storageScale;}
            if(rig && rig.ContentAnchor){rig.ContentAnchor.SetParent(anchorParent,false);rig.ContentAnchor.SetLocalPositionAndRotation(initialAnchorPosition,initialAnchorRotation);rig.ContentAnchor.localScale=initialAnchorScale;ShowContent(true);if(rig.IsTracked)rig.EndTrackedSession();}
        }
        void ShutdownProvider(){if(!provider)return;provider.Ready-=ProviderReady;provider.Failed-=Fail;try{provider.Shutdown();}catch(System.Exception error){Debug.LogWarning("AR cleanup failed: "+error.GetType().Name);}}
        void OnDestroy(){closed=true;RestoreContent();ShutdownProvider();}
    }
}
