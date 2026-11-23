using System.Collections.Generic;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// What makes a quiet village read as a country at war, kept cheap: a few smoke columns standing on the horizon,
    /// dust hanging in the air round the camera, and by night the far flash of shelling on the skyline with its late,
    /// low boom. Owned by <see cref="GameLook"/>; it only runs while the film look is on, and the tier sets how much.
    /// </summary>
    internal sealed class WarAmbience : MonoBehaviour
    {
        private readonly List<ParticleSystem> columns = new();
        private ParticleSystem dust;
        private AudioSource boom;
        private AudioClip[] booms;
        private float nextFlash = 6f, pendingBoomAt = -1f;
        private int builtTier = -1;

        /// <summary>0..1, the light of the latest far flash; GameLook warms the haze with it.</summary>
        public float Flash { get; private set; }

        public void Clear()
        {
            foreach (var c in columns) if (c != null) Destroy(c.gameObject);
            columns.Clear();
            if (dust != null) Destroy(dust.gameObject);
            dust = null; builtTier = -1; Flash = 0; pendingBoomAt = -1;
        }

        public void Tick(bool on, Camera view, float night, float dawn, bool cinematic)
        {
            if (!on || view == null) { if (builtTier >= 0) Clear(); return; }
            if (builtTier != GameOptions.Quality) { Clear(); Build(view); }
            dust.transform.position = view.transform.position;
            var main = dust.main;
            main.startColor = Color.Lerp(new Color(.86f, .8f, .68f, .32f), new Color(.62f, .68f, .82f, .16f), night);

            // Far shelling: by night, and more often as Night 3 is fought into dawn. Never over a cutscene's own sound.
            Flash = Mathf.MoveTowards(Flash, 0, Time.deltaTime / .35f);
            bool shelling = night > .5f || dawn > .05f;
            if (shelling && !cinematic && Time.time >= nextFlash)
            {
                Flash = Random.Range(.6f, 1f);
                pendingBoomAt = Time.time + Random.Range(1.2f, 3.2f);   // sound arrives after the light, from a few km
                nextFlash = Time.time + (dawn > .05f ? Random.Range(4f, 9f) : Random.Range(9f, 20f));
            }
            else if (!shelling) nextFlash = Mathf.Max(nextFlash, Time.time + 4f);
            if (pendingBoomAt > 0 && Time.time >= pendingBoomAt && booms != null && booms.Length > 0)
            {
                pendingBoomAt = -1;
                boom.pitch = Random.Range(.7f, .85f);
                boom.PlayOneShot(booms[Random.Range(0, booms.Length)], Random.Range(.18f, .3f));
            }
        }

        private void Build(Camera view)
        {
            builtTier = GameOptions.Quality;
            int count = builtTier == GameOptions.LowTier ? 2 : builtTier == GameOptions.MediumTier ? 3 : 4;
            var at = view.transform.position;
            var terrain = Terrain.activeTerrain;
            // Fixed compass bearings from where the player first stands, far enough to sit in the haze.
            float[] bearings = { 35f, 140f, 215f, 300f };
            float[] distances = { 190f, 230f, 170f, 250f };
            for (int i = 0; i < count; i++)
            {
                var p = at + Quaternion.Euler(0, bearings[i], 0) * Vector3.forward * distances[i];
                p.y = terrain != null ? terrain.transform.position.y + terrain.SampleHeight(p) : at.y - 2f;
                columns.Add(Column(p, i));
            }
            dust = Dust(builtTier == GameOptions.LowTier ? 10 : 24);
            if (boom == null)
            {
                boom = gameObject.AddComponent<AudioSource>();
                boom.spatialBlend = 0; boom.playOnAwake = false;
                booms = new[] { Resources.Load<AudioClip>("Dialogue/FullGameFree20261023/m2v3_artillery_01"),
                    Resources.Load<AudioClip>("Dialogue/FullGameFree20261023/m2v3_artillery_02") };
                booms = System.Array.FindAll(booms, c => c != null);
            }
        }

        private ParticleSystem Column(Vector3 position, int index)
        {
            var ps = Map01Fx.System(transform, "War smoke column " + index, position, Map01Fx.Lit, 12f, builtTier == GameOptions.LowTier ? 28 : 48);
            var main = ps.main;
            main.loop = true; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(24f, 32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.4f, 3.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(12f, 20f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.15f, .14f, .13f, .7f), new Color(.24f, .22f, .2f, .6f));
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 7f; shape.radius = 3f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var emission = ps.emission; emission.rateOverTime = 1.5f;
            Map01Fx.Grow(ps, 1f, 3.4f); Map01Fx.Fade(ps, .08f, .85f, .55f); Map01Fx.Billow(ps, 1.1f, .07f);
            Map01Fx.Wind(ps, Map01Fx.Breeze * 9f);
            ps.Play();
            return ps;
        }

        private ParticleSystem Dust(float rate)
        {
            var ps = Map01Fx.System(transform, "Dust in the air", Vector3.zero, Map01Fx.Unlit, 8f, 240);
            var main = ps.main;
            main.loop = true; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSpeed = .05f;
            main.startSize = new ParticleSystem.MinMaxCurve(.03f, .07f);
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(16f, 6f, 16f);
            var emission = ps.emission; emission.rateOverTime = rate;
            Map01Fx.Fade(ps, .2f, 1f, .8f); Map01Fx.Billow(ps, .25f, .3f);
            ps.Play();
            return ps;
        }
    }
}
