using System.Collections.Generic;
using System.Linq;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Coordination;
using ShadowVale.AI.Perception;
using ShadowVale.Core.Services;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Enemies.Boss;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Debugging
{
    /// <summary>
    /// Dev-build AI console. F8 opens it: a log of what the enemies decide (state changes,
    /// contacts, noises, takedowns; squad plans and solver results once the coordinator lands),
    /// a live table of every enemy, and buttons to provoke situations while testing.
    /// F7 toggles the world overlay (<see cref="AiDebugOverlay"/>). Created automatically in the
    /// editor and development builds only — the assembly does not exist in release builds.
    /// </summary>
    public sealed class AiDebugConsole : MonoBehaviour
    {
        private const int WindowId = 0x5A1D;
        private static readonly string[] Tabs = { "Nhật ký", "Lính" };
        private static readonly string[] Variants = { "greedy", "ga", "sa", "sqa", "qiea", "qaoa" };

        public static bool Visible { get; private set; }

        private readonly AiConsoleLog _log = new AiConsoleLog();
        private AiConsoleFeed _feed;
        // Laid out in "virtual" pixels at 720p and scaled up to the screen (GUI.matrix), so it reads
        // the same on a 1080p laptop and a 4K Game view. Placed on the first open, right of the
        // minimap and the objective panel; drag the title to move it, the corner to resize it.
        private const float ReferenceHeight = 720f;
        private const string ScalePref = "svl.aiconsole.scale";
        private Rect _window;
        private bool _placed, _resizing;
        private float _userScale = 1f;
        private GUIStyle _windowStyle;
        private Texture2D _background;
        private int _labelSize, _buttonSize, _toggleSize;
        private int _tab;
        private Vector2 _logScroll, _agentScroll;
        private int _seenVersion = -1;
        private readonly bool[] _showCategory = { true, true, true, true, true, true, true };
        private string _squadFilter = "";
        private string _selectedAgent;
        private bool _playerInvulnerable, _gamePaused;
        private string _status = "";
        private GUIStyle _mono;

        private void Awake()
        {
            _feed = new AiConsoleFeed(_log);
            _userScale = Mathf.Clamp(PlayerPrefs.GetFloat(ScalePref, 1f), .6f, 2.5f);
        }

        /// <summary>Screen pixels per virtual pixel: the screen's height against 720p, times the user's A−/A+.</summary>
        private float UiScale => Mathf.Max(1f, Screen.height / ReferenceHeight) * _userScale;

        private void OnDestroy()
        {
            _feed?.Dispose();
            if (Visible) SetVisible(false);
            if (_background != null) Destroy(_background);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f8Key.wasPressedThisFrame) SetVisible(!Visible);
                if (kb.f7Key.wasPressedThisFrame) AiDebugOverlay.Enabled = !AiDebugOverlay.Enabled;
            }
            if (_playerInvulnerable && TryPlayerHealth(out var health) && health.Current < health.Max)
                health.RestoreHealth(health.Max);
        }

        private void LateUpdate()
        {
            if (!Visible) return;
            // The game re-captures the mouse on click; while the console is open it stays free.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void SetVisible(bool visible)
        {
            Visible = visible;
            InputFocus.OverlayOpen = visible;
            if (!visible && _gamePaused) { _gamePaused = false; Time.timeScale = 1f; }
        }

        private void OnGUI()
        {
            if (!Visible) return;
            EnsureStyles();
            float scale = UiScale;
            float width = Screen.width / scale, height = Screen.height / scale;
            if (!_placed)
            {
                // Right half, below the top HUD line: clear of the minimap and the objective panel.
                _window = new Rect(width * .49f, 56f, width * .49f, height * .62f);
                _placed = true;
            }
            _window.width = Mathf.Clamp(_window.width, 420f, width - 8f);
            _window.height = Mathf.Clamp(_window.height, 240f, height - 8f);
            _window.x = Mathf.Clamp(_window.x, 0f, width - 60f);
            _window.y = Mathf.Clamp(_window.y, 0f, height - 30f);
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            _window = GUI.Window(WindowId, _window, DrawWindow, "AI Debug Console — F8 đóng · F7 overlay", _windowStyle);
            GUI.matrix = previous;
        }

        private void EnsureStyles()
        {
            if (_windowStyle != null) return;
            _background = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            _background.SetPixel(0, 0, new Color(.06f, .08f, .10f, .94f));
            _background.Apply();
            _windowStyle = new GUIStyle(GUI.skin.window) { fontSize = 15, fontStyle = FontStyle.Bold, padding = new RectOffset(10, 10, 28, 10) };
            _windowStyle.normal.background = _windowStyle.onNormal.background = _background;
            _windowStyle.normal.textColor = _windowStyle.onNormal.textColor = new Color(.95f, .85f, .55f);
            _mono = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = false, fontSize = 14 };
            _mono.normal.textColor = new Color(.88f, .9f, .92f);
            // Monospaced, so the columns of the Lính tab line up.
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Cascadia Mono", "Courier New" }, 14);
            if (font != null) _mono.font = font;
        }

        private void DrawWindow(int id)
        {
            // The shared skin is the game HUD's too: bigger only inside this window, restored after.
            _labelSize = GUI.skin.label.fontSize; _buttonSize = GUI.skin.button.fontSize; _toggleSize = GUI.skin.toggle.fontSize;
            GUI.skin.label.fontSize = GUI.skin.button.fontSize = GUI.skin.toggle.fontSize = 14;
            try
            {
                DrawHeader();
                _tab = GUILayout.Toolbar(_tab, Tabs, GUILayout.Height(26));
                if (_tab == 0) DrawLog(); else DrawAgents();
                if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status, _mono);
            }
            finally
            {
                GUI.skin.label.fontSize = _labelSize; GUI.skin.button.fontSize = _buttonSize; GUI.skin.toggle.fontSize = _toggleSize;
            }
            DrawResizeGrip();
            GUI.DragWindow(new Rect(0, 0, 10000, 26));
        }

        /// <summary>The ◢ in the bottom-right corner: drag it to resize the window.</summary>
        private void DrawResizeGrip()
        {
            var grip = new Rect(_window.width - 20f, _window.height - 20f, 20f, 20f);
            GUI.Label(grip, "◢", _mono);
            var e = Event.current;
            if (e.type == EventType.MouseDown && grip.Contains(e.mousePosition)) { _resizing = true; e.Use(); }
            else if (_resizing && e.type == EventType.MouseDrag)
            {
                _window.width += e.delta.x; _window.height += e.delta.y;
                e.Use();
            }
            else if (e.rawType == EventType.MouseUp) _resizing = false;
        }

        private void SetUserScale(float value)
        {
            _userScale = Mathf.Clamp(value, .6f, 2.5f);
            try { PlayerPrefs.SetFloat(ScalePref, _userScale); } catch (System.Exception) { /* Not worth failing over. */ }
        }

        private void DrawHeader()
        {
            string solver = ServiceLocator.TryResolve<SquadCoordinator>(out var coordinator)
                ? $"{coordinator.Solver.VariantId} (budget {coordinator.Solver.LatencyBudgetMs} ms)"
                  + (coordinator.SolverFallbackReason != null ? " ⚠" : "")
                  + (coordinator.SidecarOnline == true ? " · sidecar online" : coordinator.SidecarOnline == false ? " · sidecar OFF" : "")
                : ServiceLocator.TryResolve<ISquadSolver>(out var s) ? $"{s.VariantId} (chưa có coordinator)" : "chưa đăng ký";
            string graph = ServiceLocator.TryResolve<IWorldQuery>(out var g)
                ? $"{g.NodeCount} node{(g.VisibilityReady ? "" : " (đang tính tầm nhìn…)")}" : "chưa bake";
            int alive = AgentRegistry.All.Count(a => a.Alive);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Solver: <b>{solver}</b>   Graph: {graph}   Lính: {alive}/{AgentRegistry.All.Count}   Overlay: {(AiDebugOverlay.Enabled ? "bật" : "tắt")}", _mono);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("A−", GUILayout.Width(34))) SetUserScale(_userScale - .1f);
            if (GUILayout.Button("A+", GUILayout.Width(34))) SetUserScale(_userScale + .1f);
            bool paused = GUILayout.Toggle(_gamePaused, "Dừng game");
            if (paused != _gamePaused) { _gamePaused = paused; Time.timeScale = paused ? 0f : 1f; }
            _playerInvulnerable = GUILayout.Toggle(_playerInvulnerable, "Nam bất tử");
            AiDebugOverlay.ShowGraph = GUILayout.Toggle(AiDebugOverlay.ShowGraph, "Graph");
            GUILayout.EndHorizontal();
        }

        // ---- Log tab -----------------------------------------------------------------------------

        private void DrawLog()
        {
            GUILayout.BeginHorizontal();
            var names = System.Enum.GetNames(typeof(AiLogCategory));
            for (int i = 0; i < names.Length; i++) _showCategory[i] = GUILayout.Toggle(_showCategory[i], names[i]);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Squad:", GUILayout.Width(48));
            foreach (var squad in SquadChoices())
                if (GUILayout.Toggle(_squadFilter == squad, squad.Length == 0 ? "Tất cả" : squad, GUI.skin.button, GUILayout.Width(62)))
                    _squadFilter = squad;
            GUILayout.FlexibleSpace();
            _feed.VerboseNoise = GUILayout.Toggle(_feed.VerboseNoise, "Mọi bước chân");
            _log.Paused = GUILayout.Toggle(_log.Paused, "Tạm dừng log");
            if (GUILayout.Button("Xoá", GUILayout.Width(50))) _log.Clear();
            if (GUILayout.Button("Xuất", GUILayout.Width(50))) _status = "Đã lưu: " + _log.Export();
            GUILayout.EndHorizontal();

            if (_log.Version != _seenVersion && !_log.Paused) { _logScroll.y = float.MaxValue; _seenVersion = _log.Version; }
            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < _log.Count; i++)
            {
                var e = _log[i];
                if (!_showCategory[(int)e.Category]) continue;
                if (_squadFilter.Length > 0 && System.Array.IndexOf(e.Squad.Split('+'), _squadFilter) < 0) continue;
                GUILayout.Label($"<color=#{ColorOf(e.Category)}>{Escape(e.ToString())}</color>", _mono);
            }
            GUILayout.EndScrollView();
        }

        private IEnumerable<string> SquadChoices()
        {
            yield return "";
            foreach (var squad in AgentRegistry.All.Select(a => a.SquadId).Where(q => !string.IsNullOrEmpty(q)).Distinct().OrderBy(q => q))
                yield return squad;
        }

        private static string ColorOf(AiLogCategory c) => c switch
        {
            AiLogCategory.State => "c8d0d8",
            AiLogCategory.Noise => "e8d27a",
            AiLogCategory.Contact => "ff7a59",
            AiLogCategory.Combat => "ff5a7a",
            AiLogCategory.Coordination => "6ad0ff",
            AiLogCategory.Solver => "9be08a",
            _ => "d79bff",
        };

        private static string Escape(string s) => s.Replace("<", "‹").Replace(">", "›");

        // ---- Agents tab --------------------------------------------------------------------------

        private void DrawAgents()
        {
            GUILayout.Label("<b>Id                 Squad  State        HP    Nghi   Giao tranh  Thấy  Vị trí được giao</b>", _mono);
            _agentScroll = GUILayout.BeginScrollView(_agentScroll, GUILayout.Height(Mathf.Max(120f, _window.height - 250f)));
            float now = Time.time;
            foreach (var a in AgentRegistry.All.OrderBy(a => a.SquadId).ThenBy(a => a.Id))
            {
                var m = a.Brain.Memory;
                string assignment = m.HasAssignment
                    ? $"{m.AssignedRole} {(m.AssignedNode >= 0 ? "node " + m.AssignedNode : "")}{(m.ReachedAssignment ? " ✓" : "")}" : "—";
                string row = string.Format("{0,-18} {1,-6} {2,-12} {3,4:0}% {4,5:0.00}  {5,-10}  {6,-4}  {7}",
                    Trim(a.Id, 18), a.SquadId, a.Alive ? a.Brain.State.ToString() : "Down", a.HealthFraction * 100f,
                    m.Suspicion, m.Engaged(now) ? "có" : "", a.SeesPlayer ? "có" : "", assignment);
                bool selected = _selectedAgent == a.Id;
                if (GUILayout.Toggle(selected, Escape(row), _mono) && !selected) _selectedAgent = a.Id;
            }
            GUILayout.EndScrollView();

            var agent = _selectedAgent != null ? AgentRegistry.Find(_selectedAgent) : null;
            GUILayout.Label(agent != null ? $"Đang chọn: <b>{agent.Id}</b>" : "Chọn một lính trong bảng.", _mono);
            GUILayout.BeginHorizontal();
            GUI.enabled = agent != null && agent.Alive;
            if (GUILayout.Button("Ép giao tranh 10 s")) { agent.DebugEngage(10f); _status = agent.Id + " đang giao tranh."; }
            if (GUILayout.Button("Trừ 50% HP (thử Retreat)")) { agent.DebugDamage(.5f); _status = $"{agent.Id}: HP {agent.HealthFraction:P0}."; }
            GUI.enabled = true;
            if (GUILayout.Button("Tiếng động tại Nam (12 m)")) EmitNoiseAtPlayer(12f);
            GUILayout.EndHorizontal();

            ServiceLocator.TryResolve<SquadCoordinator>(out var coordinator);
            GUILayout.BeginHorizontal();
            GUI.enabled = coordinator != null && agent != null && !string.IsNullOrEmpty(agent.SquadId);
            if (GUILayout.Button("Ép re-plan squad của lính này")) { coordinator.RequestReplan(agent.SquadId); _status = "Re-plan " + agent.SquadId; }
            GUI.enabled = coordinator != null;
            GUILayout.Label("Solver:", GUILayout.Width(46));
            foreach (var id in Variants)
                if (GUILayout.Button(id, GUILayout.Width(56))) { coordinator.SetVariant(id); _status = "Solver → " + coordinator.Solver.VariantId + (coordinator.SolverFallbackReason != null ? " (" + coordinator.SolverFallbackReason + ")" : ""); }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (coordinator?.RecordPath != null) GUILayout.Label("Ghi instance: " + coordinator.RecordPath, _mono);

            var boss = (agent as Component) != null ? ((Component)agent).GetComponent<BossBehaviour>() : null;
            if (boss != null && agent.Alive)
            {
                var m = agent.Brain.Memory;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Boss: đổi chỗ {boss.Relocations}, lựu đạn {boss.GrenadesThrown}, viện {(boss.Director.ReinforcementsCalled ? "đã gọi" : "chưa")}", _mono);
                if (GUILayout.Button("Đổi vị trí")) _status = boss.Relocate(m.LastKnownPlayerPosition, "debug") ? "Boss đổi vị trí." : "Không có cover phù hợp.";
                if (GUILayout.Button("Ném lựu đạn vào Nam") && ServiceLocator.TryResolve<IPlayerObservable>(out var nam)) boss.ThrowGrenade(nam.Position);
                if (GUILayout.Button("Gọi viện")) _status = $"Viện binh: {boss.CallReinforcements(m.LastKnownPlayerPosition, Time.time)} lính.";
                GUILayout.EndHorizontal();
            }
        }

        private void EmitNoiseAtPlayer(float radius)
        {
            if (!ServiceLocator.TryResolve<NoiseSystem>(out var noise) || !ServiceLocator.TryResolve<IPlayerObservable>(out var player))
            {
                _status = "Map hiện tại chưa đăng ký NoiseSystem / người chơi.";
                return;
            }
            int heard = noise.Emit(player.Position, radius, NoiseKind.Other);
            _status = $"Tiếng động {radius} m tại Nam: {heard} lính nghe.";
        }

        private static bool TryPlayerHealth(out Health health)
        {
            health = null;
            return ServiceLocator.TryResolve<IPlayerObservable>(out var player)
                && player is Component c && c != null && c.TryGetComponent(out health);
        }

        private static string Trim(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "…";
    }
}
