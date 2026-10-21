using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EngineAssembly
{
    public static class AssemblyMotion
    {
        static readonly Dictionary<AssemblyPart,Bounds> shapes=new Dictionary<AssemblyPart,Bounds>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset()=>shapes.Clear();
        public static Bounds LocalBounds(AssemblyPart part)
        {
            bool carrier=part.Manager && part.Manager.CarrierDefinition(part.PartId)!=null;
            if(!carrier && shapes.TryGetValue(part,out var cached))return cached;
            var bounds=new Bounds();bool first=true;
            foreach(var mesh in part.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!mesh.sharedMesh || mesh.GetComponentInParent<GeneratedColliderGroup>() || (part.Manager && !part.Manager.OwnsMesh(part,mesh)))continue;
                var b=mesh.sharedMesh.bounds;var matrix=part.transform.worldToLocalMatrix*mesh.transform.localToWorldMatrix;
                for(int i=0;i<8;i++)
                {
                    var p=matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                }
            }
            if(!carrier)shapes[part]=bounds;return bounds;
        }
        public static Vector3 ShapeAxis(AssemblyPart part,bool shortest)
            =>ShapeAxis(LocalBounds(part),part.transform.lossyScale,shortest);
        public static Vector3 ShapeAxis(Bounds shape,Vector3 scale,bool shortest)
        {
            var size=Vector3.Scale(shape.size,scale);
            int index=0;for(int i=1;i<3;i++)if(shortest?size[i]<size[index]:size[i]>size[index])index=i;
            return index==0?Vector3.right:index==1?Vector3.up:Vector3.forward;
        }
        public static Vector3 TargetCenter(AssemblyPart part)
        {
            var pose=part.GetTargetPose(part.TargetSocket);return pose.position+pose.rotation*Vector3.Scale(LocalBounds(part).center,part.transform.lossyScale);
        }
        public static Vector3 DisplayInsertionOffset(PartDefinition definition,Bounds shape,Vector3 scale)
        {
            var size=Vector3.Scale(shape.size,scale);
            bool thin=definition.id.Contains("Bearing") || definition.id.Contains("Circlip") || definition.id.Contains("Washer") || definition.id.Contains("Ring") || definition.id.Contains("Pulley") || definition.id.Contains("Hub") || definition.id.Contains("Wheel") || definition.id.Contains("Seal") || definition.id.Contains("Seat") || definition.id.Contains("Retainer");
            int axis=0;for(int i=1;i<3;i++)if(thin?size[i]<size[axis]:size[i]>size[axis])axis=i;
            var direction=definition.targetRotation*(axis==0?Vector3.right:axis==1?Vector3.up:Vector3.forward);
            if(new[]{102,103,240,241,301,302,303,307,308}.Contains(definition.order))direction=Vector3.down;
            else if(new[]{101,201,211,221,230,304,305,306,401,480,490,501,502,601,602,603,604,605}.Contains(definition.order))direction=Vector3.up;
            else if(definition.order==402){if(Vector3.Dot(direction,Vector3.up)>0)direction=-direction;}
            else if(definition.order==403 || definition.order==404 || definition.order==405 || definition.order==406 || definition.order==407)direction=Vector3.up;
            else if(Vector3.Dot(direction,definition.targetPosition)<0)direction=-direction;
            // Only the final insertion stroke belongs in the timelapse. The
            // component is already oriented when it appears beside its socket.
            return direction.normalized*Mathf.Clamp(size.magnitude*.15f,.014f,.16f);
        }
        public static Vector3 InsertionDirection(AssemblyPart part)
        {
            var manager=part.Manager;var d=part.Definition;var pose=part.GetTargetPose(part.TargetSocket);var frame=manager.TargetFrame(d);
            if(new[]{102,103,240,241,301,302,303,307,308}.Contains(d.order))return -frame.up;
            if(new[]{101,201,211,221,230,304,305,306,401,480,490,501,502,601,602,603,604,605}.Contains(d.order))return frame.up;
            if(d.order==402)
            {var axis=pose.rotation*ShapeAxis(part,false);return Vector3.Dot(axis,frame.up)>=0?-axis:axis;}
            if(d.order==403)
            {
                string station=d.displayName.Split('·').Last().Trim();
                var valve=manager.AssemblyParts.FirstOrDefault(p=>p.Definition?.order==402 && string.Equals(p.PartDisplayName,station,System.StringComparison.OrdinalIgnoreCase));
                if(valve){var vp=valve.GetTargetPose(valve.TargetSocket);var axis=vp.rotation*ShapeAxis(valve,false);return Vector3.Dot(axis,frame.up)>=0?axis:-axis;}
            }
            string id=part.PartId;
            bool shortAxis=id.Contains("Bearing") || id.Contains("Circlip") || id.Contains("Washer") || id.Contains("Ring") || id.Contains("Pulley") || id.Contains("Hub") || id.Contains("Wheel") || id.Contains("Seal");
            Vector3 direction=pose.rotation*ShapeAxis(part,shortAxis);
            var reference=TargetCenter(part)-manager.WorkpieceBounds(frame).center;
            if(Vector3.Dot(direction,reference)<0)direction=-direction;
            return direction.normalized;
        }
    }
}
