using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace EngineAssembly
{
    public sealed class QuestWristMenu:MonoBehaviour
    {
        public static QuestWristMenu Create(Transform parent,Camera camera,Action menu,Action teleport,Action next)
        {
            var root=new GameObject("Hand wrist controls",typeof(RectTransform));root.transform.SetParent(parent,false);
            var canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;canvas.sortingOrder=600;
            var rect=(RectTransform)root.transform;rect.sizeDelta=new Vector2(420,88);rect.localScale=Vector3.one*.00055f;
            root.AddComponent<TrackedDeviceGraphicRaycaster>();var component=root.AddComponent<QuestWristMenu>();
            Card(rect,"MENU",menu,0);Card(rect,"TELEPORT",teleport,1);Card(rect,"NEXT",next,2);return component;
        }
        static void Card(RectTransform parent,string label,Action action,int index)
        {
            var r=new GameObject(label,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=new Vector2(index/3f,0);r.anchorMax=new Vector2((index+1)/3f,1);r.offsetMin=new Vector2(4,4);r.offsetMax=new Vector2(-4,-4);
            var image=r.gameObject.AddComponent<Image>();WorkshopUIArt.Card(image);image.color=WorkshopTheme.Surface;
            var card=r.gameObject.AddComponent<WorkshopCard>();card.normal=WorkshopTheme.Surface;card.highlight=WorkshopTheme.Hover;card.clicked=action;
            var text=new GameObject("Label",typeof(RectTransform)).AddComponent<TextMeshProUGUI>();text.transform.SetParent(r,false);var tr=(RectTransform)text.transform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=tr.offsetMax=Vector2.zero;
            text.text=label;text.font=Resources.Load<TMP_FontAsset>("WorkshopBody");text.fontSize=23;text.color=WorkshopTheme.Text;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
        }
    }
}
