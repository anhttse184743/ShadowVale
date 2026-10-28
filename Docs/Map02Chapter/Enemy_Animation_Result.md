# Animation địch Map 2 dùng chung với Map 1

Ngày kiểm tra: 22/10/2026.

Nguyên nhân tư thế dang tay: prefab `EnemyCommanderVisual` có Animator và avatar nhưng không gán controller (prefab còn được dùng trong các cảnh diễn). Map 2 trước đây chỉ cập nhật Speed khi địch đã giao chiến, bỏ qua nhánh tuần tra và kiểm tra tiếng động.

Đã bổ sung tham chiếu tới chính `AC_Enemy.controller` của Map 1 trong `Map01ActorAssets`. Mọi địch đi qua `Map02ChapterEnemy.Initialize`, gồm nhóm tuần tra, nhóm tấn công và địch khôi phục từ save, được gán controller này và dùng lại `Map01EnemyGrip` cùng súng của Map 1. Không sửa prefab cảnh diễn.

Speed cập nhật theo vận tốc NavMesh ở các nhánh di chuyển; Engaged/Searching phản ánh trạng thái địch. Phát Attack khi bắn, Hit khi chịu sát thương còn sống; Health tiếp tục điều khiển Die. Tắt root motion để NavMesh giữ quyền di chuyển.

Kiểm thử Unity: **2/2 đạt** tại `Logs/Map02Chapter/enemy-animation-results.xml`:

- Tham chiếu controller trỏ đúng asset Map 1.
- Spawn nhóm tuần tra thật trong Ngày 2; xác nhận avatar humanoid, controller, súng và hiệu chỉnh tiếp xúc tay. Xác nhận Speed khi tuần tra, trạng thái Fire/Hit/Die, rồi lưu/tải và xác nhận địch đã chết giữ animation chết. Kiểm tra presentation trên tất cả địch được khôi phục trong ca này.

Ảnh kiểm tra: `Logs/Map02Chapter/EnemyAnimation/aim.png` và `dead.png`.

Phạm vi áp dụng là đường khởi tạo chung của địch trên toàn Map 2; kiểm thử không đồng nghĩa đã chơi tuần tự toàn bộ 43 nhiệm vụ. Dừng Play rồi chạy lại Map 2 để khởi tạo địch bằng mã mới.
