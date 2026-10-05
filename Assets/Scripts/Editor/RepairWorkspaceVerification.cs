using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class RepairWorkspaceVerification
    {
        static RepairWorkspaceVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch()
        {
            if(!Application.isBatchMode)throw new Exception("Use the isolated Unity project.");
            SessionState.SetBool("RepairWorkspace.Verify",true);EditorSceneManager.OpenScene(AssemblyWorkshopBuilder.ScenePath,OpenSceneMode.Single);EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("RepairWorkspace.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Repair and workspace checks").AddComponent<RepairWorkspaceVerificationRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("RepairWorkspace.Verify",false);EditorApplication.Exit(SessionState.GetBool("RepairWorkspace.Result",false)?0:1);}
        }
    }
    public sealed class RepairWorkspaceVerificationRunner:MonoBehaviour
    {
        [Serializable] class Report{public bool passed;public string[] checks;public string error;}
        readonly List<string> checks=new List<string>();AssemblyManager manager;PlayerAssemblyController player;AssemblyEasyGuide guide;
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";
            while(stack.Count>0)
            {
                bool more=false;object step=null;try{more=stack.Peek().MoveNext();if(more)step=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();break;}
                if(!more){stack.Pop();continue;}if(step is IEnumerator nested){stack.Push(nested);continue;}yield return step;
            }
            File.WriteAllText("Tools/Verification/repair-workspace-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,checks=checks.ToArray(),error=error},true));
            SessionState.SetBool("RepairWorkspace.Result",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Scenario()
        {
            yield return null;yield return null;yield return null;
            manager=AssemblyManager.Instance;var ui=manager.GetComponent<AssemblyWorkshopUI>();
            Assert(manager.Recipe.Validate().Count==0,"PC recipe remains valid with the runtime bonus chapter");
            var cardGrid=ui.GetComponentsInChildren<UnityEngine.UI.GridLayoutGroup>().Single(g=>g.constraintCount==3);
            var viewport=cardGrid.GetComponentInParent<UnityEngine.UI.ScrollRect>().viewport;Canvas.ForceUpdateCanvases();
            var cards=ui.GetComponentsInChildren<WorkshopCard>().Where(c=>c.name.StartsWith("Chapter card · ")).ToArray();
            Assert(cards.Length==10 && cards.Any(c=>c.name.EndsWith(PCRepairChapter.ChapterId)),"Nine assembly cards and a PC Repair bonus card are available");
            var corners=new Vector3[4];viewport.GetWorldCorners(corners);var visible=new Rect(corners[0],corners[2]-corners[0]);
            Assert(cards.Count(c=>visible.Contains(c.transform.position+new Vector3(50,-50,0)))>=6,"At least six chapter cards are visible together");
            Capture(ui.GetComponentInChildren<Canvas>(),Camera.main,"chapter-grid",1440,900);
            var bonus=cards.Single(c=>c.name.EndsWith(PCRepairChapter.ChapterId));
            ExecuteEvents.Execute<IPointerClickHandler>(bonus.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},(handler,data)=>handler.OnPointerClick((PointerEventData)data));
            yield return null;
            Assert(!ui.IsOpen && manager.RepairActive && Time.timeScale==1,"Clicking Repair starts gameplay directly from its card");
            player=FindAnyObjectByType<PlayerAssemblyController>();player.enabled=false;player.GetComponent<CharacterController>().enabled=false;guide=manager.GetComponent<AssemblyEasyGuide>();
            var repair=manager.Repair;
            Assert(manager.Recipe.parts.All(p=>manager.IsPlaced(p.id)),"Repair starts from the complete assembled engine");
            Assert(repair.Replacement && repair.Replacement!=manager.FindPart(PCRepairChapter.FaultyId) && repair.Replacement.GetComponent<MeshFilter>().sharedMesh==manager.FindPart(PCRepairChapter.FaultyId).GetComponent<MeshFilter>().sharedMesh,"Replacement is a separate physical object with the exact original bearing geometry");
            Assert(Vector3.Distance(repair.Replacement.transform.lossyScale,manager.FindPart(PCRepairChapter.FaultyId).transform.lossyScale)<.00001f,"Replacement preserves the original bearing scale");
            var affected=new HashSet<string>(repair.Steps.Select(s=>s.partId));
            var retained=manager.Recipe.parts.Where(p=>!affected.Contains(p.id)).ToDictionary(p=>p.id,p=>new Pose(manager.FindPart(p.id).transform.position,manager.FindPart(p.id).transform.rotation));
            foreach(var difficulty in new[]{SnapDifficulty.Easy,SnapDifficulty.Hard})
            {
                if(difficulty!=SnapDifficulty.Easy){manager.SelectChapter(manager.Recipe.chapters.FindIndex(c=>c.id==PCRepairChapter.ChapterId));yield return null;repair=manager.Repair;}
                manager.SetDifficulty(difficulty,false);
                foreach(var p in manager.AssemblyParts){var data=new SerializedObject(p);data.FindProperty("snapDuration").floatValue=0;data.ApplyModifiedPropertiesWithoutUndo();}
                int safety=0;
                while(!repair.Complete && safety++<20)
                {
                    var step=repair.CurrentStep;var part=repair.NextPart;int before=repair.CompletedSteps;
                    if(part.PartId==PCRepairChapter.ReplacementId)Assert(SourceClickable(part),"Replacement can be picked up through its visible CAD surface on the parts table");
                    Assert(!manager.CanPickUp(manager.FindPart("101_Cylinder_Block_01"),out _),"Unrelated engine components stay locked during repair");
                    if(step.install)
                    {
                        var wrong=part.PartId==PCRepairChapter.ReplacementId?manager.FindPart(PCRepairChapter.FaultyId):repair.Replacement;
                        Assert(!manager.CanInstall(wrong,part.TargetSocket,out _),"Only the correct repair part can enter the current socket");
                    }
                    player.PickUp(part);Assert(player.CurrentHeldPart==part,"Repair pickup: "+step.partId+" in "+difficulty);
                    if(step.install)
                    {
                        var ray=ClearRay(part);var pose=part.GetTargetPose(part.TargetSocket);part.MoveHeld(pose.position,pose.rotation);
                        Assert(guide.Place(ray,20,player.ViewCamera),"Visible repair installation: "+step.partId+" in "+difficulty);player.DropHeldPart(false);
                    }
                    else
                    {
                        yield return null;Assert(repair.CompletedSteps==before,"Removal waits until the held part is set aside");player.DropHeldPart(false);
                    }
                    yield return null;yield return null;
                    Assert(repair.CompletedSteps==before+1,"Repair advances exactly one operation: "+step.partId);
                    if(step.partId==PCRepairChapter.UnitId && !step.install)
                    {
                        var platform=GameObject.Find("Portable Work Platform").GetComponent<Renderer>().bounds;
                        Assert(Mathf.Abs(AssemblyHeldView.WorldBounds(part).min.y-platform.max.y-.03f)<.03f && part.Body.isKinematic,"Removed idler is staged safely on the small work platform");
                    }
                    if(step.partId==PCRepairChapter.ReplacementId)
                        Assert(manager.Members(manager.CarrierDefinition(PCRepairChapter.UnitId)).Contains(repair.Replacement),"Subassembly membership uses the replacement bearing occupying the original socket");
                }
                Assert(repair.Complete && manager.ChapterComplete && manager.ChapterInstalledCount==14,"All fourteen removal, replacement and reassembly operations complete in "+difficulty);
                Assert(!manager.FindPart(PCRepairChapter.FaultyId).gameObject.activeSelf && repair.Replacement.IsSnapped,"Faulty bearing is retired and the new bearing stays installed");
                Assert(manager.Recipe.parts.All(p=>manager.IsPlaced(p.id)) && retained.All(pair=>Vector3.Distance(pair.Value.position,manager.FindPart(pair.Key).transform.position)<.002f),"Reassembled engine retains every unaffected component and its CAD pose");
                Assert(manager.FindPart(PCRepairChapter.UnitId).GetComponentsInChildren<MeshRenderer>().Any(r=>r.GetComponentInParent<AssemblyPart>()==repair.Replacement),"Reinstalled idler physically contains the replacement bearing");
            }
            Capture(ui.GetComponentInChildren<Canvas>(),player.ViewCamera,"repair-complete",1440,900);
            manager.SelectChapter(0);yield return null;
            Assert(!manager.RepairActive && !manager.FindPart(PCRepairChapter.ReplacementId) && manager.FindPart(PCRepairChapter.FaultyId).gameObject.activeSelf==false,"Leaving repair cleans up the temporary replacement");
            guide.RefreshTargets();
            var queryTargets=(IEnumerable)typeof(AssemblyEasyGuide).GetField("targets",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(guide);
            Assert(queryTargets.Cast<object>().All(t=>t.GetType().GetField("part").GetValue(t) is AssemblyPart p && p && manager.AssemblyParts.Contains(p)),"No stale replacement geometry remains in ghost ray queries after leaving repair");
            manager.SetMode(WorkshopMode.Edit);manager.AddChapter("Authored chapter after assembly");
            Assert(manager.Recipe.chapters.Last().id==PCRepairChapter.ChapterId && manager.Recipe.chapters[manager.CurrentChapterIndex].title=="Authored chapter after assembly","Authored chapters are inserted before the runtime repair bonus");
            manager.Save("repair-workspace-verification");var exported=AssemblyRecipeStore.Load(Path.Combine(AssemblyRecipeStore.DirectoryPath,"repair-workspace-verification.json"));
            Assert(!exported.chapters.Any(c=>c.id==PCRepairChapter.ChapterId) && !exported.parts.Any(p=>p.id==PCRepairChapter.ReplacementId) && exported.parts.Count==325,"Exported JSON contains the original assembly recipe and authored chapter without runtime repair data");
            SceneManager.LoadScene("MobileAssembly");yield return null;yield return null;yield return null;yield return null;
            manager=AssemblyManager.Instance;var mobile=manager.GetComponent<MobileAssemblyController>();
            Assert(!manager.Repair && !manager.Recipe.chapters.Any(c=>c.id==PCRepairChapter.ChapterId),"Mobile keeps assembly chapters only");
            mobile.ChooseChapter(1);yield return null;yield return null;
            var groups=manager.Recipe.subAssemblies.Where(g=>g.rootPartId.StartsWith("230_")).OrderBy(g=>g.rootPartId).ToArray();
            Assert(groups.Select(g=>mobile.WorkspaceFor(g.id)).Distinct().Count()==3,"Each piston has a separate workspace transform");
            foreach(var group in groups)
            {
                Assert(mobile.ActiveWorkspaceId==group.id,"Mobile focuses the next piston workspace: "+group.id);
                Assert(mobile.VisiblePartIds.All(id=>manager.FindPart(id).Definition.subAssemblyId==group.id),"Only active-workspace part cards are shown");
                var others=groups.Where(g=>g.id!=group.id).ToDictionary(g=>g.id,g=>new Pose(mobile.WorkspaceFor(g.id).position,mobile.WorkspaceFor(g.id).rotation));
                mobile.RotateEngine(new Vector2(50,30));mobile.PanModel(new Vector2(45,25));mobile.ZoomModel(1.2f);
                Assert(others.All(pair=>Vector3.Distance(pair.Value.position,mobile.WorkspaceFor(pair.Key).position)<.001f && Quaternion.Angle(pair.Value.rotation,mobile.WorkspaceFor(pair.Key).rotation)<.01f),"Gestures do not move or rotate the other piston workspaces");
                foreach(var part in manager.Members(group).OrderBy(p=>p.Definition.order))part.InstallImmediate(part.TargetSocket);
                yield return null;yield return null;
                Assert(manager.GroupComplete(group),"Separate piston workspace completes: "+group.id);
                if(group!=groups.Last())Assert(manager.Members(group).SelectMany(p=>p.GetComponents<MeshRenderer>()).All(r=>!r.enabled),"Finished piston is hidden while the next workspace is active (active="+mobile.ActiveWorkspaceId+", next="+manager.NextHintPart()?.PartId+", visible="+string.Join(",",manager.Members(group).Where(p=>p.GetComponent<MeshRenderer>().enabled).Select(p=>p.PartId))+")");
            }
            Assert(mobile.ActiveWorkspaceId=="engine" && mobile.VisiblePartIds.All(id=>string.IsNullOrEmpty(manager.FindPart(id).Definition.subAssemblyId)),"After bench builds, mobile returns to engine installation cards");
        }
        Ray ClearRay(AssemblyPart part)
        {
            var pose=part.GetTargetPose(part.TargetSocket);var localCenter=part.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(part).center);var center=pose.position+pose.rotation*Vector3.Scale(localCenter,part.transform.lossyScale);
            var localBounds=new Bounds();bool first=true;
            foreach(var mesh in part.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh && manager.OwnsMesh(part,f)))
            {
                var matrix=part.transform.worldToLocalMatrix*mesh.transform.localToWorldMatrix;var b=mesh.sharedMesh.bounds;
                for(int corner=0;corner<8;corner++){var p=matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1)));if(first){localBounds=new Bounds(p,Vector3.zero);first=false;}else localBounds.Encapsulate(p);}
            }
            var extent=Vector3.Scale(localBounds.extents,part.transform.lossyScale)*.65f;
            for(int i=0;i<192;i++)
            {
                float angle=(i%32)*Mathf.PI/16;float height=i<64?1.5f:i<128?.3f:-1.5f;float radius=i%64<32?.7f:1.5f;
                var origin=center+new Vector3(Mathf.Cos(angle)*radius,height,Mathf.Sin(angle)*radius);var ray=new Ray(origin,center-origin);
                foreach(var offset in new[]{Vector3.zero,Vector3.right*extent.x,Vector3.left*extent.x,Vector3.up*extent.y,Vector3.down*extent.y,Vector3.forward*extent.z,Vector3.back*extent.z})
                {
                    ray=new Ray(origin,center+pose.rotation*offset-origin);
                    player.ViewCamera.transform.SetPositionAndRotation(origin,Quaternion.LookRotation(ray.direction));if(guide.Aim(ray,20,player.ViewCamera)==part)return ray;
                }
            }
            throw new Exception("No visible repair placement ray for "+part.PartId);
        }
        bool SourceClickable(AssemblyPart part)
        {
            var query=player.GetComponent<AssemblyPickupQuery>();var filter=part.GetComponent<MeshFilter>();var vertices=filter.sharedMesh.vertices;var triangles=filter.sharedMesh.triangles;
            int stride=Mathf.Max(3,(triangles.Length/32/3)*3);
            foreach(var direction in new[]{Vector3.up,Vector3.back,Vector3.right,Vector3.forward,Vector3.left})
                for(int index=0;index+2<triangles.Length;index+=stride)
                {
                    var point=filter.transform.TransformPoint((vertices[triangles[index]]+vertices[triangles[index+1]]+vertices[triangles[index+2]])/3);
                    if(query.Raycast(new Ray(point+direction*.8f,-direction),2,Physics.DefaultRaycastLayers,out var picked,out _,out _) && picked==part)return true;
                }
            return false;
        }
        void Capture(Canvas canvas,Camera camera,string name,int width,int height)
        {
            var mode=canvas.renderMode;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.5f;
            var target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;Canvas.ForceUpdateCanvases();
            WorkshopPresentationBuilder.Render(camera,"Tools/Verification/"+name+".png",width,height);camera.targetTexture=null;target.Release();Destroy(target);canvas.renderMode=mode;
        }
        void Assert(bool value,string text){if(!value)throw new Exception(text);checks.Add(text);}
    }
}
