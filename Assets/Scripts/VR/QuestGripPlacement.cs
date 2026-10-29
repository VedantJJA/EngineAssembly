using UnityEngine;

namespace EngineAssembly
{
    // A seated pose is only a preview while the controller grip is held. No
    // socket ownership, recipe progress or install events change until release.
    public sealed class QuestGripPlacement
    {
        AssemblyPart part;
        AssemblyPart destination;
        AssemblySocket socket;
        Pose carryPose;
        Camera viewer;
        float nextVisibilityCheck;
        bool visible;
        public bool IsPreviewing => part && socket && part.IsSelected;
        public AssemblyPart Part => IsPreviewing ? part : null;
        public AssemblySocket Socket => IsPreviewing ? socket : null;

        public bool Update(AssemblyPart item, Pose carriedPose, Vector3 localContact, Camera camera)
        {
            if (part && part != item) Cancel();
            carryPose = carriedPose; viewer = camera;
            if (!item || !item.IsSelected || !item.Manager || item.Manager.DisplayActive
                || item.Manager.Task != AssemblyTask.Assemble
                || item.Manager.Interaction != WorkshopInteraction.Build)
            { Cancel(); return false; }
            var scaledContact = Vector3.Scale(localContact, item.transform.lossyScale);
            var handPoint = carriedPose.position + carriedPose.rotation * scaledContact;
            AssemblyPart candidate=null;Pose targetPose=default;float closest=float.PositiveInfinity;
            foreach(var other in item.Manager.AssemblyParts)
            {
                if(!other || !other.gameObject.activeInHierarchy || other.IsSnapped || other.IsBusy || other.IsSelected && other!=item
                    || !other.TargetSocket || other!=item && (item.Manager.RepairActive || !AssemblyInventory.Matches(item,other))
                    || !item.Manager.CanInstall(other,other.TargetSocket,out _))continue;
                var pose=AssemblyEquivalentPlacement.SeatedPose(item,other);
                float distance=Vector3.SqrMagnitude(handPoint-(pose.position+pose.rotation*scaledContact));
                if(distance>=closest)continue;closest=distance;candidate=other;targetPose=pose;
            }
            if(!candidate){Cancel();return false;}
            var target=candidate.TargetSocket;
            var targetPoint = targetPose.position + targetPose.rotation * scaledContact;
            float radius = Mathf.Clamp(item.Definition?.snapDistance ?? .15f, .08f, .20f);
            bool same = part == item && socket == target;
            // Hysteresis keeps a naturally trembling hand from flickering in and
            // out, but the distance always uses the physical controller's pose.
            if (Vector3.Distance(handPoint, targetPoint) > radius * (same ? 1.35f : 1f))
            { Cancel(); return false; }
            if (item.Manager.Difficulty == SnapDifficulty.Hard && item.Definition != null)
            {
                var definition = item.Definition;
                if (AssemblyPart.RotationError(carriedPose.rotation, targetPose.rotation, definition.rotationMode, definition.symmetry) > definition.snapAngle)
                { Cancel(); return false; }
            }
            if (!same || Time.unscaledTime >= nextVisibilityCheck)
            {
                var guide = item.Manager.GetComponent<AssemblyEasyGuide>();
                visible = guide && camera && guide.VisibleFrom(candidate, camera);
                nextVisibilityCheck = Time.unscaledTime + .12f;
            }
            if (!visible) { Cancel(); return false; }
            part = item; socket = target;destination=candidate;
            item.MoveHeld(targetPose.position, targetPose.rotation);
            return true;
        }

        // A controller that becomes untracked or was released while a menu was
        // open may resume with Grip=false without a new tracked release event.
        // Such state restoration must drop/cancel, never install a fresh preview.
        public bool Release(bool confirmedGripRelease)
        {
            if(confirmedGripRelease)return CommitOnRelease();
            Cancel();return false;
        }

        public bool CommitOnRelease()
        {
            if (!IsPreviewing) return false;
            var item = part; var target = socket;var fitting=destination;
            var guide = item.Manager ? item.Manager.GetComponent<AssemblyEasyGuide>() : null;
            if (!guide || !viewer || !fitting || !item.Manager.CanInstall(fitting,target,out _) || !guide.VisibleFrom(fitting, viewer))
            { Cancel(); return false; }
            bool installed = AssemblyEquivalentPlacement.Place(item,fitting);
            if (!installed) { Cancel(); return false; }
            Clear(); return true;
        }

        public void Cancel()
        {
            if (part && part.IsSelected) part.MoveHeld(carryPose.position, carryPose.rotation);
            Clear();
        }
        void Clear() { part = null; socket = null;destination=null; visible = false; nextVisibilityCheck = 0; }
    }
}
