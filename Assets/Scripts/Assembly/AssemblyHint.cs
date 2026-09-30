using UnityEngine;
namespace EngineAssembly
{
    public sealed class AssemblyHint : MonoBehaviour
    {
        AssemblyManager manager;
        AssemblyVisual source,target;
        public bool Enabled { get; private set; } = true;
        public AssemblyPart HighlightedPart { get; private set; }
        void Awake()
        {
            manager=GetComponent<AssemblyManager>();
            source=new GameObject("Next part hint").AddComponent<AssemblyVisual>();source.transform.SetParent(transform,false);
            target=new GameObject("Next socket hint").AddComponent<AssemblyVisual>();target.transform.SetParent(transform,false);
        }
        public void Toggle() { Enabled=!Enabled;Refresh(); }
        void Update()=>Refresh();
        void Refresh()
        {
            if(!Enabled || !manager) { HighlightedPart=null;source.Clear();target.Clear();return; }
            var held=System.Linq.Enumerable.FirstOrDefault(manager.AssemblyParts,p=>p && p.IsSelected);
            HighlightedPart=held?held:manager.NextHintPart();
            if(!HighlightedPart) { source.Clear();target.Clear();return; }
            source.Hover(HighlightedPart,true,true);
            if(held && manager.Difficulty==SnapDifficulty.Easy) { target.Clear();return; }
            if(manager.Task==AssemblyTask.Assemble || manager.Mode==WorkshopMode.Edit)
                target.ShowGhost(HighlightedPart,HighlightedPart.TargetSocket,AssemblyVisual.Ready,true);
            else target.Clear();
        }
        void OnDisable() { if(source)source.Clear();if(target)target.Clear(); }
    }
}
