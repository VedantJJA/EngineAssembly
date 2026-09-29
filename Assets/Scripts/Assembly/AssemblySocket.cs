using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace EngineAssembly
{
    [DisallowMultipleComponent]
    public class AssemblySocket : MonoBehaviour
    {
        [SerializeField] string socketId;
        [SerializeField] string geometryGroupId;
        [SerializeField] AssemblyPart targetPart;
        [SerializeField] Transform snapAnchor;
        AssemblyPart reservation;
        public UnityEvent<AssemblyPart> onPartAttached = new UnityEvent<AssemblyPart>();
        public UnityEvent<AssemblyPart> onPartDetached = new UnityEvent<AssemblyPart>();
        static readonly List<AssemblySocket> active = new List<AssemblySocket>();
        public static IReadOnlyList<AssemblySocket> AllActiveSockets => active;
        public AssemblyPart CurrentPart { get; private set; }
        public bool IsOccupied => CurrentPart != null || reservation != null;
        public AssemblyPart TargetPart => targetPart;
        public string SocketId => socketId;
        public string GeometryGroupId { get => geometryGroupId; set => geometryGroupId = value; }
        public Transform SnapTransform => snapAnchor ? snapAnchor : transform;
        public Vector3 SnapPosition => SnapTransform.position;
        public Quaternion SnapRotation => SnapTransform.rotation;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Clear() => active.Clear();
        void OnEnable() { if (!active.Contains(this)) active.Add(this); }
        void OnDisable() { active.Remove(this); if (reservation) reservation.CancelSnap(); }
        public void Configure(AssemblyPart part) { targetPart = part; socketId = part.PartId; geometryGroupId = part.GeometryGroupId; }
        public bool CanAcceptPart(AssemblyPart part)
        {
            if (!part || !isActiveAndEnabled || (CurrentPart && CurrentPart != part) || (reservation && reservation != part)) return false;
            if (targetPart && part.Manager != targetPart.Manager) return false;
            if (part.Manager && targetPart && part.ChapterId != targetPart.ChapterId) return false;
            // Equal order numbers do not imply matching geometry.
            return part == targetPart || (!string.IsNullOrEmpty(geometryGroupId) && geometryGroupId == part.GeometryGroupId)
                || (!targetPart && (part.TargetSocket == this || (!string.IsNullOrEmpty(socketId) && socketId == part.PartId)));
        }
        public bool Reserve(AssemblyPart part) { if (!CanAcceptPart(part)) return false; reservation = part; return true; }
        public void ReleaseReservation(AssemblyPart part) { if (reservation == part) reservation = null; }
        public bool AttachPart(AssemblyPart part)
        {
            if (!CanAcceptPart(part)) return false;
            reservation = null; CurrentPart = part; onPartAttached.Invoke(part); return true;
        }
        public void DetachPart()
        {
            var old = CurrentPart; CurrentPart = null; reservation = null;
            if (old) onPartDetached.Invoke(old);
        }
        void OnDrawGizmosSelected() { Gizmos.color = IsOccupied ? Color.green : Color.cyan; Gizmos.DrawWireSphere(SnapPosition, .04f); Gizmos.DrawRay(SnapPosition, SnapTransform.up * .12f); }
    }
}

