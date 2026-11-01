using System.Linq;
using System.Collections;
using System.Collections.Generic;
using ShadowVale.AI.Perception;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// What is thrown and what it does: the projectile's model, its flight along the arc, the grenade's lever and fuse, the blast and the smoke.
    /// </summary>
    public sealed partial class Map01StoneThrow
    {
        /// <summary>What flies: the item's own model where the catalog has one, else a grey pebble.</summary>
        private static GameObject Projectile(string item)
        {
            if (item == "stone") return CreateStoneVisual("Thrown stone");
            var catalog = Map01ItemCatalog.Load();
            var model = item != "stone" && catalog != null ? catalog.Model(item) : null;
            if (model != null)
            {
                var go = Instantiate(model);
                go.name = "Thrown " + item;
                go.transform.localScale = Vector3.one * catalog.Scale(item);
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                return go;
            }
            var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stone.name = "Thrown " + item;
            Destroy(stone.GetComponent<Collider>());
            stone.transform.localScale = Vector3.one * .16f;
            stone.GetComponent<Renderer>().material.color = item == "stone" ? new Color(.46f, .44f, .39f) : new Color(.28f, .31f, .2f);
            return stone;
        }

        // The same palm-sized pebble is used in the hand and in flight.
        internal static GameObject CreateStoneVisual(string name)
            => CreateHeldVisual("stone",name);

        internal static GameObject CreateHeldVisual(string item,string name)
        {
            var root = new GameObject(name);
            var model = Map01ItemCatalog.Load()?.Model(item);
            var visual = model != null ? Instantiate(model, root.transform, false) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.transform.SetParent(root.transform, false);
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) {
                collider.enabled = false;
                Destroy(collider);
            }
            foreach (var lod in visual.GetComponentsInChildren<LODGroup>(true)) lod.ForceLOD(0);
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0) {
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float size = bounds.size.magnitude;
                float scale = (item=="stone"?.15f:item=="smoke_bomb"?.21f:.18f) / Mathf.Max(.001f, size);
                visual.transform.localScale *= scale;
                visual.transform.localPosition -= bounds.center * scale;
            }
            return root;
        }

        private IEnumerator Fly(Vector3[] path, Vector3 landing, string item)
        {
            // Inventory is spent once on release; flight begins at the authored
            // hand-opening beat rather than while the arm is still drawn back.
            float release=0;
            while(release<Map01NamActions.StoneReleaseDelay) {
                yield return null;if(!mission.Stopped)release+=Time.deltaTime;
            }
            var actor=mission.player.GetComponentInChildren<Animator>();
            var hand=actor!=null&&actor.isHuman?actor.GetBoneTransform(HumanBodyBones.RightHand):null;
            if(hand!=null) {
                var offset=hand.TransformPoint(Vector3.up*.06f)-path[0];
                for(int i=0;i<path.Length;i++)path[i]+=offset*(1-i/(float)(path.Length-1));
            }
            var stone = Projectile(item);
            stone.transform.position = path[0];
            // The pin and its ring (the smoke bomb's pull cord) stayed in Nam's other hand; the F1's lever springs off
            // as it leaves his, and its fuse smokes in flight.
            var ring = FindPart(stone.transform, "ring");
            if (ring != null) Destroy(ring.gameObject);
            if (item == "grenade_f1")
            {
                var lever = FindPart(stone.transform, "lever");
                if (lever != null) StartCoroutine(LeverOff(lever, path[Mathf.Min(1, path.Length - 1)] - path[0]));
                FuseSmoke(stone.transform);
            }
            float length = 0;
            for (int i = 1; i < path.Length; i++) length += Vector3.Distance(path[i - 1], path[i]);
            float duration = Mathf.Max(.2f, length / throwSpeed), elapsed = 0;
            while (elapsed < duration)
            {
                float along = elapsed / duration * (path.Length - 1);
                int i = Mathf.Min(path.Length - 2, (int)along);
                stone.transform.position = Vector3.Lerp(path[i], path[i + 1], along - i);
                yield return null;
                if (mission.Stopped) continue; // A pause holds the stone in the air rather than dropping the noise early.
                elapsed += Time.deltaTime;
            }
            stone.transform.position = landing + Vector3.up * .06f;
            if (item == "smoke_bomb")
            {
                Map01Smoke.Spawn(landing, SmokeRadius, SmokeSeconds);
                mission.Say($"Xì... Bom khói phủ kín {SmokeRadius:0} m quanh chỗ rơi trong {SmokeSeconds:0} giây — lính không nhìn xuyên qua được.", 4);
                Destroy(stone, SmokeSeconds);
                yield break;
            }
            if (item == "grenade_f1")
            {
                Destroy(stone);   // it goes off where it comes down
                Explode(landing);
                yield break;
            }
            int heard = mission.EmitNoise(landing, mission.Settings.stoneNoise, NoiseKind.Stone);
            mission.Say(heard > 0
                ? $"Cạch! {heard} lính nghe thấy tiếng đá và bỏ vị trí đi kiểm tra — tranh thủ lúc này."
                : "Cạch! ...Không lính nào ở đủ gần để nghe thấy.", 4);
            Destroy(stone, 8f);
        }

        /// <summary>The F1 goes off at <paramref name="at"/>: killed within <see cref="KillRadius"/>, wounded (60% of
        /// their health) out to <see cref="WoundRadius"/>, Nam included; the blast is a gunshot to every guard within
        /// <see cref="GrenadeNoise"/> m. Returns (killed, wounded) guards.</summary>
        public (int killed, int wounded) Explode(Vector3 at)
        {
            int killed = 0, wounded = 0;
            foreach (var guard in mission.Enemies)
            {
                if (guard == null || !guard.Alive) continue;
                float d = Vector3.Distance(guard.transform.position, at);
                if (d > WoundRadius) continue;
                var health = guard.GetComponent<ShadowVale.Gameplay.Combat.Health>();
                if (health == null) continue;
                if (d <= KillRadius) { health.TakeDamage(health.Max * 4f, at, mission.player.gameObject); killed++; }
                else { health.TakeDamage(health.Max * .6f, at, mission.player.gameObject); wounded++; }
            }
            if(mission.MapTwo!=null){var extra=mission.MapTwo.CharacterExplosion(at);killed+=extra.killed;wounded+=extra.wounded;}
            float self = Vector3.Distance(mission.player.position, at);
            if (self <= KillRadius) mission.Damage(mission.Settings.playerHP * 4f);
            else if (self <= WoundRadius) mission.Damage(mission.Settings.playerHP * .45f);
            mission.EmitNoise(at, GrenadeNoise, NoiseKind.Gunshot);
            Vector3 up = Physics.Raycast(at + Vector3.up * 1.5f, Vector3.down, out var ground, 4f, mission.ObstructionMask, QueryTriggerInteraction.Ignore)
                ? ground.normal : Vector3.up;
            Map01Blast.Spawn(at, up, mission.gameCamera);
            Map01Smoke.Spawn(at, 1.6f, 4f, visible: false);     // the blast's dust hides a man behind it for a few seconds
            mission.Say(killed + wounded > 0
                ? $"ẦM! Lựu đạn nổ: {killed} lính chết, {wounded} lính bị thương. Tiếng nổ vang xa {GrenadeNoise:0} m."
                : $"ẦM! Lựu đạn nổ, không trúng ai. Tiếng nổ vang xa {GrenadeNoise:0} m.", 5);
            return (killed, wounded);
        }

        private static Transform FindPart(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && (t.name == name || t.name.StartsWith(name+".",System.StringComparison.Ordinal))) return t;
            return null;
        }

        /// <summary>The F1's lever (the spoon) flicked off by its spring as the grenade leaves the hand: up and back,
        /// turning over, down onto the ground near Nam, where it lies a while.</summary>
        private IEnumerator LeverOff(Transform lever, Vector3 along)
        {
            lever.SetParent(null, true);
            Vector3 back = -Vector3.ProjectOnPlane(along, Vector3.up).normalized;
            Vector3 velocity = back * 1.6f + Vector3.up * 3.2f + Vector3.Cross(Vector3.up, back) * Random.Range(-.8f, .8f);
            Vector3 spin = Random.onUnitSphere * 1400f;
            for (float t = 0; t < 2f && lever != null; )
            {
                yield return null;
                if (mission.Stopped) continue;
                float dt = Time.deltaTime; t += dt;
                velocity += Physics.gravity * dt;
                Vector3 next = lever.position + velocity * dt;
                if (velocity.y < 0 && Physics.Linecast(lever.position, next, out var hit, mission.ObstructionMask, QueryTriggerInteraction.Ignore))
                {
                    lever.position = hit.point + hit.normal * .01f;
                    lever.rotation = Quaternion.FromToRotation(lever.up, hit.normal) * lever.rotation;
                    break;
                }
                lever.position = next;
                lever.Rotate(spin * dt, Space.World);
            }
            if (lever != null) Destroy(lever.gameObject, 20f);
        }

        /// <summary>The fuse's thin smoke, trailing in flight (and rising where the commander's grenade lies until it
        /// goes off, Map01GrenadeWarning).</summary>
        internal static void FuseSmoke(Transform grenade)
        {
            var wisp = Map01Fx.System(grenade, "Fuse smoke", grenade.position, Map01Fx.Lit, 30f, 60);
            var main = wisp.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.9f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.04f, .08f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.2f, .5f);
            main.startColor = new Color(.78f, .78f, .76f, .55f);
            var emission = wisp.emission; emission.rateOverTime = 16f;
            var shape = wisp.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 10f; shape.radius = .01f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Map01Fx.Fade(wisp, .1f, .8f, .4f); Map01Fx.Grow(wisp, 1f, 5f); Map01Fx.Billow(wisp, .15f, 1.2f); Map01Fx.Wind(wisp, Map01Fx.Breeze);
            wisp.Play();
        }
    }
}
