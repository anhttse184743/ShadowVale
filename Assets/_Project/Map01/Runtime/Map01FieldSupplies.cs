using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>Finite, authored supply caches. ForestPoint IDs use the normal used-point save contract.</summary>
    [DefaultExecutionOrder(30)]
    public sealed class Map01FieldSupplies : MonoBehaviour
    {
        public const string IdPrefix = "c3_field_", ApproachId = "c3_field_approach_v1", MedicId = "c3_field_medic_v1";
        sealed class Site { public Map01Loot.FieldCache data; public ForestPoint point; public GameObject view; }
        readonly List<Site> sites = new List<Site>();
        Map01Mission mission; Map01Quest quest;
        Material canvas;
        public ForestPoint ApproachPoint => Point(ApproachId);
        public ForestPoint MedicPoint => Point(MedicId);
        ForestPoint Point(string id) => sites.Find(s => s.data.id == id)?.point;

        void Start()
        {
            mission = GetComponent<Map01Mission>(); quest = GetComponent<Map01Quest>();
            var c3 = GetComponent<Map01C3Mission>();
            if (quest == null || c3?.Data == null) { enabled = false; return; }
            var catalog = Map01ItemCatalog.Load();
            canvas = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            canvas.SetColor("_BaseColor", new Color(.30f, .34f, .21f));
            foreach (var data in Map01Loot.Load().fieldCaches ?? System.Array.Empty<Map01Loot.FieldCache>())
            {
                var host = new GameObject(data.label);
                var at = c3.At(data.x, data.z);
                if (NavMesh.SamplePosition(at, out var walkable, 2f, NavMesh.AllAreas)) at = walkable.position;
                host.transform.position = at;
                var point = host.AddComponent<ForestPoint>();
                point.id = data.id; point.label = data.label; point.kind = ForestPointKind.Loot;
                point.radius = 1.8f; point.restockSeconds = 0;
                point.items = Map01Logistics.For(mission).ContainerLoot(data.id);
                var view = new GameObject("Supply items"); view.transform.SetParent(host.transform, false);
                var cloth = GameObject.CreatePrimitive(PrimitiveType.Cube); cloth.name = "Supply canvas";
                cloth.transform.SetParent(view.transform, false); cloth.transform.localPosition = Vector3.up * .015f;
                cloth.transform.localScale = new Vector3(1.05f, .03f, .65f);
                cloth.GetComponent<Renderer>().sharedMaterial = canvas; Destroy(cloth.GetComponent<Collider>());
                for (int i = 0; i < point.items.Length; i++)
                {
                    string id = point.items[i].item_id; var model = catalog?.Model(id); if (model == null) continue;
                    var item = Instantiate(model, view.transform, false); item.name = ForestInventory.Name(id);
                    item.transform.localScale *= catalog.Scale(id);
                    item.transform.localPosition = new Vector3((i - (point.items.Length - 1) * .5f) * .3f, .035f, 0);
                    foreach (var collider in item.GetComponentsInChildren<Collider>()) Destroy(collider);
                }
                sites.Add(new Site { data = data, point = point, view = view });
                mission.RegisterPoint(point);
                host.SetActive(quest.Stage >= data.minStage);
            }
        }
        void Update()
        {
            if (quest == null) return;
            foreach (var site in sites)
            {
                site.point.gameObject.SetActive(quest.Stage >= site.data.minStage);
                site.view.SetActive(!site.point.used);
            }
        }
        void OnDestroy()
        {
            foreach (var site in sites) if (site.point != null)
            {
                if (mission != null) mission.UnregisterPoint(site.point);
                Destroy(site.point.gameObject);
            }
            if (canvas != null) Destroy(canvas);
        }
    }
}
