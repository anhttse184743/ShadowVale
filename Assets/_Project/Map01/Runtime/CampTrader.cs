using System;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    public sealed class CampTrader : MonoBehaviour
    {
        public const string Prompt = "Trao đổi với quân nhu";
        public const string Greeting = "Có vật tư hay súng cũ thì đưa tôi kiểm. Đủ điểm hậu cần, tôi cấp Mosin với năm viên.";
        public const string NightGreeting = "Tôi có khẩu M mười sáu và đạn dự phòng. Nếu cần thêm hỏa lực cho đêm nay thì đổi bằng điểm hậu cần.";
        bool mapTwo;
        public static Map01Loot.CreditEntry[] Materials => Map01Loot.Load().logistics.materialTrade;

        public readonly struct Offer
        {
            public readonly string Gun, Ammo;
            public readonly int Rounds;
            public Offer(string gun, string ammo, int rounds) { Gun = gun; Ammo = ammo; Rounds = rounds; }
            public int Price => Map01Loot.Load().BaseValue(Gun);
        }

        static readonly Offer[] Map1Stock =
        {
            new Offer("rifle_standard", "ammo_rifle", 10),
            new Offer("rifle_mosin", "ammo_sniper", 5)
        };
        static readonly Offer[] Map2Stock =
        {
            new Offer("rifle_standard", "ammo_rifle", 10),
            new Offer(Map01M16.Id, Map01M16.Ammo, 20)
        };

        Func<bool> available, panelOpen;
        Func<Transform> player;
        Func<Vector3?> anchor;
        Func<int> readCredit;
        Action<int> changeCredit;
        GameObject model;
        Map01Inventory inventory;
        bool missingModel;

        public Offer[] Stock { get; private set; }
        public int Credit => readCredit();
        public bool Available => available();
        public bool Near => Available && model != null && player() != null &&
            (model.transform.position - player().position).sqrMagnitude <= 3f * 3f;
        public bool Open => panelOpen() && Near;
        public string Feedback { get; private set; }

        public static CampTrader Install(GameObject owner, Map01Inventory bag, bool mapTwo,
            Func<bool> available, Func<bool> panelOpen, Func<Transform> player, Func<Vector3?> anchor,
            Func<int> readCredit, Action<int> changeCredit)
        {
            var trader = owner.AddComponent<CampTrader>();
            trader.inventory = bag;
            trader.mapTwo = mapTwo;
            trader.available = available;
            trader.panelOpen = panelOpen;
            trader.player = player;
            trader.anchor = anchor;
            trader.readCredit = readCredit;
            trader.changeCredit = changeCredit;
            trader.Stock = mapTwo ? Map2Stock : Map1Stock;
            return trader;
        }

        void Update()
        {
            bool show = available();
            if (show && model == null && !missingModel && anchor() is Vector3 at)
            {
                var prefab = Resources.Load<GameObject>("Characters/Map02Roles/Thanh");
                if (prefab == null) { missingModel = true; Debug.LogError("Missing camp quartermaster model."); return; }
                Vector3 preferred = at + new Vector3(2.4f, 0f, 1.1f);
                Vector3 position = NavMesh.SamplePosition(preferred, out var hit, 2f, NavMesh.AllAreas) ? hit.position : at;
                var facing = Vector3.ProjectOnPlane(at - position, Vector3.up);
                model = Instantiate(prefab, position, facing.sqrMagnitude > .01f ? Quaternion.LookRotation(facing) : Quaternion.identity, transform);
                model.name = "Quân nhu căn cứ";
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.applyRootMotion = false;
            }
            if (model != null && model.activeSelf != show) model.SetActive(show);
        }

        public void Begin()
        {
            Feedback = mapTwo ? NightGreeting : Greeting;
            if (Near) DialogueVoice.For(model).Play(mapTwo ? "camp_trade_night" : "camp_trade_greeting");
        }

        public void SellMaterial(string item)
        {
            int value = Map01Loot.Load().logistics.MaterialCredit(item);
            if (!Open || inventory.Count(item) < 1 || value <= 0) return;
            inventory.Spend(item, 1);
            changeCredit(value);
            Feedback = $"Đã nộp {ForestInventory.Name(item)}. +{value} điểm hậu cần.";
        }

        public int GunValue(Map01Inventory.Gun gun)
        {
            string id = inventory.IdOf(gun);
            if (gun.Slot != null || !Map01Inventory.HasCondition(id)) return 0;
            return inventory.TurnInValue(gun);
        }

        public void SellGun(Map01Inventory.Gun gun)
        {
            if (!Open || gun.Slot != null || !Map01Inventory.HasCondition(inventory.IdOf(gun))) return;
            string name = ForestInventory.Name(inventory.IdOf(gun));
            int value = GunValue(gun);
            if (value <= 0 || !inventory.RemoveGun(gun)) return;
            changeCredit(value);
            Feedback = $"Đã nộp {name}. +{value} điểm hậu cần.";
        }

        public void Buy(int index)
        {
            if (!Open || index < 0 || index >= Stock.Length) return;
            var offer = Stock[index];
            if (offer.Price <= 0 || Credit < offer.Price) { Feedback = "Chưa đủ điểm hậu cần."; return; }
            if (!inventory.CanFitTradeOffer(offer))
            {
                Feedback = "Túi đã đầy. Dọn chỗ cho súng và đạn rồi đổi lại.";
                return;
            }
            changeCredit(-offer.Price);
            inventory.Receive(offer.Gun, 1);
            inventory.Receive(offer.Ammo, offer.Rounds);
            Feedback = $"Đã nhận {ForestInventory.Name(offer.Gun)} (100%) và {offer.Rounds} viên. −{offer.Price} điểm.";
        }
    }

    public sealed partial class Map01Inventory
    {
        internal bool CanFitTradeOffer(CampTrader.Offer offer)
        {
            int empty = 0, ammo = offer.Rounds;
            int limit = StackLimit(offer.Ammo);
            foreach (var cell in bag)
            {
                if (cell == null) empty++;
                else if (cell.id == offer.Ammo) ammo -= Mathf.Max(0, limit - cell.count);
            }
            if (SlotItem(Slot.Primary) != null) empty--;
            return empty >= 0 && Mathf.Max(0, ammo) <= empty * limit;
        }
    }
}
