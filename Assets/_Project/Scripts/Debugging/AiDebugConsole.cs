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
        private Rect _window = new Rect(16, 16, 760, 460);
        private int _tab;
        private Vector2 _logScroll, _agentScroll;
        private int _seenVersion = -1;
        private readonly bool[] _showCategory = { true, true, true, true, true, true, true };
        private string _squadFilter = "";
        private string _selectedAgent;
        private bool _playerInvulnerable, _gamePaused;
        private string _status = "";
        private GUIStyle _mono;

        private void Awake() => _feed = new AiConsoleFeed(_log);

        private void OnDestroy()
        {
            _feed?.Dispose();
            if (Visible) SetVisible(false);
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
            _mono ??= new GUIStyle(GUI.skin.label) { richText = true, wordWrap = false, fontSize = 12 };
            _window = GUI.Window(WindowId, _window, DrawWindow, "AI Debug Console — F8 đóng · F7 overlay");
        }

        private void DrawWindow(int id)
        {
            DrawHeader();
            _tab = GUILayout.Toolbar(_tab, Tabs);
            if (_tab == 0) DrawLog(); else DrawAgents();
            if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status);
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
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
            _logScroll = GUILayout.BeginScrollView(_logScroll);
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
            _agentScroll = GUILayout.BeginScrollView(_agentScroll, GUILayout.Height(250));
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
