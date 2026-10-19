using ShadowVale.AI.Boss;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Perception;
using ShadowVale.Core.Events;
using ShadowVale.Core.Services;
using ShadowVale.Data.Content;
using ShadowVale.Gameplay.Combat;
using UnityEngine;

namespace ShadowVale.Gameplay.Enemies.Boss
{
    /// <summary>
    /// A thrown grenade: a lob to the target, a fuse on the ground — long enough to run — then a
    /// blast that hurts the player within its radius whatever cover he is behind (that is its job:
    /// getting him out of cover), and a loud noise every guard around hears.
    /// </summary>
    public sealed class Grenade : MonoBehaviour
    {
        private Vector3 _from, _to;
        private float _flight, _fuse, _age;
        private float _radius, _damage, _noiseRadius;
        private string _thrower;
        private bool _exploded;

        public bool Exploded => _exploded;
        public Vector3 Target => _to;
        /// <summary>Seconds from the throw to the blast.</summary>
        public float SecondsToBlast => Mathf.Max(0, _flight + _fuse - _age);

        /// <summary>A map's own explosion at a point with the blast radius (Map 1: Map01Blast); null = a plain flash.</summary>
        public static System.Action<Vector3, float> BlastEffect { get; set; }

        public static Grenade Launch(Vector3 from, Vector3 target, BossProfile profile, string throwerId)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Grenade";
            Destroy(go.GetComponent<Collider>()); // Visual only: it follows its arc, nothing bumps into it.
            go.transform.localScale = Vector3.one * .18f;
            go.transform.position = from;
            var g = go.AddComponent<Grenade>();
            g._from = from; g._to = target;
            g._flight = FlightSeconds(Vector3.Distance(from, target));
            g._fuse = Mathf.Max(0, profile.GrenadeFuseSeconds);
            g._radius = profile.GrenadeRadius; g._damage = profile.GrenadeDamage; g._noiseRadius = profile.GrenadeNoiseRadius;
            g._thrower = throwerId;
            return g;
        }

        /// <summary>Time in the air: longer lobs take longer, within half a second and 1.3 s.</summary>
        public static float FlightSeconds(float distance) => Mathf.Clamp(distance / 14f, .5f, 1.3f);

        /// <summary>A lob: straight line plus a parabola peaking at a quarter of the distance (1.5–5 m).</summary>
        public static Vector3 ArcPoint(Vector3 from, Vector3 to, float u)
        {
            u = Mathf.Clamp01(u);
            float apex = Mathf.Clamp(Vector3.Distance(from, to) * .25f, 1.5f, 5f);
            return Vector3.Lerp(from, to, u) + Vector3.up * (apex * 4f * u * (1f - u));
        }

        /// <summary>Full damage at the centre, a third at the edge, none beyond.</summary>
        public static float DamageAt(float distance, float radius, float maxDamage)
            => distance > radius || radius <= 0 ? 0f : maxDamage * Mathf.Lerp(1f, 1f / 3f, distance / radius);

        private void Update()
        {
            if (_exploded) return;
            _age += Time.deltaTime;
            transform.position = ArcPoint(_from, _to, _flight > 0 ? _age / _flight : 1f);
            if (_age >= _flight + _fuse) Explode();
        }

        /// <summary>Goes off now (also called by tests).</summary>
        public float Explode()
        {
            if (_exploded) return 0;
            _exploded = true;
            Vector3 at = _to;
            float dealt = 0;
            if (ServiceLocator.TryResolve<IPlayerObservable>(out var player) && player.IsAlive)
            {
                float damage = DamageAt(Vector3.Distance(player.Position, at), _radius, _damage);
                if (damage > 0 && TryFindPlayerDamageable(player, at, out var target))
                {
                    target.TakeDamage(damage, player.Position + Vector3.up, gameObject);
                    dealt = damage;
                }
            }
            if (ServiceLocator.TryResolve<NoiseSystem>(out var noise)) noise.Emit(at, _noiseRadius, NoiseKind.Grenade);
            EventBus.Publish(new GrenadeExplodedEvent(_thrower, at, dealt));
            if (BlastEffect != null) BlastEffect(at, _radius); else Flash(at, _radius);
            Destroy(gameObject);
            return dealt;
        }

        private bool TryFindPlayerDamageable(IPlayerObservable player, Vector3 at, out IDamageable target)
        {
            target = null;
            foreach (var hit in Physics.OverlapSphere(at, _radius + 1.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!player.Owns(hit.transform)) continue;
                target = hit.GetComponentInParent<IDamageable>();
                if (target != null) return true;
            }
            return false;
        }

        private static void Flash(Vector3 at, float radius)
        {
            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "Grenade blast";
            Destroy(flash.GetComponent<Collider>());
            flash.transform.position = at;
            flash.transform.localScale = Vector3.one * radius * 1.2f;
            var renderer = flash.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = new Color(1f, .55f, .15f);
            Destroy(flash, .2f);
        }
    }
}
