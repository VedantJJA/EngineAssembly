using TMPro;
using UnityEngine;
namespace EngineAssembly
{
    [RequireComponent(typeof(AssemblyManager))]
    public sealed class PCDisplayChapter : MonoBehaviour
    {
        public const string ChapterId="engine-display";
        AssemblyManager manager;RectTransform card,controls;TextMeshProUGUI title,description,playLabel,sectionLabel,cycleLabel,infoTab,motionTab,audioLabel,speedLabel;NarrationSpeech speech;AssemblyPickupQuery query;
        EngineOperatingAnimation operation;EngineOperationNarrator explanation;PCDisplayTimeline timeline;AssemblyTimelineScrubber scrubber;bool motion;string spokenStep,currentVoice;
        public PCDisplayTimeline Timeline=>timeline;
        public EngineOperatingAnimation Operation=>operation;
        public EngineOperationNarrator Explanation=>explanation;
        public bool TimelineAudioEnabled=>DisplayPlaybackSettings.AudioEnabled;
        public void SetTimelineAudio(bool enabled){DisplayPlaybackSettings.AudioEnabled=enabled;if(!enabled && !motion)speech.Stop();spokenStep=null;if(audioLabel)audioLabel.text="TIMELAPSE AUDIO · "+(enabled?"ON":"OFF");if(enabled && timeline && timeline.Playing)ShowTimelineStep(timeline.Current);}
        public void SetPlaybackSpeed(float speed){DisplayPlaybackSettings.Speed=speed;if(speedLabel)speedLabel.text="ASSEMBLY SPEED · "+DisplayPlaybackSettings.Speed.ToString("0.00")+"×";}
        void Awake(){manager=GetComponent<AssemblyManager>();speech=gameObject.AddComponent<NarrationSpeech>();EnsureChapter();}
        public void EnsureChapter(){if(manager.Recipe!=null && !manager.Recipe.chapters.Exists(c=>c.id==ChapterId))manager.Recipe.chapters.Add(new ChapterDefinition{id=ChapterId,title="Engine display",description="Explore the complete engine. Select a component to hear how it works."});}
        void Update()
        {
            EnsureChapter();var ui=manager.GetComponent<AssemblyWorkshopUI>();bool shown=manager.DisplayActive && (!ui || !ui.IsOpen);
            if(manager.DisplayActive)CreateCard();else StopAnimation();
            if(card)card.gameObject.SetActive(shown);if(controls)controls.gameObject.SetActive(shown);
            if(!manager.DisplayActive && speech.Source.isPlaying)speech.Stop();
            if(!operation)return;
            playLabel.text=motion?(explanation.Active?(explanation.Paused?"RESUME EXPLANATION":"PAUSE EXPLANATION"):operation.Paused?"PLAY ENGINE":"PAUSE ENGINE"):(timeline.Playing?"PAUSE TIMELAPSE":"PLAY TIMELAPSE");
            sectionLabel.text=motion?(operation.SectionEnabled?"SECTION ON":"SECTION OFF"):"REPLAY VOICE";
            cycleLabel.text=motion?operation.CycleSummary:timeline.Active?"Assembly timeline · "+Mathf.FloorToInt(timeline.Position*timeline.StepCount)+" / "+timeline.StepCount:"Select a component, or play the assembly timeline";
            scrubber.SetValue(motion?operation.CrankAngle/720:timeline.Position);
            TabTint(infoTab,!motion);TabTint(motionTab,motion);
            if(operation.Active && !operation.Paused && !explanation.Active){title.text="Inside the running engine";description.text=operation.Explanation;}
        }
        public void ToggleEngineAnimation()
        {
            SetMotionMode(true);TogglePlay();
        }
        public void SetMotionMode(bool value)
        {
            if(!manager.DisplayActive)return;CreateCard();if(motion==value && (!value || operation.Active))return;explanation.Cancel();speech.Stop();currentVoice=null;motion=value;
            if(value){timeline.Stop();operation.Play();operation.Paused=true;title.text="Inside the running engine";description.text=operation.Explanation;}
            else {operation.Stop();timeline.SetValue(timeline.Position);ShowTimelineStep(timeline.Current);}
        }
        public void TogglePlay()
        {
            if(!manager.DisplayActive)return;CreateCard();if(motion && explanation.Active){explanation.SetPaused(!explanation.Paused);return;}speech.Stop();if(motion)operation.Paused=!operation.Paused;else {spokenStep=null;timeline.TogglePlay();}
        }
        public void ExplainEngine(){SetMotionMode(true);explanation.Play();}
        void ShowTimelineStep(PartDefinition part)
        {
            if(part==null)return;var detail=DisplayPlaybackSettings.Description(part);string voice=detail.voiceId;
            currentVoice=voice;title.text=part.displayName;if(!speech.IsBusy)description.text=detail.description;
            if(timeline.Playing && TimelineAudioEnabled && spokenStep!=voice){spokenStep=voice;speech.Speak(voice);}
        }
        public void StopAnimation(){bool active=operation && operation.Active || timeline && timeline.Active;if(!active)return;explanation?.Cancel();speech.Stop();spokenStep=null;currentVoice=null;operation?.Stop();timeline?.Stop(true);motion=false;if(title)title.text="Complete engine";if(description)description.text="Select a component to hear its purpose, or play the timeline to watch the engine being assembled.";}
        void ToggleSection(){if(motion)operation.SectionEnabled=!operation.SectionEnabled;else ReplayVoice();}
        public void ReplayVoice(){if(!motion && !string.IsNullOrEmpty(currentVoice) && (!timeline.Playing || TimelineAudioEnabled))speech.Speak(currentVoice);}
        public bool Inspect(Ray ray)
        {
            if(!manager.DisplayActive)return false;
            if(!query){var player=FindAnyObjectByType<PlayerAssemblyController>();query=player?player.GetComponent<AssemblyPickupQuery>():gameObject.AddComponent<AssemblyPickupQuery>();}
            // Ignore only the surfaces removed by the display shader. Physical walls still block selection.
            for(int i=0;i<32;i++)
            {
                if(!query.Raycast(ray,20,~0,out var part,out var point,out _) || !part || part.Manager!=manager)return false;
                var mesh=part.GetComponentInChildren<MeshRenderer>();
                if(operation && operation.IsClipped(mesh,point) || timeline && timeline.IsFaded(mesh)){ray.origin=point+ray.direction*.001f;continue;}
                var detail=EngineLearningContent.Detail(part.Definition);CreateCard();explanation.Cancel();if(operation.Active)operation.Paused=true;timeline.Playing=false;currentVoice=detail.voiceId;title.text=detail.title;description.text=detail.description;speech.Speak(detail.voiceId);return true;
            }
            return false;
        }
        void CreateCard()
        {
            if(card)return;var ui=manager.GetComponent<AssemblyWorkshopUI>();var canvas=ui?ui.WorkshopCanvas:manager.GetComponentInChildren<Canvas>();if(!canvas)return;
            var captions=EngineDisplayCaption.Create(canvas.transform,speech,false);card=(RectTransform)captions.transform;title=captions.Heading;description=captions.Text;
            title.text="Complete engine";description.text="Select a component to hear its purpose. Choose Info / Timeline to watch the build, or Engine Motion to see how the engine works.";captions.Context.text="Engine display";
            operation=gameObject.GetComponent<EngineOperatingAnimation>()??gameObject.AddComponent<EngineOperatingAnimation>();operation.Configure(manager.AssemblyRoot,manager.Recipe,Camera.main);
            explanation=gameObject.GetComponent<EngineOperationNarrator>()??gameObject.AddComponent<EngineOperationNarrator>();explanation.Configure(operation,speech,(heading,text)=>{title.text=heading;description.text=text;});
            timeline=gameObject.GetComponent<PCDisplayTimeline>()??gameObject.AddComponent<PCDisplayTimeline>();timeline.Configure(manager,speech);timeline.StepChanged=ShowTimelineStep;
            controls=new GameObject("Engine operating controls",typeof(RectTransform)).GetComponent<RectTransform>();controls.SetParent(canvas.transform,false);controls.anchorMin=controls.anchorMax=new Vector2(.5f,0);controls.pivot=new Vector2(.5f,0);controls.sizeDelta=new Vector2(850,260);controls.anchoredPosition=new Vector2(0,22);
            infoTab=Control("INFO / TIMELINE",()=>SetMotionMode(false),0,114,418);motionTab=Control("ENGINE MOTION",()=>SetMotionMode(true),432,114,418);
            playLabel=Control("PLAY ENGINE",TogglePlay,0);sectionLabel=Control("SECTION OFF",ToggleSection,284);Control("ASSEMBLED VIEW",StopAnimation,568);
            var readout=new GameObject("Four-stroke cycle",typeof(RectTransform)).GetComponent<RectTransform>();readout.SetParent(controls,false);readout.anchorMin=Vector2.zero;readout.anchorMax=Vector2.right;readout.offsetMin=new Vector2(0,82);readout.offsetMax=new Vector2(0,110);cycleLabel=readout.gameObject.AddComponent<TextMeshProUGUI>();cycleLabel.font=Resources.Load<TMP_FontAsset>("WorkshopBody");cycleLabel.fontSize=19;cycleLabel.color=WorkshopTheme.Text;cycleLabel.alignment=TextAlignmentOptions.Center;cycleLabel.raycastTarget=false;
            var rail=new GameObject("Display timeline scrubber",typeof(RectTransform)).GetComponent<RectTransform>();rail.SetParent(controls,false);rail.anchorMin=rail.anchorMax=Vector2.zero;rail.pivot=Vector2.zero;rail.anchoredPosition=new Vector2(12,64);rail.sizeDelta=new Vector2(826,14);var image=rail.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=WorkshopTheme.Border;image.raycastPadding=new Vector4(-8,-10,-8,-10);WorkshopUIArt.Card(image);
            var fill=new GameObject("Fill",typeof(RectTransform)).GetComponent<RectTransform>();fill.SetParent(rail,false);fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;var fillImage=fill.gameObject.AddComponent<UnityEngine.UI.Image>();fillImage.color=WorkshopTheme.Accent;fillImage.raycastTarget=false;
            var thumb=new GameObject("Thumb",typeof(RectTransform)).GetComponent<RectTransform>();thumb.SetParent(rail,false);thumb.sizeDelta=new Vector2(20,30);var thumbImage=thumb.gameObject.AddComponent<UnityEngine.UI.Image>();thumbImage.color=WorkshopTheme.Text;thumbImage.raycastTarget=false;WorkshopUIArt.Card(thumbImage);
            scrubber=rail.gameObject.AddComponent<AssemblyTimelineScrubber>();scrubber.Configure(fill,thumb);scrubber.changed=value=>{explanation.Cancel();speech.Stop();if(motion){operation.Paused=true;operation.SetCrankAngle(value*720);}else {timeline.Playing=false;timeline.SetValue(value);}};
            audioLabel=Control("TIMELAPSE AUDIO · "+(TimelineAudioEnabled?"ON":"OFF"),()=>SetTimelineAudio(!TimelineAudioEnabled),0,170,418);
            speedLabel=Control("ASSEMBLY SPEED · "+DisplayPlaybackSettings.Speed.ToString("0.00")+"×",()=>{},432,170,418);speedLabel.transform.parent.GetComponent<WorkshopCard>().enabled=false;
            var speedRail=Instantiate(rail,controls);speedRail.name="Assembly speed slider";speedRail.anchoredPosition=new Vector2(444,226);speedRail.sizeDelta=new Vector2(394,12);
            var speed=speedRail.GetComponent<AssemblyTimelineScrubber>();speed.Configure((RectTransform)speedRail.Find("Fill"),(RectTransform)speedRail.Find("Thumb"));speed.changed=value=>{SetPlaybackSpeed(.1f+value*3.9f);speed.SetValue(value);};speed.SetValue((DisplayPlaybackSettings.Speed-.1f)/3.9f);
            Control("EXPLAIN ENGINE CYCLE",ExplainEngine,0,220,418);
        }
        TextMeshProUGUI Control(string text,System.Action action,float x,float y=0,float width=276)
        {
            var rect=new GameObject(text,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(controls,false);rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=Vector2.zero;rect.sizeDelta=new Vector2(width,y>0?42:52);rect.anchoredPosition=new Vector2(x,y);
            var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=WorkshopTheme.AccentWash;WorkshopUIArt.Card(image);var button=rect.gameObject.AddComponent<WorkshopCard>();button.normal=WorkshopTheme.AccentWash;button.highlight=WorkshopTheme.Hover;button.clicked=action;
            var label=new GameObject("Label",typeof(RectTransform)).GetComponent<RectTransform>();label.SetParent(rect,false);label.anchorMin=Vector2.zero;label.anchorMax=Vector2.one;label.offsetMin=label.offsetMax=Vector2.zero;var tmp=label.gameObject.AddComponent<TextMeshProUGUI>();tmp.text=text;tmp.font=Resources.Load<TMP_FontAsset>("WorkshopHeading");tmp.fontSize=20;tmp.color=WorkshopTheme.Text;tmp.alignment=TextAlignmentOptions.Center;tmp.raycastTarget=false;return tmp;
        }
        static void TabTint(TextMeshProUGUI label,bool selected){var card=label.transform.parent.GetComponent<WorkshopCard>();card.normal=selected?WorkshopTheme.AccentWash:WorkshopTheme.Surface;card.GetComponent<UnityEngine.UI.Image>().color=card.normal;}
        TextMeshProUGUI Label(string name,Vector2 min,Vector2 max,int size){var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(card,false);rect.anchorMin=Vector2.up;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(min.x,max.y);rect.offsetMax=new Vector2(max.x,min.y);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();label.font=Resources.Load<TMP_FontAsset>("WorkshopBody");label.color=WorkshopTheme.Text;label.fontSize=size;label.raycastTarget=false;return label;}
    }
}
