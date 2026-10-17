using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    /// <summary>Import the user-approved Blender proposal; preserve existing gameplay anchors.</summary>
    [InitializeOnLoad]
    public static partial class CommandBunkerUpgrade
    {
        const string Root="Assets/_Project/Art/Environment/CommandBunker";
        const string Reports="Tools/CommandBunkerReports";
        const string Layout="Assets/_Project/Art/Environment/LayoutTest/Prefabs/";
        [Serializable] public class Export { public Model[] models; }
        [Serializable] public class Model { public string name; public Part[] parts; }
        [Serializable] public class Part { public string name,material,texture; public float[] @base,vertices,normals,uv; public int[] triangles; public float metallic,roughness; }
        static CommandBunkerUpgrade(){EditorApplication.update+=Poll;EditorApplication.update+=ProbeCommander;}
        static void Poll()
        {
            const string request="Tools/CommandBunker.request";
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            var command=File.ReadAllText(request).Trim();File.Delete(request);Directory.CreateDirectory(Reports);
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play Mode before changing assets.");
                if(command=="inspect")Inspect();
                else if(command=="apply")Apply();
                else if(command=="capture")Capture();
                else if(command=="audit")Audit();
                else if(command=="chairs")RepairChairs();
                else if(command=="verify")Verify();
                else if(command=="mountboard")MountBoard();
                else if(command=="commander")PlaceCommander();
                else if(command=="playcommander"){
                    SessionState.SetString("BunkerProbeStartScene",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                    EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/Maps/Map 1.unity");
                    SessionState.SetInt("BunkerCommanderProbe",1);EditorApplication.isPlaying=true;
                }
                else throw new Exception(command);
                File.WriteAllText(Reports+"/status.txt","PASS "+command);
            }
            catch(Exception e){File.WriteAllText(Reports+"/status.txt",e.ToString());Debug.LogException(e);}
        }
        static T[] All<T>()where T:Component=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
        static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
        static void Inspect()
        {
            var report=new StringBuilder();
            foreach(var t in All<Transform>().Where(t=>t.name=="BriefingTable"||t.name=="MissionBoard"||t.name=="workbench"||t.name.StartsWith("Chair")&&!t.name.Contains(" / ")&&!t.name.Contains("Chair ")))
            {
                report.AppendLine(PathOf(t)+" | "+t.position+" | rotation "+t.eulerAngles+" | prefab "+PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t));
                foreach(var c in t.GetComponentsInChildren<Renderer>(true))report.AppendLine("  "+c.name+" enabled="+c.enabled+" mesh="+(c.GetComponent<MeshFilter>()!=null&&c.GetComponent<MeshFilter>().sharedMesh!=null?c.GetComponent<MeshFilter>().sharedMesh.name:"none")+" bounds="+c.bounds);
            }
            foreach(var point in All<ForestPoint>().Where(p=>p.kind==ForestPointKind.Workbench))report.AppendLine("INTERACTION "+PathOf(point.transform)+" position="+point.transform.position+" radius="+point.radius);
            File.WriteAllText(Reports+"/inspection.txt",report.ToString());
        }
        static void Upsert(Object asset,string path)
        {
            var old=AssetDatabase.LoadAssetAtPath<Object>(path);
            if(old==null)AssetDatabase.CreateAsset(asset,path);else{EditorUtility.CopySerialized(asset,old);Object.DestroyImmediate(asset);}
        }
        static GameObject Import(Model model)
        {
            var root=new GameObject(model.name);
            foreach(var p in model.parts)
            {
                var vertices=Enumerable.Range(0,p.vertices.Length/3).Select(i=>new Vector3(p.vertices[i*3],p.vertices[i*3+1],p.vertices[i*3+2])).ToArray();
                var normals=Enumerable.Range(0,p.normals.Length/3).Select(i=>new Vector3(p.normals[i*3],p.normals[i*3+1],p.normals[i*3+2])).ToArray();
                if(normals.Any(n=>n.sqrMagnitude<.5f))throw new Exception("Invalid normals: "+p.name);
                var mesh=new Mesh{name=p.name,indexFormat=IndexFormat.UInt32};mesh.vertices=vertices;mesh.normals=normals;
                mesh.uv=Enumerable.Range(0,p.uv.Length/2).Select(i=>new Vector2(p.uv[i*2],p.uv[i*2+1])).ToArray();mesh.triangles=p.triangles;mesh.RecalculateBounds();mesh.RecalculateTangents();
                var meshPath=Root+"/"+p.name+".asset";Upsert(mesh,meshPath);
                var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=p.material};
                mat.SetColor("_BaseColor",new Color(p.@base[0],p.@base[1],p.@base[2],1).gamma);
                mat.SetFloat("_Metallic",p.metallic);mat.SetFloat("_Smoothness",1-p.roughness);
                if(!string.IsNullOrEmpty(p.texture))
                {
                    var texturePath=Root+"/"+p.texture;var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
                    importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.anisoLevel=4;importer.SaveAndReimport();
                    mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));mat.SetColor("_BaseColor",Color.white);
                }
                var matPath=Root+"/"+p.name+".mat";Upsert(mat,matPath);
                var child=new GameObject(p.material);child.transform.SetParent(root.transform,false);child.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                child.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            }
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/"+model.name+".prefab");Object.DestroyImmediate(root);return prefab;
        }
        static void Apply()
        {
            Directory.CreateDirectory("Library/CommandBunkerBackup");
            foreach(var name in new[]{"BriefingTable","MissionBoard"})
            {
                var backup="Library/CommandBunkerBackup/"+name+".prefab";
                if(!File.Exists(backup))File.Copy(Layout+name+".prefab",backup);
            }
            var data=JsonUtility.FromJson<Export>(File.ReadAllText(Root+"/ApprovedBunker.meshdata.json"));
            var imported=data.models.ToDictionary(m=>m.name,Import);
            var chairRoot=PrefabUtility.LoadPrefabContents(Root+"/Chair.prefab");
            try
            {
                var collider=chairRoot.AddComponent<BoxCollider>();collider.center=new Vector3(0,.46f,0);collider.size=new Vector3(.46f,.93f,.46f);
                var obstacle=chairRoot.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=collider.center;obstacle.size=collider.size;obstacle.carving=true;
                PrefabUtility.SaveAsPrefabAsset(chairRoot,Root+"/Chair.prefab");
            }finally{PrefabUtility.UnloadPrefabContents(chairRoot);}
            foreach(var name in new[]{"BriefingTable","MissionBoard"})
            {
                var path=Layout+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach(Transform child in root.transform.Cast<Transform>().Where(t=>t.name.StartsWith("Approved ")).ToArray())Object.DestroyImmediate(child.gameObject);
                    foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
                    var isTable=name=="BriefingTable";
                    var visual=(GameObject)PrefabUtility.InstantiatePrefab(imported[isTable?"Workbench":"MissionBoard"],root.scene);
                    visual.name="Approved Blender "+(isTable?"workbench":"mission board");visual.transform.SetParent(root.transform,false);
                    if(isTable)
                    {
                        var top=root.transform.Find("Table top");top.localPosition=new Vector3(0,.90f,0);top.localScale=new Vector3(2.8f,.095f,1.08f);
                        var obstacle=visual.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=new Vector3(0,.47f,0);obstacle.size=new Vector3(2.8f,.94f,1.08f);obstacle.carving=true;
                        int index=0;
                        foreach(float z in new[]{-1.03f,1.03f})foreach(float x in new[]{-.93f,0,.93f})
                        {
                            var chair=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Chair.prefab"),root.scene);
                            chair.name="Approved Chair "+(++index)+" facing table";chair.transform.SetParent(root.transform,false);chair.transform.localPosition=new Vector3(x,0,z);
                            chair.transform.localRotation=Quaternion.LookRotation(z<0?Vector3.forward:Vector3.back);
                            if(Vector3.Dot(chair.transform.forward,root.transform.position-chair.transform.position)<=0)throw new Exception("Chair faces away from table.");
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();
            Inspect();Audit();
            File.WriteAllText(Reports+"/application.txt","Approved Blender workbench and mission board installed in shared LayoutTest prefabs. Original roots, GUIDs, collider objects, ForestPoint and cutscene anchors preserved. Six inward-facing independent chairs. See chair_asset_audit.txt for all furniture source models.\n");
        }
        static void Audit()
        {
            var report=new StringBuilder();
            foreach(var guid in AssetDatabase.FindAssets("t:Model",new[]{"Assets/_Project/Art/Environment"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset==null)continue;
                var all=asset.GetComponentsInChildren<Transform>(true);
                foreach(var chair in all.Where(t=>t.name=="Chair"||System.Text.RegularExpressions.Regex.IsMatch(t.name,@"^Chair\.\d+$")))
                {
                    var back=chair.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r=>r.name.StartsWith("Chair back"));
                    var seat=chair.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r=>r.name.StartsWith("Seat"));
                    var table=all.Where(t=>t.parent==chair.parent&&(t.name.StartsWith("Work table")||t.name.StartsWith("Mess table"))).OrderBy(t=>Vector3.Distance(t.position,chair.position)).FirstOrDefault();
                    if(back==null||seat==null||table==null){report.AppendLine(path+" | "+PathOf(chair)+" | no matching seat/back/table");continue;}
                    var front=Vector3.ProjectOnPlane(seat.bounds.center-back.bounds.center,Vector3.up).normalized;
                    var toward=Vector3.ProjectOnPlane(table.position-chair.position,Vector3.up).normalized;
                    report.AppendLine(path+" | "+PathOf(chair)+" | table="+table.name+" distance="+Vector3.Distance(table.position,chair.position).ToString("F2")+" angle="+Vector3.Angle(front,toward).ToString("F1"));
                }
            }
            File.WriteAllText(Reports+"/chair_asset_audit.txt",report.ToString());
            var live=new StringBuilder();
            foreach(var t in All<Transform>().Where(t=>t.name.StartsWith("Approved Chair ")))
                live.AppendLine(PathOf(t)+" front="+t.forward+" dot to table="+Vector3.Dot(t.forward,(t.parent.position-t.position).normalized).ToString("F3"));
            File.WriteAllText(Reports+"/chair_validation.txt",live.ToString());
        }
        static void Capture()
        {
            var table=All<Transform>().Single(t=>t.name=="BriefingTable");var board=All<Transform>().Single(t=>t.name=="MissionBoard");
            Render(table.position+new Vector3(3.3f,2.8f,-3.8f),table.position+new Vector3(0,.7f,0),"01_bunker_workbench",55);
            Render(board.position+new Vector3(1.15f,1.95f,-3),board.position+new Vector3(0,1.8f,0),"02_bunker_board",55);
        }
        static void Verify()
        {
            var report=new StringBuilder();var table=All<Transform>().Single(t=>t.name=="BriefingTable");
            var point=All<ForestPoint>().Single(p=>p.kind==ForestPointKind.Workbench);
            if(point.id!="workbench"||point.radius!=4||Vector3.Distance(point.transform.position,table.position)>2)throw new Exception("Workbench interaction anchor changed.");
            report.AppendLine("PASS existing crafting point, radius 4 m, and BriefingTable cutscene anchor preserved.");
            var chairs=table.Cast<Transform>().Where(t=>t.name.StartsWith("Approved Chair ")).ToArray();
            if(chairs.Length!=6||chairs.Any(t=>Vector3.Dot(t.forward,table.position-t.position)<=0))throw new Exception("Bunker chair orientation failed.");
            report.AppendLine("PASS all six bunker chairs face inward; original merged furniture renderers are disabled.");
            if(table.GetComponentsInChildren<Renderer>().Any(r=>r.name.StartsWith("Geometry ")&&r.enabled))throw new Exception("Duplicate original table geometry.");
            var board=All<Transform>().Single(t=>t.name=="MissionBoard");
            if(board.GetComponentsInChildren<Renderer>().Count(r=>r.enabled)<5)throw new Exception("Missing mission board details.");
            var paths=0;
            if(!NavMesh.SamplePosition(table.position+new Vector3(-3,0,-2),out var start,3,NavMesh.AllAreas))throw new Exception("No bunker navigation origin.");
            foreach(var offset in new[]{new Vector3(-2.0f,0,0),new Vector3(2.0f,0,0),new Vector3(0,0,-1.9f),new Vector3(0,0,1.9f)})
            {
                if(!NavMesh.SamplePosition(table.position+offset,out var end,1.5f,NavMesh.AllAreas))continue;
                var path=new NavMeshPath();if(NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete&&Vector3.Distance(end.position,point.transform.position)<point.radius)paths++;
            }
            if(paths==0)throw new Exception("Cannot reach crafting interaction range.");
            report.AppendLine("PASS "+paths+" complete navigation paths into crafting interaction range (editor NavMesh).");
            var errors=new List<string>();int count=0;
            foreach(var entry in JsonUtility.FromJson<ChairFacingManifest>(File.ReadAllText(CommandChairFacingImporter.Manifest)).entries)
            {
                var root=AssetDatabase.LoadAssetAtPath<GameObject>(entry.asset);var all=root.GetComponentsInChildren<Transform>(true);
                var chair=all.Single(t=>CommandChairFacingImporter.RelativePath(t,root.transform)==entry.node);var angle=Correction(chair,all);
                if(Mathf.Abs(angle)>1)errors.Add(entry.asset+" / "+entry.node);count++;
            }
            if(errors.Count>0)throw new Exception("Source chairs still facing away: "+string.Join("; ",errors));
            report.AppendLine("PASS "+count+" source chair corrections verified after FBX reimport.");
            foreach(var material in AssetDatabase.FindAssets("t:Material",new[]{Root}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>))
            {if(material.shader==null||material.shader.name.Contains("Error"))throw new Exception("Invalid material.");material.enableInstancing=true;EditorUtility.SetDirty(material);}
            foreach(var filter in table.GetComponentsInChildren<MeshFilter>().Concat(board.GetComponentsInChildren<MeshFilter>()))
                if(filter.sharedMesh==null)throw new Exception("Missing furniture mesh.");
            AssetDatabase.SaveAssets();Inspect();Audit();Capture();
            for(int i=1;i<=3;i++)
            {
                var camp=All<Transform>().Single(t=>t.name=="C"+i+" / Mission art");
                var chair=camp.GetComponentsInChildren<Transform>().First(t=>t.name.StartsWith("Approved facing / ")&&(i!=3||t.name.EndsWith("Chair.008")));
                var target=chair.position+Vector3.up*.65f;
                if(i==3)
                {
                    var back=chair.GetComponentsInChildren<MeshRenderer>().First(r=>r.name.StartsWith("Chair back"));
                    var seat=chair.GetComponentsInChildren<MeshRenderer>().First(r=>r.name.StartsWith("Seat"));
                    var front=Vector3.ProjectOnPlane(seat.bounds.center-back.bounds.center,Vector3.up).normalized;
                    Render(chair.position+front*.2f+Vector3.Cross(Vector3.up,front)*.65f+Vector3.up*1.9f,target+front*1.3f,"05_camp_chairs",75);
                }
                else Render(target+camp.right*1.4f-camp.forward*1.6f+Vector3.up*.9f,target,"0"+(i+2)+"_camp_chairs",65);
            }
            File.WriteAllText(Reports+"/validation.txt",report.ToString());
        }
        static void Render(Vector3 at,Vector3 target,string name,float fov)
        {
            var go=new GameObject("Bunker review camera"){hideFlags=HideFlags.HideAndDontSave};var camera=go.AddComponent<Camera>();
            var source=All<Camera>().FirstOrDefault(c=>c.CompareTag("MainCamera"));if(source!=null)camera.CopyFrom(source);
            camera.enabled=false;camera.orthographic=false;camera.useOcclusionCulling=false;camera.nearClipPlane=.03f;camera.fieldOfView=fov;camera.transform.SetPositionAndRotation(at,Quaternion.LookRotation(target-at));
            camera.cullingMask=~0;
            var rt=new RenderTexture(1440,1000,24,RenderTextureFormat.ARGB32);rt.Create();camera.targetTexture=rt;
            var previous=RenderTexture.active;bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
            try
            {
                camera.Render();camera.Render();RenderTexture.active=rt;var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();File.WriteAllBytes(Reports+"/"+name+".png",texture.EncodeToPNG());Object.DestroyImmediate(texture);
            }
            finally{RenderTexture.active=previous;ShaderUtil.allowAsyncCompilation=async;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
        }
    }
}

