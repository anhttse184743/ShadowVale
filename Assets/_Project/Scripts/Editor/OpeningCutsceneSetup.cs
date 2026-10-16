using System.IO;
using System.Linq;
using ShadowVale.Map01;
using UnityEditor;
using UnityEngine;

namespace ShadowVale.Editor
{
    /// <summary>One resource prefab references existing clips; no copied meshes or controllers.</summary>
    public static class OpeningCutsceneSetup
    {
        [System.Serializable] private sealed class OpeningTiming {public float[] commanderSubtitleStarts;}
        public const string Folder = "Assets/_Project/Art/cutsence";
        public const string PrefabPath = "Assets/_Project/Map01/Resources/Cutscenes/Map01Opening.prefab";
        [InitializeOnLoadMethod]
        private static void OnReload()
        {
            EditorApplication.delayCall += () => {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !File.Exists(PrefabPath)
                    && File.Exists(Folder + "/Salute.fbx")) Prepare();
            };
        }
        [MenuItem("ShadowVale/Cutscene/Prepare opening")]
        public static void Prepare()
        {
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/_Project/Art/Characters/Animations/Generated/Idle_Tuned.anim");
            if (idle == null) { Debug.LogError("Existing Idle_Tuned is missing."); return; }
            var salute = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Characters/Animations/Generated/Nam_Salute_Briefing.anim") ?? Import("Salute", false);
            var talking = Import("Talking", true);
            var pointing = Import("Pointing", false);
            if (salute == null) { Debug.LogError("Salute import failed."); return; }
            var root = new GameObject("Map01Opening");
            try {
                var cutscene = root.AddComponent<Map01OpeningCutscene>();
                cutscene.idle = idle; cutscene.salute = salute;
                cutscene.soldierIdle=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Characters/Animations/Generated/Nam_Briefing_Idle.anim");
                cutscene.talking = talking; cutscene.pointing = pointing;
                cutscene.commanderVoice = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/Commander.mp3");
                cutscene.soldierVoice = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/Soldier.mp3");
                var alignedCommander=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Dialogue/VI/m1_open_commander.wav");
                var alignedNam=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Dialogue/VI/m1_open_reply.wav");
                if(alignedCommander!=null && alignedNam!=null && File.Exists("SourceArt/Dialogue/opening-voice-timing.json")) {
                    cutscene.commanderVoice=alignedCommander;cutscene.soldierVoice=alignedNam;
                    cutscene.commanderSubtitleStarts=JsonUtility.FromJson<OpeningTiming>(File.ReadAllText("SourceArt/Dialogue/opening-voice-timing.json")).commanderSubtitleStarts;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                AssetDatabase.Refresh();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[OpeningCutscene] Ready: existing idle + Talking, Pointing, Salute. No copied character meshes.");
            } finally { Object.DestroyImmediate(root); }
        }

        private static AnimationClip Import(string name, bool loop)
        {
            string path = Folder + "/" + name + ".fbx";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return null;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips) {
                clip.name = name; clip.loopTime = loop;
                clip.lockRootRotation = true; clip.lockRootPositionXZ = true; clip.lockRootHeightY = true;
                clip.keepOriginalOrientation = true; clip.keepOriginalPositionXZ = true; clip.keepOriginalPositionY = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            var result = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (result != null) Debug.Log("[OpeningCutscene] " + name + ": " + result.length + "s, humanoid=" + result.humanMotion);
            return result;
        }
    }
}

