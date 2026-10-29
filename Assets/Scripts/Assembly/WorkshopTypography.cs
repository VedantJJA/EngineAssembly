using TMPro;
using UnityEngine;

namespace EngineAssembly
{
    public static class WorkshopTypography
    {
        // Match the readable assembly caption baseline on controller-operated VR panels.
        public const float VRMinimum=23;
        public static void ApplyVR(Canvas canvas)
        {
            if(!canvas)return;
            foreach(var text in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                // Controller-operated panels should not advertise keyboard shortcuts.
                switch(text.text)
                {
                    case "CHAPTERS / TAB":text.text="CHAPTERS";break;
                    case "PAUSE / ESC":text.text="PAUSE";break;
                    case "NEXT STEP  ·  N":text.text="NEXT STEP";break;
                    case "Hold ALT to use these controls":text.text="Point with a controller and press trigger";break;
                    case "RETURN  /  ESC":text.text="RETURN";break;
                }
                if(text.text?.StartsWith("TAB  Chapters     F6")==true)text.text="Point at a card with a controller and press the trigger to select.";
                if(text.fontSize<VRMinimum)text.fontSize=VRMinimum;
                if(text.enableAutoSizing){text.fontSizeMin=Mathf.Max(VRMinimum,text.fontSizeMin);text.fontSizeMax=Mathf.Max(text.fontSizeMin,text.fontSizeMax);}
                // Old 22-26 pixel label boxes cannot contain one 23 pixel TMP line.
                // Ellipsis then hides the entire line, producing apparently blank controls.
                if(text.rectTransform.rect.height<34)text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,34);
            }
        }
    }
}

