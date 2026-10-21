using System;
using ShadowVale.Gameplay.Audio;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;

namespace ShadowVale.Gameplay.Enemies
{
    /// <summary>Shared presentation for map-specific guards and reusable enemy agents.</summary>
    public sealed class EnemyFeedback : MonoBehaviour
    {
        public Func<bool> CanStep { get; set; }
        public Func<FootSurface> SurfaceProbe { get; set; }
        /// <summary>The enemy's own rifle report (a map that gives its guards their weapon's sound); null = the player's gunshot.</summary>
        public AudioClip GunshotClip { get; set; }
        private PlayerController player;
        private FootstepBank bank;
        private AudioSlice gunshot;
        private Health health;
        private Weapon weapon;
        private ShotTracer prefab;
        private readonly ShotTracer[] tracers = new ShotTracer[3];
        private int cursor;
        private Vector3 previous;
        private readonly StrideAccumulator stride = new();
        private readonly RaycastHit[] hits = new RaycastHit[64];

        public static EnemyFeedback Attach(GameObject actor, PlayerController listener = null)
        {
            var feedback = actor.GetComponent<EnemyFeedback>() ?? actor.AddComponent<EnemyFeedback>();
            if (listener != null) feedback.Bind(listener);
            return feedback;
        }

        private void Awake() { health = GetComponent<Health>(); previous = transform.position; }
        private void OnEnable() { previous = transform.position; stride.ResetFully(); }
        private void Start()
        {
            if (player == null) Bind(FindFirstObjectByType<PlayerController>());
        }

        public void Bind(PlayerController listener)
        {
            player = listener;
            if (player == null) return;
            bank = player.GetComponent<PlayerFootsteps>()?.Bank;
            gunshot = player.GetComponent<CombatAudio>()?.Gunshot ?? default;
            prefab = player.GetComponent<PlayerCombat>()?.TracerPrefab;
        }

        private void LateUpdate()
        {
            float distance = Vector3.ProjectOnPlane(transform.position - previous, Vector3.up).magnitude;
            previous = transform.position;
            if (Time.deltaTime <= 0f || health == null || health.IsDead || (CanStep != null && !CanStep())
                || distance < .001f || distance > Mathf.Max(1f, Time.deltaTime * 12f))
            { stride.Reset(); return; }
            bool running = distance / Time.deltaTime > 2.7f;
            if (bank != null && stride.Advance(distance, running ? 1.35f : 1f, out bool left)
                && bank.Resolve(SurfaceProbe?.Invoke() ?? FootSurface.Grass,
                    running ? MoveStance.Sprint : MoveStance.Walk, left,
                    out var slice, out float volume, out float pitch, out _))
                PlayAudio(slice, volume, pitch, transform.position, 20f, gainIncluded: true);
        }

        private void PlayAudio(AudioSlice slice, float volume, float pitch, Vector3 position, float range, bool gainIncluded = false)
        {
            if (!slice.IsValid || player == null || Time.timeScale <= 0f) return;
            if (Vector3.Distance(position, player.transform.position) >= range) return;
            // Preserve world direction, but measure distance from Nam instead of the trailing camera.
            var camera = Camera.main;
            Vector3 audioPosition = camera != null
                ? camera.transform.position + position - (player.transform.position + Vector3.up) : position;
            PooledAudioService.Resolve().PlayOneShot(slice.clip, audioPosition,
                volume * (gainIncluded ? 1f : slice.EffectiveGain), pitch, spatialBlend: 1f,
                startTime: slice.startTime, duration: slice.duration, nearDistance: 2f, farDistance: range);
        }

        /// <summary>Height of the shooter's chest above his feet: the second line a shot is tried along.</summary>
        public const float ChestHeight = 1.45f;

        /// <summary>
        /// Draws a shot up to the first obstruction. Returns whether it reaches its intended victim. The shot is tried
        /// along two lines, from the muzzle and from the shooter's chest (<paramref name="from"/>), and lands if either
        /// is clear: a rifle held out into a wall, a door or the cover he stands at (the muzzle inside it) does not stop
        /// every round, nor does a low sandbag a guard fires over (his chest behind it, the muzzle above). Until
        /// 19/10/2026 only the muzzle counted: Map 1's commander hit 0 of 40 rounds standing by a door. The tracer
        /// always leaves the muzzle.
        /// </summary>
        public bool Fire(Vector3 targetPoint, Transform victim, int mask, out Vector3 impact, Vector3? from = null)
        {
            if (weapon == null) weapon = GetComponentInChildren<Weapon>();
            Vector3 muzzle = weapon != null && weapon.Muzzle != null
                ? weapon.Muzzle.position : transform.position + Vector3.up * 1.3f + transform.forward * .35f;
            Vector3 chest = from ?? transform.position + Vector3.up * ChestHeight;
            bool reachesVictim = Cast(muzzle, targetPoint, victim, mask, out impact, out float travelled);
            if (!reachesVictim)
            {
                reachesVictim = Cast(chest, targetPoint, victim, mask, out var fromChest, out float chestTravelled);
                // A round that lands, or a miss that flies on further than from a buried muzzle, ends there.
                if (reachesVictim || chestTravelled > travelled) impact = fromChest;
            }
            if (prefab != null)
            {
                if (tracers[cursor] == null) tracers[cursor] = Instantiate(prefab, transform);
                tracers[cursor].gameObject.SetActive(true);
                tracers[cursor].TryPlay(muzzle, impact);
                if(weapon!=null)tracers[cursor].AnchorEmission(weapon.Muzzle);
                cursor = (cursor + 1) % tracers.Length;
            }
            weapon?.PlayMuzzleFlash();
            PlayAudio(GunshotClip != null ? new AudioSlice(GunshotClip, 0f, 0f, 1f) : gunshot, .85f, UnityEngine.Random.Range(.94f, 1.06f), muzzle, 65f);
            return reachesVictim;
        }

        /// <summary>One line of a shot: where it stops (the target if nothing is in the way), how far it got, and whether
        /// what stopped it is the victim (no victim: whether nothing did).</summary>
        private bool Cast(Vector3 start, Vector3 targetPoint, Transform victim, int mask, out Vector3 impact, out float travelled)
        {
            Vector3 delta = targetPoint - start;
            float distance = delta.magnitude;
            impact = targetPoint; travelled = distance;
            bool reachesVictim = true;
            if (distance < 1e-4f) return true;
            int count = Physics.RaycastNonAlloc(start, delta / distance, hits, distance, mask, QueryTriggerInteraction.Ignore);
            // Saturation is rare; correctness matters more than avoiding an allocation in this case.
            RaycastHit[] candidates = count == hits.Length
                ? Physics.RaycastAll(start, delta / distance, distance, mask, QueryTriggerInteraction.Ignore) : hits;
            if (candidates != hits) count = candidates.Length;
            float nearest = distance + .001f;
            for (int i = 0; i < count; i++)
            {
                var hit = candidates[i];
                if (hit.transform == transform || hit.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                nearest = hit.distance;
                impact = hit.point; travelled = hit.distance;
                reachesVictim = victim != null && (hit.transform == victim || hit.transform.IsChildOf(victim));
            }
            return reachesVictim;
        }
    }
}
