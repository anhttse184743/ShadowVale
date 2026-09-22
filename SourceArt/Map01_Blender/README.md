# Map 1 — Những dấu chân trong rừng

Bản môi trường 3D dựng bằng Blender 5.2, dựa trên proposal ShadowVale, kịch bản Map 1 và ảnh rừng được cung cấp. Phạm vi là map và đạo cụ môi trường; chưa gắn gameplay.

## File trong dự án

- Nguồn chỉnh sửa: `G:\game\ShadowVale\SourceArt\Map01_Blender\ShadowVale_Map01.blend`
- Model Unity: `G:\game\ShadowVale\Assets\_Project\Art\Environment\Map01_Blender\Map01_Environment.fbx`
- Ảnh và script dựng lại: `G:\game\ShadowVale\SourceArt\Map01_Blender`

Mở file `.blend` trong Blender để chỉnh từng collection. Ba camera được đặt sẵn: tổng thể isometric, mặt bằng từ trên xuống, cận cảnh căn cứ. Các mái nhà có thuộc tính `cutaway_roof`, có thể ẩn khi cần xem nội thất.

Trong Unity, FBX là model môi trường để kéo vào scene. Scene Map 1 cũ không bị thay thế. FBX không chứa shader procedural của Blender; cần kiểm tra/chuyển material sang URP khi lắp vào scene. Bản màu gốc nằm trong `material_palette.json`.

## Bố cục

Khu đất 180 × 160 m, đơn vị mét. Blender dùng Z-up; FBX xuất Y-up. JSON anchor giữ nguyên tọa độ Blender và được ghi rõ hệ trục, chưa phải schema nội dung runtime của game.

1. Cụm nhà tiếp tế phía tây nam: điểm xuất phát Nam–Hùng, kiện hàng và vị trí bàn chế tạo.
2. Đường rừng dẫn vào khu tuần tra: đường chính rộng, lối vòng kín phía tây và nhánh đánh lạc hướng phía đông nam. Lối vòng tái nhập trước cầu.
3. Khu tuần tra: bao cát, đá chắn tầm nhìn, thân cây đổ và thùng gỗ. Có các anchor tuần tra, điều tra, cover và thoại sau chạm trán.
4. Suối uốn giữa map, bờ đá và cầu gỗ với sàn ván, cọc, giằng, tay vịn, lối lên xuống.
5. Căn cứ bỏ hoang ở đông bắc: hai nhà gỗ, hàng rào hỏng, bao cát, thùng rỗng và khu bàn bản đồ mở. Bản đồ cùng giấy ghi chép đặt trên bàn là manh mối tuyến tiếp tế bị theo dõi.
6. Cao điểm quan sát phía tây bắc và đường rời căn cứ hướng bến sông.

Phong cách là môi trường 3D cách điệu với hình khối rõ khi nhìn isometric; không phải bản sao ảnh chân thực. Bối cảnh dùng thế giới hư cấu theo proposal. Tất cả mesh được dựng thủ tục, không dùng asset tải bên ngoài.

## Trạng thái kiểm tra

- Đã dựng và lưu Blender; xuất FBX có vật liệu màu cơ bản.
- Đã render và kiểm tra ảnh tổng thể, mặt bằng và căn cứ.
- Chưa tích hợp collider/NavMesh, điều khiển, AI, trigger, loot hoặc quest vào bản model này.
- Chưa kiểm thử đường đi hay FPS trong Unity. Bản chi tiết có khoảng 6,37 triệu tam giác trước modifier, cần LOD/culling và tối ưu khi bước sang tích hợp gameplay; không coi là đạt mục tiêu 60 FPS.

## Dựng lại

Tạo một thư mục làm việc riêng với thư mục con `outputs`. Chạy Blender background với `build_map.py`, sau đó `refine_map.py`, cuối cùng `finish_map.py`, theo đúng thứ tự. Các script ghi đè kết quả trong `outputs`; không chạy đè trên file đã chỉnh tay. `export_map.py` xuất vào đường dẫn dự án nêu trên, vì vậy chỉ chạy khi muốn cập nhật FBX. File `.blend` giao kèm đã hoàn chỉnh, không cần chạy script để mở xem.
