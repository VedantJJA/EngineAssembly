using TMPro;
using UnityEngine;
namespace EngineAssembly
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkshopQuestArrow : UnityEngine.UI.MaskableGraphic
    {
        Camera cameraView;AssemblyManager manager;PlayerAssemblyController player;MobileAssemblyController mobile;TextMeshProUGUI label;
        RectTransform hud;
        public bool Tracking { get; private set; }
        public AssemblyPart TargetPart { get; private set; }
        public bool PointsToPlacement { get; private set; }
        public bool PointsToFlip {get;private set;}
        public LiftControlHandle TargetFlipControl {get;private set;}
        QuestVRRig vr;
        public bool ArrowVisible { get; private set; }
        public bool TargetOnScreen { get; private set; }
        public Vector2 ArrowDirection { get; private set; }
        public void Configure(RectTransform parent,AssemblyManager assembly,PlayerAssemblyController controller,TMP_FontAsset font)
        {
            hud=parent;manager=assembly;player=controller;cameraView=controller.ViewCamera;raycastTarget=false;
            rectTransform.anchorMin=rectTransform.anchorMax=new Vector2(.5f,.5f);rectTransform.sizeDelta=new Vector2(36,48);
            rectTransform.pivot=new Vector2(.5f,.5f);
            var outline=gameObject.AddComponent<UnityEngine.UI.Outline>();outline.effectColor=new Color(.025f,.05f,.065f,.9f);outline.effectDistance=new Vector2(2,-2);
            var text=new GameObject("Quest target",typeof(RectTransform),typeof(TextMeshProUGUI));text.transform.SetParent(parent,false);label=text.GetComponent<TextMeshProUGUI>();label.font=font;label.fontSize=15;label.alignment=TextAlignmentOptions.Center;label.color=new Color(.8f,.96f,.9f);label.raycastTarget=false;
            label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(.5f,.5f);label.rectTransform.sizeDelta=new Vector2(220,60);
            var shadow=text.AddComponent<UnityEngine.UI.Shadow>();shadow.effectColor=new Color(.025f,.05f,.065f,.95f);shadow.effectDistance=new Vector2(1,-1);
            WorkshopGuidance.Changed+=SyncSettings;SyncSettings();
        }
        public void Toggle(bool value) { WorkshopGuidance.SetArrow(value);SyncSettings(); }
        void SyncSettings(){Tracking=WorkshopGuidance.Arrow;TargetOnScreen=false;SetVisible(false);enabled=Tracking;}
        public void ConfigureMobile(RectTransform parent,AssemblyManager assembly,MobileAssemblyController controller,TMP_FontAsset font)
        {
            // Reuse the desktop arrow's screen-edge and insertion-axis behavior.
            hud=parent;manager=assembly;mobile=controller;cameraView=controller.ViewCamera;raycastTarget=false;
            rectTransform.anchorMin=rectTransform.anchorMax=rectTransform.pivot=new Vector2(.5f,.5f);rectTransform.sizeDelta=new Vector2(36,48);
            var outline=gameObject.AddComponent<UnityEngine.UI.Outline>();outline.effectColor=new Color(.025f,.05f,.065f,.95f);outline.effectDistance=new Vector2(2,-2);
            var text=new GameObject("Mobile quest target",typeof(RectTransform),typeof(TextMeshProUGUI));text.transform.SetParent(parent,false);label=text.GetComponent<TextMeshProUGUI>();label.font=font;label.fontSize=20;label.alignment=TextAlignmentOptions.Center;label.color=WorkshopTheme.Text;label.raycastTarget=false;
            label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(.5f,.5f);label.rectTransform.sizeDelta=new Vector2(270,76);
            var shadow=text.AddComponent<UnityEngine.UI.Shadow>();shadow.effectColor=Color.white;shadow.effectDistance=new Vector2(1,-1);WorkshopGuidance.Changed+=SyncSettings;SyncSettings();
        }
        void LateUpdate()
        {
            if(mobile)cameraView=mobile.ViewCamera;
            if(QuestVRSession.Instance && QuestVRSession.Instance.Active){if(!vr)vr=FindAnyObjectByType<QuestVRRig>();if(Camera.main)cameraView=Camera.main;}
            if(!Tracking || !manager || (!player && !mobile) || !cameraView)return;
            if(mobile && (!MobileARSession.CanInteract(mobile.gameObject) || mobile.ChapterMenuOpen)){SetVisible(false);return;}
            var held=mobile?mobile.SelectedPart:vr?vr.HeldPart:player.CurrentHeldPart;
            TargetPart=held?held:manager.NextHintPart();PointsToPlacement=mobile || held;
            PointsToFlip=false;TargetFlipControl=null;
            if(!TargetPart) { TargetOnScreen=false;SetVisible(false);label.text="";return; }
            var lift=manager.GetComponent<WorkpieceLift>();var frame=manager.TargetFrame(TargetPart.Definition);
            if(manager.Task==AssemblyTask.Assemble && !manager.RepairActive && manager.RequiresWorkpieceOrientation && (!manager.OrientationReady(TargetPart.Definition) || lift && lift.FrameIsTurning(frame)))
            {TargetFlipControl=lift?lift.FlipControlFor(frame):null;PointsToFlip=TargetFlipControl;}
            if(PointsToFlip)PointsToPlacement=false;
            Vector3 position=TargetPart.transform.TransformPoint(AssemblyMotion.LocalBounds(TargetPart).center);
            if(PointsToPlacement && TargetPart.TargetSocket)position=AssemblyMotion.TargetCenter(TargetPart);
            bool settingAside=PointsToPlacement && manager.RepairActive && !manager.Repair.IsPlacementStep;
            if(settingAside)position=manager.Repair.RemovalDestination(TargetPart);
            if(PointsToFlip)position=TargetFlipControl.AimPosition;
            var projected=cameraView.WorldToScreenPoint(position);
            bool onScreen=projected.z>cameraView.nearClipPlane && cameraView.pixelRect.Contains(projected);
            TargetOnScreen=onScreen;SetVisible(true);
            var canvas=hud.GetComponentInParent<Canvas>();var uiCamera=canvas && canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hud,projected,uiCamera,out var local);
            var half=hud.rect.size*.5f-new Vector2(60,105);
            Vector2 direction;
            if(onScreen)
            {
                var axis=PointsToPlacement && !settingAside?-AssemblyMotion.InsertionDirection(TargetPart):TargetPart.transform.rotation*AssemblyMotion.ShapeAxis(TargetPart,false);
                var projectedAxis=cameraView.WorldToScreenPoint(position+axis*.3f)-(Vector3)projected;
                direction=Mathf.Abs(projectedAxis.x)>Mathf.Abs(projectedAxis.y)?new Vector2(Mathf.Sign(projectedAxis.x),0):new Vector2(0,Mathf.Abs(projectedAxis.y)<1?-1:Mathf.Sign(projectedAxis.y));
                if(PointsToFlip)direction=Vector2.down;
                var point=new Vector2(Mathf.Clamp(local.x,-half.x,half.x),Mathf.Clamp(local.y,-half.y,half.y));
                rectTransform.anchoredPosition=point-direction*(52+Mathf.Sin(Time.unscaledTime*3)*5);
                var captionPosition=rectTransform.anchoredPosition-direction*45+new Vector2(direction.y*75,-direction.x*75);
                label.rectTransform.anchoredPosition=new Vector2(Mathf.Clamp(captionPosition.x,-half.x+100,half.x-100),Mathf.Clamp(captionPosition.y,-half.y+35,half.y-35));
            }
            else
            {
                direction=local;if(projected.z<0)direction=-direction;if(direction.sqrMagnitude<1)direction=Vector2.up;direction.Normalize();
                float edge=Mathf.Min(half.x/Mathf.Max(.001f,Mathf.Abs(direction.x)),half.y/Mathf.Max(.001f,Mathf.Abs(direction.y)));
                rectTransform.anchoredPosition=direction*edge;label.rectTransform.anchoredPosition=direction*Mathf.Max(0,edge-115);
            }
            ArrowDirection=direction;rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg-90);
            color=PointsToPlacement || PointsToFlip?new Color(1,.77f,.36f):new Color(.45f,.94f,.74f);
            string action=PointsToFlip?(lift.FrameIsTurning(frame)?"TURNING · WAIT":"PRESS FLIP"):settingAside?"SET ASIDE":mobile && !held?"NEXT TARGET":PointsToPlacement?"PLACE":"PICK UP";
            label.text=action+" · "+Vector3.Distance(cameraView.transform.position,position).ToString("0.0")+" m\n"+(PointsToFlip?(TargetPart.Definition.orientation==AssemblyOrientation.UpsideDown?"Turn this workpiece upside down":"Turn this workpiece upright"):TargetPart.PartDisplayName);
        }
        void SetVisible(bool value){ArrowVisible=value;canvasRenderer.SetAlpha(value?1:0);if(label)label.enabled=value && Tracking;}
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();var c=(Color32)color;
            Vector2[] points={new Vector2(0,24),new Vector2(-17,-2),new Vector2(-6,-2),new Vector2(-6,-23),new Vector2(6,-23),new Vector2(6,-2),new Vector2(17,-2)};
            foreach(var point in points)vh.AddVert(point,c,Vector2.zero);
            vh.AddTriangle(0,1,6);vh.AddTriangle(2,3,4);vh.AddTriangle(2,4,5);
        }
        protected override void OnDisable() { base.OnDisable();if(label)label.enabled=false; }
        protected override void OnEnable() { base.OnEnable();if(label)label.enabled=Tracking; }
        protected override void OnDestroy(){WorkshopGuidance.Changed-=SyncSettings;base.OnDestroy();}
    }
}
