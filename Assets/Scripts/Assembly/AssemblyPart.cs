using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace EngineAssembly
{
    public enum AssemblyPartState { Loose, Held, Snapping, Installed }
    [SelectionBase, DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public class AssemblyPart : MonoBehaviour
    {
        [SerializeField] string partId;
        [SerializeField] string partDisplayName;
        [SerializeField] string geometryGroupId;
        [SerializeField, FormerlySerializedAs("assemblyOrderIndex")] int orderIndex = 1;
        [SerializeField] Transform targetSnapPoint;
        [SerializeField] AssemblySocket targetSocket;
        [Header("Snap feel")]
        [SerializeField, Min(.001f)] float snapDistanceThreshold = .15f;
        [SerializeField, Range(0,180)] float snapAngleThreshold = 45f;
        [SerializeField] SnapRotationMode rotationMode;
        [SerializeField, Range(1,360)] int rotationalSymmetry = 1;
        [Tooltip("Optional mating point on this part. Target pose aligns this point, not the CAD origin.")]
        [SerializeField] Transform gripAnchor;
        [SerializeField] bool snapOnRelease = true;
        [SerializeField, Min(0)] float snapDuration = .18f;
        [SerializeField] bool useGravityWhenLoose = true;
        public UnityEvent onSelected = new UnityEvent(), onDeselected = new UnityEvent(), onSnapped = new UnityEvent(), onUnsnapped = new UnityEvent(), onSnapRejected = new UnityEvent();
        static readonly List<AssemblyPart> active = new List<AssemblyPart>();
        public static IReadOnlyList<AssemblyPart> AllParts => active;
        public static IReadOnlyList<AssemblyPart> AllActiveParts => active;
        public AssemblyManager Manager { get; internal set; }
        public PartDefinition Definition { get; private set; }
        public AssemblyPartState State { get; private set; }
        public string PartId => partId;
        public string PartDisplayName => string.IsNullOrEmpty(partDisplayName) ? name : partDisplayName;
        public string ChapterId => Definition?.chapterId;
        public string GeometryGroupId { get => geometryGroupId; set => geometryGroupId = value; }
        public int OrderIndex { get => orderIndex; set { orderIndex = value; if (Definition != null) Definition.order = value; } }
        public bool IsSnapped => State == AssemblyPartState.Installed;
        public bool IsSelected => State == AssemblyPartState.Held;
        public bool IsBusy => State == AssemblyPartState.Snapping;
        public bool IsInSnapZone => Candidate && Evaluate(Candidate, true, out _);
        public AssemblySocket Candidate { get; private set; }
        public AssemblySocket InstalledSocket { get; private set; }
        public AssemblySocket TargetSocket { get => targetSocket; set => targetSocket = value; }
        public Transform TargetSnapPoint { get => targetSocket ? targetSocket.SnapTransform : targetSnapPoint; set => targetSnapPoint = value; }
        public string RejectionReason { get; private set; } = "";
        public Rigidbody Body { get { if (!body) body = GetComponent<Rigidbody>(); return body; } }
        Rigidbody body;
        Coroutine snapRoutine;
        AssemblySocket reservedSocket;
        AssemblyVisual visual;
        Vector3 anchorPosition;
        Quaternion anchorRotation;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ClearRegistry() => active.Clear();
        void Reset() { partId = Guid.NewGuid().ToString("N"); partDisplayName = name; }
        void Awake() { CacheAnchor(); }
        void OnEnable() { if (!active.Contains(this)) active.Add(this); }
        void OnDisable() { active.Remove(this); CancelSnap(); if (visual) visual.Clear(); }
        void OnDestroy() { if (InstalledSocket && InstalledSocket.CurrentPart == this) InstalledSocket.DetachPart(); }
        void CacheAnchor()
        {
            anchorPosition = gripAnchor ? transform.InverseTransformPoint(gripAnchor.position) : Vector3.zero;
            anchorRotation = gripAnchor ? Quaternion.Inverse(transform.rotation) * gripAnchor.rotation : Quaternion.identity;
        }
        public void SetIdentity(string id, string label) { partId = id; partDisplayName = label; }
        public void Configure(PartDefinition definition, AssemblyManager manager)
        {
            Definition = definition; Manager = manager; partId = definition.id; partDisplayName = definition.displayName;
            orderIndex = definition.order; geometryGroupId = definition.geometryGroup;
            snapDistanceThreshold = definition.snapDistance; snapAngleThreshold = definition.snapAngle;
            rotationMode = definition.rotationMode; rotationalSymmetry = definition.symmetry; CacheAnchor();
        }
        void Update()
        {
            if (!IsSelected || (Manager && Manager.Interaction == WorkshopInteraction.Move)) { if (visual && IsSelected) visual.Clear(); return; }
            if(Manager && Manager.Mode==WorkshopMode.Assembly && (Manager.Difficulty==SnapDifficulty.Easy || Manager.Task==AssemblyTask.Disassemble)) { if(visual)visual.Clear();return; }
            Candidate = FindBestSocket();
            bool ready = Candidate && Evaluate(Candidate, true, out _);
            if (Candidate)
            {
                Evaluate(Candidate, true, out string reason); RejectionReason = reason;
                GetVisual().ShowGhost(this, Candidate, ready ? AssemblyVisual.Ready : AssemblyVisual.Waiting,true);
            }
            else { RejectionReason = "Move toward a compatible socket."; if (visual) visual.Clear(); }
            if (ready && !snapOnRelease) TrySnap();
        }
        AssemblyVisual GetVisual() => visual ? visual : (visual = gameObject.AddComponent<AssemblyVisual>());
        public bool BeginGrab()
        {
            if (IsBusy || (Manager && !Manager.CanPickUp(this, out _))) return false;
            if (IsSnapped && !Unsnap()) return false;
            State = AssemblyPartState.Held;
            SetPhysics(true);
            transform.SetParent(null, true);
            onSelected.Invoke(); return true;
        }
        public void Select() => BeginGrab();
        public void Deselect() => Release();
        public bool Release(bool attemptSnap = true)
        {
            if (!IsSelected) return false;
            bool snapped = attemptSnap && (!Manager || (Manager.Interaction == WorkshopInteraction.Build && Manager.Difficulty != SnapDifficulty.Easy)) && TrySnap();
            if (!snapped) { State = AssemblyPartState.Loose; SetPhysics(false); if (visual) visual.Clear(); }
            onDeselected.Invoke(); return snapped;
        }
        public void MoveHeld(Vector3 position, Quaternion rotation)
        {
            if (IsSelected) transform.SetPositionAndRotation(position, rotation);
        }
        public AssemblySocket FindBestSocket()
        {
            AssemblySocket best = null; float bestScore = float.PositiveInfinity;
            foreach (var socket in AssemblySocket.AllActiveSockets)
            {
                if (!socket.CanAcceptPart(this)) continue;
                float d = Vector3.Distance(AnchorWorldPosition, socket.SnapPosition);
                if (d > Mathf.Max(2f, snapDistanceThreshold * 6)) continue;
                bool allowed = !Manager || Manager.CanInstall(this, socket, out _);
                float score = d + (allowed ? 0 : 10);
                if (score < bestScore) { bestScore = score; best = socket; }
            }
            return best;
        }
        public Vector3 AnchorWorldPosition => transform.TransformPoint(anchorPosition);
        public Quaternion AnchorWorldRotation => transform.rotation * anchorRotation;
        public Pose GetTargetPose(AssemblySocket socket)
        {
            Quaternion rot = socket.SnapRotation * Quaternion.Inverse(anchorRotation);
            return new Pose(socket.SnapPosition - rot * Vector3.Scale(anchorPosition, transform.lossyScale), rot);
        }
        public static float RotationError(Quaternion current, Quaternion target, SnapRotationMode mode, int symmetry)
        {
            if (mode == SnapRotationMode.Free) return 0;
            if (mode == SnapRotationMode.Axial) return Vector3.Angle(current * Vector3.up, target * Vector3.up);
            float angle = 180;
            for (int i = 0; i < Mathf.Max(1,symmetry); i++) angle = Mathf.Min(angle, Quaternion.Angle(current, target * Quaternion.AngleAxis(i * 360f / Mathf.Max(1,symmetry), Vector3.up)));
            return angle;
        }
        public bool Evaluate(AssemblySocket socket, bool proximity, out string reason)
        {
            reason = "";
            if (!socket || !socket.CanAcceptPart(this)) { reason = "Socket occupied or incompatible."; return false; }
            if (Manager && !Manager.CanInstall(this, socket, out reason)) return false;
            if (proximity && Vector3.Distance(AnchorWorldPosition, socket.SnapPosition) > snapDistanceThreshold) { reason = "Move closer to the target."; return false; }
            if (proximity && (!Manager || Manager.Difficulty == SnapDifficulty.Hard) && RotationError(AnchorWorldRotation, socket.SnapRotation, rotationMode, rotationalSymmetry) > snapAngleThreshold) { reason = "Rotate the part to match the target."; return false; }
            return true;
        }
        public bool TrySnap() => SnapToSocket(FindBestSocket());
        public bool SnapToSocket(AssemblySocket socket) => StartSnap(socket, true);
        // Only the line-of-sight guide calls this after its geometry and occlusion raycasts.
        internal bool GuidedSnap(AssemblySocket socket)
        {
            if(!IsSelected || !Manager || Manager.Difficulty!=SnapDifficulty.Easy)return false;
            bool snapped=StartSnap(socket,false);if(snapped)onDeselected.Invoke();return snapped;
        }
        bool StartSnap(AssemblySocket socket, bool proximity)
        {
            string reason = "";
            if (IsSnapped || IsBusy || !Evaluate(socket, proximity, out reason))
            {
                RejectionReason = IsSnapped || IsBusy ? "Part already installed or moving." : reason;
                onSnapRejected.Invoke(); return false;
            }
            if (!socket.Reserve(this)) return false;
            if(proximity && Manager && Manager.Mode==WorkshopMode.Assembly && Manager.Interaction==WorkshopInteraction.Build)
            {
                var player=FindAnyObjectByType<PlayerAssemblyController>();var guide=Manager.GetComponent<AssemblyEasyGuide>();
                if(player && player.ViewCamera && guide && !guide.VisibleFrom(this,player.ViewCamera))
                {
                    socket.ReleaseReservation(this);RejectionReason="Move until you can see the target surface.";onSnapRejected.Invoke();return false;
                }
            }
            RejectionReason="";reservedSocket = socket; State = AssemblyPartState.Snapping; SetPhysics(true);
            if (visual) visual.Clear();
            snapRoutine = StartCoroutine(SnapRoutine(socket)); return true;
        }
        IEnumerator SnapRoutine(AssemblySocket socket)
        {
            Vector3 start = transform.position; Quaternion rotation = transform.rotation;
            float elapsed = 0;
            while (elapsed < snapDuration)
            {
                if (!socket || !Evaluate(socket, false, out _)) { CancelSnap(); yield break; }
                elapsed += Time.deltaTime;
                Pose target = GetTargetPose(socket); float t = Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed / Mathf.Max(.001f,snapDuration)));
                transform.SetPositionAndRotation(Vector3.Lerp(start,target.position,t), Quaternion.Slerp(rotation,target.rotation,t));
                yield return null;
            }
            snapRoutine = null;
            if (!socket || !InstallImmediate(socket)) CancelSnap();
        }
        // Used only by chapter preparation/editor preview, not as a player shortcut.
        public bool InstallImmediate(AssemblySocket socket)
        {
            if (!socket || !socket.AttachPart(this)) return false;
            Pose pose = GetTargetPose(socket); transform.SetPositionAndRotation(pose.position,pose.rotation);
            transform.SetParent(socket.SnapTransform,true); InstalledSocket = socket; reservedSocket = null;
            State = AssemblyPartState.Installed; SetPhysics(true);
            onSnapped.Invoke(); if (Manager) Manager.NotifyPartSnapped(this);
            if (Application.isPlaying) GetVisual().Flash(this);
            return true;
        }
        public void CancelSnap()
        {
            if (snapRoutine != null) { StopCoroutine(snapRoutine); snapRoutine = null; }
            if (reservedSocket) reservedSocket.ReleaseReservation(this);
            reservedSocket = null;
            if (IsBusy || IsSelected) { State = AssemblyPartState.Loose; SetPhysics(false); }
            if (visual) visual.Clear();
        }
        public bool Unsnap(bool force = false)
        {
            if (!IsSnapped) return true;
            if (!force && Manager && !Manager.CanRemove(this, out string _)) return false;
            if (InstalledSocket && InstalledSocket.CurrentPart == this) InstalledSocket.DetachPart();
            InstalledSocket = null; transform.SetParent(null,true); State = AssemblyPartState.Loose; SetPhysics(false);
            onUnsnapped.Invoke(); if (Manager) Manager.NotifyPartUnsnapped(this); return true;
        }
        public void ResetLoose(Transform frame, Vector3 position, Quaternion rotation)
        {
            CancelSnap(); Unsnap(true); State = AssemblyPartState.Loose;
            transform.SetParent(null,true); transform.SetPositionAndRotation(frame.TransformPoint(position),frame.rotation * rotation);
            SetPhysics(true); // Tray stays staged until first pickup.
        }
        void SetPhysics(bool kinematic)
        {
            if (!Body) return;
            if (!Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
            Body.isKinematic = kinematic; Body.useGravity = !kinematic && useGravityWhenLoose;
            // Interpolating a kinematic child in world space fights its moving socket parent.
            Body.interpolation = kinematic ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        }
    }
}

