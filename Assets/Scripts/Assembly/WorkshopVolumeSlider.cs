using TMPro;
using UnityEngine;
namespace EngineAssembly
{
    public sealed class WorkshopVolumeSlider : MonoBehaviour
    {
        AssemblyTimelineScrubber scrubber;TextMeshProUGUI value;
        public static WorkshopVolumeSlider Create(Transform parent,float x,float y,float width)
        {
            var root=Rect("Volume slider",parent);root.anchorMin=root.anchorMax=Vector2.up;root.pivot=Vector2.up;root.anchoredPosition=new Vector2(x,-y);root.sizeDelta=new Vector2(width,76);
            var control=root.gameObject.AddComponent<WorkshopVolumeSlider>();
            var label=Rect("Volume percentage",root);label.anchorMin=Vector2.zero;label.anchorMax=Vector2.one;label.offsetMin=new Vector2(0,44);label.offsetMax=Vector2.zero;
            control.value=label.gameObject.AddComponent<TextMeshProUGUI>();control.value.font=Resources.Load<TMP_FontAsset>("WorkshopBody");control.value.fontSize=19;control.value.color=WorkshopTheme.Text;control.value.raycastTarget=false;
            var rail=Rect("Volume track",root);rail.anchorMin=new Vector2(0,0);rail.anchorMax=new Vector2(1,0);rail.pivot=new Vector2(.5f,0);rail.offsetMin=new Vector2(12,10);rail.offsetMax=new Vector2(-12,38);
            var image=rail.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=WorkshopTheme.Border;WorkshopUIArt.Card(image);
            var fill=Rect("Volume fill",rail);fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;
            var ink=fill.gameObject.AddComponent<UnityEngine.UI.Image>();ink.color=WorkshopTheme.Accent;ink.raycastTarget=false;WorkshopUIArt.Card(ink);
            var thumb=Rect("Volume handle",rail);thumb.anchorMin=thumb.anchorMax=new Vector2(1,.5f);thumb.sizeDelta=new Vector2(32,32);
            var handle=thumb.gameObject.AddComponent<UnityEngine.UI.Image>();handle.color=Color.white;handle.raycastTarget=false;WorkshopUIArt.Card(handle);
            control.scrubber=rail.gameObject.AddComponent<AssemblyTimelineScrubber>();control.scrubber.Configure(fill,thumb);control.scrubber.changed=WorkshopAudio.SetVolume;
            control.Refresh(WorkshopAudio.Volume);WorkshopAudio.VolumeChanged+=control.Refresh;return control;
        }
        static RectTransform Rect(string name,Transform parent){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);return r;}
        void Refresh(float volume){if(scrubber)scrubber.SetValue(volume);if(value)value.text="SOUND VOLUME   "+Mathf.RoundToInt(volume*100)+"%";}
        void OnDestroy()=>WorkshopAudio.VolumeChanged-=Refresh;
    }
}
