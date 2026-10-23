using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    [Serializable] public class ChairFacingManifest {public ChairFacingEntry[] entries;}
    [Serializable] public class ChairFacingEntry {public string asset,node;public float degrees;}
    /// <summary>Keep all related source-model chairs facing their authored desks on every import.</summary>
    public class CommandChairFacingImporter:AssetPostprocessor
    {
        public const string Manifest="Assets/_Project/Art/Environment/CommandBunker/ChairFacing.json";
        public static string RelativePath(Transform t,Transform root)=>t==root?"":(t.parent==root?t.name:RelativePath(t.parent,root)+"/"+t.name);
        void OnPostprocessModel(GameObject root)
        {
            if(!File.Exists(Manifest)||!assetPath.StartsWith("Assets/_Project/Art/Environment/"))return;
            var data=JsonUtility.FromJson<ChairFacingManifest>(File.ReadAllText(Manifest));
            foreach(var entry in data.entries.Where(e=>e.asset==assetPath))
            {
                var chair=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>RelativePath(t,root.transform)==entry.node);if(chair==null)throw new Exception("Chair import path changed: "+entry.node);
                chair.Rotate(Vector3.up,entry.degrees,Space.World);
            }
            context.DependsOnSourceAsset(Manifest);
        }
    }
    public static partial class CommandBunkerUpgrade
    {
        static bool ChairNode(Transform t)=>t.name=="Chair"||System.Text.RegularExpressions.Regex.IsMatch(t.name,@"^Chair\.\d+$");
        static float Correction(Transform chair,Transform[] all)
        {
            var back=chair.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r=>r.name.StartsWith("Chair back"));
            var seat=chair.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r=>r.name.StartsWith("Seat"));
            var table=all.Where(t=>t.parent==chair.parent&&(t.name.StartsWith("Work table")||t.name.StartsWith("Mess table"))).OrderBy(t=>Vector3.Distance(t.position,chair.position)).FirstOrDefault();
            if(back==null||seat==null||table==null)return 0;
            var front=Vector3.ProjectOnPlane(seat.bounds.center-back.bounds.center,Vector3.up).normalized;
            var toward=Vector3.ProjectOnPlane(table.position-chair.position,Vector3.up).normalized;
            // Wide rows face the long edge of the table rather than converging on its centre.
            return Vector3.Angle(front,toward)>65?Vector3.SignedAngle(front,toward,Vector3.up):0;
        }
        static void RepairChairs()
        {
            if(File.Exists(CommandChairFacingImporter.Manifest))throw new Exception("Chair corrections are already installed; use audit to verify.");
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!="Assets/_Project/Scenes/Maps/Map 1.unity")throw new Exception("Open Map 1 first.");
            File.Copy(scene.path,"Library/CommandBunkerBackup/Map1_before_chairs.unity",true);
            var entries=new List<ChairFacingEntry>();
            foreach(var guid in AssetDatabase.FindAssets("t:Model",new[]{"Assets/_Project/Art/Environment"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset==null)continue;
                var all=asset.GetComponentsInChildren<Transform>(true);
                foreach(var chair in all.Where(ChairNode))
                {
                    var angle=Correction(chair,all);if(Mathf.Abs(angle)<1)continue;
                    entries.Add(new ChairFacingEntry{asset=path,node=CommandChairFacingImporter.RelativePath(chair,asset.transform),degrees=angle});
                }
            }
            var log=new StringBuilder();int changed=0;
            for(int i=1;i<=3;i++)
            {
                string code="C"+i;var camp=All<Transform>().Single(t=>t.name==code+" / Mission art");
                var original=camp.GetChild(0);
                var path="Assets/_Project/Art/Environment/Map1_ArtRefresh_v1/Meshes/"+(i==3?"C3_MainGarrison_Art":code+"_ApprovedArt")+".fbx";
                var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));source.name="Temporary chair source";
                source.transform.SetParent(camp,false);source.transform.SetLocalPositionAndRotation(original.localPosition,original.localRotation);source.transform.localScale=original.localScale;
                try
                {
                    var edits=entries.Where(e=>e.asset==path).ToArray();var extracted=new List<MeshRenderer>();
                    foreach(var edit in edits)
                    {
                        var chair=source.GetComponentsInChildren<Transform>(true).Single(t=>CommandChairFacingImporter.RelativePath(t,source.transform)==edit.node);
                        bool setAside=i==2&&chair.name=="Chair.001";
                        if(setAside)continue;
                        extracted.AddRange(chair.GetComponentsInChildren<MeshRenderer>());
                    }
                    RemoveChairTriangles(camp,extracted,code,log);
                    foreach(var edit in edits)
                    {
                        var chair=source.GetComponentsInChildren<Transform>(true).Single(t=>CommandChairFacingImporter.RelativePath(t,source.transform)==edit.node);var parentName=chair.parent.name;
                        bool setAside=i==2&&chair.name=="Chair.001";
                        if(setAside)
                        {
                            var retainedRadio=camp.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Chair.001"&&t.parent.name=="C2 chair set aside for standing operator");
                            retainedRadio.Rotate(Vector3.up,edit.degrees,Space.World);log.AppendLine("Retained radio chair position; rotated "+edit.degrees);changed++;continue;
                        }
                        var marker=original.GetComponentsInChildren<Transform>(true).Single(t=>t.name==chair.name&&t.parent.name==parentName);
                        foreach(var collider in marker.GetComponentsInChildren<Collider>())collider.enabled=false;
                        var retained=Object.Instantiate(chair.gameObject,camp);retained.name="Approved facing / "+parentName+" / "+chair.name;
                        retained.transform.SetPositionAndRotation(chair.position,Quaternion.AngleAxis(edit.degrees,Vector3.up)*chair.rotation);
                        retained.transform.localScale=chair.lossyScale;
                        // Camp roots are unit-scale, while the imported Blender basis sits below them.
                        retained.transform.localScale=new Vector3(chair.lossyScale.x/camp.lossyScale.x,chair.lossyScale.y/camp.lossyScale.y,chair.lossyScale.z/camp.lossyScale.z);
                        foreach(var renderer in retained.GetComponentsInChildren<MeshRenderer>())renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map1_ArtRefresh_v1/Materials/"+m.name+".mat")??m).ToArray();
                        log.AppendLine(code+" / "+parentName+" / "+chair.name+" rotated "+edit.degrees.ToString("F1"));changed++;
                    }
                }finally{Object.DestroyImmediate(source);}
            }
            // Register only after extracting the original static triangles, so the comparison is exact.
            File.WriteAllText(CommandChairFacingImporter.Manifest,JsonUtility.ToJson(new ChairFacingManifest{entries=entries.ToArray()},true));
            AssetDatabase.ImportAsset(CommandChairFacingImporter.Manifest,ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            foreach(var asset in entries.Select(e=>e.asset).Distinct())AssetDatabase.ImportAsset(asset,ImportAssetOptions.ForceUpdate);
            log.AppendLine("Corrected scene chairs="+changed+"; source chair corrections="+entries.Count+" across "+entries.Select(e=>e.asset).Distinct().Count()+" models.");
            File.WriteAllText(Reports+"/chair_repairs.txt",log.ToString());Audit();
        }
        static void RemoveChairTriangles(Transform camp,IEnumerable<MeshRenderer> extracted,string label,StringBuilder report)
        {
            var triangles=new Dictionary<Vector3Int,List<Vector3[]>>();int expected=0;
            Vector3Int Cell(Vector3 p)=>Vector3Int.FloorToInt(p/.02f);
            foreach(var renderer in extracted)
            {
                var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var matrix=camp.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
                var vs=mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();var ts=mesh.triangles;expected+=ts.Length/3;
                for(int i=0;i<ts.Length;i+=3){var tri=new[]{vs[ts[i]],vs[ts[i+1]],vs[ts[i+2]]};var key=Cell((tri[0]+tri[1]+tri[2])/3);if(!triangles.TryGetValue(key,out var list))triangles[key]=list=new List<Vector3[]>();list.Add(tri);}
            }
            bool Match(Vector3 a,Vector3 b,Vector3 c)
            {
                var cell=Cell((a+b+c)/3);var tri=new[]{a,b,c};
                for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
                    if(triangles.TryGetValue(cell+new Vector3Int(x,y,z),out var list))foreach(var other in list)
                        if(tri.All(v=>other.Any(w=>(v-w).sqrMagnitude<.000009f))&&other.All(v=>tri.Any(w=>(v-w).sqrMagnitude<.000009f)))return true;
                return false;
            }
            var changes=new List<(MeshFilter filter,Mesh copy,int removed,bool near)>();int nearRemoved=0,farRemoved=0;
            foreach(var filter in camp.GetComponentsInChildren<MeshFilter>().Where(f=>f.transform.parent!=null&&f.transform.parent.name.Contains("static material batches")))
            {
                var mesh=filter.sharedMesh;var matrix=camp.worldToLocalMatrix*filter.transform.localToWorldMatrix;var vs=mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();var ts=mesh.triangles;var keep=new List<int>();int removed=0;
                for(int i=0;i<ts.Length;i+=3){if(Match(vs[ts[i]],vs[ts[i+1]],vs[ts[i+2]])){removed++;continue;}keep.Add(ts[i]);keep.Add(ts[i+1]);keep.Add(ts[i+2]);}
                if(removed==0)continue;
                var copy=Object.Instantiate(mesh);copy.name=mesh.name+"_FacingChairs";copy.SetTriangles(keep,0);copy.RecalculateBounds();
                bool near=filter.transform.parent.name.StartsWith("LOD0");if(near)nearRemoved+=removed;else farRemoved+=removed;changes.Add((filter,copy,removed,near));
            }
            if(nearRemoved!=expected){foreach(var c in changes)Object.DestroyImmediate(c.copy);throw new Exception(label+" original chair triangles mismatch "+nearRemoved+" / "+expected);}
            Directory.CreateDirectory(Root+"/RepairedBatches");AssetDatabase.Refresh();
            foreach(var c in changes)
            {
                var path=AssetDatabase.GenerateUniqueAssetPath(Root+"/RepairedBatches/"+label+"_"+c.copy.name+".asset");AssetDatabase.CreateAsset(c.copy,path);c.filter.sharedMesh=c.copy;
                var collider=c.filter.GetComponent<MeshCollider>();if(collider!=null)collider.sharedMesh=c.copy;
            }
            report.AppendLine(label+" expected="+expected+" removed LOD0="+nearRemoved+" LOD1="+farRemoved);
        }
    }
}


