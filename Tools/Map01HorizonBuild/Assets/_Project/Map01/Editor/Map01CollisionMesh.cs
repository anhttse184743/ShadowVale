using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    /// <summary>Filtering out replaced buildings can leave a mesh asset with no collision faces.</summary>
    [InitializeOnLoad]
    public static class Map01CollisionMesh
    {
        const string Request="Tools/Map01CollisionCleanup.request";
        const string Report="Tools/Map01OptimizedReports/collision-cleanup.txt";
        static Map01CollisionMesh(){EditorApplication.update+=Poll;}
        public static bool HasUsableTriangle(Mesh mesh)
        {
            if(mesh==null||mesh.vertexCount<3)return false;
            var indices=mesh.triangles;if(indices.Length<3)return false;
            var vertices=mesh.vertices;
            for(int i=0;i+2<indices.Length;i+=3)
            {
                int a=indices[i],b=indices[i+1],c=indices[i+2];
                if(a<0||b<0||c<0||a>=vertices.Length||b>=vertices.Length||c>=vertices.Length)continue;
                float areaSquared=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).sqrMagnitude;
                if(!float.IsNaN(areaSquared)&&!float.IsInfinity(areaSquared)&&areaSquared>1e-12f)return true;
            }
            return false;
        }
        // Call only in editor builders, after detaching any asset about to be rewritten.
        public static bool Assign(MeshCollider collider,Mesh mesh)
        {
            collider.sharedMesh=null;
            if(HasUsableTriangle(mesh)){collider.sharedMesh=mesh;return true;}
            Object.DestroyImmediate(collider);
            return false;
        }
        static void Poll()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(Request);
            try{Repair();}catch(Exception e){File.WriteAllText(Report,"FAIL "+e);Debug.LogException(e);}
        }
        [MenuItem("ShadowVale/Map 1/Remove empty collision components")]
        public static void Repair()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!=OptimizedMapBuilder.ScenePath||scene.isDirty)throw new Exception("Save Map 1 before collision cleanup.");
            var colliders=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshCollider>(true)).ToArray();
            var invalid=colliders.Where(c=>c.sharedMesh!=null&&!HasUsableTriangle(c.sharedMesh)).ToArray();
            var lines=new List<string>();
            foreach(var c in invalid)
            {
                lines.Add("Removed empty MeshCollider: "+c.sharedMesh.name+"; indices="+c.sharedMesh.triangles.Length);
                Undo.DestroyObjectImmediate(c);
            }
            Physics.SyncTransforms();
            var remaining=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshCollider>(true)).ToArray();
            if(remaining.Any(c=>c.sharedMesh!=null&&!HasUsableTriangle(c.sharedMesh)))throw new Exception("Invalid collision mesh remains.");
            OptimizedMapBuilder.Validate();
            var jetty=GameObject.Find("North jetty • independent from Chunk_0_3");
            if(jetty==null||jetty.GetComponents<BoxCollider>().Count(c=>c.enabled)<3)throw new Exception("Replacement jetty floor/rails missing.");
            var deck=jetty.GetComponents<BoxCollider>().First(c=>c.size.y<.3f);
            if(!deck.Raycast(new Ray(jetty.transform.position+new Vector3(6,3,0),Vector3.down),out _,5))throw new Exception("Replacement jetty floor does not support player.");
            // Regression cases: both an empty index buffer and collinear faces must be rejected.
            var probe=new Mesh();
            try
            {
                probe.vertices=new[]{Vector3.zero,Vector3.right,Vector3.right*2};probe.triangles=new[]{0,1,2};
                if(HasUsableTriangle(probe))throw new Exception("Collinear triangle accepted.");
                probe.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};
                if(!HasUsableTriangle(probe))throw new Exception("Valid triangle rejected.");
                probe.triangles=Array.Empty<int>();if(HasUsableTriangle(probe))throw new Exception("Empty mesh accepted.");
            }
            finally{Object.DestroyImmediate(probe);}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            lines.Add($"PASS: removed {invalid.Length}; {remaining.Count(c=>c.sharedMesh!=null)} valid mesh colliders retained; all map routes pass; replacement jetty floor and rails retained; empty/degenerate/valid triangle checks pass.");
            File.WriteAllLines(Report,lines);
        }
    }
}
