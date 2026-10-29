using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace EngineAssembly
{
    public sealed class QuestHandTracking
    {
        readonly bool left;readonly List<XRHandSubsystem> systems=new List<XRHandSubsystem>();XRHandSubsystem system;float nextSearch;bool pinch,grasp;
        public XRHand Hand {get;private set;}public Pose Palm {get;private set;}public Pose Aim {get;private set;}
        public bool Grabbing {get;private set;}public bool Selecting {get;private set;}public bool AimValid {get;private set;}public bool Suppressed {get;private set;}
        public QuestHandTracking(bool isLeft)=>left=isLeft;
        public bool Read()
        {
            if(system==null || !system.running)
            {
                if(Time.unscaledTime<nextSearch)return false;nextSearch=Time.unscaledTime+1;SubsystemManager.GetSubsystems(systems);system=null;
                foreach(var candidate in systems)if(candidate.running){system=candidate;break;}if(system==null)return false;
            }
            var hand=left?system.leftHand:system.rightHand;
            if(!hand.isTracked || !hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palm)){pinch=grasp=false;return false;}
            Hand=hand;Palm=palm;var aim=left?MetaAimHand.left:MetaAimHand.right;
            var flags=aim!=null?(MetaAimFlags)aim.aimFlags.ReadValue():MetaAimFlags.None;bool reserved=(flags&MetaAimFlags.SystemGesture)!=0;Suppressed=reserved;
            if(hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb) && hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var index))
                pinch=Vector3.Distance(thumb.position,index.position)<(pinch?.035f:.025f);
            else pinch=false;
            int curled=0;
            if(Curled(hand,XRHandJointID.IndexTip,palm.position,grasp))curled++;
            if(Curled(hand,XRHandJointID.MiddleTip,palm.position,grasp))curled++;
            if(Curled(hand,XRHandJointID.RingTip,palm.position,grasp))curled++;
            if(Curled(hand,XRHandJointID.LittleTip,palm.position,grasp))curled++;
            grasp=curled>=3;Selecting=!reserved && (pinch || aim!=null && aim.indexPressed.isPressed);Grabbing=!reserved && (grasp || Selecting);
            AimValid=aim!=null && aim.isTracked.isPressed && (flags&MetaAimFlags.Valid)!=0 && !reserved;
            Aim=AimValid?new Pose(aim.devicePosition.ReadValue(),aim.deviceRotation.ReadValue()):palm;
            return true;
        }
        static bool Curled(XRHand hand,XRHandJointID joint,Vector3 palm,bool wasGrasp)=>hand.GetJoint(joint).TryGetPose(out var tip) && Vector3.Distance(tip.position,palm)<(wasGrasp?.095f:.075f);
    }
}
