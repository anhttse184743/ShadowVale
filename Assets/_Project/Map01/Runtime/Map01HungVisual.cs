using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Visual-only replacement. Existing rescue, navigation and save identity stay on Hung.
    /// Follows the quest: held captive at the jetty (kneeling, wrists roped behind his back), stands
    /// up when Nam frees him, limps home during the escort, then walks and talks normally at base.
    /// A shot that brings him down plays the hit-and-fall from whatever he was doing.
    /// </summary>
    public sealed class Map01HungVisual : MonoBehaviour
    {
        public const string CaptiveState = "BiTroi", StandUpState = "DungDay", WoundedState = "Wounded",
            LocomotionState = "Locomotion", TalkingState = "Talking", DieState = "Die";

        public Animator actor;
        [Tooltip("Hemp rope wound round his wrists while he is held captive.")]
        public Material ropeMaterial;
        private Map01Mission mission;
        private Map01Rescue rescue;
        private Map01Quest quest;
        private NavMeshAgent agent;
        private Renderer[] placeholder;
        private bool down;
        private float talkUntil;
        private int lastStage = -1;
        private bool standingUp;
        /// <summary>Ground speed (m/s) at which the Hung_DiKhapKhieng cycle plants its feet.</summary>
        public const float LimpSpeed = 1.8f;
        private GameObject rope;
        private Mesh soleMesh;
        private readonly System.Collections.Generic.List<Vector3> soleVertices = new System.Collections.Generic.List<Vector3>();

        /// <summary>True while the rope is shown on his wrists (held at the jetty).</summary>
        public bool RopeVisible => rope != null && rope.activeSelf;
        public void Speak(float seconds = 6) { if (!down) talkUntil = Time.time + seconds; }

        private void ResetPose()
        {
            talkUntil = 0;
            actor.SetBool("Dead", false); actor.SetBool("Talking", false); actor.SetFloat("Speed", 0);
        }

        public static void Attach(Map01Mission owner)
        {
            if (owner.hung == null || owner.hung.GetComponentInChildren<Map01HungVisual>() != null) return;
            var prefab = Resources.Load<Map01HungVisual>("Characters/HungVisual");
            if (prefab == null) return;
            var old = owner.hung.GetComponentsInChildren<Renderer>();
            var visual = Instantiate(prefab, owner.hung, false);
            if (visual.actor == null || !visual.actor.isHuman || !visual.actor.avatar.isValid) {
                Debug.LogError("Hung requires a valid Humanoid avatar.", visual);
                Destroy(visual.gameObject); return;
            }
            var calibration=Map01ActorAssets.Load();
            if(calibration!=null) {
                visual.transform.localScale=Vector3.one;
                float scale=calibration.namHeight/calibration.hungHeight;
                visual.actor.transform.localScale=Vector3.one*scale;
                visual.actor.transform.localPosition=Vector3.up*(-calibration.hungSole*scale);
            }
            visual.mission = owner;
            visual.rescue = owner.GetComponent<Map01Rescue>();
            visual.quest = owner.GetComponent<Map01Quest>();
            visual.agent = owner.hung.GetComponent<NavMeshAgent>();
            visual.placeholder = old;
            foreach (var renderer in old) if (renderer != null && renderer.enabled) renderer.forceRenderingOff = true;
            visual.actor.applyRootMotion = false;
            visual.ResetPose();
            visual.SnapToStage();
        }

        private void LateUpdate()
        {
            if (mission == null) return;
            if (!down && !mission.Cinematic) AnchorToGround();
            // The rope is tied once the kneeling pose has been evaluated (and grounded), so it sits on his real wrists.
            if (rope == null && Captive && ropeMaterial != null
                && actor.GetCurrentAnimatorStateInfo(0).IsName(CaptiveState))
                rope = TieRope();
            if (rope != null && rope.activeSelf) BindWrists();
        }

        private void AnchorToGround()
        {
            // Anchor the calibrated sole plane to actual terrain or the raised base floor.
            float ground=float.NegativeInfinity;
            foreach(var hit in Physics.RaycastAll(mission.hung.position+Vector3.up*2,Vector3.down,5,~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(mission.hung) && !hit.transform.IsChildOf(mission.player)
                    && hit.normal.y>.65f && hit.point.y<=mission.hung.position.y+.5f)
                    ground=Mathf.Max(ground,hit.point.y);
            if(float.IsNegativeInfinity(ground)) ground=mission.hung.position.y;
            transform.position=new Vector3(mission.hung.position.x,ground,mission.hung.position.z);
            // Humanoid retargeting changes the foot plane after the neutral scale calibration.
            // Measure the evaluated skin, without changing the skeleton or standing scale.
            if (soleMesh == null) soleMesh = new Mesh();
            float sole = float.PositiveInfinity;
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                skin.BakeMesh(soleMesh); soleMesh.GetVertices(soleVertices);
                foreach (var vertex in soleVertices) sole = Mathf.Min(sole, skin.transform.TransformPoint(vertex).y);
            }
            if (!float.IsInfinity(sole)) transform.position += Vector3.up * (ground - sole);
        }

        private int Stage => quest != null ? quest.Stage : Map01Quest.BriefingStage;
        private bool Captive => !down && Stage == Map01Quest.RescueStage;
        private bool Wounded => !down && Stage == Map01Quest.EscortStage;

        /// <summary>
        /// Jumps straight to the pose the current stage calls for, without playing the way there.
        /// Used at start-up, after a checkpoint restore and after a retry; only the real
        /// rescue (captive -> escort) is allowed to play the stand-up.
        /// </summary>
        private void SnapToStage()
        {
            lastStage = Stage;
            // A jump must not inherit the body offset of the pose it leaves: played straight from the
            // kneeling captive pose, the standing idle keeps his hips 0.7 m low until rebound.
            actor.Rebind(); ResetPose();
            actor.SetBool("Captive", Captive); actor.SetBool("Wounded", Wounded);
            actor.Play(Captive ? CaptiveState : Wounded ? WoundedState : LocomotionState, 0, 0);
            actor.Update(0);
            if (rope != null) rope.SetActive(Captive);
        }

        private void Update()
        {
            if (mission == null || actor == null) return;
            bool fallen = rescue != null && rescue.HungDown;
            if (fallen != down) {
                down = fallen;
                if (down) { ReleaseHold(); talkUntil = 0; actor.SetBool("Dead", true); if (rope != null) rope.SetActive(false); }
                else {
                    actor.Rebind(); actor.Update(0);
                    ResetPose(); SnapToStage();
                }
            }
            if (!down && Stage != lastStage) {
                bool freed = lastStage == Map01Quest.RescueStage && Stage == Map01Quest.EscortStage;
                if (freed) { lastStage = Stage; standingUp = true; if (rope != null) rope.SetActive(false); }
                else { ReleaseHold(); SnapToStage(); }
            }
            actor.SetBool("Captive", Captive);
            actor.SetBool("Wounded", Wounded);
            HoldWhileStandingUp();
            float speed = !down && !Captive && agent != null && agent.isOnNavMesh && !agent.isStopped
                ? agent.velocity.magnitude : 0;
            actor.SetFloat("Speed", speed, .12f, Time.deltaTime);
            // The limp is authored at its natural pace; if the escort hurries him, play it faster rather than slide.
            actor.SetFloat("LimpRate", Mathf.Max(1, speed / LimpSpeed));
            if (speed > .15f) talkUntil = 0;
            actor.SetBool("Talking", !down && !Captive && Time.time < talkUntil);
        }

        /// <summary>
        /// He cannot follow Nam while still getting off his knees: keep the agent parked until the
        /// stand-up has handed over to the limp, then let the escort take him.
        /// </summary>
        private void HoldWhileStandingUp()
        {
            if (!standingUp || agent == null || !agent.isOnNavMesh) return;
            var now = actor.GetCurrentAnimatorStateInfo(0);
            bool rising = now.IsName(CaptiveState) || now.IsName(StandUpState) || actor.IsInTransition(0);
            if (rising && !down) { agent.isStopped = true; return; }
            ReleaseHold();
        }

        /// <summary>Hands the agent back however the stand-up ended (finished, shot, or the quest moved on).</summary>
        private void ReleaseHold()
        {
            if (standingUp && agent != null && agent.isOnNavMesh) agent.isStopped = false;
            standingUp = false;
        }

        private GameObject TieRope()
        {
            if (actor.GetBoneTransform(HumanBodyBones.LeftHand) == null || actor.GetBoneTransform(HumanBodyBones.RightHand) == null) return null;
            var go = new GameObject("Hung rope");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = BuildRopeMesh();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ropeMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rope = go; BindWrists();
            return go;
        }

        /// <summary>Keeps the coils on his crossed wrists, wound along the line from one wrist to the other.</summary>
        private void BindWrists()
        {
            Vector3 left = actor.GetBoneTransform(HumanBodyBones.LeftHand).position;
            Vector3 right = actor.GetBoneTransform(HumanBodyBones.RightHand).position;
            Vector3 across = left - right;
            if (across.sqrMagnitude < 1e-6f) across = transform.right;
            Vector3 forward = Vector3.Cross(across.normalized, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = transform.forward;
            rope.transform.SetPositionAndRotation((left + right) * .5f,
                Quaternion.LookRotation(forward, Vector3.Cross(forward, across).normalized));
            rope.transform.localScale = Vector3.one / Mathf.Max(.01f, transform.lossyScale.x);   // mesh is in metres
        }

        /// <summary>Four coils round both wrists (axis = his left-right), a cinch between them and a loose end.</summary>
        private static Mesh BuildRopeMesh()
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            void Torus(Vector3 centre, float major, float minor, Quaternion axis, int segs = 16, int ring = 6)
            {
                int start = verts.Count;
                for (int i = 0; i < segs; i++) {
                    float a = i * Mathf.PI * 2 / segs;
                    var c = new Vector3(Mathf.Cos(a) * major, Mathf.Sin(a) * major, 0);
                    var outward = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                    for (int k = 0; k < ring; k++) {
                        float b = k * Mathf.PI * 2 / ring;
                        verts.Add(centre + axis * (c + outward * Mathf.Cos(b) * minor + Vector3.forward * Mathf.Sin(b) * minor));
                    }
                }
                for (int i = 0; i < segs; i++)
                    for (int k = 0; k < ring; k++) {
                        int a0 = start + i * ring + k, a1 = start + i * ring + (k + 1) % ring;
                        int b0 = start + (i + 1) % segs * ring + k, b1 = start + (i + 1) % segs * ring + (k + 1) % ring;
                        tris.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                    }
            }
            var acrossBody = Quaternion.LookRotation(Vector3.right, Vector3.up);      // coils wrap the forearms
            foreach (float dx in new[] { -.03f, -.01f, .01f, .03f })
                Torus(new Vector3(dx, 0, 0), .05f, .0065f, acrossBody);
            Torus(Vector3.zero, .032f, .006f, Quaternion.LookRotation(Vector3.up, Vector3.forward));   // cinch
            for (int i = 0; i < 6; i++)                                                                 // loose end
                Torus(new Vector3(.03f, -.04f - i * .022f, -.012f), .006f, .006f, Quaternion.LookRotation(Vector3.up, Vector3.forward), 6, 5);
            var mesh = new Mesh { name = "Hung rope" };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            if (soleMesh != null) Destroy(soleMesh);
            if (placeholder == null) return;
            foreach (var renderer in placeholder) if (renderer != null) renderer.forceRenderingOff = false;
        }
    }
}
