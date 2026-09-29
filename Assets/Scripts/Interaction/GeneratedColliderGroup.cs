using UnityEngine;
namespace EngineAssembly
{
    // Identifies only colliders created by the batch tool, so user-authored children survive regeneration.
    public sealed class GeneratedColliderGroup : MonoBehaviour { public string backend; }
}

