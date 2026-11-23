using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        readonly HashSet<string> dayHelpSeen = new HashSet<string>();
        string dayHelpKind;
        int dayHelpPage;
        bool dayHelpVisible;
        Texture2D dayUiAtlas;
        static readonly Color DayGold = new Color(.87f, .72f, .43f);
        static readonly Color DayInk = new Color(.95f, .92f, .82f);
        static readonly Color DayMuted = new Color(.69f, .75f, .66f);

        string DayHelpKind => dayActivity switch
        {
            DayActivity.Packing => "packing",
            DayActivity.Dock => DockHammering ? "hammer" : "boards",
            DayActivity.Lamp => "lamp",
            DayActivity.Gate => "gate",
            DayActivity.Path => "path",
            _ => null
        };

        void OpenDayHelp()
        {
            if (DayHelpKind == null) return;
            EndLampDrag();
            dayHelpKind = DayHelpKind;
            dayHelpPage = 0;
            dayHelpVisible = true;
            dayInputReady = false;
        }

        void FinishDayHelp()
        {
            dayHelpSeen.Add(dayHelpKind);
            dayHelpVisible = false;
            dayInputReady = false;
            dayClock = 0;
            daySpaceReleased = false;
        }

        bool TickDayHelp(Keyboard keyboard)
        {
            var kind = DayHelpKind;
            if (kind == null) return false;
            if (!dayHelpVisible && !dayHelpSeen.Contains(kind)) OpenDayHelp();
            if (!dayHelpVisible && keyboard?.hKey.wasPressedThisFrame == true) OpenDayHelp();
            if (!dayHelpVisible) return false;
            if (!dayInputReady)
            {
                if (!DayInputHeld) dayInputReady = true;
                return true;
            }
            if (keyboard?.escapeKey.wasPressedThisFrame == true) FinishDayHelp();
            else if (keyboard?.enterKey.wasPressedThisFrame == true)
            {
                if (dayHelpPage < 2) dayHelpPage++;
                else FinishDayHelp();
            }
            return true;
        }

        Texture2D DayAtlasTexture => dayUiAtlas != null ? dayUiAtlas :
            dayUiAtlas = Resources.Load<Texture2D>("Map02DayOne/UI/DayOneAtlas");

        void DayAtlas(Rect rect, int tile, float opacity = 1)
        {
            if (DayAtlasTexture == null) return;
            var previous = GUI.color;
            GUI.color = new Color(1, 1, 1, opacity);
            GUI.DrawTextureWithTexCoords(rect, DayAtlasTexture,
                new Rect((tile % 3) / 3f, tile < 3 ? .5f : 0, 1f / 3, .5f));
            GUI.color = previous;
        }

        void DaySurface(Rect rect)
        {
            Fill(rect, new Color(.055f, .075f, .06f, .99f));
            DayAtlas(rect, 5, .22f);
            Frame(rect, new Color(.37f, .42f, .30f), 1);
            Fill(new Rect(rect.x, rect.y, rect.width, 3), DayGold);
        }

        void DayHelpButton(Rect rect)
        {
            if (DayButton(rect, "CÁCH CHƠI [H]")) OpenDayHelp();
        }

        void DrawDayHelp()
        {
            Fill(new Rect(0, 0, 1600, 900), new Color(.01f, .02f, .015f, .88f));
            DaySurface(new Rect(180, 95, 1240, 710));
            string title = dayHelpKind switch
            {
                "packing" => "CẤT VẬT TƯ VÀO KHO",
                "hammer" => "ĐÓNG CỌC CÙNG CHÚ BẢY",
                "boards" => "KÊ BA BẬC BẾN",
                "lamp" => "TRỰC ĐÊM · CHE ÁNH ĐÈN",
                "gate" => "TRỰC ĐÊM · CÀI CỬA RÀO",
                _ => "TRỰC ĐÊM · DỌN LỐI ĐI"
            };
            GUI.Label(new Rect(220, 122, 1120, 48), title, StoreText(32, DayInk, FontStyle.Bold));
            string[] headings, details;
            if (dayHelpKind == "packing")
            {
                headings = new[] { "Chọn hòm cần cất", "Tìm chỗ đặt hợp lệ", "Cất đủ ba hòm" };
                details = new[] {
                    "Bấm một hòm trong danh sách bên phải, hoặc nhấn 1, 2, 3.\n\nDi chuột vào nền kho để xem hòm sẽ chiếm những ô nào.",
                    "Bấm chuột trái để đặt. R hoặc chuột phải để xoay hòm.\n\nViền xanh là đặt được. Viền đỏ sẽ kèm lý do: chắn lối xuống, nền ẩm, chồng hòm hoặc vượt khỏi kho.",
                    "Bấm một hòm đã cất để nhấc lên và đặt lại. Nút XẾP LẠI đưa cả ba hòm về chỗ chờ.\n\nKhi đủ 3/3 hòm, bấm CẤT XONG hoặc Enter để hoàn thành." };
            }
            else if (dayHelpKind == "hammer")
            {
                headings = new[] { "Canh kim vào vùng xanh", "Một lần nhấn, một nhát búa", "Đóng đủ hai cọc" };
                details = new[] {
                    "Kim trắng chạy qua lại trên thanh nhịp.\n\nĐợi kim nằm trong vùng xanh có dấu ✓ rồi nhấn SPACE một lần.",
                    "Nhả SPACE và đợi Nam hạ búa, nâng búa xong mới nhấn tiếp.\n\nNếu trượt, chờ kim chạy lượt mới rồi thử lại. Các nhịp đã đạt vẫn được giữ.",
                    "Mỗi cọc cần 3 nhịp đúng. Có 2 cọc cần đóng.\n\nXong hai cọc, bảng chọn ván sẽ mở. Bấm CÁCH CHƠI hoặc H để xem lại hướng dẫn bất cứ lúc nào." };
            }
            else if (dayHelpKind == "boards")
            {
                headings = new[] { "Quan sát và chọn ván", "Căn ván lên hai thanh đỡ", "Kê đủ ba bậc rồi thử bến" };
                details = new[] {
                    "Bấm tấm ván bên trái hoặc nhấn 1–5.\n\nQuan sát hình và thông báo bên phải: ván nứt hoặc quá ngắn sẽ không chịu lực đủ.",
                    "Bấm một bậc trống trên sơ đồ. Dịch ván bằng thanh trượt, hai nút trái/phải hoặc A/D.\n\nHai đầu phải tựa lên hai thanh đỡ. R để xoay. Bấm KIỂM TRA VÀ KÊ VÁN hoặc E khi đã căn đúng.",
                    "Mỗi tấm ván chỉ dùng một lần. Chọn thêm ván tốt và kê đủ 3 bậc.\n\nCuối cùng bấm NHỜ CHÚ BẢY THỬ BẾN hoặc E để hoàn thành công việc." };
            }
            else if (dayHelpKind == "lamp")
            {
                headings = new[] { "Nhìn hướng vệt sáng", "Xoay chụp đèn", "Xác nhận và báo chốt" };
                details = new[] {
                    "Phía trên khung quan sát là NGOÀI LÀNG, cần giữ tối. Phía dưới là TRONG LÀNG.\n\nVệt màu vàng cho biết ánh đèn đang chiếu về đâu.",
                    "Giữ chuột trái trên model đèn rồi kéo ngang để xoay chụp che sáng. Có thể giữ A / D; nhả để dừng.\n\nHướng vệt sáng xuống phía TRONG LÀNG, tới khi trạng thái hiện ĐẠT.",
                    "Bấm XÁC NHẬN KIỂM TRA hoặc E.\n\nSửa tiếp cửa hoặc lối đi nếu còn mục chưa an toàn. Khi cả ba mục đều an toàn, trở lại người gác ở ghim vàng và nhấn E báo cáo." };
            }
            else if (dayHelpKind == "gate")
            {
                headings = new[] { "Khép cửa sát khung", "Cài thanh chốt", "Xác nhận và báo chốt" };
                details = new[] {
                    "Quan sát cánh cổng 3D và hai hàng rào nối bên đường. Nếu còn hé, bấm KHÉP CỬA SÁT KHUNG hoặc A.\n\nDấu ✓ cho biết cửa đã khép, bạn có thể làm bước cài chốt.",
                    "Bấm CÀI THANH CHỐT hoặc D. Quan sát ô CHI TIẾT CHỐT: thanh sắt phải trượt vào ngàm trên cột cổng.\n\nCửa đã khép và chốt đã cài thì trạng thái sẽ hiện ĐẠT.",
                    "Bấm XÁC NHẬN KIỂM TRA hoặc E để kết thúc kiểm tra cửa.\n\nXem các mục đèn và lối đi. Khi cả ba mục đều an toàn, trở lại người gác ở ghim vàng và nhấn E báo cáo." };
            }
            else
            {
                headings = new[] { "Tìm đồ đang chắn lối", "Chọn chỗ cất phù hợp", "Xác nhận và báo chốt" };
                details = new[] {
                    "Bó củi nằm giữa lối tuần tra sẽ cản người đi qua.\n\nQuan sát lối đi ở giữa, khoảng cạnh cửa bên trái và giá cất bên phải.",
                    "Bấm CẤT VÀO GIÁ TRONG CHỐT hoặc phím 2.\n\nĐặt cạnh cửa bằng phím 1 sẽ chắn khoảng mở cửa. Bảng sẽ giải thích để bạn chọn lại, phần đã sửa vẫn được giữ.",
                    "Khi bó củi nằm trên giá và trạng thái hiện ĐẠT, bấm XÁC NHẬN KIỂM TRA hoặc E.\n\nKhi đèn, cửa và lối đi đều an toàn, trở lại người gác ở ghim vàng và nhấn E báo cáo." };
            }
            for (int i = 0; i < 3; i++)
            {
                var tab = new Rect(220 + i * 383, 183, 365, 38);
                Fill(tab, i == dayHelpPage ? new Color(.26f, .32f, .20f) : new Color(.10f, .13f, .10f));
                GUI.Label(tab, $"{i + 1}. {headings[i]}", StoreText(18, i == dayHelpPage ? DayGold : DayMuted, FontStyle.Bold, TextAnchor.MiddleCenter));
            }
            DrawDayHelpIllustration(new Rect(225, 255, 455, 325));
            GUI.Label(new Rect(730, 265, 620, 55), headings[dayHelpPage], StoreText(29, DayInk, FontStyle.Bold));
            GUI.Label(new Rect(730, 335, 620, 255), details[dayHelpPage], StoreText(24, DayInk));
            GUI.Label(new Rect(225, 617, 1110, 48), "Tiến trình mini game tạm dừng khi đang xem hướng dẫn.", StoreText(19, DayMuted));
            bool before = GUI.enabled;
            GUI.enabled = before && dayHelpPage > 0;
            if (DayButton(new Rect(225, 700, 200, 58), "QUAY LẠI")) dayHelpPage--;
            GUI.enabled = before;
            if (DayButton(new Rect(450, 700, 285, 58), "VÀO CHƠI [ESC]")) FinishDayHelp();
            if (DayButton(new Rect(1000, 700, 350, 58), dayHelpPage == 2 ? "BẮT ĐẦU [ENTER]" : "TIẾP THEO [ENTER]"))
            {
                if (dayHelpPage < 2) dayHelpPage++;
                else FinishDayHelp();
            }
        }

        void DrawDayHelpIllustration(Rect rect)
        {
            Fill(rect, new Color(.025f, .04f, .03f));
            Frame(rect, new Color(.27f, .33f, .24f));
            if (dayHelpKind == "hammer")
            {
                DayAtlas(new Rect(rect.x + 140, rect.y + 45, 175, 100), 3);
                GUI.Label(new Rect(rect.x, rect.y + 57, rect.width, 75), "SPACE", StoreText(38, DayInk, FontStyle.Bold, TextAnchor.MiddleCenter));
                var bar = new Rect(rect.x + 30, rect.y + 185, rect.width - 60, 44);
                Fill(bar, new Color(.2f, .27f, .20f));
                Fill(new Rect(bar.center.x - 50, bar.y, 100, bar.height), new Color(.37f, .59f, .32f));
                GUI.Label(bar, "✓", StoreText(25, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter));
                Fill(new Rect(bar.center.x - 3, bar.y - 9, 6, bar.height + 18), DayInk);
                GUI.Label(new Rect(rect.x, rect.y + 255, rect.width, 45), "Nhấn khi kim ở vùng xanh", StoreText(21, DayInk, FontStyle.Bold, TextAnchor.MiddleCenter));
            }
            else if (dayHelpKind == "boards")
            {
                foreach (float side in new[] { .22f, .78f })
                    Fill(new Rect(rect.x + rect.width * side - 12, rect.y + 45, 24, 215), new Color(.39f, .33f, .20f));
                DayAtlas(new Rect(rect.x + 40, rect.y + 100, rect.width - 80, 78), 3);
                Frame(new Rect(rect.x + 40, rect.y + 100, rect.width - 80, 78), StoreGood, 3);
                GUI.Label(new Rect(rect.x, rect.y + 265, rect.width, 45), "Hai đầu cùng tựa lên thanh đỡ", StoreText(20, DayInk, FontStyle.Bold, TextAnchor.MiddleCenter));
            }
            else if (dayHelpKind == "packing")
            {
                for (int x = 0; x < 6; x++)
                for (int y = 0; y < 4; y++)
                {
                    var cell = new Rect(rect.x + 35 + x * 64, rect.y + 35 + y * 55, 62, 53);
                    Fill(cell, x < 2 ? new Color(.30f, .28f, .19f) : new Color(.18f, .24f, .16f));
                }
                DayAtlas(new Rect(rect.x + 163, rect.y + 90, 190, 108), 0);
                Frame(new Rect(rect.x + 163, rect.y + 90, 190, 108), StoreGood, 3);
                GUI.Label(new Rect(rect.x, rect.y + 265, rect.width, 45), "Giữ trống hai cột lối xuống", StoreText(21, DayInk, FontStyle.Bold, TextAnchor.MiddleCenter));
            }
            else if (dayHelpKind == "lamp")
            {
                Fill(new Rect(rect.x + 25, rect.y + 30, rect.width - 50, 90), new Color(.29f, .14f, .10f));
                GUI.Label(new Rect(rect.x, rect.y + 42, rect.width, 35), "NGOÀI LÀNG · GIỮ TỐI", StoreText(19, DayGold, FontStyle.Bold, TextAnchor.MiddleCenter));
                Fill(new Rect(rect.x + 25, rect.y + 145, rect.width - 50, 115), new Color(.13f, .27f, .17f));
                DrawNightModel(new Rect(rect.x+25,rect.y+65,rect.width-50,190));
                GUI.Label(new Rect(rect.x, rect.y + 189, rect.width, 50), "▼ TRONG LÀNG", StoreText(24, DayInk, FontStyle.Bold, TextAnchor.MiddleCenter));
                GUI.Label(new Rect(rect.x, rect.y + 275, rect.width, 40), "Xoay ánh sáng về phía trong", StoreText(20, DayInk, FontStyle.Bold, TextAnchor.MiddleCenter));
            }
            else if (dayHelpKind == "gate")
            {
                DrawNightModel(new Rect(rect.x + 15, rect.y + 25, rect.width - 30, 205));
                if(dayHelpPage>0)DrawNightModel(new Rect(rect.x+220,rect.y+145,220,105),true);
                GUI.Label(new Rect(rect.x, rect.y + 265, rect.width, 45), "Khép cửa → cài chốt → xác nhận", StoreText(20, DayInk, FontStyle.Bold, TextAnchor.MiddleCenter));
            }
            else
            {
                NightAtlas(new Rect(rect.x + 140, rect.y + 30, 175, 210), 5);
                Fill(new Rect(rect.x + 325, rect.y + 80, 110, 100), new Color(.20f, .30f, .17f));
                NightAtlas(new Rect(dayHelpPage == 0 ? rect.x + 165 : rect.x + 330, rect.y + 100, 100, 70), 2);
                GUI.Label(new Rect(rect.x, rect.y + 265, rect.width, 45), "Cất vào giá để thông lối đi", StoreText(20, DayInk, FontStyle.Bold, TextAnchor.MiddleCenter));
            }
        }
    }
}
