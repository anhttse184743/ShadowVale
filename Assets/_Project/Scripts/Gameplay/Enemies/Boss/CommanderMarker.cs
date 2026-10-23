using ShadowVale.Gameplay.Combat;
using UnityEngine;

namespace ShadowVale.Gameplay.Enemies.Boss
{
    /// <summary>
    /// A gold star over the commander's head, so the player can tell him from his men (he wears the
    /// same model as them until he gets his own). Built in code — a flat five-point star facing the
    /// camera, bobbing slowly — and gone when he dies.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CommanderMarker : MonoBehaviour
    {
        public const string StarName = "Commander star";
        /// <summary>Above the top of his head.</summary>
        public const float Clearance = .55f;
        public const float Radius = .32f;
        public static readonly Color Gold = new Color(1f, .82f, .2f);

        private Transform _star;
        private Health _health;
        private Material _material;
        private Mesh _mesh;
        private float _height;

        public Transform Star => _star;

        public static CommanderMarker Attach(GameObject enemy)
        {
            var marker = enemy.GetComponent<CommanderMarker>();
            if (marker == null) marker = enemy.AddComponent<CommanderMarker>();
            marker.Build();
            return marker;
        }

        private void Build()
        {
            if (_star != null) return;
            _health = GetComponent<Health>();
            _height = HeadHeight() + Clearance;
            var go = new GameObject(StarName);
            _star = go.transform;
            _star.SetParent(transform, false);
            _star.localPosition = Vector3.up * _height;
            go.AddComponent<MeshFilter>().sharedMesh = _mesh = StarMesh(Radius, Radius * .45f);
            var renderer = go.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            _material = new Material(shader) { name = "Commander star" };
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", Gold);
            if (_material.HasProperty("_Color")) _material.SetColor("_Color", Gold);
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Top of his body: the tallest renderer, else the NavMesh agent's height, else 2 m.</summary>
        private float HeadHeight()
        {
            float top = 0f;
            foreach (var r in GetComponentsInChildren<Renderer>())
                if (!(r is ParticleSystemRenderer)) top = Mathf.Max(top, r.bounds.max.y - transform.position.y);
            if (top > .5f && top < 4f) return top;
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            return agent != null ? agent.height : 2f;
        }

        private void LateUpdate()
        {
            if (_star == null) return;
            bool show = _health == null || !_health.IsDead;
            if (_star.gameObject.activeSelf != show) _star.gameObject.SetActive(show);
            if (!show) return;
            _star.position = transform.position + Vector3.up * (_height + Mathf.Sin(Time.time * 2.2f) * .06f);
            var cam = Camera.main;
            if (cam != null) _star.rotation = Quaternion.LookRotation(_star.position - cam.transform.position, cam.transform.up);
        }

        private void OnDestroy()
        {
            Release(_material);
            Release(_mesh);
        }

        private static void Release(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        /// <summary>A flat five-point star in the XY plane, both faces, centred on the origin.</summary>
        public static Mesh StarMesh(float outer, float inner)
        {
            var v = new Vector3[11];
            v[0] = Vector3.zero;
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2 + i * Mathf.PI / 5;
                float r = i % 2 == 0 ? outer : inner;
                v[i + 1] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0);
            }
            var t = new int[60];
            for (int i = 0; i < 10; i++)
            {
                int a = i + 1, b = (i + 1) % 10 + 1;
                t[i * 3] = 0; t[i * 3 + 1] = b; t[i * 3 + 2] = a;           // Front.
                t[30 + i * 3] = 0; t[30 + i * 3 + 1] = a; t[30 + i * 3 + 2] = b; // Back.
            }
            var mesh = new Mesh { name = "Commander star", vertices = v, triangles = t };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
