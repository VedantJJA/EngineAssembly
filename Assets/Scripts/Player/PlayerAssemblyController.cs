using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EngineAssembly
{
    public enum HudCorner
    {
        BottomRight,
        BottomLeft
    }

    /// <summary>
    /// First-person player controller attached to a Capsule.
    /// Handles walking, mouse look, crosshair targeting, and picking up/carrying/snapping engine parts.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class PlayerAssemblyController : MonoBehaviour
    {
        [Header("Camera & Head")]
        [Tooltip("The camera representing player's eyes (child of this Capsule).")]
        [SerializeField] private Camera playerCamera;

        [Tooltip("Transform in front of camera where the held part will float.")]
        [SerializeField] private Transform holdPoint;

        [Header("Camera Zoom Settings")]
        [Tooltip("Default field of view when not zooming.")]
        [SerializeField] private float defaultFov = 60f;

        [Tooltip("Field of view when zooming with empty hands.")]
        [SerializeField] private float zoomFov = 32f;

        [Tooltip("Speed of transition into and out of zoom.")]
        [SerializeField] private float zoomSpeed = 10f;

        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float sprintSpeed = 7.0f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float jumpHeight = 1.0f;

        [Header("Crawl Settings")]
        [Tooltip("Movement speed while crawling.")]
        [SerializeField] private float crawlSpeed = 1.8f;

        [Tooltip("Height of the CharacterController capsule while crawling (ultra-low prone stance).")]
        [SerializeField] private float crawlHeight = 0.35f;

        [Tooltip("Radius of the CharacterController capsule while crawling.")]
        [SerializeField] private float crawlRadius = 0.17f;

        [Tooltip("Eye height of the camera above the ground/floor while crawling (e.g. 0.20m = 20cm above the floor).")]
        [SerializeField] private float crawlCameraY = 0.20f;

        [Tooltip("Speed of transition between standing and crawling.")]
        [SerializeField] private float crawlTransitionSpeed = 10f;

        [Tooltip("If true, pressing the crawl key toggles crawling. If false, player must hold the crawl key.")]
        [SerializeField] private bool toggleCrawl = true;

        [Tooltip("Optional transform of the player visual body/mesh. If unassigned, auto-detected or created so camera is never distorted.")]
        [SerializeField] private Transform visualBody;

        [Tooltip("Layer mask used to check for ceiling obstructions before standing up.")]
        [SerializeField] private LayerMask ceilingCheckMask = ~0;

        [Header("Mouse Look Settings")]
        [SerializeField] private float mouseSensitivity = 2.0f;
        [SerializeField] private float maxPitchAngle = 85f;
        [SerializeField] private float minPitchAngle = -85f;

        [Header("Pickup & Interaction")]
        [Tooltip("Maximum distance to reach and pick up an engine part.")]
        [SerializeField] private float pickupDistance = 2.5f;

        [Tooltip("Maximum distance to click on the ghost or socket to snap the held part.")]
        [SerializeField] private float snapReachDistance = 5.0f;
        [SerializeField] private LayerMask pickupLayerMask = ~0;

        [Tooltip("How quickly the held part interpolates to the hold point position.")]
        [SerializeField] private float carrySmoothSpeed = 20f;

        [Tooltip("Rotation speed of the held object when right-click rotating.")]
        [SerializeField] private float partRotationSpeed = 3f;

        [Header("Dynamic Hold & Part Sizing")]
        [Tooltip("Default distance from camera to held object.")]
        [SerializeField] private float baseHoldDistance = 1.0f;

        [Tooltip("Minimum and maximum hold distance using scroll wheel.")]
        [SerializeField] private float minHoldDistance = 0.5f;
        [SerializeField] private float maxHoldDistance = 3.5f;

        [Tooltip("Mouse scroll sensitivity for adjusting hold distance.")]
        [SerializeField] private float scrollSensitivity = 0.5f;

        [Header("Interaction Timing & Debounce")]
        [Tooltip("Cooldown time in seconds between picking up and dropping/snapping to prevent accidental double-clicks.")]
        [SerializeField] private float interactionDebounceDuration = 0.35f;

        [Header("Hover Removal Highlight (Pointing at Removable Part)")]
        [Tooltip("How quickly the hover highlight gently pulses when pointing at a removable part.")]
        [SerializeField] private float hoverHighlightPulseSpeed = 2.0f;
        [Tooltip("Minimum alpha/intensity for hover highlight so part remains clearly visible.")]
        [SerializeField] private float hoverHighlightMinAlpha = 0.05f;
        [Tooltip("Maximum alpha/intensity for hover highlight so part remains clearly visible.")]
        [SerializeField] private float hoverHighlightMaxAlpha = 0.22f;

        public float HoverHighlightPulseSpeed
        {
            get => hoverHighlightPulseSpeed;
            set => hoverHighlightPulseSpeed = value;
        }

        public float HoverHighlightMinAlpha
        {
            get => hoverHighlightMinAlpha;
            set => hoverHighlightMinAlpha = value;
        }

        public float HoverHighlightMaxAlpha
        {
            get => hoverHighlightMaxAlpha;
            set => hoverHighlightMaxAlpha = value;
        }

        [Header("HUD / Crosshair")]
        [SerializeField] private bool showCrosshair = true;
        [SerializeField] private Color crosshairNormalColor = new Color(1f, 1f, 1f, 0.6f);
        [SerializeField] private Color crosshairHoverColor = new Color(0.2f, 1f, 0.5f, 0.9f);
        [SerializeField] private float crosshairSize = 6f;

        [Header("Bottom-Corner Game HUD")]
        [Tooltip("Corner of the screen where interaction prompts and action cards are displayed like actual games.")]
        [SerializeField] private HudCorner hudCorner = HudCorner.BottomRight;
        [Tooltip("Whether to display the bottom-corner interaction action card.")]
        [SerializeField] private bool showHudActionCard = true;

        [Header("Guidance & Hints")]
        [Tooltip("Key to flash the next part of the assembly process.")]
        [SerializeField] private KeyCode nextAssemblyHintKey = KeyCode.N;

        [Tooltip("Key to flash the next part of the disassembly process.")]
        [SerializeField] private KeyCode nextDisassemblyHintKey = KeyCode.B;

        [Header("Workshop Edit & Disassembly Recording Mode")]
        [Tooltip("Key to toggle Workshop Edit Mode (reposition parts, record disassembly order). Default is F6.")]
        [SerializeField] private KeyCode recordingToggleKey = KeyCode.F6;

        [Tooltip("Key to decrement the current recording index (default J).")]
        [SerializeField] private KeyCode recordingIndexDownKey = KeyCode.J;

        [Tooltip("Key to increment the current recording index (default L).")]
        [SerializeField] private KeyCode recordingIndexUpKey = KeyCode.L;

        [Tooltip("Key to increment the current recording group on the current index (default I).")]
        [SerializeField] private KeyCode recordingGroupUpKey = KeyCode.I;

        [Tooltip("Key to decrement the current recording group on the current index (default K).")]
        [SerializeField] private KeyCode recordingGroupDownKey = KeyCode.K;

        [Tooltip("Initial index to start recording from.")]
        [SerializeField] private int recordingStartIndex = 1;

        [Tooltip("Initial group to start recording from.")]
        [SerializeField] private int recordingStartGroup = 1;

        [Tooltip("If true, index auto-advances after each disassembled part so disassembling sets the sequential assembly index. If false, index stays the same until changed with J/L.")]
        [SerializeField] private bool autoAdvanceIndexOnDisassemble = true;

        [Tooltip("Key to toggle auto-advancing index on disassembly (default X).")]
        [SerializeField] private KeyCode toggleAutoAdvanceKey = KeyCode.X;

        [Tooltip("Key to permanently save the recorded disassembly sequence and scene layout to disk (default U).")]
        [SerializeField] private KeyCode permanentSaveKey = KeyCode.U;

        [Tooltip("Key to temporarily save the recorded disassembly sequence in memory without writing to disk (default T).")]
        [SerializeField] private KeyCode temporarySaveKey = KeyCode.T;

        [Tooltip("Key to undo the last recorded disassembly step and auto re-assemble the part (default Z).")]
        [SerializeField] private KeyCode undoRecordKey = KeyCode.Z;

        [Tooltip("Key to auto-assemble all parts onto the engine for disassembly recording (default Y).")]
        [SerializeField] private KeyCode autoAssembleKey = KeyCode.Y;

        [Header("Step-by-Step Auto-Assembly")]
        [Tooltip("Delay in seconds between consecutive part snaps during automated step-by-step assembly (faster than normal speed, not too slow).")]
        [SerializeField] private float stepByStepAssembleDelay = 0.14f;

        public float StepByStepAssembleDelay
        {
            get => stepByStepAssembleDelay;
            set => stepByStepAssembleDelay = Mathf.Max(0.01f, value);
        }

        [Header("Workbench Layout / Repositioning")]
        [Tooltip("When enabled, any unassembled parts you place/move while in Play Mode will have their positions permanently saved back to the scene upon exiting Play Mode.")]
        [SerializeField] private bool savePlacedPartsToSceneOnExit = true;

        [Tooltip("When enabled, the player character's own position and orientation will also be saved back to the scene upon exiting Play Mode.")]
        [SerializeField] private bool savePlayerPositionOnExit = true;

        public bool SavePlacedPartsToSceneOnExit
        {
            get => savePlacedPartsToSceneOnExit;
            set => savePlacedPartsToSceneOnExit = value;
        }

        public bool SavePlayerPositionOnExit
        {
            get => savePlayerPositionOnExit;
            set => savePlayerPositionOnExit = value;
        }

        // Components & State
        private CharacterController characterController;
        private Vector3 velocity;
        private float cameraPitch = 0f;
        private bool isGrounded;
        private bool isCrawling = false;
        private float standingHeight = 2.0f;
        private float standingRadius = 0.5f;
        private Vector3 standingCenter = new Vector3(0f, 1.0f, 0f);
        private float standingCameraY = 0.75f;
        private float standingEyeOffsetFromBottom = 1.75f;
        private Vector3 initialVisualScale = Vector3.one;
        private Vector3 initialVisualLocalPos = Vector3.zero;

        // Held Object State
        private AssemblyPart currentHeldPart;
        private Rigidbody heldRb;
        private Collider[] heldColliders;
        private Quaternion heldRelativeRotation = Quaternion.identity;
        private bool isHoveringPart = false;
        private AssemblyPart hoveredPart = null;
        private bool isTargetingGhost = false;
        private Dictionary<Renderer, int[]> heldPartRenderQueues = new Dictionary<Renderer, int[]>();

        // Hover Removal Highlight (fast flashing on the part that will be removed when clicked)
        private GameObject currentHoverHighlightObj = null;
        private Material currentHoverHighlightMat = null;
        private AssemblyPart currentHoverHighlightedPart = null;

        // Dynamic hold distance and debounce timers
        private float currentHoldDistance = 1.0f;
        private float currentHoldYOffset = -0.2f;
        private float interactionDebounceTimer = 0f;
        private bool requireMouseReleaseBeforeAction = false;

        // Disassembly Recording Mode
        private bool isRecordingDisassembly = false;
        private int recordingCurrentIndex = 1;
        private int recordingCurrentGroup = 1;
        private float savedNotificationTimer = 0f;
        private string savedNotificationText = "";
        private List<(AssemblyPart part, int assignedIndex)> recordingLog = new List<(AssemblyPart, int)>();

        public static PlayerAssemblyController Instance { get; private set; }
        public static event System.Action OnSaveLayoutRequested;

        public AssemblyPart CurrentHeldPart => currentHeldPart;
        public bool IsHoldingPart => currentHeldPart != null;
        public bool IsCrawling => isCrawling;
        public bool IsRecordingDisassembly => isRecordingDisassembly;
        public bool IsEditModeActive => isRecordingDisassembly;
        public bool IsInEditMode => isRecordingDisassembly;

        public void ShowSaveToastNotification(string message, float duration = 2.5f)
        {
            savedNotificationText = message;
            savedNotificationTimer = duration;
        }
        public KeyCode EditModeToggleKey
        {
            get => recordingToggleKey;
            set => recordingToggleKey = value;
        }
        public int RecordingCurrentIndex
        {
            get => recordingCurrentIndex;
            set => recordingCurrentIndex = Mathf.Max(1, value);
        }
        public int RecordingCurrentGroup
        {
            get => recordingCurrentGroup;
            set => recordingCurrentGroup = Mathf.Max(1, value);
        }
        public bool AutoAdvanceIndexOnDisassemble
        {
            get => autoAdvanceIndexOnDisassemble;
            set => autoAdvanceIndexOnDisassemble = value;
        }

        public KeyCode PermanentSaveKey
        {
            get => permanentSaveKey;
            set => permanentSaveKey = value;
        }

        public KeyCode TemporarySaveKey
        {
            get => temporarySaveKey;
            set => temporarySaveKey = value;
        }

        public IReadOnlyList<DisassemblyRecordInfo> TemporarySavedStates => temporarySavedStates;
        private List<DisassemblyRecordInfo> temporarySavedStates = new List<DisassemblyRecordInfo>();

        public void ToggleDisassemblyRecordingMode()
        {
            if (isRecordingDisassembly)
                StopRecordingMode();
            else
                StartRecordingMode();
        }

        public void AdjustRecordingIndex(int delta)
        {
            recordingCurrentIndex = Mathf.Max(1, recordingCurrentIndex + delta);
            recordingCurrentGroup = 1;
            HighlightCurrentEditModeGroup();
            ShowSaveToastNotification($"Index set to #{recordingCurrentIndex} (Group: #{recordingCurrentGroup}) [J: Prev, L: Next]", 1.8f);
        }

        public void AdjustRecordingGroup(int delta)
        {
            recordingCurrentGroup = Mathf.Max(1, recordingCurrentGroup + delta);
            HighlightCurrentEditModeGroup();
            ShowSaveToastNotification($"Group set to #{recordingCurrentGroup} on Index #{recordingCurrentIndex} [K: Decr, I: Incr]", 1.8f);
        }

        public IReadOnlyList<(AssemblyPart part, int assignedIndex)> RecordingLog => recordingLog;
        public IReadOnlyList<DisassemblyRecordInfo> RecordedDisassemblyStates => recordedDisassemblyStates;

        [System.Serializable]
        public class DisassemblyRecordInfo
        {
            public AssemblyPart part;
            public string partDisplayName;
            public string partId;
            public int assignedIndex;
            public int groupIndex;
            public int stepNumber;
            public bool isSubAssembly;
            public string subAssemblyName;
            public Vector3 worldPosition;
            public Quaternion worldRotation;
            public float timestamp;
            public string stateDescription;
        }

        [System.Serializable]
        public class DisassemblySequenceSaveFile
        {
            public string savedTimestamp;
            public int totalSteps;
            public List<DisassemblyRecordInfo> recordedSteps;
        }

        private List<DisassemblyRecordInfo> recordedDisassemblyStates = new List<DisassemblyRecordInfo>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            autoAdvanceIndexOnDisassemble = true;

            // Enforce correct hotkey assignments: J = Prev Index, L = Next Index, K = Group Down, I = Group Up
            if (recordingIndexDownKey == recordingGroupDownKey || recordingIndexDownKey == KeyCode.K)
            {
                recordingIndexDownKey = KeyCode.J;
            }
            if (recordingGroupDownKey == KeyCode.J)
            {
                recordingGroupDownKey = KeyCode.K;
            }
            if (recordingIndexUpKey == KeyCode.None) recordingIndexUpKey = KeyCode.L;
            if (recordingGroupUpKey == KeyCode.None) recordingGroupUpKey = KeyCode.I;

            characterController = GetComponent<CharacterController>();
            if (characterController != null)
            {
                standingHeight = characterController.height;
                standingRadius = characterController.radius;
                standingCenter = characterController.center;
            }

            // Auto-adapt any legacy serialized inspector values to the new ultra-low stance
            if (crawlHeight > 0.5f) crawlHeight = 0.35f;
            if (crawlRadius > crawlHeight * 0.49f) crawlRadius = crawlHeight * 0.48f;
            if (crawlCameraY > 0.3f) crawlCameraY = 0.20f;

            // Auto-locate or create camera if missing
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
                if (playerCamera == null)
                {
                    Camera mainCam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
                    if (mainCam != null && mainCam.transform.parent != transform)
                    {
                        mainCam.transform.SetParent(transform);
                        mainCam.transform.localPosition = new Vector3(0f, 0.75f, 0f); // Head height on 2m capsule
                        mainCam.transform.localRotation = Quaternion.identity;
                        playerCamera = mainCam;
                    }
                }
            }

            if (playerCamera != null)
            {
                // Prevent large engine parts from clipping through the camera near plane
                playerCamera.nearClipPlane = 0.03f;
                defaultFov = playerCamera.fieldOfView;
                standingCameraY = playerCamera.transform.localPosition.y;
                float standingBottomY = standingCenter.y - (standingHeight * 0.5f);
                standingEyeOffsetFromBottom = standingCameraY - standingBottomY;
            }

            // Auto-create hold point in front of camera
            if (holdPoint == null && playerCamera != null)
            {
                GameObject hp = new GameObject("HoldPoint");
                hp.transform.SetParent(playerCamera.transform, false);
                hp.transform.localPosition = new Vector3(0f, currentHoldYOffset, currentHoldDistance);
                holdPoint = hp.transform;
            }

            // Setup or auto-detect player visual body mesh for morphing
            SetupVisualBody();

            // Lock and hide cursor for FPS controls
            LockCursor(true);
        }

        private void OnDisable()
        {
            StopHeldGroupHighlight();
            ClearHoverHighlight();
        }

        private void OnDestroy()
        {
            ClearHoverHighlight();
        }

        private void SetupVisualBody()
        {
            if (visualBody == null)
            {
                // 1. Search for existing child visual body (excluding camera and holdPoint)
                foreach (Transform child in transform)
                {
                    if (playerCamera != null && child == playerCamera.transform) continue;
                    if (holdPoint != null && child == holdPoint) continue;

                    if (child.GetComponentInChildren<MeshRenderer>() != null)
                    {
                        visualBody = child;
                        break;
                    }
                }

                // 2. If root has MeshRenderer / MeshFilter, safely migrate to a child to avoid camera non-uniform scale distortion
                if (visualBody == null)
                {
                    MeshFilter rootMf = GetComponent<MeshFilter>();
                    MeshRenderer rootMr = GetComponent<MeshRenderer>();
                    if (rootMf != null && rootMr != null)
                    {
                        GameObject visObj = new GameObject("PlayerVisualBody");
                        visObj.transform.SetParent(transform, false);
                        visObj.transform.localPosition = standingCenter;
                        visObj.transform.localRotation = Quaternion.identity;
                        visObj.transform.localScale = Vector3.one;

                        MeshFilter childMf = visObj.AddComponent<MeshFilter>();
                        childMf.sharedMesh = rootMf.sharedMesh;

                        MeshRenderer childMr = visObj.AddComponent<MeshRenderer>();
                        childMr.sharedMaterials = rootMr.sharedMaterials;
                        childMr.shadowCastingMode = rootMr.shadowCastingMode;
                        childMr.receiveShadows = rootMr.receiveShadows;

                        // Destroy root renderer and filter so root transform stays 1,1,1
                        Destroy(rootMr);
                        Destroy(rootMf);

                        visualBody = visObj.transform;
                    }
                }
            }

            if (visualBody != null)
            {
                initialVisualScale = visualBody.localScale;
                initialVisualLocalPos = visualBody.localPosition;

                // Ignore physics collisions between CharacterController and visual body colliders
                if (characterController != null)
                {
                    Collider[] visColliders = visualBody.GetComponentsInChildren<Collider>(true);
                    for (int i = 0; i < visColliders.Length; i++)
                    {
                        if (visColliders[i] != characterController)
                        {
                            Physics.IgnoreCollision(characterController, visColliders[i], true);
                        }
                    }
                }
            }
        }


        private void Update()
        {
            // Decrement debounce timer
            if (interactionDebounceTimer > 0f)
            {
                interactionDebounceTimer -= Time.deltaTime;
            }

            // Decrement saved notification timer
            if (savedNotificationTimer > 0f)
            {
                savedNotificationTimer -= Time.deltaTime;
            }

            // Reset requirement once mouse button has been released
            if (!IsLeftClickHeld())
            {
                requireMouseReleaseBeforeAction = false;
            }

            // Adjust hold distance with scroll wheel when carrying a part
            if (currentHeldPart != null)
            {
                float scroll = GetMouseScroll();
                if (Mathf.Abs(scroll) > 0.001f)
                {
                    currentHoldDistance = Mathf.Clamp(currentHoldDistance + scroll * scrollSensitivity, minHoldDistance, maxHoldDistance);
                }
            }

            HandleCursorToggle();
            HandleCrawl();
            HandleZoom();
            HandleMouseLook();
            HandleMovement();
            HandleInteraction();
            UpdateHoverHighlightAnimation();
            HandleHints();
            HandleRecordingMode();

            UpdateHeldPartPosition();
        }

        #region First-Person Movement, Crawl & Look

        private void HandleCrawl()
        {
            // Toggle or hold crawl mode
            if (toggleCrawl)
            {
                if (IsCrawlPressed())
                {
                    if (isCrawling)
                    {
                        if (CanStandUp())
                        {
                            isCrawling = false;
                        }
                        else
                        {
                            Debug.LogWarning("[PlayerAssemblyController] Cannot stand up: space above is obstructed.");
                        }
                    }
                    else
                    {
                        isCrawling = true;
                    }
                }
            }
            else
            {
                bool wantCrawl = IsCrawlHeld();
                if (wantCrawl)
                {
                    isCrawling = true;
                }
                else if (isCrawling)
                {
                    if (CanStandUp())
                    {
                        isCrawling = false;
                    }
                }
            }

            // If player attempts to sprint while crawling and space above is clear, stand up into sprint
            if (isCrawling && IsSprinting() && CanStandUp())
            {
                isCrawling = false;
            }

            // Target dimensions for CharacterController
            float targetHeight = isCrawling ? crawlHeight : standingHeight;
            float targetRadius = isCrawling ? Mathf.Min(crawlRadius, targetHeight * 0.49f) : standingRadius;
            float bottomY = standingCenter.y - (standingHeight * 0.5f);
            float targetCenterY = bottomY + (targetHeight * 0.5f);

            // Smoothly interpolate dimensions
            float currentH = characterController != null ? characterController.height : standingHeight;
            float currentR = characterController != null ? characterController.radius : standingRadius;
            float currentCY = characterController != null ? characterController.center.y : targetCenterY;

            float newHeight = Mathf.Lerp(currentH, targetHeight, Time.deltaTime * crawlTransitionSpeed);
            float newRadius = Mathf.Lerp(currentR, targetRadius, Time.deltaTime * crawlTransitionSpeed);
            newRadius = Mathf.Min(newRadius, newHeight * 0.49f); // Enforce PhysX height >= 2 * radius constraint
            float newCenterY = Mathf.Lerp(currentCY, targetCenterY, Time.deltaTime * crawlTransitionSpeed);

            // Update CharacterController (order-sensitive to satisfy height >= 2*radius at each assignment)
            if (characterController != null)
            {
                if (newHeight < characterController.height)
                {
                    characterController.radius = newRadius;
                    characterController.height = newHeight;
                }
                else
                {
                    characterController.height = newHeight;
                    characterController.radius = newRadius;
                }
                characterController.center = new Vector3(standingCenter.x, newCenterY, standingCenter.z);
            }

            // Morph player visual body object accordingly
            if (visualBody != null)
            {
                float heightRatio = standingHeight > 0.001f ? (newHeight / standingHeight) : 1f;
                float radiusRatio = standingRadius > 0.001f ? (newRadius / standingRadius) : 1f;

                visualBody.localScale = new Vector3(
                    initialVisualScale.x * radiusRatio,
                    initialVisualScale.y * heightRatio,
                    initialVisualScale.z * radiusRatio
                );
                // Keep the visual mesh anchored cleanly to the floor at bottomY
                float targetVisualY = bottomY + (newHeight * 0.5f);
                visualBody.localPosition = new Vector3(initialVisualLocalPos.x, targetVisualY, initialVisualLocalPos.z);
            }

            // Smoothly interpolate camera local Y position grounded to the floor
            if (playerCamera != null)
            {
                // When crawling, eye height is clamped safely inside the crawling capsule (e.g. 15-20cm above the floor)
                float maxCrawlEyeHeight = Mathf.Max(0.08f, targetHeight - 0.04f);
                float crawlEyeHeight = Mathf.Clamp(crawlCameraY, 0.08f, maxCrawlEyeHeight);
                float targetEyeOffset = isCrawling ? crawlEyeHeight : standingEyeOffsetFromBottom;
                float targetCamY = bottomY + targetEyeOffset;

                Vector3 camPos = playerCamera.transform.localPosition;
                camPos.y = Mathf.Lerp(camPos.y, targetCamY, Time.deltaTime * crawlTransitionSpeed);
                playerCamera.transform.localPosition = camPos;
            }
        }

        /// <summary>
        /// Checks if there is sufficient headroom above the player to stand up without clipping through obstacles.
        /// </summary>
        public bool CanStandUp()
        {
            if (characterController == null) return true;

            float radius = standingRadius * 0.85f;
            float bottomY = standingCenter.y - (standingHeight * 0.5f);
            Vector3 currentTop = transform.position + Vector3.up * (bottomY + characterController.height);
            Vector3 standingTop = transform.position + Vector3.up * (bottomY + standingHeight);

            if (standingTop.y <= currentTop.y + 0.05f) return true;

            Vector3 point1 = currentTop + Vector3.up * radius;
            Vector3 point2 = standingTop - Vector3.up * radius;
            if (point2.y < point1.y) point2 = point1;

            Collider[] colliders = Physics.OverlapCapsule(point1, point2, radius, ceilingCheckMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col == null || col == characterController || col.transform.IsChildOf(transform)) continue;
                if (currentHeldPart != null && col.transform.IsChildOf(currentHeldPart.transform)) continue;
                return false;
            }
            return true;
        }

        public void ToggleCrawl()
        {
            if (isCrawling)
            {
                if (CanStandUp())
                {
                    isCrawling = false;
                }
            }
            else
            {
                isCrawling = true;
            }
        }

        public void SetCrawling(bool crawl)
        {
            if (!crawl && isCrawling && !CanStandUp()) return;
            isCrawling = crawl;
        }

        private void HandleZoom()
        {
            if (playerCamera == null) return;

            // Only zoom when player has no part in hand and holds Right Mouse Button
            bool isZooming = (currentHeldPart == null) && IsRightClickHeld();
            float targetFov = isZooming ? zoomFov : defaultFov;
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, Time.deltaTime * zoomSpeed);
        }

        private void HandleMouseLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;

            // When carrying a part and holding right click to rotate it, freeze camera look completely
            if (currentHeldPart != null && IsRightClickHeld())
            {
                return;
            }

            // Smoothly adjust sensitivity when zoomed with empty hands
            float currentSensitivity = mouseSensitivity;
            if (playerCamera != null && playerCamera.fieldOfView < defaultFov - 5f)
            {
                currentSensitivity *= (playerCamera.fieldOfView / defaultFov);
            }

            Vector2 lookInput = GetLookInput() * currentSensitivity;

            // Horizontal rotation (Yaw) rotates Capsule
            transform.Rotate(Vector3.up * lookInput.x);

            // Vertical rotation (Pitch) rotates Camera only
            cameraPitch -= lookInput.y;
            cameraPitch = Mathf.Clamp(cameraPitch, minPitchAngle, maxPitchAngle);
            if (playerCamera != null)
            {
                playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
            }
        }

        private void HandleMovement()
        {
            isGrounded = characterController.isGrounded;
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f; // Slight downward force to stay grounded
            }

            Vector2 moveInput = GetMovementInput();
            
            float speed;
            if (isCrawling)
            {
                speed = crawlSpeed;
            }
            else if (IsSprinting())
            {
                speed = sprintSpeed;
            }
            else
            {
                speed = walkSpeed;
            }

            Vector3 moveDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
            characterController.Move(moveDirection * (speed * Time.deltaTime));

            // Jump / Stand up from crawl
            if (IsJumpPressed() && isGrounded)
            {
                if (isCrawling)
                {
                    if (CanStandUp())
                    {
                        isCrawling = false;
                    }
                }
                else
                {
                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }

            // Gravity
            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);
        }

        private void HandleCursorToggle()
        {
            // Press Escape, Alt, or Tab to free cursor, click anywhere in game view to relock
            if (IsEscapePressed() || InputHelper.IsKeyDown(KeyCode.LeftAlt) || InputHelper.IsKeyDown(KeyCode.RightAlt) || InputHelper.IsKeyDown(KeyCode.Tab))
            {
                LockCursor(Cursor.lockState != CursorLockMode.Locked);
            }
            else if (Cursor.lockState != CursorLockMode.Locked && IsLeftClickPressed())
            {
                // Do not re-lock cursor if clicking near the top-right button, edit banner, or HUD panels
                Vector2 mousePos = InputHelper.GetMousePosition();
                float guiY = Screen.height - mousePos.y;
                float guiX = mousePos.x;

                bool isOverTopRight = (guiY <= 70f && guiX >= Screen.width - 240f);
                float bannerW = 880f;
                float bannerX = (Screen.width - bannerW) * 0.5f;
                bool isOverBanner = isRecordingDisassembly && (guiY <= 95f && guiX >= bannerX && guiX <= bannerX + bannerW);
                bool isOverPanel = isRecordingDisassembly && (guiX <= 420f && guiY >= Screen.height - 380f);

                if (!isOverTopRight && !isOverBanner && !isOverPanel)
                {
                    LockCursor(true);
                }
            }
        }

        public void ToggleCursorLock()
        {
            LockCursor(Cursor.lockState != CursorLockMode.Locked);
        }

        private void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        #endregion

        #region Pickup & Snapping Interaction

        private bool CanTriggerInteractAction()
        {
            if (interactionDebounceTimer > 0f) return false;
            if (requireMouseReleaseBeforeAction && IsLeftClickHeld()) return false;
            return true;
        }

        private void HandleInteraction()
        {
            if (playerCamera == null) return;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            isHoveringPart = false;

            if (currentHeldPart == null)
            {
                hoveredPart = null;

                // Check what player is looking at
                if (Physics.Raycast(ray, out RaycastHit hit, pickupDistance, pickupLayerMask))
                {
                    AssemblyPart part = hit.collider.GetComponentInParent<AssemblyPart>();
                    if (part != null)
                    {
                        // If this part belongs to a mounted sub-assembly (and NOT in record mode), target the sub-assembly root so it can be removed as a whole!
                        if (!isRecordingDisassembly && part.ParentSubAssembly != null && part.ParentSubAssembly.RootPart != null && part.ParentSubAssembly.RootPart.IsSnapped)
                        {
                            part = part.ParentSubAssembly.RootPart;
                        }

                        hoveredPart = part;
                        isHoveringPart = true;

                        // Check if pointing at a part that will actually be removed / picked up when clicked
                        bool willBeRemovedOnClick = false;
                        if (part.IsSnapped)
                        {
                            willBeRemovedOnClick = isRecordingDisassembly || part.CanDisassemble(out _, out _);
                        }
                        else
                        {
                            willBeRemovedOnClick = true; // Loose part will be picked up on click
                        }

                        if (willBeRemovedOnClick)
                        {
                            SetHoverHighlight(part, true);
                        }
                        else
                        {
                            ClearHoverHighlight();
                        }

                        // Pick up on 'E' key or Left Mouse Click (with debounce protection against rapid multi-clicks)
                        if (CanTriggerInteractAction() && (IsInteractPressed() || IsLeftClickPressed()))
                        {
                            if (!isRecordingDisassembly && part.IsSnapped && !part.CanDisassemble(out string reason, out _))
                            {
                                Debug.LogWarning($"[PlayerAssemblyController] Cannot disassemble '{part.PartDisplayName}': {reason}");
                                interactionDebounceTimer = 0.25f;
                                requireMouseReleaseBeforeAction = true;
                                return;
                            }

                            ClearHoverHighlight();
                            PickUp(part);
                            return;
                        }
                    }
                    else
                    {
                        ClearHoverHighlight();
                    }
                }
                else
                {
                    ClearHoverHighlight();
                }
            }
            else
            {
                ClearHoverHighlight();
                hoveredPart = null;

                // Player is holding a part in hand
                bool isCrosshairOverGhost = false;
                AssemblySocket targetedGhostSocket = null;
                Transform targetedGhostSnapPoint = null;

                // Update ghost visibility and position from player camera view
                currentHeldPart.CheckGhostVisibility(playerCamera.transform.position);

                Ray camRay = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

                // Line-of-sight test: ghosts have NO colliders (preventing physics clipping, snagging, or glitches).
                // We mathematically test whether the camera crosshair ray intersects any active ghost's bounding volume.
                if (currentHeldPart.ActiveGhostInstances != null && currentHeldPart.ActiveGhostInstances.Count > 0)
                {
                    float closestGhostDist = float.MaxValue;
                    for (int i = 0; i < currentHeldPart.ActiveGhostInstances.Count; i++)
                    {
                        GameObject gObj = currentHeldPart.ActiveGhostInstances[i];
                        if (gObj == null || !gObj.activeInHierarchy) continue;

                        GhostPreviewTarget targetComp = gObj.GetComponent<GhostPreviewTarget>();
                        Transform snapPt = targetComp != null && targetComp.TargetSnapPoint != null ? targetComp.TargetSnapPoint : gObj.transform;

                        Renderer[] rends = gObj.GetComponentsInChildren<Renderer>(true);
                        for (int r = 0; r < rends.Length; r++)
                        {
                            Renderer rend = rends[r];
                            if (rend == null || !rend.enabled) continue;

                            // 1. Ray-box intersection with expanded bounds (generous tolerance for small bolts / pins)
                            Bounds expandedBounds = rend.bounds;
                            expandedBounds.Expand(0.12f);

                            bool hit = expandedBounds.IntersectRay(camRay, out float hitDist);

                            // 2. Conical distance check from ray to center (makes targeting small components effortless)
                            if (!hit)
                            {
                                float distAlongRay = Vector3.Dot(rend.bounds.center - camRay.origin, camRay.direction);
                                if (distAlongRay > 0f && distAlongRay <= snapReachDistance)
                                {
                                    Vector3 closestPtOnRay = camRay.origin + camRay.direction * distAlongRay;
                                    float distFromRay = Vector3.Distance(closestPtOnRay, rend.bounds.center);
                                    float maxTolerance = Mathf.Max(0.18f, rend.bounds.extents.magnitude + 0.08f);
                                    if (distFromRay <= maxTolerance)
                                    {
                                        hit = true;
                                        hitDist = distAlongRay;
                                    }
                                }
                            }

                            if (hit && hitDist <= snapReachDistance && hitDist < closestGhostDist)
                            {
                                closestGhostDist = hitDist;
                                if (targetComp != null && targetComp.OwnerPart == currentHeldPart)
                                {
                                    isCrosshairOverGhost = true;
                                    targetedGhostSocket = targetComp.TargetSocket;
                                    targetedGhostSnapPoint = snapPt;
                                }
                            }
                        }
                    }
                }

                isTargetingGhost = isCrosshairOverGhost;
                currentHeldPart.SetGhostHighlight(isCrosshairOverGhost, targetedGhostSocket, targetedGhostSnapPoint);

                // Allow rotating the held part by holding Right Mouse Button
                if (IsRightClickHeld())
                {
                    Vector2 lookDelta = GetLookInput();
                    heldRelativeRotation = Quaternion.Euler(lookDelta.y * partRotationSpeed, -lookDelta.x * partRotationSpeed, 0f) * heldRelativeRotation;
                }

                // Clicking to snap or drop:
                // Debounce ensures holding mouse down does not cause parts to drop or oscillate!
                if (CanTriggerInteractAction() && (IsInteractPressed() || IsLeftClickPressed()))
                {
                    if (isCrosshairOverGhost)
                    {
                        // Crosshair IS over the ghost -> Snap to position!
                        SnapHeldPartToTarget(targetedGhostSocket, targetedGhostSnapPoint);
                    }
                    else
                    {
                        // Proximity snap fallback: if the held part is physically near a valid ghost socket / snap point, snap it!
                        Transform bestSnapPoint = null;
                        AssemblySocket bestSocket = null;
                        float closestDist = 0.55f;

                        if (currentHeldPart.ActiveGhostInstances != null)
                        {
                            for (int i = 0; i < currentHeldPart.ActiveGhostInstances.Count; i++)
                            {
                                GameObject gObj = currentHeldPart.ActiveGhostInstances[i];
                                if (gObj == null || !gObj.activeInHierarchy) continue;
                                GhostPreviewTarget targetComp = gObj.GetComponent<GhostPreviewTarget>();
                                Transform snapPt = targetComp != null && targetComp.TargetSnapPoint != null ? targetComp.TargetSnapPoint : gObj.transform;

                                float distToHeld = Vector3.Distance(currentHeldPart.transform.position, snapPt.position);
                                if (distToHeld < closestDist)
                                {
                                    closestDist = distToHeld;
                                    bestSnapPoint = snapPt;
                                    bestSocket = targetComp != null ? targetComp.TargetSocket : null;
                                }
                            }
                        }

                        if (bestSnapPoint != null)
                        {
                            SnapHeldPartToTarget(bestSocket, bestSnapPoint);
                        }
                        else
                        {
                            // Crosshair is NOT over the ghost and not in proximity -> Place on workbench / floor!
                            DropHeldPart();
                        }
                    }
                }
                else if (IsDropPressed())
                {
                    DropHeldPart();
                }
            }
        }

        public void PickUp(AssemblyPart part)
        {
            if (part == null) return;

            ClearHoverHighlight();

            // If the part was already snapped, ensure it is eligible to be disassembled
            if (part.IsSnapped)
            {
                if (!isRecordingDisassembly && !part.CanDisassemble(out string reason, out _))
                {
                    Debug.LogWarning($"[PlayerAssemblyController] Cannot pick up/disassemble '{part.PartDisplayName}': {reason}");
                    return;
                }

                if (!part.Unsnap(force: isRecordingDisassembly))
                {
                    return;
                }
            }

            // Set debounce timer and require release to prevent accidental multi-clicks or drops
            interactionDebounceTimer = interactionDebounceDuration;
            requireMouseReleaseBeforeAction = true;

            currentHeldPart = part;
            heldRb = part.GetComponent<Rigidbody>();
            heldColliders = part.GetComponentsInChildren<Collider>(true);

            // Calculate size bounds of the part to prevent camera/environment clipping
            Bounds partBounds = CalculatePartBounds(part);
            float maxExtent = Mathf.Max(partBounds.extents.x, partBounds.extents.y, partBounds.extents.z);

            if (maxExtent > 0.3f)
            {
                // For large parts (like engine block), push hold distance outward and lower Y slightly
                currentHoldDistance = Mathf.Clamp(baseHoldDistance + (maxExtent - 0.2f) * 1.5f, minHoldDistance, maxHoldDistance);
                currentHoldYOffset = Mathf.Clamp(-0.25f - (maxExtent - 0.2f) * 0.4f, -0.65f, -0.2f);
            }
            else
            {
                currentHoldDistance = baseHoldDistance;
                currentHoldYOffset = -0.2f;
            }

            // Elevate render queue of held part to 3200 (Transparent+200)
            // This guarantees the held object in hand always draws AFTER the ghost hologram (Queue 3100),
            // so the ghost will NEVER overlap or draw on top of the object in hand!
            heldPartRenderQueues.Clear();
            Renderer[] renderers = part.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null || r.gameObject.name.Contains("Ghost") || r.gameObject.name.Contains("SnapFlash")) continue;
                Material[] mats = r.materials;
                int[] queues = new int[mats.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    queues[i] = mats[i].renderQueue;
                    mats[i].renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay + 500;
                }
                heldPartRenderQueues[r] = queues;
            }

            // Ignore collisions between player capsule and the held part
            foreach (var col in heldColliders)
            {
                if (col != null && characterController != null)
                {
                    Physics.IgnoreCollision(characterController, col, true);
                }
            }

            if (heldRb != null)
            {
                if (!heldRb.isKinematic)
                {
                    heldRb.linearVelocity = Vector3.zero;
                    heldRb.angularVelocity = Vector3.zero;
                }
                heldRb.useGravity = false;
                heldRb.isKinematic = true;
                heldRb.detectCollisions = false; // Disable collision resolution while held to prevent snagging and clipping
            }

            // Align relative rotation to hold point
            if (holdPoint != null)
            {
                holdPoint.localPosition = new Vector3(0f, currentHoldYOffset, currentHoldDistance);
                heldRelativeRotation = Quaternion.Inverse(holdPoint.rotation) * part.transform.rotation;
            }

            // Trigger part selection (reveals translucent ghost!)
            currentHeldPart.Select();

            // Highlight all other items belonging to this part's sequence group while held
            StartHeldGroupHighlight(currentHeldPart);
        }

        /// <summary>
        /// Snaps the currently held part directly to the ghost target socket.
        /// Supports interchangeable geometry sockets by accepting targetSocket and targetSnapPoint.
        /// </summary>
        public void SnapHeldPartToTarget(AssemblySocket targetSocket = null, Transform targetSnapPoint = null)
        {
            if (currentHeldPart == null) return;

            StopHeldGroupHighlight();

            interactionDebounceTimer = interactionDebounceDuration;
            requireMouseReleaseBeforeAction = true;

            AssemblyPart part = currentHeldPart;

            // Restore render queues
            RestoreHeldPartRenderQueues();

            // Restore physics collision detection
            if (heldRb != null)
            {
                heldRb.detectCollisions = true;
            }

            // Restore collisions
            if (heldColliders != null)
            {
                foreach (var col in heldColliders)
                {
                    if (col != null && characterController != null)
                    {
                        Physics.IgnoreCollision(characterController, col, false);
                    }
                }
            }

            // Perform snap to ghost position (using specific targeted socket if interchangeable)
            bool snapped;
            if (targetSocket != null || targetSnapPoint != null)
            {
                snapped = part.SnapToSocket(targetSocket, targetSnapPoint);
            }
            else
            {
                snapped = part.TrySnap();
            }

            if (snapped)
            {
                currentHeldPart = null;
                isTargetingGhost = false;
                heldRb = null;
                heldColliders = null;
            }
            else
            {
                // If snap rejected, never freeze in air: drop with gravity
                DropHeldPart();
            }
        }

        /// <summary>
        /// Drops the held part back into the world with physics.
        /// </summary>
        public void DropHeldPart()
        {
            if (currentHeldPart == null) return;

            StopHeldGroupHighlight();

            interactionDebounceTimer = interactionDebounceDuration;
            requireMouseReleaseBeforeAction = true;

            AssemblyPart part = currentHeldPart;
            currentHeldPart = null;
            isTargetingGhost = false;

            // Restore render queues
            RestoreHeldPartRenderQueues();

            // Restore physics collision detection
            if (heldRb != null)
            {
                heldRb.detectCollisions = true;
            }

            // Restore collisions
            if (heldColliders != null)
            {
                foreach (var col in heldColliders)
                {
                    if (col != null && characterController != null)
                    {
                        Physics.IgnoreCollision(characterController, col, false);
                    }
                }
            }

            // Trigger deselect (hides ghost)
            part.Deselect();

            // Restore gravity and physics if not snapped
            if (!part.IsSnapped && heldRb != null)
            {
                if (part.CanFloatInAir)
                {
                    // First part of sub-assembly or sub-assembly root floats in the air when detached
                    if (!heldRb.isKinematic)
                    {
                        heldRb.linearVelocity = Vector3.zero;
                        heldRb.angularVelocity = Vector3.zero;
                    }
                    heldRb.useGravity = false;
                    heldRb.isKinematic = true;
                }
                else
                {
                    // Secondary parts and bare parts follow gravity
                    heldRb.isKinematic = false;
                    heldRb.useGravity = true;
                }
            }

            heldRb = null;
            heldColliders = null;
        }

        private void RestoreHeldPartRenderQueues()
        {
            foreach (var kvp in heldPartRenderQueues)
            {
                if (kvp.Key != null && kvp.Value != null)
                {
                    Material[] mats = kvp.Key.materials;
                    for (int i = 0; i < mats.Length && i < kvp.Value.Length; i++)
                    {
                        if (mats[i] != null)
                        {
                            mats[i].renderQueue = kvp.Value[i];
                        }
                    }
                }
            }
            heldPartRenderQueues.Clear();
        }

        private void UpdateHeldPartPosition()
        {
            if (currentHeldPart == null || holdPoint == null || playerCamera == null) return;

            // Update hold point position dynamically for current part size and scroll distance
            holdPoint.localPosition = new Vector3(0f, currentHoldYOffset, currentHoldDistance);

            // Smoothly carry part at holdPoint position
            currentHeldPart.transform.position = Vector3.Lerp(
                currentHeldPart.transform.position,
                holdPoint.position,
                Time.deltaTime * carrySmoothSpeed
            );

            // Maintain rotation relative to hold point
            Quaternion targetRotation = holdPoint.rotation * heldRelativeRotation;
            currentHeldPart.transform.rotation = Quaternion.Slerp(
                currentHeldPart.transform.rotation,
                targetRotation,
                Time.deltaTime * carrySmoothSpeed
            );
        }

        private Bounds CalculatePartBounds(AssemblyPart part)
        {
            Bounds b = new Bounds(part.transform.position, Vector3.zero);
            Renderer[] rends = part.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            foreach (var r in rends)
            {
                if (r == null || r.gameObject.name.Contains("Ghost") || r.gameObject.name.Contains("SnapFlash")) continue;
                if (!hasBounds)
                {
                    b = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }
            return b;
        }

        #endregion

        #region Input Helpers (Unity 6 Input System)

        private Vector2 GetMovementInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                float x = 0f;
                float y = 0f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y -= 1f;
                return new Vector2(x, y).normalized;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
            }
            catch (System.InvalidOperationException) { }
