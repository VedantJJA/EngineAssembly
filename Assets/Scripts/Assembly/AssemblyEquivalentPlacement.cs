using UnityEngine;

namespace EngineAssembly
{
    // Consume one matching physical copy while completing the destination's recipe
    // identity. Never duplicate IDs, sockets, colliders, or subassembly membership.
    public static class AssemblyEquivalentPlacement
    {
        public static Pose SeatedPose(AssemblyPart held,AssemblyPart destination)
        {
            if(held==destination)return held.GetTargetPose(held.TargetSocket);
            var pose=destination.GetTargetPose(destination.TargetSocket);
            var center=destination.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(destination).center);
            var worldCenter=pose.position+pose.rotation*Vector3.Scale(center,destination.transform.lossyScale);
            var local=held.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(held).center);
            var rotation=held.GetTargetPose(held.TargetSocket).rotation;
            return new Pose(worldCenter-rotation*Vector3.Scale(local,held.transform.lossyScale),rotation);
        }
        public static bool Place(AssemblyPart held,AssemblyPart destination)
        {
            if(!held || !held.IsSelected || !destination || !held.Manager || held.Manager!=destination.Manager)return false;
            var manager=held.Manager;
            if(held==destination)return Snap(held);
            if(manager.RepairActive || destination.IsSelected || destination.IsSnapped || destination.IsBusy
                || !AssemblyInventory.Matches(held,destination) || !manager.CanInstall(destination,destination.TargetSocket,out _))return false;
            var stagedPosition=destination.transform.position;var stagedRotation=destination.transform.rotation;
            var stagedCenter=AssemblyHeldView.WorldBounds(destination).center;
            var heldCenter=held.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(held).center);
            var stagedHeldRotation=stagedRotation*Quaternion.Inverse(destination.GetTargetPose(destination.TargetSocket).rotation)*held.GetTargetPose(held.TargetSocket).rotation;
            if(!destination.BeginInventoryGrab(held))return false;
            var center=AssemblyHeldView.WorldBounds(held).center;
            var localCenter=destination.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(destination).center);
            var rotation=held.transform.rotation*Quaternion.Inverse(held.GetTargetPose(held.TargetSocket).rotation)*destination.GetTargetPose(destination.TargetSocket).rotation;
            destination.MoveHeld(center-rotation*Vector3.Scale(localCenter,destination.transform.lossyScale),rotation);
            if(!Snap(destination))
            {
                destination.ResetLoose(manager.TrayFrame,manager.TrayFrame.InverseTransformPoint(stagedPosition),Quaternion.Inverse(manager.TrayFrame.rotation)*stagedRotation);
                return false;
            }
            // Return the unused canonical copy to the vacated supply position.
            // To the player one identical item was taken from their carried stack.
            held.Release(false);
            var returnedPosition=stagedCenter-stagedHeldRotation*Vector3.Scale(heldCenter,held.transform.lossyScale);
            held.ResetLoose(manager.TrayFrame,manager.TrayFrame.InverseTransformPoint(returnedPosition),Quaternion.Inverse(manager.TrayFrame.rotation)*stagedHeldRotation);
            var inventory=Object.FindAnyObjectByType<PlayerAssemblyController>()?.Inventory;
            if(inventory)inventory.Reveal(held,true);
            return true;
        }
        static bool Snap(AssemblyPart part)=>part.Manager.Difficulty==SnapDifficulty.Easy?part.GuidedSnap(part.TargetSocket):part.SnapToSocket(part.TargetSocket);
    }
}
