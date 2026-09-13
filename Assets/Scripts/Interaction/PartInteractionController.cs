using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EngineAssembly
{
    /// <summary>
    /// Desktop test controller for picking up, moving, and rotating assembly parts in 3D space.
    /// Supports the new Unity Input System with fallback compatibility.
    /// Easily test snapping and translucent ghost preview without VR hardware.
    /// </summary>
    [DisallowMultipleComponent]
    public class PartInteractionController : MonoBehaviour
    {
        [Header("Camera & Raycasting")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private LayerMask interactableLayers = ~0;
        [SerializeField] private float maxRaycastDistance = 50f;

        [Header("Movement & Manipulation")]
        [Tooltip("Speed when moving part to follow mouse ray.")]
        [SerializeField] private float moveSmoothSpeed = 15f;

        [Tooltip("Mouse scroll wheel sensitivity to push/pull part depth.")]
        [SerializeField] private float scrollSensitivity = 0.5f;

        [Tooltip("Minimum and maximum distance from camera when holding part.")]
        [SerializeField] private float minGrabDistance = 0.3f;
        [SerializeField] private float maxGrabDistance = 5f;

        [Tooltip("Rotation sensitivity when right-click dragging.")]
        [SerializeField] private float rotationSpeed = 3f;

        // Active State
        private AssemblyPart currentlyHeldPart;
        private float currentGrabDistance;
        private Vector3 grabOffset;
        private Vector2 previousMousePos;

        private void Awake()
        {
            if (FindAnyObjectByType<PlayerAssemblyController>() != null)
            {
                enabled = false;
                return;
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            }
        }

        private void OnDisable()
        {
            if (currentlyHeldPart != null)
            {
                currentlyHeldPart.Deselect();
                currentlyHeldPart = null;
            }
        }

        private void Update()
        {
            // If the FPS PlayerAssemblyController is present, disable this test script to prevent distant raycast dragging conflicts
            if (FindAnyObjectByType<PlayerAssemblyController>() != null)
            {
                enabled = false;
                return;
            }

            HandleInput();
        }

        private void HandleInput()
        {
            Vector2 mouseScreenPos = GetMousePosition();
            Vector2 mouseDelta = mouseScreenPos - previousMousePos;
            previousMousePos = mouseScreenPos;

            bool leftDown = IsLeftMouseButtonDown();
            bool leftHeld = IsLeftMouseButtonHeld();
            bool leftUp = IsLeftMouseButtonUp();
            bool rightHeld = IsRightMouseButtonHeld();
            float scroll = GetMouseScroll();

            // 1. Pick up part on Left Click
            if (leftDown && currentlyHeldPart == null)
            {
                TryPickUpPart(mouseScreenPos);
            }

            // 2. Manipulate held part
            if (currentlyHeldPart != null && leftHeld)
            {
                // Adjust depth with scroll wheel
                if (Mathf.Abs(scroll) > 0.001f)
                {
                    currentGrabDistance = Mathf.Clamp(currentGrabDistance + scroll * scrollSensitivity, minGrabDistance, maxGrabDistance);
                }

                // Rotate part if right mouse button is held
                if (rightHeld)
                {
                    Vector3 cameraUp = targetCamera != null ? targetCamera.transform.up : Vector3.up;
                    Vector3 cameraRight = targetCamera != null ? targetCamera.transform.right : Vector3.right;

                    currentlyHeldPart.transform.Rotate(cameraUp, -mouseDelta.x * rotationSpeed, Space.World);
                    currentlyHeldPart.transform.Rotate(cameraRight, mouseDelta.y * rotationSpeed, Space.World);
                }

                // Move part to ray destination
                if (targetCamera != null)
                {
                    Ray ray = targetCamera.ScreenPointToRay(mouseScreenPos);
                    Vector3 targetPosition = ray.GetPoint(currentGrabDistance) + grabOffset;

                    currentlyHeldPart.transform.position = Vector3.Lerp(
                        currentlyHeldPart.transform.position,
                        targetPosition,
                        Time.deltaTime * moveSmoothSpeed
                    );
                }
            }

            // 3. Release part on Left Click Up
            if (leftUp && currentlyHeldPart != null)
            {
                ReleaseHeldPart();
            }
        }

        private void TryPickUpPart(Vector2 screenPos)
        {
            if (targetCamera == null) return;

            Ray ray = targetCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, interactableLayers))
            {
                AssemblyPart part = hit.collider.GetComponentInParent<AssemblyPart>();
                if (part != null && !part.IsSnapped)
                {
                    GrabPart(part, hit.point);
                }
            }
        }

        /// <summary>
        /// Grabs the given part and triggers its ghost preview.
        /// Can be called directly by custom controllers or VR grab interactables.
        /// </summary>
        public void GrabPart(AssemblyPart part, Vector3? contactPoint = null)
        {
            if (part == null || part.IsSnapped) return;

            currentlyHeldPart = part;

            if (targetCamera != null)
            {
                Vector3 point = contactPoint ?? part.transform.position;
                currentGrabDistance = Vector3.Distance(targetCamera.transform.position, point);
                grabOffset = part.transform.position - point;
            }
            else
            {
                currentGrabDistance = 1.5f;
                grabOffset = Vector3.zero;
            }

            currentlyHeldPart.Select();
        }

        /// <summary>
        /// Releases the currently held part, checking for snap conditions.
        /// </summary>
        public void ReleaseHeldPart()
        {
            if (currentlyHeldPart == null) return;

            AssemblyPart part = currentlyHeldPart;
            currentlyHeldPart = null;

            part.Deselect();
        }

        #region Input Helper Abstractions

        private Vector2 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return Input.mousePosition;
            }
            catch (System.InvalidOperationException) { }
#endif
            return Vector2.zero;
        }

        private bool IsLeftMouseButtonDown()
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

        private bool IsLeftMouseButtonHeld()
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

        private bool IsLeftMouseButtonUp()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.leftButton.wasReleasedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return Input.GetMouseButtonUp(0);
            }
            catch (System.InvalidOperationException) { }
#endif
            return false;
        }

        private bool IsRightMouseButtonHeld()
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

        #endregion
    }
}
