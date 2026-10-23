using UnityEngine;

namespace EngineAssembly
{
    // Display preferences are independent from the spoken instructions in hands-on assembly.
    public static class DisplayPlaybackSettings
    {
        public static bool AudioEnabled
        {
            get=>PlayerPrefs.GetInt("Display.Audio",1)!=0;
            set=>PlayerPrefs.SetInt("Display.Audio",value?1:0);
        }
        public static float Speed
        {
            get=>Mathf.Clamp(PlayerPrefs.GetFloat("Display.Speed",1),.1f,4);
            set=>PlayerPrefs.SetFloat("Display.Speed",Mathf.Clamp(value,.1f,4));
        }
        public static EnginePartDetail Description(PartDefinition part)=>EngineLearningContent.Detail(part);
    }
}
