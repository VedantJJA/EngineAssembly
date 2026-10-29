using System.Linq;
using TMPro;
using UnityEngine;

namespace EngineAssembly
{
    // Captions are persistent task instructions. They do not enable ghosts or quest tracking.
    public sealed class AssemblyCaptionPresenter : MonoBehaviour
    {
        AssemblyManager manager;
        TextMeshProUGUI caption,context;
        TextMeshProUGUI stepTitle;
        LiveNarrationCaption liveCaption;
        bool mobile,shown=true,wasVisible;Vector2Int layoutSize;TextMeshProUGUI[] labels;UnityEngine.UI.Image background;MobileAssemblyController mobileController;AssemblyWorkshopUI desktopUI;AssemblyNarrator narrator;string titleCue,titleChapter;
        public bool InstructionsVisible=>shown;
        public void ToggleInstructions()=>shown=!shown;
        public string CurrentCueId { get; private set; }
        public string CaptionText=>caption?caption.text:"";
        public static AssemblyCaptionPresenter Create(Transform parent,AssemblyManager manager,bool mobile)
        {
            var rect=new GameObject("Assembly captions",typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            var background=rect.gameObject.AddComponent<UnityEngine.UI.Image>();background.color=WorkshopTheme.Surface;WorkshopUIArt.Card(background);background.raycastTarget=false;
            // Instructions sit over the scene like game subtitles. No opaque
            // panel or textured plane separates the player from the workpiece.
            rect.anchorMin=mobile?new Vector2(.025f,1):Vector2.one;
            rect.anchorMax=mobile?new Vector2(.975f,1):Vector2.one;
            rect.pivot=mobile?new Vector2(.5f,1):Vector2.one;
            rect.sizeDelta=new Vector2(mobile?0:540,mobile?194:248);
            rect.anchoredPosition=mobile?new Vector2(0,-194):new Vector2(-28,-144);
            var presenter=rect.gameObject.AddComponent<AssemblyCaptionPresenter>();presenter.manager=manager;presenter.mobile=mobile;
            presenter.stepTitle=Label(rect,"Step heading",mobile?15:15,0,24,mobile);
            presenter.caption=Label(rect,"Caption",mobile?22:23,32,mobile?130:170,mobile);
            var live=rect.gameObject.AddComponent<LiveNarrationCaption>();presenter.liveCaption=live;live.Label=presenter.caption;live.Assembly=manager.GetComponent<AssemblyNarrator>();
            presenter.context=Label(rect,"Instruction progress",16,mobile?163:210,26,mobile);
            presenter.context.color=WorkshopTheme.Accent;
            presenter.context.fontStyle=FontStyles.Normal;
            presenter.context.textWrappingMode=TextWrappingModes.NoWrap;presenter.context.overflowMode=TextOverflowModes.Ellipsis;
            if(mobile)
            {
                presenter.caption.fontSizeMin=16;
            }
            return presenter;
        }
        static TextMeshProUGUI Label(RectTransform parent,string name,float size,float top,float height,bool mobile)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=new Vector2(0,1);rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(.5f,1);
            rect.anchoredPosition=new Vector2(0,-top);rect.sizeDelta=new Vector2(mobile?-28:-24,height);
            var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=Resources.Load<TMP_FontAsset>("WorkshopHeading");text.fontSize=size;text.color=WorkshopTheme.Text;text.fontStyle=FontStyles.Bold;
            text.color=WorkshopTheme.Text;
            text.alignment=TextAlignmentOptions.Left;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;
            text.enableAutoSizing=true;text.fontSizeMin=size-4;text.fontSizeMax=size;
            text.outlineColor=new Color(1,1,1,.85f);
            text.outlineWidth=.1f;text.UpdateMeshPadding();
            var shadow=rect.gameObject.AddComponent<UnityEngine.UI.Shadow>();shadow.effectColor=new Color(0,0,0,.16f);shadow.effectDistance=new Vector2(1,-1);
            return text;
        }
        void LateUpdate()
        {
            if(!manager || manager.Recipe==null)return;
            if(this.mobile && layoutSize!=new Vector2Int(Screen.width,Screen.height))
            {
                layoutSize=new Vector2Int(Screen.width,Screen.height);
                var rect=(RectTransform)transform;bool landscape=Screen.width>Screen.height;
                rect.anchorMin=landscape?new Vector2(.02f,1):new Vector2(.025f,1);rect.anchorMax=landscape?new Vector2(.02f,1):new Vector2(.975f,1);rect.pivot=landscape?Vector2.up:new Vector2(.5f,1);
                rect.sizeDelta=new Vector2(landscape?590:0,194);rect.anchoredPosition=new Vector2(0,landscape?-126:manager.GetComponent<MobileARSession>()?-194:-126);
            }
            if(!mobileController)mobileController=manager.GetComponent<MobileAssemblyController>();var mobile=mobileController;
            if(!desktopUI)desktopUI=manager.GetComponent<AssemblyWorkshopUI>();
            if(!this.mobile && (!desktopUI || !desktopUI.IsOpen) && InputHelper.IsKeyDown(KeyCode.P))ToggleInstructions();
            bool visible=WorkshopGuidance.Captions && shown && !WorkshopTutorial.IsOpen && !manager.DisplayActive && manager.Mode==WorkshopMode.Assembly && (!mobile || !mobile.ChapterMenuOpen);
            if(labels==null){labels=GetComponentsInChildren<TextMeshProUGUI>();background=GetComponent<UnityEngine.UI.Image>();wasVisible=!visible;}
            if(wasVisible!=visible){wasVisible=visible;background.enabled=visible;foreach(var text in labels)text.enabled=visible;}if(!visible)return;
            var completion=manager.GetComponent<WorkshopSectionCompletion>();
            if(!narrator)narrator=manager.GetComponent<AssemblyNarrator>();
            if(liveCaption){bool completing=completion && completion.HandlesCompletion && (!narrator || !narrator.HasDescriptions);liveCaption.Assembly=completing?null:narrator;liveCaption.Speaker=completing?completion.Speech:null;}
            if(narrator && narrator.CurrentDescription!=null && narrator.HasDescriptions)
            {
                var detail=narrator.CurrentDescription;CurrentCueId=detail.voiceId;if(stepTitle)stepTitle.text=detail.title;SetCaption(detail.description);context.text="Part fitted · "+(completion && completion.HandlesCompletion?completion.StatusText:"How this part works");return;
            }
            if(manager.CurrentChapterId==WorkshopPractice.ChapterId && !manager.ChapterComplete)
            {
                if(stepTitle)stepTitle.text="CHAPTER 0 · PRACTICE";SetCaption("Try the controls with the coloured shapes. Fit the three cubes, the cylinder and the ball into their matching targets. You can drop and collect the cubes, operate the lift and change settings before starting the engine.");
                context.text=manager.ChapterInstalledCount+" / "+manager.ChapterPartCount+" practice shapes fitted";return;
            }
            if(manager.RepairActive)
            {
                titleCue=null;
                var repair=manager.Repair;CurrentCueId=repair.Complete?"repair-complete":"repair-step-"+repair.CompletedSteps;
                if(stepTitle)stepTitle.text=repair.Complete?"REPAIR COMPLETE":"REPAIR · STEP "+(repair.CompletedSteps+1).ToString("00")+" / "+repair.StepCount;
                SetCaption(repair.Complete?"Repair complete. The replacement bearing is fitted and the timing drive is rebuilt.":repair.CurrentStep.instruction);
                context.text=repair.Complete?(mobile?"Broken bearing retained in the hotbar. Tap CHAPTERS to continue.":"TAB to choose another chapter."):repair.IsPlacementStep?"Next: "+repair.NextPart.PartDisplayName:mobile?"Tap the named part to remove it into the hotbar.":"Pick up the highlighted part, then G to set aside.";
                var platform=WorkshopTutorial.DetectPlatform(manager.gameObject);
                if(platform=="VR")context.text=repair.Complete?"Open Chapters from the controller menu.":repair.IsPlacementStep?"Grip a part, preview at its ghost and release, or point and press trigger.":"Grip the named part; release away from its socket to set it aside.";
                else if(platform=="Hands")context.text=repair.Complete?"Open Chapters from the wrist MENU.":repair.IsPlacementStep?"Take the part with a pinch or grip, bring it to its ghost, then open your hand.":"Grip the named part; open your hand away from its socket to set it aside.";
                else if(platform=="Touch" && !repair.Complete)context.text=repair.IsPlacementStep?"Tap the replacement, then tap its visible matching target.":"Tap the named part, then use DROP to set it aside.";
                if(repair.Complete && completion && completion.HandlesCompletion)context.text=completion.StatusText;
                return;
            }
            if(manager.ChapterComplete)
            {
                titleCue=null;
                if(stepTitle)stepTitle.text="CHAPTER COMPLETE";
                CurrentCueId="complete";SetCaption(manager.Task==AssemblyTask.Assemble?"Chapter complete. Your section is assembled.":"Section disassembled.");
                context.text=completion && completion.HandlesCompletion?completion.StatusText:mobile?"Tap CONTINUE for the next chapter.":"Press ENTER to continue to the next chapter.";return;
            }
            var next=manager.NextHintPart();if(!next){SetCaption("Choose an assembly chapter to begin.");context.text="";return;}
            var cue=EngineLearningContent.ForPart(next.Definition);CurrentCueId=cue.id;
            if(stepTitle && (titleCue!=cue.id || titleChapter!=manager.CurrentChapterId)){titleCue=cue.id;titleChapter=manager.CurrentChapterId;var steps=EngineLearningContent.Data.steps.Where(s=>s.chapterId==manager.CurrentChapterId).ToList();stepTitle.text="STEP "+(steps.FindIndex(s=>s.id==cue.id)+1).ToString("00")+" / "+steps.Count.ToString("00");}
            int total=cue.partIds.Count(id=>manager.FindPart(id)),done=cue.partIds.Count(manager.IsPlaced);
            if(manager.Task==AssemblyTask.Disassemble)
                SetCaption("Remove "+next.PartDisplayName+" from its mounting position. Work in reverse assembly order.");
            else SetCaption(manager.RequiresWorkpieceOrientation?cue.caption:AccessibleCaption(cue.caption));
            context.text="Next: "+next.PartDisplayName+(total>1?"   ·   "+done+" / "+total:"");
            if(!manager.OrientationReady(next.Definition)){string turn=next.Definition.orientation==AssemblyOrientation.UpsideDown?"Turn the workpiece upside down":"Turn the workpiece upright";context.text=turn+(WorkshopTutorial.DetectPlatform(manager.gameObject)=="PC"?" · F":" · Red FLIP button");}
        }
        void SetCaption(string text){if(!narrator)narrator=manager.GetComponent<AssemblyNarrator>();if(narrator && narrator.Source && narrator.Source.isPlaying && NarrationText.Find(narrator.CurrentCueId)!=null)return;caption.text=text;}
        public static string AccessibleCaption(string text)=>text.Replace("Turn the engine upright. ","").Replace("Turn the engine upside down. ","").Replace("Turn the block upside down. ","").Replace("Turn the workpiece upright. ","").Replace("Turn the workpiece upside down. ","");
    }
}
