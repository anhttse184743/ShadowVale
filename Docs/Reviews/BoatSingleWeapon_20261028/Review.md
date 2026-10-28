# Nam — một súng trên thuyền, 2026-10-28

Bản sửa tiếp theo c8de240e theo phản hồi người chơi: Nam lên thuyền chỉ đeo một súng, hai tay trống. Khi bắn yểm trợ dùng chính khẩu đó, không có bản súng đeo thứ hai. Áp dụng cuối Map 1 và đầu Map 2.

- Bỏ việc giữ hai bản súng và chỉ dựa vào forceRenderingOff; nay reparent chính Weapon từ tay tới ngực model hiện hành. Các súng khác trong player root bị tắt trong thời gian cinematic; trả trạng thái cho PlayerCombat sau cảnh.
- Khi model C1 kết thúc, chuyển súng về model gốc trước khi hủy model C1.
- Phần tay khi bước đi/rời ghế dùng lớp Humanoid arms/fingers từ clip đi bộ sẵn trong repo. Giữ chân và root của clip lên/xuống thuyền; không đổi skeleton. Cảnh C1 dùng nguyên clip hỗ trợ. Khi bắn hoặc nạp đạn, bỏ lớp tay trống để dùng tư thế súng.
- Không tạo súng đeo công việc của chapter trước khi arrival bàn giao gameplay.
- Clip mới: Assets/Resources/Cutscenes/Map02/Nam_Boat_Free_Arms.anim. Tạo lại bằng menu ShadowVale/Cutscene/Prepare boat free arms, từ Standard Walk.fbx có sẵn; không tải animation ngoài hoặc tạo audio.

Source SHA-256: e1356fd98c3e7004a8343abe922fc67a10d108ccb7d4842fca566f96f5b269e6

## Kiểm thử
Unity 6000.3.24f1: **4/4 ca đạt** ở FinalTests.xml.
- AK và Mosin chạy chuỗi tự nhiên tới gameplay Map 2. Quét toàn bộ player root, kiểm tra chỉ một Weapon có renderer thực sự hiển thị; tìm cả mesh trùng bên ngoài Weapon để tránh bỏ lọt bản súng sao. Súng đeo phải chính là OriginalGun và thuộc ngực model hiện đang hiển thị.
- Kiểm tra hai tay không giữ tư thế cầm súng trước ngực liên tục khi đang đeo. Một tay nâng tạm để giữ thăng bằng lúc bước bậc không bị coi là cầm súng.
- Bắn yểm trợ vẫn hạ lính, không tiêu đạn hoặc sinh loot phụ.
- Bỏ qua lúc ba lô đang đặt dưới đất: phục hồi ba lô, hủy model tạm và trả gameplay đúng.
- Kiểm tra trực tiếp ảnh Nam bước xuống bậc, đi lên bến, đỡ Hùng và đi trên bờ. Tệp mesh đi kèm ghi trạng thái renderer, không chỉ kết quả hàm đếm riêng của BoatCarry.
- Bài C1 cũ yêu cầu giấu mọi súng đã cập nhật để khớp yêu cầu giữ súng đeo trên vai, tay trống.

AK: samples=3460, replacementBodySamples=604
Mosin: samples=3207, replacementBodySamples=612

Chưa push. Không thay âm thanh hoặc thoại trong đợt sửa này.
