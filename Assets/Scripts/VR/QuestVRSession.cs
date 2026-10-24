using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Management;
namespace EngineAssembly
{
    public sealed class QuestVRSession : MonoBehaviour
    {
        public static QuestVRSession Instance { get; private set; }
        public bool Active { get; private set; }
        public bool Starting { get; private set; }
        public InputActionAsset Actions { get; private set; }
        public bool FloorTracking { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            var session=new GameObject("Quest VR session").AddComponent<QuestVRSession>();Instance=session;DontDestroyOnLoad(session.gameObject);session.CreateActions();SceneManager.sceneLoaded+=session.SceneChanged;
            if(Application.platform==RuntimePlatform.Android && IsQuestModel(SystemInfo.deviceModel))session.Request();
        }
        public static bool IsQuestModel(string model)=>!string.IsNullOrEmpty(model) && (model.ToLowerInvariant().Contains("quest") || model.ToLowerInvariant().Contains("oculus"));
        void CreateActions()
        {
            Actions=ScriptableObject.CreateInstance<InputActionAsset>();
            var head=new InputActionMap("Head");head.AddAction("Position",InputActionType.Value,"<XRHMD>/centerEyePosition",expectedControlLayout:"Vector3");head.AddAction("Rotation",InputActionType.Value,"<XRHMD>/centerEyeRotation",expectedControlLayout:"Quaternion");Actions.AddActionMap(head);
            foreach(string hand in new[]{"Left","Right"})
            {
                var map=new InputActionMap(hand);string path="<XRController>{"+hand+"Hand}/";
                map.AddAction("Position",InputActionType.Value,path+"devicePosition",expectedControlLayout:"Vector3");map.AddAction("Rotation",InputActionType.Value,path+"deviceRotation",expectedControlLayout:"Quaternion");
                map.AddAction("AimPosition",InputActionType.Value,path+"pointerPosition",expectedControlLayout:"Vector3");map.AddAction("AimRotation",InputActionType.Value,path+"pointerRotation",expectedControlLayout:"Quaternion");
                map.AddAction("Tracked",InputActionType.Button,path+"isTracked");map.AddAction("Grip",InputActionType.Button,path+"gripPressed");map.AddAction("Trigger",InputActionType.Button,path+"triggerPressed");
                map.AddAction("Stick",InputActionType.Value,path+"primary2DAxis",expectedControlLayout:"Vector2");map.AddAction("Menu",InputActionType.Button,path+"menuButton");map.AddAction("Primary",InputActionType.Button,path+"primaryButton");Actions.AddActionMap(map);
            }
            Actions.Enable();
        }
        public InputAction Input(string name)=>Actions.FindAction(name,true);
        public void Request(){if(!Active && !Starting)StartCoroutine(Initialize());}
        IEnumerator Initialize()
        {
            Starting=true;yield return null;var settings=XRGeneralSettings.Instance;var manager=settings?settings.Manager:null;
            if(manager){yield return manager.InitializeLoader();if(manager.activeLoader){manager.StartSubsystems();yield return null;var displays=new System.Collections.Generic.List<XRDisplaySubsystem>();SubsystemManager.GetSubsystems(displays);Active=displays.Any(d=>d.running);}}
            if(Active)
            {
                var inputs=new System.Collections.Generic.List<XRInputSubsystem>();SubsystemManager.GetSubsystems(inputs);foreach(var input in inputs)FloorTracking|=input.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                var displays=new System.Collections.Generic.List<XRDisplaySubsystem>();SubsystemManager.GetSubsystems(displays);foreach(var display in displays)if(display.running)WorkshopPerformanceProfiles.ConfigureXR(display);
                BindScene();
            }
            else if(manager){manager.StopSubsystems();manager.DeinitializeLoader();}
            Starting=false;
        }
        void SceneChanged(Scene scene,LoadSceneMode mode){if(Active)StartCoroutine(BindAfterStart());}
        IEnumerator BindAfterStart(){yield return null;yield return null;BindScene();}
        void BindScene(){if(!FindAnyObjectByType<QuestVRRig>())new GameObject("Quest room-scale and controller rig").AddComponent<QuestVRRig>().Configure(this);}
#if UNITY_EDITOR
        public void EnableVerification(){Active=true;FloorTracking=true;BindScene();}
#endif
        void OnDestroy(){SceneManager.sceneLoaded-=SceneChanged;if(Actions){Actions.Disable();Destroy(Actions);}if(Instance==this)Instance=null;}
        void OnApplicationQuit(){var manager=XRGeneralSettings.Instance?XRGeneralSettings.Instance.Manager:null;if(Active && manager){manager.StopSubsystems();manager.DeinitializeLoader();}}
    }
}
