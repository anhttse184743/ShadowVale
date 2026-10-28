# C1 Map 2 — điểm tiếp tục ngày 26/10/2026

Người dùng đã yêu cầu **đưa v4 vào game, commit/push thinh, rồi merge vào dev**. Quyền này thay trạng thái “chờ duyệt/chưa xuất” trong các báo cáo demo trước. Phạm vi chỉ cảnh Hùng hụt chân, Nam đặt ba lô → đỡ tay trái còn lành → cùng đứng → nhặt ba lô → đi tiếp.

Bản xem mới nhất: **v4 sửa riêng mép cổ tay**, `D:\DO_AN_GAME\chuyen_dong\Map2_C1_NamDoHung_Demo_2026-10-26_v4_CoTay.blend`; bản repo: `SourceArt/Map02_C1_Cuff_20261026/C1_Nam_DoHung_DEMO_v4.blend`. Người dùng đã cho sửa mép lưới trên bản sao. Rig/rest pose, toàn bộ F-curve và trọng số đỉnh cũ giữ lại không đổi. v3 vẫn giữ làm đối chiếu. Đừng dùng v1/v2 hay chạy lại script nguồn ghi file chủ.

[Báo cáo tích hợp Unity, test và ảnh/video](../Reviews/C1_Unity_20261026/KET_QUA.md). Nguồn chia sẻ trên Git là [C1_Approved_v4.blend](../../SourceArt/Map02_C1_Unity_20261026/C1_Approved_v4.blend), bản nén khoảng 66 MB. Lưới, trọng số, bind rig và active curves khớp bản v4; bản gốc và cửa sổ Blender của người dùng giữ nguyên. Nam vẫn 62 xương, Hùng 28.

Unity dùng hai prefab C1 riêng tại `Assets/Resources/Map02ArrivalC1`, motion Legacy lấy mẫu trực tiếp để giữ chuyển động tay. Không thay mô hình gameplay hay scene. Chỉ chỉnh chân phải chống của Hùng cho mặt sàn game, giữ cổ chân và thân/tay. Ba lô theo thân mới khi đeo, theo tay khi cầm và đứng trên sàn khi đặt xuống; ẩn mọi súng khi đỡ. Sau cảnh/skip trả lại đúng mô hình và trang bị. [Cách xuất/nhập lại](../../SourceArt/Map02_C1_Unity_20261026/README.md).

Giới hạn còn giữ từ v4: **5 khung cờ tự va chạm (v3 có 6)**, 1 cờ nhịp xoay cẳng tay phải frame 127, nhịp buông tay nhanh và da tay còn thô khi cận. Không ghi “hoàn tất mọi lỗi animation”. Không đổi rig hoặc sơn lại trọng số vùng khác.

Không phải trại C1 Map 1. Không bật lại lớp IK tay Humanoid cũ chồng lên v4. Các thay đổi Ngày 1/Đêm 1, âm thanh và đạo cụ khác đang dở trong thư mục chính được giữ riêng, không tự gom vào commit C1.

**Bàn giao C1 mới nhất:** thinh đã push `f784a124` và `617a289c`; bản gộp với dev `978f6c6c` đã có 13 ca riêng biệt đạt qua lượt đầu (12/13) và lượt chạy lại ca quay (1/1) sau khi cách ly thư mục lưu test. Không đổi cơ chế save của game. [Báo cáo tích hợp và video dev](../Reviews/C1_Unity_20261026/KET_QUA.md).
