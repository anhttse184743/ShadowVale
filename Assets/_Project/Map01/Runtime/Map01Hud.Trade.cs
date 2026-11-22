using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01
{
    public sealed partial class Map01Hud
    {
        int tradeTab;
        Vector2 tradeScroll;
        CampTrader trade;

        public void OpenTrade(CampTrader trader)
        {
            trade = trader;
            tradeTab = 0;
            tradeScroll = Vector2.zero;
            Disarm();
            trade.Begin();
        }

        void DrawTrade(float width, float height, CampTrader trader)
        {
            BenchStyles();
            float x = (width - 1030) / 2, y = (height - 720) / 2;
            Rect At(float a, float b, float w, float h) => new Rect(x + a, y + b, w, h);
            HudFill(new Rect(0, 0, width, height), new Color(.02f, .025f, .015f, .74f));
            HudPanel(At(0, 0, 1030, 720));
            GUI.Label(At(32, 25, 660, 60), "QUÂN NHU CĂN CỨ", benchTitle);
            GUI.Label(At(35, 88, 670, 37), $"ĐIỂM HẬU CẦN  {trader.Credit}", benchHead);
            if (BenchButton(At(852, 34, 144, 42), "ĐÓNG · ESC", benchTab))
            {
                mission.SetBenchOpen(false);
                trade = null;
                return;
            }
            if (BenchButton(At(35, 138, 280, 44), "NỘP ĐỒ LẤY ĐIỂM", benchTab, true, tradeTab == 0) && tradeTab != 0)
            { tradeTab = 0; tradeScroll = Vector2.zero; Disarm(); }
            if (BenchButton(At(325, 138, 280, 44), "DÙNG ĐIỂM ĐỔI SÚNG", benchTab, true, tradeTab == 1) && tradeTab != 1)
            { tradeTab = 1; tradeScroll = Vector2.zero; Disarm(); }

            var goods = CampTrader.Materials.Where(offer => inventory.Count(offer.id) > 0).ToArray();
            var guns = inventory.Guns().Where(g => g.Slot == null).ToArray();
            GUI.Label(At(35, 198, 960, 32), tradeTab == 0
                ? "Chọn vật tư hoặc súng dự phòng trong túi. Giá súng tính theo độ bền."
                : "Súng mới có độ bền 100% và được cấp kèm đạn.", benchText);
            int count = tradeTab == 0 ? goods.Length + guns.Length : trader.Stock.Length;
            tradeScroll = GUI.BeginScrollView(At(35, 240, 960, 388), tradeScroll,
                new Rect(0, 0, 930, Mathf.Max(385, count * 78)), false, false);
            if (count == 0) GUI.Label(new Rect(12, 16, 880, 40), "Chưa có vật tư hoặc súng dự phòng để nộp.", benchText);
            for (int row = 0; row < count; row++)
            {
                var card = new Rect(0, row * 78, 928, 71);
                HudPanel(card);
                string id, detail, action;
                int points;
                bool enabled = true;
                if (tradeTab == 1)
                {
                    var offer = trader.Stock[row];
                    id = offer.Gun; points = offer.Price;
                    detail = $"100% độ bền · Kèm {offer.Rounds} viên {ForestInventory.Name(offer.Ammo).ToLowerInvariant()}";
                    enabled = trader.Credit >= points;
                    action = enabled ? $"ĐỔI · {points} ĐIỂM" : $"CẦN {points} ĐIỂM";
                }
                else if (row < goods.Length)
                {
                    id = goods[row].id; points = goods[row].points;
                    detail = $"Trong túi ×{inventory.Count(id)}"; action = $"NỘP 1 · +{points} ĐIỂM";
                }
                else
                {
                    var gun = guns[row - goods.Length];
                    id = inventory.IdOf(gun); points = trader.GunValue(gun);
                    detail = $"Độ bền {Percent(inventory.FractionOf(gun))}"; action = $"NỘP SÚNG · +{points} ĐIỂM";
                }
                BenchTileIcon(new Rect(10, card.y + 8, 95, 55), id);
                GUI.Label(new Rect(120, card.y + 9, 600, 28), ForestInventory.Name(id), benchRowName);
                GUI.Label(new Rect(120, card.y + 40, 600, 25), detail, benchDim);
                int cell = tradeTab == 0 && row >= goods.Length ? guns[row - goods.Length].Cell : -1;
                string key = $"trade:{tradeTab}:{row}:{id}:{points}:{cell}";
                if (!BenchButton(new Rect(693, card.y + 14, 219, 42), Armed(key) ? "XÁC NHẬN" : action, benchTab, enabled)) continue;
                if (!Armed(key)) { Arm(key); continue; }
                Disarm();
                if (tradeTab == 1) trader.Buy(row);
                else if (row < goods.Length) trader.SellMaterial(id);
                else trader.SellGun(guns[row - goods.Length]);
                break;
            }
            GUI.EndScrollView();
            GUI.Label(At(35, 650, 960, 48), trader.Feedback, benchText);
        }
    }
}
