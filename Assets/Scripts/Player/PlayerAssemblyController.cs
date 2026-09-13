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

        public AssemblyPart CurrentHeldPart => currentHeldPart;
        public bool IsHoldingPart => currentHeldPart != null;
        public bool IsCrawling => isCrawling;

        private void Awake()
        {
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
            // Press Escape to free cursor, click anywhere in game view to relock
            if (IsEscapePressed())
            {
                LockCursor(Cursor.lockState != CursorLockMode.Locked);
            }
            else if (Cursor.lockState != CursorLockMode.Locked && IsLeftClickPressed())
            {
                LockCursor(true);
            }
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
                if (!part.CanDisassemble(out string reason, out _))
                {
                    Debug.LogWarning($"[PlayerAssemblyController] Cannot pick up/disassemble '{part.PartDisplayName}': {reason}");
                    return;
                }

                if (!part.Unsnap())
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
        }

        /// <summary>
        /// Snaps the currently held part directly to the ghost target socket.
        /// Supports interchangeable geometry sockets by accepting targetSocket and targetSnapPoint.
        /// </summary>
        public void SnapHeldPartToTarget(AssemblySocket targetSocket = null, Transform targetSnapPoint = null)
        {
            if (currentHeldPart == null) return;

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
                heldRb.isKinematic = false;
                heldRb.useGravity = true;
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
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        }

        private Vector2 GetLookInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.delta.ReadValue() * 0.1f;
            }
#endif
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        }

        private bool IsSprinting()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.leftShiftKey.isPressed;
            }
#endif
            return Input.GetKey(KeyCode.LeftShift);
        }

        private bool IsJumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.spaceKey.wasPressedThisFrame;
            }
#endif
            return Input.GetKeyDown(KeyCode.Space);
        }

        private bool IsInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.eKey.wasPressedThisFrame;
            }
#endif
            return Input.GetKeyDown(KeyCode.E);
        }

        private bool IsLeftClickPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.leftButton.wasPressedThisFrame;
            }
#endif
            return Input.GetMouseButtonDown(0);
        }

        private bool IsLeftClickHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.leftButton.isPressed;
            }
#endif
            return Input.GetMouseButton(0);
        }

        private float GetMouseScroll()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.scroll.ReadValue().y * 0.01f;
            }
#endif
            return Input.mouseScrollDelta.y;
        }

        private bool IsRightClickHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.rightButton.isPressed;
            }
#endif
            return Input.GetMouseButton(1);
        }

        private bool IsEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.escapeKey.wasPressedThisFrame;
            }
#endif
            return Input.GetKeyDown(KeyCode.Escape);
        }

        private bool IsDropPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.qKey.wasPressedThisFrame || Keyboard.current.gKey.wasPressedThisFrame;
            }
#endif
            return Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.G);
        }

        private bool IsCrawlPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.cKey.wasPressedThisFrame || Keyboard.current.leftCtrlKey.wasPressedThisFrame;
            }
#endif
            return Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl);
        }

        private bool IsCrawlHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.cKey.isPressed || Keyboard.current.leftCtrlKey.isPressed;
            }
#endif
            return Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl);
        }

        #endregion

        #region Crosshair & Game HUD

        private void OnGUI()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;

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
                        bool canDisassemble = hoveredPart.CanDisassemble(out string blockingReason, out _);
                        if (!canDisassemble)
                        {
                            dotColor = new Color(1.0f, 0.35f, 0.2f, 0.95f); // Locked amber/red
                            keyBadge = "LOCKED";
                            actionTitle = "CANNOT DISASSEMBLE";
                            targetName = hoveredPart.PartDisplayName;
                            detailText = blockingReason;
                            hudAccentColor = dotColor;
                        }
                        else
                        {
                            dotColor = crosshairHoverColor;
                            keyBadge = "E / CLICK";
                            actionTitle = "DISASSEMBLE";
                            targetName = hoveredPart.PartDisplayName;
                            detailText = "Unsnaps part and picks it up into hand";
                            hudAccentColor = dotColor;
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
    }
}
