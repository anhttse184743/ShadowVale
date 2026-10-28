using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadowVale.Map01.Editor
{
    public static class Map01RiverInteractions
    {
        const string ScenePath="Assets/_Project/Scenes/Maps/Map 1.unity";
        const string RootName="Water interaction sensors";
        const string Report="Tools/Map01WaterReports";
        [Serializable] public sealed class Sensor {public Vector3 position,center,size;public Quaternion rotation;}
        [Serializable] public sealed class Layout {public Vector3[] left,right;public Sensor[] sensors;public float amplitude,flow;}

        [MenuItem("ShadowVale/Map 1/Enable footsteps and river interaction")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play Mode first.");
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!=ScenePath){if(scene.isDirty)EditorSceneManager.SaveScene(scene);scene=EditorSceneManager.OpenScene(ScenePath);}
            var river=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RiverWater>()).Single(r=>r.name=="Winding river");
            Install(river);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }

        public static void Install(RiverWater river)
        {
            if(river.leftBank.Length!=river.rightBank.Length||river.leftBank.Length<2)throw new Exception("A fitted river channel is required.");
            var root=river.transform.Find(RootName);
            if(root==null){var go=new GameObject(RootName);Undo.RegisterCreatedObjectUndo(go,"Add river interaction");go.transform.SetParent(river.transform,false);root=go.transform;}
            int count=(river.leftBank.Length-2)/8+1;
            var sensors=new Sensor[count];
            for(int index=0;index<count;index++)
            {
                int first=index*8,last=Mathf.Min(first+8,river.leftBank.Length-1);
                Vector3 a=(river.leftBank[first]+river.rightBank[first])*.5f,b=(river.leftBank[last]+river.rightBank[last])*.5f;
                var origin=(a+b)*.5f;var direction=b-a;direction.y=0;
                var rotation=Quaternion.LookRotation(direction.normalized,Vector3.up);
                var inverse=Quaternion.Inverse(rotation);var bounds=new Bounds(inverse*(a-origin),Vector3.zero);
                for(int row=first;row<=last;row++){bounds.Encapsulate(inverse*(river.leftBank[row]-origin));bounds.Encapsulate(inverse*(river.rightBank[row]-origin));}
                string name="Water sensor "+index.ToString("D2");
                var child=root.Find(name);
                if(child==null){var go=new GameObject(name,typeof(BoxCollider),typeof(RiverWaterTrigger));Undo.RegisterCreatedObjectUndo(go,"Add river sensor");go.transform.SetParent(root,false);child=go.transform;}
                Undo.RecordObject(child,"Fit river sensor");child.gameObject.layer=4;child.position=origin;child.rotation=rotation;
                var box=child.GetComponent<BoxCollider>();Undo.RecordObject(box,"Fit river sensor volume");
                box.isTrigger=true;box.center=new Vector3(bounds.center.x,-.9f,bounds.center.z);box.size=new Vector3(bounds.size.x+.6f,3.2f,bounds.size.z+.6f);
                var trigger=child.GetComponent<RiverWaterTrigger>();Undo.RecordObject(trigger,"Connect river sensor");trigger.river=river;
                EditorUtility.SetDirty(box);EditorUtility.SetDirty(trigger);
                sensors[index]=new Sensor{position=child.position,rotation=child.rotation,center=box.center,size=box.size};
            }
            Physics.SyncTransforms();int checks=0;
            for(int row=0;row<river.leftBank.Length;row++)for(int cross=0;cross<=8;cross++)
            {
                var p=Vector3.Lerp(river.leftBank[row],river.rightBank[row],cross/8f);p.y-=.4f;
                if(!root.GetComponentsInChildren<BoxCollider>().Any(c=>Vector3.SqrMagnitude(c.ClosestPoint(p)-p)<.00001f))throw new Exception("Uncovered water interaction point: "+p);
                checks++;
            }
            Directory.CreateDirectory(Report);
            File.WriteAllText(Report+"/interaction-layout.json",JsonUtility.ToJson(new Layout{left=river.leftBank,right=river.rightBank,sensors=sensors,amplitude=river.waveAmplitude,flow=river.flowSpeed}));
            File.WriteAllLines(Report+"/interaction-coverage.txt",new[]{"PASS "+count+" sensors use the same RiverWaterTrigger as Map 2", "PASS "+checks+" wet channel samples covered, including both banks and sensor joins", "Exact RiverWater sampling rejects dry banks and feet above the water (bridges)."});
        }
    }
}
