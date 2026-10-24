using TMPro;
using UnityEngine;
namespace EngineAssembly
{
    // Shares the assembly instruction card's typography, layout and timed word highlighting.
    public sealed class EngineDisplayCaption:MonoBehaviour
    {
        public TextMeshProUGUI Heading,Text,Context;
        bool mobile;Vector2Int layoutSize;bool laidOut;
        public static EngineDisplayCaption Create(Transform parent,NarrationSpeech speech,bool mobile)
        {
            var rect=new GameObject("Display captions",typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=WorkshopTheme.Surface;image.raycastTarget=false;WorkshopUIArt.Card(image);
            var caption=rect.gameObject.AddComponent<EngineDisplayCaption>();caption.mobile=mobile;
            caption.Heading=Label(rect,"Caption heading",15,0,26,mobile);caption.Heading.fontStyle=FontStyles.Normal;
            caption.Text=Label(rect,"Spoken caption",mobile?22:23,32,mobile?130:170,mobile);
            caption.Context=Label(rect,"Caption context",16,mobile?163:210,26,mobile);caption.Context.color=WorkshopTheme.Accent;caption.Context.fontStyle=FontStyles.Normal;caption.Context.textWrappingMode=TextWrappingModes.NoWrap;caption.Context.overflowMode=TextOverflowModes.Ellipsis;
            var live=rect.gameObject.AddComponent<LiveNarrationCaption>();live.Label=caption.Text;live.Speaker=speech;caption.Layout();return caption;
        }
        static TextMeshProUGUI Label(RectTransform parent,string name,float size,float top,float height,bool mobile)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=Vector2.up;rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-top);rect.sizeDelta=new Vector2(-28,height);
            var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=Resources.Load<TMP_FontAsset>("WorkshopHeading");text.fontSize=size;text.fontStyle=FontStyles.Bold;text.color=WorkshopTheme.Text;text.raycastTarget=false;text.enableAutoSizing=true;text.fontSizeMin=size-4;text.fontSizeMax=size;text.alignment=TextAlignmentOptions.Left;
            text.outlineColor=new Color(1,1,1,.85f);text.outlineWidth=.1f;var shadow=rect.gameObject.AddComponent<UnityEngine.UI.Shadow>();shadow.effectColor=new Color(0,0,0,.16f);shadow.effectDistance=new Vector2(1,-1);return text;
        }
        void Layout()
        {
            var size=new Vector2Int(Screen.width,Screen.height);if(laidOut && layoutSize==size)return;laidOut=true;layoutSize=size;
            var rect=(RectTransform)transform;if(!mobile){rect.anchorMin=rect.anchorMax=Vector2.one;rect.pivot=Vector2.one;rect.sizeDelta=new Vector2(540,248);rect.anchoredPosition=new Vector2(-28,-144);return;}
            bool landscape=Screen.width>Screen.height;rect.anchorMin=landscape?new Vector2(.02f,1):new Vector2(.025f,1);rect.anchorMax=landscape?new Vector2(.02f,1):new Vector2(.975f,1);rect.pivot=landscape?Vector2.up:new Vector2(.5f,1);rect.sizeDelta=new Vector2(landscape?590:0,194);rect.anchoredPosition=new Vector2(0,landscape?-126:-226);
        }
        void LateUpdate()=>Layout();
    }
}
