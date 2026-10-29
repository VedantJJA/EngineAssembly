using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.ProviderImplementation;

namespace EngineAssembly.Editor
{
    public sealed class VerificationHandSubsystem:XRHandSubsystem{}
    public sealed class VerificationHandProvider:XRHandSubsystemProvider
    {
        public static Vector3 Palm;public static bool Tracked,Closed;
        public override void Start(){}public override void Stop(){}public override void Destroy(){}
        public override void GetHandLayout(NativeArray<bool> layout){for(int i=0;i<layout.Length;i++)layout[i]=true;}
        public override XRHandSubsystem.UpdateSuccessFlags TryUpdateHands(XRHandSubsystem.UpdateType type,ref Pose leftRoot,NativeArray<XRHandJoint> left,ref Pose rightRoot,NativeArray<XRHandJoint> right)
        {
            leftRoot=new Pose(Palm+Vector3.left*.3f,Quaternion.identity);rightRoot=new Pose(Palm,Quaternion.identity);
            for(int i=0;i<right.Length;i++)
            {
                var id=XRHandJointIDUtility.FromIndex(i);var offset=id==XRHandJointID.Palm || id==XRHandJointID.Wrist?Vector3.zero:new Vector3((i%5-2)*.012f,0,Closed?.025f:.14f);
                if(!Closed && id==XRHandJointID.ThumbTip)offset=new Vector3(-.09f,0,.025f);
                right[i]=XRHandProviderUtility.CreateJoint(Handedness.Right,Tracked?XRHandJointTrackingState.Pose:XRHandJointTrackingState.None,id,new Pose(Palm+offset,Quaternion.identity));
                left[i]=XRHandProviderUtility.CreateJoint(Handedness.Left,XRHandJointTrackingState.None,id,leftRoot);
            }
            return Tracked?XRHandSubsystem.UpdateSuccessFlags.RightHandRootPose|XRHandSubsystem.UpdateSuccessFlags.RightHandJoints:XRHandSubsystem.UpdateSuccessFlags.None;
        }
    }
    [InitializeOnLoad] public static class QuestHandsVerification
    {
        static QuestHandsVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch(){SessionState.SetBool("Hands.Verify",true);EditorSceneManager.OpenScene("Assets/Scenes/AssemblyWorkshop.unity");EditorApplication.isPlaying=true;}
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("Hands.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Tracked hand integration verification").AddComponent<QuestHandsRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("Hands.Verify",false);EditorApplication.Exit(SessionState.GetBool("Hands.Passed",false)?0:1);}
        }
    }
    public sealed class QuestHandsRunner:MonoBehaviour
    {
        [Serializable] sealed class Report{public bool passed;public string error;public string[] checks;}
        readonly List<string> checks=new List<string>(),errors=new List<string>();XRHandSubsystem system;
        void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
        void OnEnable()=>Application.logMessageReceived+=Log;void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception)errors.Add(message+"\n"+stack);}
        IEnumerator Start()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";
            while(stack.Count>0){bool more=false;object next=null;try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception e){error=e.ToString();break;}if(!more){stack.Pop();continue;}if(next is IEnumerator nested)stack.Push(nested);else yield return next;}
            if(system!=null){system.Stop();system.Destroy();}if(errors.Count>0)error+="\n"+string.Join("\n",errors);
            File.WriteAllText("Tools/Verification/quest-hands-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,error=error,checks=checks.ToArray()},true));SessionState.SetBool("Hands.Passed",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Frame(QuestVRRig rig,Vector3 world,bool closed,bool tracked=true)
        {VerificationHandProvider.Palm=rig.transform.InverseTransformPoint(world);VerificationHandProvider.Closed=closed;VerificationHandProvider.Tracked=tracked;for(int i=0;i<4;i++){system.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);yield return null;}}
        IEnumerator Scenario()
        {
            yield return null;yield return null;
            foreach(var side in new[]{"Left","Right"})
            {
                var prefab=Resources.Load<GameObject>("VRHands/"+side+"Hand");Check(prefab,"Official "+side+" hand prefab exists");
                var visual=prefab.GetComponent<QuestHandVisual>();Check(visual && visual.Wrist && visual.BoneCount>=20,side+" hand has a mapped articulated skeleton");
                Check(prefab.GetComponentsInChildren<SkinnedMeshRenderer>().All(r=>r.sharedMesh.vertexCount>500 && r.sharedMesh.vertexCount<25000 && r.sharedMaterial.shader.name=="Universal Render Pipeline/Lit"),side+" hand uses a textured-ready lightweight URP skinned mesh");
                Check(prefab.GetComponentsInChildren<Collider>(true).Length==0,side+" visual hand cannot collide with or launch the player");
            }
            yield return CaptureHands();
            var buttons=new QuestHandButtons();buttons.Sample(true,true,false,false);buttons.Sample(true,true,true,false);Check(buttons.GrabDown && buttons.GrabHeld,"Closing a tracked hand creates one grab press");
            buttons.Sample(true,true,true,false);Check(!buttons.GrabDown && buttons.GrabHeld,"Holding a gesture does not repeat pickup");buttons.Sample(false,true,false,false);Check(!buttons.GrabUp,"Tracking loss never becomes a confirmed grip release");
            buttons.Sample(true,true,true,false);Check(!buttons.GrabDown && !buttons.GrabHeld,"Reacquisition while closed requires opening the hand");buttons.Sample(true,true,false,false);buttons.Sample(true,true,true,false);buttons.Sample(true,true,false,false);Check(buttons.GrabUp,"A fresh deliberate opening releases the part");
            buttons.Sample(true,true,true,false);buttons.Reset();buttons.Sample(true,true,false,false);Check(!buttons.GrabUp,"Returning from an app pause cannot commit a release that happened while suspended");
            var manager=AssemblyManager.Instance;manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);manager.GetComponent<AssemblyNarrator>().enabled=false;
            QuestVRSession.Instance.EnableVerification();yield return null;yield return null;var rig=FindAnyObjectByType<QuestVRRig>();rig.enabled=true;
            XRHandSubsystemDescriptor.Register(new XRHandSubsystemDescriptor.Cinfo{id="EngineAssembly.HandVerification",providerType=typeof(VerificationHandProvider),subsystemTypeOverride=typeof(VerificationHandSubsystem)});
            var descriptors=new List<XRHandSubsystemDescriptor>();SubsystemManager.GetSubsystemDescriptors(descriptors);system=descriptors.First(d=>d.id=="EngineAssembly.HandVerification").Create();system.Start();yield return new WaitForSecondsRealtime(1.1f);
            var part=manager.NextHintPart();var scale=part.transform.lossyScale;Vector3 center=AssemblyHeldView.WorldBounds(part).center;
            yield return Frame(rig,center,false);Check(rig.HandTrackingActive && WorkshopTutorial.DetectPlatform(manager.gameObject)=="Hands","A running joint provider automatically activates hand mode and hand-specific teaching");
            Check(rig.NearbyPart(center)==part,"Direct hand reach selects the eligible nearby component");yield return Frame(rig,center,true);Check(rig.HeldPart==part && part.IsSelected,"Closing a hand picks up the actual engine part without a controller");
            Check(Vector3.Distance(scale,part.transform.lossyScale)<.001f,"Hand pickup preserves the actual model scale");
            var seat=part.GetTargetPose(part.TargetSocket);var contact=part.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(part).center);var target=seat.position+seat.rotation*Vector3.Scale(contact,scale);
            var guide=manager.GetComponent<AssemblyEasyGuide>();bool visible=false;
            foreach(var axis in new[]{Vector3.back,Vector3.forward,Vector3.left,Vector3.right,Vector3.up,new Vector3(1,1,-1).normalized})
            {rig.HeadCamera.transform.position=target+axis*1.4f;rig.HeadCamera.transform.LookAt(target);if(guide.VisibleFrom(part,rig.HeadCamera)){visible=true;break;}}
            Check(visible,"A real visible target is available to the tracked hand");
            yield return Frame(rig,target,true);Check(part.IsSelected && !part.IsSnapped,"Hand proximity previews the fit without installing");
            yield return Frame(rig,target+Vector3.up*.6f,true);Check(part.IsSelected && !part.IsSnapped && Vector3.Distance(AssemblyHeldView.WorldBounds(part).center,target)>.2f,"Moving the closed hand away returns the part from the preview");
            yield return Frame(rig,target,true);yield return Frame(rig,target,false);Check(part.IsSnapped && !part.IsSelected,"Opening the hand at the visible ghost commits installation");
            Check(Vector3.Distance(scale,part.transform.lossyScale)<.001f,"Hand installation preserves the source scale");
            yield return Frame(rig,target,false,false);Check(WorkshopTutorial.DetectPlatform(manager.gameObject)=="Hands","A brief tracking loss does not swap teaching back to controller commands");
            foreach(var id in new[]{"tutorial-hands-navigation","tutorial-hands-parts","tutorial-hands-lift","tutorial-hands-help"})Check(WorkshopTutorial.FindLesson(id)!=null,"Hand-specific control lesson exists: "+id);
        }
        IEnumerator CaptureHands()
        {
            var stage=new GameObject("Hand presentation preview");stage.transform.position=new Vector3(0,70,0);
            var camera=new GameObject("Hand preview camera").AddComponent<Camera>();camera.transform.SetParent(stage.transform,false);camera.transform.localPosition=new Vector3(0,.28f,-.42f);camera.transform.LookAt(stage.transform.position+Vector3.forward*.07f);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.17f,.23f);camera.nearClipPlane=.01f;camera.farClipPlane=2;
            var light=new GameObject("Hand preview light").AddComponent<Light>();light.type=LightType.Point;light.intensity=.25f;light.range=2;light.transform.SetParent(stage.transform,false);light.transform.localPosition=new Vector3(-.2f,.35f,-.15f);
            foreach(var side in new[]{"Left","Right"})
            {
                var grip=new GameObject(side+" preview grip").transform;grip.SetParent(stage.transform,false);grip.localPosition=new Vector3(side=="Left"?-.12f:.12f,0,0);
                var visual=Instantiate(Resources.Load<GameObject>("VRHands/"+side+"Hand"),stage.transform).GetComponent<QuestHandVisual>();
                for(int i=0;i<20;i++)visual.Controller(grip,side=="Left",side=="Right"?1:0,side=="Right"?1:0,false);
            }
            yield return null;WorkshopPresentationBuilder.Render(camera,"Tools/Verification/quest-visible-hands.png",1280,800);Destroy(camera.gameObject);Destroy(stage);yield return null;
        }
    }
}
