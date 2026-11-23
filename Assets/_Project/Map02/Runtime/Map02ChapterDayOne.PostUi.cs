using UnityEngine;

namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        Texture2D nightUiAtlas;
        string PostPartName(int part) => new[] { "ĐÈN DẦU", "CỔNG & CHỐT CÀI", "LỐI ĐI GÁC" }[part];

        void NightAtlas(Rect rect, int tile)
        {
            if (nightUiAtlas == null) nightUiAtlas = Resources.Load<Texture2D>("Map02DayOne/UI/NightOneAtlas");
            if (nightUiAtlas == null) return;
            GUI.DrawTextureWithTexCoords(rect, nightUiAtlas,
                new Rect((tile % 3) / 3f, tile < 3 ? .5f : 0, 1f / 3, .5f));
        }

        bool PostPartSafe(int part)
        {
            var post = Day.posts[postIndex];
            return part == 0 ? post.LampSafe : part == 1 ? post.gateClosed && post.latchSeated : post.bundleStowed;
        }

        void TurnPostLamp(float degrees)
        {
            Day.posts[postIndex].lampAngle = Mathf.Repeat(Day.posts[postIndex].lampAngle + degrees, 360);
            SyncDayProps();
        }

        void ClosePostGate()
        {
            Day.posts[postIndex].gateClosed = true;
            dayFeedback = "Cửa đã khép. Cài thanh chốt vào ngàm rồi xác nhận.";
            SyncDayProps();
        }

        void LatchPostGate()
        {
            var post = Day.posts[postIndex];
            if (!post.gateClosed) { dayFeedback = "Khép cửa sát khung trước khi cài chốt."; return; }
            post.latchSeated = true;
            dayFeedback = "Thanh chốt đã vào ngàm. Bấm XÁC NHẬN KIỂM TRA.";
            SyncDayProps();
        }

        void StowPostBundle()
        {
            Day.posts[postIndex].bundleStowed = true;
            dayFeedback = "Bó củi đã vào giá, lối đi đã thông. Bấm XÁC NHẬN KIỂM TRA.";
            SyncDayProps();
        }

        void DrawPostPanel()
        {
            var post = Day.posts[postIndex];
            int part = dayActivity == DayActivity.Lamp ? 0 : dayActivity == DayActivity.Gate ? 1 : 2;
            int safeCount = (post.LampSafe ? 1 : 0) + (post.gateClosed && post.latchSeated ? 1 : 0) + (post.bundleStowed ? 1 : 0);
            Fill(new Rect(0, 0, 1600, 900), new Color(.01f, .02f, .015f, .65f));
            DaySurface(new Rect(80, 65, 1440, 775));
            GUI.Label(new Rect(112, 90, 940, 45), "TRỰC ĐÊM · " + (postIndex == 0 ? "CHỐT BỜ TRE" : "CHỐT ĐẦU LÀNG"), DockText(30, true));
            DayHelpButton(new Rect(1100, 91, 200, 44));
            if (DayButton(new Rect(1320, 91, 175, 44), "ĐÓNG [ESC]")) { RequestCloseDay(); return; }
            GUI.Label(new Rect(112, 146, 1350, 34), $"{safeCount}/3 mục đang an toàn · Sửa mục chưa đạt, xác nhận rồi trở lại người gác để báo cáo.", DockText(21));
            for (int i = 0; i < 3; i++)
            {
                var card = new Rect(112 + i * 455, 194, 440, 75);
                bool safe = PostPartSafe(i);
                DockBox(card, i == part ? new Color(.23f, .29f, .17f) : new Color(.07f, .10f, .07f));
                NightAtlas(new Rect(card.x + 11, card.y + 10, 56, 55), i);
                GUI.Label(new Rect(card.x + 83, card.y + 7, 340, 30), PostPartName(i), DockText(22, true));
                string status = (safe ? "✓ Đang an toàn" : "! Cần chỉnh") + (i == part ? " · Đang xem" : "");
                GUI.Label(new Rect(card.x + 83, card.y + 41, 340, 27), status, StoreText(18, safe ? StoreGood : DayGold, FontStyle.Bold));
            }

            DockBox(new Rect(112, 290, 825, 386), new Color(.035f, .05f, .035f));
            GUI.Label(new Rect(135, 305, 760, 34), "QUAN SÁT · " + PostPartName(part), DockText(21, true));
            if (part == 0) DrawNightLamp();
            else if (part == 1) DrawNightGate();
            else DrawNightPath();

            bool safePart = PostPartSafe(part);
            string statusText = safePart ? "✓ ĐẠT · Có thể xác nhận" : "! CHƯA ĐẠT · Cần điều chỉnh";
            GUI.Label(new Rect(970, 567, 505, 34), statusText, StoreText(22, safePart ? StoreGood : DayGold, FontStyle.Bold));
            if (DayButton(new Rect(970, 609, 505, 67), "XÁC NHẬN KIỂM TRA [E]"))
            {
                ConfirmPostPart();
                if (!DayActivityActive) return;
            }
            GUI.Label(new Rect(115, 703, 1360, 53), dayInputReady ?
                (string.IsNullOrEmpty(dayFeedback) ? "Quan sát model và chi tiết chốt, làm theo nút bên phải. Bảng báo rõ phần chưa an toàn." : dayFeedback) :
                "Nhả phím hoặc chuột mở bảng để bắt đầu.", DockText(21));
            Fill(new Rect(112, 770, 1380, 47), new Color(.13f, .19f, .12f));
            GUI.Label(new Rect(130, 779, 1340, 35), "Sau khi cả ba mục an toàn: đóng bảng, tới ghim vàng gặp " +
                (postIndex == 0 ? "tổ gác" : "anh Lực") + " rồi nhấn E báo chốt sẵn sàng.", DockText(19, true));
        }

        void DrawNightLamp()
        {
            var post = Day.posts[postIndex];
            Fill(new Rect(140,350,765,265),new Color(.045f,.055f,.043f));
            DrawNightModel(new Rect(140,365,765,240));
            GUI.Label(new Rect(150,350,745,35),"▲ NGOÀI LÀNG · KHÔNG ĐỂ LỘ SÁNG",StoreText(19,DayGold,FontStyle.Bold,TextAnchor.MiddleCenter));
            GUI.Label(new Rect(150,619,745,35),"▼ TRONG LÀNG · XOAY CHỤP CHE VỀ PHÍA NÀY",StoreText(19,DayInk,FontStyle.Bold,TextAnchor.MiddleCenter));
            DrawLampDragSurface(new Rect(140,365,765,240));
            GUI.Label(new Rect(970, 307, 505, 86), "Chụp kim loại che ánh đèn khỏi phía ngoài. Xoay cửa sáng xuống phía TRONG LÀNG.", DockText(24, true));
            if (DayButton(new Rect(970, 417, 240, 65), "◀ XOAY TRÁI [A]")) TurnPostLamp(-15);
            if (DayButton(new Rect(1230, 417, 245, 65), "XOAY PHẢI [D] ▶")) TurnPostLamp(15);
            GUI.Label(new Rect(970, 503, 505, 52), "Kéo chuột ngang trên đèn để xoay chụp.\nHoặc giữ A / D. Chụp và vệt sáng xoay cùng nhau.", DockText(19));
        }

        void DrawNightGate()
        {
            var post = Day.posts[postIndex];
            DrawNightModel(new Rect(140,350,555,265));
            GUI.Label(new Rect(710,355,195,42),"CHI TIẾT CHỐT",StoreText(19,DayGold,FontStyle.Bold,TextAnchor.MiddleCenter));
            DrawNightModel(new Rect(710,409,195,160),true);
            Frame(new Rect(710,409,195,160),DayGold,1);
            GUI.Label(new Rect(710,574,195,46),post.latchSeated?"✓ Chốt vào ngàm":post.gateClosed?"Cài chốt vào ngàm":"Khép cổng để căn ngàm",StoreText(17,post.latchSeated?StoreGood:DayGold,FontStyle.Bold,TextAnchor.MiddleCenter));
            GUI.Label(new Rect(150,625,745,32),post.gateClosed?"✓ Cánh cổng đã sát khung":"1. Cánh cổng còn mở · Khép cổng trước",StoreText(20,post.gateClosed?StoreGood:DayGold,FontStyle.Bold,TextAnchor.MiddleCenter));
            GUI.Label(new Rect(970, 307, 505, 85), "Làm theo thứ tự: khép cửa, rồi cài thanh chốt vào ngàm giữ.", DockText(24, true));
            if (DayButton(new Rect(970, 417, 505, 58), (post.gateClosed ? "✓ " : "1. ") + "KHÉP CỔNG SÁT KHUNG [A]")) ClosePostGate();
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && post.gateClosed;
            if (DayButton(new Rect(970, 488, 505, 58), (post.latchSeated ? "✓ " : "2. ") + "CÀI THANH CHỐT [D]")) LatchPostGate();
            GUI.enabled = enabled;
        }

        void DrawNightPath()
        {
            var post = Day.posts[postIndex];
            var path = new Rect(420, 355, 200, 288);
            NightAtlas(path, 5);
            Frame(path, post.bundleStowed ? StoreGood : DayGold, 2);
            GUI.Label(new Rect(423, 363, 194, 35), "LỐI TUẦN TRA", StoreText(18, DayInk, FontStyle.Bold, TextAnchor.MiddleCenter));
            DockBox(new Rect(160, 431, 190, 105), new Color(.25f, .15f, .10f));
            GUI.Label(new Rect(159, 551, 200, 66), "CẠNH CỬA\nCần chừa chỗ mở", StoreText(18, DayGold, FontStyle.Bold, TextAnchor.MiddleCenter));
            DockBox(new Rect(694, 431, 190, 105), new Color(.16f, .24f, .15f));
            GUI.Label(new Rect(692, 551, 195, 66), "GIÁ CẤT\nBên trong chốt", StoreText(18, StoreGood, FontStyle.Bold, TextAnchor.MiddleCenter));
            NightAtlas(new Rect(post.bundleStowed ? 710 : 448, post.bundleStowed ? 442 : 469, 155, 82), 2);
            GUI.Label(new Rect(970, 307, 505, 86), "Cất bó củi vào giá bên trong chốt để thông lối đi và khoảng mở cửa.", DockText(24, true));
            if (DayButton(new Rect(970, 417, 505, 58), "ĐẶT CẠNH CỬA [1]")) dayFeedback = "Bó củi sẽ chắn khoảng mở cửa. Chọn giá cất bên trong chốt.";
            if (DayButton(new Rect(970, 488, 505, 58), "CẤT VÀO GIÁ TRONG CHỐT [2]")) StowPostBundle();
        }
    }
}
