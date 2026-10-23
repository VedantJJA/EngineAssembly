using UnityEngine;
namespace EngineAssembly
{
    public static class WorkshopUIArt
    {
        static Sprite rounded;
        static Texture2D grid;
        public static Sprite Rounded
        {
            get
            {
                if(rounded)return rounded;
                const int size=64,radius=10;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Workshop equipment card",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    var nearest=new Vector2(Mathf.Clamp(x,radius,size-radius-1),Mathf.Clamp(y,radius,size-radius-1));
                    float edge=Vector2.Distance(new Vector2(x,y),nearest);
                    float alpha=Mathf.Clamp01(radius+.5f-edge);
                    float border=Mathf.Clamp01(radius-edge)-Mathf.Clamp01(radius-1.5f-edge);
                    pixels[y*size+x]=new Color(1-border*.16f,1-border*.12f,1-border*.10f,alpha);
                }
                texture.SetPixels(pixels);texture.Apply(false,true);
                return rounded=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(radius,radius,radius,radius));
            }
        }
        public static void Card(UnityEngine.UI.Image image){image.sprite=Rounded;image.type=UnityEngine.UI.Image.Type.Sliced;}
        public static Texture2D DraftGrid
        {
            get
            {
                if(grid)return grid;
                const int size=96;grid=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Workshop drafting grid",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Repeat};
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    bool axis=x==0 || y==0;
                    bool minor=x==48 || y==48;
                    pixels[y*size+x]=new Color(.13f,.27f,.45f,axis?.12f:minor?.055f:0);
                }
                grid.SetPixels(pixels);grid.Apply(false,true);return grid;
            }
        }
    }
}
