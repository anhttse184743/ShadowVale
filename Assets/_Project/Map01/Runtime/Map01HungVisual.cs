using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>Visual-only replacement. Existing rescue, navigation and save identity stay on Hung.</summary>
    public sealed class Map01HungVisual : MonoBehaviour
    {
        public Animator actor;
        private Map01Mission mission;
        private Map01Rescue rescue;
        private NavMeshAgent agent;
        private Renderer[] placeholder;
        private bool down;
        private float talkUntil;
        private Mesh soleMesh;
        private readonly System.Collections.Generic.List<Vector3> soleVertices = new System.Collections.Generic.List<Vector3>();
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
            visual.agent = owner.hung.GetComponent<NavMeshAgent>();
            visual.placeholder = old;
            foreach (var renderer in old) if (renderer != null && renderer.enabled) renderer.forceRenderingOff = true;
            visual.actor.applyRootMotion = false;
            visual.ResetPose();
        }

        private void LateUpdate()
        {
            if(mission==null || down || mission.Cinematic) return;
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

        private void Update()
        {
            if (mission == null || actor == null) return;
            bool fallen = rescue != null && rescue.HungDown;
            if (fallen != down) {
                down = fallen;
                if (down) { talkUntil = 0; actor.SetBool("Dead", true); }
                else {
                    actor.Rebind(); actor.Update(0);
                    ResetPose();
                }
            }
            float speed = !down && agent != null && agent.isOnNavMesh && !agent.isStopped
                ? agent.velocity.magnitude : 0;
            actor.SetFloat("Speed", speed, .12f, Time.deltaTime);
            if (speed > .15f) talkUntil = 0;
            actor.SetBool("Talking", !down && Time.time < talkUntil);
        }

        private void OnDestroy()
        {
            if (soleMesh != null) Destroy(soleMesh);
            if (placeholder == null) return;
            foreach (var renderer in placeholder) if (renderer != null) renderer.forceRenderingOff = false;
        }
    }
}
