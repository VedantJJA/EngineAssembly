using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EngineAssembly.Editor
{
    [InitializeOnLoad]
    public static class AssemblyVerification
    {
        const string RequestPath="Tools/Verification/request.txt";
        static double nextCheck;
        static AssemblyVerification() { EditorApplication.update+=Poll; }
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextCheck)return;nextCheck=EditorApplication.timeSinceStartup+2;
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath))return;
            string request=File.ReadAllText(RequestPath).Trim();File.Delete(RequestPath);
            if(request=="verify")Run();
            else if(request=="build-and-verify") { try { AssemblyWorkshopBuilder.Create();Run(); }catch(Exception ex) { Write(false,new List<string>(),ex.ToString()); } }
        }
        [MenuItem("Tools/Engine Assembly/Run Verification")]
        public static void Run()
        {
            var checks=new List<string>();var old=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var manager=new GameObject("Test Manager").AddComponent<AssemblyManager>();
                var root=new GameObject("Test workpiece").transform;
                var parts=new List<AssemblyPart>();
                foreach(string id in new[]{"block","bolt-a","bolt-b","cover"})
                {
                    var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=id;var p=go.AddComponent<AssemblyPart>();p.SetIdentity(id,id);parts.Add(p);
                }
                manager.Configure(root,parts);
                var recipe=new AssemblyRecipe { chapters=new List<ChapterDefinition>{new ChapterDefinition{id="one",title="First"},new ChapterDefinition{id="two",title="Second"}},parts=new List<PartDefinition>{
                    new PartDefinition{id="block",displayName="Block",chapterId="one",order=1},
                    new PartDefinition{id="bolt-a",displayName="Bolt A",chapterId="one",order=2,geometryGroup="bolt",targetPosition=Vector3.right,orientation=AssemblyOrientation.UpsideDown},
                    new PartDefinition{id="bolt-b",displayName="Bolt B",chapterId="one",order=2,geometryGroup="bolt",targetPosition=Vector3.left,orientation=AssemblyOrientation.UpsideDown},
                    new PartDefinition{id="cover",displayName="Cover",chapterId="two",order=1,prerequisites=new List<string>{"bolt-a","bolt-b"}}
                }};
                Assert(recipe.Validate().Count==0,"Valid recipe accepted",checks);
                Assert(AssemblyRecipeStore.Parse(JsonUtility.ToJson(recipe)).parts.Count==4,"JSON round trip",checks);
                var invalid=AssemblyRecipeStore.Parse(JsonUtility.ToJson(recipe));invalid.parts[1].id="block";
                Assert(invalid.Validate().Count>0,"Duplicate IDs rejected",checks);
                invalid=AssemblyRecipeStore.Parse(JsonUtility.ToJson(recipe));invalid.parts[0].prerequisites.Add("bolt-a");invalid.parts[1].prerequisites.Add("block");
                Assert(invalid.Validate().Any(e=>e.Contains("cyclic")),"Dependency cycles rejected",checks);
                invalid=AssemblyRecipeStore.Parse(JsonUtility.ToJson(recipe));invalid.parts[0].snapDistance=float.NaN;
                Assert(invalid.Validate().Count>0,"Non-finite snap settings rejected",checks);
                manager.LoadRecipe(recipe);
                var block=parts[0];var a=parts[1];var b=parts[2];var cover=parts[3];
                Assert(!manager.ContinueChapter(),"Incomplete chapter cannot continue",checks);
                Assert(!a.TargetSocket.CanAcceptPart(block),"Unrelated part rejected by socket",checks);
                Assert(!manager.CanInstall(a,a.TargetSocket,out _),"Earlier assembly step enforced",checks);
                Assert(block.InstallImmediate(block.TargetSocket),"Base installs",checks);
                Assert(!manager.CanInstall(a,a.TargetSocket,out _),"Upside-down requirement enforced",checks);
                root.rotation=Quaternion.Euler(180,0,0);
                Assert(manager.CanInstall(a,a.TargetSocket,out _) && manager.CanInstall(b,b.TargetSocket,out _),"Equal-priority peers eligible after flip",checks);
                Assert(Vector3.Distance(a.TargetSocket.SnapPosition,root.TransformPoint(Vector3.right))<.001f,"Socket tracks moving/flipped workpiece",checks);
                Assert(a.TargetSocket.Reserve(a) && !a.TargetSocket.Reserve(b),"Socket reservation prevents double occupancy",checks);
                a.TargetSocket.ReleaseReservation(a);
                Assert(a.InstallImmediate(b.TargetSocket) && b.InstallImmediate(a.TargetSocket),"Explicitly interchangeable parts can exchange sockets",checks);
                Assert(manager.ChapterComplete,"Completion counts occupied target slots",checks);
                Assert(manager.ContinueChapter() && manager.CurrentChapterIndex==1,"Completed chapter continues",checks);
                Assert(manager.IsPlaced("block") && manager.IsPlaced("bolt-a") && manager.CanInstall(cover,cover.TargetSocket,out _),"Prior chapter prepared for standalone chapter selection",checks);
                manager.SetMode(WorkshopMode.Edit);
                Assert(cover.IsSnapped,"Edit mode previews assembled chapter",checks);
                manager.SetMode(WorkshopMode.Assembly);
                Assert(!cover.IsSnapped,"Returning to Assembly stages loose chapter parts",checks);
                Assert(AssemblyPart.RotationError(Quaternion.AngleAxis(180,Vector3.up),Quaternion.identity,SnapRotationMode.Exact,2)<.01f,"Rotational symmetry accepted",checks);
                Assert(AssemblyPart.RotationError(Quaternion.AngleAxis(63,Vector3.up),Quaternion.identity,SnapRotationMode.Axial,1)<.01f,"Axial rotation ignores spin",checks);
                Assert(AssemblyPart.RotationError(Quaternion.AngleAxis(180,Vector3.right),Quaternion.identity,SnapRotationMode.Axial,1)>179,"Axial rotation rejects reversed axis",checks);
                var shader=Shader.Find("EngineAssembly/GhostHologramURP");
                Assert(shader && !ShaderUtil.ShaderHasError(shader),"URP preview shader imports without errors",checks);
                Assert(Resources.Load<Material>("AssemblyGhost")!=null,"Preview material available to player builds",checks);
                string saved=AssemblyRecipeStore.Save(recipe,"verification-"+Guid.NewGuid().ToString("N"));
                Assert(AssemblyRecipeStore.Load(saved).parts.Count==4,"Recipe disk save/load",checks);File.Delete(saved);
                if(File.Exists(AssemblyWorkshopBuilder.RecipePath))Assert(AssemblyRecipeStore.Load(AssemblyWorkshopBuilder.RecipePath).parts.Count==318,"Three-cylinder recipe contains 318 parts",checks);
                Write(true,checks,"");
            }
            catch(Exception ex) { Write(false,checks,ex.ToString());Debug.LogException(ex); }
            finally { if(old.IsValid() && old.isLoaded)SceneManager.SetActiveScene(old);EditorSceneManager.CloseScene(scene,true); }
        }
        public static void BuildAndVerify()
        {
            if(Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),"Assets/Scenes/VerificationBootstrap.unity");
            AssemblyWorkshopBuilder.Create();Run();
        }
        static void Assert(bool condition,string label,List<string> checks) { if(!condition)throw new Exception("FAILED: "+label);checks.Add(label); }
        [Serializable] class Report { public bool passed;public string utc;public string[] checks;public string error; }
        static void Write(bool passed,List<string> checks,string error)
        {
            Directory.CreateDirectory("Tools/Verification");File.WriteAllText("Tools/Verification/unity-report.json",JsonUtility.ToJson(new Report{passed=passed,utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray(),error=error},true));
            Debug.Log("Assembly verification: "+(passed?"PASS":"FAIL")+" ("+checks.Count+" checks)");
        }
    }
}
