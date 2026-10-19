using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad]
    public static class BoundaryGroundRepair
    {
        const string Reports="Tools/BoundaryGroundReports";
        static BoundaryGroundRepair(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            const string request="Tools/BoundaryGround.request";
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            var command=File.ReadAllText(request).Trim();File.Delete(request);Directory.CreateDirectory(Reports);
            try { if(command=="apply")Apply();else Inspect(); File.WriteAllText(Reports+"/status.txt","PASS "+command); }
            catch(Exception e){File.WriteAllText(Reports+"/status.txt",e.ToString());Debug.LogException(e);}
        }
        static void Inspect()
        {
            var scene=EditorSceneManager.GetActiveScene();var log=new StringBuilder("Scene="+scene.path+" dirty="+scene.isDirty+"\n");
            var view=SceneView.lastActiveSceneView;
            if(view!=null)log.AppendLine("View pivot="+view.pivot+" camera="+view.camera.transform.position+" forward="+view.camera.transform.forward);
            log.AppendLine("Selection="+(Selection.activeGameObject!=null?Selection.activeGameObject.name:"none"));
            foreach(var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                var r=filter.GetComponent<Renderer>();if(r==null)continue;var b=r.bounds;
                if((b.size.x>150&&b.size.z>150)||filter.name.ToLowerInvariant().Contains("ground ring")||filter.name.ToLowerInvariant().Contains("boundary"))
                    log.AppendLine(filter.name+" position="+filter.transform.position+" bounds="+b+" mesh="+AssetDatabase.GetAssetPath(filter.sharedMesh)+" enabled="+r.enabled+" active="+filter.gameObject.activeInHierarchy+" mats="+string.Join(",",r.sharedMaterials.Select(m=>m!=null?m.name:"NULL")));
            }
            foreach(var root in scene.GetRootGameObjects())log.AppendLine("ROOT "+root.name+" pos="+root.transform.position);
            var ground=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Single(f=>f.name=="Continuous hills — 220 x 200");
            var ring=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Single(f=>f.name=="Ground ring");
            var points=ground.sharedMesh.vertices.Select(v=>ground.transform.TransformPoint(v)).ToDictionary(v=>new Vector2(Mathf.Round(v.x),Mathf.Round(v.z)),v=>v.y);
            var differences=ring.sharedMesh.vertices.Select(v=>ring.transform.TransformPoint(v)).Where(v=>Mathf.Abs(v.x)<=110.01f&&Mathf.Abs(v.z)<=100.01f).Select(v=>(v,d:points.TryGetValue(new Vector2(Mathf.Round(v.x),Mathf.Round(v.z)),out var y)?y-v.y:float.NaN)).OrderByDescending(p=>Mathf.Abs(p.d)).ToArray();
            log.AppendLine("Ground vertices="+ground.sharedMesh.vertexCount+" ring vertices="+ring.sharedMesh.vertexCount+" seam vertices="+differences.Length);
            foreach(var p in differences.Take(20))log.AppendLine("SEAM "+p.v+" ground-minus-ring="+p.d);
            File.WriteAllText(Reports+"/inspection.txt",log.ToString());
        }
        static void Apply()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!="Assets/_Project/Scenes/Maps/Map 1.unity")throw new Exception("Map 1 must be open.");
            var ground=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Single(f=>f.name=="Continuous hills — 220 x 200");
            var ring=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Single(f=>f.name=="Ground ring");
            var mesh=ring.sharedMesh;var vertices=mesh.vertices;
            var points=ground.sharedMesh.vertices.Select(v=>ground.transform.TransformPoint(v)).ToDictionary(v=>new Vector2(Mathf.Round(v.x),Mathf.Round(v.z)),v=>v.y);
            var corrections=vertices.Select(v=>ring.transform.TransformPoint(v)).Where(v=>Mathf.Abs(v.x)<=110.01f&&Mathf.Abs(v.z)<=100.01f).ToDictionary(v=>new Vector2(Mathf.Round(v.x),Mathf.Round(v.z)),v=>points[new Vector2(Mathf.Round(v.x),Mathf.Round(v.z))]-v.y);
            float Delta(Vector3 p)
            {
                float x=Mathf.Clamp(p.x,-110,110),z=Mathf.Clamp(p.z,-100,100);
                float distance=Vector2.Distance(new Vector2(x,z),new Vector2(p.x,p.z));
                if(distance>=12)return 0;
                bool eastWest=Mathf.Abs(p.x)>=110;
                if(!eastWest&&Mathf.Abs(p.z)<100)return 0;
                float along=eastWest?z:x,lo=Mathf.Floor(along),hi=Mathf.Ceil(along);
                var a=eastWest?new Vector2(x,lo):new Vector2(lo,z);
                var b=eastWest?new Vector2(x,hi):new Vector2(hi,z);
                float offset=Mathf.Lerp(corrections[a],corrections[b],along-lo);
                return offset*(1-Mathf.SmoothStep(0,1,distance/12));
            }
            Directory.CreateDirectory("Library/BoundaryGroundBackup");
            string path=AssetDatabase.GetAssetPath(mesh);
            if(!File.Exists("Library/BoundaryGroundBackup/GroundRing.asset"))File.Copy(path,"Library/BoundaryGroundBackup/GroundRing.asset");
            if(!File.Exists("Library/BoundaryGroundBackup/Map1.unity"))File.Copy(scene.path,"Library/BoundaryGroundBackup/Map1.unity");
            Capture("before");
            int changed=0,plants=0;
            for(int i=0;i<vertices.Length;i++)
            {
                var world=ring.transform.TransformPoint(vertices[i]);float delta=Delta(world);
                if(Mathf.Abs(delta)<.000001f)continue;
                vertices[i]=ring.transform.InverseTransformPoint(world+Vector3.up*delta);changed++;
            }
            var forest=ring.transform.parent.Find("Near forest");
            if(forest!=null)foreach(Transform tree in forest)
            {
                float delta=Delta(tree.position);if(Mathf.Abs(delta)<.000001f)continue;
                Undo.RecordObject(tree,"Align boundary vegetation");tree.position+=Vector3.up*delta;plants++;
                PrefabUtility.RecordPrefabInstancePropertyModifications(tree);
            }
            Undo.RecordObject(mesh,"Close boundary ground seam");
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var collider=ring.GetComponent<MeshCollider>();if(collider!=null){collider.sharedMesh=null;collider.sharedMesh=mesh;}
            float worst=0;int checks=0;
            foreach(var v in vertices.Select(v=>ring.transform.TransformPoint(v)))
                if(Mathf.Abs(v.x)<=110.01f&&Mathf.Abs(v.z)<=100.01f){worst=Mathf.Max(worst,Mathf.Abs(v.y-points[new Vector2(Mathf.Round(v.x),Mathf.Round(v.z))]));checks++;}
            if(worst>.0001f)throw new Exception("Seam validation failed: "+worst);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Capture("after");
            File.WriteAllText(Reports+"/validation.txt",$"PASS: {checks} seam vertices match current terrain; maximum height gap {worst:F6}m. Adjusted {changed} outer-ground vertices within 12m blend band and {plants} nearby plants. Playable terrain, boundary colliders and NavMesh unchanged.");
            Inspect();
        }
        static void Capture(string label)
        {
            var go=new GameObject("Boundary ground review camera"){hideFlags=HideFlags.HideAndDontSave};
            var camera=go.AddComponent<Camera>();var source=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(c=>c!=camera&&c.gameObject.scene.IsValid());
            if(source!=null)camera.CopyFrom(source);
            camera.enabled=false;camera.useOcclusionCulling=false;camera.fieldOfView=55;
            var target=new RenderTexture(1280,800,24);var picture=new Texture2D(1280,800,TextureFormat.RGB24,false);var active=RenderTexture.active;
            bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
            try
            {
                int n=0;
                foreach(var view in new[]{(new Vector3(125,34,6),new Vector3(110,22,24)),(new Vector3(105,23.6f,22),new Vector3(116,22,23))})
                {
                    camera.transform.position=view.Item1;camera.transform.LookAt(view.Item2);camera.targetTexture=target;camera.Render();camera.Render();RenderTexture.active=target;
                    picture.ReadPixels(new Rect(0,0,1280,800),0,0);picture.Apply();File.WriteAllBytes(Reports+"/"+label+"-"+(n++==0?"overview":"eye")+".png",picture.EncodeToPNG());
                }
            }
            finally { ShaderUtil.allowAsyncCompilation=async;RenderTexture.active=active;Object.DestroyImmediate(go);Object.DestroyImmediate(target);Object.DestroyImmediate(picture); }
        }
    }
}
