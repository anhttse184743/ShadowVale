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
    }
}
