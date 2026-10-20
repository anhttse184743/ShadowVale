# Hùng tự về doanh trại — 19/10/2026

Đã áp dụng vào checkout `G:/game/ShadowVale`, nhánh Khoa. Chưa commit/push.

## Hành vi

- Giữ clip khập khiễng `Hung_DiKhapKhieng`, controller AC_Hung và tốc độ 1,8 m/s. Không nhập demo Mixamo.
- Hùng tự lấy đường NavMesh hoàn chỉnh tới cửa hầm. Đích không phụ thuộc vị trí Nam; Nam có thể quay lại/bọc sườn để yểm hộ.
- Khi có đợt truy kích mới, Hùng dùng câu đã lồng tiếng `m1_story_15`: “Tôi trúng đạn rồi... Nam, yểm hộ tôi!”. Không tạo audio trả phí hoặc giọng mới.
- Gọi yểm hộ không dừng agent; chờ thoại đang phát kết thúc, dùng chung cooldown 12 giây với lời báo trúng đạn. Mỗi đợt gọi một lần khi còn lính sống.
- Lưu mask các đợt đã gọi; save cũ bỏ qua lời gọi của đợt đã xuất hiện. Retry/checkpoint tiếp tục đường về hầm; Hùng chết thì dừng agent.
- HUD hướng dẫn bảo vệ Hùng đang tự về căn cứ. Luồng vào hầm hiện có vẫn bắt đầu khi một trong hai người tới cửa.

## File code

- `Assets/_Project/Map01/Runtime/Map01Rescue.cs:213`: GuideHungHome, đường về hầm.
- `Assets/_Project/Map01/Runtime/Map01Rescue.cs:229`: TryEscortHelp, lời gọi và cooldown.
- `Assets/_Project/Map01/Runtime/Map01Rescue.cs:325`: snapshot revision 4 / helpWaveMask.
- `Assets/_Project/Map01/Runtime/Map01PlayerInteraction.cs:137`: bàn giao điều hướng cho rescue.
- `Assets/_Project/Map01/Runtime/Map01Quest.cs:124`: mục tiêu HUD; API gate mới vẫn có wrapper cũ để tương thích.
- Test: ForestFlowTests, RescueTests, StoryAnimationTests; tài liệu Docs/HungVisual.md.

## Kiểm thử thực tế

Unity 6000.3.24f1, EditMode tests vào Play Mode, không `-nographics`.

Lượt đầu phát hiện lỗi tạo NavMeshPath trong field initializer. Đã dừng riêng tiến trình test của tác nhân, chuyển khởi tạo vào Awake. Lưu log tại `Unity_InitialFailure.log`; lượt đó không phải một lần chạy hoàn chỉnh đạt.

Lượt sau **8/8 đạt, 0 lỗi**. Kết quả từng test nằm trong `test-results.json` và XML gốc `Tests.xml`:

1. HungHeadsHomeIndependentlyThenStaysAtTheBase
2. PursuerCannotShootHungThroughSolidCover
3. EscortFailureRetriesFromFreedHungWithOriginalInventory
4. FreeRequiresFourKillsButNoHealingItemAndWaitsForStandingUp
5. HungKeepsWalkingHomeAndCallsForCoverWhenPursuersArrive
6. NamAloneAtDoorStartsShelterAndSkipRewardsExactlyOnce
7. ThreeWavesSpawnOnlyOnceOffscreenAndReloadKeepsDefeatedEnemies
8. HungEscortKeepsTheOriginalLimpClip

`git diff --check` đạt. Kiểm tra diff không có thay đổi scene Map 1/2, model/material/animation Hùng, AC_Hung hoặc code ném đá/NamActions/PlayerCombat/Map01Mission được bảo vệ. Giữ nguyên các sửa audio/C3 chưa commit trước đó.

Ảnh game Full HD nằm ở `G:/game/ShadowVale/Logs/Rescue/homebound/hung-walking-0.png` tới `-2.png`. Đã xem ảnh: cây cỏ che phần chân, nên các ảnh này không đủ để đánh giá biến dạng bàn chân. Test xác nhận di chuyển NavMesh thực tế, tốc độ, lời gọi đã phát và controller giữ clip cũ; không tuyên bố đã kiểm chứng lại toàn bộ chất lượng animation hoặc toàn tuyến chơi thủ công.
