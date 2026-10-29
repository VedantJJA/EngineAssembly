using System.Collections.Generic;
using UnityEngine;

namespace EngineAssembly
{
    /// <summary>Display motion uses rendering and exact ray queries, without CAD contact simulation.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(900)]
    public sealed class AssemblyDisplayPhysics : MonoBehaviour
    {
        readonly Dictionary<Collider,bool> saved=new Dictionary<Collider,bool>();
        AssemblyManager manager;
        bool suppressed;
        void Awake()=>manager=GetComponent<AssemblyManager>();
        void LateUpdate()=>Refresh();
        public void Refresh()
        {
            bool display=manager && manager.DisplayActive;
            if(!display){if(suppressed)Restore();return;}
            if(suppressed)return;
            foreach(var part in manager.AssemblyParts)
                if(part)foreach(var collider in part.GetComponentsInChildren<Collider>(true))
                {
                    if(collider.GetComponent<AssemblyPlayerCollisionProxy>() || saved.ContainsKey(collider))continue;
                    saved[collider]=collider.enabled;collider.enabled=false;
                }
            suppressed=true;
        }
        void Restore()
        {
            foreach(var pair in saved)if(pair.Key)pair.Key.enabled=pair.Value;
            saved.Clear();suppressed=false;
            if(manager)foreach(var part in manager.AssemblyParts)if(part)
                foreach(var precision in part.GetComponentsInChildren<AssemblyPrecisionSurface>(true))precision.RefreshPhysics();
        }
        void OnDisable(){if(suppressed)Restore();}
    }
}
