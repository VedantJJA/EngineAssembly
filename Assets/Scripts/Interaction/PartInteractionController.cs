using UnityEngine;
namespace EngineAssembly
{
    // Controller-neutral adapter. XR grab events can call GrabPart / Move / ReleaseHeldPart.
    public class PartInteractionController : MonoBehaviour
    {
        AssemblyPart held;
        public void GrabPart(AssemblyPart part) { if(!held && part && part.BeginGrab())held=part; }
        public void Move(Vector3 position,Quaternion rotation) { if(held)held.MoveHeld(position,rotation); }
        public void ReleaseHeldPart() { if(held)held.Release();held=null; }
        void OnDisable()=>ReleaseHeldPart();
    }
}

