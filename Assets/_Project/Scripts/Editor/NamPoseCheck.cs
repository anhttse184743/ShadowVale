using System.IO;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEngine;

namespace ShadowVale.Editor
{
    /// <summary>
    /// Side-by-side check against Blender: puts Player.prefab at one frame of one of Nam's clips, seats the AK in
    /// his hand exactly as PlayerCombat does, measures the barrel and photographs him from the front, the left
    /// and behind into Logs/NamPose. Menu <b>ShadowVale ▸ Characters ▸ Check Nam Poses</b>;
    /// batch: <c>-executeMethod ShadowVale.Editor.NamPoseCheck.Run</c>.
    /// </summary>
    public static class NamPoseCheck
    {
        private const string Player = "Assets/_Project/Prefabs/Player/Player.prefab";
        private const string Rifle = "Assets/_Project/Prefabs/Items/W_AK47.prefab";
        private const string Grip = "Assets/_Project/ScriptableObjects/GameSettings/WeaponGripConfig.asset";

        /// <summary>(clip file, normalised time) — the same frames are rendered from the Blender master.</summary>
        private static readonly (string clip, float t)[] Shots = {
            ("Nam_Rifle_Idle", .5f), ("Nam_Rifle_AimIdle", .5f), ("Nam_Rifle_Walk_F", .5f), ("Nam_Rifle_Run", .5f),
            ("Nam_Crouch_AimIdle", .5f), ("Nam_Rifle_Reload", .5f),
        };

        private static readonly (HumanBodyBones, HumanBodyBones)[] Segments = {
            (HumanBodyBones.Hips, HumanBodyBones.Spine), (HumanBodyBones.Spine, HumanBodyBones.Chest), (HumanBodyBones.Chest, HumanBodyBones.UpperChest),
            (HumanBodyBones.UpperChest, HumanBodyBones.Neck), (HumanBodyBones.Neck, HumanBodyBones.Head),
            (HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm), (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm),
            (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand), (HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal),
            (HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm), (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm),
            (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand), (HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal),
            (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg), (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg),
        };

