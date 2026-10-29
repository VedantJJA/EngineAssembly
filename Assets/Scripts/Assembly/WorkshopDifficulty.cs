using UnityEngine;

namespace EngineAssembly
{
    public static class WorkshopDifficulty
    {
        const string VersionKey="Assembly.EasyDefaultVersion";
        public static SnapDifficulty Saved
        {
            get
            {
                // Migrate the old implicit Hard default once; retain subsequent choices.
                if(PlayerPrefs.GetInt(VersionKey,0)<1)
                {
                    PlayerPrefs.SetInt("Assembly.SnapDifficulty",(int)SnapDifficulty.Easy);
                    PlayerPrefs.SetInt(VersionKey,1);
                }
                return (SnapDifficulty)Mathf.Clamp(PlayerPrefs.GetInt("Assembly.SnapDifficulty",(int)SnapDifficulty.Easy),0,2);
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize(){_ = Saved;}
    }
}
