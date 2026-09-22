using UnityEditor;
using UnityEngine;

namespace ShadowVale.Map01.Editor
{
    public sealed class MapModelImporter : AssetPostprocessor
    {
        bool IsMap => assetPath == "Assets/_Project/Art/Environment/Map01_Blender/Map01_Environment.fbx";
        void OnPreprocessModel()
        {
            if(!IsMap)return;
            var importer=(ModelImporter)assetImporter;
            importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;
            importer.importBlendShapes=false;importer.isReadable=false;
            importer.meshCompression=ModelImporterMeshCompression.Low;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        }
        Material OnAssignMaterialModel(Material material,Renderer renderer)
        {
            if(!IsMap)return null;
            string name=material.name.Contains("Stream")?"StreamPalette":"MapPalette";
            return AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01_Optimized/Materials/"+name+".mat");
        }
    }
}
