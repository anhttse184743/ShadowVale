using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    /// <summary>
    /// T047 review shots (director 26/11 "chụp ảnh phần đó trong game"): the game's own Hùng and enemy-commander prefabs,
    /// posed by their game clips, with the poses that load the fixed areas — Hùng's belt at the longest stride and the
    /// deepest crouch, his left forearm at its strongest twist, the commander's ankles standing / striding / crouching.
    /// Renders only; changes no asset.
    /// </summary>
    public static class T047AnhTrongGame
    {
        const string Out = "Docs/Reviews/T047_TrongSo_20261126";
        const string HungPrefab = "Assets/_Project/Art/Map02DayOne/hung.prefab";

        [MenuItem("ShadowVale/T047/Chup anh trong game")]
        public static void Run()
        {
            var log = new List<string>(); bool ok = true;
            var scene = EditorSceneManager.NewPreviewScene(); var temp = new List<Object>(); var previous = RenderTexture.active;
            try
            {
                Directory.CreateDirectory(Out);
                var root = new GameObject("T047 stage"); SceneManager.MoveGameObjectToScene(root, scene);
                Light(root.transform, new Vector3(42, -35, 0), 1.7f, Color.white);
                Light(root.transform, new Vector3(20, 150, 0), .9f, new Color(.75f, .85f, 1));
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.transform.SetParent(root.transform, false);
                var fm = new Material(Shader.Find("Universal Render Pipeline/Lit")); fm.SetColor("_BaseColor", new Color(.42f, .4f, .36f)); fm.SetFloat("_Smoothness", .1f); temp.Add(fm);
                floor.GetComponent<Renderer>().sharedMaterial = fm;
                var cam = new GameObject("T047 camera").AddComponent<Camera>(); cam.transform.SetParent(root.transform, false); cam.scene = scene;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.62f, .68f, .74f); cam.fieldOfView = 30; cam.nearClipPlane = .03f; cam.farClipPlane = 40;

                // Hùng
                var hung = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HungPrefab), root.transform);
                var ha = hung.GetComponentInChildren<Animator>();
                var hClips = ha.runtimeAnimatorController.animationClips.Distinct().ToArray();
                var twist = hung.GetComponentInChildren<ForearmTwist>(true);
                var lTwist = ha.GetComponentsInChildren<Transform>(true).First(t => t.name == "LeftForeArmTwist_V104");
                var lRest = lTwist.localRotation;
                log.Add("Hung clips: " + string.Join(", ", hClips.Select(c => $"{c.name} {c.length:F2}s")));
                using (var pose = new Map01CinematicPose(ha, null, false))
                {
                    var frames = Frames(hClips).Select(f => { pose.Sample(f.c, f.t); if (twist) twist.Apply(); return (f.c, f.t, stride: Stride(ha), hips: Hips(ha), roll: Quaternion.Angle(lTwist.localRotation, lRest)); }).ToList();
                    var idle = frames.FirstOrDefault(f => f.c.name.ToLowerInvariant().Contains("idle")); if (idle.c == null) idle = frames.OrderBy(f => f.stride).First();
                    var walk = frames.OrderByDescending(f => f.stride).First();
                    var crouchClips = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Art/Characters/Animations/Controllers/AC_Player.controller")
                        .animationClips.Distinct().Where(c => { var n = c.name.ToLowerInvariant(); return (n.Contains("crouch") || n.Contains("sneak")) && !n.Contains("rifle") && !n.Contains("pistol") && !n.Contains("knife"); }).ToArray();
                    log.Add("Hung crouch candidates: " + string.Join(", ", crouchClips.Select(c => c.name)));
                    var low = Frames(crouchClips).Select(f => { pose.Sample(f.c, f.t); if (twist) twist.Apply(); return (f.c, f.t, stride: Stride(ha), hips: Hips(ha), roll: 0f); }).OrderBy(f => f.hips).FirstOrDefault();
                    if (low.c == null) low = frames.OrderBy(f => f.hips).First();
                    var turn = frames.OrderByDescending(f => f.roll).First();
                    log.Add($"Hung idle {idle.c.name}@{idle.t:F2}; stride {walk.c.name}@{walk.t:F2} {walk.stride:F0} deg; low {low.c.name}@{low.t:F2} hips {low.hips:F2} m; left twist {turn.c.name}@{turn.t:F2} {turn.roll:F1} deg");
                    var f_ = ha.transform.forward; var r_ = ha.transform.right; var o = ha.transform.position;
                    Pose(pose, twist, idle); Shot(cam, ha, root, o + f_ * 3.4f + Vector3.up * 1.0f, o + Vector3.up * .9f, "01_hung_dung_truoc.png", temp);
                    Pose(pose, twist, walk); var hp = Hip(ha);
                    Shot(cam, ha, root, o + r_ * 3.4f + Vector3.up * 1.0f, o + Vector3.up * .9f, "02_hung_buoc_dai_nghieng.png", temp);
                    Shot(cam, ha, root, hp + (f_ * .8f + r_ * .6f).normalized * 1.25f + Vector3.up * .12f, hp, "03_hung_that_lung_buoc_truoc.png", temp);
                    Shot(cam, ha, root, hp + (-f_ * .7f - r_ * .7f).normalized * 1.25f + Vector3.up * .12f, hp, "04_hung_that_lung_buoc_sau.png", temp);
                    Pose(pose, twist, low); hp = Hip(ha);
                    Shot(cam, ha, root, hp + (f_ * .5f + r_ * .85f).normalized * 1.35f + Vector3.up * .15f, hp, "05_hung_that_lung_ngoi.png", temp);
                    Pose(pose, twist, turn);
                    var el = ha.GetBoneTransform(HumanBodyBones.LeftLowerArm).position; var hd = ha.GetBoneTransform(HumanBodyBones.LeftHand).position; var mid = Vector3.Lerp(el, hd, .5f);
                    Shot(cam, ha, root, mid - r_ * .6f + f_ * .25f + Vector3.up * .12f, mid, "06_hung_cang_tay_trai_ngoai.png", temp);
                    Shot(cam, ha, root, mid + f_ * .65f + Vector3.up * .05f, mid, "07_hung_cang_tay_trai_truoc.png", temp);
                }
                Object.DestroyImmediate(hung);

                // Enemy commander
                var chi = Object.Instantiate(Resources.Load<GameObject>("Characters/EnemyCommanderVisual"), root.transform);
                var ca = chi.GetComponentInChildren<Animator>();
                RuntimeAnimatorController ctl = Map01ActorAssets.Load().enemyController;
                try { ctl = Map01EnemyCommanderRig.Controller(ctl) ?? ctl; } catch (Exception e) { log.Add("commander rig controller: " + e.Message); }
                var cClips = ctl.animationClips.Distinct().ToArray();
                log.Add("ChiHuy clips: " + string.Join(", ", cClips.Select(c => $"{c.name} {c.length:F2}s")));
                using (var pose = new Map01CinematicPose(ca, null, false))
                {
                    var frames = Frames(cClips).Select(f => { pose.Sample(f.c, f.t); return (f.c, f.t, stride: Stride(ca), hips: Hips(ca), roll: 0f); }).ToList();
                    var idle = frames.FirstOrDefault(f => f.c.name.ToLowerInvariant().Contains("idle")); if (idle.c == null) idle = frames.OrderBy(f => f.stride).First();
                    var walk = frames.Where(f => f.c.name.Contains("Walk") || f.c.name.Contains("Run")).OrderByDescending(f => f.stride).First();
                    var low = frames.Where(f => f.c.name.Contains("Walk")).OrderByDescending(f => f.stride).First();
                    log.Add($"ChiHuy idle {idle.c.name}@{idle.t:F2}; stride {walk.c.name}@{walk.t:F2} {walk.stride:F0} deg; low {low.c.name}@{low.t:F2} hips {low.hips:F2} m");
                    var f_ = ca.transform.forward; var r_ = ca.transform.right; var o = ca.transform.position;
                    Pose(pose, null, idle); Shot(cam, ca, root, o + f_ * 3.4f + Vector3.up * 1.0f, o + Vector3.up * .9f, "08_chihuy_dung_truoc.png", temp);
                    var ft = Feet(ca); log.Add($"ChiHuy idle ankle heights L {ca.GetBoneTransform(HumanBodyBones.LeftFoot).position.y - o.y:F3} R {ca.GetBoneTransform(HumanBodyBones.RightFoot).position.y - o.y:F3} m");
                    Shot(cam, ca, root, ft + r_ * 1.05f + Vector3.up * .12f, ft, "09_chihuy_chan_dung_nghieng.png", temp);
                    Shot(cam, ca, root, ft + (f_ * .8f + r_ * .55f).normalized * 1.05f + Vector3.up * .2f, ft, "10_chihuy_chan_dung_cheo.png", temp);
                    Pose(pose, null, walk); ft = PlantedFoot(ca);
                    Shot(cam, ca, root, ft + r_ * 1.0f + Vector3.up * .12f, ft, "11_chihuy_chan_chay.png", temp);
                    Pose(pose, null, low); ft = PlantedFoot(ca);
                    Shot(cam, ca, root, ft + (r_ * .8f - f_ * .6f).normalized * 1.0f + Vector3.up * .15f, ft, "12_chihuy_chan_di.png", temp);
                }
            }
            catch (Exception e) { log.Add(e.ToString()); ok = false; }
            finally
            {
                RenderTexture.active = previous; EditorSceneManager.ClosePreviewScene(scene);
                foreach (var t in temp) if (t) Object.DestroyImmediate(t);
            }
            log.Add(ok ? "T047 ANH OK" : "T047 ANH LOI");
            Directory.CreateDirectory(Out); File.WriteAllLines(Out + "/log.txt", log); Debug.Log(string.Join("\n", log));
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static IEnumerable<(AnimationClip c, float t)> Frames(AnimationClip[] clips)
        {
            foreach (var c in clips) for (int i = 0; i < 20; i++) yield return (c, c.length * i / 20f);
        }

        static void Pose(Map01CinematicPose pose, ForearmTwist twist, (AnimationClip c, float t, float stride, float hips, float roll) f)
        { pose.Sample(f.c, f.t); if (twist) twist.Apply(); }

        static float Stride(Animator a)
        {
            Vector3 D(HumanBodyBones u, HumanBodyBones l) => a.GetBoneTransform(l).position - a.GetBoneTransform(u).position;
            return Vector3.Angle(D(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg), D(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg));
        }
        static float Hips(Animator a) => a.GetBoneTransform(HumanBodyBones.Hips).position.y - a.transform.position.y;
        static Vector3 Hip(Animator a) => a.GetBoneTransform(HumanBodyBones.Hips).position - Vector3.up * .06f;
        static Vector3 Feet(Animator a)
        {
            var p = (a.GetBoneTransform(HumanBodyBones.LeftFoot).position + a.GetBoneTransform(HumanBodyBones.RightFoot).position) / 2;
            p.y = a.transform.position.y + .1f; return p;
        }

        static Vector3 PlantedFoot(Animator a)
        {
            var l = a.GetBoneTransform(HumanBodyBones.LeftFoot).position; var r = a.GetBoneTransform(HumanBodyBones.RightFoot).position;
            var p = l.y < r.y ? l : r; p.y = Mathf.Max(a.transform.position.y + .08f, p.y - .03f); return p;
        }

        static void Light(Transform root, Vector3 angle, float intensity, Color color)
        { var l = new GameObject("T047 light").AddComponent<Light>(); l.transform.SetParent(root, false); l.transform.rotation = Quaternion.Euler(angle); l.type = LightType.Directional; l.intensity = intensity; l.color = color; l.shadows = LightShadows.Soft; }

        /// <summary>Bakes the current skinned pose (editor skinning may show a cached frame) and renders one 900² image.</summary>
        static void Shot(Camera cam, Animator a, GameObject root, Vector3 from, Vector3 at, string file, List<Object> temp)
        {
            var skins = a.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s => s.enabled && s.gameObject.activeInHierarchy).ToArray();
            var made = new List<GameObject>();
            foreach (var s in skins)
            {
                var m = new Mesh(); s.BakeMesh(m, true); temp.Add(m);
                var g = new GameObject("baked " + s.name); g.transform.SetParent(root.transform, false); g.transform.SetPositionAndRotation(s.transform.position, s.transform.rotation);
                g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterials = s.sharedMaterials; s.enabled = false; made.Add(g);
            }
            var rt = new RenderTexture(900, 900, 24) { antiAliasing = 4 }; var img = new Texture2D(900, 900, TextureFormat.RGB24, false);
            try
            {
                cam.transform.position = from; cam.transform.LookAt(at); cam.targetTexture = rt; cam.Render(); cam.Render();
                RenderTexture.active = rt; img.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); img.Apply(); File.WriteAllBytes(Out + "/" + file, img.EncodeToPNG());
            }
            finally
            {
                cam.targetTexture = null; RenderTexture.active = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(img);
                foreach (var g in made) Object.DestroyImmediate(g);
                foreach (var s in skins) s.enabled = true;
            }
        }
    }
}
