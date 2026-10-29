using System.Collections.Generic;
using UnityEngine;

namespace EngineAssembly
{
    /// <summary>Cheap player blocking, independent of the CAD surfaces used to fit parts.</summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyPlayerCollision : MonoBehaviour
    {
        public const int PlayerLayer=8;
        const int PlayerMask=1<<PlayerLayer;
        readonly HashSet<AssemblyPart> configured=new HashSet<AssemblyPart>();
        CharacterController controller;
        float nextScan;

        public static AssemblyPlayerCollision Attach(CharacterController body)
        {
            if(!body)return null;
            var collision=body.GetComponent<AssemblyPlayerCollision>();
            if(!collision)collision=body.gameObject.AddComponent<AssemblyPlayerCollision>();
            collision.controller=body;body.gameObject.layer=PlayerLayer;
            collision.RefreshParts();return collision;
        }
        public static bool CanContact(CharacterController body,Collider other)
        {
            if(!body || !other)return false;
            return ((int)other.excludeLayers&(1<<body.gameObject.layer))==0 && ((int)body.excludeLayers&(1<<other.gameObject.layer))==0;
        }
        void Update()
        {
            if(!controller || !controller.enabled || Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+.5f;RefreshParts();
        }
        public void RefreshParts()
        {
            var manager=AssemblyManager.Instance;
            if(manager && !manager.GetComponent<AssemblyDisplayPhysics>())manager.gameObject.AddComponent<AssemblyDisplayPhysics>();
            foreach(var part in AssemblyPart.AllParts)
            {
                if(!part || !configured.Add(part))continue;
                // Query meshes live in separate PhysicsScenes and do not appear in
                // this hierarchy. CAD layer/render settings stay unchanged.
                foreach(var detail in part.GetComponentsInChildren<Collider>(true))
                {
                    if(detail.GetComponent<AssemblyPlayerCollisionProxy>() || detail.GetComponentInParent<AssemblyPart>()!=part)continue;
                    if(detail is BoxCollider && detail.transform==part.transform && part.GetComponent<SubAssembly>())continue;
                    detail.excludeLayers=(int)detail.excludeLayers|PlayerMask;
                }
                var child=part.transform.Find("Player collision proxy");
                var proxy=child?child.GetComponent<AssemblyPlayerCollisionProxy>():null;
                if(!proxy)
                {
                    var go=new GameObject("Player collision proxy");go.layer=2;
                    go.transform.SetParent(part.transform,false);
                    proxy=go.AddComponent<AssemblyPlayerCollisionProxy>();
                }
                proxy.Configure(part);
            }
        }
    }

    /// <summary>One primitive per substantial physical part; never used by selection rays.</summary>
    public sealed class AssemblyPlayerCollisionProxy : MonoBehaviour
    {
        AssemblyPart part;
        BoxCollider box;
        MeshRenderer[] renderers;
        bool largeEnough;
        public BoxCollider Collider=>box;
        public AssemblyPart Part=>part;
        public void Configure(AssemblyPart owner)
        {
            part=owner;box=GetComponent<BoxCollider>();if(!box)box=gameObject.AddComponent<BoxCollider>();
            box.layerOverridePriority=100;
            box.includeLayers=1<<AssemblyPlayerCollision.PlayerLayer;
            box.excludeLayers=~(1<<AssemblyPlayerCollision.PlayerLayer);
            var sources=new List<MeshRenderer>();bool first=true;Bounds local=default;
            foreach(var filter in part.GetComponentsInChildren<MeshFilter>(true))
            {
                // A carrier's member parts each supply their own primitive. A
                // detached carrier already has its safe handling body.
                if(!filter.sharedMesh || filter.GetComponentInParent<AssemblyPart>()!=part || filter.GetComponentInParent<GeneratedColliderGroup>())continue;
                var renderer=filter.GetComponent<MeshRenderer>();if(renderer)sources.Add(renderer);
                var bounds=filter.sharedMesh.bounds;var matrix=transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int i=0;i<8;i++)
                {
                    var point=matrix.MultiplyPoint3x4(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if(first){local=new Bounds(point,Vector3.zero);first=false;}else local.Encapsulate(point);
                }
            }
            renderers=sources.ToArray();box.center=local.center;box.size=Vector3.Max(local.size,Vector3.one*.001f);
            var worldSize=Vector3.Scale(local.size,transform.lossyScale);
            largeEnough=!first && Mathf.Max(worldSize.x,worldSize.y,worldSize.z)>=.075f;
            Refresh();
        }
        void LateUpdate()=>Refresh();
        void Refresh()
        {
            if(!box || !part)return;
            bool visible=false;
            if(largeEnough && !part.IsSelected && !part.IsBusy && part.gameObject.activeInHierarchy && part.Body && part.Body.detectCollisions)
                foreach(var renderer in renderers)if(renderer && renderer.enabled && renderer.gameObject.activeInHierarchy){visible=true;break;}
            if(box.enabled!=visible)box.enabled=visible;
        }
    }
}
