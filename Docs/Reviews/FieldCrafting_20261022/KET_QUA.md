# Chế tạo dã chiến và tiếp đạn AK

Đã triển khai theo phương án được người dùng duyệt và có trong commit `11c5349d` trên thinh. Kết quả kiểm tra sau khi merge dev ngày 23/10/2026 được bổ sung bên dưới.

## Cách dùng

Mở **Tab → CHẾ DÃ CHIẾN**. Bộ dụng cụ là trang bị thường trực, không chiếm ô túi. Danh sách có thể cuộn; món cần bàn vẫn hiện lý do.

1. Thu hồi **10 / 20 / 30 đạn M16 dự trữ** trong 3 giây, nhận tương ứng 1 / 2 / 3 vật tư đạn thu hồi. Đạn đang nạp trong súng không bị lấy.
2. **2 vật tư thu hồi + 1 gói tiếp liệu AK → 10 AK**: 8 giây dã chiến hoặc 5 giây tại bàn. Tỉ lệ toàn vòng là **20 M16 + 1 gói AK → 10 AK**.
3. Băng cứu thương: 4 giây; bộ sửa chữa: 8 giây; bom khói từ ống đóng sẵn: 6 giây. Công thức nguyên liệu giữ như trước. F1, đạn Mosin và các công thức đạn từ nguyên liệu thô cần bàn.

Nam chỉ làm một job dã chiến mỗi lần. Di chuyển/nhảy, đổi vũ khí, bị thương, đóng túi, đổi tab hoặc hủy sẽ hoàn nguyên liệu. Pause đóng băng tiến độ. Lưu hủy job và hoàn nguyên liệu trước khi chụp dữ liệu. Thành phẩm và vật liệu hoàn trả không vừa túi dùng cơ chế rơi dưới chân hiện có. Job được xóa trước khi cấp đồ để callback/hủy lặp không hoàn hai lần.

Map 1 dùng động tác cúi xử lý vật phẩm hiện có và giấu mô hình vũ khí khi chế. Map 2 dùng tư thế tay trống, giấu mô hình vũ khí và cho AI tiếp tục hoạt động trong tab chế dã chiến. Chưa thêm clip chế tạo hay mô hình bộ dụng cụ mới; biểu tượng hai vật tư dùng hình hiện có với nhãn riêng.

## Nguồn tiếp tế

- Khởi đầu: 2 gói AK trong túi. Save cũ nhận 2 gói khi khôi phục, phiên bản migration được lưu cùng layout túi.
- Thưởng bàn giao C1: 2 gói; hòm C2: 2 gói; kho căn cứ bán 1 gói với 3 điểm.
- Túi mới **`c3_field_ak_v1`** cạnh điểm tiếp tế trước C3: 30 đạn AK và 2 gói. Dùng ID riêng, không đặt lại trạng thái đã nhặt của cache cũ. Đây là nguồn hữu hạn lưu bằng hệ used-point đang có.
- Bốn mốc cho tổng 8 gói; tối đa ráp thêm 80 AK nếu có đủ 160 M16. Các nguồn AK trực tiếp cũ vẫn được giữ.

## Kiểm tra đã thực hiện

- Biên dịch C# bằng Roslyn với response/reference của Unity, đầu ra riêng trong `Compile/`; không ghi lên `Library/Bee` hay DLL Unity đang dùng. Đã biên dịch Gameplay, Map01 và assembly test. Log compiler lưu cùng thư mục.
- 45 kiểm tra dữ liệu JSON/công thức/nguồn cấp/ID và 2.211 tổ hợp đạn dự trữ/gói để kiểm tra giới hạn quy đổi, không tạo thêm đạn. Kết quả: `data-validation.json`.
- Đã thêm 3 ca trong `FieldCraftingTests`: dữ liệu và kinh tế; luồng Map 1 gồm băng đạn, hủy khi bị thương, pause, lưu/khôi phục, đóng túi/đổi vũ khí, hoàn dư khi túi đầy; luồng Map 2 gồm AI trong panel và lưu/khôi phục job. Các ca này **chưa chạy trong Unity**.
- Cập nhật fixture `BenchTests` theo hai công thức mới và bộ nguyên liệu test. Chưa chạy lại fixture này trong Unity.

## Vì sao chưa chạy Unity

Theo yêu cầu của người dùng, đã kiểm tra tiến trình trước bước test. Phát hiện Claude đang chạy và ban đầu có Unity batch có dấu hiệu do Claude khởi chạy. Những lần sau batch đã hết nhưng các tiến trình Claude vẫn tồn tại, mã nguồn liên quan tiếp tục thay đổi. Không mở thêm Unity, không dừng tiến trình của Claude hay của người dùng.

