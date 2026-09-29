using UnityEngine;
namespace EngineAssembly
{
    // Explicit authoring preview only. This component never auto-creates itself or completes gameplay.
    public class AutoAssemblyController : MonoBehaviour
    {
        public void PreviewChapter()
        {
            var manager=AssemblyManager.Instance;
            if(manager && manager.Mode==WorkshopMode.Edit)manager.SelectChapter(manager.CurrentChapterIndex);
        }
    }
}

