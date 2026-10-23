using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EngineAssembly
{
    // A custom timeline rail rather than a stock selectable/slider skin.
    public sealed class AssemblyTimelineScrubber : MonoBehaviour,IPointerDownHandler,IDragHandler,IScrollHandler
    {
        public Action<float> changed;
        RectTransform fill,thumb;
        float value;
        public float Value=>value;
        public void Configure(RectTransform fill,RectTransform thumb){this.fill=fill;this.thumb=thumb;SetValue(0);}
        public void SetValue(float position)
        {
            value=Mathf.Clamp01(position);
            if(fill){fill.anchorMax=new Vector2(value,1);fill.offsetMax=Vector2.zero;}
            if(thumb){thumb.anchorMin=thumb.anchorMax=new Vector2(value,.5f);thumb.anchoredPosition=Vector2.zero;}
        }
        void Apply(PointerEventData e)
        {
            var rect=(RectTransform)transform;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,e.position,e.pressEventCamera,out var local))
            {SetValue(Mathf.InverseLerp(rect.rect.xMin,rect.rect.xMax,local.x));changed?.Invoke(value);}
        }
        public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)Apply(e);}
        public void OnDrag(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)Apply(e);}
        public void OnScroll(PointerEventData e){SetValue(value+e.scrollDelta.y*.01f);changed?.Invoke(value);}
    }
}
