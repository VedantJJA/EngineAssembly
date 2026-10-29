using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Hands;

namespace EngineAssembly
{
    public sealed class QuestHandVisual:MonoBehaviour
    {
        public Transform Wrist;
        public List<JointToTransformReference> Joints=new List<JointToTransformReference>();
        Quaternion[] rest;Vector3[] positions;bool[] index,thumb,bends;float curl,indexCurl;
        public int BoneCount=>Joints.Count;
        void Awake()
        {
            rest=new Quaternion[Joints.Count];positions=new Vector3[Joints.Count];index=new bool[Joints.Count];thumb=new bool[Joints.Count];bends=new bool[Joints.Count];
            for(int i=0;i<Joints.Count;i++){rest[i]=Joints[i].jointTransform.localRotation;positions[i]=Joints[i].jointTransform.localPosition;string id=Joints[i].xrHandJointID.ToString();index[i]=id.StartsWith("Index");thumb[i]=id.StartsWith("Thumb");bends[i]=id.EndsWith("Proximal") || id.EndsWith("Intermediate") || id.EndsWith("Distal");}
            foreach(var r in GetComponentsInChildren<Renderer>()){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
        }
        public void Tracked(XRHand hand,Transform origin,float heightOffset)
        {
            transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
            for(int i=0;i<Joints.Count;i++)if(hand.GetJoint(Joints[i].xrHandJointID).TryGetPose(out var pose))
                Joints[i].jointTransform.SetPositionAndRotation(origin.TransformPoint(pose.position+Vector3.up*heightOffset),origin.rotation*pose.rotation);
        }
        public void Controller(Transform grip,bool left,float grab,float trigger,bool holding)
        {
            curl=Mathf.MoveTowards(curl,holding?1:grab,Time.unscaledDeltaTime*9);indexCurl=Mathf.MoveTowards(indexCurl,holding?.75f:trigger,Time.unscaledDeltaTime*9);
            // The wrist is behind the palm; the real part stays at palm scale.
            transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
            Wrist.SetPositionAndRotation(grip.TransformPoint(new Vector3(0,-.025f,-.055f)),grip.rotation);
            for(int i=0;i<Joints.Count;i++)
            {
                var joint=Joints[i];if(joint.jointTransform==Wrist)continue;
                joint.jointTransform.localPosition=positions[i];float amount=index[i]?indexCurl:curl;
                joint.jointTransform.localRotation=rest[i]*(bends[i]?Quaternion.AngleAxis(amount*(thumb[i]?35:75),Vector3.right):Quaternion.identity);
            }
        }
    }
    public sealed class QuestHandButtons
    {
        bool armed,previousGrab,previousSelect,previousTracked,previousOptical;
        public bool GrabHeld {get;private set;}public bool GrabDown {get;private set;}public bool GrabUp {get;private set;}
        public bool SelectHeld {get;private set;}public bool SelectDown {get;private set;}public bool SelectUp {get;private set;}
        public void Reset(){armed=previousGrab=previousSelect=previousTracked=false;GrabHeld=GrabDown=GrabUp=SelectHeld=SelectDown=SelectUp=false;}
        public void Sample(bool tracked,bool optical,bool grab,bool select)
        {
            GrabDown=GrabUp=SelectDown=SelectUp=false;
            if(!tracked || tracked!=previousTracked || optical!=previousOptical){armed=false;previousGrab=previousSelect=false;}
            if(tracked && !grab && !select)armed=true;
            GrabHeld=tracked && armed && grab;SelectHeld=tracked && armed && select;
            if(tracked && previousTracked && optical==previousOptical){GrabDown=GrabHeld&&!previousGrab;GrabUp=!GrabHeld&&previousGrab;SelectDown=SelectHeld&&!previousSelect;SelectUp=!SelectHeld&&previousSelect;}
            previousTracked=tracked;previousOptical=optical;previousGrab=GrabHeld;previousSelect=SelectHeld;
        }
    }
}
