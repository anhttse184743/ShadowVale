using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShadowVale.Map01
{
    public sealed class ForestMission : MonoBehaviour
    {
        public Transform player, hung;
        public Camera gameCamera;
        public Transform encounterExit;
        public Vector2 mapMin = new Vector2(-45, -60), mapMax = new Vector2(45, 88);
        public TextAsset balanceJson, contentBundle;
        public ForestSettings Settings { get; private set; }
        public ForestWeapon Weapon { get; private set; }
        public ForestArchetype GuardData { get; private set; }
        public bool IsInitialized { get; private set; }
        public bool Alarmed { get; set; }
        public bool Hidden { get; private set; }
        public bool Stopped => hp <= 0 || stage == 4 || paused;
        public int ObstructionMask => LayerMask.GetMask("Obstacle", "Cover", "VisionBlocker");
        public int Stage => stage;
        private ForestBundle bundle;
        private CharacterController controller;
        private NavMeshAgent companion;
        private ForestGuard[] guards;
        private readonly List<ForestPoint> points = new List<ForestPoint>();
        private readonly Dictionary<string, int> inventory = new Dictionary<string, int>();
        private float hp, stamina, nextShot, nextNoise, dialogueUntil, craftUntil;
        private int stage, stones;
        private bool crouched, inventoryOpen, mapOpen, paused, encounterLine;
        private string dialogue, crafting;
        private Vector3 aim;
        private ForestPoint nearby;
        private GUIStyle titleStyle, textStyle, smallStyle;
        private static readonly string[] Objectives = {
            "Nhận hàng tiếp tế cạnh Hùng [E]",
            "Cùng Hùng vượt khu tuần tra — chọn lén lút, đánh lạc hướng hoặc giao chiến",
            "Khám xét bàn bản đồ trong căn cứ bỏ hoang [E]",
            "Mang tài liệu và hàng tiếp tế đến bến sông [E]",
            "Hoàn thành Map 1 — Những dấu chân trong rừng"
        };

        private void Awake()
        {
            // Unity objects can retain a managed wrapper after the asset was deleted.
            // Use Unity's null check before accessing TextAsset.text.
            if (balanceJson == null || contentBundle == null)
            {
                FailInitialization("Assign valid balanceJson and contentBundle TextAssets in the Inspector.");
                return;
            }
            try
            {
                Settings = JsonUtility.FromJson<ForestSettings>(balanceJson.text);
                bundle = JsonUtility.FromJson<ForestBundle>(contentBundle.text);
            }
            catch (ArgumentException exception)
            {
                FailInitialization("Invalid mission JSON: " + exception.Message);
                return;
            }
            Weapon = bundle?.weapons?.FirstOrDefault(w => w != null && w.id == "rifle_standard");
            GuardData = bundle?.enemy_archetypes?.FirstOrDefault(e => e != null && e.id == "grunt");
            if (Settings == null || Weapon == null || GuardData == null)
            {
                FailInitialization("Mission JSON must contain settings, rifle_standard and grunt.");
                return;
            }
            if (player == null || hung == null || gameCamera == null)
            {
                FailInitialization("Assign player, hung and gameCamera in the Inspector.");
                return;
            }
            controller = player.GetComponent<CharacterController>();
            companion = hung.GetComponent<NavMeshAgent>();
            if (controller == null || companion == null)
            {
                FailInitialization("Player needs a CharacterController and Hung needs a NavMeshAgent.");
                return;
            }
            guards = FindObjectsByType<ForestGuard>(FindObjectsSortMode.None);
            points.AddRange(FindObjectsByType<ForestPoint>(FindObjectsSortMode.None));
            hp = Settings.playerHP; stamina = Settings.stamina; stones = Settings.startingStones;
            inventory["ammo_rifle"] = Settings.startingAmmo;
            IsInitialized = true;
            Say("Hùng: Nhận hàng rồi đi thôi, Nam. Qua rừng là tới bến sông.", 9);
        }

        private void FailInitialization(string reason)
        {
            Debug.LogError("ForestMission could not initialize. " + reason, this);
            enabled = false;
        }

        public void RegisterPoint(ForestPoint point) => points.Add(point);
        public int Count(string id) => inventory.TryGetValue(id, out int count) ? count : 0;
        public void Say(string text, float seconds = 7) { dialogue = text; dialogueUntil = Time.time + seconds; }
        public void EmitNoise(Vector3 position, float radius) { foreach (var guard in guards) guard.Hear(position, radius); }
        public void Damage(float amount) { if (!Stopped) hp = Mathf.Max(0, hp - amount); }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) { paused = !paused; Time.timeScale = paused ? 0 : 1; }
            if (kb.f9Key.wasPressedThisFrame) Load();
            if (kb.enterKey.wasPressedThisFrame && (hp <= 0 || stage == 4)) Restart();
            if (Stopped) return;
            if (kb.tabKey.wasPressedThisFrame) inventoryOpen = !inventoryOpen;
            if (kb.mKey.wasPressedThisFrame) mapOpen = !mapOpen;
            if (kb.cKey.wasPressedThisFrame) crouched = !crouched;
            if (kb.f5Key.wasPressedThisFrame) Save();
            if (kb.hKey.wasPressedThisFrame && Count("medkit_small") > 0 && hp < Settings.playerHP)
            { inventory["medkit_small"]--; hp = Mathf.Min(Settings.playerHP, hp + Settings.medkitHeal); }
            if (inventoryOpen || mapOpen) { UpdateCompanion(); return; }

            var motion = new Vector2((kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0),
                (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
            var right = gameCamera.transform.right; right.y = 0;
            var forward = gameCamera.transform.forward; forward.y = 0;
            var direction = Vector3.ClampMagnitude(right.normalized * motion.x + forward.normalized * motion.y, 1);
            bool sprint = !crouched && kb.leftShiftKey.isPressed && stamina > 2 && direction.sqrMagnitude > .01f;
            float speed = crouched ? Settings.crouchSpeed : sprint ? Settings.sprintSpeed : Settings.walkSpeed;
            if (crafting != null) speed = 0;
            controller.Move((direction * speed + Vector3.down * 8) * Time.deltaTime);
            stamina = Mathf.Clamp(stamina + (sprint ? -Settings.staminaDrain : Settings.staminaRecovery) * Time.deltaTime, 0, Settings.stamina);
            Hidden = crouched && points.Any(p => p.kind == ForestPointKind.Hide && Vector3.Distance(player.position, p.transform.position) < p.radius);
            if (sprint && Time.time > nextNoise) { nextNoise = Time.time + .6f; EmitNoise(player.position, 7); }
            Aim();
            if (Mouse.current != null && Mouse.current.leftButton.isPressed && Time.time > nextShot && crafting == null) Fire();
            if (kb.qKey.wasPressedThisFrame && stones > 0)
            {
                stones--;
                var target = player.position + Vector3.ClampMagnitude(aim - player.position, Settings.stoneRange);
                EmitNoise(target, Settings.stoneNoise); Trace(player.position + Vector3.up, target + Vector3.up * .2f, Color.yellow);
                Say("Tiếng đá rơi — lính gần đó sẽ đến kiểm tra.", 3);
            }
            nearby = points.Where(p => !p.used && p.kind != ForestPointKind.Hide && p.kind != ForestPointKind.Cover)
                .OrderBy(p => Vector3.Distance(player.position, p.transform.position))
                .FirstOrDefault(p => Vector3.Distance(player.position, p.transform.position) < Settings.interactRange);
            if (kb.eKey.wasPressedThisFrame && nearby != null) Interact(nearby);
            if (kb.bKey.wasPressedThisFrame && nearWorkbench()) Craft();
            if (crafting != null && Time.time >= craftUntil)
            {
                var recipe = bundle.craft_recipes.First(r => r.id == crafting);
                inventory[recipe.output_item_id] = Count(recipe.output_item_id) + recipe.output_count;
                crafting = null; Say("Đã chế tạo băng cứu thương. Nhấn H để sử dụng.");
            }
            if (stage == 1 && (encounterExit != null
                ? Vector3.Distance(player.position, encounterExit.position) < 5f
                : player.position.z > 27))
            {
                stage = 2;
                Say(Alarmed ? "Hùng: Bọn này hôm nay phản ứng nhanh hơn bình thường." : "Hùng: Qua được rồi. Chúng tuần tra kỹ hơn bình thường… Phía trước có một căn cứ cũ.", 9);
                encounterLine = true;
            }
            if (!encounterLine && Alarmed && guards.All(g => !g.Alive))
            { encounterLine = true; Say("Hùng: Bọn này hôm nay phản ứng nhanh hơn bình thường."); }
            UpdateCompanion();
        }

        private bool nearWorkbench() => points.Any(p => p.kind == ForestPointKind.Workbench && Vector3.Distance(player.position, p.transform.position) < Settings.interactRange);

        private void Aim()
        {
            if (Mouse.current == null) return;
            var ray = gameCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (new Plane(Vector3.up, player.position).Raycast(ray, out float distance)) aim = ray.GetPoint(distance);
            var facing = aim - player.position; facing.y = 0;
            if (facing.sqrMagnitude > .01f) player.rotation = Quaternion.LookRotation(facing);
        }

        private void Fire()
        {
            nextShot = Time.time + 1 / Weapon.fire_rate;
            if (Count("ammo_rifle") <= 0) { Say("Hết đạn — vẫn có thể lén đi hoặc đánh lạc hướng.", 2); return; }
            inventory["ammo_rifle"]--;
            var origin = player.position + Vector3.up * 1.1f + player.forward * .7f;
            var target = origin + player.forward * Weapon.range;
            int mask = ObstructionMask | LayerMask.GetMask("Enemy");
            if (Physics.Raycast(origin, player.forward, out var hit, Weapon.range, mask, QueryTriggerInteraction.Ignore))
            { target = hit.point; var enemy = hit.collider.GetComponentInParent<ForestGuard>(); if (enemy != null) enemy.Hit(Weapon.damage); }
            Trace(origin, target, new Color(1, .84f, .45f));
            EmitNoise(player.position, Weapon.noise_radius);
        }

        public void Trace(Vector3 start, Vector3 end, Color color)
        {
            var go = new GameObject("Transient trail");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = trailMaterial;
            line.positionCount = 2; line.SetPosition(0, start); line.SetPosition(1, end);
            line.startWidth = .055f; line.endWidth = .015f; line.startColor = color; line.endColor = color;
            Destroy(go, .12f);
        }
        public Material trailMaterial;

        private void UpdateCompanion()
        {
            if (companion == null || !companion.isOnNavMesh) return;
            // Hùng is a narrative companion in this map; he does not reveal a stealth player.
            companion.speed = Settings.sprintSpeed;
            companion.stoppingDistance = Settings.followDistance;
            if (NavMesh.SamplePosition(player.position, out var hit, 3, NavMesh.AllAreas)) companion.SetDestination(hit.position);
        }

        private void LateUpdate()
        {
            if (gameCamera != null && player != null)
            {
                var target = player.position - gameCamera.transform.forward * 30;
                gameCamera.transform.position = Vector3.Lerp(gameCamera.transform.position, target, 1 - Mathf.Exp(-8 * Time.unscaledDeltaTime));
            }
        }

        public Vector3 GuardCover(Vector3 origin, Vector3 threat)
        {
            var cover = points.Where(p => p.kind == ForestPointKind.Cover && Vector3.Distance(origin, p.transform.position) < 12)
                .OrderBy(p => Vector3.Distance(origin, p.transform.position)).FirstOrDefault();
            return cover == null ? origin : cover.transform.position + (cover.transform.position - threat).normalized * 2;
        }

        public void Interact(ForestPoint point)
        {
            switch (point.kind)
            {
                case ForestPointKind.Supplies:
                    if (stage != 0) return;
                    point.used = true; stage = 1; inventory["supplies"] = 1;
                    Say("Hùng: Đi theo lối đất. WASD di chuyển, Shift chạy. Nhớ giữ sức.");
                    break;
                case ForestPointKind.Loot:
                    if (point.used) return;
                    foreach (var item in point.items) inventory[item.item_id] = Count(item.item_id) + item.count;
                    point.used = true; Say("Đã nhặt vật tư. Tab mở túi đồ. Bàn chế tạo nằm ở điểm tiếp tế.");
                    break;
                case ForestPointKind.Workbench:
                    Say("Bàn chế tạo: B để làm băng cứu thương (2 vải + 1 thảo dược). F5 lưu khi khu vực an toàn.");
                    break;
                case ForestPointKind.Documents:
                    if (stage < 2) { Say("Hãy nhận hàng và cùng Hùng vượt tuyến tuần tra trước."); return; }
                    point.used = true; stage = 3; inventory["river_documents"] = 1;
                    Say("Nam: Đây là bản đồ các tuyến quanh bến sông… cả đường bí mật của đơn vị!\nHùng: Có người đang theo dõi chúng ta. Mang những ghi chép này về ngay.", 15);
                    break;
                case ForestPointKind.Exit:
                    if (stage != 3) { Say("Cần lấy hàng tiếp tế và tài liệu trong căn cứ trước khi rời rừng."); return; }
                    if (Vector3.Distance(hung.position, player.position) > 8) { Say("Chờ Hùng đến cùng trước khi rời rừng."); return; }
                    stage = 4; Say("Hàng đã đến bến sông. Những tuyến đường bị lộ là đầu mối đầu tiên.", 30);
                    break;
            }
        }

        private void Craft()
        {
            if (crafting != null) return;
            var recipe = bundle.craft_recipes.First(r => r.id == "craft_medkit_small");
            if (recipe.inputs.Any(i => Count(i.item_id) < i.count)) { Say("Chưa đủ vật liệu: cần 2 vải và 1 thảo dược."); return; }
            foreach (var input in recipe.inputs) inventory[input.item_id] -= input.count;
            crafting = recipe.id; craftUntil = Time.time + recipe.craft_seconds;
        }

        [Serializable] private sealed class Checkpoint
        {
            public int version = 2, stage, stones;
            public float hp, stamina;
            public Vector3 player, hung;
            public bool alarmed;
            public string[] used;
            public ForestIngredient[] items;
            public string[] down;
        }
        private string SavePath => Path.Combine(Application.persistentDataPath, "shadowvale-map01-checkpoint.json");

        private bool CanSave() => !Stopped && crafting == null && guards.All(g => !g.Alive ||
            (g.state == ForestGuardState.Patrol && Vector3.Distance(g.transform.position, player.position) > 18));

        private void Save()
        {
            if (!CanSave()) { Say("Chỉ lưu khi đã thoát nguy hiểm và không đang chế tạo."); return; }
            var data = new Checkpoint { stage = stage, stones = stones, hp = hp, stamina = stamina,
                player = player.position, hung = hung.position, alarmed = Alarmed,
                used = points.Where(p => p.used).Select(p => p.id).ToArray(),
                items = inventory.Select(p => new ForestIngredient { item_id = p.Key, count = p.Value }).ToArray(),
                down = guards.Where(g => !g.Alive).Select(g => g.id).ToArray() };
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(data, true)); Say("Đã lưu checkpoint Map 1. F9 để tải lại."); }
            catch (IOException) { Say("Không thể ghi checkpoint. Hãy kiểm tra quyền truy cập ổ đĩa."); }
        }

        private static bool loadAfterRestart;
        private void Start()
        {
            if (loadAfterRestart) { loadAfterRestart = false; Invoke(nameof(Restore), .1f); }
        }
        private void Load()
        {
            if (!File.Exists(SavePath)) { Say("Chưa có checkpoint Map 1."); return; }
            loadAfterRestart = true; Restart();
        }
        private void Restore()
        {
            try
            {
                var data = JsonUtility.FromJson<Checkpoint>(File.ReadAllText(SavePath));
                if (data == null || data.version != 2 || data.items == null || data.used == null || data.down == null) throw new IOException();
                controller.enabled = false; player.position = data.player; controller.enabled = true;
                companion.Warp(data.hung); stage = Mathf.Clamp(data.stage, 0, 3); stones = data.stones;
                hp = Mathf.Clamp(data.hp, 1, Settings.playerHP); stamina = Mathf.Clamp(data.stamina, 0, Settings.stamina); Alarmed = data.alarmed;
                inventory.Clear(); foreach (var i in data.items) inventory[i.item_id] = i.count;
                foreach (var guard in guards) if (data.down.Contains(guard.id)) guard.Hit(GuardData.max_hp + 1);
                foreach (var point in points) point.used = data.used.Contains(point.id);
                Say("Đã tải checkpoint. Lính còn sống bắt đầu lại tuyến tuần tra.");
            }
            catch (Exception) { Say("Checkpoint không hợp lệ. Bắt đầu lại Map 1."); }
        }
        private void Restart() { Time.timeScale = 1; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
        private void OnDisable() { Time.timeScale = 1; }

        private void OnGUI()
        {
            if (!IsInitialized) return;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold, wordWrap = true };
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
                smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            }
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            Panel(new Rect(22, 20, 460, 139));
            GUI.Label(new Rect(38, 30, 425, 35), "01 / NHỮNG DẤU CHÂN TRONG RỪNG", textStyle);
            GUI.Label(new Rect(38, 67, 425, 66), Objectives[stage], textStyle);
            Panel(new Rect(width - 265, 20, 245, 138));
            GUI.Label(new Rect(width - 247, 32, 220, 120), $"NAM    HP {hp:0} / {Settings.playerHP:0}\nSức bền {stamina:0}    Đạn {Count("ammo_rifle")}\nĐá {stones}   •   {(Hidden ? "ẨN TRONG BỤI" : crouched ? "ĐANG ĐI KHOM" : "ĐANG DI CHUYỂN")}", textStyle);
            Panel(new Rect(22, height - 69, width - 44, 49));
            GUI.Label(new Rect(36, height - 59, width - 65, 43), "WASD Di chuyển  •  Shift Chạy  •  C Đi khom  •  Chuột Bắn  •  Q Ném đá  •  E Tương tác  •  Tab Túi đồ  •  M Bản đồ  •  Esc Dừng", smallStyle);
            if (Time.time < dialogueUntil)
            {
                Panel(new Rect(210, height - 205, width - 420, 110));
                GUI.Label(new Rect(232, height - 193, width - 464, 94), dialogue, textStyle);
            }
            if (nearby != null && !nearby.used && !Stopped)
                GUI.Label(new Rect(width / 2 - 250, height - 247, 500, 40), "[E] " + nearby.label, textStyle);
            if (crafting != null) GUI.Label(new Rect(38, 167, 430, 30), $"Đang chế tạo… {Mathf.Max(0, craftUntil - Time.time):0.0}s", textStyle);
            foreach (var guard in guards.Where(g => g.Alive))
            {
                var screen = gameCamera.WorldToScreenPoint(guard.transform.position + Vector3.up * 2.4f);
                if (screen.z > 0 && Vector3.Distance(player.position, guard.transform.position) < 24)
                    GUI.Label(new Rect(screen.x / scale - 60, (Screen.height - screen.y) / scale, 190, 40), guard.state + (guard.suspicion > .05f ? $" {Mathf.Min(100, guard.suspicion * 100):0}%" : ""), smallStyle);
            }
            if (inventoryOpen)
            {
                Panel(new Rect(width / 2 - 240, 170, 480, 330));
                GUI.Label(new Rect(width / 2 - 215, 185, 440, 40), "TÚI ĐỒ / VẬT TƯ", titleStyle);
                GUI.Label(new Rect(width / 2 - 215, 232, 430, 210), string.Join("\n", inventory.Where(i => i.Value > 0).Select(i => ItemName(i.Key) + "  × " + i.Value)), textStyle);
                GUI.Label(new Rect(width / 2 - 215, 457, 430, 38), "H Dùng băng cứu thương • B Chế tạo ở bàn", smallStyle);
            }
            if (mapOpen) DrawMap(width, height);
            if (Stopped)
            {
                Panel(new Rect(width / 2 - 300, 235, 600, 180));
                GUI.Label(new Rect(width / 2 - 270, 253, 540, 70), hp <= 0 ? "NAM ĐÃ GỤC NGÃ" : stage == 4 ? "ĐÃ HOÀN THÀNH MAP 1" : "TẠM DỪNG", titleStyle);
                GUI.Label(new Rect(width / 2 - 270, 332, 540, 70), paused ? "Esc Tiếp tục • F9 Tải checkpoint" : "Enter Chơi lại • F9 Tải checkpoint", textStyle);
            }
        }
        private static string ItemName(string id)
        {
            switch (id)
            {
                case "ammo_rifle": return "Đạn súng trường";
                case "cloth": return "Vải";
                case "herb": return "Thảo dược";
                case "scrap_metal": return "Kim loại";
                case "medkit_small": return "Băng cứu thương";
                case "supplies": return "Hàng tiếp tế";
                case "river_documents": return "Bản đồ và ghi chép bến sông";
                default: return id;
            }
        }
        private void DrawMap(float width, float height)
        {
            var rect = new Rect(width / 2 - 310, 164, 620, 420); Panel(rect);
            GUI.Label(new Rect(rect.x + 20, rect.y + 10, 580, 35), "TUYẾN VẬN CHUYỂN / M để đóng", titleStyle);
            GUI.Label(new Rect(rect.x + 20, rect.y + 60, 260, 300), "BẮC ↑\n\n05  Bến sông / rút lui\n04  Căn cứ / bàn tài liệu\n03  Điểm nghỉ / chế tạo\n02  Khu tuần tra\n      Tây: bụi rậm\n      Giữa: vật chắn\n      Đông: đánh lạc hướng\n01  Nhận hàng / xuất phát", textStyle);
            var area = new Rect(rect.x + 310, rect.y + 60, 280, 335);
            foreach (var p in points.Where(p => p.kind != ForestPointKind.Cover && p.kind != ForestPointKind.Hide))
            {
                var pos = MapPosition(p.transform.position, area);
                GUI.color = p.used ? Color.gray : new Color(.95f, .75f, .32f);
                GUI.DrawTexture(new Rect(pos.x - 4, pos.y - 4, 8, 8), Texture2D.whiteTexture);
            }
            GUI.color = Color.cyan; var playerPos = MapPosition(player.position, area);
            GUI.DrawTexture(new Rect(playerPos.x - 5, playerPos.y - 5, 10, 10), Texture2D.whiteTexture); GUI.color = Color.white;
        }
        private Vector2 MapPosition(Vector3 p, Rect r) => new Vector2(r.x + Mathf.InverseLerp(mapMin.x, mapMax.x, p.x) * r.width, r.yMax - Mathf.InverseLerp(mapMin.y, mapMax.y, p.z) * r.height);
        private static void Panel(Rect rect) { var old = GUI.color; GUI.color = new Color(.035f, .07f, .06f, .94f); GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
    }
}
