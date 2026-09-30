using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace EngineAssembly
{
    // Custom card interaction; no stock Button component or selectable transition styling.
    public sealed class WorkshopCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ISubmitHandler, ISelectHandler, IDeselectHandler
    {
        public Action clicked;
        public Color normal,highlight;
        public bool interactable=true;
        UnityEngine.UI.Image background;
        void Awake()=>background=GetComponent<UnityEngine.UI.Image>();
        void Tint(bool selected) { if(background)background.color=selected && interactable?highlight:normal; }
        public void OnPointerEnter(PointerEventData e)=>Tint(true);
        public void OnPointerExit(PointerEventData e)=>Tint(false);
        public void OnSelect(BaseEventData e)=>Tint(true);
        public void OnDeselect(BaseEventData e)=>Tint(false);
        public void OnPointerClick(PointerEventData e) { if(e.button==PointerEventData.InputButton.Left && interactable)clicked?.Invoke(); }
        public void OnSubmit(BaseEventData e) { if(interactable)clicked?.Invoke(); }
    }
}
