using UnityEngine;

namespace EngineAssembly
{
    // Cover the available frame, cropping around the centre without distorting
    // the source image. Recalculate when either the texture or layout changes.
    [RequireComponent(typeof(UnityEngine.UI.RawImage))]
    public sealed class CroppedArtwork : MonoBehaviour
    {
        UnityEngine.UI.RawImage image;
        Texture lastTexture;
        Vector2 lastSize;
        void Awake(){image=GetComponent<UnityEngine.UI.RawImage>();Refresh();}
        void LateUpdate()
        {
            if(!image)image=GetComponent<UnityEngine.UI.RawImage>();
            if(lastTexture!=image.texture || lastSize!=image.rectTransform.rect.size)Refresh();
        }
        void OnRectTransformDimensionsChange(){if(image)Refresh();}
        public void Refresh()
        {
            if(!image)image=GetComponent<UnityEngine.UI.RawImage>();
            lastTexture=image.texture;lastSize=image.rectTransform.rect.size;
            if(!lastTexture || lastSize.x<=0 || lastSize.y<=0){image.uvRect=new Rect(0,0,1,1);return;}
            float source=(float)lastTexture.width/lastTexture.height,frame=lastSize.x/lastSize.y;
            float width=Mathf.Min(1,frame/source),height=Mathf.Min(1,source/frame);
            image.uvRect=new Rect((1-width)*.5f,(1-height)*.5f,width,height);
        }
    }
}