#endif
            return Vector2.zero;
        }

        private Vector2 GetLookInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.delta.ReadValue() * 0.1f;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            }
            catch (System.InvalidOperationException) { }
#endif
            return Vector2.zero;
        }

        private bool IsSprinting()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.leftShiftKey.isPressed;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return Input.GetKey(KeyCode.LeftShift);
            }
            catch (System.InvalidOperationException) { }
#endif
            return false;
        }

        private bool IsJumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.spaceKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return Input.GetKeyDown(KeyCode.Space);
            }
            catch (System.InvalidOperationException) { }
#endif
            return false;
        }

        private bool IsInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.eKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return Input.GetKeyDown(KeyCode.E);
            }
            catch (System.InvalidOperationException) { }
#endif
            return false;
        }

        private bool IsLeftClickPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.leftButton.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return Input.GetMouseButtonDown(0);
            }
            catch (System.InvalidOperationException) { }
#endif
            return false;
        }

        private bool IsLeftClickHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.leftButton.isPressed;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return Input.GetMouseButton(0);
            }
            catch (System.InvalidOperationException) { }
#endif
            return false;
        }

        private float GetMouseScroll()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.scroll.ReadValue().y * 0.01f;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return Input.mouseScrollDelta.y;
            }
            catch (System.InvalidOperationException) { }
