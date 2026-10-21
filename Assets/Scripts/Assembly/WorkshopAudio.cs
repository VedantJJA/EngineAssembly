using System;
using UnityEngine;
namespace EngineAssembly
{
    public static class WorkshopAudio
    {
        public static event Action<float> VolumeChanged;
        public static float Volume => Mathf.Clamp01(PlayerPrefs.GetFloat("Assembly.Volume",1));
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset(){VolumeChanged=null;AudioListener.volume=Volume;AudioListener.pause=false;}
        public static void SetVolume(float value){value=Mathf.Clamp01(value);PlayerPrefs.SetFloat("Assembly.Volume",value);PlayerPrefs.Save();AudioListener.volume=value;VolumeChanged?.Invoke(value);}
    }
}
