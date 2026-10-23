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
        bool mobile,shown=true;
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
            var live=rect.gameObject.AddComponent<LiveNarrationCaption>();live.Label=presenter.caption;live.Assembly=manager.GetComponent<AssemblyNarrator>();
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
            if(this.mobile)
            {
                var rect=(RectTransform)transform;bool landscape=Screen.width>Screen.height;
                rect.anchorMin=landscape?new Vector2(.02f,1):new Vector2(.025f,1);rect.anchorMax=landscape?new Vector2(.02f,1):new Vector2(.975f,1);rect.pivot=landscape?Vector2.up:new Vector2(.5f,1);
                rect.sizeDelta=new Vector2(landscape?590:0,194);rect.anchoredPosition=new Vector2(0,landscape?-126:-194);
            }
            var mobile=manager.GetComponent<MobileAssemblyController>();
            var desktopUI=manager.GetComponent<AssemblyWorkshopUI>();
            if(!this.mobile && (!desktopUI || !desktopUI.IsOpen) && InputHelper.IsKeyDown(KeyCode.P))ToggleInstructions();
            bool visible=shown && !manager.DisplayActive && manager.Mode==WorkshopMode.Assembly && (!mobile || !mobile.ChapterMenuOpen);
            GetComponent<UnityEngine.UI.Image>().enabled=visible;
            foreach(var text in GetComponentsInChildren<TextMeshProUGUI>())text.enabled=visible;if(!visible)return;
            if(manager.RepairActive)
            {
                var repair=manager.Repair;CurrentCueId=repair.Complete?"repair-complete":"repair-step-"+repair.CompletedSteps;
                if(stepTitle)stepTitle.text=repair.Complete?"REPAIR COMPLETE":"REPAIR · STEP "+(repair.CompletedSteps+1).ToString("00")+" / "+repair.StepCount;
                SetCaption(repair.Complete?"Repair complete. The replacement bearing is fitted and the timing drive is rebuilt.":repair.CurrentStep.instruction);
                context.text=repair.Complete?"TAB to choose another chapter.":repair.IsPlacementStep?"Next: "+repair.NextPart.PartDisplayName:"Pick up the highlighted part, then G to set aside.";
                return;
            }
            if(manager.ChapterComplete)
            {
                if(stepTitle)stepTitle.text="CHAPTER COMPLETE";
                CurrentCueId="complete";SetCaption(manager.Task==AssemblyTask.Assemble?"Chapter complete. Your section is assembled.":"Section disassembled.");
                context.text=mobile?"Tap CONTINUE for the next chapter.":"Press ENTER to continue to the next chapter.";return;
            }
            var next=manager.NextHintPart();if(!next){SetCaption("Choose an assembly chapter to begin.");context.text="";return;}
            var cue=EngineLearningContent.ForPart(next.Definition);CurrentCueId=cue.id;
            if(stepTitle){var steps=EngineLearningContent.Data.steps.Where(s=>s.chapterId==manager.CurrentChapterId).ToList();stepTitle.text="STEP "+(steps.FindIndex(s=>s.id==cue.id)+1).ToString("00")+" / "+steps.Count.ToString("00");}
            int total=cue.partIds.Count(id=>manager.FindPart(id)),done=cue.partIds.Count(manager.IsPlaced);
            if(manager.Task==AssemblyTask.Disassemble)
                SetCaption("Remove "+next.PartDisplayName+" from its mounting position. Work in reverse assembly order.");
            else SetCaption(manager.RequiresWorkpieceOrientation?cue.caption:AccessibleCaption(cue.caption));
            context.text="Next: "+next.PartDisplayName+(total>1?"   ·   "+done+" / "+total:"");
            if(!manager.OrientationReady(next.Definition))context.text=next.Definition.orientation==AssemblyOrientation.UpsideDown?"Turn this workpiece upside down · F":"Turn this workpiece upright · F";
        }
        void SetCaption(string text){var narrator=manager.GetComponent<AssemblyNarrator>();if(narrator && narrator.Source && narrator.Source.isPlaying && NarrationText.Find(narrator.CurrentCueId)!=null)return;caption.text=text;}
        public static string AccessibleCaption(string text)=>text.Replace("Turn the engine upright. ","").Replace("Turn the engine upside down. ","").Replace("Turn the block upside down. ","").Replace("Turn the workpiece upright. ","").Replace("Turn the workpiece upside down. ","");
    }
}
