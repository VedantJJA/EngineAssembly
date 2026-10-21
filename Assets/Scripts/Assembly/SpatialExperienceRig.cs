using System;
using UnityEngine;

namespace EngineAssembly
{
    // Provider boundary for the later AR / VR integration. No XR package or
    // simulated tracking is required to play the touch / desktop fallback.
    [DisallowMultipleComponent]
    public sealed class SpatialExperienceRig : MonoBehaviour
    {
        [SerializeField] Transform contentAnchor;
        [SerializeField] Camera fallbackCamera;
        Camera trackedCamera;
        Camera backgroundCamera;
        PlayerAssemblyController fallbackPlayer;
        bool playerWasEnabled;
        public Transform ContentAnchor=>contentAnchor;
        public Camera PresentationCamera=>IsTracked?trackedCamera:fallbackCamera;
        public bool IsTracked=>trackedCamera;
        public event Action<Camera> CameraChanged;
        public static SpatialExperienceRig Ensure(GameObject host,Transform content,Camera camera)
        {
            var rig=host.GetComponent<SpatialExperienceRig>();if(!rig)rig=host.AddComponent<SpatialExperienceRig>();
            if(!rig.contentAnchor)
            {
                rig.contentAnchor=new GameObject("Spatial Content Anchor").transform;
                rig.contentAnchor.SetParent(host.transform,false);
                content.SetParent(rig.contentAnchor,true);
            }
            rig.fallbackCamera=camera;
            // The engine camera only draws its reserved viewport. Clear the
            // rest of the fallback screen too, including after scene changes.
            if(camera && (host.GetComponent<MobileAssemblyController>() || host.GetComponent<MobileEngineDisplay>()))
            {
                if(!rig.backgroundCamera)
                {
                    var backdrop=new GameObject("Phone background camera");backdrop.transform.SetParent(host.transform,false);
                    rig.backgroundCamera=backdrop.AddComponent<Camera>();
                    backdrop.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                }
                rig.backgroundCamera.cullingMask=0;rig.backgroundCamera.clearFlags=CameraClearFlags.SolidColor;
                rig.backgroundCamera.backgroundColor=WorkshopTheme.Background;rig.backgroundCamera.depth=camera.depth-1;
                rig.backgroundCamera.targetDisplay=camera.targetDisplay;rig.backgroundCamera.rect=new Rect(0,0,1,1);
            }
            if(camera && !camera.transform.parent)
            {
                var cameraRig=new GameObject("Fallback Camera Rig").transform;cameraRig.SetParent(host.transform,false);camera.transform.SetParent(cameraRig,true);
            }
            return rig;
        }
        public void SetAnchorPose(Pose pose)=>contentAnchor.SetPositionAndRotation(pose.position,pose.rotation);
        public void BeginTrackedSession(Camera camera,Pose anchorPose)
        {
            if(!camera)throw new ArgumentNullException(nameof(camera));
            if(IsTracked)EndTrackedSession();
            SetAnchorPose(anchorPose);trackedCamera=camera;
            fallbackPlayer=FindAnyObjectByType<PlayerAssemblyController>();
            if(fallbackPlayer){fallbackPlayer.DropHeldPart(false);playerWasEnabled=fallbackPlayer.enabled;fallbackPlayer.enabled=false;}
            if(fallbackCamera && fallbackCamera!=camera)fallbackCamera.gameObject.SetActive(false);
            if(backgroundCamera)backgroundCamera.gameObject.SetActive(false);
            CameraChanged?.Invoke(camera);
        }
        public void EndTrackedSession()
        {
            trackedCamera=null;if(fallbackCamera)fallbackCamera.gameObject.SetActive(true);
            if(backgroundCamera)backgroundCamera.gameObject.SetActive(true);
            if(fallbackPlayer)fallbackPlayer.enabled=playerWasEnabled;CameraChanged?.Invoke(fallbackCamera);
        }
    }
}
