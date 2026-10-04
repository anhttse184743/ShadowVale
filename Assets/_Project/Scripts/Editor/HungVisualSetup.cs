using System.IO;
using System.Linq;
using ShadowVale.Map01;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShadowVale.Editor
{
    public static class HungVisualSetup
    {
        private const string Model = "Assets/_Project/Art/Characters/NPCs/hung.fbx";
        private const string NpcFolder = "Assets/_Project/Art/Characters/NPCs/";
        private const string ClipFolder = NpcFolder + "Animations/";
        private const string BaseMaterial = NpcFolder + "Materials/hung - Material.001.mat";
        public const string WoundedMaterialPath = NpcFolder + "Materials/hung - Wounded.mat";
        public const string RopeMaterialPath = NpcFolder + "Materials/Hung_Rope.mat";
        private const string WoundsTexture = NpcFolder + "hung Textures/texture_pbr_20250901_wounds.png";

        // Hùng's own clips, retargeted in Blender onto his rig (chuyen_dong/Hung_*.blend). Name -> loops.
        private static readonly (string file, bool loop)[] HungClips = {
            ("Hung_BiTroi", true),          // kneeling at the jetty, wrists tied behind his back
            ("Hung_DungDay", false),        // freed: gets up off his knees
            ("Hung_DiKhapKhieng", true),    // limping home during the escort
            ("Hung_TrungDan", false),       // shot: hit in the shoulder and falls
        };

        [MenuItem("ShadowVale/Characters/Prepare Hung")]
        public static void Prepare()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importCameras = false; importer.importLights = false;
            importer.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Avatar>().First();
            foreach (var (file, loop) in HungClips) ImportClip(ClipFolder + file + ".fbx", loop, avatar);
            var root = new GameObject("HungVisual");
            try {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
                model.transform.SetParent(root.transform, false);
                var actor = model.GetComponentInChildren<Animator>();
                if (actor == null || actor.avatar == null || !actor.avatar.isValid || !actor.avatar.isHuman)
                    throw new System.InvalidOperationException("Hung avatar is not valid Humanoid.");
                actor.runtimeAnimatorController = BuildController();
                actor.applyRootMotion = false;
                var renderers = model.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float scale = 1.78f / bounds.size.y;
                model.transform.localScale *= scale;
                model.transform.localPosition = new Vector3(0, -bounds.min.y * scale, 0);
                // Khoa's textured material, copied with the beating painted into the base colour.
                var material = WoundedMaterial();
                foreach (var renderer in renderers)
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                var visual = root.AddComponent<Map01HungVisual>(); visual.actor = actor;
                visual.ropeMaterial = RopeMaterial();
                string folder = "Assets/_Project/Map01/Resources/Characters";
                Directory.CreateDirectory(folder); AssetDatabase.Refresh();
                PrefabUtility.SaveAsPrefabAsset(root, folder + "/HungVisual.prefab");
                AssetDatabase.SaveAssets();
                Debug.Log("[HungVisual] Valid Humanoid, height 1.78m, wounded material, captive/stand-up/limp/hit clips wired to the quest.");
            } finally { Object.DestroyImmediate(root); }
        }

        /// <summary>Humanoid clip, pose kept exactly as authored: no root drift, height or turn leaks into the agent.</summary>
        private static void ImportClip(string path, bool loop, Avatar avatar)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path)
                ?? throw new FileNotFoundException("Missing Hung clip — export it from chuyen_dong first.", path);
            importer.animationType = ModelImporterAnimationType.Human;
            // The clip files carry no mesh, hence no bind pose: an avatar built from them would take
            // the first frame (on his knees) as his T-pose. Read them through his own avatar instead.
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            var source = importer.defaultClipAnimations.FirstOrDefault()
                ?? throw new System.InvalidOperationException(path + " has no animation take.");
            source.name = Path.GetFileNameWithoutExtension(path);
            source.loopTime = loop; source.loopPose = false;
            source.lockRootRotation = true; source.keepOriginalOrientation = true;
            // Height from his feet: kneeling, getting up and falling all keep him on the ground he stands on.
            source.lockRootHeightY = true; source.keepOriginalPositionY = false; source.heightFromFeet = true;
            source.lockRootPositionXZ = true; source.keepOriginalPositionXZ = true;
            importer.clipAnimations = new[] { source };
            importer.SaveAndReimport();
        }

        private static Material WoundedMaterial()
        {
            var original = AssetDatabase.LoadAssetAtPath<Material>(BaseMaterial);
            if (original == null || original.GetTexture("_BaseMap") == null)
                throw new System.InvalidOperationException("Hung's dedicated material and base texture are required.");
            var wounds = AssetDatabase.LoadAssetAtPath<Texture2D>(WoundsTexture)
                ?? throw new FileNotFoundException("Hung's wounds texture is missing.", WoundsTexture);
            var material = AssetDatabase.LoadAssetAtPath<Material>(WoundedMaterialPath);
            if (material == null) {
                material = new Material(original) { name = "hung - Wounded" };
                AssetDatabase.CreateAsset(material, WoundedMaterialPath);
            } else material.CopyPropertiesFromMaterial(original);
            material.SetTexture("_BaseMap", wounds);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", wounds);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material RopeMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(RopeMaterialPath);
            if (material == null) {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { name = "Hung_Rope" };
                AssetDatabase.CreateAsset(material, RopeMaterialPath);
            }
            var hemp = new Color(.16f, .11f, .055f);       // same dark, dirty hemp as the Blender rope
            material.SetColor("_BaseColor", hemp);
            if (material.HasProperty("_Color")) material.SetColor("_Color", hemp);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AnimatorController BuildController()
        {
            const string path = "Assets/_Project/Art/Characters/Animations/Controllers/AC_Hung.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
            foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is BlendTree) Object.DestroyImmediate(asset, true);
            controller.parameters = new AnimatorControllerParameter[0];
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Talking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Captive", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Wounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter(new AnimatorControllerParameter { name = "LimpRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
            const string generated = "Assets/_Project/Art/Characters/Animations/Generated/";
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(generated + "Idle_Tuned.anim");

            var blend = new BlendTree { name = "Hung locomotion", blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed", useAutomaticThresholds = false };
            blend.AddChild(idle, 0);
            blend.AddChild(AssetDatabase.LoadAssetAtPath<AnimationClip>(generated + "Walk_Tuned.anim"), 2.5f);
            blend.AddChild(AssetDatabase.LoadAssetAtPath<AnimationClip>(generated + "Run_Tuned.anim"), 6);
            AssetDatabase.AddObjectToAsset(blend, controller);
            var limpTree = new BlendTree { name = "Hung wounded", blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed", useAutomaticThresholds = false };
            limpTree.AddChild(idle, 0);
            limpTree.AddChild(Clip(ClipFolder + "Hung_DiKhapKhieng.fbx"), 1.8f);
            AssetDatabase.AddObjectToAsset(limpTree, controller);

            var locomotion = machine.AddState(Map01HungVisual.LocomotionState); locomotion.motion = blend;
            var talking = machine.AddState(Map01HungVisual.TalkingState); talking.motion = Clip("Assets/_Project/Art/cutsence/Talking.fbx");
            var captive = machine.AddState(Map01HungVisual.CaptiveState); captive.motion = Clip(ClipFolder + "Hung_BiTroi.fbx");
            var standUp = machine.AddState(Map01HungVisual.StandUpState); standUp.motion = Clip(ClipFolder + "Hung_DungDay.fbx");
            var wounded = machine.AddState(Map01HungVisual.WoundedState); wounded.motion = limpTree;
            wounded.speedParameter = "LimpRate"; wounded.speedParameterActive = true;
            var dying = machine.AddState(Map01HungVisual.DieState); dying.motion = Clip(ClipFolder + "Hung_TrungDan.fbx");
            // The scene opens on the rescue: he is found kneeling and tied at the jetty.
            machine.defaultState = captive;

            Link(captive, standUp, .25f, (AnimatorConditionMode.IfNot, "Captive"));
            // Up on his feet: hand over to the limp (escort) or, if the quest has already moved on, to normal walking.
            AfterClip(Link(standUp, wounded, .35f, (AnimatorConditionMode.If, "Wounded")), .85f);
            AfterClip(Link(standUp, locomotion, .35f, (AnimatorConditionMode.IfNot, "Wounded")), .85f);
            Link(wounded, locomotion, .5f, (AnimatorConditionMode.IfNot, "Wounded"));
            Link(locomotion, wounded, .3f, (AnimatorConditionMode.If, "Wounded"));
            Link(locomotion, talking, .25f, (AnimatorConditionMode.If, "Talking"), (AnimatorConditionMode.IfNot, "Dead"));
            Link(talking, locomotion, .2f, (AnimatorConditionMode.IfNot, "Talking"));
            var death = machine.AddAnyStateTransition(dying); death.hasExitTime = false; death.duration = .1f;
            death.canTransitionToSelf = false; death.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AfterClip(AnimatorStateTransition transition, float exitTime)
        { transition.hasExitTime = true; transition.exitTime = exitTime; }

        /// <summary>Transition on conditions only, no waiting for the clip to finish.</summary>
        private static AnimatorStateTransition Link(AnimatorState from, AnimatorState to, float duration,
            params (AnimatorConditionMode mode, string parameter)[] conditions)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false; transition.duration = duration;
            foreach (var (mode, parameter) in conditions) transition.AddCondition(mode, 0, parameter);
            return transition;
        }

        private static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
    }
}
