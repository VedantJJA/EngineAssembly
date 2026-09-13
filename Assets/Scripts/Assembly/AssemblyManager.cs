using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace EngineAssembly
{
    public enum AssemblyOrderMode
    {
        [Tooltip("Parts can be assembled as long as their individual prerequisite parts are snapped.")]
        PrerequisiteBased,

        [Tooltip("Parts must be assembled strictly in step-by-step sequential order (0, 1, 2...).")]
        StrictSequence,

        [Tooltip("Any part can be assembled in any order without restriction.")]
        FreeAssembly
    }

    /// <summary>
    /// Central manager orchestrating the engine assembly workflow, tracking completion,
    /// and enforcing sequencing/prerequisites across all engine parts.
    /// </summary>
    [DisallowMultipleComponent]
    public class AssemblyManager : MonoBehaviour
    {
        public static AssemblyManager Instance { get; private set; }

        [Header("Workflow Configuration")]
        [Tooltip("How assembly order and progression should be governed.")]
        [SerializeField] private AssemblyOrderMode orderMode = AssemblyOrderMode.PrerequisiteBased;

        [Tooltip("List of all assembly parts in this engine project. Can be auto-populated.")]
        [SerializeField] private List<AssemblyPart> assemblyParts = new List<AssemblyPart>();

        [Header("Audio Feedback (Optional)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip snapSound;
        [SerializeField] private AudioClip assemblyCompleteSound;
        [SerializeField] private AudioClip errorSound;

        [Header("Manager Events")]
        public UnityEvent<AssemblyPart> onPartSnapped;
        public UnityEvent<AssemblyPart> onPartUnsnapped;
        public UnityEvent<float, int, int> onProgressUpdated; // (progress 0-1, snappedCount, totalCount)
        public UnityEvent onAssemblyCompleted;

        public AssemblyOrderMode OrderMode => orderMode;
        public IReadOnlyList<AssemblyPart> AssemblyParts => assemblyParts;
        public int TotalPartsCount => assemblyParts.Count;
        public int SnappedPartsCount => assemblyParts.Count(p => p != null && p.IsSnapped);
        public float CompletionProgress => TotalPartsCount > 0 ? (float)SnappedPartsCount / TotalPartsCount : 0f;
        public bool IsFullyAssembled => TotalPartsCount > 0 && SnappedPartsCount == TotalPartsCount;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            // Auto-gather parts if list is empty (top-level engine parts and sub-assembly roots only)
            if (assemblyParts == null || assemblyParts.Count == 0)
            {
                assemblyParts = FindObjectsByType<AssemblyPart>(FindObjectsInactive.Exclude)
                    .Where(p => p != null && p.ParentSubAssembly == null)
                    .ToList();
                SortPartsByOrderIndex();
            }
        }

        private void Start()
        {
            UpdateProgress();
        }

        public void RegisterPart(AssemblyPart part)
        {
            // Sub-assembly children are managed locally by their SubAssembly component
            if (part.ParentSubAssembly != null) return;

            if (!assemblyParts.Contains(part))
            {
                assemblyParts.Add(part);
                SortPartsByOrderIndex();
                UpdateProgress();
            }
        }

        public void UnregisterPart(AssemblyPart part)
        {
            if (assemblyParts.Contains(part))
            {
                assemblyParts.Remove(part);
                UpdateProgress();
            }
        }

        public void SortPartsByOrderIndex()
        {
            assemblyParts = assemblyParts.OrderByDescending(p => p.AssemblyOrderIndex).ToList();
        }

        /// <summary>
        /// Validates whether a specific part is eligible to be assembled according to current order mode.
        /// </summary>
        public bool IsPartEligibleToAssemble(AssemblyPart part)
        {
            if (part == null || part.IsSnapped) return false;

            switch (orderMode)
            {
                case AssemblyOrderMode.FreeAssembly:
                    return true;

                case AssemblyOrderMode.PrerequisiteBased:
                    return part.ArePrerequisitesMet();

                case AssemblyOrderMode.StrictSequence:
                    AssemblyPart currentExpectedPart = GetCurrentSequencePart();
                    return currentExpectedPart == part;

                default:
                    return true;
            }
        }

        /// <summary>
        /// Returns the part expected next in StrictSequence mode (highest index first).
        /// </summary>
        public AssemblyPart GetCurrentSequencePart()
        {
            return assemblyParts.FirstOrDefault(p => p != null && !p.IsSnapped);
        }

        /// <summary>
        /// Finds the next eligible part to assemble in reverse order sequence whose prerequisites are satisfied.
        /// </summary>
        public AssemblyPart GetNextEligibleAssemblyPart()
        {
            SortPartsByOrderIndex();

            for (int i = 0; i < assemblyParts.Count; i++)
            {
                var part = assemblyParts[i];
                if (part != null && !part.IsSnapped && IsPartEligibleToAssemble(part))
                {
                    return part;
                }
            }

            // Fallback: check sub-assembly children and all active parts
            var allParts = AssemblyPart.AllParts;
            for (int i = 0; i < allParts.Count; i++)
            {
                var part = allParts[i];
                if (part != null && !part.IsSnapped && part.ArePrerequisitesMet())
                {
                    return part;
                }
            }

            return assemblyParts.FirstOrDefault(p => p != null && !p.IsSnapped)
                   ?? allParts.FirstOrDefault(p => p != null && !p.IsSnapped);
        }

        /// <summary>
        /// Finds the next eligible part to disassemble in reverse order sequence.
        /// </summary>
        public AssemblyPart GetNextEligibleDisassemblyPart()
        {
            SortPartsByOrderIndex();

            for (int i = assemblyParts.Count - 1; i >= 0; i--)
            {
                var part = assemblyParts[i];
                if (part != null && part.IsSnapped && IsPartEligibleToDisassemble(part))
                {
                    return part;
                }
            }

            // Fallback: check sub-assembly children and all active parts
            var allParts = AssemblyPart.AllParts;
            for (int i = allParts.Count - 1; i >= 0; i--)
            {
                var part = allParts[i];
                if (part != null && part.IsSnapped && part.CanDisassemble())
                {
                    return part;
                }
            }

            return assemblyParts.LastOrDefault(p => p != null && p.IsSnapped)
                   ?? allParts.LastOrDefault(p => p != null && p.IsSnapped);
        }

        /// <summary>
        /// Validates whether a specific part is eligible to be disassembled.
        /// Opposite of assembly: parts with lower order index or parts that depend on this part must be removed first.
        /// </summary>
        public bool IsPartEligibleToDisassemble(AssemblyPart part)
        {
            if (part == null || !part.IsSnapped) return false;

            switch (orderMode)
            {
                case AssemblyOrderMode.FreeAssembly:
                    return true;

                case AssemblyOrderMode.StrictSequence:
                    AssemblyPart currentExpectedPart = GetCurrentDisassemblyPart();
                    return currentExpectedPart == part;

                case AssemblyOrderMode.PrerequisiteBased:
                default:
                    return part.CanDisassemble();
            }
        }

        /// <summary>
        /// Returns the part expected next for disassembly in StrictSequence mode (the lowest index part currently snapped).
        /// </summary>
        public AssemblyPart GetCurrentDisassemblyPart()
        {
            return assemblyParts.LastOrDefault(p => p != null && p.IsSnapped);
        }

        /// <summary>
        /// Called by AssemblyPart when it successfully locks into place.
        /// </summary>
        public void NotifyPartSnapped(AssemblyPart part)
        {
            PlaySound(snapSound);
            onPartSnapped?.Invoke(part);

            UpdateProgress();

            if (IsFullyAssembled)
            {
                PlaySound(assemblyCompleteSound);
                onAssemblyCompleted?.Invoke();
                Debug.Log("<color=green>[AssemblyManager] Congratulations! The entire V8 engine assembly is complete!</color>");
            }
        }

        /// <summary>
        /// Called by AssemblyPart if it is unsnapped.
        /// </summary>
        public void NotifyPartUnsnapped(AssemblyPart part)
        {
            onPartUnsnapped?.Invoke(part);
            UpdateProgress();
        }

        /// <summary>
        /// Plays audio clip feedback if available.
        /// </summary>
        public void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private void UpdateProgress()
        {
            int snapped = SnappedPartsCount;
            int total = TotalPartsCount;
            float progress = total > 0 ? (float)snapped / total : 0f;

            onProgressUpdated?.Invoke(progress, snapped, total);
        }

        /// <summary>
        /// Editor utility to find all parts and sort them.
        /// </summary>
        public void RefreshPartsList()
        {
            assemblyParts = new List<AssemblyPart>(FindObjectsByType<AssemblyPart>(FindObjectsInactive.Exclude));
            SortPartsByOrderIndex();
        }
    }
}
