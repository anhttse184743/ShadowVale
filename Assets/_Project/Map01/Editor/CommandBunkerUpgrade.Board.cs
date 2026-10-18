using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    public static partial class CommandBunkerUpgrade
    {
        static void MountBoard()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!="Assets/_Project/Scenes/Maps/Map 1.unity")throw new Exception("Open Map 1 first.");
            var board=All<Transform>().Single(t=>t.name=="MissionBoard");
            var direction=board.forward;var report=new StringBuilder();var probes=new List<GameObject>();
            float closestWall=float.PositiveInfinity;
            try
            {
                foreach(var filter in board.parent.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer=filter.GetComponent<MeshRenderer>();
                    if(filter.sharedMesh==null||renderer==null||!renderer.enabled||!renderer.gameObject.activeInHierarchy||filter.transform.IsChildOf(board))continue;
                    if(Vector3.Distance(renderer.bounds.ClosestPoint(board.position+Vector3.up*1.8f),board.position+Vector3.up*1.8f)>5)continue;
                    var go=new GameObject("Temporary wall surface probe"){hideFlags=HideFlags.HideAndDontSave};probes.Add(go);
                    go.transform.SetPositionAndRotation(filter.transform.position,filter.transform.rotation);go.transform.localScale=filter.transform.lossyScale;
                    var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;
                    foreach(float x in new[]{-1.1f,0,1.1f})foreach(float y in new[]{1.15f,1.8f,2.5f})
                    {
                        var origin=board.TransformPoint(new Vector3(x,y,-.4f));
                        if(collider.Raycast(new Ray(origin,direction),out var hit,5)&&Vector3.Dot(hit.normal,-direction)>.5f)
                        {
                            float plane=Vector3.Dot(hit.point,direction);closestWall=Mathf.Min(closestWall,plane);
                            report.AppendLine(filter.name+" sample="+x+","+y+" wall="+hit.point+" normal="+hit.normal);
                        }
                    }
                }
                if(float.IsInfinity(closestWall))throw new Exception("No rendered wall surface behind mission board.");
                float back=float.NegativeInfinity;
                foreach(var filter in board.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null||!renderer.enabled)continue;
                    foreach(var vertex in filter.sharedMesh.vertices)back=Mathf.Max(back,Vector3.Dot(filter.transform.TransformPoint(vertex),direction));
                }
                const float gap=.003f;float move=closestWall-back-gap;
                if(Mathf.Abs(move)>2)throw new Exception("Unexpected wall offset: "+move);
                Directory.CreateDirectory("Library/CommandBunkerBackup");
                var backup="Library/CommandBunkerBackup/Map1_before_board_mount.unity";if(!File.Exists(backup))File.Copy(scene.path,backup);
                var before=board.position;board.position+=direction*move;
                PrefabUtility.RecordPrefabInstancePropertyModifications(board);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                report.AppendLine("Before="+before.ToString("F4")+" after="+board.position.ToString("F4")+" movement="+move.ToString("F4")+" m; backing-to-wall gap="+gap.ToString("F4")+" m");
            }
            finally{foreach(var probe in probes)Object.DestroyImmediate(probe);}
            File.WriteAllText(Reports+"/board_wall_mount.txt",report.ToString());
            Capture();
            Render(board.position+new Vector3(2.1f,1.9f,-.75f),board.position+new Vector3(0,1.8f,0),"06_board_wall_side",62);
        }
    }
}
