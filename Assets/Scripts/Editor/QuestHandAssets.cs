using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Hands;

namespace EngineAssembly.Editor
{
    public static class QuestHandAssets
    {
        public static void RunBatch(){try{Prepare();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        [MenuItem("Tools/Engine Assembly/VR/Prepare visible hands")]
        public static void Prepare()
        {
            var package=UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.unity.xr.hands");if(package==null)throw new Exception("Install XR Hands first.");
            var source=Path.Combine(package.resolvedPath,"Samples~/HandVisualizer");const string art="Assets/Art/VRHands";Directory.CreateDirectory(art+"/Models");Directory.CreateDirectory(art+"/SourcePrefabs");Directory.CreateDirectory("Assets/Resources/VRHands");
            foreach(var hand in new[]{"Left","Right"})
            {
                foreach(var extension in new[]{".fbx",".fbx.meta"})File.Copy(Path.Combine(source,"Models",hand+"Hand"+extension),art+"/Models/"+hand+"Hand"+extension,true);
                foreach(var extension in new[]{".prefab",".prefab.meta"})File.Copy(Path.Combine(source,"Prefabs",hand+" Hand Tracking"+extension),art+"/SourcePrefabs/"+hand+" Hand Tracking"+extension,true);
            }
            File.Copy(Path.Combine(package.resolvedPath,"LICENSE.md"),art+"/Unity-XR-Hands-LICENSE.md",true);AssetDatabase.Refresh();
            const string materialPath="Assets/Resources/VRHands/WorkshopGloves.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,materialPath);}
            material.SetColor("_BaseColor",new Color(.34f,.46f,.62f));material.SetFloat("_Smoothness",.24f);material.SetFloat("_Metallic",0);EditorUtility.SetDirty(material);
            foreach(var hand in new[]{"Left","Right"})
            {
                var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(art+"/SourcePrefabs/"+hand+" Hand Tracking.prefab"));
                model.name=hand+" workshop hand";var driver=model.GetComponent<XRHandSkeletonDriver>();var wrist=driver.rootTransform;var joints=driver.jointTransformReferences.ToList();
                foreach(var component in model.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(component);
                var visual=model.AddComponent<QuestHandVisual>();visual.Wrist=wrist;visual.Joints=joints;
                foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)){r.enabled=true;r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.updateWhenOffscreen=false;}
                foreach(var c in model.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
                PrefabUtility.SaveAsPrefabAsset(model,"Assets/Resources/VRHands/"+hand+"Hand.prefab");UnityEngine.Object.DestroyImmediate(model);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
