using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ShadowVale.Map01.Editor {
public static class CommanderPaddleGripSetup {
[MenuItem("ShadowVale/Cutscene/Prepare commander paddle grip")]
public static void Prepare(){
 const string path="Assets/Resources/Cutscenes/Commander_Paddle_Readable.asset";
 var source=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Characters/NPCs/chỉ huy mình.fbx").OfType<Mesh>().Single(m=>m.name=="Mesh_0.001");
 // Editor access can read imported model data without enabling Read/Write on the source FBX.
 var mesh=new Mesh{name="Commander paddle readable source",indexFormat=source.indexFormat,vertices=source.vertices,normals=source.normals,tangents=source.tangents,uv=source.uv,uv2=source.uv2,colors=source.colors,boneWeights=source.boneWeights,bindposes=source.bindposes,subMeshCount=source.subMeshCount};
 for(int i=0;i<source.subMeshCount;i++)mesh.SetIndices(source.GetIndices(i),source.GetTopology(i),i);
 mesh.bounds=source.bounds;
 var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);
 if(stored==null)AssetDatabase.CreateAsset(mesh,path);else {EditorUtility.CopySerialized(mesh,stored);EditorUtility.SetDirty(stored);Object.DestroyImmediate(mesh);}
 AssetDatabase.SaveAssets();AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
}
}
}
