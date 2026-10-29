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
        WorkshopCard place,reposition,phone,retry;
        ARPlacementRing placementRing;
        bool scanning,statusPlaced,canPlace;
        public string NoticeText { get; private set; }
        public Canvas Canvas=>canvas;
        public bool NoticeVisible=>NoticeText!=null && panel && panel.gameObject.activeSelf;
        public bool PlacementCircleVisible=>placementRing && placementRing.gameObject.activeInHierarchy;
        public bool PlacementMarkerVisible=>PlacementCircleVisible;
        public bool PlacementReady=>canPlace;
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
        public void CreateSession(Action onPlace,Action onReposition,Action onPhone,Action onRetry=null)
        {
            CreateCanvas();place=Card("PLACE ENGINE",onPlace,new Vector2(16,16),new Vector2(260,70));
            reposition=Card("REPOSITION",onReposition,new Vector2(16,16),new Vector2(260,70));
            phone=Card("PHONE VIEW",onPhone,new Vector2(292,16),new Vector2(260,70));
            if(onRetry!=null)retry=Card("RESCAN",onRetry,new Vector2(292,16),new Vector2(240,70));
            SetStatus("Starting AR","Checking your camera and AR support…",false,false);
        }
        void CreateCanvas()
        {
            var go=new GameObject("AR status canvas",typeof(RectTransform),typeof(Canvas),typeof(WorkshopCanvasRegistration),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=200;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.matchWidthOrHeight=.5f;
            safe=Rect("Safe area",go.transform);safe.anchorMin=Vector2.zero;safe.anchorMax=Vector2.one;safe.offsetMin=safe.offsetMax=Vector2.zero;
            panel=Rect("AR status",safe);panel.anchorMin=panel.anchorMax=new Vector2(.5f,1);panel.pivot=new Vector2(.5f,1);panel.anchoredPosition=new Vector2(0,-230);panel.sizeDelta=new Vector2(580,210);
            var image=panel.gameObject.AddComponent<Image>();WorkshopUIArt.Card(image);image.color=WorkshopTheme.Surface;
            title=Text("Title",panel,new Vector2(22,-14),new Vector2(536,40),25,true);
            description=Text("Guidance",panel,new Vector2(22,-56),new Vector2(536,65),20,false);UpdateSafeArea();
            var circle=Rect("Detected surface square",go.transform);circle.anchorMin=circle.anchorMax=new Vector2(.5f,.5f);circle.sizeDelta=new Vector2(176,176);circle.SetAsFirstSibling();
            placementRing=circle.gameObject.AddComponent<ARPlacementRing>();placementRing.raycastTarget=false;circle.gameObject.SetActive(false);
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
            this.canPlace=canPlace;statusPlaced=placed;
            title.text=heading;description.text=guidance;
            place.gameObject.SetActive(!placed);place.interactable=canPlace;reposition.gameObject.SetActive(placed);
            Layout();
        }
        public void SetScanning(bool value){scanning=value;if(placementRing)placementRing.gameObject.SetActive(value && canPlace);}
        public void SetTargetScreenPoint(Vector3? point)
        {
            if(!placementRing)return;var rect=placementRing.rectTransform;
            if(point.HasValue && point.Value.z>0 && RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,point.Value,null,out var local))rect.anchoredPosition=local;
            else rect.anchoredPosition=Vector2.zero;
        }
        public void SetPlacementLabel(string label){var text=place.GetComponentInChildren<TextMeshProUGUI>();if(text)text.text=label;}
        void Update(){if(canvas)UpdateSafeArea();}
        void UpdateSafeArea()
        {
            canvas.GetComponent<CanvasScaler>().referenceResolution=Screen.width>Screen.height?new Vector2(1920,1080):new Vector2(1080,1920);
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight=Screen.width>Screen.height?.5f:0;
            var area=Screen.safeArea;var size=new Vector2(Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height));safe.anchorMin=area.min/size;safe.anchorMax=area.max/size;
            Layout();
        }
        void Layout()
        {
            if(!panel || !title)return;float width=Mathf.Min(800,Mathf.Max(300,safe.rect.width-32));bool compact=statusPlaced && title.text=="Engine anchored";
            panel.gameObject.SetActive(!compact || NoticeText!=null);
            panel.anchorMin=panel.anchorMax=compact?(Screen.width>Screen.height?Vector2.one:new Vector2(.5f,1)):new Vector2(.5f,0);
            panel.pivot=compact?(Screen.width>Screen.height?Vector2.one:new Vector2(.5f,1)):new Vector2(.5f,0);
            panel.anchoredPosition=compact?new Vector2(Screen.width>Screen.height?-18:0,Screen.width>Screen.height?-114:-122):new Vector2(0,32);
            panel.sizeDelta=new Vector2(width,compact?56:244);description.gameObject.SetActive(!compact);
            title.fontSize=compact?20:26;title.rectTransform.anchoredPosition=new Vector2(22,compact?-8:-16);title.rectTransform.sizeDelta=new Vector2(compact?width*.30f-28:width-44,40);
            description.rectTransform.anchoredPosition=new Vector2(22,-62);description.rectTransform.sizeDelta=new Vector2(width-44,86);
            float half=(width-48)*.5f;
            if(place){var r=(RectTransform)place.transform;r.anchoredPosition=new Vector2(16,16);r.sizeDelta=new Vector2(half,66);}
            if(reposition){var r=(RectTransform)reposition.transform;r.anchoredPosition=compact?new Vector2(width*.30f,8):new Vector2(16,16);r.sizeDelta=new Vector2(compact?width*.34f-12:half,compact?40:66);}
            if(phone){var r=(RectTransform)phone.transform;r.anchoredPosition=compact?new Vector2(width*.64f,8):new Vector2(32+half,16);r.sizeDelta=new Vector2(compact?width*.36f-16:half,compact?40:66);}
            if(retry)
            {
                bool show=scanning && !statusPlaced;retry.gameObject.SetActive(show);
                if(show){float cell=(width-64)/3;var a=(RectTransform)place.transform;var b=(RectTransform)retry.transform;var c=(RectTransform)phone.transform;a.anchoredPosition=new Vector2(16,16);b.anchoredPosition=new Vector2(32+cell,16);c.anchoredPosition=new Vector2(48+cell*2,16);a.sizeDelta=b.sizeDelta=c.sizeDelta=new Vector2(cell,66);}
            }
            if(placementRing){placementRing.gameObject.SetActive(scanning && canPlace);placementRing.color=WorkshopTheme.Accent;}
            if(NoticeText!=null){panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);panel.anchoredPosition=Vector2.zero;}
        }
    }
}
