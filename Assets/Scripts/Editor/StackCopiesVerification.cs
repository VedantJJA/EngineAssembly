using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class StackCopiesVerification
    {
        static StackCopiesVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch(){SessionState.SetBool("StackCopies.Run",true);EditorSceneManager.OpenScene("Assets/Scenes/AssemblyWorkshop.unity");EditorApplication.isPlaying=true;}
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("StackCopies.Run",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Identical physical copies checks").AddComponent<StackCopiesVerificationRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("StackCopies.Run",false);EditorApplication.Exit(SessionState.GetBool("StackCopies.Passed",false)?0:1);}
        }
    }
    public sealed class StackCopiesVerificationRunner:MonoBehaviour
    {
        [Serializable] sealed class Report{public bool passed;public string error;public string[] checks;}
        readonly List<string> checks=new List<string>();AssemblyManager manager;PlayerAssemblyController player;AssemblyEasyGuide guide;
        void Check(bool condition,string text){if(!condition)throw new Exception(text);checks.Add(text);}
        IEnumerator Start()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";
            while(stack.Count>0){bool more=false;object value=null;try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception e){error=e.ToString();break;}if(!more){stack.Pop();continue;}if(value is IEnumerator nested)stack.Push(nested);else yield return value;}
            Directory.CreateDirectory("Tools/Verification");File.WriteAllText("Tools/Verification/stack-copies-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,error=error,checks=checks.ToArray()},true));SessionState.SetBool("StackCopies.Passed",error.Length==0);EditorApplication.isPlaying=false;
        }
        void Prepare(string prefix)
        {
            manager.SelectChapter(3);manager.SetDifficulty(SnapDifficulty.Easy,false);
            var next=manager.AssemblyParts.First(p=>p.Definition!=null && p.PartId.StartsWith(prefix));
            foreach(var part in manager.AssemblyParts.Where(p=>p.ChapterId==manager.CurrentChapterId && p.Definition.order<next.Definition.order).OrderBy(p=>p.Definition.order))part.InstallImmediate(part.TargetSocket);
            foreach(var part in manager.AssemblyParts.Where(p=>p.Definition!=null && p.PartId.StartsWith(prefix)))foreach(var id in AssemblyRecipe.Dependencies(part.Definition))InstallDependency(id);
            manager.TargetFrame(next.Definition).rotation=next.Definition.orientation==AssemblyOrientation.UpsideDown?Quaternion.Euler(180,0,0):Quaternion.identity;
        }
        void InstallDependency(string id){if(manager.IsPlaced(id))return;var part=manager.FindPart(id);foreach(var required in AssemblyRecipe.Dependencies(part.Definition))InstallDependency(required);part.InstallImmediate(part.TargetSocket);}
        IEnumerator Scenario()
        {
            yield return null;yield return null;manager=AssemblyManager.Instance;manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);
            player=FindAnyObjectByType<PlayerAssemblyController>();player.enabled=false;player.GetComponent<CharacterController>().enabled=false;guide=manager.GetComponent<AssemblyEasyGuide>();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(40,-.03f,-1.5f);floor.transform.localScale=new Vector3(4,.06f,4);
            foreach(string prefix in new[]{"404_Intake","404_Exhaust","405_"})
            {
                Prepare(prefix);yield return null;yield return null;
                var copies=manager.AssemblyParts.Where(p=>p.PartId.StartsWith(prefix)).OrderBy(p=>p.PartId).Take(5).ToArray();
                Check(copies.Length==5,"Five actual model copies exist for "+prefix);
                for(int cycle=0;cycle<2;cycle++)
                {
                    player.transform.SetPositionAndRotation(new Vector3(40,1.35f,-3),Quaternion.identity);player.ViewCamera.transform.SetPositionAndRotation(new Vector3(40,3,-3),Quaternion.identity);
                    foreach(var part in copies)player.PickUp(part);
                    Check(player.Inventory.Count==5,"Five "+prefix+" copies collected, cycle "+cycle);
                    // Exercise the real carry Update path, which the older drop test skipped.
                    for(int i=0;i<3;i++){player.SendMessage("Update");yield return null;}
                    Check(copies.Count(p=>p.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled))==1,"Only the hand's representative is rendered while carrying "+prefix);
                    player.DropHeldPart(false);Physics.SyncTransforms();
                    Check(player.Inventory.Count==0 && copies.All(p=>!p.IsSelected && !p.Body.isKinematic && p.Body.useGravity && p.Body.detectCollisions),"All five "+prefix+" copies become independent physical drops");
                    Check(copies.All(p=>p.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled && !r.forceRenderingOff)),"All five "+prefix+" copies render after release");
                    for(int i=0;i<copies.Length;i++)for(int j=i+1;j<copies.Length;j++)Check(!AssemblyHeldView.WorldBounds(copies[i]).Intersects(AssemblyHeldView.WorldBounds(copies[j])),"Distinct dropped bounds for "+prefix+" "+i+" / "+j);
                    for(int i=0;i<4;i++){yield return new WaitForFixedUpdate();yield return null;}
                    Check(copies.All(p=>p.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled) && AssemblyHeldView.WorldBounds(p).center.y>1),"All five "+prefix+" drops remain visible and in the workspace after physics");
                    for(int i=0;i<85;i++){yield return new WaitForFixedUpdate();yield return null;}
                    foreach(var copy in copies){var bounds=AssemblyHeldView.WorldBounds(copy);Check(bounds.min.y>-.08f && bounds.min.y<.15f && copy.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled),"Dropped copy settles visibly above the floor: "+copy.PartId+" bounds "+bounds.ToString("F4")+" body "+copy.Body.position.ToString("F4"));}
                }
            }
            foreach(string prefix in new[]{"404_Intake","405_"})
            {
                Prepare(prefix);yield return null;yield return null;
                var candidates=manager.AssemblyParts.Where(p=>p.Definition!=null && p.PartId.StartsWith(prefix) && !p.IsSnapped && manager.CanInstall(p,p.TargetSocket,out _)).OrderBy(p=>p.PartId).ToArray();
                var source=candidates[0];var matching=candidates.Where(p=>AssemblyInventory.Matches(source,p)).ToArray();
                Check(matching.Length>1,"Several interchangeable destinations exist for "+prefix);
                int allCount=AssemblyPart.AllParts.Count;player.PickUp(source);guide.RefreshTargets();
                var targets=(IEnumerable)typeof(AssemblyEasyGuide).GetField("targets",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(guide);
                var highlighted=new HashSet<AssemblyPart>();foreach(var target in targets){var type=target.GetType();if((bool)type.GetField("isGhost").GetValue(target) && ((GameObject)type.GetField("query").GetValue(target)).activeSelf)highlighted.Add((AssemblyPart)type.GetField("part").GetValue(target));}
                Check(matching.All(highlighted.Contains),"Picking up one item highlights every matching socket for "+prefix);
                Check(highlighted.All(p=>AssemblyInventory.Matches(source,p)),"Different part types do not appear as compatible ghosts");
                var destination=matching.Last();Check(!destination.IsSelected,"The chosen destination was not separately picked up");
                var ray=ClearRay(destination);Check(player.TryPlace(ray),"One carried copy fits another matching socket for "+prefix);
                Check(destination.IsSnapped && manager.IsPlaced(destination.PartId) && !manager.IsPlaced(source.PartId) && player.Inventory.Count==0,"Only the chosen socket completes and exactly one inventory copy is consumed");
                Check(AssemblyPart.AllParts.Count==allCount,"Interchangeable placement does not duplicate recipe objects or IDs");
                Check(!source.IsSelected && source.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled),"The remaining supply copy stays visible after interchange");
                Check(!AssemblyEquivalentPlacement.Place(source,destination),"Occupied sockets cannot consume a second copy");
                player.PickUp(source);var next=matching.First(p=>!p.IsSnapped && p!=source);ClearRay(next);
                var contact=source.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(source).center);var seat=AssemblyEquivalentPlacement.SeatedPose(source,next);
                var grip=new QuestGripPlacement();Check(grip.Update(source,seat,contact,player.ViewCamera) && grip.Socket==next.TargetSocket,"VR grip previews the nearest interchangeable socket for "+prefix);
                Check(!next.IsSnapped && source.IsSelected,"An interchangeable grip preview does not install early");
                Check(grip.Release(true) && next.IsSnapped && !source.IsSelected,"Releasing grip installs into the interchangeable destination for "+prefix);
                player.Inventory.Prune();Check(player.Inventory.Count==0,"Grip interchange consumes one physical copy");
            }
            foreach(var difficulty in new[]{SnapDifficulty.Medium,SnapDifficulty.Hard})
            {
                Prepare("405_");yield return null;yield return null;manager.SetDifficulty(difficulty,false);
                var source=manager.AssemblyParts.First(p=>p.PartId.StartsWith("405_"));var destination=manager.AssemblyParts.Last(p=>p.PartId.StartsWith("405_") && AssemblyInventory.Matches(source,p));
                player.PickUp(source);player.TogglePlacementMode();var pose=AssemblyEquivalentPlacement.SeatedPose(source,destination);
                source.MoveHeld(pose.position+Vector3.right*3,pose.rotation);var ray=ClearRay(destination);
                Check(!player.TryPlace(ray) && player.Inventory.Count==1 && source.IsSelected && !destination.IsSnapped,"Interchangeable "+difficulty+" placement preserves distance and inventory on rejection");
                source.MoveHeld(pose.position,pose.rotation);ray=ClearRay(destination);
                Check(player.TryPlace(ray) && destination.IsSnapped && player.Inventory.Count==0,"Interchangeable "+difficulty+" placement works when physically aligned at the target");
            }
        }
        Ray ClearRay(AssemblyPart part)
        {
            var pose=part.GetTargetPose(part.TargetSocket);var local=part.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(part).center);var center=pose.position+pose.rotation*Vector3.Scale(local,part.transform.lossyScale);
            for(int i=0;i<48;i++){float a=i*Mathf.PI/4;var origin=center+new Vector3(Mathf.Cos(a)*.65f,i%16<8?1:-1,Mathf.Sin(a)*.65f);player.ViewCamera.transform.SetPositionAndRotation(origin,Quaternion.LookRotation(center-origin));Physics.SyncTransforms();var ray=new Ray(origin,center-origin);if(guide.Aim(ray,5,player.ViewCamera)==part)return ray;}
            throw new Exception("No visible matching ghost: "+part.PartId);
        }
    }
}
