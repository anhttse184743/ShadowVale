# Khoa đồng bộ dev — 16/10/2026

## Bản nguồn và bảo toàn

- Khoa trước cập nhật: e659a4a.
- dev đã fetch: 9e8a163ff018447faa608e9793237cd66265884a; cập nhật fast-forward 12 commit.
- Bản sao trước cập nhật: stash b3290bcdf48e9b5a3e8a9b3d5f05f8274c5f1771 (`codex-before-dev-sync-20261016`), giữ nguyên để khôi phục.
- Khôi phục đủ 527 file trước đó chưa được Git theo dõi; không thiếu file (Logs/DevSync-preservation.json).
- Không force push, không xóa thay đổi đang làm. Các phần Map 1 chưa commit được áp lại trên nền dev mới.

## Xử lý conflict

- Map01Mission: dùng API RestartCurrentMission từ dev; giữ lọc phụ đề, hệ thống thoại và nhận biết tiếng động hiện tại.
- Map01SaveSystem: giữ tiến trình kỹ năng, khám phá bản đồ và cơ chế retry của dev; ghép snapshot story/operation, khóa lưu giữa tương tác và phục hồi lính động trước snapshot. Retry điểm nhiệm vụ đi qua PrepareRetry để kỹ năng không bỏ qua luật rollback của dev.
- Không còn conflict Git hoặc dấu conflict trong code.

## Chìa kho điểm 1

Nguyên nhân: SpawnBlockers sao chép actor lính gác kho và kế thừa carriesSupplyKey. Đã xóa cờ và operationIndex cho lính ra bến, lính cinematic và pursuer. Loot chỉ trao chìa khi actor chính là Layout.keyKeeper thuộc điểm 1, không dựa riêng cờ sao chép. Nhãn/loot chìa cũ trên blocker được loại bỏ khi chuẩn bị extraction, bao gồm khi tải save.

Kiểm tra mới OnlyOriginalSupplyKeeperDropsKeyEvenWhenClonedForExtraction mô phỏng bản sao giữ cờ, tiêu diệt cả bản gốc/bản sao và lính chặn bến, sau đó khôi phục snapshot. Chỉ bản gốc được phép có một chìa.

## Kiểm thử

- `Logs/DevSync-results.xml`: 91 trường hợp, 89 đạt ở lượt đầu. Phạm vi: hai mini game, toàn luồng điểm 1–2–3, kiểm tra chìa kho mới, hội thoại/audio, save/load, kỹ năng/rollback, minimap/khám phá, mặt sàn hầm, súng/băng đạn, camera và hai nhánh chuyển Map 2.
- Hai bài chưa đạt dùng fixture cũ: bài extraction thiếu các mốc nhiệm vụ và báo cáo; bài thoại bấm xác nhận trước khi hết khóa input 1 giây sau câu “Rõ.” rất ngắn. Cập nhật fixture theo luồng thực, giữ kiểm tra thứ tự và đầy đủ trạng thái.
- `Logs/DevSync-recheck.xml`: 1/2 đạt. Bài extraction sau cập nhật phát hiện lỗi thật: `CompleteBossDefeat(false)` khi skip cảnh chết có thể bỏ qua báo cáo. Đã sửa API luôn yêu cầu retreatReported, áp dụng cả natural/skip. Test gọi cả hai chế độ và xác nhận chưa được ra bến trước báo cáo.
- `Logs/DevSync-final.xml`: **4/4 đạt**, thoát Unity mã 0. Chạy lại hai bài trên, bài chìa kho và bài quan sát/bàn giao chiến đấu điểm 3. Các trường hợp ban đầu chưa đạt đều đã đạt ở lượt cuối; không cộng ba lượt thành một bộ kiểm tra mới.
- Đã xem trực tiếp ảnh Unity mới `Logs/Map01Operations/radio-panel.png` và `cargo-in-hand.png`: tay thao tác bảng đài và nắm quai túi được giữ sau hợp nhất. Không coi hai ảnh là nghiệm thu mọi frame hoạt ảnh.
- Kiểm tra whitespace code C# đạt. Scene Unity còn các khoảng trắng YAML do Editor sinh từ thay đổi trước, không sửa lại toàn bộ scene chỉ để dọn định dạng.

Bản kiểm thử dùng fixture để đặt tiến trình/actor; không thay cho lượt chơi thủ công toàn nhiệm vụ 20–30 phút hoặc đo FPS. Chưa push bản làm việc lên remote; nhánh Khoa cục bộ đã ở dev 9e8a163, các thay đổi đang phát triển vẫn được giữ để người dùng kiểm tra.