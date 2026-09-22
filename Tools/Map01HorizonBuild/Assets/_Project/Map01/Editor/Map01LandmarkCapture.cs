using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
 [InitializeOnLoad] public static class Map01LandmarkCapture
 {
  static Map01LandmarkCapture(){EditorApplication.update+=Poll;}
  static void Poll(){const string request="Tools/Map01LandmarkCapture.request";if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(request))return;File.Delete(request);Capture();}
  static void Capture()
  {
   var main=Camera.main;var go=new GameObject("Temporary landmark capture");var camera=go.AddComponent<Camera>();camera.CopyFrom(main);camera.enabled=false;camera.orthographic=false;camera.fieldOfView=55;camera.farClipPlane=600;
   try
   {
    var bunker=GameObject.Find("Bunker_Entrance");Shot(camera,bunker.transform.position+new Vector3(10,7,15),bunker.transform.position+new Vector3(0,1.4f,2),"bunker");
    Shot(camera,new Vector3(27,14,-23),new Vector3(7.65f,3,0),"bridge");
    var house=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.StartsWith("House ")||t.name.StartsWith("Village thatch ")).OrderByDescending(t=>t.position.y).First();
    Shot(camera,house.position+new Vector3(12,6,-16),house.position+new Vector3(0,1.2f,-1),"stilts");
    Shot(camera,new Vector3(-43,13,-33),new Vector3(-58,7,-42),"canopy");
   }
   finally{Object.DestroyImmediate(go);}
  }
  static void Shot(Camera camera,Vector3 position,Vector3 target,string name)
  {
   camera.transform.position=position;camera.transform.LookAt(target);var rt=new RenderTexture(1440,1000,24);var old=RenderTexture.active;var image=new Texture2D(1440,1000,TextureFormat.RGB24,false);
   try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1440,1000),0,0);image.Apply();File.WriteAllBytes("Tools/Map01OptimizedReports/sept22-"+name+".png",image.EncodeToPNG());}
   finally{RenderTexture.active=old;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);}
  }
 }
}
