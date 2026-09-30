using UnityEngine;
namespace EngineAssembly
{
    // Runtime carrier: the original, individually serviceable parts stay intact as children.
    public class SubAssembly : MonoBehaviour
    {
        [SerializeField] string subAssemblyName;
        public string SubAssemblyName=>string.IsNullOrEmpty(subAssemblyName)?name:subAssemblyName;
        public AssemblyPart[] ChildParts=>GetComponentsInChildren<AssemblyPart>(true);
        public AssemblyPart Carrier=>GetComponent<AssemblyPart>();
        public bool Ready=>Carrier && Carrier.Manager && Carrier.Manager.GroupComplete(Carrier.Manager.CarrierDefinition(Carrier.PartId));
    }
}

