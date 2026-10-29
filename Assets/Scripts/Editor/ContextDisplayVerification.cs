using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EngineAssembly.Editor
{
    [InitializeOnLoad] public static class ContextDisplayVerification
    {
        static ContextDisplayVerification()=>EditorApplication.playModeStateChanged+=Changed;
        public static void RunBatch(){SessionState.SetBool("ContextDisplay.Verify",true);EditorSceneManager.OpenScene("Assets/Scenes/AssemblyWorkshop.unity");EditorApplication.isPlaying=true;}
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("ContextDisplay.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Context narration and display verification").AddComponent<ContextDisplayRunner>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("ContextDisplay.Verify",false);EditorApplication.Exit(SessionState.GetBool("ContextDisplay.Passed",false)?0:1);}
        }
    }
    public sealed class ContextDisplayRunner:MonoBehaviour
    {
        [Serializable] sealed class Report{public bool passed;public string error;public string[] checks;public string[] materials;}
        readonly List<string> checks=new List<string>(),errors=new List<string>(),materials=new List<string>();AssemblyManager manager;PlayerAssemblyController player;
        void Check(bool value,string text){if(!value)throw new Exception(text);checks.Add(text);}
        void OnEnable()=>Application.logMessageReceived+=Log;void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string text,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception)errors.Add(text+"\n"+stack);}
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);var stack=new Stack<IEnumerator>();stack.Push(Scenario());string error="";
            while(stack.Count>0){bool more=false;object next=null;try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception e){error=e.ToString();break;}if(!more){stack.Pop();continue;}if(next is IEnumerator nested)stack.Push(nested);else yield return next;}
            WorkshopSectionCompletion.VerificationEnabled=false;Time.timeScale=1;if(errors.Count>0)error+="\n"+string.Join("\n",errors);File.WriteAllText("Tools/Verification/context-display-report.json",JsonUtility.ToJson(new Report{passed=error.Length==0,error=error,checks=checks.ToArray(),materials=materials.ToArray()},true));SessionState.SetBool("ContextDisplay.Passed",error.Length==0);EditorApplication.isPlaying=false;
        }
        IEnumerator Fresh(string name="AssemblyWorkshop")
        {
            yield return SceneManager.LoadSceneAsync(name);for(int i=0;i<5;i++)yield return null;manager=AssemblyManager.Instance;player=FindAnyObjectByType<PlayerAssemblyController>();if(manager){manager.GetComponent<AssemblyWorkshopUI>()?.SetOpen(false);manager.SetDifficulty(SnapDifficulty.Easy,false);}
        }
        IEnumerator WaitFor(Func<bool> predicate,string text,float seconds=5)
        {float end=Time.realtimeSinceStartup+seconds;while(!predicate() && Time.realtimeSinceStartup<end)yield return null;Check(predicate(),text);}
        static string Voice(AssemblyInstruction step)=>string.IsNullOrEmpty(step.voiceId)?step.id:step.voiceId;
        void Install(AssemblyInstruction step){foreach(var id in step.partIds){var part=manager.FindPart(id);if(part && !manager.IsPlaced(id))part.InstallImmediate(part.TargetSocket);}}
        IEnumerator Scenario()
        {
            for(int i=0;i<5;i++)yield return null;manager=AssemblyManager.Instance;player=FindAnyObjectByType<PlayerAssemblyController>();manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);yield return Narration();yield return Fresh();yield return Desktop();yield return Fresh("MobileEngineDisplay");yield return Mobile();
        }
        IEnumerator Narration()
        {
            var narrator=manager.GetComponent<AssemblyNarrator>();narrator.enabled=false;manager.SelectChapter(0);foreach(var d in manager.Recipe.parts)d.orientation=AssemblyOrientation.Any;
            var fake=ScriptableObject.CreateInstance<NarrationLibrary>();var clip=AudioClip.Create("Original lesson timing fixture",24000*60,1,24000,false);
            fake.cues=EngineLearningContent.Data.steps.Select(Voice).Concat(new[]{"complete","flip-upside-down","flip-upright"}).Distinct().Select(id=>new NarrationLibrary.Cue{id=id,clip=clip}).ToArray();typeof(AssemblyNarrator).GetField("library",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(narrator,fake);narrator.enabled=true;
            var first=EngineLearningContent.ForPart(manager.NextHintPart().Definition);yield return WaitFor(()=>narrator.Source.isPlaying && narrator.CurrentCueId==Voice(first),"Original first lesson starts");
            float start=narrator.LastCueStartedAt;narrator.Source.time=.4f;Install(first);yield return new WaitForSecondsRealtime(.2f);
            Check(narrator.CurrentCueId==Voice(first) && narrator.LastCueStartedAt==start && narrator.Source.time>=.4f,"Placement continues the original description in place, without restarting or adding another recording");
            Check(narrator.CurrentDescription?.description==first.purpose && narrator.CaptionWordLimit==first.purpose.Split(' ').Length,"Completed lessons expose only their original purpose to live captions");
            var second=EngineLearningContent.ForPart(manager.NextHintPart().Definition);yield return null;Install(second);yield return null;
            Check(narrator.CurrentCueId==Voice(first),"A second completed lesson waits behind the first original description");narrator.Source.time=narrator.CurrentPurposeEnd-.025f;yield return new WaitForSecondsRealtime(.18f);Check(!narrator.Source.isPlaying,"The completed first lesson stops before its obsolete placement tail");
            yield return WaitFor(()=>narrator.Source.isPlaying && narrator.CurrentCueId==Voice(second),"The already-completed second lesson plays next in its original order");Check(narrator.CurrentDescription?.description==second.purpose,"A queued completed lesson keeps its purpose instead of being discarded");
            string displayVoice=EngineLearningContent.Detail(manager.FindPart(second.partIds[0]).Definition).voiceId;Check(narrator.CurrentCueId!=displayVoice,"No separate display-description voice is injected after installation");
            var third=EngineLearningContent.ForPart(manager.NextHintPart().Definition);narrator.Source.time=narrator.CurrentPurposeEnd-.025f;yield return new WaitForSecondsRealtime(.18f);yield return WaitFor(()=>narrator.Source.isPlaying && narrator.CurrentCueId==Voice(third),"Third lesson follows the second without replaying either description");
            narrator.Source.time=narrator.CurrentPurposeEnd+.2f;yield return new WaitForSecondsRealtime(.15f);Check(narrator.Source.isPlaying,"An unfinished step retains its placement instructions");float thirdStart=narrator.LastCueStartedAt;Install(third);yield return new WaitForSecondsRealtime(.15f);Check(!narrator.Source.isPlaying && narrator.LastCueStartedAt==thirdStart,"Finishing during placement wording skips that tail without replaying its purpose");
            yield return PickupNarration(narrator);
            narrator.enabled=false;manager.SelectChapter(manager.Recipe.chapters.FindIndex(c=>c.title.Contains("Pistons")));narrator.enabled=true;yield return WaitFor(()=>narrator.Source.isPlaying,"Shared piston lesson starts");string shared=narrator.CurrentCueId;float sharedStart=narrator.LastCueStartedAt;var chosen=manager.AssemblyParts.First(p=>p.PartId.StartsWith("201_Piston_Cyl02"));chosen.InstallImmediate(chosen.TargetSocket);yield return null;Check(narrator.CurrentCueId==shared && narrator.LastCueStartedAt==sharedStart && narrator.CurrentDescription!=null,"Shared speech binds to the piston actually chosen, without replaying or requesting an already-fitted piston");
            narrator.enabled=false;manager.SelectChapter(0);var parts=manager.Recipe.parts.Where(p=>p.chapterId==manager.CurrentChapterId && !p.isBase).OrderBy(p=>p.order).ToArray();foreach(var d in parts.Take(parts.Length-1)){var part=manager.FindPart(d.id);part.InstallImmediate(part.TargetSocket);}var last=manager.FindPart(parts.Last().id);narrator.enabled=true;yield return null;yield return null;WorkshopSectionCompletion.VerificationEnabled=true;last.InstallImmediate(last.TargetSocket);
            yield return WaitFor(()=>narrator.Source.isPlaying && narrator.CurrentDescription!=null,"Final queued purpose survives chapter completion");var completion=manager.GetComponent<WorkshopSectionCompletion>();yield return new WaitForSecondsRealtime(.3f);Check(completion.SecondsRemaining>4.9f && !completion.Speech.IsBusy,"Completion countdown waits for original learning audio");narrator.Source.time=narrator.CurrentPurposeEnd-.025f;yield return new WaitForSecondsRealtime(1.2f);Check(!narrator.HasDescriptions && completion.SecondsRemaining<4.9f,"Completion continues once the original description ends");WorkshopSectionCompletion.VerificationEnabled=false;narrator.enabled=false;manager.SelectChapter(0);Destroy(fake);Destroy(clip);
        }
        IEnumerator PickupNarration(AssemblyNarrator narrator)
        {
            narrator.enabled=false;manager.SelectChapter(0);narrator.enabled=true;
            yield return WaitFor(()=>narrator.Source.isPlaying,"Pickup scenario starts with its original lesson");
            var first=EngineLearningContent.ForPart(manager.NextHintPart().Definition);Install(first);yield return null;
            var second=manager.NextHintPart();var secondStep=EngineLearningContent.ForPart(second.Definition);
            Check(second.BeginGrab(),"A real shared grab selects the next bearing");
            yield return WaitFor(()=>narrator.Source.isPlaying && narrator.CurrentCueId==Voice(secondStep),"Pickup immediately replaces the previous description with the selected part");
            Check(!((IEnumerable)typeof(AssemblyNarrator).GetField("pending",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(narrator)).Cast<object>().Any(),"Previous queued lessons are cleared on pickup");
            float started=narrator.LastCueStartedAt;narrator.Source.time=.4f;second.InstallImmediate(second.TargetSocket);yield return null;
            Check(narrator.LastCueStartedAt==started && narrator.Source.time>=.4f && narrator.CurrentDescription!=null,"Placing the picked part keeps its current description without restarting it");
            var third=manager.NextHintPart();var thirdStep=EngineLearningContent.ForPart(third.Definition);yield return null;
            Check(third.BeginGrab(),"Another real pickup selects the larger bearing");
            yield return WaitFor(()=>narrator.Source.isPlaying && narrator.CurrentCueId==Voice(thirdStep),"The new bearing voice replaces both old playback and queued speech");
            third.InstallImmediate(third.TargetSocket);yield return null;
            var shell=manager.NextHintPart();var shellStep=EngineLearningContent.ForPart(shell.Definition);Check(shellStep.partIds.Count>1 && shell.BeginGrab(),"A grouped shell lesson can be selected next");
            yield return WaitFor(()=>narrator.Source.isPlaying && narrator.CurrentCueId==Voice(shellStep),"Grouped-part pickup starts the correct lesson");
            started=narrator.LastCueStartedAt;narrator.Source.time=.4f;var another=manager.FindPart(shellStep.partIds.First(id=>id!=shell.PartId));Check(another.BeginGrab(),"Another matching shell can be picked up");yield return null;
            Check(narrator.LastCueStartedAt==started && narrator.Source.time>=.4f,"Picking another copy of the same lesson does not restart its description");
            another.InstallImmediate(another.TargetSocket);yield return null;
            Check(!manager.IsPlaced(shell.PartId) && narrator.CurrentDescription!=null && narrator.CaptionWordLimit<int.MaxValue,"The selected installed copy omits placement wording even while other copies remain");
            narrator.Source.time=narrator.CurrentPurposeEnd-.025f;yield return new WaitForSecondsRealtime(.2f);
            Check(!narrator.Source.isPlaying,"The placed copy stops at the description boundary");
        }
        MeshRenderer RendererFor(Transform root,string prefix)=>root.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name.StartsWith(prefix));
        void TextureCheck(MeshRenderer renderer,string label)
        {
            var material=renderer.sharedMaterial;Check(material && material.shader.name=="EngineAssembly/Surface Triplanar" && material.GetTexture("_BaseMap") is Texture2D texture && texture.width>8,label+" keeps the textured engine surface shader");Check(material.GetTexture("_RoughnessMap") && material.GetTexture("_DetailNormal"),label+" retains roughness and normal maps");var mesh=renderer.GetComponent<MeshFilter>();materials.Add(label+": "+material.name+" mesh="+mesh.sharedMesh.bounds.size+" scale="+renderer.transform.lossyScale+" textureDensity="+material.GetFloat("_TextureScale"));
        }
        IEnumerator Desktop()
        {
            manager.GetComponent<AssemblyNarrator>().enabled=false;var block=manager.FindPart("101_Cylinder_Block_01").GetComponentInChildren<MeshRenderer>();var temporary=new Material(Shader.Find("Universal Render Pipeline/Lit"));block.sharedMaterial=temporary;
            manager.SelectChapter(manager.Recipe.chapters.FindIndex(c=>c.id==PCDisplayChapter.ChapterId));manager.GetComponent<AssemblyWorkshopUI>().SetOpen(false);yield return null;yield return null;
            var display=manager.GetComponent<PCDisplayChapter>();block=RendererFor(manager.AssemblyRoot,"101_");TextureCheck(block,"PC info casing");Check(manager.GetComponentInChildren<InvertingCrosshair>().gameObject.activeInHierarchy,"PC display retains the centred inverting crosshair");Check(Vector3.Angle(player.InteractionRay.direction,player.ViewCamera.transform.forward)<.01f,"PC display interaction uses the centre aim ray");
            typeof(PCDisplayChapter).GetField("settingsOpen",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(display,true);yield return null;yield return null;Check(Cursor.visible && !manager.GetComponentInChildren<InvertingCrosshair>(true).gameObject.activeInHierarchy,"Display settings free the pointer and hide the aiming reticle");typeof(PCDisplayChapter).GetField("settingsOpen",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(display,false);yield return null;Check(manager.GetComponentInChildren<InvertingCrosshair>().gameObject.activeInHierarchy,"Closing display settings restores the fixed reticle");
            display.Timeline.SetValue(.3f);display.SetMotionMode(true);yield return null;yield return null;var treatment=manager.GetComponent<EngineSectionView>();Check(display.Operation.XRayEnabled && treatment.ContextVisibility(block)<=.1f,"PC motion defaults to an X-ray casing that exposes combustion");TextureCheck(block,"PC motion casing");var piston=RendererFor(manager.AssemblyRoot,"201_");TextureCheck(piston,"PC moving piston");Check(treatment.ContextVisibility(piston)==1,"Moving pistons retain full visibility inside the X-ray shell");Check(display.Operation.IsClipped(block,block.bounds.center) && !display.Operation.IsClipped(piston,piston.bounds.center),"X-ray inspection can pass through the casing to its visible moving parts");display.Operation.SetCrankAngle(display.Operation.CrankForCycle(410));Capture(player.ViewCamera,manager.AssemblyRoot,"context-motion-pc");
            display.SetMotionMode(false);display.Timeline.SetValue(1);yield return null;Check(treatment.ContextVisibility(block)==1,"Info mode restores opaque textured casing");TextureCheck(block,"PC restored info");display.SetMotionMode(true);yield return null;Check(treatment.ContextVisibility(block)<=.1f,"Repeated mode changes restore X-ray automatically");TextureCheck(piston,"PC repeated mode");Destroy(temporary);
        }
        IEnumerator Mobile()
        {
            var display=FindAnyObjectByType<MobileEngineDisplay>();yield return WaitFor(()=>display && display.Ready,"Mobile display is ready");var block=RendererFor(display.EngineRoot,"101_");TextureCheck(block,"Phone info casing");display.SetMode(MobileEngineDisplay.DisplayMode.OperatingEngine);yield return null;yield return null;var operation=display.GetComponent<EngineOperatingAnimation>();Check(operation.XRayEnabled && display.GetComponent<EngineSectionView>().ContextVisibility(block)<=.1f,"Phone and AR motion use the same X-ray default");TextureCheck(RendererFor(display.EngineRoot,"201_"),"Phone moving piston");operation.SetCrankAngle(operation.CrankForCycle(410));Capture(display.ViewCamera,display.EngineRoot,"context-motion-phone");display.SetMode(MobileEngineDisplay.DisplayMode.InfoTimeline);display.SetTimeline(1);yield return null;TextureCheck(block,"Phone restored info");
        }
        void Capture(Camera camera,Transform root,string name)
        {
            var renderers=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);var position=camera.transform.position;var rotation=camera.transform.rotation;camera.transform.SetPositionAndRotation(b.center+new Vector3(1,.45f,-1.3f).normalized*b.extents.magnitude*2.8f,Quaternion.LookRotation(b.center-(b.center+new Vector3(1,.45f,-1.3f).normalized*b.extents.magnitude*2.8f)));
            var canvases=FindObjectsByType<Canvas>(FindObjectsInactive.Include);var enabled=canvases.Select(c=>c.enabled).ToArray();foreach(var c in canvases)c.enabled=false;var target=new RenderTexture(1440,900,24);camera.targetTexture=target;camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1440,900),0,0);texture.Apply();File.WriteAllBytes("Tools/Verification/"+name+".png",texture.EncodeToPNG());for(int i=0;i<canvases.Length;i++)canvases[i].enabled=enabled[i];camera.targetTexture=null;RenderTexture.active=previous;camera.transform.SetPositionAndRotation(position,rotation);Destroy(texture);target.Release();Destroy(target);
        }
    }
}
