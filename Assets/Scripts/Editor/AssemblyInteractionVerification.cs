using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class AssemblyInteractionVerification
    {
        static AssemblyInteractionVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch()
        {
            if(!Application.isBatchMode || !Application.dataPath.Replace('\\','/').EndsWith("/Tools/Verification/UnityProject/Assets"))throw new InvalidOperationException("Isolated verification project only.");
            SessionState.SetBool("Interaction.Verify",true);EditorSceneManager.OpenScene("Assets/Scenes/AssemblyWorkshop.unity");EditorApplication.isPlaying=true;
        }
        public static void RunCrosshairBatch(){SessionState.SetBool("Interaction.CrosshairOnly",true);RunBatch();}
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("Interaction.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Interaction verification").AddComponent<AssemblyInteractionVerificationRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("Interaction.Verify",false);EditorApplication.Exit(SessionState.GetBool("Interaction.Result",false)?0:1);}
        }
    }
    public sealed class AssemblyInteractionVerificationRunner:MonoBehaviour
    {
        [Serializable] class Report {public bool passed;public string[] checks;public string error;}
        readonly List<string> checks=new List<string>();float originalVolume;
        void Check(bool result,string message){if(!result)throw new Exception(message);checks.Add(message);}
        IEnumerator Start()
        {
            originalVolume=WorkshopAudio.Volume;DontDestroyOnLoad(gameObject);var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";
            while(stack.Count>0){bool more=false;object item=null;try{more=stack.Peek().MoveNext();if(more)item=stack.Peek().Current;}catch(Exception e){error=e.ToString();break;}if(!more){stack.Pop();continue;}if(item is IEnumerator nested)stack.Push(nested);else yield return item;}
            WorkshopAudio.SetVolume(originalVolume);File.WriteAllText("Tools/Verification/interaction-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,checks=checks.ToArray(),error=error},true));SessionState.SetBool("Interaction.Result",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Scenario()
        {
            yield return null;yield return null;yield return null;
            if(SessionState.GetBool("Interaction.CrosshairOnly",false)){yield return VerifyCrosshair();yield break;}
            var manager=AssemblyManager.Instance;var ui=manager.GetComponent<AssemblyWorkshopUI>();var player=FindAnyObjectByType<PlayerAssemblyController>();player.enabled=false;
            Check(manager.AssemblyRoot.lossyScale==Vector3.one*.6f,"Engine and socket frame use the reduced uniform scale");
            var scale=manager.FindPart("401_Cylinder_Head_01").transform.lossyScale;
            manager.LoadRecipe(manager.Recipe);Check(manager.FindPart("401_Cylinder_Head_01").transform.lossyScale==scale,"Recipe reload does not scale physical parts a second time");
            manager.SelectChapter(3);var firstHead=manager.FindPart("401_Cylinder_Head_01");firstHead.InstallImmediate(firstHead.TargetSocket);
            var valveDefinition=manager.Recipe.parts.First(d=>d.order==402);var p=manager.FindPart(valveDefinition.id);var headFrame=manager.TargetFrame(valveDefinition);headFrame.rotation=Quaternion.identity;
            Check(manager.RequiresWorkpieceOrientation && !manager.CanInstall(p,p.TargetSocket,out _),"PC requires an upside-down head to install valves");
            headFrame.rotation=Quaternion.Euler(180,0,0);Check(manager.CanInstall(p,p.TargetSocket,out _),"PC accepts the same valve after the destination is flipped");headFrame.rotation=Quaternion.identity;
            ui.Open(AssemblyWorkshopUI.Page.Settings);yield return null;
            Check(ui.GetComponentInChildren<WorkshopVolumeSlider>(),"PC settings contain a custom volume slider");WorkshopAudio.SetVolume(.73f);
            Check(Mathf.Abs(AudioListener.volume-.73f)<.001f && Mathf.Abs(PlayerPrefs.GetFloat("Assembly.Volume")-.73f)<.001f,"Volume slider controls actual audio gain and persists it");
            ui.SetOpen(false);manager.SelectChapter(0);yield return new WaitForSecondsRealtime(.5f);
            var narrator=manager.GetComponent<AssemblyNarrator>();Check(narrator && narrator.Source.clip && narrator.Source.clip.loadState==AudioDataLoadState.Loaded && narrator.Source.isPlaying,"Narration loads and starts on the active caption step");
            Check(narrator.ReplayCurrent() && narrator.CurrentCueId=="chapter-1-cue-001","The current instruction can be replayed without advancing assembly");yield return null;
            Check(narrator.Source.spatialBlend==0 && narrator.Source.volume>0 && FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l=>l.isActiveAndEnabled)==1,"Voice has nonzero gain, a 2D source and exactly one active listener");
            var wav=File.ReadAllBytes(AssetDatabase.GetAssetPath(narrator.Source.clip));bool signal=false;for(int sample=44;sample+1<wav.Length;sample+=2)if(Math.Abs((int)BitConverter.ToInt16(wav,sample))>700){signal=true;break;}Check(signal,"Connected narration file contains audible waveform samples");
            ui.Open(AssemblyWorkshopUI.Page.Pause);yield return null;Check(!narrator.Source.isPlaying,"Narration pauses with the PC menu");ui.SetOpen(false);yield return null;Check(narrator.Source.isPlaying,"Narration resumes when gameplay continues");
            manager.SelectChapter(1);yield return new WaitForSecondsRealtime(.25f);
            Check(narrator.CurrentCueId=="piston-201" && narrator.Source.isPlaying,"First piston uses a generic shared voice instruction");
            foreach(var definition in manager.Recipe.parts.Where(d=>d.chapterId==manager.CurrentChapterId && d.order>=201 && d.order<=209).OrderBy(d=>d.order))
            {var part=manager.FindPart(definition.id);part.InstallImmediate(part.TargetSocket);yield return new WaitForSecondsRealtime(.15f);}
            yield return null;yield return null;
            Check(narrator.CurrentCueId=="repeat-piston" && narrator.Source.isPlaying,"After the first piston, one short reminder tells the player to repeat the process");
            manager.SelectChapter(1);yield return new WaitForSecondsRealtime(.2f);
            Check(narrator.Source.isPlaying,"Restarting a chapter restores the first assembly's narration");
            manager.SelectChapter(3);var head=manager.FindPart("401_Cylinder_Head_01");head.InstallImmediate(head.TargetSocket);
            manager.TargetFrame(manager.NextHintPart().Definition).rotation=Quaternion.identity;
            yield return null;yield return null;
            Check(narrator.CurrentCueId=="flip-upside-down" && narrator.Source.isPlaying,"The player hears a flip instruction when the head must be turned (actual "+narrator.CurrentCueId+")");
            foreach(var valve in manager.AssemblyParts.Where(v=>v.Definition?.order==402))valve.InstallImmediate(valve.TargetSocket);
            manager.TargetFrame(manager.Recipe.parts.First(d=>d.order==403)).rotation=Quaternion.identity;
            var seats=manager.AssemblyParts.Where(v=>v.PartId.StartsWith("402_") && v.ChapterId=="chapter-4").ToArray();
            Check(manager.NextHintPart().PartId.StartsWith("402_"),"Valve hints recommend spring seats before the other top-side types");
            Check(EngineLearningContent.Data.steps.Where(s=>s.voiceId=="valve-405").Count()==12 && EngineLearningContent.Data.steps.Where(s=>s.voiceId=="valve-405").Select(s=>s.narration).Distinct().Count()==1,"All twelve valve springs share a single generic instruction");
            var seed=manager.NextHintPart();var supplies=manager.GetComponent<WorkshopSupplyLayout>();var center=AssemblyHeldView.WorldBounds(seed).center;
            var table=supplies.Surfaces.OrderBy(s=>Vector3.Distance(s.center,center)).First();player.ViewCamera.transform.position=new Vector3(center.x,1.65f,table.center.z-table.size.y*.5f-.6f);Physics.SyncTransforms();
            var pickupScales=manager.AssemblyParts.ToDictionary(i=>i,i=>i.transform.lossyScale);
            player.PickUp(seed);Check(player.Inventory.Count==1 && player.Inventory.Contains(seed),"Pickup collects only the selected part");
            var second=seats.First(i=>i!=seed && AssemblyInventory.Matches(seed,i));player.PickUp(second);
            Check(player.Inventory.Count==2 && player.Inventory.Items.All(i=>AssemblyInventory.Matches(seed,i)),"Matching parts stack only after individual pickups");
            float stackDeadline=Time.realtimeSinceStartup+15;
            while((narrator.CurrentCueId!="inventory-stack" || !narrator.Source.isPlaying) && Time.realtimeSinceStartup<stackDeadline)yield return null;
            Check(narrator.CurrentCueId=="inventory-stack" && narrator.Source.isPlaying,"First matching-item stack plays its one-time voice instruction (actual "+narrator.CurrentCueId+")");
            Check(player.Inventory.Items.All(i=>Vector3.Distance(i.transform.lossyScale,pickupScales[i])<.00001f),"Collected items retain their exact reduced scale");
            yield return null;ui.SendMessage("Update");var count=ui.GetComponentsInChildren<TextMeshProUGUI>().First(t=>t.text=="×"+player.Inventory.Count || t.text=="x"+player.Inventory.Count);
            Check(count.outlineColor==Color.white && count.outlineWidth>.1f,"Inventory quantity has its own white text border");
            player.DropHeldPart(false);manager.SelectChapter(3);head.InstallImmediate(head.TargetSocket);foreach(var valve in manager.AssemblyParts.Where(v=>v.Definition?.order==402))valve.InstallImmediate(valve.TargetSocket);
            manager.TargetFrame(manager.Recipe.parts.First(d=>d.order==403)).rotation=Quaternion.identity;
            seed=manager.NextHintPart();seed.InstallImmediate(seed.TargetSocket);var seal=manager.AssemblyParts.First(v=>v.PartId.StartsWith("403_") && v.Definition.prerequisites.Contains(seed.PartId));
            Check(manager.CanInstall(seal,seal.TargetSocket,out _),"An eligible seal remains freely placeable alongside remaining equal-priority seats");
            while(manager.NextHintPart()?.PartId.StartsWith("402_")==true){var part=manager.NextHintPart();part.InstallImmediate(part.TargetSocket);}
            Check(manager.NextHintPart().PartId.StartsWith("403_"),"Highlight moves to seals only after all seats are fitted");
            while(manager.NextHintPart()?.PartId.StartsWith("403_")==true){var part=manager.NextHintPart();part.InstallImmediate(part.TargetSocket);}
            Check(manager.NextHintPart().PartId.StartsWith("405_"),"Highlight moves from all seals to springs");
            manager.SelectChapter(1);
            var pistonGroup=manager.Recipe.subAssemblies.First(g=>g.id=="230_Piston_Rod_Unit_Cyl01");
            foreach(var group in manager.Recipe.subAssemblies.Where(g=>g.id.StartsWith("230_Piston")))
                foreach(var member in manager.Members(group).OrderBy(i=>i.OrderIndex))member.InstallImmediate(member.TargetSocket);
            var piston=manager.FindPart(pistonGroup.rootPartId);player.PickUp(piston);
            Check(player.Inventory.Contains(piston),"Complete piston can be picked up after the ordered subassembly work");
            var capture=typeof(PlayerAssemblyController).GetMethod("CaptureContact",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            capture.Invoke(player,new object[]{AssemblyHeldView.WorldBounds(piston).max});
            var local=(Vector3)typeof(PlayerAssemblyController).GetField("contactLocal",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(player);
            Check(Vector3.Distance(piston.transform.TransformPoint(local),AssemblyHeldView.WorldBounds(piston).center)<.0001f,"Cursor pickup rotates a complete piston about its mesh center instead of the clicked edge");
            player.DropHeldPart(false);
            yield return VerifyCrosshair();
            yield return SceneManager.LoadSceneAsync("MobileAssemblyARFallback");yield return null;yield return null;yield return null;
            manager=AssemblyManager.Instance;Check(!manager.RequiresWorkpieceOrientation,"Mobile omits destination flip requirements");
            manager.SelectChapter(3);firstHead=manager.FindPart("401_Cylinder_Head_01");firstHead.InstallImmediate(firstHead.TargetSocket);p=manager.FindPart(valveDefinition.id);manager.TargetFrame(p.Definition).rotation=Quaternion.Euler(0,0,90);
            Check(manager.CanInstall(p,p.TargetSocket,out _),"Phone permits eligible installation with the engine sideways");
            Check(manager.GetComponent<AssemblyNarrator>() && Resources.Load<NarrationLibrary>("NarrationLibrary").cues.Length>=135,"Phone shares the expanded narration library");
            var mobile=manager.GetComponent<MobileAssemblyController>();mobile.ChooseChapter(1);yield return null;yield return null;
            var unit=manager.NextHintPart();unit.InstallImmediate(unit.TargetSocket);yield return null;yield return null;
            var pivot=mobile.WorldFocus;mobile.RotateEngine(new Vector2(100,45));
            Check(Vector3.Distance(pivot,mobile.WorldFocus)<.001f,"Phone fallback rotates a partial piston about the geometry center");
        }
        IEnumerator VerifyCrosshair()
        {
            var camera=new GameObject("Crosshair render camera").AddComponent<Camera>();camera.gameObject.AddComponent<UniversalAdditionalCameraData>();camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;
            var canvas=new GameObject("Crosshair render canvas",typeof(RectTransform)).AddComponent<Canvas>();canvas.gameObject.layer=31;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.5f;
            var graphic=new GameObject("Crosshair test",typeof(RectTransform)).AddComponent<InvertingCrosshair>();graphic.transform.SetParent(canvas.transform,false);graphic.gameObject.layer=31;graphic.rectTransform.anchorMin=graphic.rectTransform.anchorMax=graphic.rectTransform.pivot=new Vector2(.5f,.5f);graphic.rectTransform.sizeDelta=new Vector2(32,32);graphic.rectTransform.anchoredPosition=Vector2.zero;
            var rt=new RenderTexture(64,64,24);rt.Create();camera.targetTexture=rt;
            var reference=new GameObject("UI render reference",typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();reference.transform.SetParent(canvas.transform,false);reference.gameObject.layer=31;reference.color=Color.white;reference.rectTransform.anchorMin=reference.rectTransform.anchorMax=new Vector2(.5f,.5f);reference.rectTransform.sizeDelta=new Vector2(4,4);reference.rectTransform.anchoredPosition=new Vector2(22,22);
            yield return null;
            foreach(var background in new[]{Color.black,Color.white})
            {
                camera.backgroundColor=background;Canvas.ForceUpdateCanvases();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});var active=RenderTexture.active;RenderTexture.active=rt;
                var image=new Texture2D(64,64);image.ReadPixels(new Rect(0,0,64,64),0,0);image.Apply();var pixel=image.GetPixel(32,32);File.WriteAllBytes("Tools/Verification/crosshair-"+(background==Color.black?"dark":"light")+".png",image.EncodeToPNG());Check((background==Color.black?pixel.r>.9f:pixel.r<.1f) && pixel.a>.9f,"Crosshair inverts "+(background==Color.black?"dark":"light")+" background, preserves opacity, and avoids double inversion at its center");Destroy(image);RenderTexture.active=active;
            }
            camera.targetTexture=null;rt.Release();Destroy(rt);Destroy(canvas.gameObject);Destroy(camera.gameObject);
        }
    }
}