Kiểm tra cuối ghi trong `process-check.json`: 16 tiến trình Claude, 1 tiến trình Unity có tham số dự án/batch. Không khởi chạy Unity từ lượt làm việc này.

Lượt biên dịch đầu khi cập nhật reference gặp DLL Gameplay cũ thiếu `ShotPassesThrough`; đã biên dịch riêng Gameplay hiện tại rồi dùng DLL riêng đó. Một file chẩn đoán tạm của công việc khác bị xóa giữa hai lần biên dịch; đã loại đường dẫn không còn tồn tại khỏi response riêng. Không sửa hoặc khôi phục các thay đổi của công việc khác.

Cần chạy các ca Unity và xem trực tiếp UI/animation trên Map 1/2 khi Claude nhả dự án. Biên dịch và kiểm tra dữ liệu không thay cho playtest. Các tỉ lệ/thời gian là mức khởi đầu đã duyệt, chưa có kết quả cân bằng qua lượt chơi mới.

## Phạm vi giữ gìn

Không sửa scene, prefab, clip, thông số sát thương hay tầm phát hiện địch. Các sửa đổi camera, cảnh báo lựu đạn và ray bắn đang dở của công việc khác được giữ nguyên. Bản sao trước sửa và hash nằm ở `Before/` và `before.json`, bao gồm phần dirty đã có trước lượt này; không dùng chúng để ghi đè toàn bộ workspace.

## Kết quả chạy Unity (23/10/2026, Claude Code làm nốt)

- Lượt tổng của Codex (`Docs/Reviews/FullRegression_AfterClaude_20261022/Run_233531`, không commit vì nhiều log) gộp lại theo kết quả mới nhất mỗi ca: 614 đạt, 11 hỏng, 2 bỏ qua.
- Chạy lại 206 ca liên quan trên workspace có chế dã chiến: 194 đạt. `FieldCraftingTests` (3/3), `BenchTests` và ba ca Codex sửa cuối (đồ rơi dưới mái, hội thoại chỉ huy, giao diện chế tạo) đều đạt. Ba ca hỏng vì file ảnh báo cáo bị tiến trình khác khóa (Win32 1224) và một ca phim cứu Hùng chạy chậm khi hai Unity chạy song song: chạy lại riêng đều đạt.
- 8 ca còn hỏng cũng hỏng y hệt trên worktree sạch ở `e73346bd` và ở `86d8360d` (trước cả phần lựu đạn/Mosin), nên không do chế dã chiến: `CrouchStoneTests` (hông hạ 15–18 cm khi ném đá lúc ngồi — cần sửa tư thế trong Blender), `ExtractionTests` (chiều cao đứng), `GuardApproachTests.RecordOverseerContacts` và `Map01RevisionTests.GunContacts…` (chân chạm đất của clip lính), `Map01StoryTests.RescueApproach…` (góc camera nhảy 44°), `StoneMouseInputTests.SlotFour…` (test viết cho prop đá cũ "Stone in hand"/viên sỏi danh mục), `ThrowEffectsTests.BlastAndSmoke…` (ngưỡng ×3 không đạt được với công thức lọc âm hiện tại), `TutorialSceneTests.TutorialPreserves…` (cảnh hướng dẫn ít hơn Map 1 138 đối tượng).
- Chưa chạy: hai ca cần solver Python (`svl-api` ở cổng 8011, mã ở `D:\DO_AN_GAME\shadowvale-solver-main`) vì máy chưa có Python.
- Đã gỡ `FullRegressionAudit.cs` tạm (bản sao ở thư mục lượt chạy) và trả các ảnh/báo cáo đã commit mà lượt test ghi đè.

## Kiểm tra sau merge dev — 23/10/2026

`FieldCraftingTests` hiện có **4 ca, đều đạt** trên bản tích hợp: kinh tế vật tư; quy đổi/hủy/hoàn vật liệu Map 1; nguồn AK C3 hữu hạn qua save/load; chế tạo Map 2 giữ thế giới hoạt động và không nhân vật tư khi lưu. Hai ca QIEA cũng đã đạt với solver Python ở đường dẫn người dùng cung cấp. Hồi quy liên quan có 122/123 ca đạt; còn lỗi pose ngón cái cầm đá, đã ghi rõ trong [báo cáo merge](../DevMerge_20261023/KET_QUA.md). Các ghi chú chưa chạy Unity phía trên mô tả giai đoạn triển khai ban đầu.

Đã chạy lại chế tạo 4/4 và QIEA 2/2 sau khi nhận thêm dev `7abc29af`. Kết quả các ca liên quan trên snapshot sau cùng: **101/102 đạt**, còn ca ngón cái cầm đá; xem [báo cáo bổ sung](../DevMergeLatest_20261023/KET_QUA.md).
