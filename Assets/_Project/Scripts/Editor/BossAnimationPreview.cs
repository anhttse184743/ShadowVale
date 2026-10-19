using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ShadowVale.Map01;

namespace ShadowVale.Editor
{
    public sealed class BossAnimationPreview : EditorWindow
    {
        PreviewRenderUtility preview;
        GameObject model;
        Map01CinematicPose pose;
        AnimationClip[] clips;
        int selected;
        double started;
        bool playing = true, cycle = true;
        Vector2 scroll;
        bool captureNext;
        Map01EnemyGrip previewGrip;
        [InitializeOnLoadMethod]
        static void Initialize() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
        static void Poll()
        {
            const string request = "Tools/BossAnimationPreview.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var command = File.ReadAllText(request).Trim();
            File.Delete(request);
            Open();
            var window = GetWindow<BossAnimationPreview>();
            if(command == "lifecycle")
            {
                var errors=new System.Collections.Generic.List<string>();
                Application.LogCallback collect=(message,trace,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);};
                Application.logMessageReceived+=collect;
                try { for(int i=0;i<3;i++) { window.OnDisable();window.OnEnable(); } }
                finally { Application.logMessageReceived-=collect; }
                File.WriteAllText("Tools/BossPreviewLifecycle.result.txt",errors.Count==0?"PASS: 3 preview cleanup/reinitialization cycles, no errors.":string.Join("\n",errors));
            }
            if(command == "patrol") { Map01EnemyGrip.ResetPatrolCalibration();window.OnDisable();window.OnEnable(); }
            window.clips = window.clips.Select(c => c.name.Contains("Rifle_Fire") ? StoryAnimationSetup.Clip("", "Enemy_Rifle_Fire") : c.name.Contains("Rifle_AimIdle") ? StoryAnimationSetup.Clip("", "Enemy_Rifle_AimIdle") : c).ToArray();
            window.clips = window.clips.Select(c => c.name.StartsWith("Enemy_Patrol_Walk") ? StoryAnimationSetup.Clip("", "Enemy_Patrol_Walk") : c).ToArray();
            if(command == "patrol") { window.selected = Mathf.Max(0,System.Array.FindIndex(window.clips,c=>c.name.StartsWith("Enemy_Patrol_Walk")));window.cycle=false;window.started=EditorApplication.timeSinceStartup;window.captureNext=true; }
            window.playing=true;window.Repaint();
            File.WriteAllText("Tools/BossAnimationPreview.command.txt",command+" selected="+window.clips[window.selected].name+" capture="+window.captureNext);
            if(command=="patrol") EditorApplication.delayCall += window.CapturePatrol;
            if (command == "audit") window.CaptureAudit();
            if (command == "fire") { window.selected = Mathf.Max(0, System.Array.FindIndex(window.clips, c => c.name.Contains("Rifle_Fire"))); window.cycle = false; window.started = EditorApplication.timeSinceStartup; window.captureNext = true; }
        }
        [MenuItem("ShadowVale/Characters/Preview Boss Animations")]
        public static void Open()
        {
            var window = GetWindow<BossAnimationPreview>();
            window.titleContent = new GUIContent("Boss animations");
            window.minSize = new Vector2(850, 650);
            window.Show(); window.Focus();
        }
        void OnEnable()
        {
            clips = Directory.GetFiles("Assets/_Project/Art/Characters/Animations/Enemy", "*.fbx")
                .OrderBy(p => Path.GetFileName(p).StartsWith("Boss_") ? 0 : 1).ThenBy(p => p)
                .SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>())
                .Where(c => !c.name.StartsWith("__preview__"))
                .Select(c => c.name == "Enemy_Rifle_Fire" || c.name == "Enemy_Rifle_AimIdle" || c.name == "Enemy_Patrol_Walk" ? StoryAnimationSetup.Clip("", c.name) : c).ToArray();
            preview = new PreviewRenderUtility();
            model = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Map01/Resources/Characters/EnemyCommanderVisual.prefab"));
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            preview.AddSingleGO(model);
            model.GetComponent<Animator>().runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(EnemyAnimatorBuilder.ControllerPath);
            previewGrip = Map01EnemyGrip.BindPreview(model.GetComponent<Animator>());
            pose = new Map01CinematicPose(model.GetComponent<Animator>(), skinGrounding:false);
            preview.camera.transform.position = new Vector3(2.8f, 1.7f, 3.8f);
            preview.camera.transform.LookAt(new Vector3(0, .95f, 0));
            preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 30;
            preview.camera.fieldOfView = 30;
            preview.camera.backgroundColor = new Color(.10f, .12f, .15f);
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.lights[0].intensity = 1.4f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35, 35, 0);
            preview.lights[1].intensity = .8f;
            preview.ambientColor = new Color(.65f, .65f, .65f);
            preview.lights[0].intensity = 3;
            preview.lights[1].intensity = 2;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Animate;
            File.WriteAllText("Tools/BossAnimationPreview.result.txt", "OPEN: " + clips.Length + " clips\n" + string.Join("\n", clips.Select(c => c.name + " | " + c.length.ToString("F2") + "s | humanoid=" + c.humanMotion)));
        }
        void Animate() { if (playing) Repaint(); }
        void CapturePatrol()
        {
            var clip=StoryAnimationSetup.Clip("","Enemy_Patrol_Walk");
            var actor=model.GetComponent<Animator>();
            var audit=new System.Text.StringBuilder();
            var auditRifle=model.GetComponent<Map01Rifle>();
            if(auditRifle!=null&&auditRifle.weapon!=null)
                foreach(var renderer in auditRifle.weapon.GetComponentsInChildren<Renderer>()) audit.AppendLine(renderer.name+" localCentre="+auditRifle.weapon.transform.InverseTransformPoint(renderer.bounds.center));
            for(int i=0;i<=60;i++)
            {
                pose.Sample(clip,clip.length*i/60f,true);
                if(i%10!=0)continue;
                audit.Append("t="+(i/60f).ToString("F2")+" palmError="+(previewGrip!=null?previewGrip.PalmError:0).ToString("F4"));
                foreach(bool left in new[]{false,true})
                {
                    var hand=actor.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                    var lower=actor.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
                    var end=hand.Cast<Transform>().First();
                    var upper=actor.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
                    audit.Append(" "+(left?"Left":"Right")+" wrist="+Vector3.Angle(hand.position-lower.position,end.position-hand.position).ToString("F2"));
                    audit.Append(" hand="+hand.position);
                    audit.Append(" elbowBend="+Vector3.Angle(lower.position-upper.position,hand.position-lower.position).ToString("F1"));
                }
                if(auditRifle!=null&&auditRifle.weapon!=null&&auditRifle.weapon.Muzzle!=null) audit.Append(" muzzleForward="+auditRifle.weapon.Muzzle.forward+" muzzlePos="+auditRifle.weapon.Muzzle.position);
                audit.AppendLine();
            }
            File.WriteAllText("Tools/BossPose/PatrolFullAudit.txt",audit.ToString());
            float maxContact=0,maxWrist=0;
            for(int i=0;i<=60;i++)
            {
                pose.Sample(clip,clip.length*i/60f,true);previewGrip?.Apply(true);
                maxContact=Mathf.Max(maxContact,previewGrip!=null?previewGrip.PalmError:0);
                foreach(bool left in new[]{false,true})
                {
                    var h=actor.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                    var e=actor.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
                    maxWrist=Mathf.Max(maxWrist,Vector3.Angle(h.position-e.position,h.GetChild(0).position-h.position));
                }
            }
            File.WriteAllText("Tools/BossPose/PatrolGripValidation.txt","61 runtime-style samples: worst palm gap="+maxContact.ToString("F5")+"m; worst wrist angle="+maxWrist.ToString("F2")+" degrees");
            pose.Sample(clip,.25f,true);
            var rifle=model.GetComponent<Map01Rifle>();if(rifle!=null&&rifle.weapon!=null)rifle.weapon.gameObject.SetActive(false);
            preview.BeginPreview(new Rect(0,0,650,750),GUIStyle.none);preview.Render(true);
            var tex=preview.EndPreview();var old=RenderTexture.active;RenderTexture.active=tex as RenderTexture;
            var png=new Texture2D(tex.width,tex.height,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,tex.width,tex.height),0,0);png.Apply();
            File.WriteAllBytes("Tools/BossPose/PatrolPreview.png",png.EncodeToPNG());RenderTexture.active=old;DestroyImmediate(png);
            File.WriteAllText("Tools/BossPose/PatrolPreview.txt","clip="+AssetDatabase.GetAssetPath(clip)+" grip="+(previewGrip!=null&&previewGrip.Calibrated)+" palmError="+(previewGrip!=null?previewGrip.PalmError:0));
            var originalPosition=preview.camera.transform.position;var originalRotation=preview.camera.transform.rotation;
            foreach(bool side in new[]{false,true})
            {
                preview.camera.transform.position=side?new Vector3(4,1.3f,0):new Vector3(0,1.3f,4.5f);
                preview.camera.transform.LookAt(new Vector3(0,1,0));
                preview.BeginPreview(new Rect(0,0,650,750),GUIStyle.none);preview.Render(true);
                var view=preview.EndPreview();var active=RenderTexture.active;RenderTexture.active=view as RenderTexture;
                var image=new Texture2D(view.width,view.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,view.width,view.height),0,0);image.Apply();
                File.WriteAllBytes("Tools/BossPose/Patrol"+(side?"Side":"Front")+".png",image.EncodeToPNG());RenderTexture.active=active;DestroyImmediate(image);
            }
            preview.camera.transform.SetPositionAndRotation(originalPosition,originalRotation);
        }
        void CaptureAudit()
        {
            Directory.CreateDirectory("Tools/BossPose");
            foreach (var name in new[] { "Enemy_Rifle_Fire", "Enemy_Rifle_AimIdle" })
            {
                var clip = clips.First(c => c.name.StartsWith(name) || c.name == name.Replace("Enemy_", "Nam_"));
                pose.Sample(clip, 0);
                preview.BeginPreview(new Rect(0, 0, 600, 700), GUIStyle.none);
                preview.Render(true);
                var texture = preview.EndPreview();
                var saved = RenderTexture.active; RenderTexture.active = texture as RenderTexture;
                var png = new Texture2D(600, 700, TextureFormat.RGB24, false);
                png.ReadPixels(new Rect(0, 0, 600, 700), 0, 0); png.Apply();
                File.WriteAllBytes("Tools/BossPose/" + name + ".png", png.EncodeToPNG());
                RenderTexture.active = saved; DestroyImmediate(png);
            }
        }
        void OnDisable()
        {
            EditorApplication.update -= Animate;
            pose?.Dispose(); pose = null;
            preview?.Cleanup(); preview = null;
        }
        void OnGUI()
        {
            if (clips == null || clips.Length == 0 || preview == null) return;
            selected = Mathf.Clamp(selected, 0, clips.Length - 1);
            EditorGUILayout.LabelField("CHỈ HUY MỸ — ANIMATION PREVIEW", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Boss_*: 4 clip riêng của boss. Enemy_*: animation lính dùng chung trong gameplay. Chọn clip bên trái hoặc để tự chuyển mỗi 6 giây.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            playing = GUILayout.Toggle(playing, "Play", "Button", GUILayout.Width(80));
            cycle = GUILayout.Toggle(cycle, "Auto cycle", "Button", GUILayout.Width(100));
            if (GUILayout.Button("Restart", GUILayout.Width(80))) started = EditorApplication.timeSinceStartup;
            EditorGUILayout.EndHorizontal();
            float elapsed = (float)(EditorApplication.timeSinceStartup - started);
            if (playing && cycle && elapsed >= Mathf.Max(6, clips[selected].length + 1))
            { selected = (selected + 1) % clips.Length; started = EditorApplication.timeSinceStartup; elapsed = 0; }
            EditorGUILayout.BeginHorizontal();
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Width(240));
            for (int i = 0; i < clips.Length; i++)
                if (GUILayout.Toggle(selected == i, clips[i].name, "Button", GUILayout.Height(28)) && selected != i)
                { selected = i; started = EditorApplication.timeSinceStartup; elapsed = 0; }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.BeginVertical();
            GUILayout.Label(clips[selected].name + " — " + clips[selected].length.ToString("F2") + " s", EditorStyles.boldLabel);
            var rect = GUILayoutUtility.GetRect(400, 550, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type == EventType.Repaint)
            {
                pose.Sample(clips[selected], elapsed, true);
                bool patrol = clips[selected].name.StartsWith("Enemy_Patrol_Walk");
                if(previewGrip != null) { var weapon = model.GetComponent<Map01Rifle>();if(weapon != null && weapon.weapon != null)weapon.weapon.gameObject.SetActive(false); }
                preview.BeginPreview(rect, GUIStyle.none);
                preview.Render(true);
                var rendered = preview.EndPreview();
                GUI.DrawTexture(rect, rendered, ScaleMode.StretchToFill, false);
                if (captureNext)
                {
                    captureNext = false;
                    var previous = RenderTexture.active; RenderTexture.active = rendered as RenderTexture;
                    var png = new Texture2D(rendered.width, rendered.height, TextureFormat.RGB24, false);
                    png.ReadPixels(new Rect(0, 0, rendered.width, rendered.height), 0, 0); png.Apply();
                    Directory.CreateDirectory("Tools/BossPose");
                    File.WriteAllBytes("Tools/BossPose/"+(patrol?"PatrolPreview":"FirePreview")+".png", png.EncodeToPNG());
                    RenderTexture.active = previous; DestroyImmediate(png);
                    var actor = model.GetComponent<Animator>();
                    File.WriteAllText("Tools/BossPose/FirePreview.txt", "Clip=" + AssetDatabase.GetAssetPath(clips[selected]) + "\nR=" + actor.GetBoneTransform(HumanBodyBones.RightHand).position + "\nL=" + actor.GetBoneTransform(HumanBodyBones.LeftHand).position + "\nHead=" + actor.GetBoneTransform(HumanBodyBones.Head).position);
                }
            }
            EditorGUILayout.EndVertical(); EditorGUILayout.EndHorizontal();
        }
    }
}
