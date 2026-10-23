# Dev → thinh — 23/10/2026

Đây là đợt ghép `c8667a4e`. Sau đó dev có thêm cập nhật `7abc29af`; kết quả bản tích hợp sau cùng nằm ở [báo cáo bổ sung](../DevMergeLatest_20261023/KET_QUA.md).

Đã lấy `origin/dev` tại `c8667a4e` để merge vào `thinh` tại `3918bba6`. Giữ các commit chế dã chiến, nguồn đạn AK, cân bằng súng, chuyển động Nam và bản hướng dẫn mới của thinh; nhận nội dung ngày 1/đêm 1 Map 2, nhân vật, 49 bản thoại và các sửa rig/hội thoại từ dev.

## Sửa khi tích hợp

- Giải quyết xung đột duy nhất ở `Map01StoryVoiceTests`: giữ helper đặt Nam cạnh chỉ huy, chờ tiếp đất và các kiểm tra góc máy/bảng nhiệm vụ.
- Sửa vị trí Bé Tí ở đêm 3. Độ lệch bố trí gia đình trước đó bị cộng cả vào điểm tìm trẻ, đặt bé cách marker 3,36 m. Nhánh đêm 3 nay dùng đúng điểm cứu trẻ; sau cứu vẫn về cạnh mẹ. Test cầu, vị trí trẻ và khôi phục save đã đạt.
- Test catalog nhận cả `vi` và `vi-VN`, vẫn kiểm tra âm thanh mono 48 kHz và thời lượng.
- Test save M16 hoàn tất phần giới thiệu cập bến mới trước khi đổi súng; vẫn xác nhận chưa hoàn thành nhiệm vụ gặp Lực, kiểm tra đúng model, cỡ đạn, băng đạn và fallback AK cho save cũ.

## Kiểm tra Unity

Unity 6000.3.24f1, EditMode có Enter/Exit PlayMode. Chạy sau khi batch Unity của Claude kết thúc. Test scene dùng thư mục save thử riêng.

**123 ca khác nhau: kết quả mới nhất của từng ca là 122 đạt, 1 lỗi, không bỏ qua.** Đây là hồi quy có chọn lọc quanh các phần tích hợp, không phải một lượt toàn bộ suite đều xanh.

| Lượt | Đạt / tổng | Kết quả chính |
| --- | --- | --- |
| focused-01 | 106/109 | Chế tạo, save/load, rig, thoại, hướng dẫn, Map 2, QIEA |
| followup-03 | 15/17 | Toàn tuyến ngày 1/đêm 1 đạt; phát hiện vị trí trẻ đêm 3; test cầm đá còn lỗi |
| targeted-04 | 2/3 | Sửa vị trí trẻ đạt; tiếp tục chẩn đoán cầm đá |
| stone-final-09 | 0/1 | Chạy lại test cầm đá nguyên gốc trên mã cuối, xác nhận lỗi còn tồn tại |

- `FieldCraftingTests`: **4/4 đạt**, gồm quy đổi đạn dự trữ, túi tiếp tế C3 hữu hạn, hủy/hoàn nguyên liệu, pause, save/load và chế dã chiến trên Map 2 không dừng thế giới.
- Hai test QIEA trước đây thiếu dịch vụ: **2/2 đạt** với `.venv` trong `D:\DO_AN_GAME\shadowvale-solver-main`, API tại cổng 8011. Không thay mã solver; đã dừng dịch vụ thử do lượt này khởi chạy.
- Toàn tuyến Map 2 hoàn thành **10 mục tiêu**, chuyển sang ngày 2 và trả điều khiển bằng WASD/E cùng input mini game. Timescale 2; không dùng lượt này để đo thời lượng chơi tự nhiên. Bước chạy và checkpoint ở `Map02Flow/`.
- 49 câu thoại mới phát hết, không chồng tiếng. Các ca nhân vật, cập bến tự nhiên, bắn trúng địch/tường chắn và lưu M16 đạt.

## Lỗi còn lại

`StoneMouseInputTests.SlotFourAndMouseThrowWithoutFiringOrLosingCancelledStones` vẫn lỗi tại kiểm tra ngón cái: góc đầu ngón khoảng **20,007°**, test nguyên gốc yêu cầu **trên 30°**.

`NamMotionRefresh.cs`, `NamMotionV3.asset` và test này có cùng Git blob trước/sau merge; bằng chứng trong `stone-baseline-source.json`. Không có lượt Unity đối chứng trên checkout trước merge. Các lượt 05–08 đo thêm mesh bàn tay để chẩn đoán; không thay kết quả lỗi thành đạt. Test nguyên gốc đã được trả lại, không đưa phép đo thử vào assembly game. Bản nguồn chẩn đoán giữ riêng ở `StoneGripDiagnostic.cs.txt`. Cần sửa/đối chiếu pose ngón cái v3 trong một lượt chuyển động riêng.

## Bằng chứng và giữ gìn workspace

- `results.json`: từng ca và lần chạy mới nhất; các XML `*-results.xml` là cây kết quả đầy đủ từ callback Unity, tránh bộ gom CLI mất ca khi reload PlayMode.
- Lượt 02 dừng ở biên dịch do biến trùng trong test chẩn đoán, đã bỏ khai báo trùng trước lượt 03.
- Gỡ observer tạm khỏi Assets. Trả 10 ảnh kiểm tra cầm đá đã bị test ghi đè về đúng nội dung trước test; ảnh mới giữ tại `GeneratedEvidence/` trên máy.
- Giữ nguyên hai ảnh dirty đã có trước công việc: `Tools/M16Reports/inventory.png`, `Tools/M16Reports/m16-hud.png`; không đưa vào commit merge.
