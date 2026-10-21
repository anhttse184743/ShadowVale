# Rà phần Map 1 còn dang dở — 20/10/2026

Mốc bắt đầu: thinh, merge commit 6c86097b. Người dùng chọn rà Map 1/gameplay còn dang dở trong repo. Không suy ra ai là tác giả hoặc ai đang làm từ tên thư mục.

## Phạm vi sửa có căn cứ

Đối chiếu mục A4–A6 và prompt 2 trong `Docs/Handoff/PhatHienXac_ViecConLai_Prompts.md` với code hiện tại và cả bản trên đĩa `ShadowVale_DevAI`: cả ba mục vẫn thiếu. Không lấy dòng "đã chốt" trong một tài liệu làm bằng chứng duyệt một thiết kế hình ảnh mới. Lượt này khép các đường code theo đặc tả đang có, không thêm cơ chế mới.

1. Map01Mission.Say: lời nhân vật chưa có bản thu vẫn hiện nguyên phụ đề; lời đã có bản thu giữ chính sách audio-only của GameplayCaption. Giữ callback LineStarted và hàm GameplayCaption.
2. ForestSettings + Map01Balance.json + Map01EnemyController: tác giả có thể chỉnh waryDetectionScale; giá trị phát hành vẫn 0.75; file cũ hoặc giá trị không dương dùng mặc định 0.75.
3. Map01C3Mission.GoWary: walk:true thay warp khi điểm danh thiếu. Load/khởi tạo vẫn dùng quy trình sẵn có. Giữ các API chiến đấu/gọi quân của chỉ huy.
4. Test nhánh lính thường phát hiện xác, đường đi C3, lời thoại và cấu hình; quay C3 trong Unity bằng test, không sửa scene.

Rủi ro cần kiểm: callback phát thoại muộn có thể ghi đè chữ; Repost đi bộ phụ thuộc NavMesh và bộ đếm pause tuần tra; chỉ huy có driver riêng. Test dùng save riêng và batch fallback hiện có.

## Tài liệu đã thực sự đọc

| Nguồn | Điều áp dụng |
|---|---|
| [Unity 6.3 — NavMeshAgent.SetDestination](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AI.NavMeshAgent.SetDestination.html) | Yêu cầu đường đi là bất đồng bộ; chờ lính tới đích có hạn giờ, không kiểm vị trí đích ngay sau gọi. |
| [Unity 6.3 — JsonUtility.FromJson](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/JsonUtility.FromJson.html) | Field thiếu dùng khởi tạo mặc định; vẫn kiểm thêm fallback khi cấu hình ghi 0/âm. |
| ForestSceneTestBase.cs; DialogueVoice.cs; Map01BodyDiscovery.cs; NavMeshEnemyBody.cs; PatrolState.cs | Đi theo pipeline thật, bảo vệ save, không giả kết quả AI hay chuyển hitscan/pose để đáp ứng test. |

Lượt sửa code này không xem video mới, không tải Mixamo, không tạo hay thay rig/clip. Video xuất từ Unity là chứng cứ chức năng, không phải đo FPS phần cứng.

## Kiểm chứng trước sửa

`before.xml`: 3/3 ca mới lỗi đúng chỗ: Outpost guard 5 đổi vị trí tức thì 25.4365387 m; lời hô chưa ghi âm trả caption null; cấu hình 0.4 vẫn cho detection 1.125 s thay vì 0.6 s.

`after.log`: một lượt dừng biên dịch do test gọi setter SquadId nội bộ từ assembly test. Đã sửa fixture bằng reflection; không mở rộng API sản xuất.

`after-r2.xml`: 6/6 đạt, gồm 5 ca mới và ca điểm danh hiện có đã đổi sang chờ đi bộ.

## Lượt tiếp tục ngày 21/10

Người dùng đã yêu cầu kiểm dev mới, gộp và push cả thinh/dev. Không hỏi lại quyền commit/push; chỉ xuất bản sau kiểm tra. Dev mới là fbff9a6d, với M16 và model NPC mới; kế hoạch giải xung đột ở MERGE_PLAN.md.

