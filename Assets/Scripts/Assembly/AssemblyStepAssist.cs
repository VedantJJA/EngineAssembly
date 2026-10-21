using UnityEngine;

namespace EngineAssembly
{
    [DisallowMultipleComponent,RequireComponent(typeof(AssemblyManager))]
    public sealed class AssemblyStepAssist : MonoBehaviour
    {
        AssemblyManager manager;
        public bool Busy=>manager && manager.AssemblyParts.AnyBusy();
        public bool Available=>manager && MobileARSession.CanInteract(gameObject) && manager.Recipe!=null && manager.Mode==WorkshopMode.Assembly && manager.Task==AssemblyTask.Assemble && !manager.RepairActive && !manager.ChapterComplete && !Busy;
        void Awake()=>manager=GetComponent<AssemblyManager>();
        public bool NextStep()
        {
            if(!Available || manager.AssemblyParts.AnyBusy())return false;
            var part=manager.NextHintPart();
            if(!part || !part.TargetSocket || !manager.CanInstall(part,part.TargetSocket,out _))return false;
            var player=FindAnyObjectByType<PlayerAssemblyController>();
            if(player)player.DetachForAssistance(part);
            var mobile=GetComponent<MobileAssemblyController>();
            if(mobile && !mobile.PrepareAssistedStep(part))return false;
            if(!part.IsSelected && !part.BeginGrab())return false;
            if(player)player.GetComponent<HeldPlayerCollisionGuard>()?.Track(part.transform,part,null);
            if(!part.AssistedSnap(part.TargetSocket)){part.Release(false);return false;}
            manager.Report("Installed "+part.PartDisplayName);return true;
        }
    }
    static class AssemblyStepCollection
    {
        public static bool AnyBusy(this System.Collections.Generic.IReadOnlyList<AssemblyPart> parts)
        {foreach(var part in parts)if(part && part.IsBusy)return true;return false;}
    }
}
