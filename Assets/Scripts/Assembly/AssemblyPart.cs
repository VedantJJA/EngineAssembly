using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace EngineAssembly
{
    /// <summary>
    /// Core component for assemblable engine parts.
    /// Manages target snapping, proximity detection, translucent ghost preview, and assembly prerequisites.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class AssemblyPart : MonoBehaviour
    {
        // Static registry of all active parts in the scene for order checking
        private static readonly List<AssemblyPart> s_AllActiveParts = new List<AssemblyPart>();
        public static IReadOnlyList<AssemblyPart> AllActiveParts => s_AllActiveParts;

        [Header("Part Information")]
        [Tooltip("Identifier for this part (e.g. 'Crankshaft', 'Piston_1', 'OilPan').")]
        [SerializeField] private string partId = "Part_01";
        
        [Tooltip("Display name shown in UI instructions.")]
        [SerializeField] private string partDisplayName = "Engine Part";

        [Tooltip("Optional geometry group ID for interchangeable parts (e.g. 'Piston', 'SparkPlug', 'Valve'). Parts with the same geometry group can dock into each other's sockets.")]
        [SerializeField] private string geometryGroupId = "";

        [Header("Snapping Destination")]
        [Tooltip("The target socket or transform where this part must be assembled.")]
        [SerializeField] private Transform targetSnapPoint;

        [Tooltip("Optional reference to an AssemblySocket component.")]
        [SerializeField] private AssemblySocket targetSocket;

        [Header("Snap Thresholds")]
        [Tooltip("Distance in meters between part and target to trigger snap zone.")]
        [SerializeField] private float snapDistanceThreshold = 0.15f;

        [Tooltip("Whether to check if rotation roughly matches before allowing snap.")]
        [SerializeField] private bool requireRotationMatch = true;

        [Tooltip("Maximum allowed angular deviation in degrees.")]
        [SerializeField] private float snapAngleThreshold = 50f;

        [Tooltip("If true, snaps only when released. If false, auto-snaps magnetically when touching snap zone.")]
        [SerializeField] private bool snapOnRelease = true;

        [Header("Snap Animation")]
        [Tooltip("Whether to smoothly animate the part into place upon snapping.")]
        [SerializeField] private bool smoothSnap = true;

        [Tooltip("Duration of the smooth snap interpolation in seconds.")]
        [SerializeField] private float snapDuration = 0.25f;

        [Tooltip("Parent this part under the target snap point or socket after assembly.")]
        [SerializeField] private bool parentToTargetOnSnap = true;

        [Header("Ghost Preview Settings")]
        [Tooltip("Show translucent ghost at the target location when near the position.")]
        [SerializeField] private bool showGhostOnSelect = true;

        [Tooltip("Distance from target socket within which the ghost becomes visible while holding the part.")]
        [SerializeField] private float ghostRevealDistance = 8.0f;

        [Tooltip("Translucent material to apply to the ghost. If null, a URP ghost material is auto-created.")]
        [SerializeField] private Material ghostMaterial;

        [Tooltip("Color of ghost when part is held.")]
        [SerializeField] private Color ghostDefaultColor = new Color(0.15f, 0.6f, 1.0f, 0.4f);

        [Tooltip("Color of ghost when crosshair is aiming directly at it.")]
        [SerializeField] private Color ghostInRangeColor = new Color(0.3f, 0.85f, 1.0f, 0.6f);

        [Tooltip("Color of ghost if prerequisite parts are missing.")]
        [SerializeField] private Color ghostLockedColor = new Color(1.0f, 0.35f, 0.2f, 0.4f);

        [Tooltip("Optional custom ghost prefab. If null, a ghost is cloned automatically from this part's mesh.")]
        [SerializeField] private GameObject customGhostPrefab;

        [Header("Snap Confirmation Flash")]
        [Tooltip("Whether the snapped part flashes green for a moment after snapping.")]
        [FormerlySerializedAs("glowGreenOnSnap")]
        [SerializeField] private bool flashGreenOnSnap = true;

        [Tooltip("Number of rapid green flashes after placing.")]
        [SerializeField] private int snapFlashCount = 3;

        [Tooltip("Total duration in seconds for the green snap confirmation flashes.")]
        [FormerlySerializedAs("snapGlowDuration")]
        [SerializeField] private float snapFlashDuration = 0.45f;

        [Tooltip("Color of the post-snap confirmation flash (high opacity green, less transparency).")]
        [FormerlySerializedAs("snapGlowColor")]
        [SerializeField] private Color snapFlashColor = new Color(0.15f, 1.0f, 0.35f, 0.90f);

        [Header("Assembly Order & Priority")]
        [Tooltip("Sequential priority index. Parts with lower indices must be placed first. Parts with the same index share equal priority.")]
        [FormerlySerializedAs("assemblyOrderIndex")]
        [SerializeField] private int orderIndex = 0;

        [Tooltip("Other parts that MUST be snapped before this part can be snapped.")]
        [SerializeField] private List<AssemblyPart> prerequisiteParts = new List<AssemblyPart>();

        // Assembly Lifecycle Events (accessible via Inspector Events foldout dropdown)
        public UnityEvent onSelected = new UnityEvent();
        public UnityEvent onDeselected = new UnityEvent();
        public UnityEvent onEnterSnapZone = new UnityEvent();
        public UnityEvent onExitSnapZone = new UnityEvent();
        public UnityEvent onSnapped = new UnityEvent();
        public UnityEvent onUnsnapped = new UnityEvent();
        public UnityEvent onSnapRejected = new UnityEvent();

        // Runtime state
        private bool isSelected = false;
        private bool isSnapped = false;
        private bool isInSnapZone = false;
        private bool isGhostHighlighted = false;
        private Coroutine snapCoroutine;

        // References
        private Rigidbody rb;
        private Collider[] partColliders;
        private GameObject ghostInstance;
        private List<GameObject> activeGhostInstances = new List<GameObject>();
        private List<Renderer> ghostRenderers = new List<Renderer>();
        private Material runtimeGhostMat;
        private Material runtimeGhostInRangeMat;
        private Material runtimeGhostLockedMat;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Vector3 initialWorldScale = Vector3.one;
        private Transform initialParent;

        // Cached shader property IDs
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int RimColorProp = Shader.PropertyToID("_RimColor");

        public string PartId => partId;
        public string PartDisplayName => string.IsNullOrEmpty(partDisplayName) ? gameObject.name : partDisplayName;
        public string GeometryGroupId
        {
            get => geometryGroupId;
            set => geometryGroupId = value;
        }
        public bool IsSelected => isSelected;
        public bool IsSnapped => isSnapped;
        public bool IsInSnapZone => isInSnapZone;
        public int OrderIndex => orderIndex;
        public int AssemblyOrderIndex => orderIndex;
        public List<AssemblyPart> PrerequisiteParts => prerequisiteParts;
        public IReadOnlyList<GameObject> ActiveGhostInstances => activeGhostInstances;

        public Transform TargetSnapPoint
        {
            get
            {
                if (targetSnapPoint != null) return targetSnapPoint;
                if (targetSocket != null) return targetSocket.SnapTransform;
                return null;
            }
            set => targetSnapPoint = value;
        }

        public AssemblySocket TargetSocket
        {
            get => targetSocket;
            set => targetSocket = value;
        }

        public GameObject GhostInstance => ghostInstance;

        /// <summary>
        /// Calculates the next sequential order index based on active and inactive parts in the scene.
        /// If parts in the scene currently have indices [1, 2, 3, 3], the next index is 4.
        /// </summary>
        public int CalculateNextOrderIndex()
        {
            AssemblyPart[] allSceneParts = FindObjectsByType<AssemblyPart>(FindObjectsInactive.Include);
            int highestIndex = 0;
            foreach (var p in allSceneParts)
            {
                if (p != null && p != this && p.gameObject.scene == gameObject.scene)
                {
                    if (p.orderIndex > highestIndex)
                    {
                        highestIndex = p.orderIndex;
                    }
                }
            }
            return highestIndex + 1;
        }

        private void Reset()
        {
            orderIndex = CalculateNextOrderIndex();
            partId = gameObject.name;
            partDisplayName = gameObject.name;
        }

        private void OnEnable()
        {
            if (!s_AllActiveParts.Contains(this))
            {
                s_AllActiveParts.Add(this);
            }
        }

        private void OnDisable()
        {
            s_AllActiveParts.Remove(this);
            CleanupAllGhosts();
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }

            // Ensure part follows gravity until snapped
            if (!isSnapped && rb != null)
            {
                rb.useGravity = true;
                rb.isKinematic = false;
            }

            partColliders = GetComponentsInChildren<Collider>(true);

            initialPosition = transform.position;
            initialRotation = transform.rotation;
            initialWorldScale = transform.lossyScale;
            initialParent = transform.parent;

            if (targetSocket == null && targetSnapPoint != null)
            {
                targetSocket = targetSnapPoint.GetComponent<AssemblySocket>();
                if (targetSocket == null)
                {
                    targetSocket = targetSnapPoint.GetComponentInParent<AssemblySocket>();
                }
                if (targetSocket == null)
                {
                    targetSocket = targetSnapPoint.GetComponentInChildren<AssemblySocket>();
                }
            }

            // Ensure no ghosts exist at startup before the part is picked up
            CleanupAllGhosts();
            if (targetSnapPoint != null)
            {
                for (int i = targetSnapPoint.childCount - 1; i >= 0; i--)
                {
                    Transform child = targetSnapPoint.GetChild(i);
                    if (child.name.Contains("Ghost"))
                    {
                        if (Application.isPlaying) Destroy(child.gameObject);
                        else DestroyImmediate(child.gameObject);
                    }
                }
            }
        }

        private void Start()
        {
            // Register with manager if present
            if (AssemblyManager.Instance != null)
            {
                AssemblyManager.Instance.RegisterPart(this);
            }
        }

        private void Update()
        {
            if (isSnapped) return;

            if (isSelected)
            {
                CheckGhostVisibility();
                CheckSnapZoneProximity();
            }
        }

        /// <summary>
        /// Call when the user/controller picks up or selects this part.
        /// </summary>
        public void Select()
        {
            if (isSnapped)
            {
                Unsnap();
            }

            isSelected = true;
            onSelected?.Invoke();

            if (showGhostOnSelect)
            {
                SetupGhost();
                CheckGhostVisibility();
                UpdateGhostVisualState();
            }
        }

        public void CheckGhostVisibility(Vector3? viewerPosition = null)
        {
            // Ghosts must NEVER appear if this part is not currently selected/held, or is already snapped
            if (!isSelected || isSnapped || !showGhostOnSelect)
            {
                SetGhostVisible(false);
                return;
            }

            if (activeGhostInstances.Count == 0)
            {
                SetupGhost();
                if (activeGhostInstances.Count == 0) return;
            }

            Vector3 viewPoint = viewerPosition ?? (Camera.main != null ? Camera.main.transform.position : transform.position);

            for (int i = 0; i < activeGhostInstances.Count; i++)
            {
                var g = activeGhostInstances[i];
                if (g == null) continue;

                float dist = Vector3.Distance(viewPoint, g.transform.position);
                bool inRange = dist <= ghostRevealDistance;
                if (g.activeSelf != inRange)
                {
                    g.SetActive(inRange);
                }
            }
        }

        /// <summary>
        /// Call when the user/controller releases or deselects this part.
        /// </summary>
        public void Deselect()
        {
            if (isSnapped) return;

            isSelected = false;
            onDeselected?.Invoke();

            if (isInSnapZone && snapOnRelease)
            {
                TrySnap();
            }
            else
            {
                CleanupAllGhosts();
                isInSnapZone = false;
            }
        }

        /// <summary>
        /// Validates distance and angle to target snap location.
        /// </summary>
        private void CheckSnapZoneProximity()
        {
            Transform target = TargetSnapPoint;
            if (target == null) return;

            float distance = Vector3.Distance(transform.position, target.position);
            bool withinDistance = distance <= snapDistanceThreshold;

            bool withinAngle = true;
            if (requireRotationMatch)
            {
                float angle = Quaternion.Angle(transform.rotation, target.rotation);
                withinAngle = angle <= snapAngleThreshold;
            }

            bool validZone = withinDistance && withinAngle;

            if (validZone && !isInSnapZone)
            {
                isInSnapZone = true;
                onEnterSnapZone?.Invoke();
                UpdateGhostVisualState();

                if (!snapOnRelease)
                {
                    TrySnap();
                }
            }
            else if (!validZone && isInSnapZone)
            {
                isInSnapZone = false;
                onExitSnapZone?.Invoke();
                UpdateGhostVisualState();
            }
        }

        /// <summary>
        /// Attempts to snap the part into place. Validates prerequisites before snapping.
        /// </summary>
        public bool TrySnap()
        {
            if (isSnapped) return true;

            // Check if prerequisites are satisfied
            if (!IsOrderPriorityMet(out int blockingIndex, out string blockingName))
            {
                Debug.LogWarning($"[AssemblyPart] Cannot snap {PartDisplayName}: Must place earlier part '{blockingName}' (Order Priority: {blockingIndex}) before this part (Order Priority: {orderIndex})!", this);
                onSnapRejected?.Invoke();
                if (showGhostOnSelect) SetGhostVisible(false);
                isInSnapZone = false;
                return false;
            }

            if (!ArePrerequisitesMet())
            {
                Debug.LogWarning($"[AssemblyPart] Cannot snap {PartDisplayName} ({partId}): Missing prerequisite parts!", this);
                onSnapRejected?.Invoke();
                if (showGhostOnSelect) SetGhostVisible(false);
                isInSnapZone = false;
                return false;
            }

            // Check if socket allows it
            if (targetSocket != null && !targetSocket.CanAcceptPart(this))
            {
                Debug.LogWarning($"[AssemblyPart] Socket {targetSocket.name} rejected {PartDisplayName}!", this);
                onSnapRejected?.Invoke();
                if (showGhostOnSelect) SetGhostVisible(false);
                isInSnapZone = false;
                return false;
            }

            ExecuteSnap();
            return true;
        }

        /// <summary>
        /// Programmatically snaps this part into its target snap point or matching socket.
        /// Useful for automated assembly sequences, tutorial demonstrations, and testing.
        /// </summary>
        /// <param name="smooth">Whether to animate the snap movement or place immediately.</param>
        /// <param name="ignorePrerequisites">If false, validates prerequisites and order priority before snapping.</param>
        public bool SnapDirectly(bool smooth = true, bool ignorePrerequisites = false)
        {
            if (isSnapped) return true;

            if (!ignorePrerequisites)
            {
                if (!IsOrderPriorityMet(out int blockingIndex, out string blockingName))
                {
                    Debug.LogWarning($"[AssemblyPart] Cannot auto-snap {PartDisplayName}: Must place earlier part '{blockingName}' (Order Priority: {blockingIndex}) before this part (Order Priority: {orderIndex})!", this);
                    return false;
                }

                if (!ArePrerequisitesMet())
                {
                    Debug.LogWarning($"[AssemblyPart] Cannot auto-snap {PartDisplayName} ({partId}): Missing prerequisite parts!", this);
                    return false;
                }
            }

            // Resolve target socket if null and geometry group is specified
            if (TargetSnapPoint == null && !string.IsNullOrEmpty(geometryGroupId))
            {
                var allSockets = AssemblySocket.AllActiveSockets;
                for (int i = 0; i < allSockets.Count; i++)
                {
                    var s = allSockets[i];
                    if (s != null && !s.IsOccupied && string.Equals(s.GeometryGroupId, geometryGroupId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        targetSocket = s;
                        targetSnapPoint = s.SnapTransform;
                        break;
                    }
                }

                if (TargetSnapPoint == null)
                {
                    for (int i = 0; i < s_AllActiveParts.Count; i++)
                    {
                        var p = s_AllActiveParts[i];
                        if (p != null && p != this && !string.IsNullOrEmpty(p.GeometryGroupId) && string.Equals(p.GeometryGroupId, geometryGroupId, System.StringComparison.OrdinalIgnoreCase))
                        {
                            if (p.IsSnapped) continue;
                            Transform pt = p.TargetSnapPoint;
                            AssemblySocket ps = p.TargetSocket;
                            if (pt != null && (ps == null || !ps.IsOccupied))
                            {
                                targetSnapPoint = pt;
                                targetSocket = ps;
                                break;
                            }
                        }
                    }
                }
            }

            Transform target = TargetSnapPoint;
            if (target == null)
            {
                Debug.LogWarning($"[AssemblyPart] Cannot auto-snap {PartDisplayName}: No target snap point or socket assigned!", this);
                return false;
            }

            if (targetSocket != null && !targetSocket.CanAcceptPart(this))
            {
                Debug.LogWarning($"[AssemblyPart] Socket {targetSocket.name} rejected {PartDisplayName}!", this);
                return false;
            }

            ExecuteSnap(smooth);
            return true;
        }

        /// <summary>
        /// Performs the snap action, disabling physics and locking into the target transform.
        /// </summary>
        private void ExecuteSnap(bool? forceSmooth = null)
        {
            if (snapCoroutine != null) StopCoroutine(snapCoroutine);

            Transform target = TargetSnapPoint;
            if (target == null) return;

            isSnapped = true;
            isInSnapZone = false;
            isSelected = false;

            // Disable Rigidbody physics on snap (only zero velocities if not already kinematic)
            if (rb != null)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.useGravity = false;
                rb.isKinematic = true;
            }

            // Attach to socket
            if (targetSocket != null)
            {
                targetSocket.AttachPart(this);
            }

            // Hide all ghosts
            CleanupAllGhosts();

            bool useSmooth = forceSmooth.HasValue ? forceSmooth.Value : smoothSnap;

            if (useSmooth && gameObject.activeInHierarchy)
            {
                snapCoroutine = StartCoroutine(SmoothSnapRoutine(target.position, target.rotation));
            }
            else
            {
                transform.position = target.position;
                transform.rotation = target.rotation;
                FinalizeSnap();
            }
        }

        private IEnumerator SmoothSnapRoutine(Vector3 targetPos, Quaternion targetRot)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            float elapsed = 0f;

            while (elapsed < snapDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / snapDuration);
                // Ease out cubic
                float curveT = 1f - Mathf.Pow(1f - t, 3f);

                transform.position = Vector3.Lerp(startPos, targetPos, curveT);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, curveT);
                yield return null;
            }

            transform.position = targetPos;
            transform.rotation = targetRot;
            FinalizeSnap();
        }

        private void FinalizeSnap()
        {
            Transform target = TargetSnapPoint;
            if (target != null)
            {
                if (parentToTargetOnSnap)
                {
                    transform.SetParent(target, true);
                    transform.localPosition = Vector3.zero;
                    transform.localRotation = Quaternion.identity;

                    // Preserve original world scale even if socket parent is scaled
                    Vector3 pScale = target.lossyScale;
                    transform.localScale = new Vector3(
                        pScale.x != 0 ? initialWorldScale.x / pScale.x : 1f,
                        pScale.y != 0 ? initialWorldScale.y / pScale.y : 1f,
                        pScale.z != 0 ? initialWorldScale.z / pScale.z : 1f
                    );
                }
                else
                {
                    transform.position = target.position;
                    transform.rotation = target.rotation;
                }
            }

            onSnapped?.Invoke();

            // Flash green confirmation effect (high opacity, solid model stays visible)
            if (flashGreenOnSnap && gameObject.activeInHierarchy)
            {
                StartCoroutine(SnapFlashRoutine());
            }

            if (AssemblyManager.Instance != null)
            {
                AssemblyManager.Instance.NotifyPartSnapped(this);
            }
        }

        private IEnumerator SnapFlashRoutine()
        {
            // Real model stays completely untouched, so it NEVER disappears!
            GameObject flashObj = new GameObject($"{gameObject.name}_SnapFlash");
            flashObj.transform.SetParent(transform, false);
            flashObj.transform.localPosition = Vector3.zero;
            flashObj.transform.localRotation = Quaternion.identity;
            flashObj.transform.localScale = Vector3.one;

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");

            Material flashMat = new Material(unlitShader)
            {
                name = "SnapFlash_Instance"
            };

            // Configure transparent flash material with high opacity (less transparency)
            flashMat.SetFloat("_Surface", 1.0f); // Transparent
            flashMat.SetFloat("_Blend", 0.0f);   // Alpha blend
            flashMat.SetInt("_ZWrite", 0);
            flashMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
            flashMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 200;

            Color baseColor = snapFlashColor;
            if (flashMat.HasProperty(BaseColorProp)) flashMat.SetColor(BaseColorProp, baseColor);
            else flashMat.color = baseColor;

            CloneFlashMeshHierarchy(transform, flashObj.transform, flashMat);

            int flashCount = Mathf.Max(1, snapFlashCount);
            float totalDuration = snapFlashDuration > 0.05f ? snapFlashDuration : 0.45f;
            float singleFlashDuration = totalDuration / flashCount;

            for (int f = 0; f < flashCount; f++)
            {
                float elapsed = 0f;
                while (elapsed < singleFlashDuration)
                {
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / singleFlashDuration);

                    // Pulse up and down: smooth bell curve with Mathf.Sin(progress * Mathf.PI)
                    float alpha = Mathf.Sin(progress * Mathf.PI) * baseColor.a;

                    Color currentColor = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                    if (flashMat.HasProperty(BaseColorProp)) flashMat.SetColor(BaseColorProp, currentColor);
                    else flashMat.color = currentColor;

                    yield return null;
                }
            }

            Destroy(flashObj);
            Destroy(flashMat);
        }

        private void CloneFlashMeshHierarchy(Transform source, Transform destination, Material flashMat)
        {
            if (source.TryGetComponent<MeshFilter>(out var mf) && source.TryGetComponent<MeshRenderer>(out _))
            {
                MeshFilter newMf = destination.gameObject.AddComponent<MeshFilter>();
                newMf.sharedMesh = mf.sharedMesh;

                MeshRenderer newMr = destination.gameObject.AddComponent<MeshRenderer>();
                newMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                newMr.receiveShadows = false;
                newMr.sharedMaterial = flashMat;
            }

            for (int i = 0; i < source.childCount; i++)
            {
                Transform childSource = source.GetChild(i);
                if (childSource.name.Contains("Ghost") || childSource.name.Contains("SnapFlash")) continue;

                GameObject childDest = new GameObject(childSource.name);
                childDest.transform.SetParent(destination, false);
                childDest.transform.localPosition = childSource.localPosition;
                childDest.transform.localRotation = childSource.localRotation;
                childDest.transform.localScale = childSource.localScale;

                CloneFlashMeshHierarchy(childSource, childDest.transform, flashMat);
            }
        }

        /// <summary>
        /// Detaches/unsnaps the part and restores its physics.
        /// If force is false, unsnapping will be rejected if disassembly rules are violated.
        /// </summary>
        public bool Unsnap(bool force = false)
        {
            if (!isSnapped) return true;

            if (!force && !CanDisassemble(out string reason, out _))
            {
                Debug.LogWarning($"[AssemblyPart] Cannot disassemble '{PartDisplayName}': {reason}", this);
                return false;
            }

            isSnapped = false;
            isInSnapZone = false;

            if (parentToTargetOnSnap)
            {
                transform.SetParent(initialParent, true);
            }

            if (targetSocket != null)
            {
                targetSocket.DetachPart();
            }

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }

            onUnsnapped?.Invoke();

            if (AssemblyManager.Instance != null)
            {
                AssemblyManager.Instance.NotifyPartUnsnapped(this);
            }

            return true;
        }

        /// <summary>
        /// Validates whether this part can be disassembled (unsnapped).
        /// Disassembly is the exact opposite of assembly:
        /// 1. Higher orderIndex parts must be disassembled first.
        /// 2. Any parts that have this part as a prerequisite must be disassembled first.
        /// Parts with equal orderIndex share priority and do not block each other.
        /// </summary>
        public bool CanDisassemble()
        {
            return CanDisassemble(out _, out _);
        }

        public bool CanDisassemble(out string blockingReason, out string blockingPartName)
        {
            blockingReason = null;
            blockingPartName = null;

            if (!isSnapped) return true;

            // 1. Check if any parts with LOWER orderIndex are currently snapped (lower numbers are removed first)
            AssemblyPart lowestOrderPart = null;
            int lowestOrder = int.MaxValue;

            for (int i = 0; i < s_AllActiveParts.Count; i++)
            {
                var other = s_AllActiveParts[i];
                if (other == null || other == this) continue;

                if (other.IsSnapped && other.OrderIndex < this.orderIndex)
                {
                    if (other.OrderIndex < lowestOrder)
                    {
                        lowestOrder = other.OrderIndex;
                        lowestOrderPart = other;
                    }
                }
            }

            if (lowestOrderPart != null)
            {
                blockingPartName = lowestOrderPart.PartDisplayName;
                blockingReason = $"Remove '{lowestOrderPart.PartDisplayName}' (Order: {lowestOrderPart.OrderIndex}) first";
                return false;
            }

            // 2. Check if any snapped parts have this part as a prerequisite
            for (int i = 0; i < s_AllActiveParts.Count; i++)
            {
                var other = s_AllActiveParts[i];
                if (other == null || other == this) continue;

                if (other.IsSnapped && other.PrerequisiteParts != null && other.PrerequisiteParts.Contains(this))
                {
                    blockingPartName = other.PartDisplayName;
                    blockingReason = $"Remove dependent part '{other.PartDisplayName}' first";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns true if all other parts in the scene with a strictly higher orderIndex have already been snapped.
        /// Reverse assembly order: Higher numbers are placed first, lower numbers placed last.
        /// Parts with the same orderIndex share equal priority and do not block each other.
        /// </summary>
        public bool IsOrderPriorityMet()
        {
            return IsOrderPriorityMet(out _, out _);
        }

        public bool IsOrderPriorityMet(out int blockingOrderIndex, out string blockingPartName)
        {
            blockingOrderIndex = -1;
            blockingPartName = null;

            int highestBlockingOrder = -1;
            AssemblyPart highestBlockingPart = null;

            for (int i = 0; i < s_AllActiveParts.Count; i++)
            {
                var other = s_AllActiveParts[i];
                if (other == null || other == this) continue;

                // In reverse order: higher numbers must be snapped first!
                if (other.OrderIndex > this.orderIndex && !other.IsSnapped)
                {
                    if (other.OrderIndex > highestBlockingOrder)
                    {
                        highestBlockingOrder = other.OrderIndex;
                        highestBlockingPart = other;
                    }
                }
            }

            if (highestBlockingPart != null)
            {
                blockingOrderIndex = highestBlockingOrder;
                blockingPartName = highestBlockingPart.PartDisplayName;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns true if all prerequisite assembly parts have already been snapped.
        /// </summary>
        public bool ArePrerequisitesMet()
        {
            // 1. Check order index priority
            if (!IsOrderPriorityMet()) return false;

            // 2. Check explicit prerequisite parts if any assigned
            if (prerequisiteParts == null || prerequisiteParts.Count == 0) return true;

            foreach (var prereq in prerequisiteParts)
            {
                if (prereq != null && !prereq.IsSnapped)
                {
                    return false;
                }
            }
            return true;
        }

        #region Ghost Management

        /// <summary>
        /// Cleans up all active ghost instances across all compatible sockets.
        /// </summary>
        private void CleanupAllGhosts()
        {
            for (int i = 0; i < activeGhostInstances.Count; i++)
            {
                var g = activeGhostInstances[i];
                if (g != null)
                {
                    if (Application.isPlaying) Destroy(g);
                    else DestroyImmediate(g);
                }
            }
            activeGhostInstances.Clear();
            ghostRenderers.Clear();
            ghostInstance = null;
        }

        /// <summary>
        /// Initializes translucent ghosts at all compatible sockets (or the default snap target).
        /// If this part belongs to a geometry group, all available matching sockets in the scene show ghost holograms.
        /// </summary>
        private void SetupGhost()
        {
            if (!showGhostOnSelect || !isSelected || isSnapped) return;

            CleanupAllGhosts();

            // Gather all compatible targets
            List<(Transform snapTarget, AssemblySocket sock)> targets = new List<(Transform, AssemblySocket)>();

            // 1. If geometry group is specified, find all active unoccupied sockets with matching geometry
            if (!string.IsNullOrEmpty(geometryGroupId))
            {
                var allSockets = AssemblySocket.AllActiveSockets;
                for (int i = 0; i < allSockets.Count; i++)
                {
                    var s = allSockets[i];
                    if (s != null && !s.IsOccupied && string.Equals(s.GeometryGroupId, geometryGroupId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        targets.Add((s.SnapTransform, s));
                    }
                }

                // Also check other parts in the scene belonging to the same geometry group whose snap targets are unoccupied
                for (int i = 0; i < s_AllActiveParts.Count; i++)
                {
                    var p = s_AllActiveParts[i];
                    if (p != null && p != this && !string.IsNullOrEmpty(p.GeometryGroupId) && string.Equals(p.GeometryGroupId, geometryGroupId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (p.IsSnapped) continue; // Already occupied by snapped part

                        Transform pt = p.TargetSnapPoint;
                        AssemblySocket ps = p.TargetSocket;
                        if (pt != null && (ps == null || !ps.IsOccupied))
                        {
                            bool alreadyInTargets = false;
                            for (int t = 0; t < targets.Count; t++)
                            {
                                if (targets[t].snapTarget == pt) { alreadyInTargets = true; break; }
                            }
                            if (!alreadyInTargets)
                            {
                                targets.Add((pt, ps));
                            }
                        }
                    }
                }
            }

            // 2. If no group sockets found, fallback to this part's own targetSnapPoint / targetSocket
            if (targets.Count == 0)
            {
                Transform target = TargetSnapPoint;
                if (target != null)
                {
                    targets.Add((target, targetSocket));
                }
            }

            if (targets.Count == 0) return;

            // Prepare ghost materials for the 3 visual states
            runtimeGhostMat = GetOrCreateGhostMaterial(ghostDefaultColor);
            runtimeGhostInRangeMat = GetOrCreateGhostMaterial(ghostInRangeColor);
            runtimeGhostLockedMat = GetOrCreateGhostMaterial(ghostLockedColor);

            int ghostLayer = LayerMask.NameToLayer("TransparentFX");
            if (ghostLayer < 0) ghostLayer = 1;

            Vector3 sourceScale = (Application.isPlaying && initialWorldScale != Vector3.zero) ? initialWorldScale : transform.lossyScale;

            for (int i = 0; i < targets.Count; i++)
            {
                var (snapTarget, sock) = targets[i];
                if (snapTarget == null) continue;

                Vector3 pScale = snapTarget.lossyScale;
                Vector3 calculatedScale = new Vector3(
                    pScale.x != 0 ? sourceScale.x / pScale.x : 1f,
                    pScale.y != 0 ? sourceScale.y / pScale.y : 1f,
                    pScale.z != 0 ? sourceScale.z / pScale.z : 1f
                );

                GameObject ghostObj;
                if (customGhostPrefab != null)
                {
                    ghostObj = Instantiate(customGhostPrefab, snapTarget);
                    ghostObj.transform.localPosition = Vector3.zero;
                    ghostObj.transform.localRotation = Quaternion.identity;
                    ghostObj.transform.localScale = calculatedScale;

                    // Ensure ghost has NO colliders to prevent any physics or clipping issues
                    foreach (var col in ghostObj.GetComponentsInChildren<Collider>(true))
                    {
                        if (Application.isPlaying) Destroy(col);
                        else DestroyImmediate(col);
                    }

                    foreach (var tr in ghostObj.GetComponentsInChildren<Transform>(true))
                    {
                        tr.gameObject.layer = ghostLayer;
                    }
                }
                else
                {
                    string nameSuffix = sock != null ? sock.name : snapTarget.name;
                    ghostObj = new GameObject($"{gameObject.name}_Ghost_{nameSuffix}");
                    ghostObj.layer = ghostLayer;
                    ghostObj.transform.SetParent(snapTarget, false);
                    ghostObj.transform.localPosition = Vector3.zero;
                    ghostObj.transform.localRotation = Quaternion.identity;
                    ghostObj.transform.localScale = calculatedScale;

                    CloneVisualHierarchy(transform, ghostObj.transform, runtimeGhostMat, sock, snapTarget);

                    // Ensure ghost has NO colliders
                    foreach (var col in ghostObj.GetComponentsInChildren<Collider>(true))
                    {
                        if (Application.isPlaying) Destroy(col);
                        else DestroyImmediate(col);
                    }
                }

                GhostPreviewTarget targetComp = ghostObj.GetComponent<GhostPreviewTarget>();
                if (targetComp == null) targetComp = ghostObj.AddComponent<GhostPreviewTarget>();
                targetComp.OwnerPart = this;
                targetComp.TargetSocket = sock;
                targetComp.TargetSnapPoint = snapTarget;

                // Apply default ghost material to all renderers
                foreach (var rend in ghostObj.GetComponentsInChildren<Renderer>(true))
                {
                    if (rend != null && runtimeGhostMat != null)
                    {
                        rend.sharedMaterial = runtimeGhostMat;
                        ghostRenderers.Add(rend);
                    }
                }

                activeGhostInstances.Add(ghostObj);
                if (ghostInstance == null) ghostInstance = ghostObj;
            }
        }

        private void CloneVisualHierarchy(Transform source, Transform destination, Material ghostMat, AssemblySocket targetSock = null, Transform targetSnap = null)
        {
            int ghostLayer = LayerMask.NameToLayer("TransparentFX");
            if (ghostLayer < 0) ghostLayer = 1;
            destination.gameObject.layer = ghostLayer;

            // Copy MeshFilter & MeshRenderer if present (visuals only, NO colliders!)
            if (source.TryGetComponent<MeshFilter>(out var mf) && source.TryGetComponent<MeshRenderer>(out _))
            {
                MeshFilter newMf = destination.gameObject.AddComponent<MeshFilter>();
                newMf.sharedMesh = mf.sharedMesh;

                MeshRenderer newMr = destination.gameObject.AddComponent<MeshRenderer>();
                newMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                newMr.receiveShadows = false;
                if (ghostMat != null) newMr.sharedMaterial = ghostMat;

                GhostPreviewTarget targetComp = destination.gameObject.GetComponent<GhostPreviewTarget>();
                if (targetComp == null) targetComp = destination.gameObject.AddComponent<GhostPreviewTarget>();
                targetComp.OwnerPart = this;
                targetComp.TargetSocket = targetSock;
                targetComp.TargetSnapPoint = targetSnap;
            }

            // Recursively clone visual children
            for (int i = 0; i < source.childCount; i++)
            {
                Transform childSource = source.GetChild(i);

                // Skip if it's already a ghost, flash, or collider-only object
                if (childSource.name.Contains("Ghost") || childSource.name.Contains("SnapFlash")) continue;

                GameObject childDest = new GameObject(childSource.name);
                childDest.transform.SetParent(destination, false);
                childDest.transform.localPosition = childSource.localPosition;
                childDest.transform.localRotation = childSource.localRotation;
                childDest.transform.localScale = childSource.localScale;

                CloneVisualHierarchy(childSource, childDest.transform, ghostMat, targetSock, targetSnap);
            }
        }

        private Material GetOrCreateGhostMaterial(Color? overrideColor = null)
        {
            Color col = overrideColor ?? ghostDefaultColor;

            if (ghostMaterial != null)
            {
                Material m = new Material(ghostMaterial);
                if (m.HasProperty(BaseColorProp)) m.SetColor(BaseColorProp, col);
                else m.color = col;
                return m;
            }

            // Try custom URP shader first
            Shader shader = Shader.Find("EngineAssembly/GhostHologramURP");
            if (shader == null)
            {
                // Fallback to URP unlit
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material mat = new Material(shader)
            {
                name = $"GhostMat_{col.r:F2}_{col.g:F2}_{col.b:F2}"
            };

            // Set default translucent color and rim
            if (mat.HasProperty(BaseColorProp))
            {
                mat.SetColor(BaseColorProp, col);
            }
            else
            {
                mat.color = col;
            }

            if (mat.HasProperty(RimColorProp))
            {
                mat.SetColor(RimColorProp, col * 1.4f);
            }

            // Explicitly set shader animation and alpha properties
            mat.SetFloat("_AlphaMultiplier", 0.85f);
            mat.SetFloat("_RimPower", 2.2f);
            mat.SetFloat("_PulseSpeed", 2.5f);
            mat.SetFloat("_PulseIntensity", 0.25f);
            mat.SetFloat("_CullMode", (float)UnityEngine.Rendering.CullMode.Off);
            mat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);

            // Enable transparency flags on standard URP shaders
            mat.SetFloat("_Surface", 1.0f); // Transparent
            mat.SetFloat("_Blend", 0.0f);   // Alpha blend
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 100;

            return mat;
        }

        private void SetGhostVisible(bool visible)
        {
            for (int i = 0; i < activeGhostInstances.Count; i++)
            {
                var g = activeGhostInstances[i];
                if (g != null) g.SetActive(visible);
            }
        }

        private void UpdateGhostTransform()
        {
            // Sockets are stationary or tracked via parent transform
        }

        /// <summary>
        /// Highlights the ghost in green/blue when the player is aiming at it.
        /// If a specific socket is targeted among multiple interchangeable sockets, only that socket's ghost highlights.
        /// </summary>
        public void SetGhostHighlight(bool highlighted)
        {
            SetGhostHighlight(highlighted, null, null);
        }

        public void SetGhostHighlight(bool highlighted, AssemblySocket targetedSocket)
        {
            SetGhostHighlight(highlighted, targetedSocket, null);
        }

        public void SetGhostHighlight(bool highlighted, AssemblySocket targetedSocket, Transform targetedSnapPoint)
        {
            isGhostHighlighted = highlighted;
            bool prereqsMet = ArePrerequisitesMet();

            for (int i = 0; i < activeGhostInstances.Count; i++)
            {
                var g = activeGhostInstances[i];
                if (g == null) continue;

                GhostPreviewTarget targetComp = g.GetComponent<GhostPreviewTarget>();

                // ONLY highlight the specific ghost whose socket or snap point matches!
                bool isThisGhostTargeted = false;
                if (highlighted)
                {
                    if (targetedSocket != null && targetComp != null && targetComp.TargetSocket == targetedSocket)
                    {
                        isThisGhostTargeted = true;
                    }
                    else if (targetedSnapPoint != null && targetComp != null && targetComp.TargetSnapPoint == targetedSnapPoint)
                    {
                        isThisGhostTargeted = true;
                    }
                    else if (targetedSocket == null && targetedSnapPoint == null && activeGhostInstances.Count == 1)
                    {
                        isThisGhostTargeted = true;
                    }
                }

                Material chosenMat;
                if (!prereqsMet && isThisGhostTargeted)
                {
                    chosenMat = runtimeGhostLockedMat;
                }
                else if (isThisGhostTargeted)
                {
                    chosenMat = runtimeGhostInRangeMat;
                }
                else
                {
                    chosenMat = runtimeGhostMat; // All other interchangeable sockets remain translucent blue!
                }

                foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                {
                    if (r != null && chosenMat != null) r.sharedMaterial = chosenMat;
                }
            }
        }

        private void UpdateGhostVisualState()
        {
            SetGhostHighlight(isGhostHighlighted, null, null);
        }

        /// <summary>
        /// Snaps this part into a specific socket chosen by the player (e.g. one of multiple interchangeable sockets).
        /// </summary>
        public bool SnapToSocket(AssemblySocket socket, Transform snapPoint = null)
        {
            if (socket != null)
            {
                targetSocket = socket;
                targetSnapPoint = socket.SnapTransform;
            }
            else if (snapPoint != null)
            {
                targetSnapPoint = snapPoint;
            }

            return TrySnap();
        }

        #endregion

        #region Public Helpers / Editor Actions

        /// <summary>
        /// Editor helper to generate or align the ghost preview directly in the Scene view.
        /// </summary>
        public void PreviewGhostInEditor(bool show)
        {
            if (show)
            {
                if (ghostInstance != null)
                {
                    if (Application.isPlaying) Destroy(ghostInstance);
                    else DestroyImmediate(ghostInstance);
                    ghostInstance = null;
                }
                SetupGhost();
                SetGhostVisible(true);
                UpdateGhostTransform();
                UpdateGhostVisualState();
            }
            else
            {
                SetGhostVisible(false);
                if (!Application.isPlaying && ghostInstance != null)
                {
                    DestroyImmediate(ghostInstance);
                    ghostInstance = null;
                }
            }
        }

        /// <summary>
        /// Resets the part back to its starting world position.
        /// </summary>
        public void ResetToInitialPosition()
        {
            if (isSnapped) Unsnap(force: true);
            transform.position = initialPosition;
            transform.rotation = initialRotation;
        }

        #endregion

        private void OnDrawGizmos()
        {
            Transform target = TargetSnapPoint;
            if (target == null) return;

            // Draw line connecting part to target
            Gizmos.color = isSnapped ? Color.green : (isInSnapZone ? Color.yellow : new Color(0.2f, 0.75f, 1f, 0.4f));
            Gizmos.DrawLine(transform.position, target.position);

            // Draw snap zone sphere around target
            Gizmos.color = isInSnapZone ? new Color(0.2f, 1f, 0.4f, 0.6f) : new Color(0.2f, 0.75f, 1f, 0.25f);
            Gizmos.DrawWireSphere(target.position, snapDistanceThreshold);
        }

        private void OnDestroy()
        {
            CleanupAllGhosts();
            if (runtimeGhostMat != null) Destroy(runtimeGhostMat);
            if (runtimeGhostInRangeMat != null) Destroy(runtimeGhostInRangeMat);
            if (runtimeGhostLockedMat != null) Destroy(runtimeGhostLockedMat);
        }
    }
}
