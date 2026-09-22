# Map 1 — Những dấu chân trong rừng

Scene: `Assets/_Project/Scenes/Maps/Map 1.unity` (Unity 6000.3.24f1).

Map rừng được mở rộng từ nguồn Blender hiện có lên 220 × 200 m. Có điểm nhận hàng, ba hướng vượt tuần tra, cầu gỗ, căn cứ bỏ hoang với bàn tài liệu, tuyến vòng phía đông và bến giao hàng phía bắc. Nhân vật vẫn dùng model placeholder; hình ảnh môi trường theo phong cách stylized.

## Prefab và tối ưu

- 1.398 cây dùng 5 prefab, mỗi prefab có 3 LOD dùng chung mesh: 1.972 / 904 / 400 tam giác.
- 613 thùng, phuy và đá dùng 3 prefab. Tổng 2.011 prefab instance còn liên kết trong scene.
- Prefab nằm tại `Assets/_Project/Art/Environment/Map01_Optimized/Prefabs`.
- 114 phần môi trường tĩnh; collider tách riêng, không dùng tán lá làm collider.
- Material môi trường dùng chung, hỗ trợ GPU instancing; màu chi tiết lưu trong vertex color.
- Khoảng 2,99 triệu tam giác khi toàn bộ cây ở LOD0, giảm 60,1% so với bản mở rộng chưa tối ưu. LOD xa giảm tiếp. Chưa đo FPS trên máy đích.
- `Map01_Environment.fbx` giảm từ khoảng 260,6 MB xuống 10,4 MB. Scene dùng mesh asset/prefab tách từ cùng dữ liệu Blender để giữ LOD và liên kết prefab.

## Nguồn và cập nhật

`SourceArt/Map01_Optimized/ShadowVale_Map01_Master.blend` là nguồn thiết kế. `ShadowVale_Map01_Optimized.blend` là bản tối ưu để xem và xuất. `optimize_map.py` tạo dữ liệu mesh, bố trí và LOD; `export_optimized_fbx.py` xuất FBX từ dữ liệu đó.

Trong Unity chọn `ShadowVale > Map 1 > Build Optimized Blender Map`. Rebuild thay thế scene sinh tự động: lưu riêng các chỉnh sửa thủ công trước khi chạy. Hoặc đóng project trong Editor rồi chạy `Tools/Build-Map01.ps1`.

## Chơi thử

WASD di chuyển; Shift chạy; C đi khom; chuột ngắm/bắn; Q ném đá; E tương tác; Tab mở túi; M bản đồ; B chế tạo tại bàn gần điểm nhận hàng; H hồi máu; F5/F9 lưu/tải; Esc tạm dừng.

Nhận hàng → vượt tuần tra và qua cầu → đọc bản đồ trong căn cứ → giao hàng tại bến phía bắc cùng Hùng. Không bắt buộc tiêu diệt lính. Lính có Patrol → Investigate → SpotPlayer → TakeCover → Engage. Đây là prototype Map 1 offline, chưa tích hợp squad solver/backend hoặc các map khác.

## Kiểm tra

48 đoạn NavMesh đạt kiểm tra đường đi. Play Mode đạt kiểm tra khởi tạo nhiệm vụ và ba lính, chặn bỏ qua nhiệm vụ, nhận hàng, qua cầu, thu tài liệu và giao hàng hoàn thành stage 4. Kiểm tra flow dịch chuyển giữa các mục tiêu; không thay thế lượt chơi thủ công toàn map. Audit scene xác nhận 2.011 prefab instance và không thiếu material. Báo cáo và ảnh nằm tại `Tools/Map01OptimizedReports`.

ContentBundle dùng TextAsset trong `Map01/Generated/Map01Content.json`; có kiểm tra tham chiếu thiếu/hỏng và guard chỉ khởi tạo sau khi mission sẵn sàng. Checkpoint phiên bản 2 để loại tọa độ cũ.


## Bản cập nhật đường mòn, cỏ và di chuyển
Đã áp dụng vào Map 1.unity: đường mòn cong, mái lá nhiều lớp, bốn prefab cỏ dùng chung mesh. Bỏ collider Stream boundary và bake lại NavMesh; ba tuyến kiểm tra suối đạt. Kiểm tra Play Mode qua InputSystem: Space nhảy cao 1,07 m và hạ đất; CharacterController đi xuống lòng suối ở cao độ -0,72 m. Camera và các actor hiện có được giữ lại. Chi tiết tại Tools/Map01OptimizedReports/movement-check.txt và river-revision.txt.
