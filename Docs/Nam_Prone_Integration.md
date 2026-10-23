# Nằm trườn dùng chung Map 1 và Map 2

## Điều khiển

- Z khi đứng/khom: chuyển sang nằm, khi đang chạm đất.
- Z hoặc Space khi nằm: trở về đứng. Lần nhấn Space này không đồng thời nhảy.
- WASD: trườn theo camera; dừng di chuyển thì dừng nhịp trườn.
- Giữ chuột phải: ngắm, thả chuột: trở lại cầm chờ.
- R với AK/M16/K54/Mosin khi nằm: nạp đạn, giữ tư thế nằm.
- Đổi trang bị vẫn giữ tư thế nằm. Shift/C không tự hủy nằm.
- Khi có vật cản phía trên, giữ nằm đến khi đủ khoảng trống đứng dậy.

`PlayerController` sở hữu trạng thái, vận tốc và collider. `NamProneMotion`
được cài khi khởi tạo cả hai mission, đọc cùng một resource `Animations/NamProne`.
Không thay prefab Nam, Avatar hay controller gốc. Root motion không điều khiển
vị trí gameplay. Tốc độ trườn mặc định 0,65 m/s; collider được hạ và camera đổi
độ cao mượt. Tầm nhìn của địch dùng điểm thấp tương ứng khi nằm.

## Animation

Nguồn là bản Blender đã duyệt `SourceArt/Animation/NamCrawl/Nam_Crawl_Mixamo_Review_v4.blend`.
Sáu clip: tay không, cầm súng, ngắm, dao, lựu đạn và đá. Giữ nhịp chân cũ,
nắm đấm và cổ tay cố định; giữ sửa căn mặt đất và giảm nhấp nhô của thân.
Runtime nội suy giữa các khung và trộn tư thế khi vào/ra nằm, ngắm và đổi đồ.
Theo chỉnh sửa sau khi vào Unity, súng giữ băng đạn hướng xuống cả ở tư thế
cầm chờ và ngắm. Trục trên được lấy từ thân súng/băng đạn thực tế; không dùng
trục trên tùy ý của marker đầu nòng. Hai bàn tay theo góc xoay đã sửa.
Súng hạ thêm 5 cm so với điểm gắn đã sửa; không hạ hai tay theo yêu cầu mới nhất.

Animation được bake lên xương Nam hiện có, gồm các ngón tay và xương twist.
Hệ thống K54/Mosin khi đứng nhường toàn thân cho tư thế nằm. Khi dùng một
động tác toàn thân có sẵn như hồi máu, Nam đứng dậy trước;
nếu vướng trần thì không bắt đầu động tác đứng xuyên vật cản.
AK, M16, K54 và Mosin dùng chung động tác nạp đạn khi nằm từ
`Nam_Prone_Reload_MagazineOnly_v3.blend` theo yêu cầu dùng chung của người chơi.
Nhấn R khi nằm để thay băng, không có động tác kéo khóa lên đạn. Animation và
băng đạn cũ/mới lấy tiến độ trực tiếp từ `PlayerCombat.ReloadProgress`, dùng
thời gian thực tế sau nâng cấp Shooting và các hệ số reload hiện có.
Không có timer cố định riêng cho tư thế nằm. Đứng dậy bằng Z/Space hủy reload;
đổi vũ khí, chết hoặc vô hiệu hóa component khôi phục băng đạn gốc.
M16/K54 dùng băng đạn của model đang cầm; không tạo băng AK thay thế.
Mosin giữ băng trong và cơ chế nạp từng viên, chỉ mượn chuyển động thân/tay AK.
Controller tư thế đứng nhường animation cho hệ thống nằm, không hủy reload.
Clip bắn/ném riêng ở tư thế nằm không nằm trong bộ trườn đã duyệt.

## Cập nhật nguồn

1. Chạy `export_game_motion.py` bằng Blender để xuất `game-motion.json`.
   Chạy tiếp `export_prone_reload.py` để thêm take thay đạn đã duyệt.
2. Trong Unity: **ShadowVale → Animation → Nằm trườn → Cập nhật animation cho cả hai map**.
3. `NamProneSetup` bake resource và kiểm tra độ cao đầu, tiếp xúc mesh với sàn.

Chỉ bake các trận hình đã duyệt; không đưa FBX tham chiếu Mixamo hoặc Blender
vào bản build. Dữ liệu runtime nằm trong `Assets/_Project/Map01/Resources/Animations/NamProne.asset`.

## Kiểm tra

`NamProneIntegrationTests` mở từng map thật, vào Play Mode và gửi input qua
Unity Input System. Kiểm tra Z/Space, trườn, collider, vật cản trên đầu,
đổi súng/dao, ngắm và chuyển sang hồi máu. Reload AK được kiểm tra ở Shooting
cấp 0 và cấp tối đa; M16/K54/Mosin được kiểm tra giữ nằm, theo tiến độ reload,
hoàn tất nạp đạn. Kiểm tra thêm hủy khi đứng dậy và đổi vũ khí, phục hồi băng đạn.
Hai bài kiểm tra Map 1 và Map 2 đã pass sau khi mở rộng reload dùng chung.
Kết quả và ảnh chụp nằm trong
`Logs/AnimationPreview/NamCrawl/Unity/`.
