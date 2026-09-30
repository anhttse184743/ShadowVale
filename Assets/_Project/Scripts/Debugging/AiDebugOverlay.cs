using System.Collections.Generic;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Fsm;
using ShadowVale.AI.Perception;
using ShadowVale.Core.Events;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadowVale.Debugging
{
    /// <summary>
    /// F7 world overlay: each enemy's view cone coloured by FSM state, a line to the position it was
    /// assigned (cover / squad plan) or is retreating to, noise radii fading out, and a state label
    /// with a suspicion bar above each head. Drawn with GL after the camera renders (works in URP).
    /// </summary>
    public sealed class AiDebugOverlay : MonoBehaviour
    {
        public static bool Enabled { get; set; }

        private const float NoiseFadeSeconds = 1.5f;
        private const int ArcSegments = 14, CircleSegments = 32;

        private struct Ping { public Vector3 Position; public float Radius, Time; }
        private readonly List<Ping> _noises = new List<Ping>();
        private System.Action<NoiseEvent> _onNoise;
        private Material _material;
        private GUIStyle _label;

        private void OnEnable()
        {
            _onNoise = e => _noises.Add(new Ping { Position = e.Position, Radius = e.Radius, Time = Time.time });
            EventBus.Subscribe(_onNoise);
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe(_onNoise);
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        public static Color ColorOf(EnemyStateId state) => state switch
        {
            EnemyStateId.Patrol => new Color(.7f, .75f, .8f),
            EnemyStateId.Investigate => new Color(1f, .85f, .3f),
            EnemyStateId.SpotPlayer => new Color(1f, .55f, .15f),
            EnemyStateId.TakeCover => new Color(.3f, .85f, 1f),
            EnemyStateId.Engage => new Color(1f, .25f, .25f),
            _ => new Color(.85f, .4f, 1f),
        };

        private void OnEndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!Enabled || camera.cameraType != CameraType.Game) return;
            if (_material == null)
            {
                var shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null) return; // Stripped from this build: labels still work.
                _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                _material.SetInt("_ZTest", (int)CompareFunction.Always);
                _material.SetInt("_Cull", (int)CullMode.Off);
            }
            _material.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(camera.projectionMatrix);
            GL.modelview = camera.worldToCameraMatrix;
            GL.Begin(GL.LINES);
            foreach (var agent in AgentRegistry.All)
            {
                if (!agent.Alive) continue;
                DrawAgent(agent);
            }
            float now = Time.time;
            _noises.RemoveAll(n => now - n.Time > NoiseFadeSeconds);
            foreach (var n in _noises)
            {
                GL.Color(new Color(1f, .9f, .4f, 1f - (now - n.Time) / NoiseFadeSeconds));
                Circle(n.Position + Vector3.up * .15f, n.Radius);
            }
            GL.End();
            GL.PopMatrix();
        }

        private static void DrawAgent(IEnemyAgent agent)
        {
            var brain = agent.Brain;
            var m = brain.Memory;
            Vector3 eye = agent.Position + Vector3.up * 1.3f;
            GL.Color(ColorOf(brain.State));
            // Engaged guards track all around, so the cone only matters while unaware.
            float half = m.Engaged(Time.time) ? 180f : agent.VisionConeAngle * .5f;
            Vector3 forward = Vector3.ProjectOnPlane(agent.Forward, Vector3.up).normalized;
            Vector3 prev = eye;
            for (int i = 0; i <= ArcSegments; i++)
            {
                float angle = Mathf.Lerp(-half, half, i / (float)ArcSegments);
                Vector3 p = eye + Quaternion.Euler(0, angle, 0) * forward * agent.VisionRange;
                Line(prev, p);
                prev = p;
            }
            if (half < 180f) Line(prev, eye);

            if (m.HasAssignment)
            {
                GL.Color(new Color(.3f, .85f, 1f));
                Line(agent.Position + Vector3.up * .3f, m.AssignedPosition + Vector3.up * .3f);
                Circle(m.AssignedPosition + Vector3.up * .3f, .6f);
            }
            if (m.Retreating)
            {
                GL.Color(new Color(.85f, .4f, 1f));
                Line(agent.Position + Vector3.up * .3f, m.RetreatPoint + Vector3.up * .3f);
            }
            if (m.Alerted(Time.time) && !m.Engaged(Time.time))
            {
                GL.Color(new Color(1f, .85f, .3f, .7f));
                Line(agent.Position + Vector3.up * .2f, m.InvestigatePoint + Vector3.up * .2f);
            }
        }

        private static void Line(Vector3 a, Vector3 b) { GL.Vertex(a); GL.Vertex(b); }

        private static void Circle(Vector3 center, float radius)
        {
            Vector3 prev = center + Vector3.forward * radius;
            for (int i = 1; i <= CircleSegments; i++)
            {
                float a = i * Mathf.PI * 2f / CircleSegments;
                Vector3 p = center + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * radius;
                Line(prev, p);
                prev = p;
            }
        }

        private void OnGUI()
        {
            if (!Enabled) return;
            var cam = Camera.main;
            if (cam == null) return;
            _label ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, richText = true };
            foreach (var agent in AgentRegistry.All)
            {
                if (!agent.Alive) continue;
                Vector3 screen = cam.WorldToScreenPoint(agent.Position + Vector3.up * 2.3f);
                if (screen.z <= 0f) continue;
                var rect = new Rect(screen.x - 70, Screen.height - screen.y - 22, 140, 18);
                var m = agent.Brain.Memory;
                string role = m.HasAssignment && m.AssignedRole.Length > 0 ? " · " + m.AssignedRole : "";
                GUI.color = ColorOf(agent.Brain.State);
                GUI.Label(rect, $"<b>{agent.Id}</b> {agent.Brain.State}{role}", _label);
                var bar = new Rect(rect.x + 30, rect.yMax, 80, 4);
                GUI.color = new Color(0, 0, 0, .6f);
                GUI.DrawTexture(bar, Texture2D.whiteTexture);
                GUI.color = Color.Lerp(Color.yellow, Color.red, m.Suspicion);
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * m.Suspicion, bar.height), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }
    }
}
