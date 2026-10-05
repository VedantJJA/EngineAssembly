using UnityEngine;
namespace EngineAssembly
{
    public static class WorkshopUIArt
    {
        static Sprite rounded;
        public static Sprite Rounded
        {
            get
            {
                if(rounded)return rounded;
                const int size=64,radius=16;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Workshop rounded card",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    var nearest=new Vector2(Mathf.Clamp(x,radius,size-radius-1),Mathf.Clamp(y,radius,size-radius-1));
                    pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(radius+.5f-Vector2.Distance(new Vector2(x,y),nearest)));
                }
                texture.SetPixels(pixels);texture.Apply(false,true);
                return rounded=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(radius,radius,radius,radius));
            }
        }
        public static void Card(UnityEngine.UI.Image image){image.sprite=Rounded;image.type=UnityEngine.UI.Image.Type.Sliced;}
    }
}
