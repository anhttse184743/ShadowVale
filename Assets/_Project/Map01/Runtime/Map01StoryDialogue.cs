using System;
using System.Collections.Generic;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    /// <summary>Short two-way conversations. Only the final, separate E press commits an order.</summary>
    [DefaultExecutionOrder(110)]
    public sealed class Map01StoryDialogue : MonoBehaviour
    {
        [Serializable] public sealed class Line
        {
            public string speaker, text, voice;
            public bool radio;
            public Line(string who, string words, bool remote = false, string audio = null)
            { speaker = who; text = words; radio = remote; voice = audio; }
        }
        public bool IsPlaying => lines != null;
        public Line Current => IsPlaying ? lines[index] : null;
        public int Index => index;
        public int Count => lines?.Length ?? 0;
        Map01Mission mission;
        Map01OpeningCutscene commander;
        Line[] lines;
        int index, startedFrame;
        float elapsed, escapeHeld, turnElapsed, cameraElapsed;
        Action accepted;
        string finalLabel;
        DialogueVoice voice;
        Func<bool> previousVoiceGate;
        Map01CinematicPose pose;
        AnimationClip idle;
        ThirdPersonCamera rig;
        Vector3 position, cameraPosition;
        Vector3 conversationCamera;
        Quaternion rotation, facing, cameraRotation, conversationRotation;
        Quaternion commanderFacing;
        bool turnedCommander;
        float fov, conversationFov;
        bool closeBriefing, radioConversation;
        Animator speakingActor;
        bool playerEnabled, combatEnabled, actionsEnabled, invulnerable, voicedLine;
        bool acceptOnSkip;
        float spokenSeconds;
        Map01NamActions actions;
        readonly List<GameObject> hidden = new();
        GUIStyle caption, heading, hint;
        Transform leftFoot,rightFoot;float supportY;

        public bool Begin(Line[] conversation, Action commit, string confirm = "Nhận nhiệm vụ", bool commitOnSkip=false)
        {
            mission = GetComponent<Map01Mission>();
            if (IsPlaying || mission == null || mission.Stopped || conversation == null || conversation.Length == 0) return false;
            var watch=System.Diagnostics.Stopwatch.StartNew();
            lines = conversation; index = 0; accepted = commit; finalLabel = confirm; acceptOnSkip=commitOnSkip;
            closeBriefing = conversation[0].voice == "m1_story_rescue_order_01";
            radioConversation=Array.Exists(conversation,l=>l.radio)||conversation[0].voice=="m1_v2_commander_confirm_01";
            startedFrame = Time.frameCount; escapeHeld = turnElapsed = 0;
            commander = mission.GetComponentInChildren<Map01OpeningCutscene>();
            mission.CloseGameplayPanel();
            mission.GetComponent<Map01StoneThrow>()?.Cancel();
            mission.GetComponent<Map01Scouting>()?.HoldBinoculars(false);
            DialogueVoice.StopAll(); mission.Say(null, 0);
            voice = DialogueVoice.For(gameObject);
            previousVoiceGate = voice.CanSpeak;
            voice.CanSpeak = () => IsPlaying && !mission.Paused && !ForestMenu.Visible;
            position = mission.player.position; rotation = mission.player.rotation;
            var post=mission.alliedCommanderPost;
            if(!radioConversation&&post!=null&&commander!=null&&commander.CommanderTransform!=null&&
                Vector3.Distance(commander.CommanderTransform.position,post.position)<.5f)
            {
                // Stage every shelter conversation at the same mark, independent of approach.
                var heading=Vector3.ProjectOnPlane(post.forward,Vector3.up).normalized;
                position=post.position+heading*1.8f;rotation=Quaternion.LookRotation(-heading);
                var body=mission.player.GetComponent<CharacterController>();bool wasEnabled=body!=null&&body.enabled;
                if(wasEnabled)body.enabled=false;
                mission.player.SetPositionAndRotation(position,rotation);
                if(wasEnabled)body.enabled=true;
                Physics.SyncTransforms();
            }
            facing = rotation;
            turnedCommander=false;
            if (!radioConversation && commander != null && commander.CommanderTransform != null) {
                var toward = Vector3.ProjectOnPlane(commander.CommanderTransform.position-position, Vector3.up);
                if (toward.sqrMagnitude > .01f) {
                    facing = Quaternion.LookRotation(toward);
                    commanderFacing=commander.CommanderTransform.rotation;turnedCommander=true;
                    commander.CommanderTransform.rotation=Quaternion.LookRotation(-toward);
                }
            }
            cameraPosition = mission.gameCamera.transform.position; cameraRotation = mission.gameCamera.transform.rotation; fov = mission.gameCamera.fieldOfView;
            conversationCamera = cameraPosition; conversationRotation = cameraRotation; conversationFov = fov;
            if (!radioConversation && commander != null && commander.CommanderTransform != null)
                FrameCommander();
            else if(radioConversation&&conversation[0].voice!=null&&conversation[0].voice.StartsWith("m1_ops_"))FrameRadioNam();
            rig = mission.gameCamera.GetComponent<ThirdPersonCamera>();
            rig.SetBinoculars(false, 1.6f);
            playerEnabled = mission.ModernPlayer.enabled; combatEnabled = mission.ModernCombat.enabled;
            actions = Map01NamActions.For(mission); actionsEnabled = actions != null && actions.enabled;
            if (actions != null) actions.enabled = false;
            mission.ModernPlayer.enabled = mission.ModernCombat.enabled = false;
            invulnerable = mission.ModernHealth.CinematicInvulnerable;
            mission.ModernHealth.CinematicInvulnerable = true;
            mission.Cinematic = true;
            foreach (var guard in mission.Enemies) if (guard != null && guard.Alive) guard.PauseForCinematic(true);
            var actor = mission.player.GetComponentInChildren<Animator>();speakingActor=actor;
            idle = commander != null ? commander.soldierIdle ?? commander.idle : null;
            if (idle == null) idle = Resources.Load<AnimationClip>("Rescue/Nam_Shelter_Idle");
            leftFoot=actor.GetBoneTransform(HumanBodyBones.LeftFoot);rightFoot=actor.GetBoneTransform(HumanBodyBones.RightFoot);
            supportY=Mathf.Min(leftFoot.position.y,rightFoot.position.y);
            pose = new Map01CinematicPose(actor,null,false); pose.Sample(idle, 0, true);
            foreach (var weapon in mission.player.GetComponentsInChildren<Weapon>(true))
                if (weapon.gameObject.activeSelf) { hidden.Add(weapon.gameObject); weapon.gameObject.SetActive(false); }
            Present();Map01InteractionWarmup.Record("dialogue",watch.Elapsed.TotalMilliseconds); return true;
        }
        void Present()
        {
            elapsed = 0;
            if(!radioConversation) {
                cameraPosition=mission.gameCamera.transform.position;cameraRotation=mission.gameCamera.transform.rotation;
                fov=mission.gameCamera.fieldOfView;cameraElapsed=0;FrameBriefingSpeaker();
            }
            // Explicit IDs and exact captions prevent an old monologue from voicing new text.
            voice.Stop();
            var line = Current;
            var recorded = string.IsNullOrEmpty(line.voice) ? null : DialogueVoice.Find(line.voice);
            string expectedCaption = line.speaker + (line.radio ? " (bộ đàm): " : ": ") + line.text;
            voicedLine = recorded != null && recorded.clip != null && recorded.caption == expectedCaption && recorded.radio == line.radio;
            spokenSeconds = voicedLine ? recorded.clip.length : 0;
            if (voicedLine) voice.Play(line.voice);
            else if (!string.IsNullOrEmpty(line.voice)) Debug.LogError("Missing or mismatched story recording: " + line.voice, this);
            commander?.SetDialogueSpeaking(line.speaker == "Chỉ huy" && !line.radio);
        }
        void Update()
        {
            if (!IsPlaying || mission.Paused || ForestMenu.Visible) return;
            elapsed += Time.deltaTime; turnElapsed += Time.deltaTime; cameraElapsed += Time.deltaTime;
            var keyboard = Keyboard.current;
            escapeHeld = keyboard != null && keyboard.escapeKey.isPressed ? escapeHeld + Time.unscaledDeltaTime : 0;
            if (escapeHeld >= 1) { Close(acceptOnSkip); return; }
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame && Time.frameCount > startedFrame && elapsed >= 1) {
                Advance(); return;
            }
            // Audio owns the pace. The last line still waits for a separate acceptance press.
            if (voicedLine && index + 1 < lines.Length && elapsed >= spokenSeconds + .3f && !voice.HasSpeech)
                Advance();
        }
        public void Advance()
        {
            if (!IsPlaying || elapsed < 1) return;
            if (index + 1 == lines.Length) {
                if (voicedLine && (voice.HasSpeech || elapsed < spokenSeconds)) return;
                Close(true);
            }
            else { index++; Present(); }
        }
        void LateUpdate()
        {
            if (!IsPlaying) return;
            mission.player.SetPositionAndRotation(position, Quaternion.Slerp(rotation, facing, Mathf.SmoothStep(0, 1, turnElapsed/.5f)));
            pose?.Sample(idle, elapsed, true);
            // A stationary conversation keeps the existing planted ankle height;
            // it does not need to load and scan the full skin-contact dataset.
            speakingActor.transform.position+=Vector3.up*Mathf.Clamp(supportY-Mathf.Min(leftFoot.position.y,rightFoot.position.y),-.2f,.2f);
            if(radioConversation)Map01ArmContact.Radio(speakingActor,Mathf.SmoothStep(0,1,turnElapsed/.7f));
            float blend=Mathf.SmoothStep(0,1,(!radioConversation?cameraElapsed:turnElapsed)/.65f);
            rig.SetCinematicView(Vector3.Lerp(cameraPosition,conversationCamera,blend),Quaternion.Slerp(cameraRotation,conversationRotation,blend),Mathf.Lerp(fov,conversationFov,blend),1);
        }
        void FrameBriefingSpeaker()
        {
            if(Current.speaker=="Chỉ huy"&&TryFrameCommanderWithBoard())return;
            var resting=mission.hung!=null?mission.hung.GetComponentInChildren<Map01HungRest>():null;
            if(Current.speaker=="Hùng"&&resting!=null&&resting.IsResting){
                var target=resting.DialogueFocus;var bed=resting.Cot;float bestClear=-1;conversationCamera=target+bed.right*1.05f+Vector3.up*.8f-bed.forward*.75f;
                foreach(float side in new[]{1f,-1f}){
                    var candidate=target+bed.right*side*1.05f+Vector3.up*.8f-bed.forward*.75f;
                    var ray=candidate-target;float clear=ray.magnitude;
                    foreach(var hit in Physics.SphereCastAll(target,.10f,ray.normalized,ray.magnitude,mission.ObstructionMask,QueryTriggerInteraction.Ignore))
                        if(hit.transform.GetComponentInParent<Health>()==null)clear=Mathf.Min(clear,hit.distance-.12f);
                    if(clear<.6f||clear<=bestClear)continue;bestClear=clear;conversationCamera=target+ray.normalized*clear;
                }
                conversationRotation=Quaternion.LookRotation(target-conversationCamera);conversationFov=52;return;
            }
            bool namSpeaking=Current.speaker=="Nam";
            var forward=facing*Vector3.forward;var right=facing*Vector3.right;
            var subject=Current.speaker=="Hùng"&&mission.hung!=null?mission.hung.position:namSpeaking?position:commander.CommanderTransform.position;
            var focus=subject+Vector3.up*1.42f;
            float best=-1;
            foreach(float side in new[]{1f,-1f}) {
                var candidate=Current.speaker=="Hùng"?subject-forward*1.4f+right*(side*1.6f)+Vector3.up*1.62f:position+forward*(namSpeaking?1.05f:1.1f)+right*(side*1.5f)+Vector3.up*1.64f;
                var delta=candidate-focus;float clear=delta.magnitude;
                foreach(var hit in Physics.SphereCastAll(focus,.12f,delta.normalized,delta.magnitude,mission.ObstructionMask,QueryTriggerInteraction.Ignore))
                    if(hit.transform.GetComponentInParent<Health>()==null)clear=Mathf.Min(clear,hit.distance-.15f);
                if(clear<.8f||clear<=best)continue;
                best=clear;conversationCamera=focus+delta.normalized*clear;
            }
            conversationFov=48;
            conversationRotation=Quaternion.LookRotation(focus-conversationCamera);
        }
        bool TryFrameCommanderWithBoard()
        {
            var post=mission.alliedCommanderPost;
            if(post==null||commander==null||commander.CommanderTransform==null||
                Vector3.Distance(commander.CommanderTransform.position,post.position)>.5f)return false;
            var board=post.parent!=null?post.parent.Find("MissionBoard"):null;
            if(board==null)return false;
            // Look diagonally into the room's back wall: officer in the left foreground,
            // mission board in the right background. Other speakers retain their existing shot.
            var subject=commander.CommanderTransform.position+Vector3.up*1.42f;
            var boardFocus=board.position+Vector3.up*1.7f-board.forward*.08f;
            var focus=Vector3.Lerp(subject,boardFocus,.42f);
            const float shotFov=52;
            float distance=Mathf.Max(2.9f,1.85f/(Mathf.Tan(shotFov*.5f*Mathf.Deg2Rad)*Mathf.Max(.7f,mission.gameCamera.aspect)));
            var view=(-board.right*.65f-board.forward*.76f).normalized;
            var candidate=focus+view*distance+Vector3.up*.1f-board.right*.65f;
            var shiftedLeft=candidate;
            // Leave room for Nam's shoulders/backpack when he approaches from the left.
            bool NamBlocks(Vector3 eye,Vector3 target)
            {
                var ray=target-eye;
                foreach(var height in new[]{1.05f,1.55f})
                {
                    var centre=mission.player.position+Vector3.up*height;
                    float t=Vector3.Dot(centre-eye,ray)/ray.sqrMagnitude;
                    if(t<=0||t>=1)continue;
                    float radius=height<1.5f?.55f:.4f;
                    if((centre-(eye+ray*t)).sqrMagnitude<radius*radius)return true;
                }
                return false;
            }
            for(int i=0;i<4&&(NamBlocks(candidate,subject)||NamBlocks(candidate,boardFocus));i++)candidate-=board.right*.25f;
            if(NamBlocks(candidate,subject)||NamBlocks(candidate,boardFocus))
                for(int i=1;i<=6;i++)
                {
                    var nearer=shiftedLeft+board.forward*(i*.25f);
                    if(NamBlocks(nearer,subject)||NamBlocks(nearer,boardFocus))continue;
                    candidate=nearer;break;
                }
            var delta=candidate-focus;float clear=delta.magnitude;
            foreach(var hit in Physics.SphereCastAll(focus,.12f,delta.normalized,delta.magnitude,mission.ObstructionMask,QueryTriggerInteraction.Ignore))
                if(hit.transform.GetComponentInParent<Health>()==null)clear=Mathf.Min(clear,hit.distance-.15f);
            if(clear<1.7f)return false;
            conversationCamera=focus+delta.normalized*clear;
            conversationRotation=Quaternion.LookRotation(focus-conversationCamera);conversationFov=shotFov;
            return true;
        }
        void FrameRadioNam()
        {
            var focus=position+Vector3.up*1.42f;float best=0;
            foreach(float side in new[]{1f,-1f})foreach(float front in new[]{.35f,-.35f}){
                var candidate=position+mission.player.right*side*.95f+mission.player.forward*front+Vector3.up*1.58f;
                var delta=candidate-focus;float clear=delta.magnitude;
                foreach(var hit in Physics.SphereCastAll(focus,.1f,delta.normalized,delta.magnitude,mission.ObstructionMask,QueryTriggerInteraction.Ignore))
                    if(hit.transform.GetComponentInParent<Health>()==null)clear=Mathf.Min(clear,hit.distance-.12f);
                if(clear<.65f||clear<=best)continue;
                best=clear;conversationCamera=focus+delta.normalized*clear;conversationRotation=Quaternion.LookRotation(focus-conversationCamera);conversationFov=58;
            }
        }
        void FrameCommander()
        {
            var forward=facing*Vector3.forward;var right=facing*Vector3.right;
            var focus=(position+commander.CommanderTransform.position)*.5f+Vector3.up*(closeBriefing?1.4f:1.3f);
            if(closeBriefing) { focus+=forward*.25f; conversationFov=48; }
            float best=-1;
            foreach(float side in new[]{1f,-1f}) {
                var candidate=closeBriefing
                    ? position-forward*.65f+right*(side*1.2f)+Vector3.up*1.6f
                    : position-forward*1.6f+right*(side*1.8f)+Vector3.up*1.7f;
                var delta=candidate-focus;float clear=delta.magnitude;
                foreach(var hit in Physics.SphereCastAll(focus,.14f,delta.normalized,delta.magnitude,mission.ObstructionMask,QueryTriggerInteraction.Ignore))
                    if(hit.transform.GetComponentInParent<Health>()==null)clear=Mathf.Min(clear,hit.distance-.15f);
                if(clear<.8f||clear<=best)continue;
                best=clear;conversationCamera=focus+delta.normalized*clear;
            }
            // The starting two-shot is followed by a soft cut to each local speaker.
            conversationRotation=Quaternion.LookRotation(focus-conversationCamera);
        }
        public void Close(bool confirm)
        {
            if (!IsPlaying) return;
            var commit = accepted;
            lines = null; accepted = null;
            voice?.Stop(); commander?.SetDialogueSpeaking(false);
            if(turnedCommander&&commander!=null&&commander.CommanderTransform!=null)
                commander.CommanderTransform.rotation=commanderFacing;
            turnedCommander=false;
            if (mission != null) mission.Say(null, 0);
            if (voice != null) voice.CanSpeak = previousVoiceGate;
            previousVoiceGate = null;
            pose?.Dispose(); pose = null;
            foreach (var prop in hidden) if (prop != null) prop.SetActive(true);
            hidden.Clear();
            if (mission != null && mission.ModernPlayer != null)
            {
                mission.ModernPlayer.enabled = playerEnabled;
                mission.ModernCombat.enabled = combatEnabled;
                if (actions != null) actions.enabled = actionsEnabled;
                mission.ModernHealth.CinematicInvulnerable = invulnerable;
                mission.ModernPlayer.RestoreMotion(mission.Crouched);
                mission.ModernCombat.RestoreAfterCinematic();
                rig.ClearCinematicView(); mission.Cinematic = false;
                ForestMenu.SuppressKeysAfterCutscene();
                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            }
            if (confirm) commit?.Invoke();
        }
        void OnDisable() { if (IsPlaying) Close(false); }
        void OnGUI()
        {
            if (!IsPlaying || ForestMenu.Visible || mission.Paused) return;
            var previousMatrix=GUI.matrix;
            float scale=Mathf.Max(.65f,Mathf.Min(Screen.width/1600f,Screen.height/900f));
            float canvasWidth=Screen.width/scale,canvasHeight=Screen.height/scale;
            GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            caption ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, wordWrap = true, fontSize = 18 };
            heading ??= new GUIStyle(caption) { fontStyle = FontStyle.Bold, fontSize = 18 };
            hint ??= new GUIStyle(caption) { fontSize = 14, alignment = TextAnchor.MiddleRight };
            caption.fontSize=28; heading.fontSize=26; hint.fontSize=20;
            float width=Mathf.Min(1240,canvasWidth-64);
            float headingHeight=32, hintHeight=27;
            float contentTop=headingHeight+10;
            float textHeight=caption.CalcHeight(new GUIContent(Current.text),width-32);
            float height=Mathf.Ceil(contentTop+textHeight+hintHeight+14);
            var area=new Rect((canvasWidth-width)/2,canvasHeight-height-16,width,height);
            var old = GUI.color; GUI.color = new Color(.025f, .04f, .03f, .93f); GUI.DrawTexture(area, Texture2D.whiteTexture); GUI.color = old;
            GUI.Label(new Rect(area.x+16,area.y+4,width-32,headingHeight+2), Current.speaker + (Current.radio ? " · BỘ ĐÀM" : ""), heading);
            GUI.Label(new Rect(area.x+16,area.y+contentTop,width-32,textHeight), Current.text, caption);
            string action = index + 1 == lines.Length
                ? (voicedLine && voice.HasSpeech ? "Đang nghe chỉ thị…" : "E: " + finalLabel)
                : (voicedLine ? "Tự chuyển sau lời thoại  ·  E: Bỏ qua câu" : "E: Tiếp tục");
            GUI.Label(new Rect(area.x+16,area.yMax-hintHeight-5,width-32,hintHeight), $"{index + 1}/{lines.Length}  ·  {action}  ·  Giữ Esc: Đóng", hint);
            GUI.matrix=previousMatrix;
        }
    }
}
