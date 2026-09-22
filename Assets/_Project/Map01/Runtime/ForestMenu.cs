using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShadowVale.Map01
{
    /// <summary>Shared title, pause and checkpoint browser for the playable Map 1 prototype.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class ForestMenu : MonoBehaviour
    {
        private static ForestMenu instance;
        public static bool Visible => instance != null && instance.visible;
        private bool visible, browser, saving, quitAuthorized;
        private int selected, latest;
        private ForestMission mission;
        private Texture2D background, buttonPlate, wordmark;
        private Font font, displayFont;
        private GUIStyle title, heading, body, small, button, mainButton, caption, pointer;
        // Generated PNGs retain alpha padding. UVs select the artwork without modifying the source images.
        private static readonly Rect PlateUv = new Rect(12f / 2172, 152f / 724, 2148f / 2172, 462f / 724);
        private static readonly Rect WordmarkUv = new Rect(18f / 2172, 168f / 724, 2136f / 2172, 406f / 724);
        private readonly ForestSaveSlots.Entry[] entries = new ForestSaveSlots.Entry[ForestSaveSlots.Count];
        private readonly Texture2D[] previews = new Texture2D[ForestSaveSlots.Count];
        private readonly bool[] damaged = new bool[ForestSaveSlots.Count];
        private string message, question;
        private Action confirmed;
        private static readonly Color Ink = new Color(.09f, .12f, .075f, .96f);
        private static readonly Color Paper = new Color(.89f, .83f, .63f);
        private static readonly Color Gold = new Color(.8f, .65f, .28f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void EnsureCreated()
        {
            if (instance != null) return;
            var existing = FindFirstObjectByType<ForestMenu>();
            if (existing != null) { existing.OnEnable(); return; }
            var go = new GameObject("ShadowVale Menu");
            go.AddComponent<ForestMenu>();
        }
        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this; DontDestroyOnLoad(gameObject);
        }
        private void OnEnable()
        {
            if (!Application.isPlaying || (instance != null && instance != this)) return;
            instance = this;
            background = Resources.Load<Texture2D>("Menu/Background");
            buttonPlate = Resources.Load<Texture2D>("Menu/ButtonPlate");
            wordmark = Resources.Load<Texture2D>("Menu/Wordmark");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            displayFont = Resources.Load<Font>("Menu/Fonts/BlackOpsOne-Regular") ?? font;
            body = small = heading = title = button = mainButton = caption = pointer = null;
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
            Application.wantsToQuit -= WantsToQuit;
            Application.wantsToQuit += WantsToQuit;
            BindScene();
        }
        private void OnScene(Scene scene, LoadSceneMode mode) => BindScene();
        private void BindScene()
        {
            mission = FindFirstObjectByType<ForestMission>();
            var name = SceneManager.GetActiveScene().name;
            // A blank/unsaved editor scene has no gameplay. It must show the title rather than just its skybox.
            visible = mission == null || mission.Paused;
            browser = saving = quitAuthorized = false; selected = 0; question = message = null; confirmed = null;
            Time.timeScale = mission != null && mission.Paused ? 0 : 1;
            Refresh();
            if (mission == null && latest < 0) selected = 1;
            if (name == "00_Boot") Invoke(nameof(GoToTitle), .01f);
        }
        private void GoToTitle() { if (!SaveBeforeExit()) return; Time.timeScale = 1; SceneManager.LoadScene("01_MainMenu"); }
        private bool SaveBeforeExit()
        {
            if (mission == null) return true;
            if (mission.AutoSaveOnExit(out var error)) return true;
            visible = true; mission.SetPaused(true); message = error;
            return false;
        }
        private bool WantsToQuit() => quitAuthorized || SaveBeforeExit();
        private void Refresh()
        {
            latest = ForestSaveSlots.Latest();
            for (int i = 0; i < ForestSaveSlots.Count; i++) {
                if (previews[i] != null) Destroy(previews[i]);
                previews[i] = null; entries[i] = null; damaged[i] = false;
                try {
                    entries[i] = ForestSaveSlots.Read(i);
                    if (!string.IsNullOrEmpty(entries[i]?.thumbnail)) {
                        var texture = new Texture2D(2, 2);
                        if (texture.LoadImage(Convert.FromBase64String(entries[i].thumbnail))) previews[i] = texture;
                        else Destroy(texture);
                    }
                } catch (Exception) { damaged[i] = true; }
            }
        }
        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (visible && question != null) {
                if (kb.escapeKey.wasPressedThisFrame) { question = null; confirmed = null; }
                else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Confirm();
                return;
            }
            if (kb.escapeKey.wasPressedThisFrame) {
                if (question != null) { question = null; confirmed = null; }
                else if (browser) { browser = false; selected = 0; }
                else if (mission != null) { visible = !visible; mission.SetPaused(visible); Refresh(); }
            }
            if (!visible || question != null) return;
            int count = browser ? ForestSaveSlots.Count : mission != null ? 5 : 4;
            if (kb.downArrowKey.wasPressedThisFrame) selected = (selected + 1) % count;
            if (kb.upArrowKey.wasPressedThisFrame) selected = (selected + count - 1) % count;
            if (kb.enterKey.wasPressedThisFrame) { if (browser) SlotAction(); else MainAction(selected); }
            if (browser && kb.deleteKey.wasPressedThisFrame) DeleteSelected();
        }
        private void Ask(string text, Action action) { question = text; confirmed = action; }
        private void Confirm() { var action = confirmed; question = null; confirmed = null; action?.Invoke(); }
        private void Resume() { visible = false; mission.SetPaused(false); }
        private void OpenSlots(bool save) { browser = true; saving = save; message = null; Refresh(); selected = save ? 1 : Mathf.Max(0, latest); }
        private void StartGame(int slot)
        {
            try { ForestMission.BeginGame(slot); }
            catch (Exception e) { message = "Không thể mở bản lưu: " + e.Message; }
        }
        private void MainAction(int index)
        {
            if (mission == null) {
                if (index == 0 && latest >= 0) StartGame(latest);
                if (index == 1) StartGame(-1);
                if (index == 2) OpenSlots(false);
                if (index == 3) Ask("Thoát game?", Quit);
            } else {
                if (index == 0) Resume();
                if (index == 1) OpenSlots(true);
                if (index == 2) OpenSlots(false);
                if (index == 3) Ask("Tự động lưu và về menu chính?", GoToTitle);
                if (index == 4) Ask("Tự động lưu và thoát game?", Quit);
            }
        }
        private void SlotAction()
        {
            int slot = selected;
            if (saving) {
                if (mission == null) { message = "Hãy vào màn chơi trước khi lưu."; return; }
                if (slot == 0 || slot == ForestSaveSlots.AutoSlot) { message = "Chọn Ô lưu 01, 02 hoặc 03 để lưu backup thủ công."; return; }
                if (!mission.CanSave()) { message = mission.ManualSaveBlockReason(); return; }
                if (ForestSaveSlots.Exists(slot)) Ask("Ghi đè " + ForestSaveSlots.Title(slot) + "?", () => Save(slot));
                else Save(slot);
            } else if (entries[slot] != null && !damaged[slot]) {
                if (mission != null) Ask("Tải bản lưu? Tiến trình chưa lưu sẽ mất.", () => StartGame(slot));
                else StartGame(slot);
            }
        }
        private void Save(int slot)
        {
            message = mission.SaveSlot(slot, out var error) ? "Đã lưu vào " + ForestSaveSlots.Title(slot) + ". Bạn có thể chuyển sang Tải game để kiểm tra." : error;
            Refresh();
        }
        private void DeleteSelected()
        {
            int slot = selected;
            if (!ForestSaveSlots.Exists(slot)) return;
            Ask("Xóa " + ForestSaveSlots.Title(slot) + "? Không thể hoàn tác.", () => {
                try { ForestSaveSlots.Delete(slot); message = "Đã xóa bản lưu."; Refresh(); }
                catch (Exception e) { message = "Không thể xóa: " + e.Message; }
            });
        }
        private void Quit()
        {
            if (!SaveBeforeExit()) return;
            quitAuthorized = true;
            Time.timeScale = 1;
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
        private void Styles()
        {
            if (body != null) return;
            body = new GUIStyle(GUI.skin.label) { font = font, fontSize = 23, wordWrap = true, normal = { textColor = Paper } };
            small = new GUIStyle(body) { fontSize = 21 };
            heading = new GUIStyle(body) { font = displayFont, fontSize = 29, fontStyle = FontStyle.Normal };
            title = new GUIStyle(heading) { fontSize = 85, normal = { textColor = Ink } };
            button = new GUIStyle(body) { font = displayFont, alignment = TextAnchor.MiddleCenter, fontSize = 25, fontStyle = FontStyle.Normal, wordWrap = false };
            mainButton = new GUIStyle(button) { fontSize = 56 };
            caption = new GUIStyle(button) { fontSize = 23, normal = { textColor = Ink } };
            pointer = new GUIStyle(body) { fontSize = 35, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1, .86f, .43f) } };
        }
        private static void Fill(Rect r, Color c) { var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old; }
        private static void Frame(Rect r, Color color)
        {
            Fill(new Rect(r.x, r.y, r.width, 2), color); Fill(new Rect(r.x, r.yMax - 2, r.width, 2), color);
            Fill(new Rect(r.x, r.y, 2, r.height), color); Fill(new Rect(r.xMax - 2, r.y, 2, r.height), color);
        }
        private void Plate(Rect r, bool highlighted = false, bool pressed = false, bool enabled = true)
        {
            if (buttonPlate == null) { Fill(r, Ink); Frame(r, highlighted ? Gold : Paper); return; }
            var old = GUI.color;
            GUI.color = pressed ? new Color(.65f, .62f, .44f, 1) : !enabled ? new Color(.64f, .67f, .61f, 1) : Color.white;
            DrawPlateTexture(r);
            GUI.color = old;
            if (highlighted && enabled) {
                var inner = new Rect(r.x + 8, r.y + 8, r.width - 16, r.height - 16);
                Fill(inner, new Color(.75f, .59f, .12f, pressed ? .18f : .32f));
                Frame(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), new Color(.9f, .72f, .32f, .85f));
            }
        }
        private void DrawPlateTexture(Rect r)
        {
            // Nine-slice preserves the round rivets on short buttons and wide footer plates.
            float edge = Mathf.Min(24, r.height * .26f);
            float uEdge = 136f / 2172, vEdge = 136f / 724;
            for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) {
                float dx = x == 0 ? r.x : x == 1 ? r.x + edge : r.xMax - edge;
                float dy = y == 0 ? r.y : y == 1 ? r.y + edge : r.yMax - edge;
                float dw = x == 1 ? r.width - edge * 2 : edge;
                float dh = y == 1 ? r.height - edge * 2 : edge;
                float u = x == 0 ? PlateUv.x : x == 1 ? PlateUv.x + uEdge : PlateUv.xMax - uEdge;
                float v = y == 0 ? PlateUv.yMax - vEdge : y == 1 ? PlateUv.y + vEdge : PlateUv.y;
                float uw = x == 1 ? PlateUv.width - uEdge * 2 : uEdge;
                float vh = y == 1 ? PlateUv.height - vEdge * 2 : vEdge;
                GUI.DrawTextureWithTexCoords(new Rect(dx, dy, dw, dh), buttonPlate, new Rect(u, v, uw, vh), true);
            }
        }
        private bool Button(Rect r, string text, bool active = false, bool enabled = true, bool prominent = false)
        {
            bool hover = r.Contains(Event.current.mousePosition) && GUI.enabled && enabled;
            bool pressed = hover && Mouse.current != null && Mouse.current.leftButton.isPressed;
            bool highlighted = (active || hover) && enabled;
            Plate(r, highlighted, pressed, enabled);
            var label = prominent ? mainButton : button;
            // Fit long Vietnamese labels without clipping accents or touching the rivets.
            int size = label.fontSize;
            while (label.CalcSize(new GUIContent(text)).x > r.width - (prominent ? 100 : 38) && label.fontSize > 15) label.fontSize--;
            var textRect = new Rect(r.x + (prominent ? 30 : 12), r.y + (pressed ? 2 : 0), r.width - (prominent ? 60 : 24), r.height);
            var old = GUI.color;
            GUI.color = !enabled ? new Color(1, 1, 1, .48f) : highlighted ? new Color(1, .94f, .72f) : Color.white;
            GUI.Label(textRect, text, label);
            GUI.color = old; label.fontSize = size;
            if (prominent && highlighted) GUI.Label(new Rect(r.x + 36, r.y + 2, 40, r.height - 4), "▶", pointer);
            bool previous = GUI.enabled; GUI.enabled = previous && enabled;
            bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none); GUI.enabled = previous; return clicked;
        }
        private void OnGUI()
        {
            if (!visible) return;
            Styles(); GUI.depth = -100;
            var matrix = GUI.matrix; var color = GUI.color; GUI.color = Color.white;
            Fill(new Rect(0, 0, Screen.width, Screen.height), Color.black);
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1600 * scale) / 2, (Screen.height - 900 * scale) / 2), Quaternion.identity, new Vector3(scale, scale, 1));
            if (background != null) GUI.DrawTexture(new Rect(0, 0, 1600, 900), background, ScaleMode.ScaleAndCrop);
            else Fill(new Rect(0, 0, 1600, 900), Ink);
            GUI.enabled = question == null;
            if (browser) DrawBrowser(); else DrawMain();
            GUI.enabled = true;
            if (question != null) {
                Fill(new Rect(0, 0, 1600, 900), new Color(0, 0, 0, .7f));
                Fill(new Rect(430, 295, 740, 290), Ink); Frame(new Rect(430, 295, 740, 290), Gold);
                GUI.Label(new Rect(470, 330, 660, 110), question, heading);
                if (Button(new Rect(470, 475, 310, 65), "XÁC NHẬN", true)) Confirm();
                if (Button(new Rect(820, 475, 310, 65), "HỦY")) { question = null; confirmed = null; }
            }
            GUI.matrix = matrix; GUI.color = color;
        }
        private void DrawMain()
        {
            if (wordmark != null) GUI.DrawTextureWithTexCoords(new Rect(75, 48, 710, 134), wordmark, WordmarkUv, true);
            else GUI.Label(new Rect(75, 48, 730, 134), "SHADOWVALE", title);
            Fill(new Rect(145, 201, 150, 2), Ink); Fill(new Rect(560, 201, 150, 2), Ink);
            GUI.Label(new Rect(300, 180, 255, 45), mission != null ? "TẠM DỪNG" : "MENU CHÍNH", caption);
            var labels = mission == null ? new[] { "TIẾP TỤC", "CHƠI MỚI", "TẢI GAME", "THOÁT GAME" }
                : new[] { "TIẾP TỤC", "LƯU GAME", "TẢI GAME", "MENU CHÍNH", "THOÁT GAME" };
            for (int i = 0; i < labels.Length; i++)
                if (Button(new Rect(115, 244 + (mission == null ? 115 : 92) * i, 535, mission == null ? 102 : 82), labels[i], selected == i, !(mission == null && i == 0 && latest < 0), true)) { selected = i; MainAction(i); }
            string summary = latest >= 0 && entries[latest] != null ? "Điểm lưu: " + entries[latest].location + "\nThời gian chơi: " + ForestSaveSlots.Duration(entries[latest].playSeconds) : "Chưa có bản lưu";
            Plate(new Rect(115, 737, 535, 72));
            GUI.Label(new Rect(143, 752, 480, 54), message ?? summary, small);
            Plate(new Rect(75, 833, 685, 48));
            GUI.Label(new Rect(110, 846, 640, 32), "↑ ↓ Chọn     Enter Xác nhận     Esc Quay lại", small);
        }
        private void DrawBrowser()
        {
            Fill(new Rect(35, 55, 480, 245), Ink);
            GUI.Label(new Rect(60, 85, 450, 80), "SHADOWVALE", heading);
            GUI.Label(new Rect(60, 170, 440, 45), "LƯU / TẢI GAME", heading);
            if (Button(new Rect(60, 230, 420, 55), "QUAY LẠI")) { browser = false; selected = 0; }
            Fill(new Rect(535, 50, 1025, 790), Ink); Frame(new Rect(535, 50, 1025, 790), Gold);
            GUI.Label(new Rect(575, 75, 900, 45), "DỮ LIỆU ĐÃ LƯU", heading);
            if (Button(new Rect(575, 135, 310, 55), "TẢI GAME", !saving)) saving = false;
            if (mission != null && Button(new Rect(900, 135, 310, 55), "LƯU GAME", saving)) OpenSlots(true);
            for (int i = 0; i < ForestSaveSlots.Count; i++) {
                var r = new Rect(575, 210 + i * 104, 945, 96);
                Fill(r, selected == i ? new Color(.31f, .33f, .18f) : new Color(.12f, .15f, .10f)); Frame(r, selected == i ? Gold : new Color(.3f, .32f, .23f));
                if (previews[i] != null) GUI.DrawTexture(new Rect(r.x + 4, r.y + 4, 192, 88), previews[i], ScaleMode.ScaleAndCrop);
                else GUI.Label(new Rect(r.x + 60, r.y + 33, 130, 50), damaged[i] ? "!" : "+", heading);
                var e = entries[i];
                GUI.Label(new Rect(r.x + 215, r.y + 6, 440, 40), ForestSaveSlots.Title(i), i == ForestSaveSlots.AutoSlot ? body : heading);
                GUI.Label(new Rect(r.x + 215, r.y + 47, 430, 45), damaged[i] ? "Bản lưu bị hỏng · Có thể xóa" : e?.location ?? "Ô TRỐNG", body);
                if (e != null) GUI.Label(new Rect(r.x + 655, r.y + 24, 280, 85), DateTime.Parse(e.savedAt).ToLocalTime().ToString("dd/MM/yyyy HH:mm") + "\n" + ForestSaveSlots.Duration(e.playSeconds), small);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) selected = i;
            }
            bool canAct = saving ? mission != null : entries[selected] != null && !damaged[selected];
            if (Button(new Rect(575, 750, 360, 60), saving ? "LƯU VÀO Ô NÀY" : "TẢI BẢN LƯU", true, canAct)) SlotAction();
            if (Button(new Rect(960, 750, 250, 60), "XÓA", false, ForestSaveSlots.Exists(selected))) DeleteSelected();
            if (Button(new Rect(1235, 750, 285, 60), "QUAY LẠI")) { browser = false; selected = 0; }
            Fill(new Rect(535, 842, 1025, 52), Ink);
            GUI.Label(new Rect(575, 850, 980, 45), message ?? (saving ? mission?.ManualSaveBlockReason() ?? "Chọn Ô lưu 01–03, rồi bấm Lưu vào ô này. Enter xác nhận ghi đè." : "↑ ↓ Chọn ô     Enter Tải     Delete Xóa     Esc Quay lại"), small);
        }
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnScene;
            Application.wantsToQuit -= WantsToQuit;
            foreach (var texture in previews) if (texture != null) Destroy(texture);
            if (instance == this) instance = null;
        }
    }
}
