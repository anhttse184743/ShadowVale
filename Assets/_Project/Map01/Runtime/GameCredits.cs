using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The outro at the end of the game: the last shot fades to black, a short closing verse and the credits scroll slowly
    /// up the screen, then the game returns to the title. Hold Space (or a mouse button) to scroll faster, Esc to skip.
    ///
    /// The text is <c>Resources/Credits/Credits.txt</c>, so the team can change names and roles without touching code:
    /// <c>@logo</c> the wordmark, <c># </c> a title, <c>## </c> a role heading, <c>&gt; </c> a verse line, a blank line a
    /// gap, anything else a name. An optional <c>Resources/Credits/Music</c> clip plays under it.
    /// </summary>
    public sealed class GameCredits : MonoBehaviour
    {
        /// <summary>Test runs skip the outro (it would leave the scene for the title) unless a test asks for it.</summary>
        public static bool AllowInBatchMode;
        public static bool Rolling => current != null;
        private static GameCredits current;

        private enum Kind { Logo, Title, Heading, Verse, Name, Gap }
        private struct Line { public Kind kind; public string text; }

        private const float FadeIn = 2.5f, Hold = 4f, FadeOut = 1.8f, Speed = 52f, FastSpeed = 300f;
        private readonly List<Line> lines = new();
        private float delay, clock, scroll, endedAt = -1, total;
        private bool returnToMenu, loading;
        private Texture2D logo, backdrop;
        private Font font, displayFont;
        private GUIStyle title, heading, verse, name;
        private AudioSource music;

        /// <summary>Starts the outro after <paramref name="after"/> seconds of the final shot. Returns null in a test run.</summary>
        public static GameCredits Roll(float after = 0f, bool toMenu = true)
        {
            if (current != null) return current;
            if (Application.isBatchMode && !AllowInBatchMode) return null;
            var credits = new GameObject("Game credits").AddComponent<GameCredits>();
            credits.delay = after; credits.returnToMenu = toMenu;
            return current = credits;
        }

        /// <summary>The names and headings in reading order, as parsed from the credits file.</summary>
        public IEnumerable<string> Texts { get { foreach (var l in lines) if (!string.IsNullOrEmpty(l.text)) yield return l.text; } }
        public bool Finished => endedAt >= 0 && clock - endedAt >= Hold + FadeOut;
        public float Scroll => scroll;

        private void Awake()
        {
            var file = Resources.Load<TextAsset>("Credits/Credits");
            foreach (var raw in (file != null ? file.text : "# SHADOWVALE").Replace("\r", "").Split('\n'))
            {
                var t = raw.TrimEnd();
                if (t == "@logo") lines.Add(new Line { kind = Kind.Logo });
                else if (t.StartsWith("## ")) lines.Add(new Line { kind = Kind.Heading, text = t.Substring(3).Trim() });
                else if (t.StartsWith("# ")) lines.Add(new Line { kind = Kind.Title, text = t.Substring(2).Trim() });
                else if (t.StartsWith(">")) lines.Add(new Line { kind = t.Trim() == ">" ? Kind.Gap : Kind.Verse, text = t.Substring(1).Trim() });
                else if (t.Length == 0) lines.Add(new Line { kind = Kind.Gap });
                else lines.Add(new Line { kind = Kind.Name, text = t.Trim() });
            }
            while (lines.Count > 0 && lines[^1].kind == Kind.Gap) lines.RemoveAt(lines.Count - 1);
            logo = Resources.Load<Texture2D>("Menu/Wordmark");
            backdrop = Resources.Load<Texture2D>("Menu/Background");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            displayFont = Resources.Load<Font>("Menu/Fonts/BlackOpsOne-Regular") ?? font;
            var clip = Resources.Load<AudioClip>("Credits/Music");
            if (clip != null) { music = gameObject.AddComponent<AudioSource>(); music.clip = clip; music.spatialBlend = 0; music.volume = 0; music.ignoreListenerPause = true; }
        }

        private void OnDestroy() { if (current == this) current = null; }

        // Heights at 1080p; scaled with the screen.
        private static float Height(Kind kind) => kind switch
        {
            Kind.Logo => 260, Kind.Title => 110, Kind.Heading => 78, Kind.Verse => 50, Kind.Name => 50, _ => 44,
        };

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (delay > 0) { delay -= dt; return; }
            clock += dt;
            if (music != null) { if (!music.isPlaying) music.Play(); music.volume = Mathf.Clamp01(clock / FadeIn) * (endedAt >= 0 ? 1 - Mathf.Clamp01((clock - endedAt - Hold) / FadeOut) : 1) * .8f; }
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (endedAt < 0 && clock > FadeIn * .5f && kb != null && kb.escapeKey.wasPressedThisFrame) { endedAt = clock - Hold; }
            if (endedAt < 0 && clock > FadeIn)
            {
                bool fast = kb != null && (kb.spaceKey.isPressed || kb.enterKey.isPressed) || mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed);
                scroll += (fast ? FastSpeed : Speed) * dt;
                if (total > 0 && scroll >= total) { scroll = total; endedAt = clock; }
            }
            if (Finished && returnToMenu && !loading)
            {
                loading = true; Time.timeScale = 1; ForestMenu.SuppressKeysAfterCutscene();
                SceneManager.LoadScene("01_MainMenu");
            }
        }

        private void Styles(float s)
        {
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { font = displayFont, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                heading = new GUIStyle(title);
                verse = new GUIStyle(title) { font = font, fontStyle = FontStyle.Italic };
                name = new GUIStyle(title) { font = font };
            }
            title.fontSize = Mathf.RoundToInt(72 * s); heading.fontSize = Mathf.RoundToInt(30 * s);
            verse.fontSize = Mathf.RoundToInt(30 * s); name.fontSize = Mathf.RoundToInt(36 * s);
        }

        private void OnGUI()
        {
            if (delay > 0) return;
            GUI.depth = -1000;   // above every HUD
            float w = Screen.width, h = Screen.height, s = h / 1080f;
            Styles(s);
            var tint = GUI.color;
            float black = Mathf.Clamp01(clock / FadeIn);
            GUI.color = new Color(0, 0, 0, black); GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            if (backdrop != null)
            {
                // The title painting, very dark behind the text, like a film's end card.
                float cover = Mathf.Max(w / backdrop.width, h / backdrop.height);
                var size = new Vector2(backdrop.width * cover, backdrop.height * cover);
                GUI.color = new Color(.32f, .3f, .27f, black * .55f);
                GUI.DrawTexture(new Rect((w - size.x) * .5f, (h - size.y) * .5f, size.x, size.y), backdrop, ScaleMode.StretchToFill);
            }

            float text = Mathf.Clamp01((clock - FadeIn * .6f) / 1.2f);
            if (endedAt >= 0) text *= 1 - Mathf.Clamp01((clock - endedAt - Hold) / FadeOut);
            // The block starts below the screen and stops when its last line reaches the middle.
            float sum = 0; foreach (var l in lines) sum += Height(l.kind) * s;
            total = sum + h * .5f - Height(lines.Count > 0 ? lines[^1].kind : Kind.Gap) * s * .5f;
            float y = h - scroll, column = Mathf.Min(w - 80 * s, 1100 * s), x = (w - column) * .5f;
            foreach (var l in lines)
            {
                float lh = Height(l.kind) * s;
                if (y + lh > 0 && y < h)
                {
                    // Lines fade in at the bottom edge and out at the top, so nothing pops.
                    float edge = Mathf.Clamp01(Mathf.Min(y + lh, h - y) / (120 * s));
                    switch (l.kind)
                    {
                        case Kind.Logo when logo != null:
                            float lw = Mathf.Min(column, 900 * s), lhh = lw * logo.height / logo.width;
                            GUI.color = new Color(1, 1, 1, text * edge);
                            GUI.DrawTexture(new Rect((w - lw) * .5f, y + (lh - lhh) * .5f, lw, lhh), logo, ScaleMode.ScaleToFit);
                            break;
                        case Kind.Title: Draw(title, l.text, new Color(.93f, .86f, .66f), x, y, column, lh, text * edge); break;
                        case Kind.Heading: Draw(heading, l.text.ToUpperInvariant(), new Color(.72f, .66f, .5f), x, y, column, lh, text * edge); break;
                        case Kind.Verse: Draw(verse, l.text, new Color(.86f, .84f, .78f), x, y, column, lh, text * edge); break;
                        case Kind.Name: Draw(name, l.text, new Color(.97f, .96f, .92f), x, y, column, lh, text * edge); break;
                    }
                }
                y += lh;
            }
            if (endedAt < 0 && clock > FadeIn)
            {
                GUI.color = new Color(.7f, .68f, .62f, .55f * text);
                var hint = new GUIStyle(name) { fontSize = Mathf.RoundToInt(18 * s), fontStyle = FontStyle.Normal, alignment = TextAnchor.LowerRight };
                GUI.Label(new Rect(0, 0, w - 28 * s, h - 22 * s), "Giữ Space để cuộn nhanh · Esc để bỏ qua", hint);
            }
            GUI.color = tint;
        }

        private static void Draw(GUIStyle style, string text, Color color, float x, float y, float width, float height, float alpha)
        {
            color.a = alpha; GUI.color = Color.white; style.normal.textColor = color;
            GUI.Label(new Rect(x, y, width, height), text, style);
        }
    }
}
