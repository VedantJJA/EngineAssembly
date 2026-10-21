using System;
using UnityEngine;

namespace EngineAssembly
{
    public enum ARFailure { UnsupportedDevice, UnsupportedPlatform, CameraDenied, Configuration, Initialization }
    public abstract class ARTrackingProvider : MonoBehaviour
    {
        public abstract Camera TrackingCamera { get; }
        public abstract bool CanRunHere { get; }
        public virtual bool RequiresCameraPermission=>true;
        public abstract bool HasSurface { get; }
        public abstract bool TrackingReliable { get; }
        public abstract void Begin();
        public abstract Transform PlaceAnchor();
        public abstract void BeginRepositioning();
        public abstract void Shutdown();
        public event Action Ready;
        public event Action<ARFailure> Failed;
        protected void ReportReady()=>Ready?.Invoke();
        protected void ReportFailure(ARFailure reason)=>Failed?.Invoke(reason);
    }
}
