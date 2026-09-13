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

        [Tooltip("Key to permanently save the recorded disassembly sequence and scene layout (default U).")]
        [SerializeField] private KeyCode permanentSaveKey = KeyCode.U;

        [Tooltip("Key to undo the last recorded disassembly step and auto re-assemble the part (default Z).")]
        [SerializeField] private KeyCode undoRecordKey = KeyCode.Z;

        [Tooltip("Key to auto-assemble all parts onto the engine for disassembly recording (default Y).")]
        [SerializeField] private KeyCode autoAssembleKey = KeyCode.Y;

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

        // Dynamic hold distance and debounce timers
        private float currentHoldDistance = 1.0f;
        private float currentHoldYOffset = -0.2f;
        private float interactionDebounceTimer = 0f;
        private bool requireMouseReleaseBeforeAction = false;

        // Disassembly Recording Mode
        private bool isRecordingDisassembly = false;
        private int recordingCurrentIndex = 1;
        private int recordingCurrentGroup = 1;
        private bool userHasExplicitlyChangedGroup = false;
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
            if (delta > 0)
            {
                userHasExplicitlyChangedGroup = false;
            }
            HighlightCurrentEditModeGroup();
        }

        public void AdjustRecordingGroup(int delta)
        {
            recordingCurrentGroup = Mathf.Max(1, recordingCurrentGroup + delta);
            userHasExplicitlyChangedGroup = true;
            HighlightCurrentEditModeGroup();
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
            HandleHints();
            HandleRecordingMode();

            // Permanent Save via key (default U)
            if (permanentSaveKey != KeyCode.None && InputHelper.IsKeyDown(permanentSaveKey))
            {
                SaveRecordedDisassemblyNow();
                Debug.Log("<color=green>[Save] Permanent save triggered via [U] key. Sequence saved to JSON and scene layout captured.</color>");
            }

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
                // Do not re-lock cursor if clicking near the record button at the top-right
                Vector2 mousePos = InputHelper.GetMousePosition();
                bool isOverTopButton = (mousePos.y >= Screen.height - 65f && mousePos.x >= Screen.width - 240f);
                if (!isOverTopButton)
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

                        // Pick up on 'E' key or Left Mouse Click (with debounce protection against rapid multi-clicks)
                        if (CanTriggerInteractAction() && (IsInteractPressed() || (Cursor.lockState == CursorLockMode.Locked && IsLeftClickPressed())))
                        {
                            if (part.IsSnapped && !part.CanDisassemble(out string reason, out _))
                            {
                                Debug.LogWarning($"[PlayerAssemblyController] Cannot disassemble '{part.PartDisplayName}': {reason}");
                                interactionDebounceTimer = 0.25f;
                                requireMouseReleaseBeforeAction = true;
                                return;
                            }

                            PickUp(part);
                            return;
                        }
                    }
                }
            }
            else
            {
                hoveredPart = null;

                // Player is holding a part in hand
                bool isCrosshairOverGhost = false;
                AssemblySocket targetedGhostSocket = null;
                Transform targetedGhostSnapPoint = null;

                // Update ghost visibility and position from player camera view
                currentHeldPart.CheckGhostVisibility(playerCamera.transform.position);

                Ray camRay = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

                // Line-of-sight test: ghosts have NO colliders (preventing physics clipping, snagging, or glitches).
                // We mathematically test whether the camera crosshair ray directly intersects any active ghost's renderer bounding volume.
                if (currentHeldPart.ActiveGhostInstances != null)
                {
                    float closestGhostDist = float.MaxValue;
                    for (int i = 0; i < currentHeldPart.ActiveGhostInstances.Count; i++)
                    {
                        GameObject gObj = currentHeldPart.ActiveGhostInstances[i];
                        if (gObj == null || !gObj.activeInHierarchy) continue;

                        Renderer[] rends = gObj.GetComponentsInChildren<Renderer>(true);
                        for (int r = 0; r < rends.Length; r++)
                        {
                            Renderer rend = rends[r];
                            if (rend == null || !rend.enabled) continue;

                            if (rend.bounds.IntersectRay(camRay, out float hitDist))
                            {
                                if (hitDist <= snapReachDistance && hitDist < closestGhostDist)
                                {
                                    closestGhostDist = hitDist;
                                    GhostPreviewTarget targetComp = gObj.GetComponent<GhostPreviewTarget>();
                                    if (targetComp != null && targetComp.OwnerPart == currentHeldPart)
                                    {
                                        isCrosshairOverGhost = true;
                                        targetedGhostSocket = targetComp.TargetSocket;
                                        targetedGhostSnapPoint = targetComp.TargetSnapPoint != null ? targetComp.TargetSnapPoint : targetComp.transform;
                                    }
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

                // Clicking to snap or drop: ONLY snap if the crosshair is directly over the ghost!
                // Debounce ensures holding mouse down does not cause parts to drop or oscillate!
                if (CanTriggerInteractAction() && (IsInteractPressed() || IsLeftClickPressed()))
                {
                    if (isTargetingGhost)
                    {
                        // Crosshair IS over the ghost -> Snap to position!
                        SnapHeldPartToTarget(targetedGhostSocket, targetedGhostSnapPoint);
                    }
                    else
                    {
                        // Crosshair is NOT over the ghost -> Place on workbench / floor!
                        DropHeldPart();
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
                    mats[i].renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 200;
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
            // Toggle Edit / Recording mode on F6 or configured key (P also accepted as legacy shortcut)
            bool togglePressed = (recordingToggleKey != KeyCode.None && InputHelper.IsKeyDown(recordingToggleKey))
                              || InputHelper.IsKeyDown(KeyCode.F6);

            if (togglePressed)
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

            // Y: Auto assemble all parts for recording
            if (autoAssembleKey != KeyCode.None && InputHelper.IsKeyDown(autoAssembleKey))
            {
                AutoAssembleAllForRecording();
            }

            // X: Toggle auto-advancing index on disassembly
            if (toggleAutoAdvanceKey != KeyCode.None && InputHelper.IsKeyDown(toggleAutoAdvanceKey))
            {
                autoAdvanceIndexOnDisassemble = !autoAdvanceIndexOnDisassemble;
                ShowSaveToastNotification($"⚡ Auto-Advance Index: {(autoAdvanceIndexOnDisassemble ? "ON" : "OFF")}");
                Debug.Log($"<color=yellow>[Edit Mode] Auto-Advance Index toggled: {(autoAdvanceIndexOnDisassemble ? "ON" : "OFF")}</color>");
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
                    p.OrderIndex == heldPart.OrderIndex && 
                    p.GroupIndex == heldPart.GroupIndex && 
                    p.ParentSubAssembly == heldPart.ParentSubAssembly)
                {
                    groupParts.Add(p);
                }
            }

            if (groupParts.Count == 0) yield break;

            // Create highlight overlay instances on each group part
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");

            Material hlMat = new Material(unlitShader)
            {
                name = "HeldGroup_Highlight_Instance"
            };
            hlMat.SetFloat("_Surface", 1.0f);
            hlMat.SetFloat("_Blend", 0.0f);
            hlMat.SetInt("_ZWrite", 0);
            hlMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 210;

            Color hlColor = new Color(0.2f, 0.95f, 0.45f, 0.85f); // vibrant emerald green

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
                yield return null;
            }

            ClearHeldGroupHighlights();
            Destroy(hlMat);
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

            // Save updated sequence and refresh layout
            SaveRecordedDisassemblyNow();
            HighlightCurrentEditModeGroup();

            ShowSaveToastNotification($"↩ Undid Step #{lastRecord.stepNumber}: '{lastRecord.partDisplayName}' Re-assembled!");
            Debug.Log($"<color=yellow>[Edit Mode] Undid Step #{lastRecord.stepNumber} ('{lastRecord.partDisplayName}'). Part re-assembled back to socket.</color>");
        }

        /// <summary>
        /// Automatically assembles all parts onto the engine so the player can cleanly record the disassembly order from scratch.
        /// </summary>
        public void AutoAssembleAllForRecording()
        {
            // Drop any currently held part
            if (currentHeldPart != null)
            {
                DropHeldPart();
            }

            // Clear existing recording states
            recordedDisassemblyStates.Clear();
            recordingLog.Clear();
            recordingCurrentIndex = 1;
            recordingCurrentGroup = 1;
            userHasExplicitlyChangedGroup = false;

            // Trigger full auto-assembly
            if (AutoAssemblyController.Instance != null)
            {
                AutoAssemblyController.Instance.AssembleWhole();
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
                    }
                }
            }

            SaveRecordedDisassemblyNow();
            HighlightCurrentEditModeGroup();

            ShowSaveToastNotification("⚙ All Parts Auto-Assembled! Ready to record disassembly.");
            Debug.Log("<color=green>[Edit Mode] Entire engine assembly auto-assembled. Recording session ready to start from step 1.</color>");
        }

        public void StartRecordingMode()
        {
            isRecordingDisassembly = true;
            userHasExplicitlyChangedGroup = false;
            recordingLog.Clear();
            recordedDisassemblyStates.Clear();
            recordingCurrentIndex = recordingStartIndex > 0 ? recordingStartIndex : 1;
            recordingCurrentGroup = recordingStartGroup > 0 ? recordingStartGroup : 1;

            HighlightCurrentEditModeGroup();

            Debug.Log($"<color=cyan>[Edit Mode]</color> <color=red>WORKSHOP EDIT & DISASSEMBLY RECORDING MODE STARTED.</color> Starting index: #{recordingCurrentIndex} (Group: #{recordingCurrentGroup}). Disassemble parts to record assembly order. Click 'Stop Record' or press [F6] to save.");
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

            // Automatically save recorded disassembly to persistent JSON file and scene layout!
            SaveRecordedDisassemblyNow();

            Debug.Log($"<color=green>[Edit Mode] {recordedDisassemblyStates.Count} disassembly states recorded and saved. Changes will persist to scene on Play Mode exit.</color>");
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
            if (!isRecordingDisassembly || part == null) return;

            int step = recordedDisassemblyStates.Count + 1;
            bool isSub = part.ParentSubAssembly != null;
            string subName = isSub ? part.ParentSubAssembly.SubAssemblyName : null;

            // Preserve part's existing group index from batch setup unless user explicitly adjusted group via I/K
            int assignedGroup;
            if (userHasExplicitlyChangedGroup)
            {
                assignedGroup = recordingCurrentGroup;
            }
            else if (part.GroupIndex > 0)
            {
                assignedGroup = part.GroupIndex;
                recordingCurrentGroup = assignedGroup;
            }
            else
            {
                assignedGroup = recordingCurrentGroup;
            }

            part.GroupIndex = assignedGroup;

            if (isSub)
            {
                part.LocalOrderIndex = recordingCurrentIndex;
            }
            else
            {
                part.OrderIndex = recordingCurrentIndex;
            }

            string scope = isSub ? $"Sub-Assembly '{subName}'" : "Main Assembly";
            string stateDesc = $"Step #{step}: Disassembled '{part.PartDisplayName}' from {scope} => Assembly Index #{recordingCurrentIndex} (Group #{assignedGroup})";

            var recordInfo = new DisassemblyRecordInfo
            {
                part = part,
                partDisplayName = part.PartDisplayName,
                partId = part.PartId,
                assignedIndex = recordingCurrentIndex,
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
            recordingLog.Add((part, recordingCurrentIndex));

            Debug.Log($"<color=orange>[Record Mode] {stateDesc}</color>");

            if (autoAdvanceIndexOnDisassemble)
            {
                recordingCurrentIndex++;
                userHasExplicitlyChangedGroup = false;
            }
        }

        private void DrawRecordModeButton()
        {
            float btnHeight = 36f;
            float margin = 20f;
            Color prevColor = GUI.backgroundColor;

            if (!isRecordingDisassembly)
            {
                float btnWidth = 175f;
                float assembleWidth = 135f;
                float saveWidth = 70f;
                bool hasRecorded = recordedDisassemblyStates.Count > 0;
                float totalW = hasRecorded ? btnWidth + assembleWidth + saveWidth + 12f : btnWidth + assembleWidth + 6f;
                float startX = Screen.width - totalW - margin;

                GUI.backgroundColor = new Color(0.2f, 0.85f, 0.45f);
                if (GUI.Button(new Rect(startX, margin, btnWidth, btnHeight), new GUIContent("▶ START RECORD (F6)", "Click or press F6 to start recording disassembly sequence.")))
                {
                    StartRecordingMode();
                }

                GUI.backgroundColor = new Color(0.85f, 0.55f, 0.15f);
                if (GUI.Button(new Rect(startX + btnWidth + 6f, margin, assembleWidth, btnHeight), new GUIContent("⚙ AUTO ASSEMBLE", "Assembles all parts onto the engine so you can record disassembly.")))
                {
                    AutoAssembleAllForRecording();
                }

                if (hasRecorded)
                {
                    GUI.backgroundColor = new Color(0.25f, 0.7f, 1.0f);
                    if (GUI.Button(new Rect(startX + btnWidth + assembleWidth + 12f, margin, saveWidth, btnHeight), new GUIContent("💾 SAVE", "Saves recorded disassembly to JSON file and scene.")))
                    {
                        SaveRecordedDisassemblyNow();
                    }
                }
            }
            else
            {
                float btnWidth = 160f;
                float stepWidth = 115f;
                float undoWidth = 95f;
                float assembleWidth = 100f;
                float saveWidth = 65f;
                float totalW = btnWidth + stepWidth + undoWidth + assembleWidth + saveWidth + 24f;
                float startX = Screen.width - totalW - margin;

                bool pulse = (int)(Time.unscaledTime * 3f) % 2 == 0;
                GUI.backgroundColor = pulse ? new Color(1f, 0.28f, 0.22f) : new Color(0.72f, 0.15f, 0.15f);
                if (GUI.Button(new Rect(startX, margin, btnWidth, btnHeight), new GUIContent("■ STOP & SAVE (F6)", "Click or press F6 to stop recording and permanently save.")))
                {
                    StopRecordingMode();
                }

                GUI.backgroundColor = autoAdvanceIndexOnDisassemble ? new Color(0.2f, 0.8f, 0.9f) : new Color(0.5f, 0.55f, 0.6f);
                string stepLabel = autoAdvanceIndexOnDisassemble ? "⚡ STEP: ON (X)" : "⚡ STEP: OFF (X)";
                if (GUI.Button(new Rect(startX + btnWidth + 6f, margin, stepWidth, btnHeight), new GUIContent(stepLabel, "Toggle auto-incrementing assembly index on disassemble [X]. When ON, each disassembled part gets the next sequential index.")))
                {
                    autoAdvanceIndexOnDisassemble = !autoAdvanceIndexOnDisassemble;
                    ShowSaveToastNotification($"⚡ Auto-Advance Index: {(autoAdvanceIndexOnDisassemble ? "ON" : "OFF")}");
                }

                GUI.backgroundColor = new Color(0.9f, 0.7f, 0.2f);
                if (GUI.Button(new Rect(startX + btnWidth + stepWidth + 12f, margin, undoWidth, btnHeight), new GUIContent("↩ UNDO (Z)", "Undoes last recorded step and auto re-assembles the part.")))
                {
                    UndoLastDisassemblyStep();
                }

                GUI.backgroundColor = new Color(0.85f, 0.55f, 0.15f);
                if (GUI.Button(new Rect(startX + btnWidth + stepWidth + undoWidth + 18f, margin, assembleWidth, btnHeight), new GUIContent("⚙ ASSEMBLE", "Auto-assembles all parts so you can restart recording.")))
                {
                    AutoAssembleAllForRecording();
                }

                GUI.backgroundColor = new Color(0.25f, 0.7f, 1.0f);
                if (GUI.Button(new Rect(startX + btnWidth + stepWidth + undoWidth + assembleWidth + 24f, margin, saveWidth, btnHeight), new GUIContent("💾 SAVE", "Saves current recorded disassembly immediately to JSON file and scene.")))
                {
                    SaveRecordedDisassemblyNow();
                }
            }

            GUI.backgroundColor = prevColor;

            // Render save confirmation toast if recently saved
            if (savedNotificationTimer > 0f)
            {
                float toastW = 480f;
                float toastH = 28f;
                float toastX = Screen.width - toastW - margin;
                float toastY = margin + btnHeight + 6f;

                Color prevGui = GUI.color;
                GUI.color = new Color(0.1f, 0.5f, 0.2f, 0.94f);
                GUI.DrawTexture(new Rect(toastX, toastY, toastW, toastH), Texture2D.whiteTexture);
                GUI.color = prevGui;

                GUIStyle toastStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
                string toastMsg = string.IsNullOrEmpty(savedNotificationText) ? "✔ Disassembly Saved to Scene & JSON!" : savedNotificationText;
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
            float bannerWidth = 740f;
            float bannerHeight = 62f;
            float bannerX = (Screen.width - bannerWidth) * 0.5f;
            float bannerY = 16f;

            Rect bannerRect = new Rect(bannerX, bannerY, bannerWidth, bannerHeight);

            // Dark card background
            Color prevColor = GUI.color;
            GUI.color = new Color(0.07f, 0.09f, 0.14f, 0.94f);
            GUI.DrawTexture(bannerRect, Texture2D.whiteTexture);

            // Top accent stripe
            Rect accentRect = new Rect(bannerX, bannerY, bannerWidth, 4f);
            GUI.color = new Color(0.25f, 0.85f, 1f, 1f);
            GUI.DrawTexture(accentRect, Texture2D.whiteTexture);
            GUI.color = prevColor;

            // Blinking "EDIT MODE" badge
            bool blink = (int)(Time.unscaledTime * 2.5f) % 2 == 0;
            GUIStyle recBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 10,
                normal = { textColor = blink ? Color.white : new Color(1f, 0.4f, 0.4f) }
            };

            GUI.color = blink ? new Color(0.85f, 0.15f, 0.15f, 0.95f) : new Color(0.55f, 0.1f, 0.1f, 0.95f);
            GUI.DrawTexture(new Rect(bannerX + 14f, bannerY + 10f, 90f, 20f), Texture2D.whiteTexture);
            GUI.color = prevColor;
            GUI.Label(new Rect(bannerX + 14f, bannerY + 10f, 90f, 20f), "● EDIT MODE", recBadgeStyle);

            // Title
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(bannerX + 115f, bannerY + 10f, bannerWidth - 125f, 22f),
                "DISASSEMBLY ORDER & WORKBENCH RECORDING", titleStyle);

            // Status line with last disassembly state info
            GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.85f, 0.9f, 0.95f) }
            };

            string lastInfo = recordedDisassemblyStates.Count > 0
                ? $"Last: <b><color=#5cf>{recordedDisassemblyStates[recordedDisassemblyStates.Count - 1].partDisplayName}</color></b> (Order #{recordedDisassemblyStates[recordedDisassemblyStates.Count - 1].assignedIndex}, Grp #{recordedDisassemblyStates[recordedDisassemblyStates.Count - 1].groupIndex})"
                : "Disassemble any part to begin recording";

            string autoAdvText = autoAdvanceIndexOnDisassemble ? "<color=#5cf>ON [X]</color>" : "<color=#aaa>OFF [X]</color>";
            string statusText = $"Next: <b><color=#ffd24d>Index #{recordingCurrentIndex}</color></b> (Group: <b><color=#5cf>#{recordingCurrentGroup}</color></b>)   |   Auto-Step: <b>{autoAdvText}</b>   |   Recorded: <b>{recordedDisassemblyStates.Count}</b>   |   <b>[Z]</b> Undo   |   <b>[U]</b> Save   |   {lastInfo}";
            GUI.Label(new Rect(bannerX + 16f, bannerY + 34f, bannerWidth - 32f, 22f), statusText, statusStyle);
        }

        private void DrawEditModeControlsPanel()
        {
            float panelWidth = 370f;
            float panelHeight = recordedDisassemblyStates.Count > 0 ? 335f : 295f;
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
                "[F6] / Click",
                "[X] / Click",
                "[Z] / Click",
                "[Y] / Click",
                "[U]",
                "[N] / [B]",
                "[LMB] / [E]",
                "[J] / [L]",
                "[I] / [K]",
                "[Alt] / [Tab]",
                "[Q] / [RMB]"
            };

            string[] descs = new string[]
            {
                "Stop Record & Save Order",
                $"Auto-Advance Index (Currently {(autoAdvanceIndexOnDisassemble ? "ON" : "OFF")})",
                "Undo last disassembly (auto re-assembles)",
                "Auto assemble entire engine to record",
                "Permanent Save (JSON & Scene)",
                "Next Assembly [N] / Disassembly [B] Hint",
                "Disassemble & Record into group/index",
                $"Change Index (Currently #{recordingCurrentIndex})",
                $"Change Group on Index (Currently #{recordingCurrentGroup})",
                "Unlock Mouse to click Start/Save buttons",
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
