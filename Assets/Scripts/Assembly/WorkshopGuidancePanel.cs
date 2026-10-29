using TMPro;
using UnityEngine;

namespace EngineAssembly
{
    public sealed class WorkshopGuidancePanel:MonoBehaviour
    {
        TextMeshProUGUI hints,arrow,captions;
        public bool IsOpen=>gameObject.activeSelf;
        int actionRows;
        public void AddActionRow(string left,System.Action leftAction,string right,System.Action rightAction)
        {
            float y=630+actionRows++*76;Card(transform,left,leftAction,24,y,348,58);Card(transform,right,rightAction,388,y,348,58);((RectTransform)transform).sizeDelta=new Vector2(760,640+actionRows*76);
        }
        public static WorkshopGuidancePanel Create(Transform parent)
        {
            var rect=new GameObject("Guidance settings",typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(760,640);
            var background=rect.gameObject.AddComponent<UnityEngine.UI.Image>();background.color=WorkshopTheme.Surface;WorkshopUIArt.Card(background);
            var panel=rect.gameObject.AddComponent<WorkshopGuidancePanel>();
            Label(rect,"SETTINGS",28,24,20,480,44);
            Card(rect,"DONE",()=>panel.SetOpen(false),612,20,124,48);
            Label(rect,"Placement · Easy",21,24,84,700,34);
            panel.hints=Card(rect,"",()=>WorkshopGuidance.SetHints(!WorkshopGuidance.Hints),24,140,712,58);
            Label(rect,"Show matching fitting targets and the next component.",20,24,208,712,34);
            panel.arrow=Card(rect,"",()=>WorkshopGuidance.SetArrow(!WorkshopGuidance.Arrow),24,258,712,58);
            Label(rect,"Point toward the next component or its fitting position.",20,24,326,712,34);
            WorkshopVolumeSlider.Create(rect,24,386,712);
            panel.captions=Card(rect,"",()=>WorkshopGuidance.SetCaptions(!WorkshopGuidance.Captions),24,486,712,58);
            Label(rect,"Guidance choices are shared across experiences.",20,24,574,712,38);
            panel.Refresh();WorkshopGuidance.Changed+=panel.Refresh;panel.SetOpen(false);return panel;
        }
        public void SetOpen(bool value){gameObject.SetActive(value);if(value)WorkshopPractice.Record("menu");}
        void Refresh(){if(hints)hints.text="GHOST HINTS · "+(WorkshopGuidance.Hints?"ON":"OFF");if(arrow)arrow.text="DIRECTION ARROW · "+(WorkshopGuidance.Arrow?"ON":"OFF");if(captions)captions.text="CAPTIONS · "+(WorkshopGuidance.Captions?"ON":"OFF");}
        void OnDestroy()=>WorkshopGuidance.Changed-=Refresh;
        static RectTransform Rect(Transform parent,string name,float x,float y,float width,float height)
        {var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.up;rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);return rect;}
        static TextMeshProUGUI Label(Transform parent,string text,float size,float x,float y,float width,float height)
        {var rect=Rect(parent,text,x,y,width,height);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();label.font=Resources.Load<TMP_FontAsset>("WorkshopBody");label.text=text;label.fontSize=size;label.color=WorkshopTheme.Text;label.raycastTarget=false;return label;}
        static TextMeshProUGUI Card(Transform parent,string text,System.Action action,float x,float y,float width,float height)
        {
            var rect=Rect(parent,text.Length>0?text:"Guidance toggle",x,y,width,height);var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=WorkshopTheme.AccentWash;WorkshopUIArt.Card(image);
            var card=rect.gameObject.AddComponent<WorkshopCard>();card.normal=WorkshopTheme.AccentWash;card.highlight=WorkshopTheme.Hover;card.clicked=action;
            var label=Label(rect,text,23,12,6,width-24,height-12);label.alignment=TextAlignmentOptions.Center;return label;
        }
    }
}
