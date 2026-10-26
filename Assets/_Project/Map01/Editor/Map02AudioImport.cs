using System.IO;
using UnityEditor;
using UnityEngine;
namespace ShadowVale.Map01.Editor
{
    /// <summary>Import settings for the Map 2 Day 1 / Night 1 sound (SourceArt/Audio/Map02_DayOne_20261025/build_audio.py):
    /// levels are mastered in the build, so nothing is normalised; long beds stay compressed in memory.</summary>
    public static class Map02AudioImport
    {
        [MenuItem("ShadowVale/Map 2/Import Day 1 sound")]
        public static void Run()
        {
            foreach(var path in Directory.GetFiles("Assets/Resources/Map02DayOne","*.wav"))
            {
                var importer=(AudioImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));if(importer==null)continue;
                var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path.Replace('\\','/'));bool bed=clip!=null&&clip.length>10;
                var settings=importer.defaultSampleSettings;settings.loadType=bed?AudioClipLoadType.CompressedInMemory:AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=bed?.6f:.85f;settings.preloadAudioData=true;
                importer.defaultSampleSettings=settings;importer.forceToMono=false;importer.loadInBackground=bed;
                var serialized=new SerializedObject(importer);var normalize=serialized.FindProperty("normalize")??serialized.FindProperty("m_Normalize");
                if(normalize!=null){normalize.boolValue=false;serialized.ApplyModifiedPropertiesWithoutUndo();}else Debug.LogWarning("MAP02_AUDIO no normalize property on "+path);
                importer.SaveAndReimport();Debug.Log($"MAP02_AUDIO {path} {(clip!=null?clip.length:0):F1}s {(bed?"bed":"one-shot")}");
            }
            Debug.Log("MAP02_AUDIO_IMPORT_PASS");
        }
    }
}
