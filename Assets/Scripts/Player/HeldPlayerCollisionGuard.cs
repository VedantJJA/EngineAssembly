using System.Collections.Generic;
using UnityEngine;
namespace EngineAssembly
{
    public sealed class HeldPlayerCollisionGuard : MonoBehaviour
    {
        struct Pair { public Collider player,item;public bool previous;public AssemblyPart part;public PickupablePlatform platform; }
        readonly List<Pair> pairs=new List<Pair>();
        public void Track(Transform item,AssemblyPart heldPart,PickupablePlatform heldPlatform)
        {
            foreach(var own in GetComponentsInChildren<Collider>())
                foreach(var other in item.GetComponentsInChildren<Collider>(true))
                {
                    if(own==other || !other.enabled || other.isTrigger)continue;
                    if(pairs.Exists(p=>p.player==own && p.item==other))continue;
                    pairs.Add(new Pair{player=own,item=other,previous=Physics.GetIgnoreCollision(own,other),part=heldPart,platform=heldPlatform});
                    Physics.IgnoreCollision(own,other,true);
                }
        }
        public void Refresh() { for(int i=pairs.Count-1;i>=0;i--){var pair=pairs[i];if(pair.part && (pair.part.IsSelected || pair.part.IsBusy) || pair.platform && pair.platform.IsHeld)continue;if(pair.player && pair.item)Physics.IgnoreCollision(pair.player,pair.item,pair.previous);pairs.RemoveAt(i);} }
        void Restore() { foreach(var pair in pairs)if(pair.player && pair.item)Physics.IgnoreCollision(pair.player,pair.item,pair.previous);pairs.Clear(); }
        void OnDisable()=>Restore();
    }
}
