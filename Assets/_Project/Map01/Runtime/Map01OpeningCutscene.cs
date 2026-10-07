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
        public AnimationClip soldierIdle;
        public AudioClip commanderVoice, soldierVoice;
        public float[] commanderSubtitleStarts = { 0, 6, 13 };
        public float saluteRaiseSeconds=.7f, saluteLowerSeconds=1f;
        public float saluteHoldTime=.7f, saluteLowerStart=.9f;
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
        private readonly List<GameObject> hiddenWeapons = new();
        private readonly List<Material> materials = new();
        private Vector3 origin, forward, right;
        private Quaternion initialRotation;
        private const float IntroDuration = 3.5f;
        private float introRemaining, clock, skipHeld, replyAt, releaseAt, finishAt;
        private float commanderSpeechTime;
        private float saluteAt,lowerAt=-1,viewBlend=1;
        private bool replyPlayed;
        private int previousShot=-1;
        private Vector3 viewFrom;
        private Vector3 viewFromFocus,viewFocus;
        private bool ownsInput, finishing, playerRootMotion;
        private RuntimeAnimatorController gameplayAnimator;
        private float[] gameplayLayerWeights;
        private bool playerWasEnabled, combatWasEnabled;
        private string subtitle;
        private GUIStyle subtitleStyle;
        public bool IsPlaying => ownsInput;
        public float Duration => finishAt + IntroDuration;
        public float BriefingTime => clock;
        public float SaluteBeginsAt => saluteAt;
        public bool IsIntroducing => introRemaining > 0;

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
            foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>()) if (!(behaviour is Weapon)) Destroy(behaviour);
            foreach (var collider in visual.GetComponentsInChildren<Collider>()) Destroy(collider);
            foreach (var weapon in visual.GetComponentsInChildren<Weapon>(true)) weapon.gameObject.SetActive(false);
            commander = visual.GetComponent<Animator>();
            commander.runtimeAnimatorController = null;
            commander.applyRootMotion = false;
            visual.transform.SetPositionAndRotation(origin + forward * 2.1f, Quaternion.LookRotation(-forward));
            DecorateCommander();
            // Briefing is unarmed; extraction attaches/enables the rifle when seating him.
            commanderClips = new ClipPlayer(commander, true, idle, talking != null ? talking : idle,
                pointing != null ? pointing : idle);
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false; voice.spatialBlend = 0; voice.volume = .9f;
            if (!play) return;
            ownsInput = Active = true;
            introRemaining = IntroDuration;
            mission.Cinematic = true;
            playerRootMotion = soldier.applyRootMotion;
            // Input gating does not stop gameplay's upper-body stance updates.
            // Give the cutscene exclusive ownership, then restore it on every exit path.
            playerWasEnabled = mission.ModernPlayer != null && mission.ModernPlayer.enabled;
            combatWasEnabled = mission.ModernCombat != null && mission.ModernCombat.enabled;
            if (mission.ModernPlayer != null) mission.ModernPlayer.enabled = false;
            if (mission.ModernCombat != null) mission.ModernCombat.enabled = false;
            gameplayAnimator = soldier.runtimeAnimatorController;
            gameplayLayerWeights = new float[soldier.layerCount];
            for (int i = 0; i < gameplayLayerWeights.Length; i++) gameplayLayerWeights[i] = soldier.GetLayerWeight(i);
            soldier.runtimeAnimatorController = null;
            soldier.applyRootMotion = false;
            mission.player.rotation = Quaternion.LookRotation(forward);
            foreach (var weapon in mission.player.GetComponentsInChildren<Weapon>(true))
                if (weapon.gameObject.activeSelf) {
                    // Disable the prop itself: gameplay grip/visibility updates cannot
                    // bring the rifle back during the salute or camera handoff.
                    hiddenWeapons.Add(weapon.gameObject);
                    weapon.gameObject.SetActive(false);
                }
            soldierClips = new ClipPlayer(soldier, false, soldierIdle != null ? soldierIdle : idle, salute);
            replyAt = commanderVoice != null ? commanderVoice.length + .95f : 20.95f;
            saluteAt=replyAt-saluteRaiseSeconds;
            float saluteTime = Mathf.Max(salute.length, soldierVoice != null ? soldierVoice.length : 0);
            releaseAt = replyAt + saluteTime + .25f;
            finishAt = releaseAt + 1.2f;
            SetView(0, 1);
            // Voice starts after the map title fades out.
        }

        private void Update()
        {
            commanderClips?.Tick(Time.deltaTime);
            soldierClips?.Tick(Time.deltaTime);
            if (!ownsInput) return;
            skipHeld = Keyboard.current != null && Keyboard.current.escapeKey.isPressed
                ? skipHeld + Time.unscaledDeltaTime : 0;
            if (skipHeld >= 1 && !finishing) BeginHandoff();
            if (introRemaining > 0) {
                introRemaining = Mathf.Max(0, introRemaining - Mathf.Min(Time.unscaledDeltaTime, .05f));
                SetView(0, 1);
                if (introRemaining == 0 && commanderVoice != null) { voice.clip = commanderVoice; voice.Play(); }
                return;
            }
            clock += Time.deltaTime;
            if (finishing) {
                float weight = 1 - Mathf.Clamp01((clock - releaseAt) / 1.2f);
                SetView(2, weight);
                if (clock >= finishAt) Complete();
                return;
            }
            // AudioSource time keeps subtitles on the actual spoken phrase even during slow frames.
            if(voice.clip==commanderVoice)commanderSpeechTime=Mathf.Max(commanderSpeechTime,voice.time);
            float speechTime=commanderSpeechTime;
            int part = speechTime < commanderSubtitleStarts[1] ? 0 : speechTime < commanderSubtitleStarts[2] ? 1 : 2;
            if(clock>=replyAt && !(voice.clip==commanderVoice && voice.isPlaying))part=3;
            string[] lines = {
                "Chỉ huy: Đồng chí ra bến tàu phía Bắc nhận tiếp tế cho đơn vị.",
                "Chỉ huy: Kiểm tra lương thực, thuốc men và trang bị. Phối hợp bốc dỡ, đưa hàng về điểm tập kết.",
                "Chỉ huy: Hùng chưa trở về từ bến tàu. Nếu gặp anh ấy, đưa về căn cứ an toàn. Có trở ngại, báo ngay cho tôi!",
                "Nam: Rõ! Tôi sẽ hoàn thành nhiệm vụ!"
            };
            subtitle = lines[part];
            commanderClips.Select(part == 0 ? 2 : part < 3 ? 1 : 0);
            if(clock>=saluteAt && !(voice.clip==commanderVoice && voice.isPlaying))soldierClips.Select(1);
            if (part == 3 && !replyPlayed) {
                replyPlayed=true;
                soldierClips.Select(1);
                voice.Stop();
                if (soldierVoice != null) { voice.clip = soldierVoice; voice.Play(); }
            }
            if(soldierClips.Selected==1) {
                if(replyPlayed && !voice.isPlaying && lowerAt<0)lowerAt=clock;
                // The dedicated clip already eases its arm path. Preserve that timing;
                // hold the authored salute rather than compressing the old clip's release.
                float clipTime=lowerAt>=0?Mathf.Lerp(saluteLowerStart,salute.length,Mathf.Clamp01((clock-lowerAt)/saluteLowerSeconds))
                    :Mathf.Lerp(0,saluteHoldTime,Mathf.Clamp01((clock-saluteAt)/saluteRaiseSeconds));
                soldierClips.SampleSelected(clipTime);
            }
            SetView(part == 0 ? 0 : clock>=saluteAt-.4f ? 2 : 1, 1);
            if (lowerAt>=0 && clock>=lowerAt+saluteLowerSeconds+.15f) BeginHandoff();
        }

        private void SetView(int shot, float weight)
        {
            Vector3 focus = origin + forward * 1.05f + Vector3.up * 1.35f;
            Vector3 position = shot == 0 ? origin - forward * 1.6f + right * 2 + Vector3.up * 1.7f
                : shot == 1 ? origin + right * 1.9f + forward * 1.3f + Vector3.up * 1.65f
                : origin + forward * 1.05f + right * 1.9f + Vector3.up * 1.62f;
            if (shot == 1) focus = commander.transform.position + Vector3.up * 1.4f;
            if (shot == 2) focus = origin + Vector3.up * 1.4f;
            // Keep a shot on the subject's side of shelter walls.
            var direction = position - focus;
            if (Physics.SphereCast(focus, .12f, direction.normalized, out var hit, direction.magnitude,
                mission.ObstructionMask, QueryTriggerInteraction.Ignore))
                position = focus + direction.normalized * Mathf.Max(.4f, hit.distance - .15f);
            if(previousShot!=shot){viewFromFocus=previousShot<0?focus:viewFocus;previousShot=shot;viewBlend=0;viewFrom=rig.transform.position;}
            viewBlend=Mathf.Min(1,viewBlend+Time.deltaTime/.65f);
            float blend=Mathf.SmoothStep(0,1,viewBlend);
            var eye=Vector3.Lerp(viewFrom,position,blend);viewFocus=Vector3.Lerp(viewFromFocus,focus,blend);
            rig.SetCinematicView(eye,Quaternion.LookRotation(viewFocus-eye),shot==2?48:52,weight);
        }

        private void BeginHandoff()
        {
            if (!ownsInput || finishing) return;
            introRemaining = 0;
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
            soldier.runtimeAnimatorController = gameplayAnimator;
            for (int i = 0; i < gameplayLayerWeights.Length && i < soldier.layerCount; i++)
                soldier.SetLayerWeight(i, gameplayLayerWeights[i]);
            soldier.applyRootMotion = playerRootMotion;
            foreach (var weapon in hiddenWeapons) if (weapon != null) weapon.SetActive(true);
            hiddenWeapons.Clear();
            if (mission.ModernPlayer != null) mission.ModernPlayer.enabled = playerWasEnabled;
            if (mission.ModernCombat != null) mission.ModernCombat.enabled = combatWasEnabled;
            rig.ClearCinematicView();
            mission.Cinematic = false;
            mission.ModernCombat?.RestoreAfterCinematic();
            ForestMenu.SuppressKeysAfterCutscene();
            // The quest HUD retains the objective. End subtitles and queued speech here,
            // instead of repeating the briefing after either natural playback or skip.
            mission.Say(null, 0);
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }

        public void ReleaseCommanderForExtraction()
        {
            commanderClips?.Dispose(); commanderClips = null;
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
            if (introRemaining > 0) {
                var savedColor = GUI.color; int savedDepth = GUI.depth;
                GUI.depth = -1000;
                float alpha = Mathf.Clamp01(introRemaining / .8f);
                GUI.color = new Color(0, 0, 0, alpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = new Color(.96f, .92f, .8f, alpha * Mathf.Clamp01((IntroDuration - introRemaining) / .6f));
                var title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.RoundToInt(Screen.height * .055f), fontStyle = FontStyle.Bold };
                GUI.Label(new Rect(0, Screen.height * .39f, Screen.width, Screen.height * .12f), "MAP 1", title);
                title.fontSize = Mathf.RoundToInt(Screen.height * .027f); title.fontStyle = FontStyle.Normal;
                GUI.Label(new Rect(0, Screen.height * .52f, Screen.width, Screen.height * .08f), "BẾN TÀU PHÍA BẮC", title);
                GUI.color = savedColor; GUI.depth = savedDepth;
                return;
            }
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
            private AnimationLayerMixerPlayable armLayers;
            private bool commanderArms;
            public int Selected { get; private set; }
            public ClipPlayer(Animator target, bool relaxedArms, params AnimationClip[] assets)
            {
                commanderArms=relaxedArms;
                graph = PlayableGraph.Create("Briefing " + target.name);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
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
                    armLayers = AnimationLayerMixerPlayable.Create(graph, 2);
                    graph.Connect(mixer, 0, armLayers, 0);
                    graph.Connect(relaxedIdle, 0, armLayers, 1);
                    armLayers.SetInputWeight(0, 1);
                    armLayers.SetInputWeight(1, 1);
                    armLayers.SetLayerMaskFromAvatarMask(1, relaxedArmMask);
                    output.SetSourcePlayable(armLayers);
                } else {
                    // Keep Nam's standing idle, head and left arm intact. The salute owns
                    // only his right arm/fingers, so the imported pose cannot tilt his body.
                    relaxedArmMask=new AvatarMask();
                    for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)relaxedArmMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
                    relaxedArmMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm,true);
                    relaxedArmMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers,true);
                    var baseIdle=AnimationClipPlayable.Create(graph,assets[0]);
                    armLayers=AnimationLayerMixerPlayable.Create(graph,2);
                    graph.Connect(baseIdle,0,armLayers,0);graph.Connect(mixer,0,armLayers,1);
                    armLayers.SetInputWeight(0,1);armLayers.SetInputWeight(1,1);
                    armLayers.SetLayerMaskFromAvatarMask(1,relaxedArmMask);output.SetSourcePlayable(armLayers);
                }
                graph.Play();
            }
            public void Select(int index)
            {
                if (Selected == index) return;
                Selected = index; clips[index].SetTime(0);
            }
            public void SampleSelected(float time){clips[Selected].SetTime(time);clips[Selected].SetSpeed(0);graph.Evaluate(0);}
            public void Tick(float dt)
            {
                if (!graph.IsValid()) return;
                // Let Pointing use its authored arms; keep relaxed arms for dialogue only.
                if (commanderArms && armLayers.IsValid())
                    armLayers.SetInputWeight(1, Mathf.MoveTowards(armLayers.GetInputWeight(1),
                        Selected == 2 ? 0 : 1, dt * 3));
                for (int i = 0; i < clips.Length; i++)
                    mixer.SetInputWeight(i, Mathf.MoveTowards(mixer.GetInputWeight(i), i == Selected ? 1 : 0, dt * 5));
                graph.Evaluate(dt);
            }
            public void Dispose()
            {
                if (graph.IsValid()) graph.Destroy();
                if (relaxedArmMask != null) Object.Destroy(relaxedArmMask);
            }
        }
    }
}

