using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EngineAssembly.Editor
{
    [InitializeOnLoad]
    public static class QuestGripPlacementVerification
    {
        static QuestGripPlacementVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch()
        {
            SessionState.SetBool("QuestGrip.Verify",true);
            EditorSceneManager.OpenScene("Assets/Scenes/AssemblyWorkshop.unity");
            EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("QuestGrip.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Quest grip placement verification").AddComponent<QuestGripPlacementVerificationRunner>();
            if(state==PlayModeStateChange.EnteredEditMode)
            {SessionState.SetBool("QuestGrip.Verify",false);EditorApplication.Exit(SessionState.GetBool("QuestGrip.Passed",false)?0:1);}
        }
    }

    public sealed class QuestGripPlacementVerificationRunner:MonoBehaviour
    {
        [Serializable] sealed class Report{public bool passed;public string error;public string[] checks;}
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        Camera viewer;RenderTexture target;QuestGripPlacement placement;
        void OnEnable()=>Application.logMessageReceived+=Log;
        void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string stack,LogType type)
        {if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors.Add(message+"\n"+stack);}
        void Check(bool value,string text){if(!value)throw new Exception(text);checks.Add(text);}
        IEnumerator Start()
        {
            var work=new Stack<IEnumerator>();work.Push(Scenario());string error="";
            while(work.Count>0)
            {
                bool more=false;object next=null;
                try{more=work.Peek().MoveNext();if(more)next=work.Peek().Current;}catch(Exception ex){error=ex.ToString();break;}
                if(!more){work.Pop();continue;}if(next is IEnumerator nested)work.Push(nested);else yield return next;
            }
            placement?.Cancel();
            if(errors.Count>0)error+="\nRuntime errors: "+string.Join("\n",errors);
            if(viewer)Destroy(viewer.gameObject);if(target){target.Release();Destroy(target);}
            Directory.CreateDirectory("Tools/Verification");
            File.WriteAllText("Tools/Verification/quest-grip-placement-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,error=error,checks=checks.ToArray()},true));
            SessionState.SetBool("QuestGrip.Passed",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Scenario()
        {
            for(int i=0;i<6;i++)yield return null;
            var manager=AssemblyManager.Instance;
            Check(manager && manager.Recipe!=null,"Workshop recipe is ready for physical grip tests");
            if(WorkshopTutorial.IsOpen)WorkshopTutorial.Active.Close();
            var player=FindAnyObjectByType<PlayerAssemblyController>();if(player){player.DropHeldPart(false);player.enabled=false;}
            manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);
            var narrator=manager.GetComponent<AssemblyNarrator>();if(narrator){narrator.enabled=false;if(narrator.Source)narrator.Source.Stop();}
            manager.SetDifficulty(SnapDifficulty.Easy,false);manager.SetInteraction(WorkshopInteraction.Build);manager.SelectChapter(0);
            for(int i=0;i<3;i++)yield return null;
            var first=manager.NextHintPart();var guide=manager.GetComponent<AssemblyEasyGuide>();
            Check(first && first.TargetSocket && manager.CanInstall(first,first.TargetSocket,out _),"First real workshop component is eligible for installation");
            viewer=new GameObject("Exact visibility verification camera").AddComponent<Camera>();viewer.enabled=false;viewer.nearClipPlane=.01f;viewer.farClipPlane=30;viewer.fieldOfView=60;
            target=new RenderTexture(768,512,16);target.Create();viewer.targetTexture=target;
            var scale=first.transform.lossyScale;int installed=0;first.onSnapped.AddListener(()=>installed++);
            Check(first.BeginGrab(),"VR test grabs the same actual component used in the chapter");
            var contact=first.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(first).center);
            var seat=first.GetTargetPose(first.TargetSocket);var carry=new Pose(seat.position+Vector3.right*.025f,seat.rotation);
            first.MoveHeld(carry.position,carry.rotation);
            Check(FindVisibleCamera(first,contact,guide),"A camera can see the exact target CAD surface");
            placement=new QuestGripPlacement();int progress=manager.ChapterInstalledCount;
            Check(placement.Update(first,carry,contact,viewer),"A near grip previews the component seated in its ghost");
            Check(first.IsSelected && !first.IsSnapped && !first.TargetSocket.IsOccupied,"Preview stays Held and never reserves or occupies its socket");
            Check(installed==0 && manager.ChapterInstalledCount==progress,"Preview sends no snap event and advances no recipe progress");
            Check(Vector3.Distance(first.transform.position,seat.position)<.00001f && Quaternion.Angle(first.transform.rotation,seat.rotation)<.01f,"Preview uses the exact seated position and rotation");
            Check(SameScale(first.transform.lossyScale,scale),"Preview preserves source part scale");
            var physicalContact=carry.position+carry.rotation*Vector3.Scale(contact,scale);
            var turnedRotation=Quaternion.AngleAxis(65,Vector3.up)*carry.rotation;
            var turnedCarry=new Pose(physicalContact-turnedRotation*Vector3.Scale(contact,scale),turnedRotation);
            Check(placement.Update(first,turnedCarry,contact,viewer),"Turning a controller around its fixed grip contact keeps the Easy preview seated");
            Check(first.IsSelected && !first.IsSnapped && installed==0,"Controller rotation while previewing never commits or shifts recipe state");
            var far=new Pose(carry.position+viewer.transform.right*.65f,carry.rotation);
            Check(!placement.Update(first,far,contact,viewer) && !placement.IsPreviewing,"Moving the physical grip away removes the seated preview");
            Check(first.IsSelected && !first.IsSnapped && Vector3.Distance(first.transform.position,far.position)<.00001f,"Moving away returns the same uninstalled object to the physical carry pose");
            Check(installed==0 && manager.ChapterInstalledCount==progress,"Leaving a preview never installs the component");
            first.MoveHeld(carry.position,carry.rotation);
            Check(placement.Update(first,carry,contact,viewer),"The grip can re-enter the target after moving away");
            placement.Cancel();
            Check(first.IsSelected && !first.IsSnapped && !placement.IsPreviewing && Vector3.Distance(first.transform.position,carry.position)<.00001f,"Tracking-loss style cancellation restores the held pose without installing");
            Check(!placement.CommitOnRelease() && installed==0,"Grip release after tracking cancellation cannot commit an old preview");
            Check(SameScale(first.transform.lossyScale,scale),"Cancellation preserves source part scale");
            first.MoveHeld(carry.position,carry.rotation);
            Check(placement.Update(first,carry,contact,viewer),"A reacquired controller pose can preview the target again");
            Check(!placement.Release(false) && !placement.IsPreviewing && first.IsSelected && !first.IsSnapped && installed==0,"Tracking or pause resume without a fresh grip release cancels instead of committing the preview");
            Check(Vector3.Distance(first.transform.position,carry.position)<.00001f && manager.ChapterInstalledCount==progress,"Unconfirmed release restores the carry pose and preserves chapter progress");

            // Block the entire target with an ordinary world obstacle. The helper
            // must use the actual guide's CAD and world line-of-sight queries.
            var center=TargetCenter(first,contact);var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Grip visibility blocker";
            wall.transform.SetPositionAndRotation(Vector3.Lerp(viewer.transform.position,center,.45f),Quaternion.LookRotation(center-viewer.transform.position));wall.transform.localScale=new Vector3(10,10,.06f);Physics.SyncTransforms();
            Check(!guide.VisibleFrom(first,viewer),"An opaque wall hides the exact target surface");
            Check(!placement.Update(first,carry,contact,viewer) && !placement.IsPreviewing && !first.IsSnapped,"Proximity cannot preview or install through an opaque wall");
            Check(!placement.CommitOnRelease() && installed==0,"Release behind a wall leaves the part uninstalled");
            wall.SetActive(false);Destroy(wall);Physics.SyncTransforms();
            Check(guide.VisibleFrom(first,viewer),"Target visibility returns after removing the obstacle");

            var prerequisite=manager.Recipe.parts.First(p=>p.chapterId==first.ChapterId && p.id!=first.PartId && !manager.IsPlaced(p.id)).id;
            first.Definition.prerequisites.Add(prerequisite);
            try
            {
                Check(!manager.CanInstall(first,first.TargetSocket,out _),"A missing real chapter prerequisite locks installation");
                Check(!placement.Update(first,carry,contact,viewer) && !placement.IsPreviewing,"Proximity preview respects the locked assembly order");
                Check(!placement.CommitOnRelease() && !first.IsSnapped && installed==0,"Release never bypasses assembly ordering");
            }
            finally{first.Definition.prerequisites.Remove(prerequisite);}
            first.MoveHeld(carry.position,carry.rotation);
            Check(placement.Update(first,carry,contact,viewer),"Restoring the prerequisite state allows seating again");
            Check(placement.CommitOnRelease(),"Releasing grip while visibly seated commits installation");
            Check(first.IsSnapped && !first.IsSelected && first.TargetSocket.CurrentPart==first && !placement.IsPreviewing,"Grip release ends Held state and owns exactly one target socket");
            Check(installed==1 && manager.ChapterInstalledCount==progress+1,"Grip release raises one install event and one progress increment");
            Check(!placement.CommitOnRelease() && installed==1,"Repeated release cannot duplicate an installation");
            Check(SameScale(first.transform.lossyScale,scale),"Installed part retains its source scale (before "+scale.ToString("G9")+", after "+first.transform.lossyScale.ToString("G9")+")");

            manager.SelectChapter(0);for(int i=0;i<2;i++)yield return null;
            first=manager.NextHintPart();Check(first.BeginGrab(),"Pointer placement still starts with a real held component");
            contact=first.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(first).center);seat=first.GetTargetPose(first.TargetSocket);
            first.MoveHeld(seat.position+Vector3.right*.65f,seat.rotation);
            Check(FindVisibleCamera(first,contact,guide),"Legacy trigger placement has a visible target");
            Check(FindVisibleRay(first,contact,guide,out var ray),"Controller ray can aim at the exact ghost surface");
            Check(guide.Aim(ray,viewer.farClipPlane,viewer)==first && guide.Place(ray,viewer.farClipPlane,viewer),"Pointing and pressing trigger still installs an Easy-mode held component");
            Check(first.IsSnapped,"Legacy trigger placement commits the same normal installation state");

            // A real Quest rig leaves the walking player's camera disabled in the
            // scene. Medium/Hard release must validate the headset's view instead.
            QuestVRSession.Instance.EnableVerification();var rig=FindAnyObjectByType<QuestVRRig>();rig.enabled=false;
            Check(rig && rig.HeadCamera && player && !player.enabled && !player.ViewCamera.enabled,"Quest verification keeps a disabled PC camera alongside its active headset camera");
            foreach(var difficulty in new[]{SnapDifficulty.Medium,SnapDifficulty.Hard})
            {
                manager.SetDifficulty(difficulty,false);manager.SelectChapter(0);for(int i=0;i<2;i++)yield return null;
                first=manager.NextHintPart();Check(first.BeginGrab(),difficulty+" allows the next real component to be gripped");
                contact=first.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(first).center);seat=first.GetTargetPose(first.TargetSocket);scale=first.transform.lossyScale;
                carry=new Pose(seat.position+Vector3.right*.025f,seat.rotation);first.MoveHeld(carry.position,carry.rotation);
                Check(FindVisibleCamera(first,contact,guide),difficulty+" has an exact visible target from the headset position");
                rig.HeadCamera.transform.SetPositionAndRotation(viewer.transform.position,viewer.transform.rotation);
                var hidden=TargetCenter(first,contact);player.ViewCamera.transform.position=hidden+Vector3.down*20;player.ViewCamera.transform.LookAt(hidden);Physics.SyncTransforms();
                Check(!guide.VisibleFrom(first,player.ViewCamera) && guide.VisibleFrom(first,rig.HeadCamera),difficulty+" headset sees the target while the disabled PC camera is blocked by the garage floor");
                var wrongRotation=Quaternion.AngleAxis(100,Vector3.up)*seat.rotation;
                var handPoint=carry.position+carry.rotation*Vector3.Scale(contact,scale);
                var unaligned=new Pose(handPoint-wrongRotation*Vector3.Scale(contact,scale),wrongRotation);first.MoveHeld(unaligned.position,unaligned.rotation);
                bool wrongPreview=placement.Update(first,unaligned,contact,viewer);
                Check(wrongPreview==(difficulty==SnapDifficulty.Medium),difficulty+" retains its intended orientation rule while using physical grip distance");
                placement.Cancel();first.MoveHeld(carry.position,carry.rotation);
                Check(placement.Update(first,carry,contact,viewer) && first.IsSelected && !first.IsSnapped,difficulty+" previews an aligned part without installing it");
                Check(placement.CommitOnRelease() && first.IsSnapped,difficulty+" release commits using headset line of sight instead of the disabled PC camera");
                Check(SameScale(first.transform.lossyScale,scale),difficulty+" release preserves imported part scale");
            }
            manager.SetDifficulty(SnapDifficulty.Easy,false);

            var display=manager.GetComponent<PCDisplayChapter>();display.EnsureChapter();manager.SelectChapter(manager.Recipe.chapters.FindIndex(c=>c.id==PCDisplayChapter.ChapterId));
            for(int i=0;i<2;i++)yield return null;
            var meshes=manager.AssemblyRoot.GetComponentsInChildren<MeshRenderer>(true).Where(r=>manager.Recipe.parts.Any(p=>p.id==r.name)).ToArray();
            var materials=meshes.ToDictionary(r=>r,r=>r.sharedMaterials);int sourceMaterialCount=materials.Values.SelectMany(a=>a).Where(m=>m).Distinct().Count();
            display.SetMotionMode(true);var operation=display.Operation;operation.Paused=true;operation.SectionEnabled=true;operation.SetCrankAngle(0);
            var section=manager.GetComponent<EngineSectionView>();
            Check(operation.Active && operation.MovingValveCount==12,"Real display motion starts with all twelve valve mechanisms");
            Check(section.SharedSectionMaterialCount==0 && section.CachedRendererCount>0 && materials.All(pair=>pair.Key.sharedMaterials.SequenceEqual(pair.Value)),"Motion uses original shared materials without fixed-cutaway material variants");
            int sectionUpdates=operation.SectionRefreshCount;
            for(int i=0;i<30;i++)operation.SetCrankAngle(i*3);
            Check(operation.SectionRefreshCount==sectionUpdates,"Crank motion does not rebuild an unchanged section plane or material state");
            operation.SectionEnabled=false;operation.SetCrankAngle(90);
            Check(section.SharedSectionMaterialCount==0 && materials.Keys.All(renderer=>!section.IsClipped(renderer,renderer.bounds.center)),"Legacy section requests never cut the complete engine geometry");
            operation.Stop();
            Check(materials.All(pair=>pair.Key.sharedMaterials.SequenceEqual(pair.Value)),"Stopping motion restores every original engine material assignment");
            Check(!operation.Active,"Operating display stops cleanly after rendering checks");
        }
        static bool SameScale(Vector3 actual,Vector3 expected)
        {
            // Imported CAD uses world scales around 240. A fixed 1e-6 absolute
            // tolerance is smaller than one float ULP at that scale. Check one
            // part per million per axis, including an absolute floor near zero.
            for(int axis=0;axis<3;axis++)if(Mathf.Abs(actual[axis]-expected[axis])>Mathf.Max(.0000001f,Mathf.Abs(expected[axis])*.000001f))return false;
            return true;
        }
        Vector3 TargetCenter(AssemblyPart part,Vector3 localContact)
        {var pose=part.GetTargetPose(part.TargetSocket);return pose.position+pose.rotation*Vector3.Scale(localContact,part.transform.lossyScale);}
        bool FindVisibleCamera(AssemblyPart part,Vector3 localContact,AssemblyEasyGuide guide)
        {
            var center=TargetCenter(part,localContact);float distance=Mathf.Max(.65f,AssemblyHeldView.WorldBounds(part).extents.magnitude*3.2f);
            var directions=new[]{Vector3.up,Vector3.right,Vector3.left,Vector3.forward,Vector3.back,new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(1,1,1),new Vector3(-1,1,1),Vector3.down};
            foreach(var direction in directions)
            {
                viewer.transform.position=center+direction.normalized*distance;
                viewer.transform.rotation=Quaternion.LookRotation(center-viewer.transform.position,Mathf.Abs(Vector3.Dot(direction.normalized,Vector3.up))>.95f?Vector3.forward:Vector3.up);
                Physics.SyncTransforms();if(guide.VisibleFrom(part,viewer))return true;
            }
            return false;
        }
        bool FindVisibleRay(AssemblyPart part,Vector3 localContact,AssemblyEasyGuide guide,out Ray ray)
        {
            var pose=part.GetTargetPose(part.TargetSocket);var scale=part.transform.lossyScale;
            foreach(var mesh in part.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!mesh.sharedMesh || !part.Manager.OwnsMesh(part,mesh))continue;
                var matrix=part.transform.worldToLocalMatrix*mesh.transform.localToWorldMatrix;var vertices=mesh.sharedMesh.vertices;var triangles=mesh.sharedMesh.triangles;int stride=Mathf.Max(3,triangles.Length/384/3*3);
                for(int i=0;i+2<triangles.Length;i+=stride)
                {
                    var point=matrix.MultiplyPoint3x4((vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3);
                    var world=pose.position+pose.rotation*Vector3.Scale(point,scale);ray=new Ray(viewer.transform.position,world-viewer.transform.position);
                    if(guide.Aim(ray,viewer.farClipPlane,viewer)==part)return true;
                }
            }
            ray=default;return false;
        }
    }
}
