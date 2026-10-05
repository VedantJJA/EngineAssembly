using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif
namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class WorkshopControlsVerification
    {
        static WorkshopControlsVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void UpdateWorkspace()
        {
            if(!Application.isBatchMode || !Application.dataPath.Replace('\\','/').EndsWith("/Tools/Verification/UnityProject/Assets"))throw new Exception("Use the isolated project.");
            var scene=EditorSceneManager.OpenScene(AssemblyWorkshopBuilder.ScenePath,OpenSceneMode.Single);
            var manager=UnityEngine.Object.FindAnyObjectByType<AssemblyManager>();var settings=new SerializedObject(manager);
            settings.FindProperty("useHeadBenchPosition").boolValue=true;settings.FindProperty("headBenchPosition").vector3Value=new Vector3(2.4f,-.5f,0);settings.ApplyModifiedPropertiesWithoutUndo();
            var player=UnityEngine.Object.FindAnyObjectByType<PlayerAssemblyController>();var data=new SerializedObject(player);data.FindProperty("inventoryArtwork").objectReferenceValue=AssetDatabase.LoadAssetAtPath<MobilePartArtwork>("Assets/UI/MobilePartArtwork.asset");data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);EditorApplication.Exit(0);
        }
        public static void RunBatch(){SessionState.SetBool("Controls.Verify",true);EditorSceneManager.OpenScene(AssemblyWorkshopBuilder.ScenePath,OpenSceneMode.Single);EditorApplication.isPlaying=true;}
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("Controls.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Workshop control checks").AddComponent<WorkshopControlsVerificationRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("Controls.Verify",false);EditorApplication.Exit(SessionState.GetBool("Controls.Result",false)?0:1);}
        }
    }
    public sealed class WorkshopControlsVerificationRunner:MonoBehaviour
    {
        [Serializable] class Report{public bool passed;public string[] checks;public string error;}
        readonly List<string> checks=new List<string>();AssemblyManager manager;PlayerAssemblyController player;AssemblyEasyGuide guide;
        IEnumerator Start()
        {
            var routine=Scenario();string error="";
            while(true){bool more=false;object step=null;try{more=routine.MoveNext();if(more)step=routine.Current;}catch(Exception ex){error=ex.ToString();}if(!more || error.Length>0)break;yield return step;}
            File.WriteAllText("Tools/Verification/workshop-controls-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,checks=checks.ToArray(),error=error},true));SessionState.SetBool("Controls.Result",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Scenario()
        {
            yield return null;yield return null;yield return null;
            manager=AssemblyManager.Instance;var ui=manager.GetComponent<AssemblyWorkshopUI>();ui.SetOpen(false);
            player=FindAnyObjectByType<PlayerAssemblyController>();player.enabled=false;guide=manager.GetComponent<AssemblyEasyGuide>();
            manager.SetDifficulty(SnapDifficulty.Easy,false);manager.SelectChapter(3);
            foreach(var part in manager.AssemblyParts){var so=new SerializedObject(part);so.FindProperty("snapDuration").floatValue=0;so.ApplyModifiedPropertiesWithoutUndo();}
            foreach(var part in manager.AssemblyParts.Where(p=>p.ChapterId==manager.CurrentChapterId && p.Definition.order<403).OrderBy(p=>p.Definition.order))part.InstallImmediate(part.TargetSocket);
            var head=manager.FindPart("401_Cylinder_Head_01");manager.TargetFrame(head.Definition).rotation=Quaternion.identity;
            foreach(var part in manager.AssemblyParts.Where(p=>p.PartId.StartsWith("402_") || p.PartId.StartsWith("403_")))part.InstallImmediate(part.TargetSocket);
            var springs=manager.AssemblyParts.Where(p=>p.PartId.StartsWith("405_")).OrderBy(p=>p.PartId).ToArray();
            foreach(var spring in springs)player.PickUp(spring);
            Assert(player.Inventory.Count==12,"All twelve identical valve springs form one carried stack, including flipped CAD instances");
            var keeper=manager.AssemblyParts.First(p=>p.PartId.StartsWith("407_"));player.PickUp(keeper);Assert(player.Inventory.Count==12 && !keeper.IsSelected,"A different part type cannot enter the carried spring stack");
            var screws=manager.AssemblyParts.Where(p=>p.PartId.Contains("Screw") || p.PartId.Contains("Bolt")).ToArray();
            var different=screws.SelectMany(a=>screws.Where(b=>b!=a && AssemblyInventory.TypeName(a)!=AssemblyInventory.TypeName(b)).Select(b=>(a,b))).First();
            Assert(!AssemblyInventory.Matches(different.a,different.b),"Different screw specifications cannot be grouped");
            var heldScale=springs[0].transform.lossyScale;var selected=springs.Last();var ray=ClearRay(selected);
            var body=player.GetComponent<CharacterController>();body.enabled=true;body.height=1.8f;body.center=new Vector3(0,.9f,0);
            player.transform.position=ray.origin-Vector3.up*1.65f;player.ViewCamera.transform.SetPositionAndRotation(ray.origin,Quaternion.LookRotation(ray.direction));
            var self=GameObject.CreatePrimitive(PrimitiveType.Cube);self.name="Player query regression surface";self.transform.SetParent(player.transform,true);self.transform.position=ray.GetPoint(.25f);self.transform.localScale=Vector3.one*.1f;self.GetComponent<Renderer>().enabled=false;Physics.SyncTransforms();
            Assert(guide.Aim(ray,10,player.ViewCamera)==selected,"Standing player colliders cannot obstruct a directly visible ghost");
            body.height=1.05f;body.center=new Vector3(0,.525f,0);Physics.SyncTransforms();Assert(guide.Aim(ray,10,player.ViewCamera)==selected,"Crouching uses the same world occlusion rules");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=ray.GetPoint(.5f);wall.transform.localScale=Vector3.one*.2f;Physics.SyncTransforms();
            Assert(!player.TryPlace(ray),"Inventory placement still rejects an unrelated world obstacle");wall.SetActive(false);Destroy(wall);self.SetActive(false);Destroy(self);body.enabled=false;
            Assert(player.TryPlace(ray) && selected.IsSnapped && player.Inventory.Count==11,"Clicking a different held spring's ghost places that exact CAD identity and decrements inventory");
            Assert(Vector3.Distance(heldScale,player.CurrentHeldPart.transform.lossyScale)<.001f,"The remaining carried object keeps its original size");
            yield return new WaitForSeconds(.6f);Canvas.ForceUpdateCanvases();Capture(ui,"inventory-hud");
            foreach(var spring in springs.Where(p=>!p.IsSnapped))Assert(player.TryPlace(ClearRay(spring)),"Stack spring placement: "+spring.PartId);
            Assert(player.Inventory.Count==0 && !player.CurrentHeldPart,"Inventory empties after the last matching component is placed");
            foreach(var difficulty in new[]{SnapDifficulty.Medium,SnapDifficulty.Hard})
            {
                manager.SelectChapter(3);manager.SetDifficulty(difficulty,false);
                foreach(var p in manager.AssemblyParts.Where(p=>p.ChapterId==manager.CurrentChapterId && p.Definition.order<403).OrderBy(p=>p.Definition.order))p.InstallImmediate(p.TargetSocket);
                foreach(var p in manager.AssemblyParts.Where(p=>p.PartId.StartsWith("402_") || p.PartId.StartsWith("403_")))p.InstallImmediate(p.TargetSocket);
                manager.TargetFrame(head.Definition).rotation=Quaternion.identity;
                player.PickUp(springs[0]);player.PickUp(springs[1]);player.TogglePlacementMode();
                var primary=springs[0];var target=springs[1];var pose=target.GetTargetPose(target.TargetSocket);
                var primaryCenter=primary.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(primary).center);
                var targetCenter=target.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(target).center);
                var rotation=primary.GetTargetPose(primary.TargetSocket).rotation;
                var center=pose.position+pose.rotation*Vector3.Scale(targetCenter,target.transform.lossyScale);
                primary.MoveHeld(center-rotation*Vector3.Scale(primaryCenter,primary.transform.lossyScale)+Vector3.right*3,rotation);
                var placementRay=ClearRay(target);
                Assert(!player.TryPlace(placementRay) && !target.IsSnapped,"Stacked "+difficulty+" placement retains the snap-distance rule");
                // The candidate may become the displayed part after a rejected snap.
                target.MoveHeld(pose.position,pose.rotation);
                Assert(player.TryPlace(placementRay) && target.IsSnapped && player.Inventory.Count==1,"Stacked "+difficulty+" placement installs the aimed identity at its own socket");
                player.DropHeldPart(false);
            }
            // Complete member parts without installing the carrier so the next operation
            // is insertion into the engine, rather than assembly on the bench.
            foreach(int chapter in new[]{1,3})
            {
                manager.SelectChapter(chapter);InstallMembers();
                var next=manager.NextHintPart();Assert(next && manager.CarrierDefinition(next.PartId)!=null,"A completed bench unit becomes the next insertion step");
                player.PickUp(next);Assert(player.CurrentHeldPart==next,"A complete unit can be carried before the engine is turned into the required orientation");
                Assert(manager.FlipFrame()==manager.AssemblyRoot,"Flip resolves the engine destination for "+next.PartId);
                var before=next.transform.rotation;var engineBefore=manager.AssemblyRoot.rotation;var center=manager.AssemblyRoot.GetComponent<PickupablePlatform>().GeometryCenter();
                player.FlipWorkpiece();yield return new WaitForSeconds(.6f);
                Assert(Quaternion.Angle(engineBefore,manager.AssemblyRoot.rotation)>179 && Quaternion.Angle(before,next.transform.rotation)<.01f,"F flips the engine and leaves the carried "+next.PartId+" unchanged");
                Assert(player.CurrentHeldPart==next && next.IsSelected && Vector3.Distance(center,manager.AssemblyRoot.GetComponent<PickupablePlatform>().GeometryCenter())<.001f,"Flipping preserves inventory and the engine's centre pivot");player.DropHeldPart(false);
            }
            manager.SelectChapter(0);manager.SetDifficulty(SnapDifficulty.Hard,false);player.PickUp(manager.NextHintPart());
            Assert(player.CurrentHeldPart,"A current-step part is held for placement-control testing");
#if ENABLE_INPUT_SYSTEM
            var originalSettings=InputSystem.settings;InputSystem.settings=Instantiate(originalSettings);InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Q));InputSystem.Update();player.SendMessage("Update");Assert(player.PlacementMode,"Q enters placement mode");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.R));InputSystem.Update();player.SendMessage("Update");Assert(player.Tool==PlayerAssemblyController.PlacementTool.Move,"R switches the placement tool to Move");
            var centerBefore=AssemblyHeldView.WorldBounds(player.CurrentHeldPart).center;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(30,20)}.WithButton(MouseButton.Right));InputSystem.Update();player.SendMessage("Update");
            Assert(Vector3.Distance(centerBefore,AssemblyHeldView.WorldBounds(player.CurrentHeldPart).center)>.05f,"Right-mouse dragging moves the item in the view plane");
            var rotationBefore=player.CurrentHeldPart.transform.rotation;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.R));InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(24,12)}.WithButton(MouseButton.Right));InputSystem.Update();player.SendMessage("Update");
            Assert(player.Tool==PlayerAssemblyController.PlacementTool.Rotate && Quaternion.Angle(rotationBefore,player.CurrentHeldPart.transform.rotation)>5,"The Rotate tool uses mouse motion to orient the held item");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.P));InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();var caption=manager.GetComponentInChildren<AssemblyCaptionPresenter>();caption.SendMessage("LateUpdate");Assert(!caption.InstructionsVisible,"P hides the top-right step card");
            InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);InputSystem.settings=originalSettings;