        [MenuItem("ShadowVale/Characters/Check Nam Poses")]
        public static void Run()
        {
            Directory.CreateDirectory("Logs/NamPose");
            var grip = AssetDatabase.LoadAssetAtPath<WeaponGripConfig>(Grip);
            var report = new System.Text.StringBuilder();
            GameObject nam = null, rifle = null, rig = null;
            try
            {
                nam = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Player));
                nam.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var behaviour in nam.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                // several poses are photographed within one editor frame: skin every render, not once per frame
                foreach (var skin in nam.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.forceMatrixRecalculationPerRender = true;
                var actor = nam.GetComponentInChildren<Animator>();
                Transform anchor = actor.GetComponentsInChildren<Transform>(true).First(t => t.name == "WeaponAnchor");
                rifle = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Rifle), anchor);
                grip.Apply(rifle.transform, WeaponKind.Rifle);
                // the rifle's mesh in its own frame: where its pivot (the grip point the seat is for) really is
                Vector3 lo = Vector3.positiveInfinity, hi = Vector3.negativeInfinity;
                foreach (var filter in rifle.GetComponentsInChildren<MeshFilter>(true))
                    foreach (Vector3 v in filter.sharedMesh.vertices)
                    {
                        Vector3 p = rifle.transform.InverseTransformPoint(filter.transform.TransformPoint(v));
                        lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p);
                    }
                Vector3 span = hi - lo;
                foreach (string part in new[] { "magazine", "stock", "belt", "barrel", "dustcover" })
                {
                    var pieces = rifle.GetComponentsInChildren<Renderer>(true).Where(r => r.name.ToLower().Contains(part)).ToArray();
                    if (pieces.Length == 0) continue;
                    Vector3 plo = Vector3.positiveInfinity, phi = Vector3.negativeInfinity;
                    foreach (var r in pieces)
                        if (r.TryGetComponent(out MeshFilter mf))
                            foreach (Vector3 v in mf.sharedMesh.vertices)
                            {
                                Vector3 p = rifle.transform.InverseTransformPoint(mf.transform.TransformPoint(v));
                                plo = Vector3.Min(plo, p); phi = Vector3.Max(phi, p);
                            }
                    report.AppendLine($"rifle {part} pieces ({pieces.Length}) span {plo:F3} .. {phi:F3} in its frame");
                }
                report.AppendLine($"rifle mesh in its frame: min {lo:F3} max {hi:F3}; pivot at {-lo.z / span.z:P0} of the length from the stock, " +
                                  $"{-lo.y / span.y:P0} of the height from the bottom, {-lo.x / span.x:P0} across");

                rig = new GameObject("NamPoseRig");
                var light = rig.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.3f;
                rig.transform.rotation = Quaternion.Euler(40, 150, 0);
                var fill = new GameObject("Fill").AddComponent<Light>(); fill.transform.SetParent(rig.transform, false);
                fill.type = LightType.Directional; fill.intensity = .6f; fill.transform.rotation = Quaternion.Euler(20, -30, 0);
                var camera = new GameObject("Camera").AddComponent<Camera>(); camera.transform.SetParent(rig.transform, false);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.62f, .64f, .66f);
                camera.orthographic = true; camera.orthographicSize = .62f; camera.nearClipPlane = .05f;

                AnimationMode.StartAnimationMode();
                foreach (var (clipName, t) in Shots)
                {
                    var clip = AssetDatabase.LoadAllAssetsAtPath(StoryAnimationSetup.NamFolder + clipName + ".fbx")
                        .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
                    if (clip == null) { report.AppendLine($"{clipName}: missing"); continue; }
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(actor.gameObject, clip, t * clip.length);
                    AnimationMode.EndSampling();
                    // the sleeves as the game shows them: there the twist bones follow the hand after the animation
                    var twist = actor.GetComponent<ForearmTwist>();
                    if (twist != null) twist.Apply();
                    Transform hips = actor.GetBoneTransform(HumanBodyBones.Hips);
                    Vector3 barrel = rifle.transform.TransformDirection(Vector3.forward);
                    Vector3 facing = Vector3.ProjectOnPlane(actor.transform.forward, Vector3.up).normalized;
                    Transform leftHand = actor.GetBoneTransform(HumanBodyBones.LeftHand);
                    float leftToRifle = Vector3.Distance(leftHand.position, ClosestOnLine(rifle.transform.position, barrel, leftHand.position));
                    report.AppendLine($"{clipName} @ {t:F2}: barrel {barrel:F2} ({Vector3.Angle(barrel, facing):F0} deg from his facing, {Mathf.Asin(Mathf.Clamp(barrel.y, -1, 1)) * Mathf.Rad2Deg:F0} deg up), " +
                                      $"left hand {leftToRifle * 100:F0} cm off the barrel line");
                    // bone segments in his own frame (right, up, forward), to compare with Blender's
                    foreach (var (from, to) in Segments)
                    {
                        Transform a = actor.GetBoneTransform(from), b = actor.GetBoneTransform(to);
                        if (a == null || b == null) continue;
                        Vector3 v = actor.transform.InverseTransformDirection(b.position - a.position).normalized;
                        report.AppendLine($"   {from}->{to} {v.x:F3} {v.y:F3} {v.z:F3}");
                    }
                    Vector3 focus = hips.position + Vector3.up * .3f;
                    camera.orthographicSize = .62f;
                    foreach (var (view, dir) in new[] { ("front", new Vector3(.45f, .15f, 1f)), ("left", new Vector3(-1f, .1f, .25f)), ("back", new Vector3(.5f, .3f, -1f)) })
                    {
                        Vector3 d = actor.transform.TransformDirection(dir.normalized);
                        camera.transform.SetPositionAndRotation(focus + d * 3f, Quaternion.LookRotation(-d));
                        Capture(camera, $"Logs/NamPose/{clipName}_{view}.png");
                    }
                    // the grip: both hands close up, from his front-right and from below the gun
                    Vector3 hands = (actor.GetBoneTransform(HumanBodyBones.LeftHand).position + actor.GetBoneTransform(HumanBodyBones.RightHand).position) / 2;
                    float reach = Vector3.Distance(actor.GetBoneTransform(HumanBodyBones.LeftHand).position, actor.GetBoneTransform(HumanBodyBones.RightHand).position);
                    camera.orthographicSize = Mathf.Max(.14f, reach * .5f + .1f);
                    foreach (var (view, dir) in new[] { ("hands", new Vector3(.6f, .25f, 1f)), ("handsLow", new Vector3(.2f, -.6f, 1f)) })
                    {
                        Vector3 d = actor.transform.TransformDirection(dir.normalized);
                        camera.transform.SetPositionAndRotation(hands + d * 3f, Quaternion.LookRotation(-d));
                        Capture(camera, $"Logs/NamPose/{clipName}_{view}.png");
                    }
                }
                AnimationMode.StopAnimationMode();
            }
            finally
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                if (nam != null) Object.DestroyImmediate(nam);
                if (rig != null) Object.DestroyImmediate(rig);
            }
            File.WriteAllText("Logs/NamPose/report.txt", report.ToString());
            Debug.Log("[NamPose]\n" + report);
        }

        /// <summary>
        /// Every Nam clip at a quarter, half and three quarters: the bone segments and the barrel in his own frame
        /// (right, up, forward), one line each, into Logs/NamPose/measure.txt — compared against the same numbers
        /// taken from the Blender master (blender_scripts/measure_nam_clips.py).
        /// </summary>
        public static void Measure()
        {
            Directory.CreateDirectory("Logs/NamPose");
            var grip = AssetDatabase.LoadAssetAtPath<WeaponGripConfig>(Grip);
            var lines = new System.Collections.Generic.List<string>();
            GameObject nam = null;
            try
            {
                nam = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Player));
                nam.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var behaviour in nam.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                var actor = nam.GetComponentInChildren<Animator>();
                Transform anchor = actor.GetComponentsInChildren<Transform>(true).First(t => t.name == "WeaponAnchor");
                var rifle = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Rifle), anchor);
                grip.Apply(rifle.transform, WeaponKind.Rifle);
                MeasureClips(actor, rifle.transform, Directory.GetFiles(StoryAnimationSetup.NamFolder, "Nam_*.fbx"), lines);
            }
            finally
            {
                if (nam != null) Object.DestroyImmediate(nam);
            }
            File.WriteAllLines("Logs/NamPose/measure.txt", lines);
            Debug.Log($"[NamPose] {lines.Count} measurements written.");
        }

        /// <summary>Same measurement for the guards (lính rig) and the commander, on their own models and clips.</summary>
        public static void MeasureEnemies()
        {
            Directory.CreateDirectory("Logs/NamPose");
            foreach (var (model, prefix, file) in new[] {
                ("Assets/_Project/Art/Characters/Enemies/lính rig.fbx", "Enemy_", "measure_enemy.txt"),
                ("Assets/_Project/Art/Characters/Enemies/chỉ huy mỹ.fbx", "Boss_", "measure_boss.txt") })
            {
                var lines = new System.Collections.Generic.List<string>();
                GameObject body = null;
                try
                {
                    body = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(model));
                    body.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    MeasureClips(body.GetComponentInChildren<Animator>(), null, Directory.GetFiles(StoryAnimationSetup.EnemyFolder, prefix + "*.fbx"), lines);
                }
                finally
                {
                    if (body != null) Object.DestroyImmediate(body);
                }
                File.WriteAllLines("Logs/NamPose/" + file, lines);
                Debug.Log($"[NamPose] {file}: {lines.Count} measurements.");
            }
        }

        /// <summary>
        /// Nam's fingers in every clip: per finger the knuckle, the two phalanx segments and the fingertip direction
        /// (the distal bone's own axis), in his frame, into Logs/NamPose/measure_fingers.txt.
        /// </summary>
        [MenuItem("ShadowVale/Characters/Measure Nam Fingers")]
        public static void MeasureFingers()
        {
            Directory.CreateDirectory("Logs/NamPose");
            var lines = new System.Collections.Generic.List<string>();
            GameObject nam = null;
            try
            {
                nam = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Player));
                nam.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var behaviour in nam.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                MeasureClips(nam.GetComponentInChildren<Animator>(), null, Directory.GetFiles(StoryAnimationSetup.NamFolder, "Nam_*.fbx"), lines, fingers: true);
            }
            finally
            {
                if (nam != null) Object.DestroyImmediate(nam);
            }
            File.WriteAllLines("Logs/NamPose/measure_fingers.txt", lines);

            // the finger muscles each clip asks for: beyond +-1 the avatar's limits cut the pose off
            var muscles = new System.Collections.Generic.List<string>();
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
                if (HumanTrait.MuscleName[i].Contains("Thumb") || HumanTrait.MuscleName[i].Contains("Index") || HumanTrait.MuscleName[i].Contains("Middle")
                    || HumanTrait.MuscleName[i].Contains("Ring") || HumanTrait.MuscleName[i].Contains("Little"))
                    muscles.Add($"limit {HumanTrait.MuscleName[i]} {HumanTrait.GetMuscleDefaultMin(i):F1} {HumanTrait.GetMuscleDefaultMax(i):F1}");
            foreach (string path in Directory.GetFiles(StoryAnimationSetup.NamFolder, "Nam_*.fbx").OrderBy(p => p))
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(path.Replace('\\', '/')).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
                if (clip == null) continue;
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (!(binding.propertyName.Contains("Thumb") || binding.propertyName.Contains("Index") || binding.propertyName.Contains("Middle")
                          || binding.propertyName.Contains("Ring") || binding.propertyName.Contains("Little"))) continue;
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    float lo = curve.keys.Min(k => k.value), hi = curve.keys.Max(k => k.value);
                    if (lo < -1.001f || hi > 1.001f)
                        muscles.Add($"over {Path.GetFileNameWithoutExtension(path)} {binding.propertyName} {lo:F2} {hi:F2}");
                }
            }
            File.WriteAllLines("Logs/NamPose/finger_muscles.txt", muscles);
            Debug.Log($"[NamPose] {lines.Count} finger measurements written, {muscles.Count(m => m.StartsWith("over"))} finger curves beyond the limits.");
        }

        private static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Little" };

        /// <summary>(layer, state, the clip it plays) — through AC_Player itself, its layers and their masks.</summary>
        private static readonly (string layer, string state, string clip)[] ControllerShots = {
            ("Base Layer", "Locomotion_Rifle", "Nam_Rifle_Idle"), (PlayerCombat.UpperBodyLayer, "Aim_Rifle", "Nam_Rifle_AimIdle"),
            (PlayerCombat.UpperBodyLayer, "Reload_Rifle", "Nam_Rifle_ReloadTactical"), (StoryAnimationSetup.ActionsUpperLayer, "Act_Throw", "Nam_Throw"),
            ("Base Layer", "Act_Bandage", "Nam_Bandage"), ("Base Layer", "Act_Untie", "Nam_Untie"),
        };

        /// <summary>
        /// The fingers as the game's Animator actually plays them (layers, masks, the Humanoid body), at the middle
        /// of each state, into Logs/NamPose/measure_fingers_controller.txt under the clip's name.
        /// </summary>
        public static void MeasureFingersInController()
        {
            Directory.CreateDirectory("Logs/NamPose");
            var lines = new System.Collections.Generic.List<string>();
            GameObject nam = null;
            try
            {
                nam = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Player));
                nam.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var behaviour in nam.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                var actor = nam.GetComponentInChildren<Animator>();
                Vector3 hipsForward = HipsForward(actor);
                foreach (var (layerName, state, clip) in ControllerShots)
                {
                    actor.Rebind();
                    int layer = actor.GetLayerIndex(layerName);
                    if (layer < 0 || !actor.HasState(layer, Animator.StringToHash(state))) { lines.Add($"# {layerName}/{state} missing"); continue; }
                    for (int i = 1; i < actor.layerCount; i++) actor.SetLayerWeight(i, i == layer ? 1f : 0f);
                    actor.Play(state, layer, .5f);
                    actor.Update(0f); actor.Update(0f);
                    Transform hips = actor.GetBoneTransform(HumanBodyBones.Hips);
                    Vector3 fwd = Vector3.ProjectOnPlane(hips.rotation * hipsForward, Vector3.up).normalized;
                    MeasureFingerChain(actor, Quaternion.LookRotation(fwd, Vector3.up), clip, .5f, lines);
                }
            }
            finally
            {
                if (nam != null) Object.DestroyImmediate(nam);
            }
            File.WriteAllLines("Logs/NamPose/measure_fingers_controller.txt", lines);
            Debug.Log($"[NamPose] {lines.Count} finger measurements through AC_Player.");
        }

        /// <summary>
        /// Where each arm's roll sits in Nam's clips, into Logs/NamPose/measure_twist.txt: the forearm bone (a pure hinge
        /// in Blender, so close to 0), the hand, and the two twist bones both as the clip bakes them and as
        /// <see cref="ForearmTwist"/> sets them from the hand. Degrees from the rest pose, at a quarter, half and three
        /// quarters of each clip; compared with the master by blender_scripts/measure_twist.py. The knife-carry clips
        /// (posed in Unity, no baked twist) are listed too.
        /// </summary>
        [MenuItem("ShadowVale/Characters/Measure Nam Forearm Twist")]
        public static void MeasureForearmTwist()
        {
            Directory.CreateDirectory("Logs/NamPose");
            var lines = new System.Collections.Generic.List<string>();
            var rest = AssetDatabase.LoadAssetAtPath<GameObject>(NamModelSetup.ModelPath).GetComponentsInChildren<Transform>(true)
                .GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First().localRotation);
            GameObject nam = null;
            try
            {
                nam = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Player));
                nam.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var behaviour in nam.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                var actor = nam.GetComponentInChildren<Animator>();
                var driver = actor.GetComponent<ForearmTwist>();
                var byName = actor.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                float Roll(string bone) => byName.TryGetValue(bone, out Transform t) && rest.TryGetValue(bone, out Quaternion r)
                    ? ForearmTwist.Roll(Quaternion.Inverse(r) * t.localRotation, bone.StartsWith("Left") ? 45f : -80f) : float.NaN;
                var paths = Directory.GetFiles(StoryAnimationSetup.NamFolder, "Nam_*.fbx")
                    .Concat(Directory.GetFiles(QuickTakedownSetup.Folder, "Nam_*.anim")).OrderBy(p => p);
                lines.Add($"# avatar forearm twist share {actor.avatar.humanDescription.lowerArmTwist:F2}, driver {(driver != null ? "present" : "absent")}");
                AnimationMode.StartAnimationMode();
                try
                {
                    foreach (string path in paths)
                    {
                        var clip = AssetDatabase.LoadAllAssetsAtPath(path.Replace('\\', '/')).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
                        if (clip == null) continue;
                        foreach (float t in new[] { .25f, .5f, .75f })
                        {
                            // from rest each time: a clip without twist curves must not show the last clip's
                            foreach (var pair in byName)
                                if (StoryAnimationSetup.IsForearmTwist(pair.Key)) pair.Value.localRotation = rest[pair.Key];
                            AnimationMode.BeginSampling();
                            AnimationMode.SampleAnimationClip(actor.gameObject, clip, t * clip.length);
                            AnimationMode.EndSampling();
                            var baked = new[] { "Left", "Right" }.ToDictionary(s => s, s => (Roll(s + "ForeArmTwist1"), Roll(s + "ForeArmTwist2")));
                            if (driver != null) driver.Apply();
                            foreach (string side in new[] { "Left", "Right" })
                                lines.Add($"{clip.name} {t:F2} {side} forearm {Roll(side + "ForeArm"):F1} hand {Roll(side + "Hand"):F1} " +
                                          $"baked {baked[side].Item1:F1} {baked[side].Item2:F1} driven {Roll(side + "ForeArmTwist1"):F1} {Roll(side + "ForeArmTwist2"):F1}");
                        }
                    }
                }
                finally
                {
                    AnimationMode.StopAnimationMode();
                }
            }
            finally
            {
                if (nam != null) Object.DestroyImmediate(nam);
            }
            File.WriteAllLines("Logs/NamPose/measure_twist.txt", lines);
            Debug.Log($"[NamPose] {lines.Count - 1} forearm twist measurements written.");
        }

        /// <summary>What one clip carries: its curve bindings grouped by kind, and the finger ones listed.</summary>
        public static void ClipBindings()
        {
            Directory.CreateDirectory("Logs/NamPose");
            var path = StoryAnimationSetup.NamFolder + "Nam_Rifle_Idle.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
            var bindings = AnimationUtility.GetCurveBindings(clip);
            var lines = new System.Collections.Generic.List<string> {
                $"clip {clip.name} humanMotion={clip.humanMotion} bindings={bindings.Length}",
                $"importer maskType={importer.clipAnimations[0].maskType} mask={importer.clipAnimations[0].maskSource} transforms in avatar skeleton={importer.sourceAvatar?.humanDescription.skeleton.Length}" };
            lines.AddRange(bindings.GroupBy(b => b.type.Name + (string.IsNullOrEmpty(b.path) ? " (root)" : " (path)")).Select(g => $"  {g.Key}: {g.Count()}"));
            lines.AddRange(bindings.Where(b => b.path.Contains("Index") || b.propertyName.Contains("Index")).Take(12).Select(b => $"  {b.path} {b.propertyName}"));
            File.WriteAllLines("Logs/NamPose/bindings.txt", lines);
        }

        private static void MeasureFingerChain(Animator actor, Quaternion frame, string clipName, float t, System.Collections.Generic.List<string> lines)
        {
            // the finger bones by their rig names (LeftHandIndex1..3): they are not Humanoid bones
            var byName = actor.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            foreach (string side in new[] { "Left", "Right" })
                foreach (string finger in FingerNames)
                {
                    var hand = actor.GetBoneTransform((HumanBodyBones)System.Enum.Parse(typeof(HumanBodyBones), side + "Hand"));
                    string rigFinger = finger == "Little" ? "Pinky" : finger;
                    var chain = new[] { 1, 2, 3 }.Select(i => byName.TryGetValue($"{side}Hand{rigFinger}{i}", out var b) ? b : null).ToArray();
                    if (hand == null || chain.Any(b => b == null)) continue;
                    void Add(string key, Vector3 v)
                    {
                        v = Quaternion.Inverse(frame) * v.normalized;
                        lines.Add($"{clipName} {t:F2} {key} {v.x:F4} {v.y:F4} {v.z:F4}");
                    }
                    Add($"{side}Hand-{side}{finger}Proximal", chain[0].position - hand.position);
                    Add($"{side}{finger}Proximal-{side}{finger}Intermediate", chain[1].position - chain[0].position);
                    Add($"{side}{finger}Intermediate-{side}{finger}Distal", chain[2].position - chain[1].position);
                    // the fingertip: the distal bone along its own axis, which the chain shows (intermediate -> distal)
                    Vector3 axis = Quaternion.Inverse(chain[1].rotation) * (chain[2].position - chain[1].position);
                    Add($"{side}{finger}Distal-tip", chain[2].rotation * axis);
                }
        }

        private static void MeasureClips(Animator actor, Transform rifle, System.Collections.Generic.IEnumerable<string> paths, System.Collections.Generic.List<string> lines, bool fingers = false)
        {
            Vector3 hipsForward = HipsForward(actor);
            AnimationMode.StartAnimationMode();
            try
            {
                foreach (string path in paths.OrderBy(p => p))
                {
                    string clipName = Path.GetFileNameWithoutExtension(path);
                    var clip = AssetDatabase.LoadAllAssetsAtPath(path.Replace('\\', '/')).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
                    if (clip == null) continue;
                    foreach (float t in new[] { .25f, .5f, .75f })
                    {
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(actor.gameObject, clip, t * clip.length);
                        AnimationMode.EndSampling();
                        Transform hips = actor.GetBoneTransform(HumanBodyBones.Hips);
                        // their own frame is the hips' heading, so root motion baked into a take does not count as error
                        Vector3 fwd = Vector3.ProjectOnPlane(hips.rotation * hipsForward, Vector3.up).normalized;
                        var frame = Quaternion.LookRotation(fwd, Vector3.up);
                        if (fingers) { MeasureFingerChain(actor, frame, clipName, t, lines); continue; }
                        foreach (var (from, to) in Segments)
                        {
                            Transform a = actor.GetBoneTransform(from), b = actor.GetBoneTransform(to);
                            if (a == null || b == null) continue;
                            Vector3 v = Quaternion.Inverse(frame) * (b.position - a.position).normalized;
                            lines.Add($"{clipName} {t:F2} {from}-{to} {v.x:F4} {v.y:F4} {v.z:F4}");
                        }
                        if (rifle == null) continue;
                        Vector3 barrel = Quaternion.Inverse(frame) * rifle.forward;
                        lines.Add($"{clipName} {t:F2} Barrel {barrel.x:F4} {barrel.y:F4} {barrel.z:F4}");
                    }
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }
        }

        /// <summary>The hips bone's axis that points where the character faces at rest.</summary>
        private static Vector3 HipsForward(Animator actor)
        {
            // at rest he faces the model's +Z; express that in the hips' rest frame
            Transform hips = actor.GetBoneTransform(HumanBodyBones.Hips);
            var skeleton = actor.avatar.humanDescription.skeleton;
            Quaternion rest = Quaternion.identity;
            for (Transform t = hips; t != null && t != actor.transform; t = t.parent)
            {
                var bone = skeleton.FirstOrDefault(s => s.name == t.name);
                rest = bone.rotation * rest;
            }
            return Quaternion.Inverse(rest) * Vector3.forward;
        }

        private static Vector3 ClosestOnLine(Vector3 origin, Vector3 direction, Vector3 point) =>
            origin + direction * Vector3.Dot(point - origin, direction);

        private static void Capture(Camera camera, string path)
        {
            var target = RenderTexture.GetTemporary(520, 520, 24);
            var image = new Texture2D(520, 520, TextureFormat.RGB24, false);
            var active = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 520, 520), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(image);
            }
        }
    }
}
