# Kiểm tra chuyển thuyền và bước lên bến Map 2

Map 2 tự thiết lập trạng thái đạo cụ khi nhận nhóm nhân vật từ Map 1. Hùng không cầm súng trong các đoạn chèo, cất chèo, đứng dậy và lên bến. Súng chỉ được bật lại sau khi cutscene kết thúc và nhóm đã đứng trên bờ. Quy tắc này áp dụng cả khi chơi tiếp bình thường và khi tải bản lưu đã hoàn thành Map 1.

Thân thuyền, ghế và mái chèo được giữ hiện khi chuyển scene. Chỉ các renderer nhỏ của nhóm đạo cụ này dùng MaterialPropertyBlock để tránh phụ thuộc dữ liệu GPU Resident Drawer của scene vừa đóng; màu gỗ và các thuộc tính vật liệu cũ được giữ lại. Không đổi cấu hình render toàn dự án hoặc mesh của thuyền.

Trong `Map02Arrival.cs`, `StairX` là vị trí cổ chân, không phải tâm bậc. Mũi giày hướng về -X trong hệ tọa độ bến, nên cổ chân đặt gần mép ngoài bậc để mũi giày không lọt vào bậc kế tiếp. `StairY` là độ cao mặt đỡ trong hệ tọa độ thuyền đã cập bến. `StairContact` chia mỗi bước thành nhấc chân, chuyển chân qua mép bậc và đặt xuống; độ nhấc 14 cm được cộng trên bậc cao hơn. `StairSupportHeight` điều khiển thân người theo chuyển trọng lượng, không kéo thân lên cùng độ nhấc của chân đang vung.

`PlantStairFeet` căn hướng mũi chân, giữ chân chịu lực và giải IK hai chân trên rig gốc. Các clip đứng, cất chèo, bước lên bến và đi bộ hiện có vẫn được dùng. Muốn chỉnh tay hoặc thân trong Blender, xem `POLISH-EDITING-VI.md` và `MANUAL-ANIMATION-EDIT-VI.md`; muốn chỉnh chỗ đặt chân trên địa hình, kiểm tra lại các thông số runtime ở trên sau khi xuất clip. Không đổi rest pose, scale hoặc tên xương.

Kiểm thử `Map02ArrivalTests` kiểm tra luồng thường, bản lưu hoàn thành và skip; Hùng không có súng đang bật trong cinematic; thân thuyền đi cùng hành khách; cổ chân bám điểm đỡ; đế giày không xuyên khối bậc quá 2 cm; giữ nguyên máu, đạn, vật phẩm, aim và bắn sau bàn giao. Video trong `Verification/TransferFix` được dựng từ ảnh render thực tế của Unity theo thời gian cinematic, không có âm thanh.

Các thay đổi chỉ nằm trong `Map02Arrival.cs` và test tương ứng. Cảnh Map 1, cảnh Map 2, animation và bản sửa đi vào hầm Map 1 được giữ nguyên. Khi nhận bản sửa: dừng Play, chờ Unity import và biên dịch xong rồi chạy lại từ Map 1 hoặc bản lưu hoàn thành; một cinematic đang chạy giữ trạng thái cũ sẽ không tự dựng lại đúng đạo cụ sau hot reload.
