using UnityEngine;

namespace EngineAssembly
{
    /// <summary>
    /// Attached to the runtime ghost preview GameObject.
    /// Provides a raycast target so the player can look at the ghost and click to snap the held part.
    /// </summary>
    [DisallowMultipleComponent]
    public class GhostPreviewTarget : MonoBehaviour
    {
        [SerializeField] private AssemblyPart ownerPart;
        [SerializeField] private AssemblySocket targetSocket;
        [SerializeField] private Transform targetSnapPoint;

        public AssemblyPart OwnerPart
        {
            get => ownerPart;
            set => ownerPart = value;
        }

        public AssemblySocket TargetSocket
        {
            get => targetSocket;
            set => targetSocket = value;
        }

        public Transform TargetSnapPoint
        {
            get => targetSnapPoint;
            set => targetSnapPoint = value;
        }
    }
}
