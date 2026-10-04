using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShadowVale.Editor
{
    /// <summary>
    /// Brings the clips made in Blender (D:\DO_AN_GAME\chuyen_dong) into the story.
    /// <list type="bullet">
    /// <item>Nam: his own AK carry, walk (8 ways), run, crouch, aim, fire, reloads and hit, on top of
    /// <see cref="PlayerAnimatorBuilder"/>'s controller; plus an "Actions" layer for the story moves
    /// (knife takedown, stone throw, binoculars, looting, bandaging, untying Hùng).</item>
    /// <item>Guards: <c>AC_Enemy</c> rebuilt on their own clips — post idle, patrol, look-around search,
    /// startle, aim, fire, run, hit, five deaths and the takedown victim.</item>
    /// </list>
    /// Every clip is Humanoid, read through its character's own avatar (the clip files carry no mesh),
    /// and plays exactly as authored relative to the character: no root drift leaks into the agent or
    /// the character controller.
    /// Menu <b>ShadowVale ▸ Setup Story Animations</b> (run after Build Player Animator).
    /// </summary>
    public static class StoryAnimationSetup
    {
        public const string NamFolder = "Assets/_Project/Art/Characters/Animations/Nam/";
        public const string EnemyFolder = "Assets/_Project/Art/Characters/Animations/Enemy/";
        private const string PlayerModel = "Assets/_Project/Art/Characters/Player/Player.fbx";
        private const string SoldierModel = "Assets/_Project/Art/Characters/Enemies/lính rig.fbx";
        private const string CommanderModel = "Assets/_Project/Art/Characters/Enemies/chỉ huy mỹ.fbx";

        // ---- shared names (runtime reads them through Map01's own constants) ----
        public const string ActionsLayer = "Actions", ActionsUpperLayer = "ActionsUpper";
        public const string RifleLocomotion = "Locomotion_Rifle", RifleSneak = "Sneak_Rifle";

        /// <summary>
        /// Story moves: Action = id, then DoAction. Full-body ones play while Nam's controls wait; the
        /// upper-body ones (stone, binoculars) let him keep walking. Ids match Map01NamActions.
        /// </summary>
        public static readonly (int id, string state, string clip, bool hold, float speed, bool upper)[] NamActions = {
            (1, "Act_Takedown", "Nam_Takedown", false, 1f, false),
            (2, "Act_ThrowAim", "Nam_Throw", true, 0f, true),          // wound up, held while Q is down
            (3, "Act_Throw", "Nam_Throw", false, 1.2f, true),          // the throw itself, from the wind-up
            (4, "Act_Binoculars", "Nam_Binoculars_Stand", true, 0f, true),
            (5, "Act_BinocularsCrouch", "Nam_Binoculars_Crouch", true, 0f, true),
            (6, "Act_PickUp", "Nam_PickUp", false, 2.4f, false),
            (7, "Act_OpenChest", "Nam_OpenChest", false, 2.6f, false),
            (8, "Act_Bandage", "Nam_Bandage", false, 1.4f, false),
            (9, "Act_Untie", "Nam_Untie", false, 1f, false),
        };
        /// <summary>Frame (fraction) the held moves freeze on: the stone drawn back, the glasses at the eyes.</summary>
        public const float ThrowWindUp = .42f, BinocularsUp = .45f, BinocularsCrouchUp = .45f;

        private static readonly (string file, bool loop)[] NamClips = {
            ("Nam_Rifle_Idle", true), ("Nam_Rifle_AimIdle", true), ("Nam_Rifle_Fire", false), ("Nam_Rifle_Run", true),
            ("Nam_Rifle_Walk_F", true), ("Nam_Rifle_Walk_B", true), ("Nam_Rifle_Walk_L", true), ("Nam_Rifle_Walk_R", true),
            ("Nam_Rifle_Walk_FL", true), ("Nam_Rifle_Walk_FR", true), ("Nam_Rifle_Walk_BL", true), ("Nam_Rifle_Walk_BR", true),
            ("Nam_Rifle_Reload", false), ("Nam_Rifle_ReloadTactical", false), ("Nam_Rifle_ReloadWalk", false),
            ("Nam_Crouch_Idle", true), ("Nam_Crouch_AimIdle", true), ("Nam_Crouch_Walk_F", true), ("Nam_Crouch_Reload", false),
            ("Nam_HitReaction", false), ("Nam_Throw", false), ("Nam_Binoculars_Stand", false), ("Nam_Binoculars_Crouch", false),
            ("Nam_PickUp", false), ("Nam_OpenChest", false), ("Nam_Bandage", false), ("Nam_KneelDown", false), ("Nam_Untie", false),
            ("Nam_Takedown", false),
        };
        private static readonly (string file, bool loop)[] EnemyClips = {
            ("Enemy_Guard_Idle", true), ("Enemy_Patrol_Walk", true), ("Enemy_Search_LookAround", true), ("Enemy_Startled", false),
            ("Enemy_Turn180", false), ("Enemy_Rifle_AimIdle", true), ("Enemy_Rifle_Fire", false), ("Enemy_Rifle_Run", true),
            ("Enemy_HitReaction", false), ("Enemy_Death_Backward", false), ("Enemy_Death_Forward", false),
            ("Enemy_Death_Shot", false), ("Enemy_Death_Knees", false), ("Enemy_Death_Stomach", false),
            ("Enemy_Takedown_Victim", false),
        };
        private static readonly (string file, bool loop)[] BossClips = {
            ("Boss_Idle_Survey", true), ("Boss_Point", false), ("Boss_Charge", false), ("Boss_Orders", true),
        };
        /// <summary>Nam's / the guards' Blender clips are in the project (exported from chuyen_dong).</summary>
        public static bool HasNamClips => File.Exists(NamFolder + "Nam_Rifle_Idle.fbx");
        public static bool HasEnemyClips => File.Exists(EnemyFolder + "Enemy_Guard_Idle.fbx");

        public static readonly string[] EnemyDeaths = {
            "Enemy_Death_Backward", "Enemy_Death_Forward", "Enemy_Death_Shot", "Enemy_Death_Knees", "Enemy_Death_Stomach" };

        [MenuItem("ShadowVale/Setup Story Animations")]
        public static void Run()
        {
            if (NamModelSetup.HasFingers) NamModelSetup.Run();   // the clips read the avatar it fills in
            ImportAll();
            PlayerAnimatorBuilder.Build();   // rebuilds the base, then adds Nam's own clips (AugmentPlayer)
            AlignNamRifleSeat();
            BuildEnemy();
            AssetDatabase.SaveAssets();
            Debug.Log("[StoryAnimation] Nam, guards and the knife takedown wired to Blender's clips.");
        }

        /// <summary>
        /// Markers on the Blender AK exactly as Nam's approved clips hold it, exported under his right hand at rest
        /// (blender_scripts: grip_probe.py): the game rifle's grip point (W_AK47's origin: 32% of the length from
        /// the stock, half the height incl. the magazine, 27% of the width from its left side), a point down the
        /// barrel, one straight up, and the two ends of the gun.
        /// </summary>
        public const string GripProbe = NamFolder + "Probe/NamRifleGripProbe.fbx";

        /// <summary>
        /// W_AK47's own axes: +Z down the barrel but -X up (its magazine hangs toward +X). A frame built as
        /// "+Z muzzle, +Y up" is turned into the model's by this.
        /// </summary>
        private static readonly Quaternion RifleModelFrame = Quaternion.Euler(0, 0, -90);

        /// <summary>
        /// Nam's AK sits in his fist exactly where the Blender clips have it: the probe and the player model share
        /// the same skeleton at rest, so the markers' place relative to the right hand is the rifle's seat. Carried
        /// through PlayerCombat's WeaponAnchor (a child of the right hand) into WeaponGripConfig, with the rifle
        /// scaled to the Blender gun's length (W_AK47 is 19% smaller). The gun is fixed to the hand in every
        /// Blender clip, aiming included, so the aim seat is the same one: the clips do the levelling.
        /// </summary>
        public static void AlignNamRifleSeat()
        {
            const string PlayerPrefab = "Assets/_Project/Prefabs/Player/Player.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            var combat = prefab != null ? prefab.GetComponent<PlayerCombat>() : null;
            var so = combat != null ? new SerializedObject(combat) : null;
            var grip = so?.FindProperty("gripConfig").objectReferenceValue as WeaponGripConfig;
            var probeAsset = AssetDatabase.LoadAssetAtPath<GameObject>(GripProbe);
            if (grip == null || probeAsset == null) { Debug.LogWarning("[StoryAnimation] Rifle seat not aligned: grip config or grip probe missing."); return; }
            GameObject probe = null, nam = null;
            try {
                probe = (GameObject)PrefabUtility.InstantiatePrefab(probeAsset);
                probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
                Transform probeHand = Find(probe.transform, "RightHand"), g = Find(probe.transform, "GripPoint"),
                          m = Find(probe.transform, "MuzzlePoint"), u = Find(probe.transform, "UpPoint"),
                          stockEnd = Find(probe.transform, "StockEnd"), muzzleEnd = Find(probe.transform, "MuzzleEnd");
                nam = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                nam.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var actor = nam.GetComponentInChildren<Animator>();
                Transform hand = actor != null ? actor.GetBoneTransform(HumanBodyBones.RightHand) : null;
                if (probeHand == null || g == null || m == null || u == null || hand == null) {
                    Debug.LogWarning("[StoryAnimation] Rifle seat not aligned: probe markers or Nam's hand missing."); return;
                }
                // The probe and Nam's model come out of the same Blender rig: the markers, read in the probe's own
                // right-hand frame, sit the same way in Nam's. (Their roots need not line up — the two imports are
                // placed differently — so nothing is compared in world space.)
                Quaternion world = Quaternion.LookRotation(m.position - g.position, u.position - g.position) * RifleModelFrame;
                Quaternion local = Quaternion.Inverse(probeHand.rotation) * world;
                Vector3 position = probeHand.InverseTransformPoint(g.position);
                position = Vector3.Scale(position, Divide(probeHand.lossyScale, hand.lossyScale));
                var anchor = so.FindProperty("handAnchor").objectReferenceValue as Transform;
                Quaternion toAnchor = anchor != null ? Quaternion.Inverse(anchor.localRotation) : Quaternion.identity;
                Vector3 anchorPos = anchor != null ? anchor.localPosition : Vector3.zero;
                Vector3 seat = toAnchor * (position - anchorPos), seatEuler = (toAnchor * local).eulerAngles;
                grip.SetOffset(WeaponKind.Rifle, seat, seatEuler);
                grip.SetAimOffset(WeaponKind.Rifle, seat, seatEuler);
                float scale = 1f, rifleLength = RifleLength(so);
                if (stockEnd != null && muzzleEnd != null && rifleLength > 0)
                    scale = Vector3.Distance(stockEnd.position, muzzleEnd.position) / probeHand.lossyScale.x * hand.lossyScale.x / rifleLength;
                grip.SetScale(WeaponKind.Rifle, scale);
                EditorUtility.SetDirty(grip);
                Debug.Log($"[StoryAnimation] Nam's rifle seat from the Blender grip: {seat:F3} / {seatEuler:F1}, scale {scale:F3}" +
                          $" (probe hand scale {probeHand.lossyScale.x:F3}, Nam's {hand.lossyScale.x:F3})");
            } finally {
                if (probe != null) Object.DestroyImmediate(probe);
                if (nam != null) Object.DestroyImmediate(nam);
            }
        }

        private static Vector3 Divide(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);

        /// <summary>Length of Nam's rifle prefab along its barrel (+Z), from its meshes; 0 if it is not there.</summary>
        private static float RifleLength(SerializedObject combat)
        {
            var list = combat.FindProperty("weaponPrefabs");
            for (int i = 0; list != null && i < list.arraySize; i++)
            {
                if (!(list.GetArrayElementAtIndex(i).objectReferenceValue is Weapon weapon) || weapon.Kind != WeaponKind.Rifle) continue;
                float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
                foreach (var filter in weapon.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) continue;
                    foreach (Vector3 v in filter.sharedMesh.vertices)
                    {
                        float z = weapon.transform.InverseTransformPoint(filter.transform.TransformPoint(v)).z;
                        lo = Mathf.Min(lo, z); hi = Mathf.Max(hi, z);
                    }
                }
                return hi > lo ? hi - lo : 0f;
            }
            return 0f;
        }

        private static Bounds TransformBounds(Transform root, Bounds world)
        {
            var local = new Bounds(root.InverseTransformPoint(world.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                local.Encapsulate(root.InverseTransformPoint(world.center + Vector3.Scale(world.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return local;
        }

        // ---------------------------------------------------------------- import
        public static void ImportAll()
        {
            var nam = AvatarOf(PlayerModel); var soldier = AvatarOf(SoldierModel); var commander = AvatarOf(CommanderModel);
            foreach (var (file, loop) in NamClips) ImportClip(NamFolder + file + ".fbx", loop, nam, RootMotionClips.Contains(file));
            foreach (var (file, loop) in EnemyClips) ImportClip(EnemyFolder + file + ".fbx", loop, soldier);
            foreach (var (file, loop) in BossClips) ImportClip(EnemyFolder + file + ".fbx", loop, commander);
        }

        private static Avatar AvatarOf(string model) =>
            AssetDatabase.LoadAllAssetsAtPath(model).OfType<Avatar>().FirstOrDefault(a => a.isValid && a.isHuman)
            ?? throw new System.InvalidOperationException(model + " has no valid Humanoid avatar.");

        /// <summary>Humanoid clip, pose kept exactly as authored: no root drift, height or turn leaks out.</summary>
        /// <summary>
        /// Nam's full-body story moves travel (the takedown lunges ~0.7 m): their ground travel stays root
        /// motion, which Map01NamActions applies to his model while the move plays, then hands to his controller.
        /// </summary>
        private static readonly HashSet<string> RootMotionClips = new HashSet<string> {
            "Nam_Takedown", "Nam_PickUp", "Nam_OpenChest", "Nam_Bandage", "Nam_Untie" };

        private static void ImportClip(string path, bool loop, Avatar avatar, bool rootMotion = false)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path)
                ?? throw new FileNotFoundException("Missing clip — export it from chuyen_dong first.", path);
            importer.animationType = ModelImporterAnimationType.Human;
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
            source.lockRootHeightY = true; source.keepOriginalPositionY = false; source.heightFromFeet = true;
            source.lockRootPositionXZ = !rootMotion; source.keepOriginalPositionXZ = true;
            importer.clipAnimations = new[] { source };
            importer.SaveAndReimport();
            // the take is converted to muscles through the avatar at import: re-convert even when nothing in
            // this file changed (the avatar's limits may have)
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        public static AnimationClip Clip(string folder, string name) =>
            AssetDatabase.LoadAllAssetsAtPath(folder + name + ".fbx").OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"))
            ?? throw new FileNotFoundException("Clip not imported: " + name, folder + name + ".fbx");

        private static void Param(AnimatorController c, string name, AnimatorControllerParameterType type)
        {
            if (c.parameters.All(p => p.name != name)) c.AddParameter(name, type);
        }

        private static AnimatorStateTransition Link(AnimatorState from, AnimatorState to, float blend,
            params (AnimatorConditionMode mode, float value, string param)[] conditions)
        {
            var t = from.AddTransition(to); t.hasExitTime = false; t.duration = blend;
            foreach (var (mode, value, param) in conditions) t.AddCondition(mode, value, param);
            return t;
        }

        // ---------------------------------------------------------------- Nam
        public static void AugmentPlayer()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerAnimatorBuilder.ControllerPath)
                ?? throw new FileNotFoundException("Build the player animator first.", PlayerAnimatorBuilder.ControllerPath);
            string W = PlayerAnimatorBuilder.Params.Weapon, S = PlayerAnimatorBuilder.Params.Sneaking,
                   Spd = PlayerAnimatorBuilder.Params.Speed, Aim = PlayerAnimatorBuilder.Params.Aiming;
            Param(controller, "MoveX", AnimatorControllerParameterType.Float);
            Param(controller, "MoveZ", AnimatorControllerParameterType.Float);
            Param(controller, "Reload", AnimatorControllerParameterType.Trigger);
            Param(controller, "ReloadEmpty", AnimatorControllerParameterType.Bool);
            if (controller.parameters.All(p => p.name != "ReloadSpeed"))
                controller.AddParameter(new AnimatorControllerParameter { name = "ReloadSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            Param(controller, "Hit", AnimatorControllerParameterType.Trigger);
            Param(controller, "Action", AnimatorControllerParameterType.Int);
            Param(controller, "DoAction", AnimatorControllerParameterType.Trigger);
            Param(controller, "ActionHold", AnimatorControllerParameterType.Bool);
            const int rifle = (int)WeaponKind.Rifle;

            // IK pass on the base layer: Map01TakedownIK keeps his hands on the guard during the takedown
            var withIk = controller.layers; withIk[0].iKPass = true; controller.layers = withIk;

            // ---- base layer: Nam's own carry when the AK is in his hands
            var root = controller.layers[0].stateMachine;
            var locomotion = root.states.First(s => s.state.name == PlayerCombat.LocomotionState).state;
            var sneak = root.states.Select(s => s.state).FirstOrDefault(s => s.name == "Sneak");
            var jump = root.states.Select(s => s.state).FirstOrDefault(s => s.name == "Jump");

            var walk = new BlendTree { name = RifleLocomotion, blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "MoveX", blendParameterY = "MoveZ", useAutomaticThresholds = false };
            // Ground speeds the Mixamo takes were authored at (measured in Blender): rifle walk 1.94 m/s,
            // rifle run 4.34 m/s, crouched walk 2.05 m/s. Placed at PlayerController's own speeds and played
            // faster/slower to match, so the feet do not slide.
            const float WalkClip = 1.94f, RunClip = 4.34f, CrouchClip = 2.05f;
            float w = PlayerAnimatorBuilder.WalkThreshold, r = PlayerAnimatorBuilder.RunThreshold;
            walk.AddChild(Clip(NamFolder, "Nam_Rifle_Idle"), Vector2.zero);
            walk.AddChild(Clip(NamFolder, "Nam_Rifle_Walk_F"), new Vector2(0, w));
            walk.AddChild(Clip(NamFolder, "Nam_Rifle_Walk_B"), new Vector2(0, -w));
            walk.AddChild(Clip(NamFolder, "Nam_Rifle_Walk_L"), new Vector2(-w, 0));
            walk.AddChild(Clip(NamFolder, "Nam_Rifle_Walk_R"), new Vector2(w, 0));
            walk.AddChild(Clip(NamFolder, "Nam_Rifle_Walk_FL"), new Vector2(-w, w) * .7071f);
            walk.AddChild(Clip(NamFolder, "Nam_Rifle_Walk_FR"), new Vector2(w, w) * .7071f);
            walk.AddChild(Clip(NamFolder, "Nam_Rifle_Walk_BL"), new Vector2(-w, -w) * .7071f);
            walk.AddChild(Clip(NamFolder, "Nam_Rifle_Walk_BR"), new Vector2(w, -w) * .7071f);
            walk.AddChild(Clip(NamFolder, "Nam_Rifle_Run"), new Vector2(0, r));
            var children = walk.children;
            for (int i = 1; i < children.Length; i++) children[i].timeScale = i == children.Length - 1 ? r / RunClip : w / WalkClip;
            walk.children = children;
            AssetDatabase.AddObjectToAsset(walk, controller);
            var rifleLoco = root.AddState(RifleLocomotion); rifleLoco.motion = walk; rifleLoco.writeDefaultValues = false;

            var crouch = new BlendTree { name = RifleSneak, blendType = BlendTreeType.Simple1D, blendParameter = Spd,
                useAutomaticThresholds = false };
            crouch.AddChild(Clip(NamFolder, "Nam_Crouch_Idle"), 0f);
            const float sneakSpeed = 1.2f;   // PlayerController.sneakSpeed
            crouch.AddChild(Clip(NamFolder, "Nam_Crouch_Walk_F"), sneakSpeed);
            var crouchChildren = crouch.children; crouchChildren[1].timeScale = sneakSpeed / CrouchClip; crouch.children = crouchChildren;
            AssetDatabase.AddObjectToAsset(crouch, controller);
            var rifleSneak = root.AddState(RifleSneak); rifleSneak.motion = crouch; rifleSneak.writeDefaultValues = false;

            float b = PlayerAnimatorBuilder.StanceBlend;
            Link(locomotion, rifleLoco, b, (AnimatorConditionMode.Equals, rifle, W), (AnimatorConditionMode.IfNot, 0, S));
            Link(rifleLoco, locomotion, b, (AnimatorConditionMode.NotEqual, rifle, W));
            Link(rifleLoco, rifleSneak, b, (AnimatorConditionMode.If, 0, S));
            Link(rifleSneak, rifleLoco, b, (AnimatorConditionMode.IfNot, 0, S), (AnimatorConditionMode.Equals, rifle, W));
            Link(rifleSneak, sneak ?? locomotion, b, (AnimatorConditionMode.NotEqual, rifle, W));
            Link(locomotion, rifleSneak, b, (AnimatorConditionMode.Equals, rifle, W), (AnimatorConditionMode.If, 0, S));
            if (sneak != null) Link(sneak, rifleSneak, b, (AnimatorConditionMode.Equals, rifle, W));
            if (jump != null) {
                var t = Link(rifleLoco, jump, .08f, (AnimatorConditionMode.If, 0, PlayerAnimatorBuilder.Params.Jump));
            }

            // ---- upper body: aim, fire, reload, hit with Nam's clips (the layer is only raised for these)
            int upperIndex = System.Array.FindIndex(controller.layers, l => l.name == PlayerAnimatorBuilder.UpperBodyLayer);
            if (upperIndex > 0) {
                var upper = controller.layers[upperIndex].stateMachine;
                var hold = upper.states.Select(s => s.state).First(s => s.name == "Hold_Rifle");
                hold.motion = Clip(NamFolder, "Nam_Rifle_Idle"); hold.speed = 1f; hold.cycleOffset = 0f;
                var fire = upper.states.Select(s => s.state).FirstOrDefault(s => s.name == "Attack_Rifle");
                if (fire != null) fire.motion = Clip(NamFolder, "Nam_Rifle_Fire");
                var aim = upper.AddState("Aim_Rifle"); aim.writeDefaultValues = false;
                var aimTree = new BlendTree { name = "Aim_Rifle", blendType = BlendTreeType.Simple1D, blendParameter = Spd,
                    useAutomaticThresholds = false };
                aimTree.AddChild(Clip(NamFolder, "Nam_Rifle_AimIdle"), 0f);
                aimTree.AddChild(Clip(NamFolder, "Nam_Rifle_AimIdle"), 1f);
                AssetDatabase.AddObjectToAsset(aimTree, controller); aim.motion = aimTree;
                Link(hold, aim, .15f, (AnimatorConditionMode.If, 0, Aim), (AnimatorConditionMode.Equals, rifle, W));
                Link(aim, hold, .2f, (AnimatorConditionMode.IfNot, 0, Aim));
                Link(aim, hold, .2f, (AnimatorConditionMode.NotEqual, rifle, W));
                var crouchAim = upper.AddState("AimCrouch_Rifle"); crouchAim.writeDefaultValues = false;
                crouchAim.motion = Clip(NamFolder, "Nam_Crouch_AimIdle");
                Link(aim, crouchAim, .15f, (AnimatorConditionMode.If, 0, S));
                Link(crouchAim, aim, .15f, (AnimatorConditionMode.IfNot, 0, S));
                Link(crouchAim, hold, .2f, (AnimatorConditionMode.IfNot, 0, Aim));

                // Reloads: a part-used magazine, an empty one, or crouched. PlayerCombat sets ReloadSpeed so
                // the take ends with the magazine timer.
                foreach (var (state, clip, crouched, empty) in new[] {
                    ("Reload_Rifle", "Nam_Rifle_ReloadTactical", false, false), ("ReloadEmpty_Rifle", "Nam_Rifle_Reload", false, true),
                    ("ReloadCrouch_Rifle", "Nam_Crouch_Reload", true, false) }) {
                    var reload = upper.AddState(state); reload.motion = Clip(NamFolder, clip); reload.writeDefaultValues = false;
                    reload.speedParameterActive = true; reload.speedParameter = "ReloadSpeed";
                    var enter = upper.AddAnyStateTransition(reload); enter.hasExitTime = false; enter.duration = .12f;
                    enter.canTransitionToSelf = false;
                    enter.AddCondition(AnimatorConditionMode.If, 0, "Reload");
                    enter.AddCondition(AnimatorConditionMode.Equals, rifle, W);
                    enter.AddCondition(crouched ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, S);
                    if (!crouched) enter.AddCondition(empty ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "ReloadEmpty");
                    var back = reload.AddTransition(hold); back.hasExitTime = true; back.exitTime = .95f; back.duration = .2f;
                    Link(reload, hold, .15f, (AnimatorConditionMode.NotEqual, rifle, W));
                }
                var hit = upper.AddState("Hit"); hit.motion = Clip(NamFolder, "Nam_HitReaction"); hit.writeDefaultValues = false;
                hit.speed = 1.6f;
                var toHit = upper.AddAnyStateTransition(hit); toHit.hasExitTime = false; toHit.duration = .06f;
                toHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");
                toHit.AddCondition(AnimatorConditionMode.Equals, rifle, W);
                var fromHit = hit.AddTransition(hold); fromHit.hasExitTime = true; fromHit.exitTime = .55f; fromHit.duration = .2f;
            }

            // ---- Story moves. Full-body ones live in the base layer: only the base layer places a humanoid's
            // body, so a takedown lunge or a kneel played on an upper layer would stay rooted at standing
            // height. Upper-body ones (stone, binoculars) get a masked layer, raised by Map01NamActions only
            // while one plays — a muted layer freezes nothing, an empty state at full weight would.
            for (int i = controller.layers.Length - 1; i > 0; i--)
                if (controller.layers[i].name == ActionsLayer || controller.layers[i].name == ActionsUpperLayer)
                    controller.RemoveLayer(i);
            var upperBody = new AnimatorStateMachine { name = ActionsUpperLayer, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(upperBody, controller);
            controller.AddLayer(new AnimatorControllerLayer { name = ActionsUpperLayer, stateMachine = upperBody, defaultWeight = 0f,
                blendingMode = AnimatorLayerBlendingMode.Override, avatarMask = PlayerAnimatorBuilder.EnsureUpperBodyMask() });
            var upperRest = upperBody.AddState("None"); upperRest.writeDefaultValues = false; upperBody.defaultState = upperRest;
            var states = new Dictionary<int, AnimatorState>();
            foreach (var (id, name, clip, hold, speed, onUpper) in NamActions) {
                var actions = onUpper ? upperBody : root; var none = onUpper ? upperRest : locomotion;
                var s = actions.AddState(name); s.motion = Clip(NamFolder, clip); s.writeDefaultValues = false;
                s.speed = hold ? 0f : speed;
                if (hold) s.cycleOffset = id == 2 ? ThrowWindUp : id == 4 ? BinocularsUp : BinocularsCrouchUp;
                states[id] = s;
                var enter = actions.AddAnyStateTransition(s); enter.hasExitTime = false; enter.duration = hold ? .3f : .15f;
                enter.canTransitionToSelf = false;
                enter.AddCondition(AnimatorConditionMode.If, 0, "DoAction");
                enter.AddCondition(AnimatorConditionMode.Equals, id, "Action");
                if (hold) Link(s, none, .3f, (AnimatorConditionMode.IfNot, 0, "ActionHold"));
                else { var back = s.AddTransition(none); back.hasExitTime = true; back.exitTime = onUpper ? 1f : .97f; back.duration = onUpper ? .01f : .15f; }
                if (!onUpper) {   // cut short (walking off, going for the gun): Map01NamActions sends Action 0
                    var cut = s.AddTransition(none); cut.hasExitTime = false; cut.duration = .2f;
                    cut.AddCondition(AnimatorConditionMode.Equals, 0, "Action");
                }
            }
            // the throw picks up from the wind-up it was held at
            var aimToThrow = states[2].AddTransition(states[3]); aimToThrow.hasExitTime = false; aimToThrow.duration = .05f;
            aimToThrow.offset = ThrowWindUp;
            aimToThrow.AddCondition(AnimatorConditionMode.If, 0, "DoAction");
            aimToThrow.AddCondition(AnimatorConditionMode.Equals, 3, "Action");
            EditorUtility.SetDirty(controller);
        }

        // ---------------------------------------------------------------- guards
        public static void BuildEnemy()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyAnimatorBuilder.ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(EnemyAnimatorBuilder.ControllerPath);
            PlayerAnimatorBuilder.ClearController(controller);
            string Spd = PlayerAnimatorBuilder.Params.Speed;
            controller.AddParameter(Spd, AnimatorControllerParameterType.Float);
            controller.AddParameter(PlayerAnimatorBuilder.Params.Attack, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(PlayerAnimatorBuilder.Params.Die, AnimatorControllerParameterType.Trigger);
            foreach (var p in new[] { "Engaged", "Searching", "Silent" }) controller.AddParameter(p, AnimatorControllerParameterType.Bool);
            foreach (var p in new[] { "Startled", "Hit" }) controller.AddParameter(p, AnimatorControllerParameterType.Trigger);
            controller.AddParameter("DeathIndex", AnimatorControllerParameterType.Int);

            var root = controller.layers[0].stateMachine;
            var tree = new BlendTree { name = PlayerCombat.LocomotionState, blendType = BlendTreeType.Simple1D,
                blendParameter = Spd, useAutomaticThresholds = false };
            tree.AddChild(Clip(EnemyFolder, "Enemy_Guard_Idle"), 0f);
            // at the takes' own ground speeds (rifle walk 1.94 m/s, run 4.34 m/s): a 3.5 m/s patrol is a brisk
            // walk-into-jog, a 5.25 m/s investigation a run
            tree.AddChild(Clip(EnemyFolder, "Enemy_Patrol_Walk"), 1.94f);
            tree.AddChild(Clip(EnemyFolder, "Enemy_Rifle_Run"), 4.34f);
            AssetDatabase.AddObjectToAsset(tree, controller);
            var locomotion = root.AddState(PlayerCombat.LocomotionState); locomotion.motion = tree;
            locomotion.writeDefaultValues = false; root.defaultState = locomotion;

            AnimatorState State(string name, string clip, float speed = 1f)
            {
                var s = root.AddState(name); s.motion = Clip(EnemyFolder, clip); s.writeDefaultValues = false; s.speed = speed; return s;
            }
            var aim = State("Aim", "Enemy_Rifle_AimIdle");
            var fire = State("Fire", "Enemy_Rifle_Fire", PlayerAnimatorBuilder.RifleAttackSpeed);
            var search = State("Search", "Enemy_Search_LookAround");
            var startled = State("Startled", "Enemy_Startled", 1.3f);
            var hit = State("Hit", "Enemy_HitReaction", 1.6f);

            Link(locomotion, aim, .2f, (AnimatorConditionMode.If, 0, "Engaged"), (AnimatorConditionMode.Less, .3f, Spd));
            Link(aim, locomotion, .2f, (AnimatorConditionMode.Greater, .3f, Spd));
            Link(aim, locomotion, .3f, (AnimatorConditionMode.IfNot, 0, "Engaged"));
            Link(locomotion, search, .35f, (AnimatorConditionMode.If, 0, "Searching"), (AnimatorConditionMode.Less, .3f, Spd));
            Link(search, locomotion, .3f, (AnimatorConditionMode.IfNot, 0, "Searching"));
            Link(search, locomotion, .2f, (AnimatorConditionMode.Greater, .3f, Spd));
            Link(search, aim, .2f, (AnimatorConditionMode.If, 0, "Engaged"));
            foreach (var from in new[] { locomotion, aim, search }) {
                var t = from.AddTransition(fire); t.hasExitTime = false; t.duration = .05f;
                t.AddCondition(AnimatorConditionMode.If, 0, PlayerAnimatorBuilder.Params.Attack);
            }
            var refire = fire.AddTransition(fire); refire.hasExitTime = false; refire.duration = .03f;
            refire.AddCondition(AnimatorConditionMode.If, 0, PlayerAnimatorBuilder.Params.Attack);
            var afterFire = fire.AddTransition(aim); afterFire.hasExitTime = true; afterFire.exitTime = .9f; afterFire.duration = .1f;
            foreach (var from in new[] { locomotion, search }) {
                var t = from.AddTransition(startled); t.hasExitTime = false; t.duration = .1f;
                t.AddCondition(AnimatorConditionMode.If, 0, "Startled");
            }
            var afterStartle = startled.AddTransition(aim); afterStartle.hasExitTime = true; afterStartle.exitTime = .8f; afterStartle.duration = .25f;
            foreach (var from in new[] { locomotion, aim, search, fire }) {
                var t = from.AddTransition(hit); t.hasExitTime = false; t.duration = .06f;
                t.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            }
            var afterHit = hit.AddTransition(aim); afterHit.hasExitTime = true; afterHit.exitTime = .55f; afterHit.duration = .2f;

            // deaths: one of five, picked per guard; the silent takedown has its own
            for (int i = 0; i < EnemyDeaths.Length; i++) {
                var die = State("Die_" + i, EnemyDeaths[i]);
                var t = root.AddAnyStateTransition(die); t.hasExitTime = false; t.duration = .1f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0, PlayerAnimatorBuilder.Params.Die);
                t.AddCondition(AnimatorConditionMode.IfNot, 0, "Silent");
                t.AddCondition(AnimatorConditionMode.Equals, i, "DeathIndex");
            }
            var victim = State("TakedownVictim", "Enemy_Takedown_Victim");
            var tv = root.AddAnyStateTransition(victim); tv.hasExitTime = false; tv.duration = .05f; tv.canTransitionToSelf = false;
            tv.AddCondition(AnimatorConditionMode.If, 0, PlayerAnimatorBuilder.Params.Die);
            tv.AddCondition(AnimatorConditionMode.If, 0, "Silent");
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(EnemyAnimatorBuilder.ControllerPath, ImportAssetOptions.ForceUpdate);
        }
    }
}
