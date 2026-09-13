using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace EngineAssembly
{
    /// <summary>
    /// Represents a target snapping location on the engine block or sub-assembly.
    /// Acts as the destination socket where an AssemblyPart docks.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class AssemblySocket : MonoBehaviour
    {
        [Header("Socket Identification")]
        [Tooltip("Unique or matching identifier for this socket (e.g., 'Crankshaft', 'Piston_BankA_1').")]
        [SerializeField] private string socketId = "PartSocket";

        [Tooltip("Optional geometry group ID for interchangeable parts (e.g. 'Piston', 'SparkPlug'). Any part with matching geometry can dock here.")]
        [SerializeField] private string geometryGroupId = "";

        [Tooltip("Optional direct reference to the specific part meant for this socket.")]
        [SerializeField] private AssemblyPart targetPart;

        [Header("Snapping Alignment")]
        [Tooltip("Custom snap transform if alignment should differ from this GameObject's pivot.")]
        [SerializeField] private Transform snapAnchor;

        [Header("Gizmo Settings")]
        [SerializeField] private Color gizmoColor = new Color(0.2f, 0.8f, 1f, 0.75f);
        [SerializeField] private float gizmoRadius = 0.05f;
        [SerializeField] private bool showGizmos = true;

        [Header("Socket Events")]
        public UnityEvent<AssemblyPart> onPartAttached;
        public UnityEvent<AssemblyPart> onPartDetached;

        private bool isOccupied = false;
        private AssemblyPart currentPart;

        private static readonly List<AssemblySocket> s_AllActiveSockets = new List<AssemblySocket>();
        public static IReadOnlyList<AssemblySocket> AllActiveSockets => s_AllActiveSockets;

        private void OnEnable()
        {
            if (!s_AllActiveSockets.Contains(this))
            {
                s_AllActiveSockets.Add(this);
            }
        }

        private void OnDisable()
        {
            s_AllActiveSockets.Remove(this);
        }

        public string SocketId => socketId;
        public string GeometryGroupId
        {
            get
            {
                if (!string.IsNullOrEmpty(geometryGroupId)) return geometryGroupId;
                if (targetPart != null && !string.IsNullOrEmpty(targetPart.GeometryGroupId)) return targetPart.GeometryGroupId;
                return "";
            }
            set => geometryGroupId = value;
        }
        public bool IsOccupied => isOccupied;
        public AssemblyPart CurrentPart => currentPart;
        public AssemblyPart TargetPart => targetPart;

        public Transform SnapTransform => snapAnchor != null ? snapAnchor : transform;
        public Vector3 SnapPosition => SnapTransform.position;
        public Quaternion SnapRotation => SnapTransform.rotation;

        private void Awake()
        {
            if (isOccupied && (currentPart == null || !currentPart.IsSnapped))
            {
                isOccupied = false;
                currentPart = null;
            }
        }

        private void Reset()
        {
            if (string.IsNullOrEmpty(socketId) || socketId == "PartSocket")
            {
                socketId = gameObject.name;
            }
        }

        /// <summary>
        /// Attaches a part to this socket.
        /// </summary>
        public bool AttachPart(AssemblyPart part)
        {
            if (isOccupied && currentPart != null && currentPart != part && currentPart.IsSnapped)
            {
                Debug.LogWarning($"[AssemblySocket] {name} is already occupied by {currentPart.name}!", this);
                return false;
            }

            isOccupied = true;
            currentPart = part;
            onPartAttached?.Invoke(part);
            return true;
        }

        /// <summary>
        /// Detaches the part currently occupying this socket.
        /// </summary>
        public void DetachPart()
        {
            if (!isOccupied) return;

            AssemblyPart detachedPart = currentPart;
            isOccupied = false;
            currentPart = null;
            onPartDetached?.Invoke(detachedPart);
        }

        /// <summary>
        /// Validates whether the given part is permitted to dock into this socket.
        /// </summary>
        public bool CanAcceptPart(AssemblyPart part)
        {
            if (part == null) return false;

            // Reject only if occupied by a different, currently-snapped part
            if (isOccupied && currentPart != null && currentPart != part && currentPart.IsSnapped)
            {
                return false;
            }

            // 0. Geometry Group matching (interchangeable parts like identical spark plugs, pistons, etc.)
            string group = GeometryGroupId;
            if (!string.IsNullOrEmpty(group) && !string.IsNullOrEmpty(part.GeometryGroupId))
            {
                if (string.Equals(group, part.GeometryGroupId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // 0.5 Sequence Group matching (parts sharing the exact same OrderIndex and GroupIndex)
            // Allows placing any part of a group into any of its group's item slots (only for positive recorded IDs)
            if (part.OrderIndex > 0 && part.GroupIndex > 0)
            {
                if (targetPart != null && 
                    targetPart.OrderIndex == part.OrderIndex && 
                    targetPart.GroupIndex == part.GroupIndex && 
                    targetPart.ParentSubAssembly == part.ParentSubAssembly)
                {
                    return true;
                }

                var allParts = AssemblyPart.AllParts;
                for (int i = 0; i < allParts.Count; i++)
                {
                    var p = allParts[i];
                    if (p != null && (p.TargetSocket == this || p.TargetSnapPoint == transform || p.TargetSnapPoint == SnapTransform || (snapAnchor != null && p.TargetSnapPoint == snapAnchor)))
                    {
                        if (p.OrderIndex == part.OrderIndex && p.GroupIndex == part.GroupIndex && p.ParentSubAssembly == part.ParentSubAssembly)
                        {
                            return true;
                        }
                    }
                }
            }

            // 1. If this socket explicitly designates a specific target part
            if (targetPart != null)
            {
                return targetPart == part;
            }

            // 2. If the part points directly to this socket or snap anchor
            if (part.TargetSocket == this || 
                part.TargetSnapPoint == transform || 
                part.TargetSnapPoint == SnapTransform ||
                (snapAnchor != null && part.TargetSnapPoint == snapAnchor))
            {
                return true;
            }

            // 3. Match against Socket ID or GameObject name if customized
            if (!string.IsNullOrEmpty(socketId) && socketId != "PartSocket")
            {
                if (string.Equals(socketId, part.PartId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(socketId, part.gameObject.name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(socketId, $"{part.gameObject.name}_Socket", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals($"{part.PartId}_Socket", socketId, StringComparison.OrdinalIgnoreCase) ||
                    socketId.IndexOf(part.gameObject.name, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    part.gameObject.name.IndexOf(socketId.Replace("_Socket", ""), StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            // Default: allow snap so user workflow is seamless
            return true;
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            Transform t = SnapTransform;
            Gizmos.color = isOccupied ? Color.green : gizmoColor;
            Gizmos.DrawWireSphere(t.position, gizmoRadius);

            // Draw alignment coordinate vectors
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(t.position, t.forward * (gizmoRadius * 2f));
            Gizmos.color = Color.green;
            Gizmos.DrawRay(t.position, t.up * (gizmoRadius * 2f));
            Gizmos.color = Color.red;
            Gizmos.DrawRay(t.position, t.right * (gizmoRadius * 2f));
        }

        private void OnDrawGizmosSelected()
        {
            Transform t = SnapTransform;
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(t.position, gizmoRadius * 1.25f);
        }
    }
}
