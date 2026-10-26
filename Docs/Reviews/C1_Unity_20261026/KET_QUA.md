# C1 Map 2 v4 — tích hợp Unity, 26/10/2026

Người dùng yêu cầu “thêm vào game và commit push lên thinh xong merge vào dev”. Phạm vi là cảnh Hùng hụt chân, Nam đặt ba lô xuống và đỡ tay trái còn lành; không phải trại C1 Map 1. Các công việc Ngày 1/Đêm 1 và âm thanh khác đang dở trong thư mục chính được giữ riêng.

## Đã tích hợp

- Dùng đúng thân và hai bàn tay v4 đã sửa mép cổ tay. Nam giữ 62 xương, Hùng 28. Không thay FBX nhân vật gameplay hoặc scene.
- Hai prefab C1 dùng chuyển động transform trực tiếp, giữ hướng xoắn và ngón tay của Blender. Khi đi tiếp hoặc bỏ qua cảnh, trả lại mô hình và cờ hiển thị trang bị ban đầu.
- Ba lô bám thân Nam của bản C1 khi đeo, bám tay khi cầm, đứng yên trên sàn khi đặt xuống. Ẩn cả AK và Mosin trong lúc đỡ, không chỉ khẩu đầu tiên tìm được.
- Chỉnh riêng chân phải chống của Hùng cho mặt sàn đặc trong game: tâm gối cao 3,5 cm để chừa thể tích đầu gối; giữ điểm cổ chân, hướng bàn chân và toàn bộ thân/tay đã duyệt.
- Giữ Nam và anh Thành trên đúng mặt bến ở hội thoại chuyển tiếp.

## Kiểm tra và bằng chứng

| Kiểm tra | Kết quả |
|---|---|
| Bản nguồn nén so với v4 | Hash lưới, trọng số, bind rig và toàn bộ đường cong active giống nhau: `original-fingerprint.json` và `archive-fingerprint.json`. Nguồn gốc giữ nguyên SHA-256 `15723d4da3281b245f401c11446c7ee006ec79889436096adf5fc9b7a8234ae0`. |
| Nhập pose | Nam/Hùng có avatar hợp lệ; 11 mốc × 6 điểm xương × 2 người khớp Blender trong 8 mm. Lần nhập Generic đầu sai hông khoảng 1,08 m nên đã bỏ, dùng Legacy transform tracks và kiểm lại đạt. |
| Đầu gối / cổ chân / tay | 72 khung kiểm liên tiếp: gối nằm trên mặt sàn, cổ chân giữ trong 1 mm, tay giữ trong 0,1 mm. `source-ground.xml`: **2/2 đạt**, gồm kiểm nguồn pose. |
| Cảnh C1 trước lượt chỉnh ba lô cuối | `contact-second.xml`: **3/3 đạt** — chạy hết, bỏ qua khi ba lô dưới đất, quay video. |
| Arrival hiện có | 7/7 ca Arrival đạt trong `arrival-first.xml`; 3 lỗi C1 của lượt đó đã chỉ đúng sai mặt gối và được sửa, không coi cả lượt đầu là đạt. |
| Vòng cuối trên nền thinh | `thinh-final.xml`: **7/7 đạt** (pose/C1/arrival). Sau khi kiểm thêm hình và sửa kích thước/tâm ba lô: `c1-ready.xml` **6/6 đạt** (3 ca nguồn/đầu gối/ba lô và 3 ca C1 thực tế). Bảy ca Arrival, ba ca C1 và ba ca nguồn/đầu gối/ba lô là **13 ca riêng biệt** qua các lượt; không cộng lượt lặp thành số ca mới. |
| Sau merge dev | Đang cập nhật theo kết quả bản gộp. |

[Video Unity bản cuối](C1_v4_Unity_1080p60.mp4), [Nam đỡ Hùng](c1-kneel.png), [cùng đứng dậy](c1-up.png), [đeo lại ba lô](c1-on.png). Lỗi ba lô 7 mm và lượt thử chỉ đổi cờ gây lệch tâm 130 m đều đã bỏ; bản cuối lấy snapshot bằng toàn bộ trọng số và bind pose trong world space, kiểm cả kích thước thật, vị trí sau lưng và đáy chạm sàn trong test. Video có khung 1920×1080, track 60 fps; ghi rõ số mẫu render thực và khung lặp trong file `.info.txt`. Đây là video duyệt hình, không phải đo game đạt 60 FPS.

## Giới hạn còn giữ từ bản được duyệt

Không đổi keyframe/nhịp v4. Còn 5 cờ tự va chạm và 1 cờ tốc độ cẳng tay phải ở frame 127 trong báo cáo Blender; nhịp buông tay còn nhanh và da tay thô khi quay quá sát. Test bàn tay cũ dùng cổ tay của bản Humanoid làm đích áp sát cố định nên không áp nguyên cho rig v4; thay bằng đối chiếu pose nguồn và kiểm Nam đỡ phía tay trái, tránh tay phải bị thương. Không khẳng định mọi điểm nắm đều kín hay mọi va chạm toàn thân/mũ/ba lô đã sạch.

Bản nguồn chia sẻ: [C1_Approved_v4.blend](../../../SourceArt/Map02_C1_Unity_20261026/C1_Approved_v4.blend), [cách xuất lại](../../../SourceArt/Map02_C1_Unity_20261026/README.md). Bản nén khoảng 66 MB để đẩy được lên GitHub; không đưa file nguồn thô 166 MB hay hàng trăm ảnh trung gian lên repo.
