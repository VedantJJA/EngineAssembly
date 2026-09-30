using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EngineAssembly.Editor
{
    public static class WorkshopPresentationBuilder
    {
        [MenuItem("Tools/Engine Assembly/Prepare Workshop UI")]
        public static void Prepare()
        {
            if(!Resources.Load<TMP_Settings>("TMP Settings"))
            {
                AssetDatabase.importPackageCompleted+=AfterImport;
                TMP_PackageResourceImporter.ImportResources(true,false,false);return;
            }
            Font("Inter-Regular","WorkshopBody");Font("Inter-SemiBold","WorkshopHeading");
            AssetDatabase.SaveAssets();
        }
        static void AfterImport(string name) { AssetDatabase.importPackageCompleted-=AfterImport;EditorApplication.delayCall+=Prepare; }
        static void Font(string source,string name)
        {
            string path="Assets/Resources/"+name+".asset";
            if(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path))return;
            var font=TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/"+source+".ttf"));
            font.name=name;AssetDatabase.CreateAsset(font,path);
            AssetDatabase.AddObjectToAsset(font.material,font);
            foreach(var atlas in font.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,font);
            font.TryAddCharacters(string.Concat(Enumerable.Range(32,95).Select(i=>(char)i))+"→●·—°");
            EditorUtility.SetDirty(font);
        }
        public static void PrepareBatch()
        {
            Prepare();
            EditorApplication.update+=WaitForFonts;
        }
        static void WaitForFonts()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || !Resources.Load<TMP_FontAsset>("WorkshopHeading"))return;
            EditorApplication.update-=WaitForFonts;
            try { FinishBatch();EditorApplication.Exit(0); }catch(Exception ex) { Debug.LogException(ex);EditorApplication.Exit(1); }
        }
        static void FinishBatch()
        {
            // This explicit rebuild is restricted to the disposable verification project.
            if(!Application.dataPath.Replace('\\','/').EndsWith("Tools/Verification/UnityProject/Assets"))throw new InvalidOperationException("Verification project only.");
            if(File.Exists(AssemblyWorkshopBuilder.ScenePath))AssetDatabase.DeleteAsset(AssemblyWorkshopBuilder.ScenePath);
            if(File.Exists(AssemblyWorkshopBuilder.RecipePath))AssetDatabase.DeleteAsset(AssemblyWorkshopBuilder.RecipePath);
            AssemblyVerification.BuildAndVerify();
            AssemblyPreviewCapture.EnsureMaterial();
            CaptureArtwork();
        }
        public static void CaptureArtwork()
        {
            var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scene=EditorSceneManager.OpenScene(AssemblyWorkshopBuilder.ScenePath,OpenSceneMode.Additive);
            try
            {
                var manager=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<AssemblyManager>()).First();
                manager.LoadRecipe(AssemblyRecipeStore.Load(AssemblyWorkshopBuilder.RecipePath));manager.SetMode(WorkshopMode.Edit);
                var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.067f,.095f,.125f);camera.fieldOfView=36;camera.cullingMask=1<<30;
                Directory.CreateDirectory("Assets/UI/ChapterImages");
                var art=AssetDatabase.LoadAssetAtPath<AssemblyChapterArtwork>("Assets/Resources/AssemblyChapterArtwork.asset");
                if(!art) { art=ScriptableObject.CreateInstance<AssemblyChapterArtwork>();AssetDatabase.CreateAsset(art,"Assets/Resources/AssemblyChapterArtwork.asset"); }
                var contextMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));contextMaterial.color=new Color(.18f,.26f,.30f);contextMaterial.SetFloat("_Metallic",.3f);contextMaterial.SetFloat("_Smoothness",.35f);
                var chapterMaterial=new Material(contextMaterial);chapterMaterial.color=new Color(.32f,.66f,.61f);
                for(int i=0;i<manager.Recipe.chapters.Count;i++)
                {
                    manager.SelectChapter(i);
                    var meshes=manager.AssemblyParts.Where(p=>p.gameObject.activeInHierarchy).SelectMany(p=>p.GetComponentsInChildren<MeshRenderer>()).Distinct().ToArray();
                    var bounds=meshes[0].bounds;foreach(var r in meshes) { bounds.Encapsulate(r.bounds);r.gameObject.layer=30;var p=r.GetComponentInParent<AssemblyPart>();r.sharedMaterials=Enumerable.Repeat(p && p.ChapterId==manager.CurrentChapterId?chapterMaterial:contextMaterial,r.sharedMaterials.Length).ToArray(); }
                    var center=bounds.center;float distance=Mathf.Max(.3f,bounds.extents.magnitude)*3.5f;
                    camera.transform.position=center+new Vector3(1,.6f,-1).normalized*distance;camera.transform.LookAt(center);
                    string path="Assets/UI/ChapterImages/chapter-"+(i+1)+".png";Render(camera,path,640,320);
                    AssetDatabase.ImportAsset(path);
                    var entry=art.cards.Find(c=>c.recipeTitle==manager.Recipe.title && c.chapterId==manager.Recipe.chapters[i].id);
                    if(entry==null) { entry=new AssemblyChapterArtwork.Card{recipeTitle=manager.Recipe.title,chapterId=manager.Recipe.chapters[i].id};art.cards.Add(entry); }
                    entry.image=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();
                UnityEngine.Object.DestroyImmediate(contextMaterial);UnityEngine.Object.DestroyImmediate(chapterMaterial);
            }
            finally { EditorSceneManager.CloseScene(scene,true);if(previous.IsValid())UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous); }
        }
        public static void Render(Camera camera,string path,int width,int height)
        {
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);target.Create();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            var previous=RenderTexture.active;RenderTexture.active=target;
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());
            RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(texture);target.Release();UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
