using UnityEngine;

namespace EngineAssembly
{
    // Runtime menus notify the VR rig once, instead of searching every scene each frame.
    [DisallowMultipleComponent,RequireComponent(typeof(Canvas))]
    public sealed class WorkshopCanvasRegistration:MonoBehaviour
    {
        public static int Revision { get; private set; }
        void OnEnable()=>Revision++;
        void OnDisable()=>Revision++;
    }
}
