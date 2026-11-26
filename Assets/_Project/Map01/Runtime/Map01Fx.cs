using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// What the code-built effects (Map01Smoke, Map01Blast) share: a soft round puff texture, a scorch mark, the few
    /// particle materials (lit and unlit alpha-blended smoke and dust, additive glow) and a particle system set up the
    /// way every one of them starts - stopped, one-shot, world space, no emission until asked for.
    /// </summary>
    internal static class Map01Fx
    {
        private static Texture2D puff, scorch;
        private static Material lit, unlit, glow, burn;

        /// <summary>A soft round puff, transparent at its rim, so overlapping billboards blend into one cloud.</summary>
        public static Texture2D Puff
        {
            get
            {
                if (puff != null) return puff;
                const int n = 64;
                puff = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Fx puff" };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - r); a = a * a * (3f - 2f * a);
                        float grain = .8f + .2f * Mathf.PerlinNoise(x * .19f, y * .19f);
                        px[y * n + x] = new Color(1f, 1f, 1f, a * grain);
                    }
                puff.SetPixels(px); puff.Apply();
                return puff;
            }
        }

        /// <summary>A burnt patch: black-brown in the middle, ragged and soot-streaked toward its edge.</summary>
        public static Texture2D Scorch
        {
            get
            {
                if (scorch != null) return scorch;
                const int n = 128;
                scorch = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Fx scorch" };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy), angle = Mathf.Atan2(dy, dx);
                        float rag = .25f * Mathf.PerlinNoise(Mathf.Cos(angle) * 2.3f + 5f, Mathf.Sin(angle) * 2.3f + 5f)   // ragged rim
                                  + .12f * Mathf.PerlinNoise(x * .09f, y * .09f);
                        float a = Mathf.Clamp01((.92f - rag - r) / .35f); a = a * a;
                        float streak = .75f + .25f * Mathf.PerlinNoise(angle * 6f + 20f, r * 3f);                     // soot rays
                        float shade = Mathf.Lerp(.05f, .16f, r) * streak;
                        px[y * n + x] = new Color(shade * 1.1f, shade * .95f, shade * .8f, a * .9f);
                    }
                scorch.SetPixels(px); scorch.Apply();
                return scorch;
            }
        }

        /// <summary>Smoke and dust that the sun and shade fall on.</summary>
        public static Material Lit => lit != null ? lit : lit = Make("Fx smoke (lit)", "Universal Render Pipeline/Particles/Simple Lit", false, Puff);
        /// <summary>Grit and flying dirt: dark whatever the light.</summary>
        public static Material Unlit => unlit != null ? unlit : unlit = Make("Fx grit", "Universal Render Pipeline/Particles/Unlit", false, Puff);
        /// <summary>Flash and flame: added onto what is behind.</summary>
        public static Material Glow => glow != null ? glow : glow = Make("Fx glow", "Universal Render Pipeline/Particles/Unlit", true, Puff);
        /// <summary>The scorch mark on the ground.</summary>
        public static Material Burn => burn != null ? burn : burn = Make("Fx scorch", "Universal Render Pipeline/Particles/Unlit", false, Scorch);

        private static Material Make(string name, string shaderName, bool additive, Texture texture)
        {
            var shader = Shader.Find(shaderName) ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
            var m = new Material(shader) { name = name, mainTexture = texture };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);                  // transparent
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", additive ? 2f : 0f);       // additive / alpha
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            if (m.HasProperty("_SrcBlendAlpha")) m.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetInt("_DstBlendAlpha", (int)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        /// <summary>A stopped one-shot particle system under <paramref name="parent"/>, world space, emitting nothing until
        /// given bursts or a rate; every particle starts at a random turn.</summary>
        public static ParticleSystem System(Transform parent, string name, Vector3 position, Material material, float duration, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = duration; main.loop = false; main.playOnAwake = false; main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = ps.emission; emission.rateOverTime = 0f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        /// <summary>Alpha over each particle's life: in by <paramref name="inBy"/>, held at <paramref name="hold"/> until
        /// <paramref name="outFrom"/>, gone at the end. Colour stays the start colour.</summary>
        public static void Fade(ParticleSystem ps, float inBy, float hold, float outFrom)
        {
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inBy), new GradientAlphaKey(hold, outFrom), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        /// <summary>Size over each particle's life, from <paramref name="from"/> to <paramref name="to"/> times its start size, fast at first.</summary>
        public static void Grow(ParticleSystem ps, float from, float to)
        {
            var size = ps.sizeOverLifetime; size.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, from, 0f, (to - from) * 2.2f), new Keyframe(1f, to, 0f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        /// <summary>Slows particles down as air would (speed falls by e every 1/<paramref name="perSecond"/> s): a puff
        /// thrown out fast settles about speed/perSecond metres away.</summary>
        public static void Drag(ParticleSystem ps, float perSecond)
        {
            var limit = ps.limitVelocityOverLifetime; limit.enabled = true;
            limit.limit = 100f; limit.dampen = 0f; limit.drag = perSecond;
            limit.multiplyDragByParticleSize = false; limit.multiplyDragByParticleVelocity = false;
        }

        /// <summary>Slow billowing: noise that turns each puff over as it drifts.</summary>
        public static void Billow(ParticleSystem ps, float strength, float frequency)
        {
            var noise = ps.noise; noise.enabled = true;
            noise.strength = strength; noise.frequency = frequency; noise.scrollSpeed = .15f; noise.damping = true; noise.octaveCount = 2;
        }

        /// <summary>A light breeze carrying the smoke off: a fixed drift in world space.</summary>
        public static void Wind(ParticleSystem ps, Vector3 drift)
        {
            var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
            v.x = new ParticleSystem.MinMaxCurve(drift.x); v.y = new ParticleSystem.MinMaxCurve(drift.y); v.z = new ParticleSystem.MinMaxCurve(drift.z);
        }

        /// <summary>A light breeze, the same everywhere on the map, so every cloud leans the same way.</summary>
        public static readonly Vector3 Breeze = new Vector3(-.22f, 0f, .08f);

        // ---- Fire (26/11/2026): tongues of flame rather than glowing blobs, after the classic VFX-graph fire: a flipbook of
        // ragged flame shapes, a white-yellow heart going orange then deep red as each tongue rises and dies, turbulence,
        // embers wandering up, and an HDR tint so the bloom haloes it. Plain particle systems: no extra package, any GPU.

        private const int FlameFrames = 4;   // a 4 x 4 sheet
        private static Texture2D flameSheet, cloud;
        private static Material fire, smoke;

        /// <summary>16 frames of a flame tongue licking upward: wide and solid at the foot, torn into wisps at the top.
        /// White; the colour comes from the particle.</summary>
        public static Texture2D FlameSheet
        {
            get
            {
                if (flameSheet != null) return flameSheet;
                const int cell = 64, n = cell * FlameFrames;
                flameSheet = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Fx flame sheet" };
                var px = new Color[n * n];
                for (int frame = 0; frame < FlameFrames * FlameFrames; frame++)
                {
                    // Unity plays sheets from the top-left cell, row by row.
                    int cx = frame % FlameFrames, cy = FlameFrames - 1 - frame / FlameFrames;
                    float scroll = frame * .42f;
                    for (int y = 0; y < cell; y++)
                        for (int x = 0; x < cell; x++)
                        {
                            float u = (x + .5f) / cell * 2f - 1f, v = (y + .5f) / cell;               // v: 0 foot, 1 tip
                            float sway = (Mathf.PerlinNoise(v * 2.2f - scroll, 3.1f) - .5f) * .55f * v;  // the tongue bends as it rises
                            float width = .78f * Mathf.Pow(1f - v, .55f) * Mathf.Clamp01(v * 5f + .35f);   // teardrop
                            float d = Mathf.Abs(u - sway) / Mathf.Max(.001f, width);
                            float body = Mathf.Clamp01((1f - d) * 2.2f);
                            float tear = Mathf.PerlinNoise(u * 2.6f + 11f, v * 3.4f - scroll * 1.3f) + (1f - v) * .75f - .45f;   // ragged, torn at the top
                            float a = body * Mathf.Clamp01(tear * 2.4f) * Mathf.Clamp01((1f - v) * 1.6f);
                            float heat = Mathf.Clamp01(1f - d) * (1f - v * .6f);                             // hotter (more opaque) at the heart
                            a = Mathf.Clamp01(a * (.55f + .6f * heat));
                            px[(cy * cell + y) * n + cx * cell + x] = new Color(1f, 1f, 1f, a);
                        }
                }
                flameSheet.SetPixels(px); flameSheet.Apply();
                return flameSheet;
            }
        }

        /// <summary>A billowing smoke puff: soft-edged, lumpy with a few octaves of noise, so a column reads as rolling cloud.</summary>
        public static Texture2D Cloud
        {
            get
            {
                if (cloud != null) return cloud;
                const int n = 128;
                cloud = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Fx cloud" };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float lumps = .5f * Mathf.PerlinNoise(x * .045f + 3f, y * .045f + 7f) + .3f * Mathf.PerlinNoise(x * .1f + 1f, y * .1f) + .2f * Mathf.PerlinNoise(x * .22f, y * .22f + 5f);
                        float a = Mathf.Clamp01((1f - r - (1f - lumps) * .55f) * 2.2f); a = a * a * (3f - 2f * a);
                        float shade = .78f + .22f * lumps;                                                // the lumps catch a little light
                        px[y * n + x] = new Color(shade, shade, shade, a);
                    }
                cloud.SetPixels(px); cloud.Apply();
                return cloud;
            }
        }

        /// <summary>Flame: additive, the flame sheet, an HDR tint so the bloom (threshold 1) haloes it.</summary>
        public static Material Fire
        {
            get
            {
                if (fire != null) return fire;
                fire = Soft(Make("Fx fire", "Universal Render Pipeline/Particles/Unlit", true, FlameSheet), .6f);
                if (fire.HasProperty("_BaseColor")) fire.SetColor("_BaseColor", new Color(1.55f, 1.35f, 1.15f, .8f));   // just over the bloom threshold at the heart, not a white blob
                return fire;
            }
        }
        /// <summary>Fire smoke: lit, alpha-blended billowing cloud.</summary>
        public static Material Smoke => smoke != null ? smoke : smoke = Soft(Make("Fx smoke (cloud)", "Universal Render Pipeline/Particles/Simple Lit", false, Cloud), 1.5f);

        /// <summary>Soft particles: a billboard fades out where it meets the ground or a wall instead of being cut off in a
        /// hard line. Every quality tier keeps the camera depth texture this reads.</summary>
        private static Material Soft(Material m, float fadeDistance)
        {
            if (!m.HasProperty("_SoftParticlesEnabled")) return m;
            m.SetFloat("_SoftParticlesEnabled", 1f); m.EnableKeyword("_SOFTPARTICLES_ON");
            m.SetFloat("_SoftParticlesNearFadeDistance", 0f); m.SetFloat("_SoftParticlesFarFadeDistance", fadeDistance);
            m.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / fadeDistance, 0f, 0f));
            return m;
        }

        /// <summary>Sends a system's particles up, whatever its emitter shape: the start speed becomes an upward speed (a box
        /// or sphere would throw them sideways and down), with a little spread and a drift on top (e.g. <see cref="Breeze"/>).</summary>
        public static void Rise(ParticleSystem ps, Vector3 drift, float spread)
        {
            var main = ps.main; var speed = main.startSpeed;
            float low = speed.mode == ParticleSystemCurveMode.TwoConstants ? speed.constantMin : speed.constant;
            float high = speed.mode == ParticleSystemCurveMode.TwoConstants ? speed.constantMax : speed.constant;
            main.startSpeed = 0f;
            var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
            v.x = new ParticleSystem.MinMaxCurve(drift.x - spread, drift.x + spread);
            v.y = new ParticleSystem.MinMaxCurve(low + drift.y, high + drift.y);
            v.z = new ParticleSystem.MinMaxCurve(drift.z - spread, drift.z + spread);
        }

        /// <summary>Turns a particle system into tongues of flame: the flame sheet played over each tongue's life, white-yellow
        /// to orange to deep red and out, swelling then thinning as it rises, and shaken by turbulence. Keeps its emission,
        /// shape, speeds and sizes; its start colour still tints it.</summary>
        public static void Flame(ParticleSystem ps, float turbulence = .8f)
        {
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = Fire;
            var main = ps.main; main.startRotation = new ParticleSystem.MinMaxCurve(-.18f, .18f);   // tongues stay upright
            var sheet = ps.textureSheetAnimation; sheet.enabled = true; sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = FlameFrames; sheet.numTilesY = FlameFrames; sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, FlameFrames * FlameFrames - 1f); sheet.cycleCount = 1;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, .97f, .82f), 0f), new GradientColorKey(new Color(1f, .78f, .42f), .22f),
                              new GradientColorKey(new Color(1f, .42f, .12f), .55f), new GradientColorKey(new Color(.55f, .1f, .03f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .08f), new GradientAlphaKey(.85f, .55f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .55f), new Keyframe(.25f, 1f), new Keyframe(1f, .35f)));
            var noise = ps.noise; noise.enabled = true; noise.strength = turbulence; noise.frequency = .9f; noise.scrollSpeed = 1.2f;
            noise.damping = true; noise.octaveCount = 2; noise.quality = ParticleSystemNoiseQuality.Low;
            Rise(ps, Breeze * .6f, .25f);
            // Cheaper on the lower graphics tiers: fewer tongues (each is a little bigger to cover the same fire).
            float share = GameOptions.Quality == GameOptions.LowTier ? .45f : GameOptions.Quality == GameOptions.MediumTier ? .7f : 1f;
            if (share < 1f)
            {
                var emission = ps.emission; emission.rateOverTimeMultiplier *= share;
                main.startSizeMultiplier *= Mathf.Lerp(1.25f, 1f, share);
            }
        }
        /// <summary>Embers: small hot sparks that wander as they rise and fade from yellow to red.</summary>
        public static void Ember(ParticleSystem ps)
        {
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = Glow;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, .9f, .55f), 0f), new GradientColorKey(new Color(1f, .45f, .12f), .5f), new GradientColorKey(new Color(.7f, .12f, .03f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var noise = ps.noise; noise.enabled = true; noise.strength = 1.4f; noise.frequency = .6f; noise.scrollSpeed = .8f; noise.damping = false;
            var main = ps.main; main.startSize = new ParticleSystem.MinMaxCurve(main.startSize.constantMin * .6f, main.startSize.constantMax * .6f);
            Rise(ps, Breeze, .8f);
        }
    }
}
