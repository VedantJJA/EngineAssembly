using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace EngineAssembly
{
    /// <summary>
    /// Automates the engine assembly workflow, enabling complete assembly or partial assembly
    /// up to an N-th order priority / part count with the press of a button or key.
    /// In reverse order mode, parts with higher order indices are assembled first.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Engine Assembly/Auto Assembly Controller")]
    public class AutoAssemblyController : MonoBehaviour
    {
        public static AutoAssemblyController Instance { get; private set; }

        [Header("Keyboard Controls")]
        [Tooltip("Key to trigger automated assembly of the entire engine.")]
        [SerializeField] private KeyCode autoAssembleAllKey = KeyCode.U;

        [Tooltip("Key to assemble parts down to targetOrderIndex.")]
        [SerializeField] private KeyCode autoAssembleToOrderIndexKey = KeyCode.I;

        [Tooltip("Key to assemble the next single part in sequence.")]
        [SerializeField] private KeyCode assembleNextStepKey = KeyCode.O;

        [Tooltip("Key to disassemble and reset all parts.")]
        [SerializeField] private KeyCode disassembleAllKey = KeyCode.P;

        [Header("Assembly Targets")]
        [Tooltip("Target Order Priority index. In reverse mode (high numbers first), all parts with OrderIndex >= targetOrderIndex are assembled.")]
        [SerializeField] private int targetOrderIndex = 1;

        [Tooltip("Target number of parts to assemble in sequence (1 to TotalPartsCount).")]
        [SerializeField] private int targetPartCount = 1;

        [Header("Animation & Timing")]
        [Tooltip("If true, parts fly and snap sequentially with a delay between them. If false, parts snap immediately.")]
        [SerializeField] private bool animateSequence = true;

        [Tooltip("Delay in seconds between consecutive part snaps when animating.")]
        [Range(0.05f, 2f)]
        [SerializeField] private float delayBetweenParts = 0.35f;

        [Tooltip("Whether individual parts use smooth easing motion when snapping.")]
        [SerializeField] private bool smoothSnap = true;

        [Header("On-Screen Interactive UI")]
        [Tooltip("Displays a convenient on-screen overlay window with interactive buttons (no Canvas required).")]
        [SerializeField] private bool showOnScreenGUI = true;

        [Tooltip("Key to toggle the on-screen GUI visibility.")]
        [SerializeField] private KeyCode toggleGuiKey = KeyCode.F1;

        [Header("Events")]
        public UnityEvent onAutoAssemblyStarted;
        public UnityEvent<AssemblyPart> onStepAssembled;
        public UnityEvent onAutoAssemblyCompleted;
        public UnityEvent onDisassemblyCompleted;

        private Coroutine activeAssemblyRoutine;
        private AssemblyManager manager;
        private bool isAssembling = false;
        private Rect windowRect = new Rect(20, 20, 320, 370);

        public bool IsAssembling => isAssembling;
        public int TargetOrderIndex
        {
            get => targetOrderIndex;
            set => targetOrderIndex = value;
        }
        public int TargetPartCount
        {
            get => targetPartCount;
            set => targetPartCount = Mathf.Max(1, value);
        }
        public bool AnimateSequence
        {
            get => animateSequence;
            set => animateSequence = value;
        }
        public float DelayBetweenParts
        {
            get => delayBetweenParts;
            set => delayBetweenParts = Mathf.Max(0.01f, value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null && FindAnyObjectByType<AutoAssemblyController>() == null)
            {
                var mgr = FindAnyObjectByType<AssemblyManager>();
                if (mgr != null)
                {
                    mgr.gameObject.AddComponent<AutoAssemblyController>();
                }
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            EnsureManager();
            if (manager != null && manager.AssemblyParts != null && manager.AssemblyParts.Count > 0)
            {
                targetPartCount = Mathf.Clamp(targetPartCount, 1, manager.AssemblyParts.Count);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleGuiKey))
            {
                showOnScreenGUI = !showOnScreenGUI;
            }

            if (autoAssembleAllKey != KeyCode.None && Input.GetKeyDown(autoAssembleAllKey))
            {
                AssembleWhole();
            }

            if (autoAssembleToOrderIndexKey != KeyCode.None && Input.GetKeyDown(autoAssembleToOrderIndexKey))
            {
                AssembleToOrderIndex(targetOrderIndex);
            }

            if (assembleNextStepKey != KeyCode.None && Input.GetKeyDown(assembleNextStepKey))
            {
                AssembleNextStep();
            }

            if (disassembleAllKey != KeyCode.None && Input.GetKeyDown(disassembleAllKey))
            {
                DisassembleAll();
            }
        }

        private void EnsureManager()
        {
            if (manager == null)
            {
                manager = AssemblyManager.Instance;
                if (manager == null)
                {
                    manager = FindAnyObjectByType<AssemblyManager>();
                }
            }
        }

        #region Public Assembly Methods

        /// <summary>
        /// Assembles all engine parts from highest order priority down to lowest order priority.
        /// </summary>
        [ContextMenu("Assemble Whole Assembly")]
        public void AssembleWhole()
        {
            StopAutoAssembly();

            List<AssemblyPart> sequence = GetOrderedPartsSequence();
            if (sequence == null || sequence.Count == 0)
            {
                Debug.LogWarning("[AutoAssemblyController] No assembly parts found in scene!");
                return;
            }

            // Filter down to parts that are not yet snapped
            List<AssemblyPart> toAssemble = sequence.Where(p => p != null && !p.IsSnapped).ToList();
            if (toAssemble.Count == 0)
            {
                Debug.Log("[AutoAssemblyController] All parts are already assembled!");
                return;
            }

            if (animateSequence && Application.isPlaying)
            {
                activeAssemblyRoutine = StartCoroutine(RunAssemblySequenceRoutine(toAssemble));
            }
            else
            {
                ExecuteInstantAssembly(toAssemble);
            }
        }

        /// <summary>
        /// Assembles all parts down to the specified order priority index.
        /// In reverse order mode (where higher numbers are assembled first), this assembles all parts with OrderIndex >= targetIndex.
        /// </summary>
        public void AssembleToOrderIndex(int targetIndex)
        {
            StopAutoAssembly();
            targetOrderIndex = targetIndex;

            List<AssemblyPart> sequence = GetOrderedPartsSequence();
            if (sequence == null || sequence.Count == 0) return;

            // In reverse assembly: higher numbers assembled first, so parts with OrderIndex >= targetIndex should be assembled
            List<AssemblyPart> toAssemble = sequence
                .Where(p => p != null && p.AssemblyOrderIndex >= targetIndex && !p.IsSnapped)
                .ToList();

            if (toAssemble.Count == 0)
            {
                Debug.Log($"[AutoAssemblyController] All parts with OrderIndex >= {targetIndex} are already assembled!");
                return;
            }

            if (animateSequence && Application.isPlaying)
            {
                activeAssemblyRoutine = StartCoroutine(RunAssemblySequenceRoutine(toAssemble));
            }
            else
            {
                ExecuteInstantAssembly(toAssemble);
            }
        }

        /// <summary>
        /// Assembles the first N parts in the assembly sequence.
        /// </summary>
        public void AssembleToPartCount(int count)
        {
            StopAutoAssembly();
            targetPartCount = Mathf.Max(1, count);

            List<AssemblyPart> sequence = GetOrderedPartsSequence();
            if (sequence == null || sequence.Count == 0) return;

            int targetLimit = Mathf.Min(targetPartCount, sequence.Count);
            List<AssemblyPart> targetSublist = sequence.Take(targetLimit).ToList();
            List<AssemblyPart> toAssemble = targetSublist.Where(p => p != null && !p.IsSnapped).ToList();

            if (toAssemble.Count == 0)
            {
                Debug.Log($"[AutoAssemblyController] First {targetLimit} parts in sequence are already assembled!");
                return;
            }

            if (animateSequence && Application.isPlaying)
            {
                activeAssemblyRoutine = StartCoroutine(RunAssemblySequenceRoutine(toAssemble));
            }
            else
            {
                ExecuteInstantAssembly(toAssemble);
            }
        }

        /// <summary>
        /// Assembles the next single part in sequence.
        /// </summary>
        [ContextMenu("Assemble Next Step")]
        public void AssembleNextStep()
        {
            EnsureManager();
            List<AssemblyPart> sequence = GetOrderedPartsSequence();
            if (sequence == null) return;

            AssemblyPart nextPart = sequence.FirstOrDefault(p => p != null && !p.IsSnapped);
            if (nextPart == null)
            {
                Debug.Log("[AutoAssemblyController] All parts are already assembled!");
                return;
            }

            SafetyDropIfHeld(nextPart);
            nextPart.SnapDirectly(smoothSnap, ignorePrerequisites: true);
            onStepAssembled?.Invoke(nextPart);
        }

        /// <summary>
        /// Disassembles all parts and resets them to their initial positions.
        /// </summary>
        [ContextMenu("Disassemble All")]
        public void DisassembleAll()
        {
            StopAutoAssembly();
            EnsureManager();

            List<AssemblyPart> parts = manager != null ? manager.AssemblyParts.ToList() : FindObjectsByType<AssemblyPart>(FindObjectsInactive.Exclude).ToList();
            if (parts == null) return;

            // Disassemble in reverse of assembly (lowest index first)
            var sortedForDisassembly = parts.OrderBy(p => p.AssemblyOrderIndex).ToList();
            for (int i = 0; i < sortedForDisassembly.Count; i++)
            {
                var p = sortedForDisassembly[i];
                if (p != null)
                {
                    SafetyDropIfHeld(p);
                    p.ResetToInitialPosition();
                }
            }

            onDisassemblyCompleted?.Invoke();
            Debug.Log("[AutoAssemblyController] Disassembled all parts and reset to initial positions.");
        }

        /// <summary>
        /// Stops any currently running animated assembly coroutine.
        /// </summary>
        public void StopAutoAssembly()
        {
            if (activeAssemblyRoutine != null)
            {
                StopCoroutine(activeAssemblyRoutine);
                activeAssemblyRoutine = null;
            }
            isAssembling = false;
        }

        #endregion

        #region String / UI Hooks

        public void SetTargetOrderIndex(string text)
        {
            if (int.TryParse(text, out int val))
            {
                targetOrderIndex = val;
            }
        }

        public void SetTargetPartCount(string text)
        {
            if (int.TryParse(text, out int val))
            {
                targetPartCount = Mathf.Max(1, val);
            }
        }

        #endregion

        #region Internal Sequencing

        private List<AssemblyPart> GetOrderedPartsSequence()
        {
            EnsureManager();
            if (manager != null)
            {
                manager.SortPartsByOrderIndex();
                return manager.AssemblyParts.ToList();
            }

            // Fallback: search scene and sort descending (highest index first)
            var found = FindObjectsByType<AssemblyPart>(FindObjectsInactive.Exclude).ToList();
            return found.OrderByDescending(p => p.AssemblyOrderIndex).ToList();
        }

        private IEnumerator RunAssemblySequenceRoutine(List<AssemblyPart> partsToAssemble)
        {
            isAssembling = true;
            onAutoAssemblyStarted?.Invoke();

            for (int i = 0; i < partsToAssemble.Count; i++)
            {
                AssemblyPart part = partsToAssemble[i];
                if (part == null || part.IsSnapped) continue;

                SafetyDropIfHeld(part);
                part.SnapDirectly(smoothSnap, ignorePrerequisites: true);
                onStepAssembled?.Invoke(part);

                if (delayBetweenParts > 0f)
                {
                    yield return new WaitForSeconds(delayBetweenParts);
                }
                else
                {
                    yield return null;
                }
            }

            isAssembling = false;
            activeAssemblyRoutine = null;
            onAutoAssemblyCompleted?.Invoke();
            Debug.Log("<color=cyan>[AutoAssemblyController] Auto-assembly sequence finished!</color>");
        }

        private void ExecuteInstantAssembly(List<AssemblyPart> partsToAssemble)
        {
            isAssembling = true;
            onAutoAssemblyStarted?.Invoke();

            for (int i = 0; i < partsToAssemble.Count; i++)
            {
                AssemblyPart part = partsToAssemble[i];
                if (part == null || part.IsSnapped) continue;

                SafetyDropIfHeld(part);
                part.SnapDirectly(smooth: false, ignorePrerequisites: true);
                onStepAssembled?.Invoke(part);
            }

            isAssembling = false;
            onAutoAssemblyCompleted?.Invoke();
            Debug.Log("<color=cyan>[AutoAssemblyController] Instant assembly complete!</color>");
        }

        private void SafetyDropIfHeld(AssemblyPart part)
        {
            // Drop part from player hands if currently held
            var player = PlayerAssemblyController.Instance;
            if (player != null && player.CurrentHeldPart == part)
            {
                player.DropHeldPart();
            }
        }

        #endregion

        #region OnGUI Overlay

        private void OnGUI()
        {
            if (!showOnScreenGUI) return;

            GUI.skin.window.fontSize = 12;
            windowRect = GUI.Window(98721, windowRect, DrawAutoAssemblyWindow, "Auto Assembly Controls");
        }

        private void DrawAutoAssemblyWindow(int windowID)
        {
            EnsureManager();
            int total = manager != null ? manager.TotalPartsCount : 0;
            int snapped = manager != null ? manager.SnappedPartsCount : 0;

            GUILayout.BeginVertical();

            // Status bar
            GUILayout.Label($"Status: {snapped} / {total} Parts Assembled ({(total > 0 ? (snapped * 100 / total) : 0)}%)", GUI.skin.box);

            if (isAssembling)
            {
                GUI.color = Color.yellow;
                GUILayout.Label("Status: Auto-Assembling...", GUI.skin.box);
                GUI.color = Color.white;

                if (GUILayout.Button("Stop Assembly"))
                {
                    StopAutoAssembly();
                }
            }
            else
            {
                // Full Assemble
                GUI.color = new Color(0.4f, 1f, 0.5f);
                if (GUILayout.Button($"Assemble Whole (Key: {autoAssembleAllKey})", GUILayout.Height(30)))
                {
                    AssembleWhole();
                }
                GUI.color = Color.white;

                GUILayout.Space(6);

                // Assemble to Order Index
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Order Index: {targetOrderIndex}", GUILayout.Width(100));
                if (GUILayout.Button("-", GUILayout.Width(25))) targetOrderIndex = Mathf.Max(0, targetOrderIndex - 1);
                if (GUILayout.Button("+", GUILayout.Width(25))) targetOrderIndex++;
                if (GUILayout.Button($"Assemble >= {targetOrderIndex} ({autoAssembleToOrderIndexKey})"))
                {
                    AssembleToOrderIndex(targetOrderIndex);
                }
                GUILayout.EndHorizontal();

                // Assemble to Part Count
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Step Count: {targetPartCount}", GUILayout.Width(100));
                if (GUILayout.Button("-", GUILayout.Width(25))) targetPartCount = Mathf.Max(1, targetPartCount - 1);
                if (GUILayout.Button("+", GUILayout.Width(25))) targetPartCount = Mathf.Min(total > 0 ? total : 999, targetPartCount + 1);
                if (GUILayout.Button($"Assemble {targetPartCount} Parts"))
                {
                    AssembleToPartCount(targetPartCount);
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(6);

                // Single Step
                if (GUILayout.Button($"Assemble Next Step ({assembleNextStepKey})"))
                {
                    AssembleNextStep();
                }

                // Disassemble All
                GUI.color = new Color(1f, 0.6f, 0.5f);
                if (GUILayout.Button($"Disassemble All ({disassembleAllKey})"))
                {
                    DisassembleAll();
                }
                GUI.color = Color.white;
            }

            GUILayout.Space(6);

            // Animation Settings
            animateSequence = GUILayout.Toggle(animateSequence, "Animate Sequential Flight");
            if (animateSequence)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Delay: {delayBetweenParts:F2}s", GUILayout.Width(80));
                delayBetweenParts = GUILayout.HorizontalSlider(delayBetweenParts, 0.05f, 1.5f);
                GUILayout.EndHorizontal();
            }

            GUILayout.Label($"Press {toggleGuiKey} to toggle this panel", GUI.skin.label);

            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        #endregion
    }
}