- Hồi quy r2: 39/40 đạt; ca quay C3 lỗi closure sau domain reload. Đã chuyển phần quay sang helper tạo sau EnterPlayMode.
- Mười hai ca backlog: 1/12 đạt ở lần chạy lại ban đầu. Phân biệt fixture chưa nhận lệnh E/tầm dao cũ/chưa warm clip/đứng ngoài NavMesh với lỗi runtime.
- R3: 11/14 đạt. C3 film hết timeout 180 s thật khi mới quay được khoảng 35 s mô phỏng 1080p60; không phải lỗi compile hay bằng chứng FPS game. Tăng giới hạn quay riêng lên 600 s, giữ tốc độ game.
- Hùng: ép đánh giá một lần pose khi SnapToStage trước khi trả lại culling. Head từ 1,72 m về 1,20 m trên gốc, gối khoảng 0,02 m. Test phần đứng dậy chuyển sang BeginFree thật thay vì giả load stage rồi đòi phát lại đứng dậy.
- Đá trên mái kho: lưu tọa độ tiếng động nhưng chọn điểm điều tra trên sàn NavMesh có đường hoàn chỉnh. Phạm vi thăm dò đường tối đa 8 m không thay bán kính nghe. Test riêng mái và đất trống.
- Cụm đá 17: giữ ID stones_17 và số lượng 2, chuyển từ (91.11269,20.825281,44.27331) sang ứng viên gần nhất thỏa điều kiện NavMesh/đường đi/khoảng cách đã khảo sát (98.48,18.32,48.16), dịch 8,6983 m. Không sửa scene. Kiểm cả static layout và nhặt thực tế.
- R4 bổ sung ca chứng minh lệnh đổi chốt phải hủy thời gian dừng tuần tra cũ; chưa coi ca này đạt trước khi xem XML.
- Preflight dev riêng: 17/18 ở lần đầu; test sát thương AK/M16 sau đồng bộ kỳ vọng đạt 1/1. Tổng mới nhất của nhóm này là 18 ca riêng biệt đạt. Bản nguồn dev cũ chưa có batch save fallback: ô auto bị test ghi, đã giữ thêm bản test và khôi phục từ backup đã xác minh SHA256; ô thủ công 0/1 không đổi.
- Quét tham chiếu GUID: 0 tham chiếu từ asset tracked tới metadata local chưa track. Scene sandbox/recovery là dữ liệu kiểm tra, không stage vào game.

Các XML đỏ, lỗi compile fixture và timeout đều được giữ để kiểm toán; báo cáo cuối dùng kết quả mới nhất từng ca, không cộng trùng lượt chạy.

R4–R7 đã khép các ca Map 1 được chọn. Bổ sung hai lỗi runtime có bằng chứng đỏ trước sửa: Repost giữ lại 60 s dừng tuần tra cũ; tải checkpoint hộ tống không áp lại tư thế Hùng, phát lại đứng dậy. Đã sửa hủy pause khi nhận chốt mới và áp pose sau khôi phục health/stage. C3 film r6 đạt: 452 khung 1080p60, bốn người về chốt, bước ngang lớn nhất 0,06 m; bước cao nền của chỉ huy 0,2025 m được đo riêng. Hùng r7 đạt toàn bộ bắt đầu quỳ → cởi trói/đứng dậy thật → đi bị thương → nghỉ giường ở căn cứ → chết/retry checkpoint hộ tống.

Đá hiện có 36 cụm, giữ mọi ID cũ. Công cụ RepairCoverage chỉ nối thêm cụm mới, dùng cửa C1/bàn đài C2 làm đầu đường; không dùng tâm công trình nằm dưới mái. Khoảng thiếu đá trên năm tuyến là 38/38/30/4/44 m (giới hạn 46 m). Test static và nhặt cụm 17 thực tế đều đạt r5; tiếng đá rơi trên mái được điều tra ở sàn có đường đi, đạt r4.
