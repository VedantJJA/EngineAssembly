using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EngineAssembly
{
    /// <summary>
    /// Represents a modular sub-assembly unit (e.g. Piston + Conrod + Pin, or Cylinder Head + Valves).
    /// Sub-assemblies can be nested arbitrarily in a tree hierarchy.
    /// 
    /// Architecture:
    /// - The parent GameObject gets BOTH SubAssembly + AssemblyPart components.
    /// - Children get ONLY AssemblyPart (with parentSubAssembly set to this).
    /// - Each sub-assembly maintains its own local index counter (localOrderIndex on each child)
    ///   separate from the main assembly's global orderIndex.
    /// - The sub-assembly root's global orderIndex participates in the parent's ordering system.
    /// - Child sockets are parented under the sub-assembly root so they move as a rigid group
    ///   when the sub-assembly is detached and floating.
    /// 
    /// Rules:
    /// 1. A sub-assembly can only be docked into its parent when 100% of its internal parts are assembled.
    /// 2. When docked into its parent, internal parts cannot be stripped off; the sub-assembly must be removed as a whole first.
    /// 3. Sub-assemblies float in the air when detached for convenient workshop bench assembly.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Engine Assembly/Sub Assembly")]
    public class SubAssembly : MonoBehaviour
    {
        [Header("Sub-Assembly Information")]
        [SerializeField] private string subAssemblyName = "Sub Assembly";

        [Tooltip("The root AssemblyPart that docks this entire sub-assembly into the parent assembly or engine block.")]
        [SerializeField] private AssemblyPart rootPart;

        [Header("Components & Tree Hierarchy")]
        [Tooltip("Direct child AssemblyParts that make up this sub-assembly (excluding rootPart).")]
        [SerializeField] private List<AssemblyPart> childParts = new List<AssemblyPart>();

        [Tooltip("Nested child SubAssemblies (e.g. Camshaft/Valvetrain sub-assembly inside Cylinder Head).")]
        [SerializeField] private List<SubAssembly> childSubAssemblies = new List<SubAssembly>();

        [Tooltip("Parent SubAssembly if this unit is nested inside another sub-assembly, or null if mounted to main engine.")]
        [SerializeField] private SubAssembly parentSubAssembly;

        public string SubAssemblyName => subAssemblyName;
        public AssemblyPart RootPart => rootPart;
        public IReadOnlyList<AssemblyPart> ChildParts => childParts;
        public IReadOnlyList<SubAssembly> ChildSubAssemblies => childSubAssemblies;
        public SubAssembly ParentSubAssembly => parentSubAssembly;

        /// <summary>
        /// A sub-assembly is fully assembled only when all of its direct child parts are snapped
        /// and all nested child sub-assemblies are fully assembled and docked.
        /// </summary>
        public bool IsFullyAssembled
        {
            get
            {
                // Verify all direct child parts are snapped
                for (int i = 0; i < childParts.Count; i++)
                {
                    AssemblyPart p = childParts[i];
                    if (p != null && !p.IsSnapped) return false;
                }

                // Verify all child sub-assemblies are fully assembled and docked
                for (int i = 0; i < childSubAssemblies.Count; i++)
                {
                    SubAssembly sub = childSubAssemblies[i];
                    if (sub != null)
                    {
                        if (!sub.IsFullyAssembled) return false;
                        if (sub.RootPart != null && !sub.RootPart.IsSnapped) return false;
                    }
                }

                return true;
            }
        }

        public int TotalComponentsCount
        {
            get
            {
                int count = childParts.Count(p => p != null);
                foreach (var sub in childSubAssemblies)
                {
                    if (sub != null) count += 1 + sub.TotalComponentsCount;
                }
                return count;
            }
        }

        public int SnappedComponentsCount
        {
            get
            {
                int count = childParts.Count(p => p != null && p.IsSnapped);
                foreach (var sub in childSubAssemblies)
                {
                    if (sub != null && sub.RootPart != null && sub.RootPart.IsSnapped)
                    {
                        count += 1 + sub.SnappedComponentsCount;
                    }
                }
                return count;
            }
        }

        public float CompletionProgress
        {
            get
            {
                int total = TotalComponentsCount;
                return total > 0 ? (float)SnappedComponentsCount / total : 1f;
            }
        }

        private void Awake()
        {
            if (rootPart == null)
            {
                rootPart = GetComponent<AssemblyPart>();
            }

            // Register parent-child references
            LinkParentChildReferences();
        }

        private void Start()
        {
            ReparentChildSocketsToFirstPart();
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(subAssemblyName))
            {
                subAssemblyName = gameObject.name;
            }
            if (rootPart == null)
            {
                rootPart = GetComponent<AssemblyPart>();
            }
        }

        /// <summary>
        /// Links this sub-assembly to its root and child parts.
        /// </summary>
        public void LinkParentChildReferences()
        {
            if (rootPart != null)
            {
                rootPart.SubAssemblyRoot = this;
            }

            foreach (var part in childParts)
            {
                if (part != null)
                {
                    part.ParentSubAssembly = this;
                }
            }

            foreach (var sub in childSubAssemblies)
            {
                if (sub != null)
                {
                    sub.parentSubAssembly = this;
                    if (sub.RootPart != null)
                    {
                        sub.RootPart.ParentSubAssembly = this;
                    }
                }
            }
        }

        /// <summary>
        /// Validates whether this sub-assembly can dock into its parent socket/engine.
        /// </summary>
        public bool CanDockIntoParent(out string reason)
        {
            if (!IsFullyAssembled)
            {
                reason = $"Sub-assembly '{subAssemblyName}' is not fully assembled ({SnappedComponentsCount}/{TotalComponentsCount} parts)! Assemble all internal components first.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// Validates whether child parts of this sub-assembly can be disassembled.
        /// </summary>
        public bool CanDisassembleChildren(out string reason)
        {
            // If the sub-assembly root is docked into the parent engine, you cannot take it apart
            if (rootPart != null && rootPart.IsSnapped)
            {
                reason = $"Cannot disassemble components while sub-assembly '{subAssemblyName}' is mounted in the main assembly! Remove the entire sub-assembly first.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// Returns child parts sorted by their local order index (highest first for reverse assembly order).
        /// </summary>
        public List<AssemblyPart> GetChildPartsSortedByLocalIndex()
        {
            return childParts
                .Where(p => p != null)
                .OrderByDescending(p => p.LocalOrderIndex)
                .ToList();
        }

        /// <summary>
        /// Finds the next eligible child part to assemble within this sub-assembly.
        /// Follows reverse local order (highest localOrderIndex first).
        /// </summary>
        public AssemblyPart GetNextLocalAssemblyPart()
        {
            var sorted = GetChildPartsSortedByLocalIndex();
            foreach (var part in sorted)
            {
                if (!part.IsSnapped && part.ArePrerequisitesMet())
                {
                    return part;
                }
            }
            return sorted.FirstOrDefault(p => !p.IsSnapped);
        }

        /// <summary>
        /// Finds the next eligible child part to disassemble within this sub-assembly.
        /// Follows forward local order (lowest localOrderIndex first).
        /// </summary>
        public AssemblyPart GetNextLocalDisassemblyPart()
        {
            var sorted = childParts
                .Where(p => p != null)
                .OrderBy(p => p.LocalOrderIndex)
                .ToList();

            foreach (var part in sorted)
            {
                if (part.IsSnapped && part.CanDisassemble())
                {
                    return part;
                }
            }
            return sorted.LastOrDefault(p => p.IsSnapped);
        }

        /// <summary>
        /// Auto-assigns default local order index 1 to child parts while grouping identical parts or assigning distinct groups.
        /// </summary>
        public void AutoNumberChildren()
        {
            Dictionary<string, int> groupMap = new Dictionary<string, int>();
            int nextGroup = 1;

            for (int i = 0; i < childParts.Count; i++)
            {
                if (childParts[i] != null)
                {
                    childParts[i].LocalOrderIndex = 1;

                    MeshFilter mf = childParts[i].GetComponent<MeshFilter>();
                    string key = (mf != null && mf.sharedMesh != null && !string.IsNullOrEmpty(mf.sharedMesh.name))
                        ? mf.sharedMesh.name.ToLowerInvariant()
                        : System.Text.RegularExpressions.Regex.Replace(childParts[i].gameObject.name, @"[\s_.]*[\(\[\#]?\d+[\)\]]?$", "").ToLowerInvariant();

                    if (!groupMap.TryGetValue(key, out int grp))
                    {
                        grp = nextGroup++;
                        groupMap[key] = grp;
                    }
                    childParts[i].GroupIndex = grp;
                }
            }
        }

        /// <summary>
        /// Gets the first part in the sub-assembly index-wise (highest local index in reverse order, or first child).
        /// </summary>
        public AssemblyPart GetFirstPart()
        {
            var validParts = (childParts != null && childParts.Count > 0)
                ? childParts.Where(p => p != null).ToList()
                : GetComponentsInChildren<AssemblyPart>(true)
                    .Where(p => p != null && p != rootPart && p.gameObject != gameObject)
                    .ToList();

            if (validParts.Count == 0) return rootPart;

            bool hasOrder = validParts.Any(p => p.LocalOrderIndex > 0);
            if (hasOrder)
            {
                return validParts.OrderByDescending(p => p.LocalOrderIndex).First();
            }

            return validParts[0];
        }

        /// <summary>
        /// Ensures child part sockets dynamically follow the first part in the sub-assembly (index-wise)
        /// so when the base part moves or floats, all dependent child sockets move with it.
        /// </summary>
        public void ReparentChildSocketsToFirstPart()
        {
            AssemblyPart firstPart = GetFirstPart();
            Transform anchor = (firstPart != null && firstPart != rootPart) ? firstPart.transform : transform;

            // Anchor firstPart's socket to the sub-assembly root
            if (firstPart != null && firstPart != rootPart)
            {
                Transform firstTarget = firstPart.TargetSnapPoint;
                if (firstTarget != null && !firstTarget.IsChildOf(transform))
                {
                    firstTarget.SetParent(transform, true);
                }
            }

            foreach (var part in childParts)
            {
                if (part == null || part == firstPart) continue;

                Transform snapTarget = part.TargetSnapPoint;
                if (snapTarget != null && !snapTarget.IsChildOf(anchor))
                {
                    snapTarget.SetParent(anchor, true);
                }
            }
        }

        /// <summary>
        /// Ensures child part sockets are parented under this sub-assembly root
        /// so they move as a rigid group when the sub-assembly is detached.
        /// </summary>
        public void ReparentChildSocketsUnderRoot()
        {
            ReparentChildSocketsToFirstPart();
        }

        /// <summary>
        /// Scans child GameObjects to populate child parts and nested sub-assemblies.
        /// Also ensures the root part has an AssemblyPart component and sets up local indices.
        /// </summary>
        public void AutoGatherChildren()
        {
            // Ensure root has AssemblyPart
            if (rootPart == null)
            {
                rootPart = GetComponent<AssemblyPart>();
            }
            if (rootPart == null)
            {
                rootPart = gameObject.AddComponent<AssemblyPart>();
            }

            childSubAssemblies.Clear();
            childParts.Clear();

            // Find immediate child sub-assemblies
            SubAssembly[] allSubs = GetComponentsInChildren<SubAssembly>(true);
            foreach (var sub in allSubs)
            {
                if (sub != this && (sub.transform.parent == transform || sub.transform.parent.GetComponentInParent<SubAssembly>() == this))
                {
                    if (!childSubAssemblies.Contains(sub))
                    {
                        childSubAssemblies.Add(sub);
                        sub.parentSubAssembly = this;
                    }
                }
            }

            // Find all child assembly parts
            AssemblyPart[] allParts = GetComponentsInChildren<AssemblyPart>(true);
            foreach (var part in allParts)
            {
                if (part == rootPart) continue;

                // Check if this part belongs to a nested child sub-assembly
                bool belongsToNested = false;
                foreach (var childSub in childSubAssemblies)
                {
                    if (childSub != null && part.transform.IsChildOf(childSub.transform) && part != childSub.RootPart)
                    {
                        belongsToNested = true;
                        break;
                    }
                }

                if (!belongsToNested && !childParts.Contains(part))
                {
                    childParts.Add(part);
                    part.ParentSubAssembly = this;
                }
            }

            // Auto-assign local indices if they are all zero
            bool allZero = childParts.All(p => p == null || p.LocalOrderIndex == 0);
            if (allZero && childParts.Count > 0)
            {
                AutoNumberChildren();
            }

            LinkParentChildReferences();
        }

        /// <summary>
        /// Checks whether a given part is a bare standalone part (meaning it does not belong to a sub-assembly and has no sub-assemblies).
        /// </summary>
        public static bool IsBarePart(AssemblyPart part)
        {
            if (part == null) return true;

            // If it is a sub-assembly root, it is not a bare part
            if (part.SubAssemblyRoot != null || part.GetComponent<SubAssembly>() != null)
            {
                return false;
            }

            // If it belongs to a sub-assembly, it is not a bare part
            if (part.ParentSubAssembly != null)
            {
                return false;
            }

            return true;
        }
    }
}
