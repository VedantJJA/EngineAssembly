using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EngineAssembly
{
    public sealed class MobileAROverlay : MonoBehaviour
    {
        static string pendingScene,pendingMessage;
        RectTransform safe,panel;
        Canvas canvas;
        TextMeshProUGUI title,description;
        WorkshopCard place,reposition,phone;
        public string NoticeText { get; private set; }
        public Canvas Canvas=>canvas;
        public bool NoticeVisible=>NoticeText!=null && panel && panel.gameObject.activeSelf;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register(){SceneManager.sceneLoaded-=SceneLoaded;SceneManager.sceneLoaded+=SceneLoaded;}
        public static void QueueFallback(string scene,string message){pendingScene=scene;pendingMessage=message;}
        static void SceneLoaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.name!=pendingScene)return;
            string message=pendingMessage;pendingScene=pendingMessage=null;
            new GameObject("AR availability notice").AddComponent<MobileAROverlay>().StartCoroutine(ShowNotice(message));
        }
        static IEnumerator ShowNotice(string message)
        {
            yield return null;yield return null;
            var overlay=FindAnyObjectByType<MobileAROverlay>();if(!overlay)yield break;
            overlay.CreateCanvas();overlay.NoticeText=message;
            overlay.title.text="Standard phone view";overlay.description.text=message;
            overlay.Card("CONTINUE",overlay.DismissNotice,new Vector2(16,16),new Vector2(240,70));
        }
        public void DismissNotice(){if(panel)panel.gameObject.SetActive(false);}
        public void CreateSession(Action onPlace,Action onReposition,Action onPhone)
        {
            CreateCanvas();place=Card("PLACE ENGINE",onPlace,new Vector2(16,16),new Vector2(260,70));
            reposition=Card("REPOSITION",onReposition,new Vector2(16,16),new Vector2(260,70));
            phone=Card("PHONE VIEW",onPhone,new Vector2(292,16),new Vector2(260,70));
            SetStatus("Starting AR","Checking your camera and AR support…",false,false);
        }
        void CreateCanvas()
        {
            var go=new GameObject("AR status canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=200;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.matchWidthOrHeight=.5f;
            safe=Rect("Safe area",go.transform);safe.anchorMin=Vector2.zero;safe.anchorMax=Vector2.one;safe.offsetMin=safe.offsetMax=Vector2.zero;
            panel=Rect("AR status",safe);panel.anchorMin=panel.anchorMax=new Vector2(.5f,1);panel.pivot=new Vector2(.5f,1);panel.anchoredPosition=new Vector2(0,-230);panel.sizeDelta=new Vector2(580,210);
            var image=panel.gameObject.AddComponent<Image>();WorkshopUIArt.Card(image);image.color=WorkshopTheme.Surface;
            title=Text("Title",panel,new Vector2(22,-14),new Vector2(536,40),25,true);
            description=Text("Guidance",panel,new Vector2(22,-56),new Vector2(536,65),20,false);UpdateSafeArea();
        }
        WorkshopCard Card(string label,Action action,Vector2 position,Vector2 size)
        {
            var rect=Rect(label,panel);rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=Vector2.zero;rect.anchoredPosition=position;rect.sizeDelta=size;
            var image=rect.gameObject.AddComponent<Image>();WorkshopUIArt.Card(image);image.color=WorkshopTheme.AccentWash;
            var card=rect.gameObject.AddComponent<WorkshopCard>();card.normal=image.color;card.highlight=WorkshopTheme.Hover;card.clicked=action;
            var text=Text(label,rect,Vector2.zero,size,19,true);text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=text.rectTransform.offsetMax=Vector2.zero;text.alignment=TextAlignmentOptions.Center;text.color=WorkshopTheme.Text;
            return card;
        }
        static RectTransform Rect(string name,Transform parent){var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);return rect;}
        static TextMeshProUGUI Text(string label,Transform parent,Vector2 position,Vector2 size,float fontSize,bool bold)
        {
            var rect=Rect(label,parent);rect.anchorMin=rect.anchorMax=Vector2.up;rect.pivot=Vector2.up;rect.anchoredPosition=position;rect.sizeDelta=size;
            var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.text=label;text.font=Resources.Load<TMP_FontAsset>(bold?"WorkshopHeading":"WorkshopBody");text.fontSize=fontSize;text.color=WorkshopTheme.Text;text.raycastTarget=false;return text;
        }
        public void SetStatus(string heading,string guidance,bool canPlace,bool placed)
        {
            title.text=heading;description.text=guidance;
            place.gameObject.SetActive(!placed);place.interactable=canPlace;reposition.gameObject.SetActive(placed);
            bool compact=placed && heading=="Engine anchored";
            description.gameObject.SetActive(!compact);panel.sizeDelta=new Vector2(580,compact?80:210);panel.anchoredPosition=new Vector2(0,compact?-342:-230);
            title.rectTransform.sizeDelta=new Vector2(compact?200:536,40);
            var first=(RectTransform)reposition.transform;first.anchoredPosition=compact?new Vector2(218,16):new Vector2(16,16);first.sizeDelta=compact?new Vector2(170,48):new Vector2(260,70);
            var last=(RectTransform)phone.transform;last.anchoredPosition=compact?new Vector2(400,16):new Vector2(292,16);last.sizeDelta=compact?new Vector2(164,48):new Vector2(260,70);
        }
        public void SetPlacementLabel(string label){var text=place.GetComponentInChildren<TextMeshProUGUI>();if(text)text.text=label;}
        void Update(){if(canvas)UpdateSafeArea();}
        void UpdateSafeArea()
        {
            canvas.GetComponent<CanvasScaler>().referenceResolution=Screen.width>Screen.height?new Vector2(1920,1080):new Vector2(1080,1920);
            var area=Screen.safeArea;var size=new Vector2(Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height));safe.anchorMin=area.min/size;safe.anchorMax=area.max/size;
        }
    }
}
