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
        [MenuItem("ShadowVale/Characters/Prepare Hung")]
        public static void Prepare()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importCameras = false; importer.importLights = false;
            importer.SaveAndReimport();
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
                // Reuse the dedicated textured material supplied on Khoa.
                var material = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Art/Characters/NPCs/Materials/hung - Material.001.mat");
                if (material == null || material.GetTexture("_BaseMap") == null)
                    throw new System.InvalidOperationException("Hung's dedicated material and base texture are required.");
                foreach (var renderer in renderers)
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                var visual = root.AddComponent<Map01HungVisual>(); visual.actor = actor;
                string folder = "Assets/_Project/Map01/Resources/Characters";
                Directory.CreateDirectory(folder); AssetDatabase.Refresh();
                PrefabUtility.SaveAsPrefabAsset(root, folder + "/HungVisual.prefab");
                AssetDatabase.SaveAssets();
                Debug.Log("[HungVisual] Valid Humanoid, existing controller, height 1.78m. Dedicated textured Hung material applied.");
            } finally { Object.DestroyImmediate(root); }
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
            var blend = new BlendTree { name = "Hung locomotion", blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed", useAutomaticThresholds = false };
            const string generated = "Assets/_Project/Art/Characters/Animations/Generated/";
            blend.AddChild(AssetDatabase.LoadAssetAtPath<AnimationClip>(generated + "Idle_Tuned.anim"), 0);
            blend.AddChild(AssetDatabase.LoadAssetAtPath<AnimationClip>(generated + "Walk_Tuned.anim"), 2.5f);
            blend.AddChild(AssetDatabase.LoadAssetAtPath<AnimationClip>(generated + "Run_Tuned.anim"), 6);
            AssetDatabase.AddObjectToAsset(blend, controller);
            var locomotion = machine.AddState("Locomotion"); locomotion.motion = blend;
            var talking = machine.AddState("Talking"); talking.motion = Clip("Assets/_Project/Art/cutsence/Talking.fbx");
            var dying = machine.AddState("Die"); dying.motion = Clip("Assets/_Project/Art/Characters/Animations/Clips/Dying.fbx");
            machine.defaultState = locomotion;
            var enter = locomotion.AddTransition(talking); enter.hasExitTime = false; enter.duration = .25f;
            enter.AddCondition(AnimatorConditionMode.If, 0, "Talking");
            enter.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            var leave = talking.AddTransition(locomotion); leave.hasExitTime = false; leave.duration = .2f;
            leave.AddCondition(AnimatorConditionMode.IfNot, 0, "Talking");
            var death = machine.AddAnyStateTransition(dying); death.hasExitTime = false; death.duration = .1f;
            death.canTransitionToSelf = false; death.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            EditorUtility.SetDirty(controller);
            return controller;
        }
        private static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
    }
}
