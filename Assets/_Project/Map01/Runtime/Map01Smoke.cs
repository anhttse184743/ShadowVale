using System.Collections.Generic;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// A smoke bomb's cloud: a sphere of thick smoke (4 m across the ground by default) that stands for 15 seconds,
    /// thinning out over the last few. While it stands no guard sees through it (Map01EnemyController.CanSeePlayer
    /// asks <see cref="Blocks"/>): a line of sight that enters the cloud is lost, from either side. It looks like hand
    /// smoke: the igniter spits sparks, a thick white-grey jet hisses out of the can and rolls into a billowing cloud
    /// that leans with the breeze and thins out at the end.
    /// </summary>
    public sealed class Map01Smoke : MonoBehaviour
    {
        private static readonly List<Map01Smoke> active = new List<Map01Smoke>();
        /// <summary>Clouds standing now.</summary>
        public static IReadOnlyList<Map01Smoke> Active => active;

        public Vector3 Centre { get; private set; }
        public float Radius { get; private set; }
        public float Until { get; private set; }
        private float started, seconds;
        private ParticleSystem particles;

        /// <summary>Lights a cloud of <paramref name="radius"/> metres at <paramref name="ground"/> for <paramref name="lasts"/> seconds.
        /// <paramref name="visible"/> false: it only hides (the dust of a blast, drawn by Map01Blast).</summary>
        public static Map01Smoke Spawn(Vector3 ground, float radius, float lasts, bool visible = true)
        {
            var go = new GameObject("Smoke cloud");
            var smoke = go.AddComponent<Map01Smoke>();
            smoke.Centre = ground + Vector3.up * radius * .4f;      // the cloud sits on the ground, a little squashed
            smoke.Radius = radius;
            smoke.started = Time.time; smoke.seconds = lasts; smoke.Until = Time.time + lasts;
            go.transform.position = ground;
            if (visible) smoke.BuildParticles();
            active.Add(smoke);
            return smoke;
        }

        /// <summary>True when the line from <paramref name="from"/> to <paramref name="to"/> passes through a standing cloud.</summary>
        public static bool Blocks(Vector3 from, Vector3 to)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var s = active[i];
                if (s == null) { active.RemoveAt(i); continue; }
                if (Time.time >= s.Until) continue;
                // Thinning out over the last 2 seconds: the cloud no longer hides once it is mostly gone.
                float r = s.Radius * Mathf.Clamp01((s.Until - Time.time) / 2f + .35f);
                if (SegmentDistance(from, to, s.Centre) < Mathf.Min(r, s.Radius)) return true;
            }
            return false;
        }
        /// <summary>True when <paramref name="point"/> is inside a standing cloud.</summary>
        public static bool Inside(Vector3 point)
        {
            foreach (var s in active) if (s != null && Time.time < s.Until && Vector3.Distance(point, s.Centre) < s.Radius) return true;
            return false;
        }

        private static float SegmentDistance(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a; float len = ab.sqrMagnitude;
            float t = len < 1e-6f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / len);
            return Vector3.Distance(p, a + ab * t);
        }

        private void Update()
        {
            if (Time.time < Until + 4f) return;                  // the last wisps drift off before the object goes
            active.Remove(this);
            Destroy(gameObject);
        }
        private void OnDestroy() => active.Remove(this);

        private void BuildParticles()
        {
            // The cloud itself. Hand smoke (RDG-2 and its kind) is a thick white-grey that builds up in a few seconds
            // from the canister's jet; the game hides from the first second (Blocks), so the first puffs come at once
            // and small, and swell out to the full cloud while the jet keeps feeding it.
            particles = Map01Fx.System(transform, "Cloud", transform.position, Map01Fx.Lit, seconds, 110);
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(seconds * .5f, seconds * .75f);
            main.startSize = new ParticleSystem.MinMaxCurve(Radius * .85f, Radius * 1.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.05f, .35f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.84f, .85f, .82f, .92f), new Color(.70f, .71f, .68f, .92f));
            main.gravityModifier = -.008f;
            particles.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24), new ParticleSystem.Burst(.5f, 22), new ParticleSystem.Burst(1.2f, 20),
                                                 new ParticleSystem.Burst(seconds * .35f, 14), new ParticleSystem.Burst(seconds * .55f, 10) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = Radius * .75f;
            shape.scale = new Vector3(1f, 1f, .6f);           // a low dome hugging the ground
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Map01Fx.Fade(particles, .08f, .9f, .75f);
            Map01Fx.Grow(particles, .4f, 1.2f);
            Map01Fx.Billow(particles, .25f, .3f);
            Map01Fx.Wind(particles, Map01Fx.Breeze * .5f);
            particles.Play();

            // The canister's jet: a thick stream hissing out of the top, leaning with the breeze, rolling into the cloud.
            var jet = Map01Fx.System(transform, "Jet", transform.position + Vector3.up * .1f, Map01Fx.Lit, seconds - 1.5f, 120);
            main = jet.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.25f, .45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.4f, 4f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.9f, .9f, .88f, .95f), new Color(.78f, .78f, .76f, .95f));
            var rate = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.8f, .85f), new Keyframe(1f, .2f));
            var emission = jet.emission; emission.rateOverTime = new ParticleSystem.MinMaxCurve(26f, rate);
            shape = jet.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 18f; shape.radius = .03f;
            shape.rotation = new Vector3(-75f, 30f, 0f);      // up and a little off true, as a can lying on its side throws it
            Map01Fx.Fade(jet, .04f, .85f, .6f);
            Map01Fx.Grow(jet, .5f, 5.5f);
            Map01Fx.Drag(jet, 1.5f);
            Map01Fx.Billow(jet, .4f, .6f);
            Map01Fx.Wind(jet, Map01Fx.Breeze);
            jet.Play();

            // The igniter catching: a spit of sparks for the first half second.
            var sparks = Map01Fx.System(transform, "Ignition", transform.position + Vector3.up * .1f, Map01Fx.Glow, .5f, 24);
            main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.12f, .3f);
            main.startSize = new ParticleSystem.MinMaxCurve(.03f, .07f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.gravityModifier = .6f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .75f, .35f, 1f), new Color(1f, .5f, .15f, 1f));
            emission = sparks.emission; emission.rateOverTime = 40f;
            shape = sparks.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 35f; shape.radius = .02f;
            shape.rotation = new Vector3(-75f, 30f, 0f);
            sparks.Play();
        }

        /// <summary>Clears every cloud (a checkpoint load: the save does not keep smoke).</summary>
        public static void ClearAll()
        {
            foreach (var s in active.ToArray()) if (s != null) Destroy(s.gameObject);
            active.Clear();
        }
    }
}
