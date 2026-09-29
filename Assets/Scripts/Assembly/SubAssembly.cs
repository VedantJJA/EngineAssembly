using UnityEngine;
namespace EngineAssembly
{
    // Optional organizational marker; chapter recipes now own ordering and socket parenting.
    public class SubAssembly : MonoBehaviour
    {
        [SerializeField] string subAssemblyName;
        public string SubAssemblyName=>string.IsNullOrEmpty(subAssemblyName)?name:subAssemblyName;
        public AssemblyPart[] ChildParts=>GetComponentsInChildren<AssemblyPart>(true);
    }
}

