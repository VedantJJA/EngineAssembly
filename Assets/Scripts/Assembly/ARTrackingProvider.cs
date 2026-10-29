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
        public virtual string TrackingGuidance=>"Point toward a textured, well-lit floor and move the phone slowly sideways.";
        public virtual Vector3? SurfacePosition=>null;
        public abstract void Begin();
        public abstract Transform PlaceAnchor();
        public abstract void BeginRepositioning();
        public virtual void RestartSurfaceSearch()=>BeginRepositioning();
        public abstract void Shutdown();
        public event Action Ready;
        public event Action<ARFailure> Failed;
        protected void ReportReady()=>Ready?.Invoke();
        protected void ReportFailure(ARFailure reason)=>Failed?.Invoke(reason);
    }
}
