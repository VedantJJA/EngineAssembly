using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace EngineAssembly
{
    public enum WorkshopMode { Assembly, Edit }
    public enum SnapDifficulty { Hard, Medium, Easy }
    public enum WorkshopInteraction { Build, Move }
    public enum AssemblyTask { Assemble, Disassemble }
    public enum AssemblyOrientation { Any, Upright, UpsideDown }
    public enum SnapRotationMode { Exact, Axial, Free }
    [Serializable] public class ChapterDefinition
    {
        public string id = "chapter-1";
        public string title = "Chapter 1";
        public string description = "";
    }
    [Serializable] public class PartDefinition
    {
        public string id, chapterId, displayName;
        public int order = 1;
        public bool isBase;
        public string geometryGroup = "", parentPartId = "";
        public string subAssemblyId = "";
        public List<string> prerequisites = new List<string>();
        public Vector3 targetPosition, trayPosition;
        public Quaternion targetRotation = Quaternion.identity, trayRotation = Quaternion.identity;
        public AssemblyOrientation orientation;
        public SnapRotationMode rotationMode;
        public float snapDistance = .15f, snapAngle = 45f;
        public int symmetry = 1;
    }
    [Serializable] public class SubAssemblyDefinition
    {
        public string id, rootPartId, previewPartId;
        public Vector3 benchPosition = new Vector3(1.5f,.3f,0);
        public Vector3 focusCenter;
        public float focusRadius = .65f;
    }
    [Serializable] public class AssemblyRecipe
    {
        public int version = 1;
        public string title = "Engine assembly";
        public List<ChapterDefinition> chapters = new List<ChapterDefinition>();
        public List<PartDefinition> parts = new List<PartDefinition>();
        public List<SubAssemblyDefinition> subAssemblies = new List<SubAssemblyDefinition>();
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (version != 1 && version != 2) errors.Add("Unsupported recipe version: " + version);
            if (chapters == null || parts == null) { errors.Add("Missing chapters or parts."); return errors; }
            if (chapters.Count == 0 || parts.Count == 0) errors.Add("A recipe needs chapters and parts.");
            var chapterIds = new HashSet<string>();
            foreach (var c in chapters)
                if (c == null || string.IsNullOrWhiteSpace(c.id) || !chapterIds.Add(c.id)) errors.Add("Missing or duplicate chapter ID.");
            var byId = new Dictionary<string, PartDefinition>();
            foreach (var p in parts)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.id) || byId.ContainsKey(p.id)) { errors.Add("Missing or duplicate part ID."); continue; }
                byId.Add(p.id, p);
                if (!chapterIds.Contains(p.chapterId ?? "")) errors.Add(p.id + ": unknown chapter.");
                if (p.order < 0 || !Finite(p.snapDistance) || p.snapDistance <= 0 || !Finite(p.snapAngle) || p.snapAngle < 0 || p.snapAngle > 180 || p.symmetry < 1 || p.symmetry > 360) errors.Add(p.id + ": invalid snap/order settings.");
                if (!Valid(p.targetPosition) || !Valid(p.trayPosition) || !Valid(p.targetRotation) || !Valid(p.trayRotation)) errors.Add(p.id + ": invalid pose.");
                if (!Enum.IsDefined(typeof(AssemblyOrientation), p.orientation) || !Enum.IsDefined(typeof(SnapRotationMode), p.rotationMode)) errors.Add(p.id + ": invalid orientation mode.");
                if (p.prerequisites == null) errors.Add(p.id + ": missing prerequisites list.");
            }
            // Empty chapters are valid authoring drafts; play selection disables them.
            var groups = subAssemblies ?? new List<SubAssemblyDefinition>();
            var groupIds = new HashSet<string>();var roots = new HashSet<string>();
            foreach(var group in groups)
            {
                if(group==null || string.IsNullOrEmpty(group.id) || !groupIds.Add(group.id) || !roots.Add(group.rootPartId ?? "")) { errors.Add("Missing or duplicate subassembly ID/root.");continue; }
                if(!byId.TryGetValue(group.rootPartId ?? "",out var root)) { errors.Add(group.id+": missing carrier part.");continue; }
                var members=parts.Where(p=>p!=null && p.subAssemblyId==group.id).ToArray();
                if(members.Length==0 || !members.Any(p=>p.id==group.previewPartId))errors.Add(group.id+": missing members/preview part.");
                if(!string.IsNullOrEmpty(root.subAssemblyId) || root.isBase)errors.Add(group.id+": nested/preset carriers are not supported.");
                if(!Valid(group.benchPosition) || !Valid(group.focusCenter) || !Finite(group.focusRadius) || group.focusRadius<=0)errors.Add(group.id+": invalid fixture framing.");
                foreach(var member in members)
                {
                    if(member.chapterId!=root.chapterId || member.order>=root.order || member.isBase || !string.IsNullOrEmpty(member.parentPartId))errors.Add(member.id+": invalid subassembly membership/order.");
                    if(root.prerequisites==null || !root.prerequisites.Contains(member.id))errors.Add(root.id+": carrier must depend on every member.");
                }
            }
            foreach(var p in byId.Values)if(!string.IsNullOrEmpty(p.subAssemblyId) && !groupIds.Contains(p.subAssemblyId))errors.Add(p.id+": unknown subassembly.");
            foreach (var p in byId.Values)
            {
                foreach (string id in Dependencies(p))
                {
                    if (!byId.TryGetValue(id, out var d)) { errors.Add(p.id + ": missing dependency " + id); continue; }
                    int a = chapters.FindIndex(c => c != null && c.id == d.chapterId), b = chapters.FindIndex(c => c != null && c.id == p.chapterId);
                    if (a > b || (a == b && d.order > p.order && !d.isBase)) errors.Add(p.id + ": dependency comes after this part.");
                    if (p.isBase && !d.isBase) errors.Add(p.id + ": a preset base cannot depend on a loose part.");
                }
                if (HasCycle(p.id, byId, new HashSet<string>(), new HashSet<string>())) errors.Add(p.id + ": cyclic dependencies or socket parenting.");
            }
            return errors.Distinct().ToList();
        }
        public static IEnumerable<string> Dependencies(PartDefinition p)
        {
            foreach (var id in p.prerequisites ?? new List<string>()) yield return id ?? "";
            if (!string.IsNullOrEmpty(p.parentPartId)) yield return p.parentPartId;
        }
        static bool HasCycle(string id, Dictionary<string, PartDefinition> parts, HashSet<string> visiting, HashSet<string> done)
        {
            if (visiting.Contains(id)) return true;
            if (done.Contains(id) || !parts.TryGetValue(id, out var p)) return false;
            visiting.Add(id);
            foreach (string d in Dependencies(p)) if (HasCycle(d, parts, visiting, done)) return true;
            visiting.Remove(id); done.Add(id); return false;
        }
        static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        static bool Valid(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        static bool Valid(Quaternion q) => Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w) && Mathf.Abs(Quaternion.Dot(q, q) - 1) < .02f;
    }
    public static class AssemblyRecipeStore
    {
        public static string DirectoryPath => Path.Combine(Application.isEditor ? Application.dataPath : Application.persistentDataPath, "AssemblyRecipes");
        public static string[] Files() => Directory.Exists(DirectoryPath) ? Directory.GetFiles(DirectoryPath, "*.json").OrderBy(p => p).ToArray() : Array.Empty<string>();
        public static AssemblyRecipe Parse(string json)
        {
            var recipe = JsonUtility.FromJson<AssemblyRecipe>(json);
            if (recipe == null) throw new InvalidDataException("Empty recipe.");
            var errors = recipe.Validate();
            if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors));
            return recipe;
        }
        public static AssemblyRecipe Load(string path) => Parse(File.ReadAllText(path));
        public static string Save(AssemblyRecipe recipe, string filename)
        {
            Parse(JsonUtility.ToJson(recipe));
            if (string.IsNullOrWhiteSpace(filename) || filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || filename != Path.GetFileName(filename)) throw new ArgumentException("Use a filename without folders.");
            Directory.CreateDirectory(DirectoryPath);
            string path = Path.Combine(DirectoryPath, Path.GetFileNameWithoutExtension(filename) + ".json"), temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(recipe, true));
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            return path;
        }
    }
}
