using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace EngineAssembly.Editor
{
    public static class AssemblyPreviewCapture
    {
        public static void EnsureMaterial()
        {
            const string path="Assets/Resources/AssemblyGhost.mat";
            var shader=Shader.Find("EngineAssembly/GhostHologramURP");
            if(!shader)throw new InvalidOperationException("Missing assembly preview shader.");
            var material=new Material(shader) { name="AssemblyGhost",enableInstancing=true };
            material.SetColor("_BaseColor",AssemblyVisual.Waiting);material.SetColor("_RimColor",new Color(.2f,.8f,1,.8f));
            AssetDatabase.CreateAsset(material,path);AssetDatabase.SaveAssets();
        }
        public static void CaptureBatch()
        {
            EnsureMaterial();
            var scene=EditorSceneManager.OpenScene(AssemblyWorkshopBuilder.ScenePath,OpenSceneMode.Single);
            var manager=UnityEngine.Object.FindAnyObjectByType<AssemblyManager>();
            manager.LoadRecipe(AssemblyRecipeStore.Load(AssemblyWorkshopBuilder.RecipePath));manager.SetMode(WorkshopMode.Edit);manager.SelectChapter(8);
            var camera=Camera.main;camera.transform.SetParent(null,true);
            camera.transform.position=new Vector3(4,3.6f,-5);camera.transform.LookAt(manager.AssemblyRoot.position);camera.fieldOfView=38;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.055f,.08f);
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            Directory.CreateDirectory("Tools/Verification");
            Save(camera,"Tools/Verification/workshop-preview.png");
            // A separate nearby ghost makes translucency, edge highlighting and occlusion reviewable.
            var part=manager.AssemblyParts.First(p=>p.PartId.StartsWith("201_Piston"));
            var socket=new GameObject("Preview only socket").AddComponent<AssemblySocket>();socket.Configure(part);
            socket.transform.position=manager.AssemblyRoot.position+Vector3.up*.35f+Vector3.left*1.1f;
            socket.transform.rotation=part.TargetSocket.SnapRotation;
            var visual=part.gameObject.AddComponent<AssemblyVisual>();visual.ShowGhost(part,socket,AssemblyVisual.Ready);
            Save(camera,"Tools/Verification/highlight-preview.png");
            var shader=Shader.Find("EngineAssembly/GhostHologramURP");
            var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
            File.WriteAllText("Tools/Verification/shader-report.json","{\"passed\":"+(errors.Length==0?"true":"false")+",\"errors\":"+errors.Length+"}");
            if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.message)));
        }
        static void Save(Camera camera,string path)
        {
            var target=new RenderTexture(1280,900,24,RenderTextureFormat.ARGB32);target.Create();
            var request=new UniversalRenderPipeline.SingleCameraRequest { destination=target };
            if(GraphicsSettings.currentRenderPipeline)RenderPipeline.SubmitRenderRequest(camera,request);
            else { camera.targetTexture=target;camera.Render();camera.targetTexture=null; }
            var previous=RenderTexture.active;RenderTexture.active=target;
            var texture=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(texture);target.Release();UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
