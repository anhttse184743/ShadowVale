using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace ShadowVale.Map01
{
    /// <summary>In-world briefing. Reuses the player's visual, avatar and the existing idle.
    /// No video, duplicate character textures, Timeline package or additional controller.</summary>
    public sealed class Map01OpeningCutscene : MonoBehaviour
    {
        public AnimationClip idle, talking, pointing, salute;
        public AudioClip commanderVoice, soldierVoice;
        public static bool Pending { get; private set; }
        public static bool Active { get; private set; }
        public static void RequestNewGame() => Pending = true;
        public static void CancelPending() => Pending = false;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Pending = Active = false; }

        private Map01Mission mission;
        private Animator soldier, commander;
        private ThirdPersonCamera rig;
        private ClipPlayer soldierClips, commanderClips;
        private AudioSource voice;
        private readonly List<Renderer> hiddenWeapons = new();
        private readonly List<Material> materials = new();
        private Vector3 origin, forward, right;
        private Quaternion initialRotation;
        private float clock, skipHeld, replyAt, releaseAt, finishAt;
        private bool ownsInput, finishing, playerRootMotion;
        private string subtitle;
        private GUIStyle subtitleStyle;
        public bool IsPlaying => ownsInput;
        public float Duration => finishAt;

        public static void Attach(Map01Mission mission)
        {
            bool requested = Pending; Pending = false;
            var prefab = Resources.Load<Map01OpeningCutscene>("Cutscenes/Map01Opening");
            if (prefab == null) {
                if (requested) Debug.LogError("Opening cutscene assets missing. Run ShadowVale/Cutscene/Prepare opening.");
                return;
            }
            var instance = Instantiate(prefab, mission.transform);
            instance.name = "Map 1 briefing";
            instance.Initialize(mission, requested);
        }

        private void Initialize(Map01Mission owner, bool play)
        {
            mission = owner;
            soldier = owner.player.GetComponentInChildren<Animator>();
            rig = owner.gameCamera.GetComponent<ThirdPersonCamera>();
            if (soldier == null || !soldier.isHuman || idle == null || salute == null || rig == null) {
                Debug.LogError("Opening requires a Humanoid player, idle, salute and ThirdPersonCamera.", this);
                Destroy(gameObject); return;
            }
            origin = owner.player.position;
            initialRotation = owner.player.rotation;
            forward = owner.player.forward; forward.y = 0; forward.Normalize();
            // Face the closest briefing table when one is within the shelter.
            var table = FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name == "BriefingTable")
                .OrderBy(t => (t.position - origin).sqrMagnitude).FirstOrDefault();
            if (table != null && Vector3.Distance(table.position, origin) < 7) {
                var direction = table.position - origin; direction.y = 0;
                if (direction.sqrMagnitude > .1f) forward = direction.normalized;
            }
            right = Vector3.Cross(Vector3.up, forward);
            // Clone only the visual hierarchy, never the player controller, health or quest.
            var visual = Instantiate(soldier.gameObject, transform);
            visual.name = "Commander (shared player mesh)";
            foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>()) Destroy(behaviour);
            foreach (var collider in visual.GetComponentsInChildren<Collider>()) Destroy(collider);
            foreach (var weapon in visual.GetComponentsInChildren<Weapon>()) weapon.gameObject.SetActive(false);
            commander = visual.GetComponent<Animator>();
            commander.runtimeAnimatorController = null;
            commander.applyRootMotion = false;
            visual.transform.SetPositionAndRotation(origin + forward * 2.1f, Quaternion.LookRotation(-forward));
            DecorateCommander();
            commanderClips = new ClipPlayer(commander, true, idle, talking != null ? talking : idle,
                pointing != null ? pointing : idle);
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false; voice.spatialBlend = 0; voice.volume = .9f;
            if (!play) return;
            ownsInput = Active = true;
            mission.Cinematic = true;
            playerRootMotion = soldier.applyRootMotion;
            soldier.applyRootMotion = false;
            mission.player.rotation = Quaternion.LookRotation(forward);
            foreach (var weapon in mission.player.GetComponentsInChildren<Weapon>(true))
                foreach (var renderer in weapon.GetComponentsInChildren<Renderer>())
                    if (renderer.enabled) { hiddenWeapons.Add(renderer); renderer.enabled = false; }
            soldierClips = new ClipPlayer(soldier, false, idle, salute);
            replyAt = Mathf.Max(20.5f, commanderVoice != null ? commanderVoice.length + .5f : 0);
            float saluteTime = Mathf.Max(salute.length, soldierVoice != null ? soldierVoice.length : 0);
            releaseAt = replyAt + saluteTime + .25f;
            finishAt = releaseAt + 1.2f;
            SetView(0, 1);
            if (commanderVoice != null) { voice.clip = commanderVoice; voice.Play(); }
        }

        private void Update()
        {
            commanderClips?.Tick(Time.deltaTime);
            soldierClips?.Tick(Time.deltaTime);
            if (!ownsInput) return;
            clock += Time.deltaTime;
            skipHeld = Keyboard.current != null && Keyboard.current.escapeKey.isPressed
                ? skipHeld + Time.unscaledDeltaTime : 0;
            if (skipHeld >= 1 && !finishing) BeginHandoff();
            if (finishing) {
                float weight = 1 - Mathf.Clamp01((clock - releaseAt) / 1.2f);
                SetView(2, weight);
                if (clock >= finishAt) Complete();
                return;
            }
            int part = clock < 6 ? 0 : clock < 13 ? 1 : clock < replyAt ? 2 : 3;
            string[] lines = {
                "Chỉ huy: Đồng chí ra bến tàu phía Bắc nhận tiếp tế cho đơn vị.",
                "Chỉ huy: Kiểm tra lương thực, thuốc men và trang bị. Phối hợp bốc dỡ, đưa hàng về điểm tập kết.",
                "Chỉ huy: Hùng chưa trở về từ bến tàu. Nếu gặp anh ấy, đưa về căn cứ an toàn. Có trở ngại, báo ngay cho tôi!",
                "Nam: Rõ! Tôi sẽ hoàn thành nhiệm vụ!"
            };
            subtitle = lines[part];
            commanderClips.Select(part == 0 ? 2 : part < 3 ? 1 : 0);
            if (part == 3 && soldierClips.Selected != 1) {
                soldierClips.Select(1);
                voice.Stop();
                if (soldierVoice != null) { voice.clip = soldierVoice; voice.Play(); }
            }
            SetView(part == 0 ? 0 : part == 3 ? 2 : 1, 1);
            if (clock >= releaseAt) BeginHandoff();
        }

        private void SetView(int shot, float weight)
        {
            Vector3 focus = origin + forward * 1.05f + Vector3.up * 1.35f;
            Vector3 position = shot == 0 ? origin - forward * 1.6f + right * 2 + Vector3.up * 1.7f
                : shot == 1 ? origin + right * .75f - forward * .35f + Vector3.up * 1.65f
                : origin + forward * 1.3f - right * 1.05f + Vector3.up * 1.6f;
            if (shot == 1) focus = commander.transform.position + Vector3.up * 1.4f;
            if (shot == 2) focus = origin + Vector3.up * 1.4f;
            // Keep a shot on the subject's side of shelter walls.
            var direction = position - focus;
            if (Physics.SphereCast(focus, .12f, direction.normalized, out var hit, direction.magnitude,
                mission.ObstructionMask, QueryTriggerInteraction.Ignore))
                position = focus + direction.normalized * Mathf.Max(.4f, hit.distance - .15f);
            rig.SetCinematicView(position, Quaternion.LookRotation(focus - position), 52, weight);
        }

        private void BeginHandoff()
        {
            if (!ownsInput || finishing) return;
            finishing = true; releaseAt = clock; finishAt = clock + 1.2f;
            voice.Stop(); subtitle = null;
            soldierClips.Select(0); commanderClips.Select(0);
        }

        public void Skip() => BeginHandoff();

        private void Complete()
        {
            if (!ownsInput) return;
            ownsInput = Active = false;
            soldierClips?.Dispose(); soldierClips = null;
            soldier.applyRootMotion = playerRootMotion;
            foreach (var renderer in hiddenWeapons) if (renderer != null) renderer.enabled = true;
            hiddenWeapons.Clear();
            rig.ClearCinematicView();
            mission.Cinematic = false;
            ForestMenu.SuppressKeysAfterCutscene();
            mission.Say("Nhiệm vụ: Ra bến tàu phía Bắc, tìm Hùng và đưa anh ấy cùng hàng tiếp tế về căn cứ.", 7);
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }

        private void DecorateCommander()
        {
            // Property blocks keep the original skin/material assets shared and untouched.
            var tint = new MaterialPropertyBlock();
            foreach (var renderer in commander.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                renderer.GetPropertyBlock(tint);
                tint.SetColor("_BaseColor", new Color(.82f, .88f, .75f, 1));
                renderer.SetPropertyBlock(tint);
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var badge = new Material(shader) { color = new Color(.64f, .49f, .15f) };
            materials.Add(badge);
            AddBadge(HumanBodyBones.LeftUpperArm, new Vector3(0, .035f, 0), new Vector3(.075f, .018f, .09f), badge);
            AddBadge(HumanBodyBones.RightUpperArm, new Vector3(0, .035f, 0), new Vector3(.075f, .018f, .09f), badge);
            AddBadge(HumanBodyBones.Chest, new Vector3(.10f, .06f, .13f), new Vector3(.085f, .025f, .009f), badge);
        }

        private void AddBadge(HumanBodyBones bone, Vector3 offset, Vector3 size, Material material)
        {
            var anchor = commander.GetBoneTransform(bone);
            if (anchor == null) return;
            var badge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            badge.name = "Commander insignia";
            Destroy(badge.GetComponent<Collider>());
            badge.transform.SetParent(anchor, false);
            badge.transform.localPosition = offset; badge.transform.localScale = size;
            badge.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void OnGUI()
        {
            if (!ownsInput) return;
            subtitleStyle ??= new GUIStyle(GUI.skin.label) {
                fontSize = Mathf.Max(16, Mathf.RoundToInt(Screen.height / 36f)),
                alignment = TextAnchor.MiddleCenter, wordWrap = true,
                normal = { textColor = new Color(.96f, .92f, .8f) }
            };
            var old = GUI.color; int depth = GUI.depth; GUI.depth = -90;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height * .08f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, Screen.height * .82f, Screen.width, Screen.height * .18f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width * .12f, Screen.height * .835f, Screen.width * .76f, Screen.height * .1f),
                subtitle ?? "", subtitleStyle);
            GUI.Label(new Rect(Screen.width - 240, 12, 225, 30), "Giữ Esc 1 giây để bỏ qua");
            GUI.color = old; GUI.depth = depth;
        }

        private void OnDisable() { if (ownsInput) Complete(); }
        private void OnDestroy()
        {
            soldierClips?.Dispose(); commanderClips?.Dispose();
            foreach (var material in materials) if (material != null) Destroy(material);
        }

        private sealed class ClipPlayer
        {
            private PlayableGraph graph;
            private AnimationMixerPlayable mixer;
            private AnimationClipPlayable[] clips;
            private AvatarMask relaxedArmMask;
            public int Selected { get; private set; }
            public ClipPlayer(Animator target, bool relaxedArms, params AnimationClip[] assets)
            {
                graph = PlayableGraph.Create("Briefing " + target.name);
                mixer = AnimationMixerPlayable.Create(graph, assets.Length);
                clips = new AnimationClipPlayable[assets.Length];
                for (int i = 0; i < assets.Length; i++) {
                    clips[i] = AnimationClipPlayable.Create(graph, assets[i]);
                    clips[i].SetApplyFootIK(true);
                    graph.Connect(clips[i], 0, mixer, i);
                    mixer.SetInputWeight(i, i == 0 ? 1 : 0);
                }
                var output = AnimationPlayableOutput.Create(graph, "Pose", target);
                if (relaxedArms) {
                    // The shared rig's tuned idle already straightens elbows and wrists.
                    // Reuse only its arms; retain the dialogue clip's torso/head motion.
                    // This layer belongs to the commander, never the soldier's salute.
                    relaxedArmMask = new AvatarMask();
                    for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                        relaxedArmMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
                    relaxedArmMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
                    relaxedArmMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
                    relaxedArmMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
                    relaxedArmMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
                    var relaxedIdle = AnimationClipPlayable.Create(graph, assets[0]);
                    var layers = AnimationLayerMixerPlayable.Create(graph, 2);
                    graph.Connect(mixer, 0, layers, 0);
                    graph.Connect(relaxedIdle, 0, layers, 1);
                    layers.SetInputWeight(0, 1);
                    layers.SetInputWeight(1, 1);
                    layers.SetLayerMaskFromAvatarMask(1, relaxedArmMask);
                    output.SetSourcePlayable(layers);
                } else output.SetSourcePlayable(mixer);
                graph.Play();
            }
            public void Select(int index)
            {
                if (Selected == index) return;
                Selected = index; clips[index].SetTime(0);
            }
            public void Tick(float dt)
            {
                if (!graph.IsValid()) return;
                for (int i = 0; i < clips.Length; i++)
                    mixer.SetInputWeight(i, Mathf.MoveTowards(mixer.GetInputWeight(i), i == Selected ? 1 : 0, dt * 5));
            }
            public void Dispose()
            {
                if (graph.IsValid()) graph.Destroy();
                if (relaxedArmMask != null) Object.Destroy(relaxedArmMask);
            }
        }
    }
}

