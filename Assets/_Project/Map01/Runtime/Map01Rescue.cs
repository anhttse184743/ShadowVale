using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The opening order. Hùng, a signals runner caught on his way home with the post, is held
    /// under the shelter at the north jetty by a squad of four ("patrol_…", placed there in
    /// Map 1.unity). Nam gets there however he likes — unseen is easiest, and creeping close lets
    /// him overhear the squad — then frees and treats Hùng and brings him home. Once the squad is
    /// fighting, Hùng is in the line of fire (Map01EnemyController shoots him too): he has his own
    /// health, and if he falls the rescue starts over from a way back down the road. Once he is
    /// home the jetty post is manned again, at full strength and calm.
    /// </summary>
    public sealed class Map01Rescue : MonoBehaviour
    {
        public static readonly string[] SquadNames = { "patrol_0", "patrol_1", "patrol_2", "rbl_patrol_3" };

        [Tooltip("How close Nam has to creep to overhear the squad.")]
        [SerializeField] private float overhearRange = 34f;
        [Tooltip("How far back down the road from the jetty a failed rescue restarts.")]
        [SerializeField] private float retryDistance = 45f;

        private static readonly string[] Chatter = {
            "Lính địch: Bắt được thằng lính thông tin của bọn nó rồi. Trong túi toàn thư từ, mật lệnh.",
            "Lính địch: Trói chặt vào! Sáng mai giải nó về đồn chỉ huy khai thác.",
            "Lính địch: Canh cho kỹ — đồng bọn nó thế nào cũng mò tới cứu.",
            "Lính địch: Để xổng tù binh ở cái bến này thì cả tổ ăn đòn.",
        };

        public IReadOnlyList<Map01EnemyController> Squad => squad;
        public float HungHealth { get; private set; }
        public float HungMaxHealth => mission.Settings.hungHP > 0 ? mission.Settings.hungHP : 150f;
        public bool HungDown => HungHealth <= 0;
        public float HungHitAt { get; private set; } = float.NegativeInfinity;
        /// <summary>Where Hùng is held, and where a failed rescue puts him back.</summary>
        public Vector3 CaptivePost { get; private set; }
        /// <summary>Where Nam restarts a failed rescue: a way back down the road to the jetty.</summary>
        public Vector3 RetryPoint { get; private set; }
        /// <summary>Hùng can be shot: held at the jetty, or following Nam home.</summary>
        public bool Exposed => mission.IsInitialized && !HungDown
            && (quest.Stage == Map01Quest.RescueStage || quest.Stage == Map01Quest.EscortStage);

        private readonly List<Map01EnemyController> squad = new List<Map01EnemyController>();
        private Map01Mission mission;
        private Map01Quest quest;
        private Map01Inventory inventory;
        private Quaternion captiveRotation;
        private int chatterIndex;
        private float nextChatter, nextHitLine;
        private bool whispered;

        private void Awake()
        {
            mission = GetComponent<Map01Mission>();
            quest = GetComponent<Map01Quest>();
            inventory = GetComponent<Map01Inventory>();
        }

        private void Start()
        {
            if (!mission.IsInitialized) return;
            // Before any checkpoint restore (Map01SaveSystem waits a moment): the scene's own
            // placement is the prisoner's post.
            CaptivePost = mission.hung.position; captiveRotation = mission.hung.rotation;
            HungHealth = HungMaxHealth;
            foreach (var name in SquadNames)
            {
                var go = GameObject.Find(name);
                if (go != null && go.TryGetComponent(out Map01EnemyController guard)) squad.Add(guard);
            }
            RetryPoint = FindRetryPoint(mission.player.position);
        }

        /// <summary>Back down the walking route from Nam's spawn to the jetty, retryDistance short of it.</summary>
        private Vector3 FindRetryPoint(Vector3 from)
        {
            var path = new NavMeshPath();
            if (!NavMesh.SamplePosition(from, out var start, 3, NavMesh.AllAreas) || !NavMesh.SamplePosition(CaptivePost, out var end, 3, NavMesh.AllAreas)
                || !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) || path.corners.Length < 2)
                return from;
            var corners = path.corners;
            for (int i = corners.Length - 1; i > 0; i--)
                if (Vector3.Distance(corners[i - 1], CaptivePost) >= retryDistance)
                {
                    // Along the leg that crosses the circle, at the circle.
                    Vector3 a = corners[i - 1], b = corners[i];
                    for (float t = 1; t >= 0; t -= .02f)
                    {
                        var p = Vector3.Lerp(a, b, t);
                        if (Vector3.Distance(p, CaptivePost) >= retryDistance)
                            return NavMesh.SamplePosition(p, out var hit, 2, NavMesh.AllAreas) ? hit.position : p;
                    }
                    return a;
                }
            return from;
        }

        private void Update()
        {
            if (!mission.IsInitialized || mission.Stopped || quest.Stage != Map01Quest.RescueStage) return;
            Overhear();
        }

        /// <summary>Creeping up unseen, Nam hears the squad talk about their prisoner — and, close
        /// enough, Hùng himself.</summary>
        private void Overhear()
        {
            if (Time.time < nextChatter || squad.Any(g => g.Alive && g.Engaged)) return;
            float distance = Vector3.Distance(mission.player.position, CaptivePost);
            if (distance > overhearRange) return;
            if (!whispered && distance < 12f)
            {
                whispered = true; nextChatter = Time.time + 8f;
                mission.Say("Hùng (thì thào): ...Nam? Chúng có bốn tên. Cẩn thận, đừng để chúng thấy.", 6);
                return;
            }
            if (!squad.Any(g => g.Alive)) return;
            mission.Say(Chatter[chatterIndex % Chatter.Length], 6);
            chatterIndex++;
            nextChatter = Time.time + (chatterIndex < Chatter.Length ? 9f : 25f);
        }

        /// <summary>A shot meant for the prisoner (or the man walking home beside Nam).</summary>
        public void HitHung(float damage)
        {
            if (!Exposed) return;
            HungHealth = Mathf.Max(0, HungHealth - damage);
            HungHitAt = Time.time;
            if (HungDown) { mission.Say("Hùng đã trúng đạn và hy sinh. Giải cứu thất bại.", 10); return; }
            if (Time.time < nextHitLine) return;
            nextHitLine = Time.time + 12f;
            mission.Say(quest.Stage == Map01Quest.RescueStage ? "Hùng: Chúng bắn tôi! Nam, hạ chúng nhanh lên!" : "Hùng: Tôi trúng đạn rồi... Nam, yểm hộ tôi!", 4);
        }

        /// <summary>
        /// [Enter] after Hùng has fallen: the rescue starts over. Hùng is back at his post and on
        /// his feet, the squad back at theirs and calm, and Nam a way back down the road — with a
        /// herb to treat him with, in case the first one went on him before he fell.
        /// </summary>
        public void Retry()
        {
            if (!HungDown) return;
            quest.RestoreStage(Map01Quest.RescueStage);
            HungHealth = HungMaxHealth; HungHitAt = float.NegativeInfinity;
            var agent = mission.hung.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh) { agent.ResetPath(); agent.Warp(CaptivePost); }
            else mission.hung.position = CaptivePost;
            mission.hung.rotation = captiveRotation;
            ResetSquad();
            var controller = mission.player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            mission.player.position = RetryPoint;
            if (controller != null) controller.enabled = true;
            if (inventory.Count("herb") <= 0) inventory.Add("herb", 1);
            mission.Alarmed = false;
            whispered = false; chatterIndex = 0; nextChatter = Time.time + 3f;
            mission.Say("Làm lại: Hùng vẫn bị giữ ở bến tàu. Lần này tiếp cận thật kín đáo — đừng để địch nổ súng.", 8);
        }

        /// <summary>Everyone in the jetty squad back at his post, alive and calm.</summary>
        public void ResetSquad()
        {
            foreach (var guard in squad) guard.ReturnToPost();
        }

        /// <summary>After a checkpoint load; a negative value (older saves) means unhurt.</summary>
        public void RestoreHealth(float value) => HungHealth = value < 0 ? HungMaxHealth : Mathf.Clamp(value, 1f, HungMaxHealth);
    }
}