#endif
            return 0f;
        }

        private bool IsRightClickHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.rightButton.isPressed;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return Input.GetMouseButton(1);
            }
            catch (System.InvalidOperationException) { }
#endif
            return false;
        }

        private bool IsEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.escapeKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return Input.GetKeyDown(KeyCode.Escape);
            }
            catch (System.InvalidOperationException) { }
#endif
            return false;
        }

        private bool IsDropPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.qKey.wasPressedThisFrame || Keyboard.current.gKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.G);
            }
            catch (System.InvalidOperationException) { }
#endif
            return false;
        }

        private bool IsCrawlPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.cKey.wasPressedThisFrame || Keyboard.current.leftCtrlKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl);
            }
            catch (System.InvalidOperationException) { }
#endif
            return false;
        }

        private bool IsCrawlHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.cKey.isPressed || Keyboard.current.leftCtrlKey.isPressed;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl);
            }
            catch (System.InvalidOperationException) { }
#endif
            return false;
        }

        #endregion

        #region Crosshair & Game HUD

        private void OnGUI()
        {
            // Draw clickable Start/Stop Record button at the top-right
            DrawRecordModeButton();

            // Draw disassembly recording & workshop edit mode HUDs
            DrawRecordingHUD();

            // When cursor is unlocked, show reminder hint and allow clicking HUD buttons
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                DrawCursorFreeHint();
                return;
            }

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Color dotColor = crosshairNormalColor;

            string keyBadge = null;
            string actionTitle = null;
            string targetName = null;
            string detailText = null;
            Color hudAccentColor = crosshairNormalColor;

            if (currentHeldPart == null)
            {
                if (isHoveringPart && hoveredPart != null)
                {
                    if (hoveredPart.IsSnapped)
                    {
                        bool isFullyAssembledSub = hoveredPart.ParentSubAssembly != null && hoveredPart.ParentSubAssembly.IsFullyAssembled;
                        bool isShiftHeld = InputHelper.IsKeyHeld(KeyCode.LeftShift) || InputHelper.IsKeyHeld(KeyCode.RightShift);

                        bool canDisassemble = hoveredPart.CanDisassemble(out string blockingReason, out _);
                        if (!canDisassemble)
                        {
                            if (isFullyAssembledSub && !isShiftHeld)
                            {
                                dotColor = new Color(1.0f, 0.75f, 0.2f, 0.95f); // Amber gold
                                keyBadge = "HOLD SHIFT";
                                actionTitle = "SUB-ASSEMBLY ASSEMBLED";
                                targetName = hoveredPart.PartDisplayName;
                                detailText = $"Hold [Shift] to start disassembling '{hoveredPart.ParentSubAssembly.SubAssemblyName}'";
                                hudAccentColor = dotColor;
                            }
                            else
                            {
                                dotColor = new Color(1.0f, 0.35f, 0.2f, 0.95f); // Locked amber/red
                                keyBadge = "LOCKED";
                                actionTitle = "CANNOT DISASSEMBLE";
                                targetName = hoveredPart.PartDisplayName;
                                detailText = blockingReason;
                                hudAccentColor = dotColor;
                            }
                        }
                        else
                        {
                            dotColor = crosshairHoverColor;
                            if (isFullyAssembledSub && isShiftHeld)
                            {
                                keyBadge = isRecordingDisassembly ? "SHIFT + REC" : "SHIFT + CLICK";
                                actionTitle = isRecordingDisassembly ? $"RECORD INDEX #{recordingCurrentIndex}" : "START DISASSEMBLY";
                                targetName = hoveredPart.PartDisplayName;
                                detailText = $"Breaks open sub-assembly '{hoveredPart.ParentSubAssembly.SubAssemblyName}'";
                                hudAccentColor = new Color(0.3f, 0.9f, 0.6f);
                            }
                            else
                            {
                                keyBadge = isRecordingDisassembly ? "RECORD DISASSEMBLY" : "E / CLICK";
                                actionTitle = isRecordingDisassembly ? $"RECORD INDEX #{recordingCurrentIndex}" : "DISASSEMBLE";
                                targetName = hoveredPart.PartDisplayName;
                                detailText = isRecordingDisassembly ? $"Unsnaps and records sequence #{recordingCurrentIndex}" : "Unsnaps part and picks it up into hand";
                                hudAccentColor = dotColor;
                            }
                        }
                    }
                    else
                    {
                        dotColor = crosshairHoverColor;
                        keyBadge = "E / CLICK";
                        actionTitle = "PICK UP";
                        targetName = hoveredPart.PartDisplayName;
                        detailText = "Picks up part into hand";
                        hudAccentColor = dotColor;
                    }
                }
                else if (IsRightClickHeld())
                {
                    keyBadge = "RMB";
                    actionTitle = "ZOOM VIEW";
                    targetName = "INSPECTING";
                    detailText = "Release RMB to zoom out";
                    hudAccentColor = new Color(0.35f, 0.75f, 1.0f, 0.9f);
                }
                else if (isCrawling)
                {
                    keyBadge = "C / SPACE";
                    actionTitle = "CRAWLING";
                    targetName = "LOW STANCE";
                    detailText = "[C] or [Space] to Stand Up | [W,A,S,D] Crawl";
                    hudAccentColor = new Color(1.0f, 0.75f, 0.2f, 0.95f);
                }
            }
            else
            {
                if (isTargetingGhost)
                {
                    bool prereqsMet = currentHeldPart.ArePrerequisitesMet();
                    if (prereqsMet)
                    {
                        dotColor = crosshairHoverColor;
                        keyBadge = "CLICK / E";
                        actionTitle = "SNAP TO SOCKET";
                        targetName = currentHeldPart.PartDisplayName;
                        detailText = isCrawling
                            ? "[C] Stand Up | [RMB] Rotate | [Scroll] Distance | [Q] Drop"
                            : "[RMB] Rotate | [Scroll] Distance | [Q] Drop";
                        hudAccentColor = dotColor;
                    }
                    else
                    {
                        dotColor = new Color(1.0f, 0.35f, 0.2f, 0.9f); // Locked amber/red
                        keyBadge = "LOCKED";
                        actionTitle = "SEQUENCE BLOCKED";
                        targetName = currentHeldPart.PartDisplayName;
                        detailText = $"Assemble higher priority parts first (Order: {currentHeldPart.OrderIndex})";
                        hudAccentColor = dotColor;
                    }
                }
                else
                {
                    dotColor = crosshairNormalColor;
                    keyBadge = "CLICK / Q";
                    actionTitle = "PLACE ON WORKBENCH";
                    targetName = currentHeldPart.PartDisplayName;
                    detailText = isCrawling
                        ? "[C] Stand Up | [RMB] Hold to Rotate | [Scroll] Distance"
                        : "[RMB] Hold to Rotate | [Scroll] Adjust Distance";
                    hudAccentColor = new Color(0.35f, 0.75f, 1.0f, 0.9f);
                }
            }

            // Draw center dot only (clean, minimal crosshair with zero text clutter in center screen)
            if (showCrosshair)
            {
                float halfSize = crosshairSize * 0.5f;
                Rect rect = new Rect(center.x - halfSize, center.y - halfSize, crosshairSize, crosshairSize);

                Color prevColor = GUI.color;
                GUI.color = dotColor;
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = prevColor;
            }

            // Draw proper HUD action card at the bottom corner (like actual games)
            if (showHudActionCard && !string.IsNullOrEmpty(keyBadge))
            {
                DrawGameHudCard(keyBadge, actionTitle, targetName, detailText, hudAccentColor);
            }

        }

        private void DrawCursorFreeHint()
        {
            float w = 340f;
            float h = 26f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - 38f;

            Color prevColor = GUI.color;
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.85f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = prevColor;

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = new Color(0.75f, 0.9f, 1f) }
            };
            GUI.Label(new Rect(x, y, w, h), "MOUSE UNLOCKED — Click button or press [Alt] to look", style);
        }

        private void DrawGameHudCard(string keyBadge, string actionTitle, string targetName, string detailText, Color accentColor)
        {
            float cardWidth = 360f;
            float cardHeight = string.IsNullOrEmpty(detailText) ? 62f : 80f;
            float marginX = 35f;
            float marginY = 35f;

            float cardX = (hudCorner == HudCorner.BottomRight)
                ? Screen.width - cardWidth - marginX
                : marginX;
            float cardY = Screen.height - cardHeight - marginY;

            Rect cardRect = new Rect(cardX, cardY, cardWidth, cardHeight);

            // Semi-transparent dark background card
            Color prevGuiColor = GUI.color;
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.88f);
            GUI.DrawTexture(cardRect, Texture2D.whiteTexture);

            // Leading accent stripe (4px width)
            Rect accentRect = new Rect(cardX, cardY, 4f, cardHeight);
            GUI.color = accentColor;
            GUI.DrawTexture(accentRect, Texture2D.whiteTexture);
            GUI.color = prevGuiColor;

            float contentX = cardX + 16f;
            float contentWidth = cardWidth - 28f;

            // Key badge style
            GUIStyle badgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                normal = { textColor = accentColor }
            };
            string badgeLabel = $"[{keyBadge}]";
            float badgeWidth = badgeStyle.CalcSize(new GUIContent(badgeLabel)).x + 8f;
            GUI.Label(new Rect(contentX, cardY + 8f, badgeWidth, 20f), badgeLabel, badgeStyle);

            // Action title style
            GUIStyle actionStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(contentX + badgeWidth, cardY + 8f, contentWidth - badgeWidth, 20f), actionTitle, actionStyle);

            // Target part name style
            if (!string.IsNullOrEmpty(targetName))
            {
                GUIStyle targetStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Normal,
                    normal = { textColor = new Color(0.92f, 0.94f, 0.96f, 0.95f) }
                };
                GUI.Label(new Rect(contentX, cardY + 28f, contentWidth, 22f), targetName, targetStyle);
            }

            // Detail / sub-instructions style
            if (!string.IsNullOrEmpty(detailText))
            {
                GUIStyle detailStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Italic,
                    normal = { textColor = accentColor }
                };
                GUI.Label(new Rect(contentX, cardY + 52f, contentWidth, 20f), detailText, detailStyle);
            }
        }

        #endregion

        #region Guidance Hints

        private void HandleHints()
        {
            if (nextAssemblyHintKey != KeyCode.None && InputHelper.IsKeyDown(nextAssemblyHintKey))
            {
                TriggerNextAssemblyHint();
            }

            if (nextDisassemblyHintKey != KeyCode.None && InputHelper.IsKeyDown(nextDisassemblyHintKey))
            {
                TriggerNextDisassemblyHint();
            }
        }

        public void TriggerNextAssemblyHint()
        {
            AssemblyPart nextPart = null;
            if (AssemblyManager.Instance != null)
            {
                nextPart = AssemblyManager.Instance.GetNextEligibleAssemblyPart();
            }

            if (nextPart == null)
            {
                var all = AssemblyPart.AllParts;
                for (int i = 0; i < all.Count; i++)
                {
                    var p = all[i];
                    if (p != null && !p.IsSnapped && p.ArePrerequisitesMet())
                    {
                        nextPart = p;
                        break;
                    }
                }
            }

            if (nextPart != null)
            {
                nextPart.FlashAssemblyHint();

                var all = AssemblyPart.AllParts;
                int eligibleCount = 0;
                for (int i = 0; i < all.Count; i++)
                {
                    var p = all[i];
                    if (p != null && !p.IsSnapped && p.ArePrerequisitesMet())
                    {
                        eligibleCount++;
                    }
                }

                string extra = eligibleCount > 1 ? $" ({eligibleCount} parts eligible — any can be assembled!)" : "";
                string msg = $"[N] Next to Assemble: '{nextPart.PartDisplayName}' (Order #{nextPart.OrderIndex}){extra}";
                ShowSaveToastNotification(msg, 3.5f);
                Debug.Log($"<color=cyan>[Assembly Hint] {msg}</color>");
            }
            else
            {
                bool anySnapped = false;
                var all = AssemblyPart.AllParts;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] != null && all[i].IsSnapped)
                    {
                        anySnapped = true;
                        break;
                    }
                }

                string msg = anySnapped
                    ? "[N] All parts are assembled!"
                    : "[N] No parts available to assemble in scene.";
                ShowSaveToastNotification(msg, 2.5f);
                Debug.Log($"<color=green>[Assembly Hint] {msg}</color>");
            }
        }

        public void TriggerNextDisassemblyHint()
        {
            AssemblyPart nextPart = null;
            if (AssemblyManager.Instance != null)
            {
                nextPart = AssemblyManager.Instance.GetNextEligibleDisassemblyPart();
            }

            if (nextPart == null)
            {
                var all = AssemblyPart.AllParts;
                for (int i = all.Count - 1; i >= 0; i--)
                {
                    var p = all[i];
                    if (p != null && p.IsSnapped && p.CanDisassemble())
                    {
                        nextPart = p;
                        break;
                    }
                }
            }

            if (nextPart != null)
            {
                nextPart.FlashDisassemblyHint();

                var all = AssemblyPart.AllParts;
                int eligibleCount = 0;
                for (int i = 0; i < all.Count; i++)
                {
                    var p = all[i];
                    if (p != null && p.IsSnapped && p.CanDisassemble())
                    {
                        eligibleCount++;
                    }
                }

                string extra = eligibleCount > 1 ? $" ({eligibleCount} parts eligible — any can be removed!)" : "";
                string msg = $"[B] Next to Disassemble: '{nextPart.PartDisplayName}' (Order #{nextPart.OrderIndex}){extra}";
                ShowSaveToastNotification(msg, 3.5f);
                Debug.Log($"<color=orange>[Disassembly Hint] {msg}</color>");
            }
            else
            {
                bool anySnapped = false;
                var all = AssemblyPart.AllParts;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] != null && all[i].IsSnapped)
                    {
                        anySnapped = true;
                        break;
                    }
                }

                string msg = anySnapped
                    ? "[B] Cannot disassemble: parts locked or hold [Shift] to break sub-assembly!"
                    : "[B] No parts currently assembled.";
                ShowSaveToastNotification(msg, 2.5f);
                Debug.Log($"<color=yellow>[Disassembly Hint] {msg}</color>");
            }
        }

        #endregion

        #region Disassembly Recording Mode

        private void HandleRecordingMode()
        {
            // X: Toggle auto-advancing index on disassembly (always accessible!)
            if (InputHelper.IsKeyDown(KeyCode.X) || (toggleAutoAdvanceKey != KeyCode.None && InputHelper.IsKeyDown(toggleAutoAdvanceKey)))
            {
                autoAdvanceIndexOnDisassemble = !autoAdvanceIndexOnDisassemble;
                ShowSaveToastNotification($"⚡ Auto-Step (Auto-Advance Index): {(autoAdvanceIndexOnDisassemble ? "ON" : "OFF")}", 2.5f);
                Debug.Log($"<color=yellow>[Edit Mode] Auto-Advance Index (Auto-Step) toggled: {(autoAdvanceIndexOnDisassemble ? "ON" : "OFF")}</color>");
            }

            // Toggle Edit / Recording mode on F6 or configured key
            bool togglePressed = (recordingToggleKey != KeyCode.None && InputHelper.IsKeyDown(recordingToggleKey))
                              || InputHelper.IsKeyDown(KeyCode.F6);

            // Escape key will also cleanly exit edit mode if active
            bool exitPressed = isRecordingDisassembly && InputHelper.IsKeyDown(KeyCode.Escape);

            if (togglePressed || exitPressed)
            {
                if (!isRecordingDisassembly)
                {
                    StartRecordingMode();
                }
                else
                {
                    StopRecordingMode();
                }
            }

            // U: Permanent save to file & scene (only pressing U saves permanently!)
            if (permanentSaveKey != KeyCode.None && InputHelper.IsKeyDown(permanentSaveKey))
            {
                SavePermanent();
            }

            // T: Temporary save in memory (session snapshot, not written to disk)
            if (temporarySaveKey != KeyCode.None && InputHelper.IsKeyDown(temporarySaveKey))
            {
                SaveTemporary();
            }

            // Y: Instant assemble all parts in reverse order
            if (autoAssembleKey != KeyCode.None && InputHelper.IsKeyDown(autoAssembleKey))
            {
                AutoAssembleAllForRecording();
            }

            if (!isRecordingDisassembly) return;

            // J: decrement current index
            if (recordingIndexDownKey != KeyCode.None && InputHelper.IsKeyDown(recordingIndexDownKey))
            {
                AdjustRecordingIndex(-1);
                Debug.Log($"<color=yellow>[Edit Mode] Recording Index: #{recordingCurrentIndex} (Group: #{recordingCurrentGroup})</color>");
            }

            // L: increment current index
            if (recordingIndexUpKey != KeyCode.None && InputHelper.IsKeyDown(recordingIndexUpKey))
            {
                AdjustRecordingIndex(1);
                Debug.Log($"<color=yellow>[Edit Mode] Recording Index: #{recordingCurrentIndex} (Group: #{recordingCurrentGroup})</color>");
            }

            // I: increment group on current index (group of same parts)
            if (recordingGroupUpKey != KeyCode.None && InputHelper.IsKeyDown(recordingGroupUpKey))
            {
                AdjustRecordingGroup(1);
                Debug.Log($"<color=yellow>[Edit Mode] Recording Group on Index #{recordingCurrentIndex} increased to: #{recordingCurrentGroup}</color>");
            }

            // K: decrement group on current index (group of same parts)
            if (recordingGroupDownKey != KeyCode.None && InputHelper.IsKeyDown(recordingGroupDownKey))
            {
                AdjustRecordingGroup(-1);
                Debug.Log($"<color=yellow>[Edit Mode] Recording Group on Index #{recordingCurrentIndex} decreased to: #{recordingCurrentGroup}</color>");
            }

            // Z: Undo last disassembly record and auto re-assemble that part
            if (undoRecordKey != KeyCode.None && InputHelper.IsKeyDown(undoRecordKey))
            {
                UndoLastDisassemblyStep();
            }
        }

        /// <summary>
        /// Highlights all parts belonging to the currently selected edit mode index and group.
        /// </summary>
        public void HighlightCurrentEditModeGroup()
        {
            int matchCount = 0;
            var allParts = AssemblyPart.AllParts;
            Color groupHighlightColor = new Color(0.2f, 0.85f, 1f, 0.9f); // bright cyan

            for (int i = 0; i < allParts.Count; i++)
            {
                var part = allParts[i];
                if (part == null || !part.gameObject.activeInHierarchy) continue;

                if (part.OrderIndex == recordingCurrentIndex && part.GroupIndex == recordingCurrentGroup)
                {
                    part.FlashHint(groupHighlightColor, flashCount: 2, duration: 0.65f);
                    matchCount++;
                }
            }

            if (matchCount > 0)
            {
                Debug.Log($"<color=cyan>[Edit Mode] Highlighted {matchCount} object(s) in Index #{recordingCurrentIndex}, Group #{recordingCurrentGroup}</color>");
            }
        }

        #region Held Part Group Highlighting

        private Coroutine heldGroupHighlightCoroutine = null;
        private List<GameObject> activeGroupHighlightObjects = new List<GameObject>();

        private void StartHeldGroupHighlight(AssemblyPart heldPart)
        {
            StopHeldGroupHighlight();
            if (heldPart != null)
            {
                heldGroupHighlightCoroutine = StartCoroutine(HeldGroupHighlightRoutine(heldPart));
            }
        }

        private void StopHeldGroupHighlight()
        {
            if (heldGroupHighlightCoroutine != null)
            {
                StopCoroutine(heldGroupHighlightCoroutine);
                heldGroupHighlightCoroutine = null;
            }
            ClearHeldGroupHighlights();
        }

        private void ClearHeldGroupHighlights()
        {
            for (int i = 0; i < activeGroupHighlightObjects.Count; i++)
            {
                if (activeGroupHighlightObjects[i] != null)
                {
                    Destroy(activeGroupHighlightObjects[i]);
                }
            }
            activeGroupHighlightObjects.Clear();
        }

        private System.Collections.IEnumerator HeldGroupHighlightRoutine(AssemblyPart heldPart)
        {
            if (heldPart == null) yield break;

            // Find all other active parts in the exact same sequence group that are NOT yet placed/snapped in sockets
            List<AssemblyPart> groupParts = new List<AssemblyPart>();
            var allParts = AssemblyPart.AllParts;
            for (int i = 0; i < allParts.Count; i++)
            {
                var p = allParts[i];
                if (p != null && p != heldPart && p.gameObject.activeInHierarchy && !p.IsSnapped &&
                    heldPart.OrderIndex > 0 && heldPart.GroupIndex > 0 &&
                    p.OrderIndex == heldPart.OrderIndex && 
                    p.GroupIndex == heldPart.GroupIndex && 
                    p.ParentSubAssembly == heldPart.ParentSubAssembly)
                {
                    groupParts.Add(p);
                }
            }

            if (groupParts.Count == 0) yield break;

            // Create highlight overlay instances on each group part using top-most overlay shader
            Shader shader = Shader.Find("EngineAssembly/GhostHologramURP");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material hlMat = new Material(shader)
            {
                name = "HeldGroup_Highlight_Instance"
            };
            hlMat.SetFloat("_Surface", 1.0f);
            hlMat.SetFloat("_Blend", 0.0f);
            hlMat.SetInt("_ZWrite", 0);
            hlMat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            hlMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            hlMat.SetFloat("_CullMode", (float)UnityEngine.Rendering.CullMode.Off);
            hlMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay + 250;

            Color hlColor = new Color(0.2f, 0.95f, 0.45f, 0.85f); // vibrant emerald green
            if (hlMat.HasProperty("_RimColor")) hlMat.SetColor("_RimColor", hlColor * 1.5f);
            if (hlMat.HasProperty("_AlphaMultiplier")) hlMat.SetFloat("_AlphaMultiplier", 0.95f);
            if (hlMat.HasProperty("_RimPower")) hlMat.SetFloat("_RimPower", 1.8f);
            if (hlMat.HasProperty("_PulseSpeed")) hlMat.SetFloat("_PulseSpeed", 3.5f);
            if (hlMat.HasProperty("_PulseIntensity")) hlMat.SetFloat("_PulseIntensity", 0.35f);

            for (int i = 0; i < groupParts.Count; i++)
            {
                var p = groupParts[i];
                GameObject hlObj = new GameObject($"{p.gameObject.name}_GroupHighlight");
                p.CloneFlashMeshHierarchy(p.transform, hlObj.transform, hlMat, 0);
                hlObj.transform.SetParent(p.transform, false);
                hlObj.transform.localPosition = Vector3.zero;
                hlObj.transform.localRotation = Quaternion.identity;
                hlObj.transform.localScale = Vector3.one;
                activeGroupHighlightObjects.Add(hlObj);
            }

            // Pulse smoothly while this part remains held in player's hand
            while (currentHeldPart == heldPart)
            {
                float pulse = Mathf.PingPong(Time.time * 2.2f, 1f);
                float alpha = Mathf.Lerp(0.15f, 0.75f, pulse);
                Color c = new Color(hlColor.r, hlColor.g, hlColor.b, alpha);
                if (hlMat.HasProperty("_BaseColor")) hlMat.SetColor("_BaseColor", c);
                else hlMat.color = c;
                if (hlMat.HasProperty("_RimColor")) hlMat.SetColor("_RimColor", c * 1.5f);
                yield return null;
            }

            ClearHeldGroupHighlights();
            Destroy(hlMat);
        }

        #endregion

        #region Hover Removal Highlight (Flashing Fast on Click-Removable Parts)

        private void SetHoverHighlight(AssemblyPart part, bool canBeRemoved)
        {
            if (part == null || !canBeRemoved)
            {
                ClearHoverHighlight();
                return;
            }

            if (currentHoverHighlightedPart == part && currentHoverHighlightObj != null)
            {
                return; // Already actively highlighting this part
            }

            ClearHoverHighlight();

            currentHoverHighlightedPart = part;

            // Render with top-most overlay shader so glow is visible through engine geometry
            Shader shader = Shader.Find("EngineAssembly/GhostHologramURP");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            currentHoverHighlightMat = new Material(shader)
            {
                name = "HoverRemovalHighlight_Instance"
            };

            currentHoverHighlightMat.SetFloat("_Surface", 1.0f); // Transparent
            currentHoverHighlightMat.SetFloat("_Blend", 0.0f);   // Alpha blend
            currentHoverHighlightMat.SetInt("_ZWrite", 0);
            currentHoverHighlightMat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            currentHoverHighlightMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            currentHoverHighlightMat.SetFloat("_CullMode", (float)UnityEngine.Rendering.CullMode.Off);
            currentHoverHighlightMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay + 350;

            // Slow, low-intensity configuration: gentle GPU pulse + rim edge accent so part details remain visible
            currentHoverHighlightMat.SetFloat("_AlphaMultiplier", 0.5f);
            currentHoverHighlightMat.SetFloat("_RimPower", 3.0f); // Edge contour accent, keeping faces clear
            currentHoverHighlightMat.SetFloat("_PulseSpeed", hoverHighlightPulseSpeed); // Slow pulse
            currentHoverHighlightMat.SetFloat("_PulseIntensity", 0.2f); // Gentle variance

            Color initialColor = part.IsSnapped
                ? new Color(1.0f, 0.55f, 0.05f, hoverHighlightMinAlpha)   // Low-intensity amber-gold so part is visible
                : new Color(0.15f, 0.95f, 0.5f, hoverHighlightMinAlpha);   // Gentle emerald for loose parts on table

            Color initialRim = part.IsSnapped
                ? new Color(1.0f, 0.65f, 0.2f, hoverHighlightMinAlpha * 1.5f)
                : new Color(0.25f, 1.0f, 0.6f, hoverHighlightMinAlpha * 1.5f);

            if (currentHoverHighlightMat.HasProperty("_BaseColor")) currentHoverHighlightMat.SetColor("_BaseColor", initialColor);
            else currentHoverHighlightMat.color = initialColor;

            if (currentHoverHighlightMat.HasProperty("_RimColor")) currentHoverHighlightMat.SetColor("_RimColor", initialRim);

            currentHoverHighlightObj = new GameObject($"{part.gameObject.name}_HoverRemovalHighlight");
            part.CloneFlashMeshHierarchy(part.transform, currentHoverHighlightObj.transform, currentHoverHighlightMat, 0);

            currentHoverHighlightObj.transform.SetParent(part.transform, false);
            currentHoverHighlightObj.transform.localPosition = Vector3.zero;
            currentHoverHighlightObj.transform.localRotation = Quaternion.identity;
            currentHoverHighlightObj.transform.localScale = Vector3.one;
        }

        private void UpdateHoverHighlightAnimation()
        {
            if (currentHoverHighlightObj == null || currentHoverHighlightMat == null || currentHoverHighlightedPart == null)
                return;

            // Slow, smooth breathing pulse (~1 cycle every 1.5 - 2s)
            float t = Mathf.PingPong(Time.time * hoverHighlightPulseSpeed, 1f);
            float pulse = Mathf.SmoothStep(0f, 1f, t);

            if (currentHoverHighlightedPart.IsSnapped)
            {
                // Low intensity amber-orange so the part's model, textures, and details remain clearly visible
                float alpha = Mathf.Lerp(hoverHighlightMinAlpha, hoverHighlightMaxAlpha, pulse);
                Color c = new Color(1.0f, 0.55f, 0.05f, alpha);
                Color rim = new Color(1.0f, 0.65f, 0.2f, Mathf.Lerp(hoverHighlightMinAlpha * 1.5f, hoverHighlightMaxAlpha * 1.6f, pulse));

                if (currentHoverHighlightMat.HasProperty("_BaseColor"))
                    currentHoverHighlightMat.SetColor("_BaseColor", c);
                else
                    currentHoverHighlightMat.color = c;

                if (currentHoverHighlightMat.HasProperty("_RimColor"))
                    currentHoverHighlightMat.SetColor("_RimColor", rim);
            }
            else
            {
                // Gentle emerald for loose parts on workbench / floor
                float alpha = Mathf.Lerp(hoverHighlightMinAlpha, hoverHighlightMaxAlpha * 1.15f, pulse);
                Color c = new Color(0.15f, 0.95f, 0.5f, alpha);
                Color rim = new Color(0.25f, 1.0f, 0.6f, Mathf.Lerp(hoverHighlightMinAlpha * 1.5f, hoverHighlightMaxAlpha * 1.7f, pulse));

                if (currentHoverHighlightMat.HasProperty("_BaseColor"))
                    currentHoverHighlightMat.SetColor("_BaseColor", c);
                else
                    currentHoverHighlightMat.color = c;

                if (currentHoverHighlightMat.HasProperty("_RimColor"))
                    currentHoverHighlightMat.SetColor("_RimColor", rim);
            }
        }

        private void ClearHoverHighlight()
        {
            if (currentHoverHighlightObj != null)
            {
                Destroy(currentHoverHighlightObj);
                currentHoverHighlightObj = null;
            }
            if (currentHoverHighlightMat != null)
            {
                Destroy(currentHoverHighlightMat);
                currentHoverHighlightMat = null;
            }
            currentHoverHighlightedPart = null;
        }

        #endregion

        /// <summary>
        /// Undoes the last recorded disassembly step: removes the record, automatically re-assembles the part
        /// back into its socket, and restores the sequence index.
        /// </summary>
        public void UndoLastDisassemblyStep()
        {
            if (recordedDisassemblyStates == null || recordedDisassemblyStates.Count == 0)
            {
                Debug.LogWarning("[Edit Mode] No disassembly steps to undo.");
                ShowSaveToastNotification("No disassembly steps to undo");
                return;
            }

            // Pop the last step
            int lastIndex = recordedDisassemblyStates.Count - 1;
            var lastRecord = recordedDisassemblyStates[lastIndex];
            recordedDisassemblyStates.RemoveAt(lastIndex);

            // Also remove from recordingLog if present
            if (recordingLog != null && recordingLog.Count > 0)
            {
                for (int i = recordingLog.Count - 1; i >= 0; i--)
                {
                    if (recordingLog[i].part == lastRecord.part)
                    {
                        recordingLog.RemoveAt(i);
                        break;
                    }
                }
            }

            // Restore current index and group to what it was
            recordingCurrentIndex = lastRecord.assignedIndex;
            recordingCurrentGroup = lastRecord.groupIndex;

            // Automatically re-assemble the part back into its engine socket
            if (lastRecord.part != null)
            {
                // Reset undone part back to unrecorded status: index -1 and negative group ID
                lastRecord.part.OrderIndex = -1;
                lastRecord.part.LocalOrderIndex = -1;
                int undoneGrp = lastRecord.groupIndex != 0 ? lastRecord.groupIndex : 1;
                lastRecord.part.GroupIndex = -Mathf.Abs(undoneGrp);

                // If the player is currently holding this exact part, release it cleanly
                if (currentHeldPart == lastRecord.part)
                {
                    DropHeldPart();
                }

                // Snap the part back into place
                if (lastRecord.part.TargetSocket != null || lastRecord.part.TargetSnapPoint != null)
                {
                    lastRecord.part.SnapToSocket(lastRecord.part.TargetSocket, lastRecord.part.TargetSnapPoint);
                }
                else
                {
                    lastRecord.part.SnapDirectly(smooth: true, ignorePrerequisites: true);
                }

                // Flash cyan confirmation on re-assembly
                lastRecord.part.FlashHint(new Color(0.2f, 0.9f, 1f, 0.9f), 2, 0.5f);
            }

            HighlightCurrentEditModeGroup();

            ShowSaveToastNotification($"↩ Undid Step #{lastRecord.stepNumber}: '{lastRecord.partDisplayName}' Re-assembled!");
            Debug.Log($"<color=yellow>[Edit Mode] Undid Step #{lastRecord.stepNumber} ('{lastRecord.partDisplayName}'). Part re-assembled back to socket.</color>");
        }

        private Coroutine activeAutoAssemblyRoutine = null;
        private float autoAssemblyStartTime = -10f;
        private const float AUTO_ASSEMBLE_DEBOUNCE_SECONDS = 1.0f;
        private List<AssemblyPart> currentAutoAssemblyList = null;

        public bool IsAutoAssemblingInProgress => activeAutoAssemblyRoutine != null;

        /// <summary>
        /// Dual-Mode Auto-Assembly:
        /// 1. First press of [Y]: starts normal-fast speed auto-assembly in reverse order.
        /// 2. Second press of [Y] (after 1-second debounce): instantly completes the assembly.
        /// </summary>
        public void AutoAssembleAllForRecording()
        {
            // If assembly is already running, check if 1-second debounce has passed to trigger instant build
            if (activeAutoAssemblyRoutine != null)
            {
                float elapsed = Time.time - autoAssemblyStartTime;
                if (elapsed < AUTO_ASSEMBLE_DEBOUNCE_SECONDS)
                {
                    float remaining = AUTO_ASSEMBLE_DEBOUNCE_SECONDS - elapsed;
                    ShowSaveToastNotification($"⚡ Wait {remaining:F1}s for Instant Build...", 0.6f);
                    return;
                }

                // Stop step-by-step routine and immediately finish everything instantly!
                StopCoroutine(activeAutoAssemblyRoutine);
                activeAutoAssemblyRoutine = null;

                ExecuteInstantCompletionOfAssembly();
                return;
            }

            // Not yet assembling: start normal-fast speed auto-assembly
            StartNormalSpeedAutoAssembly();
        }

        private void StartNormalSpeedAutoAssembly()
        {
            // Drop any currently held part
            if (currentHeldPart != null)
            {
                DropHeldPart();
            }

            var allParts = AssemblyPart.AllParts;
            List<AssemblyPart> toAssemble = new List<AssemblyPart>();
            for (int i = 0; i < allParts.Count; i++)
            {
                var p = allParts[i];
                if (p != null && !p.IsSnapped)
                {
                    toAssemble.Add(p);
                }
            }

            if (toAssemble.Count == 0)
            {
                ShowSaveToastNotification("⚙ All parts are already assembled! Ready to record disassembly.", 2.5f);
                return;
            }

            // Create lookup of recorded disassembly order (if any)
            Dictionary<AssemblyPart, int> disassemblyStepLookup = new Dictionary<AssemblyPart, int>();
            for (int i = 0; i < recordedDisassemblyStates.Count; i++)
            {
                if (recordedDisassemblyStates[i].part != null && !disassemblyStepLookup.ContainsKey(recordedDisassemblyStates[i].part))
                {
                    disassemblyStepLookup[recordedDisassemblyStates[i].part] = recordedDisassemblyStates[i].stepNumber;
                }
            }

            // Sort in the exact OPPOSITE order of disassembly:
            // Parts disassembled LAST are assembled FIRST; parts disassembled FIRST are assembled LAST!
            toAssemble.Sort((a, b) =>
            {
                bool aInRecorded = disassemblyStepLookup.TryGetValue(a, out int aStep);
                bool bInRecorded = disassemblyStepLookup.TryGetValue(b, out int bStep);

                // If both are in recorded disassembly, sort in reverse step order (highest step number first)
                if (aInRecorded && bInRecorded)
                {
                    return bStep.CompareTo(aStep); // Reverse step order
                }
                if (aInRecorded && !bInRecorded) return -1;
                if (!aInRecorded && bInRecorded) return 1;

                // Sub-assembly base floating anchor should be positioned first
                bool aIsSubBase = a.ParentSubAssembly != null && (a.ParentSubAssembly.RootPart == a || a.ParentSubAssembly.GetFirstPart() == a);
                bool bIsSubBase = b.ParentSubAssembly != null && (b.ParentSubAssembly.RootPart == b || b.ParentSubAssembly.GetFirstPart() == b);
                if (aIsSubBase && !bIsSubBase) return -1;
                if (!aIsSubBase && bIsSubBase) return 1;

                // Within the same sub-assembly: higher local index first (reverse of disassembly)
                if (a.ParentSubAssembly != null && b.ParentSubAssembly != null && a.ParentSubAssembly == b.ParentSubAssembly)
                {
                    return b.LocalOrderIndex.CompareTo(a.LocalOrderIndex);
                }

                if (a.ParentSubAssembly != null && b.ParentSubAssembly == null) return -1;
                if (a.ParentSubAssembly == null && b.ParentSubAssembly != null) return 1;

                // Main assembly parts: higher order index first (reverse of disassembly)
                return b.AssemblyOrderIndex.CompareTo(a.AssemblyOrderIndex);
            });

            currentAutoAssemblyList = toAssemble;
            autoAssemblyStartTime = Time.time;
            activeAutoAssemblyRoutine = StartCoroutine(StepByStepAssembleRoutine(toAssemble));
        }

        private System.Collections.IEnumerator StepByStepAssembleRoutine(List<AssemblyPart> toAssemble)
        {
            int total = toAssemble.Count;
            ShowSaveToastNotification($"⚙ Auto-Assembling {total} parts... (Press [Y] again for Instant Build)", 2.0f);

            for (int i = 0; i < toAssemble.Count; i++)
            {
                var p = toAssemble[i];
                if (p == null || p.IsSnapped) continue;

                p.SnapDirectly(smooth: true, ignorePrerequisites: true);
                ShowSaveToastNotification($"⚙ Assembling ({i + 1}/{total}): '{p.PartDisplayName}' (Press [Y] for Instant)", stepByStepAssembleDelay + 0.15f);

                if (stepByStepAssembleDelay > 0f)
                {
                    yield return new WaitForSeconds(stepByStepAssembleDelay);
                }
                else
                {
                    yield return null;
                }
            }

            activeAutoAssemblyRoutine = null;
            currentAutoAssemblyList = null;

            FinishAssemblyResetState(total, isInstant: false);
        }

        private void ExecuteInstantCompletionOfAssembly()
        {
            int snappedCount = 0;
            if (currentAutoAssemblyList != null)
            {
                for (int i = 0; i < currentAutoAssemblyList.Count; i++)
                {
                    var p = currentAutoAssemblyList[i];
                    if (p != null && !p.IsSnapped)
                    {
                        p.SnapDirectly(smooth: false, ignorePrerequisites: true);
                        snappedCount++;
                    }
                }
            }
            else
            {
                var allParts = AssemblyPart.AllParts;
                for (int i = 0; i < allParts.Count; i++)
                {
                    var p = allParts[i];
                    if (p != null && !p.IsSnapped)
                    {
                        p.SnapDirectly(smooth: false, ignorePrerequisites: true);
                        snappedCount++;
                    }
                }
            }

            currentAutoAssemblyList = null;
            FinishAssemblyResetState(snappedCount, isInstant: true);
        }

        private void FinishAssemblyResetState(int count, bool isInstant)
        {
            // Clear existing recording states so recording starts fresh from step 1
            recordedDisassemblyStates.Clear();
            recordingLog.Clear();
            recordingCurrentIndex = 1;
            recordingCurrentGroup = 1;

            // Reset all parts to Index -1 and negative Group ID so they are ready for fresh recording
            var allPartsList = AssemblyPart.AllParts;
            for (int i = 0; i < allPartsList.Count; i++)
            {
                var p = allPartsList[i];
                if (p != null)
                {
                    p.OrderIndex = -1;
                    p.LocalOrderIndex = -1;
                    int curGrp = p.GroupIndex != 0 ? p.GroupIndex : 1;
                    p.GroupIndex = -Mathf.Abs(curGrp);
                }
            }

            HighlightCurrentEditModeGroup();

            string toastMsg = isInstant 
                ? $"⚡ Instant Build Complete! ({count} parts) Ready to record." 
                : $"✔ All {count} Parts Assembled in Reverse Order! Ready to record.";
            ShowSaveToastNotification(toastMsg, 3.0f);
            Debug.Log($"<color=green>[Edit Mode] {toastMsg} All parts initialized to Index -1 and negative group IDs.</color>");
        }

        public void StartRecordingMode(bool clearExisting = false)
        {
            isRecordingDisassembly = true;
            autoAdvanceIndexOnDisassemble = true;

            if (clearExisting || recordedDisassemblyStates.Count == 0)
            {
                recordingLog.Clear();
                recordedDisassemblyStates.Clear();
                recordingCurrentIndex = recordingStartIndex > 0 ? recordingStartIndex : 1;
                recordingCurrentGroup = recordingStartGroup > 0 ? recordingStartGroup : 1;
            }
            else
            {
                recordingCurrentIndex = Mathf.Max(recordingCurrentIndex, recordedDisassemblyStates.Count + 1);
            }

            // Set all unrecorded parts to Index -1 and negative Group ID so they will not interfere with positive recorded IDs
            HashSet<AssemblyPart> recordedParts = new HashSet<AssemblyPart>();
            if (!clearExisting)
            {
                for (int i = 0; i < recordedDisassemblyStates.Count; i++)
                {
                    if (recordedDisassemblyStates[i].part != null)
                    {
                        recordedParts.Add(recordedDisassemblyStates[i].part);
                    }
                }
            }

            var allParts = AssemblyPart.AllParts;
            for (int i = 0; i < allParts.Count; i++)
            {
                var p = allParts[i];
                if (p == null) continue;

                if (clearExisting || !recordedParts.Contains(p))
                {
                    p.OrderIndex = -1;
                    p.LocalOrderIndex = -1;
                    int curGrp = p.GroupIndex != 0 ? p.GroupIndex : 1;
                    p.GroupIndex = -Mathf.Abs(curGrp);
                }
            }

            HighlightCurrentEditModeGroup();

            Debug.Log($"<color=cyan>[Edit Mode]</color> <color=red>WORKSHOP EDIT & DISASSEMBLY RECORDING MODE STARTED.</color> Starting index: #{recordingCurrentIndex} (Group: #{recordingCurrentGroup}). All unrecorded parts initialized to Index -1 and negative Group ID.");
        }

        public void StopRecordingMode()
        {
            isRecordingDisassembly = false;

            // Print summary
            Debug.Log("<color=green>[Edit Mode] WORKSHOP EDIT MODE EXITED. Summary of recorded disassembly states:</color>");
            for (int i = 0; i < recordedDisassemblyStates.Count; i++)
            {
                var rec = recordedDisassemblyStates[i];
                Debug.Log($"  [{rec.stepNumber}] {rec.stateDescription}");
            }

            ShowSaveToastNotification($"■ Recording Stopped ({recordedDisassemblyStates.Count} steps). Press [{permanentSaveKey}] to save permanently, [{temporarySaveKey}] to save temporary.", 3.0f);
            Debug.Log($"<color=yellow>[Edit Mode] Recording stopped (Not saved to disk). Press [{permanentSaveKey}] to save permanently, [{temporarySaveKey}] to save temporary.</color>");
        }

        /// <summary>
        /// Saves the recorded disassembly sequence permanently to Assets/RecordedDisassemblySequence.json
        /// and captures current workbench positions/indices for the scene.
        /// </summary>
        public void SavePermanent()
        {
            SaveRecordedDisassemblyNow();
            ShowSaveToastNotification($"✔ [{permanentSaveKey}] Disassembly Permanently Saved to Scene & JSON!", 3.0f);
            Debug.Log($"<color=green>[PlayerAssemblyController] Permanent save complete: {recordedDisassemblyStates.Count} steps written to JSON and scene layout.</color>");
        }

        /// <summary>
        /// Saves the current recorded disassembly sequence temporarily in memory as a session snapshot.
        /// Does NOT write to disk or modify the scene file.
        /// </summary>
        public void SaveTemporary()
        {
            temporarySavedStates.Clear();
            if (recordedDisassemblyStates != null)
            {
                temporarySavedStates.AddRange(recordedDisassemblyStates);
            }
            ShowSaveToastNotification($"⚡ [{temporarySaveKey}] TEMPORARY SAVE: {temporarySavedStates.Count} step(s) cached in memory (Not written to disk)", 2.5f);
            Debug.Log($"<color=cyan>[PlayerAssemblyController] Temporary save: {temporarySavedStates.Count} steps cached in memory snapshot.</color>");
        }

        /// <summary>
        /// Saves the recorded disassembly sequence permanently to Assets/RecordedDisassemblySequence.json
        /// and captures current workbench positions/indices for the scene.
        /// </summary>
        public void SaveRecordedDisassemblyNow()
        {
            SaveDisassemblySequenceToFile(recordedDisassemblyStates);

            OnSaveLayoutRequested?.Invoke();
            savedNotificationText = "✔ Disassembly Saved to Scene & JSON!";
            savedNotificationTimer = 2.5f;
        }

        public static void SaveDisassemblySequenceToFile(List<DisassemblyRecordInfo> steps)
        {
            try
            {
                DisassemblySequenceSaveFile saveFile = new DisassemblySequenceSaveFile
                {
                    savedTimestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    totalSteps = steps != null ? steps.Count : 0,
                    recordedSteps = steps ?? new List<DisassemblyRecordInfo>()
                };

                string json = JsonUtility.ToJson(saveFile, true);
                string filePath = System.IO.Path.Combine(Application.dataPath, "RecordedDisassemblySequence.json");
                System.IO.File.WriteAllText(filePath, json);

#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif
                Debug.Log($"<color=green>[PlayerAssemblyController] Saved {saveFile.totalSteps} recorded disassembly steps to: {filePath}</color>");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PlayerAssemblyController] Failed to save disassembly sequence: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when a part is unsnapped while recording mode is active.
        /// Assigns the current recording index and group to the part, captures disassembly state as info, and advances if configured.
        /// </summary>
        public void RecordDisassembly(AssemblyPart part)
        {
            if (part == null || !isRecordingDisassembly) return;

            int step = recordedDisassemblyStates.Count + 1;
            bool isSub = part.ParentSubAssembly != null;
            string subName = isSub ? part.ParentSubAssembly.SubAssemblyName : null;
            int assignedIndex = recordingCurrentIndex;
            int assignedGroup = recordingCurrentGroup;

            part.GroupIndex = assignedGroup;

            if (isSub)
            {
                part.LocalOrderIndex = assignedIndex;
            }
            else
            {
                part.OrderIndex = assignedIndex;
            }

            string scope = isSub ? $"Sub-Assembly '{subName}'" : "Main Assembly";
            string stateDesc = $"Step #{step}: Disassembled '{part.PartDisplayName}' from {scope} => Assembly Index #{assignedIndex} (Group #{assignedGroup})";

            var recordInfo = new DisassemblyRecordInfo
            {
                part = part,
                partDisplayName = part.PartDisplayName,
                partId = part.PartId,
                assignedIndex = assignedIndex,
                groupIndex = assignedGroup,
                stepNumber = step,
                isSubAssembly = isSub,
                subAssemblyName = subName,
                worldPosition = part.transform.position,
                worldRotation = part.transform.rotation,
                timestamp = Time.time,
                stateDescription = stateDesc
            };

            recordedDisassemblyStates.Add(recordInfo);
            recordingLog.Add((part, assignedIndex));

            if (autoAdvanceIndexOnDisassemble)
            {
                recordingCurrentIndex++;
                recordingCurrentGroup = 1;
                ShowSaveToastNotification($"Step #{step}: '{part.PartDisplayName}' => Index #{assignedIndex} (Next: #{recordingCurrentIndex})", 2.5f);
            }
            else
            {
                // Auto-Step is OFF: auto-increment group count so subsequent parts at this index get different groups by default.
                // To place another part in the same group, press K to decrement the group count back.
                recordingCurrentGroup++;
                ShowSaveToastNotification($"Step #{step}: '{part.PartDisplayName}' => Index #{assignedIndex} (Group #{assignedGroup}) [Next Grp: #{recordingCurrentGroup} | Press K for Grp #{assignedGroup}]", 3.0f);
            }

            // Disassembly recorded in memory. Only pressing [U] saves permanently or [T] saves temporary snapshot.
            Debug.Log($"<color=orange>[Record Mode] {stateDesc}</color>");
        }

        private void DrawRecordModeButton()
        {
            // Top-Right Corner HUD contains the [⚙ AUTO ASSEMBLE (Y)] / [⚡ INSTANT BUILD (Y)] button
            float btnWidth = 175f;
            float btnHeight = 36f;
            float margin = 20f;
            float x = Screen.width - margin - btnWidth;
            float y = margin;

            bool isBuilding = activeAutoAssemblyRoutine != null;
            bool canInstant = isBuilding && (Time.time - autoAssemblyStartTime >= AUTO_ASSEMBLE_DEBOUNCE_SECONDS);

            Color prevBg = GUI.backgroundColor;
            Color prevContent = GUI.contentColor;

            string btnText;
            string tooltip;

            if (!isBuilding)
            {
                GUI.backgroundColor = new Color(0.15f, 0.45f, 0.85f, 0.92f); // Blue
                btnText = "⚙ AUTO ASSEMBLE (Y)";
                tooltip = "Start normal-fast speed auto assembly in reverse order (Y)";
            }
            else if (canInstant)
            {
                GUI.backgroundColor = new Color(1f, 0.55f, 0.08f, 0.98f); // Vibrant orange
                btnText = "⚡ INSTANT BUILD (Y)";
                tooltip = "Click or press [Y] to instantly finish building the entire engine!";
            }
            else
            {
                GUI.backgroundColor = new Color(0.55f, 0.45f, 0.15f, 0.92f); // Amber waiting debounce
                btnText = "⚙ ASSEMBLING...";
                tooltip = "Building in progress... (Instant build ready in 1s)";
            }

            GUI.contentColor = Color.white;

            if (GUI.Button(new Rect(x, y, btnWidth, btnHeight), new GUIContent(btnText, tooltip)))
            {
                AutoAssembleAllForRecording();
            }

            GUI.backgroundColor = prevBg;
            GUI.contentColor = prevContent;

            // Render notification toast if active
            if (savedNotificationTimer > 0f)
            {
                float toastW = 460f;
                float toastH = 28f;
                float toastX = Screen.width - toastW - margin;
                float toastY = margin + btnHeight + 6f;

                Color prevGui = GUI.color;
                GUI.color = new Color(0.1f, 0.45f, 0.25f, 0.95f);
                GUI.DrawTexture(new Rect(toastX, toastY, toastW, toastH), Texture2D.whiteTexture);
                GUI.color = prevGui;

                GUIStyle toastStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
                string toastMsg = string.IsNullOrEmpty(savedNotificationText) ? "✔ Disassembly Saved!" : savedNotificationText;
                GUI.Label(new Rect(toastX, toastY, toastW, toastH), toastMsg, toastStyle);
            }
        }

        private void DrawRecordingHUD()
        {
            if (!isRecordingDisassembly) return;

            DrawEditModeTopBanner();
            DrawEditModeControlsPanel();
        }

        private void DrawEditModeTopBanner()
        {
            float bannerWidth = 880f;
            float bannerHeight = 72f;
            float bannerX = (Screen.width - bannerWidth) * 0.5f;
            float bannerY = 14f;

            Color prevColor = GUI.color;
            Color prevBg = GUI.backgroundColor;
            Color prevContent = GUI.contentColor;

            GUI.color = new Color(0.08f, 0.10f, 0.16f, 0.94f);
            GUI.DrawTexture(new Rect(bannerX, bannerY, bannerWidth, bannerHeight), Texture2D.whiteTexture);

            // Red recording status bar at top of banner
            GUI.color = new Color(0.95f, 0.22f, 0.22f, 1f);
            GUI.DrawTexture(new Rect(bannerX, bannerY, bannerWidth, 4f), Texture2D.whiteTexture);
            GUI.color = prevColor;

            // Row 1: Title and Status Info
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                normal = { textColor = new Color(1f, 0.4f, 0.4f) }
            };
            GUI.Label(new Rect(bannerX + 16f, bannerY + 6f, 370f, 20f), "● RECORDING MODE ACTIVE (Press [Esc/Alt] to free mouse)", titleStyle);

            string lastInfo = recordedDisassemblyStates.Count > 0
                ? $"Last: <b><color=#5cf>{recordedDisassemblyStates[recordedDisassemblyStates.Count - 1].partDisplayName}</color></b> (Order #{recordedDisassemblyStates[recordedDisassemblyStates.Count - 1].assignedIndex}, Grp #{recordedDisassemblyStates[recordedDisassemblyStates.Count - 1].groupIndex})"
                : "Disassemble any part to begin recording";
            GUIStyle rightInfoStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 11,
                normal = { textColor = new Color(0.85f, 0.9f, 0.95f) }
            };
            GUI.Label(new Rect(bannerX + bannerWidth - 480f, bannerY + 6f, 464f, 20f), lastInfo, rightInfoStyle);

            // Row 2: Interactive Stepper Buttons & Controls
            float curX = bannerX + 16f;
            float row2Y = bannerY + 32f;
            float btnH = 26f;

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                fontSize = 11,
                normal = { textColor = new Color(0.9f, 0.92f, 0.95f) }
            };
            GUIStyle badgeStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                normal = { textColor = new Color(1f, 0.85f, 0.3f) }
            };

            // --- Index Stepper ---
            GUI.Label(new Rect(curX, row2Y, 44f, btnH), "Index:", labelStyle);
            curX += 46f;

            GUI.backgroundColor = new Color(0.2f, 0.25f, 0.35f, 1f);
            if (GUI.Button(new Rect(curX, row2Y, 52f, btnH), "◀ J"))
            {
                AdjustRecordingIndex(-1);
            }
            curX += 54f;

            GUI.Box(new Rect(curX, row2Y, 36f, btnH), $"#{recordingCurrentIndex}", badgeStyle);
            curX += 38f;

            if (GUI.Button(new Rect(curX, row2Y, 52f, btnH), "L ▶"))
            {
                AdjustRecordingIndex(1);
            }
            curX += 60f;

            // --- Group Stepper ---
            GUI.Label(new Rect(curX, row2Y, 48f, btnH), "Group:", labelStyle);
            curX += 50f;

            if (GUI.Button(new Rect(curX, row2Y, 52f, btnH), "◀ K"))
            {
                AdjustRecordingGroup(-1);
            }
            curX += 54f;

            badgeStyle.normal.textColor = new Color(0.35f, 0.85f, 1f);
            GUI.Box(new Rect(curX, row2Y, 36f, btnH), $"#{recordingCurrentGroup}", badgeStyle);
            curX += 38f;

            if (GUI.Button(new Rect(curX, row2Y, 52f, btnH), "I ▶"))
            {
                AdjustRecordingGroup(1);
            }
            curX += 60f;

            // --- Auto-Step Toggle Button ---
            GUI.backgroundColor = autoAdvanceIndexOnDisassemble ? new Color(0.15f, 0.55f, 0.35f, 1f) : new Color(0.35f, 0.35f, 0.38f, 1f);
            string autoStepLabel = autoAdvanceIndexOnDisassemble ? "⚡ Auto-Step: ON (X)" : "⚡ Auto-Step: OFF (X)";
            if (GUI.Button(new Rect(curX, row2Y, 130f, btnH), autoStepLabel))
            {
                autoAdvanceIndexOnDisassemble = !autoAdvanceIndexOnDisassemble;
                ShowSaveToastNotification($"Auto-Advance Index is now {(autoAdvanceIndexOnDisassemble ? "ENABLED" : "DISABLED")}", 2.0f);
            }
            curX += 136f;

            // --- Undo Button ---
            GUI.backgroundColor = new Color(0.5f, 0.25f, 0.25f, 1f);
            if (GUI.Button(new Rect(curX, row2Y, 76f, btnH), "↺ Undo (Z)"))
            {
                UndoLastDisassemblyStep();
            }
            curX += 82f;

            // --- Temp Save Button ---
            GUI.backgroundColor = new Color(0.2f, 0.45f, 0.65f, 1f);
            if (GUI.Button(new Rect(curX, row2Y, 86f, btnH), $"⚡ Temp ({temporarySaveKey})"))
            {
                SaveTemporary();
            }
            curX += 92f;

            // --- Perm Save Button ---
            GUI.backgroundColor = new Color(0.2f, 0.65f, 0.3f, 1f);
            if (GUI.Button(new Rect(curX, row2Y, 86f, btnH), $"💾 Save ({permanentSaveKey})"))
            {
                SavePermanent();
            }

            GUI.backgroundColor = prevBg;
            GUI.contentColor = prevContent;
        }

        private void DrawEditModeControlsPanel()
        {
            float panelWidth = 370f;
            float panelHeight = recordedDisassemblyStates.Count > 0 ? 345f : 305f;
            float panelX = 25f;
            float panelY = Screen.height - panelHeight - 25f;

            Rect panelRect = new Rect(panelX, panelY, panelWidth, panelHeight);

            // Dark background
            Color prevColor = GUI.color;
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.92f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);

            // Left vertical accent stripe
            Rect accentRect = new Rect(panelX, panelY, 4f, panelHeight);
            GUI.color = new Color(0.25f, 0.8f, 1f, 1f);
            GUI.DrawTexture(accentRect, Texture2D.whiteTexture);
            GUI.color = prevColor;

            float textX = panelX + 14f;
            float textWidth = panelWidth - 24f;

            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                normal = { textColor = new Color(0.3f, 0.85f, 1f) }
            };
            GUI.Label(new Rect(textX, panelY + 8f, textWidth, 20f), "WORKSHOP RECORDING CONTROLS", headerStyle);

            GUIStyle itemKeyStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 11,
                normal = { textColor = new Color(1f, 0.82f, 0.35f) }
            };
            GUIStyle itemDescStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.85f, 0.88f, 0.92f) }
            };

            string[] keys = new string[]
            {
                "[F6] / [Esc]",
                "[X]",
                "[Z]",
                "[Y]",
                "[U]",
                "[T]",
                "[N] / [B]",
                "[LMB] / [E]",
                "[J] / [L]",
                "[K] / [I]",
                "[Q] / [RMB]"
            };

            string[] descs = new string[]
            {
                "Exit Edit Mode (Does NOT save)",
                $"Auto-Advance Index (Currently {(autoAdvanceIndexOnDisassemble ? "ON" : "OFF")})",
                "Undo last disassembly (auto re-assembles)",
                "Auto assemble entire engine step-by-step",
                "Permanent Save (JSON & Scene)",
                "Temporary Save (In-memory snapshot)",
                "Next Assembly [N] / Disassembly [B] Hint",
                "Disassemble & Record into group/index",
                $"Change Index: J=Prev, L=Next (#{recordingCurrentIndex})",
                $"Change Group: K=Decr, I=Incr (#{recordingCurrentGroup})",
                "Drop / Rotate Part on table"
            };

            float lineY = panelY + 28f;
            for (int i = 0; i < keys.Length; i++)
            {
                GUI.Label(new Rect(textX, lineY, 95f, 18f), keys[i], itemKeyStyle);
                GUI.Label(new Rect(textX + 98f, lineY, textWidth - 98f, 18f), descs[i], itemDescStyle);
                lineY += 20f;
            }

            // If any parts have been disassembled, show recent sequence
            if (recordedDisassemblyStates.Count > 0)
            {
                lineY += 4f;
                GUIStyle historyHeader = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 11,
                    normal = { textColor = new Color(0.5f, 0.9f, 0.6f) }
                };
                GUI.Label(new Rect(textX, lineY, textWidth, 18f), $"Disassembly State Log ({recordedDisassemblyStates.Count} parts):", historyHeader);
                lineY += 18f;

                int startIdx = Mathf.Max(0, recordedDisassemblyStates.Count - 2);
                for (int i = startIdx; i < recordedDisassemblyStates.Count; i++)
                {
                    var rec = recordedDisassemblyStates[i];
                    GUIStyle recItemStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 10,
                        normal = { textColor = new Color(0.8f, 0.85f, 0.9f) }
                    };
                    GUI.Label(new Rect(textX, lineY, textWidth, 16f), $"• Step {rec.stepNumber}: Idx #{rec.assignedIndex} (Grp #{rec.groupIndex}) {rec.partDisplayName}", recItemStyle);
                    lineY += 16f;
                }
            }
        }

        #endregion
    }
}
