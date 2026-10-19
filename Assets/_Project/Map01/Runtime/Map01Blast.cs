using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// What an F1 going off looks like (Map01StoneThrow.Explode). Range and test footage of fragmentation grenades shows
    /// very little flame - the TNT filling gives a white-orange flash of a tenth of a second, not a film fireball - then
    /// grit and earth thrown up in a cone, a brown dust burst that rolls out along the ground as a ring, and a pale grey
    /// smoke cloud that climbs and drifts for several seconds; a black scorch is left where it lay. The fragments themselves
    /// are too fast to see. Built from code-made particles (Map01Fx), so it needs no assets; the camera shakes with
    /// distance (Map01CameraShake). It is heard in 3D where it went off (<see cref="Sound"/>): sharp up close, a dull
    /// rumble with a soft echo far off, and late by the speed of sound.
    /// </summary>
    public sealed class Map01Blast : MonoBehaviour
    {
        /// <summary>How long the scorch stays on the ground (it fades over the last quarter).</summary>
        public const float ScorchSeconds = 60f;
        /// <summary>"Explosion, Medium Blast" by Kinoton (freesound.org/s/516914, CC0): a grenade at medium distance with a
        /// long reverb tail. Imported mono, so Unity places it.</summary>
        public const string SoundResource = "Audio/Explosion_Medium_Blast";
        /// <summary>Full loudness within this, fading (logarithmically) to nothing at <see cref="SoundFar"/>, metres.</summary>
        public const float SoundNear = 5f, SoundFar = 120f;
        public const float SpeedOfSound = 340f;
        private Light flash;
        private Renderer scorch;
        private float born;
        private static AudioClip soundClip;
        /// <summary>The bang (null when the clip is missing).</summary>
        public AudioSource Sound { get; private set; }

        /// <summary>The blast at <paramref name="ground"/>, the ground facing <paramref name="up"/>;
        /// <paramref name="viewer"/> is the camera that shakes and <paramref name="radius"/> sizes the dust spread.</summary>
        public static Map01Blast Spawn(Vector3 ground, Vector3 up, Camera viewer = null, float radius = Map01StoneThrow.WoundRadius)
        {
            var go = new GameObject("F1 blast");
            go.transform.position = ground;
            var blast = go.AddComponent<Map01Blast>();
            blast.born = Time.time;
            blast.Build(ground, up.sqrMagnitude > .01f ? up.normalized : Vector3.up, radius);
            blast.Sound = blast.PlaySound(ground, radius, viewer);
            Map01CameraShake.Kick(viewer, ground, .22f, 35f, .7f);
            return blast;
        }

        /// <summary>
        /// The bang, in 3D at the blast. The further the listener, the more its top end is lost (a low-pass, down to a
        /// rumble) and the more of it is echo (a light reverb); it also arrives later, at the speed of sound. A bigger
        /// blast (the commander's 7.5 m) is a little louder than a small one.
        /// </summary>
        private AudioSource PlaySound(Vector3 ground, float radius, Camera viewer)
        {
            if (soundClip == null) soundClip = Resources.Load<AudioClip>(SoundResource);
            if (soundClip == null) return null;
            var go = new GameObject("Blast sound");
            go.transform.SetParent(transform, false);
            go.transform.position = ground + Vector3.up * .5f;
            var source = go.AddComponent<AudioSource>();
            source.clip = soundClip;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = SoundNear;
            source.maxDistance = SoundFar;
            source.dopplerLevel = 0f;
            source.spread = 60f; // A blast fills the space round it rather than coming from a point.
            source.volume = Mathf.Clamp(.8f + .2f * radius / 7.5f, .7f, 1f);
            source.pitch = Random.Range(.94f, 1.04f);

            float distance = Vector3.Distance(ListenerPosition(viewer, ground), ground);
            go.AddComponent<AudioLowPassFilter>().cutoffFrequency = MuffleCutoff(distance);
            var echo = go.AddComponent<AudioReverbFilter>();
            echo.reverbPreset = AudioReverbPreset.User;
            echo.dryLevel = 0f;
            echo.room = Mathf.Lerp(-1600f, -700f, Mathf.Clamp01(distance / SoundFar)); // More echo the further off.
            echo.roomHF = -1800f;
            echo.decayTime = 2.2f;
            echo.decayHFRatio = .5f;
            echo.reflectionsLevel = -1400f;
            echo.reverbLevel = -500f;
            echo.diffusion = 100f;
            echo.density = 100f;
            source.PlayDelayed(distance / SpeedOfSound);
            return source;
        }

        /// <summary>Low-pass cutoff for a blast heard at <paramref name="distance"/> m: open up close, about 1 kHz at the
        /// edge of hearing, where it is a dull rumble.</summary>
        public static float MuffleCutoff(float distance)
            => Mathf.Lerp(20000f, 1000f, Mathf.Pow(Mathf.Clamp01(distance / SoundFar), .6f));

        private static Vector3 ListenerPosition(Camera viewer, Vector3 fallback)
        {
            var listener = FindFirstObjectByType<AudioListener>();
            if (listener != null) return listener.transform.position;
            return viewer != null ? viewer.transform.position : fallback;
        }

        private void Build(Vector3 ground, Vector3 up, float radius)
        {
            var t = transform;
            // The original particles were authored for the F1's 7 m wound radius. Keep the flash
            // brief and compact while letting the earth, dust ring and cloud fill the wider blast.
            float spread = Mathf.Max(.1f, radius / 7f);
            float plume = Mathf.Sqrt(spread);
            // the flash: a light that dies in a tenth of a second, and a white-hot core on top of it
            var lightGo = new GameObject("Flash");
            lightGo.transform.SetParent(t, false); lightGo.transform.position = ground + up * .6f;
            flash = lightGo.AddComponent<Light>();
            flash.type = LightType.Point; flash.color = new Color(1f, .8f, .55f); flash.range = 18f * plume; flash.intensity = 30f;
            flash.shadows = LightShadows.None;

            var core = Map01Fx.System(t, "Core", ground + up * .45f, Map01Fx.Glow, .2f, 4);
            var main = core.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.06f, .1f); main.startSize = new ParticleSystem.MinMaxCurve(1.7f, 2.5f);
            main.startSpeed = 0f; main.startColor = new Color(1f, .9f, .7f, 1f);
            core.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 3) });
            var noShape = core.shape; noShape.enabled = false;
            Map01Fx.Fade(core, .01f, .8f, .4f);

            // short tongues of flame thrown out low
            var fire = Map01Fx.System(t, "Flame", ground + up * .25f, Map01Fx.Glow, .3f, 16);
            main = fire.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.08f, .2f); main.startSize = new ParticleSystem.MinMaxCurve(.45f, 1.05f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .62f, .25f, 1f), new Color(1f, .42f, .12f, 1f));
            fire.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
            var shape = fire.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = .2f;
            shape.rotation = Quaternion.FromToRotation(Vector3.forward, up).eulerAngles;
            Map01Fx.Fade(fire, .05f, .7f, .3f); Map01Fx.Grow(fire, .7f, 1.6f); Map01Fx.Drag(fire, 6f);

            // grit and clods thrown up in a cone, streaking, falling back
            var dirt = Map01Fx.System(t, "Dirt", ground + up * .1f, Map01Fx.Unlit, 1.8f, 70);
            main = dirt.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.7f, 1.5f); main.startSize = new ParticleSystem.MinMaxCurve(.03f * plume, .09f * plume);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f * plume, 14f * plume); main.gravityModifier = 1.1f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.16f, .12f, .09f, 1f), new Color(.27f, .21f, .15f, 1f));
            dirt.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 55) });
            shape = dirt.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 34f; shape.radius = .15f;
            shape.rotation = Quaternion.FromToRotation(Vector3.forward, up).eulerAngles;
            var dirtRenderer = dirt.GetComponent<ParticleSystemRenderer>();
            dirtRenderer.renderMode = ParticleSystemRenderMode.Stretch; dirtRenderer.velocityScale = .035f; dirtRenderer.lengthScale = 1.2f;

            // the dust burst: brown-grey, thrown out fast, stopped by the air within a couple of metres, then hanging
            var dust = Map01Fx.System(t, "Dust", ground + up * .3f, Map01Fx.Lit, 3.5f, 24);
            main = dust.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3f); main.startSize = new ParticleSystem.MinMaxCurve(1f * plume, 2f * plume);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f * spread, 7f * spread);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.47f, .41f, .33f, .9f), new Color(.38f, .33f, .27f, .9f));
            dust.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });
            shape = dust.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = .3f;
            shape.rotation = Quaternion.FromToRotation(Vector3.forward, up).eulerAngles;
            Map01Fx.Fade(dust, .04f, .75f, .45f); Map01Fx.Grow(dust, .8f, 2.2f); Map01Fx.Drag(dust, 2f); Map01Fx.Wind(dust, Map01Fx.Breeze);

            // the ring that rolls out along the ground
            var ring = Map01Fx.System(t, "Ground ring", ground + up * .15f, Map01Fx.Lit, 1.5f, 32);
            main = ring.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.6f, 1.1f); main.startSize = new ParticleSystem.MinMaxCurve(.6f * plume, 1.1f * plume);
            main.startSpeed = new ParticleSystem.MinMaxCurve(7f * spread, 10f * spread);
            main.startColor = new Color(.52f, .47f, .38f, .7f);
            ring.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });
            shape = ring.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .25f; shape.radiusThickness = 0f;
            shape.rotation = (Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(90f, 0f, 0f)).eulerAngles;   // the circle lies on the ground
            Map01Fx.Fade(ring, .05f, .6f, .35f); Map01Fx.Grow(ring, 1f, 2.6f); Map01Fx.Drag(ring, 3f);

            // Pale blast smoke climbs above the brown ground dust. Keep its puffs distinct as they expand.
            var smoke = Map01Fx.System(t, "Smoke", ground + up * .55f, Map01Fx.Lit, 1.1f, 70);
            main = smoke.main;
            main.startDelay = .04f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.3f, 5.2f); main.startSize = new ParticleSystem.MinMaxCurve(.7f * plume, 1.3f * plume);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 2.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.7f, .76f, .82f, .8f), new Color(.85f, .9f, .96f, .7f));
            var emission = smoke.emission; emission.rateOverTime = 48f;
            shape = smoke.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 25f; shape.radius = .75f;
            shape.rotation = Quaternion.FromToRotation(Vector3.forward, up).eulerAngles;
            Map01Fx.Fade(smoke, .05f, .8f, .58f); Map01Fx.Grow(smoke, .8f, 1.8f); Map01Fx.Drag(smoke, .45f);
            Map01Fx.Billow(smoke, .65f, .65f); Map01Fx.Wind(smoke, Map01Fx.Breeze * 1.4f);

            foreach (var ps in GetComponentsInChildren<ParticleSystem>()) ps.Play();

            // the scorch: a flat patch on the ground, a little above it so it does not flicker
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Scorch";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(t, false);
            quad.transform.position = ground + up * .03f;
            quad.transform.rotation = Quaternion.LookRotation(-up) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            quad.transform.localScale = Vector3.one * 2.8f * plume;
            scorch = quad.GetComponent<Renderer>();
            scorch.sharedMaterial = Map01Fx.Burn;
            scorch.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            scorch.receiveShadows = false;
        }

        private void Update()
        {
            float age = Time.time - born;
            if (flash != null)
            {
                flash.intensity = 30f * Mathf.Exp(-age * 26f) * (1f + .25f * Mathf.Sin(age * 160f));
                if (age > .3f) Destroy(flash.gameObject);
            }
            if (scorch != null && age > ScorchSeconds * .75f)
            {
                var block = new MaterialPropertyBlock();
                scorch.GetPropertyBlock(block);
                block.SetColor("_BaseColor", new Color(1f, 1f, 1f, Mathf.Clamp01((ScorchSeconds - age) / (ScorchSeconds * .25f))));
                scorch.SetPropertyBlock(block);
            }
            if (age > ScorchSeconds) Destroy(gameObject);
        }

        /// <summary>Clears every blast (a checkpoint load).</summary>
        public static void ClearAll()
        {
            foreach (var b in FindObjectsByType<Map01Blast>(FindObjectsSortMode.None)) Destroy(b.gameObject);
        }
    }
}
