using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EngineAssembly
{
    // Visual-only meshes: no cloned scripts, colliders, rigidbodies, or source material changes.
    public sealed class AssemblyVisual : MonoBehaviour
    {
        public static readonly Color Waiting = new Color(.2f,.65f,1,.22f), Ready = new Color(.15f,1,.55f,.32f), Blocked = new Color(1,.55f,.12f,.25f);
        GameObject visualRoot;
        AssemblyPart owner;
        AssemblySocket socket;
        readonly List<Renderer> renderers = new List<Renderer>();
        MaterialPropertyBlock block;
        float flashUntil;
        bool overlay;
        static Material material;
        public void ShowGhost(AssemblyPart part, AssemblySocket target, Color color)
        {
            if (!target) { Clear(); return; }
            if (!visualRoot || owner != part || overlay) Build(part,false);
            socket=target; flashUntil=0;
            if (part.Manager && !part.Manager.CanInstall(part,target,out _)) color=Blocked;
            Tint(color); UpdatePose();
        }
        public void Hover(AssemblyPart part, bool allowed)
        {
            if (!visualRoot || !overlay || owner!=part) Build(part,true);
            socket=null; Tint(allowed?new Color(.5f,.9f,1,.16f):Blocked);
        }
        public void Flash(AssemblyPart part)
        {
            if (part.Manager && part.Manager.IsPreparing) return;
            Build(part,true); Tint(Ready); flashUntil=Time.unscaledTime+.35f;
        }
        void Build(AssemblyPart part, bool isOverlay)
        {
            Clear(); owner=part; overlay=isOverlay;
            if (!material) material=Resources.Load<Material>("AssemblyGhost");
            if (!material) { Debug.LogError("Missing Resources/AssemblyGhost material.",this); return; }
            visualRoot=new GameObject(isOverlay?"Part Highlight":"Snap Preview") { hideFlags=HideFlags.DontSave, layer=2 };
            foreach (var filter in part.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh || filter.GetComponentInParent<AssemblyPart>()!=part || !filter.GetComponent<MeshRenderer>()) continue;
                var go=new GameObject("Visual") { hideFlags=HideFlags.DontSave, layer=2 };
                go.transform.SetParent(visualRoot.transform,false);
                Matrix4x4 local=part.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                go.transform.localPosition=local.GetColumn(3);go.transform.localRotation=local.rotation;go.transform.localScale=local.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                var renderer=go.AddComponent<MeshRenderer>();
                var materials=new Material[filter.sharedMesh.subMeshCount];
                for(int i=0;i<materials.Length;i++) materials[i]=material;
                renderer.sharedMaterials=materials;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderers.Add(renderer);
            }
            UpdatePose();
        }
        void Tint(Color color)
        {
            if(block==null) block=new MaterialPropertyBlock();
            block.SetColor("_BaseColor",color);block.SetColor("_RimColor",new Color(color.r,color.g,color.b,.8f));
            block.SetFloat("_SurfaceOffset",overlay?.0005f:0);
            foreach(var r in renderers) if(r) r.SetPropertyBlock(block);
        }
        void LateUpdate()
        {
            if(flashUntil>0 && Time.unscaledTime>=flashUntil) { Clear(); return; }
            UpdatePose();
        }
        void UpdatePose()
        {
            if(!visualRoot || !owner) return;
            var pose=socket?owner.GetTargetPose(socket):new Pose(owner.transform.position,owner.transform.rotation);
            visualRoot.transform.SetPositionAndRotation(pose.position,pose.rotation);
            visualRoot.transform.localScale=owner.transform.lossyScale;
        }
        public void Clear()
        {
            if(visualRoot) { visualRoot.SetActive(false); if(Application.isPlaying) Destroy(visualRoot);else DestroyImmediate(visualRoot); }
            visualRoot=null;renderers.Clear();socket=null;flashUntil=0;
        }
        void OnDisable()=>Clear();
        void OnDestroy()=>Clear();
    }
}

