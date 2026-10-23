using System.IO;
using UnityEditor;
using UnityEngine;
namespace ShadowVale.Map01.Editor {
    public static class DialogueVoiceSetup {
        [System.Serializable] sealed class OpeningTiming {public float[] commanderSubtitleStarts;}
        [MenuItem("ShadowVale/Audio/Prepare Vietnamese dialogue")]
        public static void Run() {
            Directory.CreateDirectory("Assets/Resources/Dialogue");
            File.Copy("SourceArt/Dialogue/voice-catalog.json","Assets/Resources/Dialogue/voice-catalog.json",true);
            AssetDatabase.Refresh();
            foreach(var path in Directory.GetFiles("Assets/Resources/Dialogue/VI","*.wav")) {
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(AudioImporter)AssetImporter.GetAtPath(path);
                importer.forceToMono=true;importer.loadInBackground=false;
                var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;
                settings.preloadAudioData=true;settings.compressionFormat=AudioCompressionFormat.PCM;settings.sampleRateSetting=AudioSampleRateSetting.OverrideSampleRate;settings.sampleRateOverride=48000;
                importer.defaultSampleSettings=settings;importer.SaveAndReimport();
            }
            string prefabPath="Assets/_Project/Map01/Resources/Cutscenes/Map01Opening.prefab";
            if(File.Exists("SourceArt/Dialogue/opening-voice-timing.json")) {
                var root=PrefabUtility.LoadPrefabContents(prefabPath);
                try {
                    var opening=root.GetComponent<Map01OpeningCutscene>();
                    opening.commanderVoice=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Dialogue/VI/m1_open_commander.wav");
                    opening.soldierVoice=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Dialogue/VI/m1_open_reply.wav");
                    opening.commanderSubtitleStarts=JsonUtility.FromJson<OpeningTiming>(File.ReadAllText("SourceArt/Dialogue/opening-voice-timing.json")).commanderSubtitleStarts;
                    PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
                } finally {PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();Debug.Log("Vietnamese dialogue prepared; opening uses the same character voices as later dialogue when aligned audio is available.");
        }
    }
}