#endif
            player.DropHeldPart(false);manager.SetDifficulty(SnapDifficulty.Easy,false);player.PickUp(manager.NextHintPart());player.TogglePlacementMode();Assert(player.CarryMode && !player.PlacementMode,"Easy difficulty has only carry mode");player.DropHeldPart(false);
            var save=new Event{type=EventType.KeyDown,keyCode=KeyCode.S,modifiers=EventModifiers.Control};
            Assert(WorkshopPlayShortcutGuard.Consume(save,true,true,true) && save.type==EventType.Used,"Ctrl+S is consumed before the editor Save shortcut during workshop play");
            Assert(!WorkshopPlayShortcutGuard.Consume(new Event{type=EventType.KeyDown,keyCode=KeyCode.S,modifiers=EventModifiers.Control},false,true,true),"Ctrl+S remains available outside Play mode");
            Assert(!WorkshopPlayShortcutGuard.Consume(new Event{type=EventType.KeyDown,keyCode=KeyCode.S,modifiers=EventModifiers.Control},true,false,true),"Other editor windows retain their normal Save shortcut");
            var handlers=typeof(EditorApplication).GetField("globalEventHandler",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null) as Delegate;
            Assert(handlers!=null && handlers.GetInvocationList().First().Method.DeclaringType==typeof(WorkshopPlayShortcutGuard),"The workshop guard runs before Unity's registered shortcut handlers");
            ui.Open(AssemblyWorkshopUI.Page.Settings);Capture(ui,"settings-selector");ui.Open(AssemblyWorkshopUI.Page.Controls);Capture(ui,"controls-menu");
            Assert(ui.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text=="Q") && ui.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length==0,"Controls are documented on custom settings cards without stock buttons");
        }
        void InstallMembers()
        {
            var pending=manager.AssemblyParts.Where(p=>p.ChapterId==manager.CurrentChapterId && manager.CarrierDefinition(p.PartId)==null && !p.IsSnapped).OrderBy(p=>p.Definition.order).ToList();
            while(pending.Count>0){var next=pending.FirstOrDefault(p=>AssemblyRecipe.Dependencies(p.Definition).All(manager.IsPlaced));if(!next)break;next.InstallImmediate(next.TargetSocket);pending.Remove(next);}
        }
        Ray ClearRay(AssemblyPart part)
        {
            var pose=part.GetTargetPose(part.TargetSocket);var local=part.transform.InverseTransformPoint(AssemblyHeldView.WorldBounds(part).center);var center=pose.position+pose.rotation*Vector3.Scale(local,part.transform.lossyScale);
            for(int i=0;i<32;i++)
            {
                float angle=i*Mathf.PI/4;var origin=center+new Vector3(Mathf.Cos(angle)*(i<16?.35f:.7f),i%16<8?1:.55f,Mathf.Sin(angle)*(i<16?.35f:.7f));
                player.ViewCamera.transform.SetPositionAndRotation(origin,Quaternion.LookRotation(center-origin));var ray=new Ray(origin,center-origin);
                if(guide.Aim(ray,10,player.ViewCamera)==part)return ray;
            }
            throw new Exception("No visible inventory ghost: "+part.PartId);
        }
        void Capture(AssemblyWorkshopUI ui,string name)
        {
            var canvas=ui.GetComponentInChildren<Canvas>();var mode=canvas.renderMode;var camera=player.ViewCamera;
            var target=new RenderTexture(1440,900,24);target.Create();camera.targetTexture=target;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.5f;Canvas.ForceUpdateCanvases();
            WorkshopPresentationBuilder.Render(camera,"Tools/Verification/"+name+".png",1440,900);canvas.renderMode=mode;camera.targetTexture=null;target.Release();Destroy(target);
        }
        void Assert(bool condition,string message){if(!condition)throw new Exception(message);checks.Add(message);}
    }
}
