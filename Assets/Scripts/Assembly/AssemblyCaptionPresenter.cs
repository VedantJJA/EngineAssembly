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
            rect.anchorMin=new Vector2(mobile?.025f:1,1);rect.anchorMax=new Vector2(mobile?.975f:1,1);
            rect.pivot=new Vector2(mobile?.5f:1,1);rect.sizeDelta=new Vector2(mobile?0:430,mobile?138:246);rect.anchoredPosition=new Vector2(mobile?0:-28,mobile?-194:-28);
            var background=rect.gameObject.AddComponent<UnityEngine.UI.Image>();background.color=new Color(.018f,.029f,.042f,.86f);background.raycastTarget=false;
            WorkshopUIArt.Card(background);
            var presenter=rect.gameObject.AddComponent<AssemblyCaptionPresenter>();presenter.manager=manager;presenter.mobile=mobile;
            presenter.caption=Label(rect,"Caption",mobile?24:20,new Vector2(16,mobile?38:64),new Vector2(-16,mobile?-14:-50));
            if(!mobile)
            {
                presenter.caption.alignment=TextAlignmentOptions.TopLeft;
                presenter.stepTitle=Label(rect,"Step heading",16,new Vector2(16,204),new Vector2(-16,-12));presenter.stepTitle.rectTransform.anchorMin=new Vector2(0,1);presenter.stepTitle.rectTransform.offsetMin=new Vector2(16,-42);presenter.stepTitle.alignment=TextAlignmentOptions.Left;presenter.stepTitle.color=new Color(.53f,.94f,.78f);
                var toggle=Label(rect,"Steps toggle",13,new Vector2(16,4),new Vector2(-16,24));toggle.rectTransform.anchorMax=new Vector2(1,0);toggle.rectTransform.offsetMax=new Vector2(-16,24);toggle.text="P to hide steps";toggle.fontStyle=FontStyles.Normal;
            }
            presenter.context=Label(rect,"Instruction progress",mobile?16:16,new Vector2(16,7),new Vector2(-16,35));
            presenter.context.rectTransform.anchorMax=new Vector2(1,0);presenter.context.rectTransform.offsetMin=new Vector2(16,mobile?7:27);presenter.context.rectTransform.offsetMax=new Vector2(-16,mobile?35:57);
            presenter.context.color=new Color(.53f,.94f,.78f);presenter.context.fontStyle=FontStyles.Normal;
            presenter.context.textWrappingMode=TextWrappingModes.NoWrap;presenter.context.overflowMode=TextOverflowModes.Ellipsis;
            return presenter;
        }
        static TextMeshProUGUI Label(RectTransform parent,string name,float size,Vector2 low,Vector2 high)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=low;rect.offsetMax=high;
            var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=Resources.Load<TMP_FontAsset>("WorkshopHeading");text.fontSize=size;text.color=Color.white;text.fontStyle=FontStyles.Bold;
            text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;
            text.enableAutoSizing=true;text.fontSizeMin=size-5;text.fontSizeMax=size;
            var shadow=rect.gameObject.AddComponent<UnityEngine.UI.Shadow>();shadow.effectColor=new Color(0,0,0,.95f);shadow.effectDistance=new Vector2(2,-3);
            return text;
        }
        void LateUpdate()
        {
            if(!manager || manager.Recipe==null)return;
            var mobile=manager.GetComponent<MobileAssemblyController>();
            var desktopUI=manager.GetComponent<AssemblyWorkshopUI>();
            if(!this.mobile && (!desktopUI || !desktopUI.IsOpen) && InputHelper.IsKeyDown(KeyCode.P))ToggleInstructions();
            bool visible=shown && manager.Mode==WorkshopMode.Assembly && (!mobile || !mobile.ChapterMenuOpen);
            foreach(var text in GetComponentsInChildren<TextMeshProUGUI>())text.enabled=visible;GetComponent<UnityEngine.UI.Image>().enabled=visible;if(!visible)return;
            if(manager.RepairActive)
            {
                var repair=manager.Repair;CurrentCueId="repair-step-"+repair.CompletedSteps;
                if(stepTitle)stepTitle.text=repair.Complete?"REPAIR COMPLETE":"REPAIR · STEP "+(repair.CompletedSteps+1).ToString("00")+" / "+repair.StepCount;
                caption.text=repair.Complete?"Repair complete. The replacement bearing is fitted and the timing drive is rebuilt.":repair.CurrentStep.instruction;
                context.text=repair.Complete?"TAB to choose another chapter.":repair.IsPlacementStep?"Next: "+repair.NextPart.PartDisplayName:"Pick up the highlighted part, then G to set aside.";
                return;
            }
            if(manager.ChapterComplete)
            {
                if(stepTitle)stepTitle.text="CHAPTER COMPLETE";
                CurrentCueId="complete";caption.text=manager.Task==AssemblyTask.Assemble?"Chapter complete. Your section is assembled.":"Section disassembled.";
                context.text=mobile?"Tap CONTINUE for the next chapter.":"Press ENTER to continue to the next chapter.";return;
            }
            var next=manager.NextHintPart();if(!next){caption.text="Choose an assembly chapter to begin.";context.text="";return;}
            var cue=EngineLearningContent.ForPart(next.Definition);CurrentCueId=cue.id;
            if(stepTitle){var steps=EngineLearningContent.Data.steps.Where(s=>s.chapterId==manager.CurrentChapterId).ToList();stepTitle.text="STEP "+(steps.FindIndex(s=>s.id==cue.id)+1).ToString("00")+" / "+steps.Count.ToString("00");}
            int total=cue.partIds.Count(id=>manager.FindPart(id)),done=cue.partIds.Count(manager.IsPlaced);
            if(manager.Task==AssemblyTask.Disassemble)
                caption.text="Remove "+next.PartDisplayName+" from its mounting position. Work in reverse assembly order.";
            else caption.text=cue.caption;
            string orientation="";
            float up=Vector3.Dot(manager.TargetFrame(next.Definition).up,Vector3.up);
            if((next.Definition.orientation==AssemblyOrientation.Upright && up<.85f) || (next.Definition.orientation==AssemblyOrientation.UpsideDown && up>-.85f))
                orientation=(next.Definition.orientation==AssemblyOrientation.Upright?"Turn this workpiece upright. ":"Turn this workpiece upside down. ")+(mobile?"Use FLIP or drag. ":"Press F. ");
            context.text=orientation+"Next: "+next.PartDisplayName+(total>1?"   ·   "+done+" / "+total:"");
        }
    }
}
