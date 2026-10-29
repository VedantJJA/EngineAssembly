using TMPro;
using UnityEngine;
namespace EngineAssembly
{
    [RequireComponent(typeof(AssemblyManager))]
    public sealed class PCDisplayChapter : MonoBehaviour
    {
        public const string ChapterId="engine-display";
        AssemblyManager manager;RectTransform card,controls,settingsPanel;bool settingsOpen;TextMeshProUGUI title,description,playLabel,sectionLabel,cycleLabel,infoTab,motionTab,audioLabel,speedLabel;NarrationSpeech speech;AssemblyPickupQuery query;
        EngineOperatingAnimation operation;EngineOperationNarrator explanation;PCDisplayTimeline timeline;AssemblyTimelineScrubber scrubber;bool motion;int shownStep=-2;string spokenStep,currentVoice;
        public PCDisplayTimeline Timeline=>timeline;
        public EngineOperatingAnimation Operation=>operation;
        public EngineOperationNarrator Explanation=>explanation;
        public bool MotionMode=>motion;
        public bool SettingsOpen=>manager && manager.DisplayActive && settingsOpen;
        public EngineDisplayStation Station {get;private set;}
        public bool TimelineAudioEnabled=>DisplayPlaybackSettings.AudioEnabled;
        public void SetTimelineAudio(bool enabled){DisplayPlaybackSettings.AudioEnabled=enabled;if(!enabled && !motion)speech.Stop();spokenStep=null;if(audioLabel)audioLabel.text="TIMELAPSE AUDIO · "+(enabled?"ON":"OFF");if(enabled && timeline && timeline.Playing)ShowTimelineStep(timeline.Current);}
        public void SetPlaybackSpeed(float speed){DisplayPlaybackSettings.Speed=speed;if(speedLabel)speedLabel.text="ASSEMBLY SPEED · "+DisplayPlaybackSettings.Speed.ToString("0.00")+"×";}
        void Awake(){manager=GetComponent<AssemblyManager>();speech=gameObject.AddComponent<NarrationSpeech>();EnsureChapter();}
        public void EnsureChapter(){if(manager.Recipe!=null && !manager.Recipe.chapters.Exists(c=>c.id==ChapterId))manager.Recipe.chapters.Add(new ChapterDefinition{id=ChapterId,title="Engine display",description="Explore the complete engine. Select a component to hear how it works."});}
        void Update()
        {
            EnsureChapter();var ui=manager.GetComponent<AssemblyWorkshopUI>();bool shown=manager.DisplayActive && (!ui || !ui.IsOpen);
            if(manager.DisplayActive){CreateCard();if(!Station)Station=EngineDisplayStation.Create(this,manager);}else StopAnimation();
            if(Station)Station.gameObject.SetActive(manager.DisplayActive);
            if(card)card.gameObject.SetActive(shown && !WorkshopTutorial.IsOpen && (!string.IsNullOrEmpty(currentVoice) || motion || timeline.Active && timeline.Position<1));if(controls)controls.gameObject.SetActive(shown);if(settingsPanel)settingsPanel.gameObject.SetActive(shown && settingsOpen && !WorkshopTutorial.IsOpen);
            if(!manager.DisplayActive && speech.Source.isPlaying)speech.Stop();
            if(!operation || !shown)return;
            playLabel.text=motion?(explanation.Active?(explanation.Paused?"RESUME EXPLANATION":"PAUSE EXPLANATION"):operation.Paused?"PLAY ENGINE":"PAUSE ENGINE"):(timeline.Playing?"PAUSE TIMELAPSE":"PLAY TIMELAPSE");
            sectionLabel.text="CAPTIONS · "+(WorkshopGuidance.Captions?"ON":"OFF");
            if(motion)cycleLabel.text=operation.CycleSummary;
            else {int step=timeline.Active?Mathf.FloorToInt(timeline.Position*timeline.StepCount):-1;if(step!=shownStep){shownStep=step;cycleLabel.text=step<0?"Choose a mode in Display Settings":"Assembly timeline · "+step+" / "+timeline.StepCount;}}
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
            if(!manager.DisplayActive)return;CreateCard();if(motion==value && (!value || operation.Active))return;explanation.Cancel();speech.Stop();currentVoice=null;shownStep=-2;motion=value;
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
            Physics.SyncTransforms();
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
            EngineSurfaceLibrary.Apply(manager.AssemblyRoot);
            var captions=EngineDisplayCaption.Create(canvas.transform,speech,false);card=(RectTransform)captions.transform;title=captions.Heading;description=captions.Text;
            title.text="Complete engine";description.text="Select a component to hear its purpose. Choose Info / Timeline to watch the build, or Engine Motion to see how the engine works.";captions.Context.text="";
            operation=gameObject.GetComponent<EngineOperatingAnimation>()??gameObject.AddComponent<EngineOperatingAnimation>();operation.Configure(manager.AssemblyRoot,manager.Recipe,Camera.main);
            explanation=gameObject.GetComponent<EngineOperationNarrator>()??gameObject.AddComponent<EngineOperationNarrator>();explanation.Configure(operation,speech,(heading,text)=>{title.text=heading;description.text=text;});
            timeline=gameObject.GetComponent<PCDisplayTimeline>()??gameObject.AddComponent<PCDisplayTimeline>();timeline.Configure(manager,speech);timeline.StepChanged=ShowTimelineStep;
            controls=Rect("Engine operating controls",canvas.transform,new Vector2(850,92));controls.anchorMin=controls.anchorMax=new Vector2(.5f,0);controls.pivot=new Vector2(.5f,0);controls.anchoredPosition=new Vector2(0,20);
            playLabel=Control("PLAY ENGINE",TogglePlay,0,0,228);
            Control("DISPLAY SETTINGS",()=>settingsOpen=!settingsOpen,650,0,200).text="SETTINGS";
            cycleLabel=Text(controls,"Display progress",19,0,58,850,28);cycleLabel.alignment=TextAlignmentOptions.Center;cycleLabel.color=WorkshopTheme.OverlayText;cycleLabel.outlineColor=new Color(.03f,.06f,.10f,.95f);cycleLabel.outlineWidth=.18f;cycleLabel.UpdateMeshPadding();
            scrubber=Rail(controls,"Display timeline scrubber",250,18,376,value=>{explanation.Cancel();speech.Stop();if(motion){operation.Paused=true;operation.SetCrankAngle(value*720);}else {timeline.Playing=false;timeline.SetValue(value);}});
            settingsPanel=Rect("Display settings",canvas.transform,new Vector2(800,630));settingsPanel.anchorMin=settingsPanel.anchorMax=new Vector2(.5f,.5f);settingsPanel.pivot=new Vector2(.5f,.5f);var panelImage=settingsPanel.gameObject.AddComponent<UnityEngine.UI.Image>();panelImage.color=WorkshopTheme.Surface;WorkshopUIArt.Card(panelImage);
            Text(settingsPanel,"ENGINE DISPLAY",28,24,568,600,40);Control("DONE",()=>settingsOpen=false,644,568,132,settingsPanel);
            infoTab=Control("INFO / TIMELINE",()=>SetMotionMode(false),24,500,366,settingsPanel);motionTab=Control("ENGINE MOTION",()=>SetMotionMode(true),410,500,366,settingsPanel);
            audioLabel=Control("TIMELAPSE AUDIO · "+(TimelineAudioEnabled?"ON":"OFF"),()=>SetTimelineAudio(!TimelineAudioEnabled),24,426,366,settingsPanel);
            sectionLabel=Control("CAPTIONS",()=>WorkshopGuidance.SetCaptions(!WorkshopGuidance.Captions),410,426,366,settingsPanel);
            speedLabel=Text(settingsPanel,"",23,24,358,752,36);SetPlaybackSpeed(DisplayPlaybackSettings.Speed);
            var speed=Rail(settingsPanel,"Assembly speed slider",28,330,744,value=>SetPlaybackSpeed(.1f+value*3.9f));speed.SetValue((DisplayPlaybackSettings.Speed-.1f)/3.9f);
            var engineSpeedLabel=Text(settingsPanel,"Engine movement · 12 rpm",23,24,266,752,36);
            var engineSpeed=Rail(settingsPanel,"Engine movement speed",28,234,744,value=>{operation.SetSpeed(4+value*26);engineSpeedLabel.text="Engine movement · "+operation.TeachingRPM.ToString("0")+" rpm";});engineSpeed.SetValue(8f/26);
            Control("EXPLAIN ENGINE CYCLE",ExplainEngine,24,144,366,settingsPanel);Control("ASSEMBLED VIEW",()=>{StopAnimation();settingsOpen=false;},410,144,366,settingsPanel);
            Text(settingsPanel,"Select a display mode, then use Play and the timeline below the engine.",23,24,48,752,70);
            settingsPanel.gameObject.SetActive(false);if(QuestVRSession.Instance && QuestVRSession.Instance.Active)WorkshopTypography.ApplyVR(canvas);
        }
        static RectTransform Rect(string name,Transform parent,Vector2 size){var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.sizeDelta=size;return rect;}
        TextMeshProUGUI Control(string text,System.Action action,float x,float y=0,float width=276,RectTransform parent=null)
        {
            var rect=Rect(text,parent?parent:controls,new Vector2(width,48));rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=Vector2.zero;rect.anchoredPosition=new Vector2(x,y);
            var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=WorkshopTheme.AccentWash;WorkshopUIArt.Card(image);var button=rect.gameObject.AddComponent<WorkshopCard>();button.normal=WorkshopTheme.AccentWash;button.highlight=WorkshopTheme.Hover;button.clicked=action;
            var tmp=Text(rect,text,22,8,2,width-16,44);tmp.alignment=TextAlignmentOptions.Center;return tmp;
        }
        static TextMeshProUGUI Text(RectTransform parent,string text,float size,float x,float y,float width,float height)
        {
            var rect=Rect(text,parent,new Vector2(width,height));rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=Vector2.zero;rect.anchoredPosition=new Vector2(x,y);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();label.font=Resources.Load<TMP_FontAsset>("WorkshopBody");label.text=text;label.fontSize=size;label.color=WorkshopTheme.Text;label.raycastTarget=false;return label;
        }
        static AssemblyTimelineScrubber Rail(RectTransform parent,string name,float x,float y,float width,System.Action<float> changed)
        {
            var rail=Rect(name,parent,new Vector2(width,14));rail.anchorMin=rail.anchorMax=Vector2.zero;rail.pivot=Vector2.zero;rail.anchoredPosition=new Vector2(x,y);var image=rail.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=WorkshopTheme.Border;image.raycastPadding=new Vector4(-8,-14,-8,-14);WorkshopUIArt.Card(image);
            var fill=Rect("Fill",rail,Vector2.zero);fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;var fillImage=fill.gameObject.AddComponent<UnityEngine.UI.Image>();fillImage.color=WorkshopTheme.Accent;fillImage.raycastTarget=false;
            var thumb=Rect("Thumb",rail,new Vector2(20,30));var thumbImage=thumb.gameObject.AddComponent<UnityEngine.UI.Image>();thumbImage.color=WorkshopTheme.Text;thumbImage.raycastTarget=false;WorkshopUIArt.Card(thumbImage);
            var scrubber=rail.gameObject.AddComponent<AssemblyTimelineScrubber>();scrubber.Configure(fill,thumb);scrubber.changed=changed;return scrubber;
        }
        static void TabTint(TextMeshProUGUI label,bool selected){var card=label.transform.parent.GetComponent<WorkshopCard>();var colour=selected?WorkshopTheme.AccentWash:WorkshopTheme.Surface;if(card.normal==colour)return;card.normal=colour;card.GetComponent<UnityEngine.UI.Image>().color=colour;}
        TextMeshProUGUI Label(string name,Vector2 min,Vector2 max,int size){var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(card,false);rect.anchorMin=Vector2.up;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(min.x,max.y);rect.offsetMax=new Vector2(max.x,min.y);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();label.font=Resources.Load<TMP_FontAsset>("WorkshopBody");label.color=WorkshopTheme.Text;label.fontSize=size;label.raycastTarget=false;return label;}
    }
}
