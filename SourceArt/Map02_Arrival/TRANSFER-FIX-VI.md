# Kiểm tra chuyển thuyền và bước lên bến Map 2

Map 2 tự thiết lập trạng thái đạo cụ khi nhận nhóm nhân vật từ Map 1. Hùng không cầm súng trong các đoạn chèo, cất chèo, đứng dậy và lên bến. Súng chỉ được bật lại sau khi cutscene kết thúc và nhóm đã đứng trên bờ. Quy tắc này áp dụng cả khi chơi tiếp bình thường và khi tải bản lưu đã hoàn thành Map 1.

Thân thuyền, ghế và phần gỗ mái chèo có renderer, mesh và material riêng cho chuyển map; renderer gốc được tắt để tránh vẽ trùng. Bộ render mới dùng MaterialPropertyBlock và không phụ thuộc culling/static batch của scene đã đóng. Giữ nguyên hình học, màu gỗ, transform và pivot gốc; không đổi cấu hình render toàn dự án.

Trong `Map02Arrival.cs`, `StairX` là vị trí cổ chân, không phải tâm bậc. Mũi giày hướng về -X trong hệ tọa độ bến, nên cổ chân đặt gần mép ngoài bậc để mũi giày không lọt vào bậc kế tiếp. `StairY` là độ cao mặt đỡ trong hệ tọa độ thuyền đã cập bến. `StairContact` chia mỗi bước thành nhấc chân, chuyển chân qua mép bậc và đặt xuống; độ nhấc 14 cm được cộng trên bậc cao hơn. `StairSupportHeight` điều khiển thân người theo chuyển trọng lượng, không kéo thân lên cùng độ nhấc của chân đang vung.

`PlantStairFeet` căn hướng mũi chân, giữ chân chịu lực và giải IK hai chân trên rig gốc. Các clip đứng, cất chèo, bước lên bến và đi bộ hiện có vẫn được dùng. Muốn chỉnh tay hoặc thân trong Blender, xem `POLISH-EDITING-VI.md` và `MANUAL-ANIMATION-EDIT-VI.md`; muốn chỉnh chỗ đặt chân trên địa hình, kiểm tra lại các thông số runtime ở trên sau khi xuất clip. Không đổi rest pose, scale hoặc tên xương.

Khi đi bộ, `LevelWalkingFoot` dùng bind pose của model gốc để lấy mặt đế trung tính, không lấy trục cổ chân → ngón chân làm mặt đế. Cả chân chịu lực và chân đang vung được căn theo pháp tuyến mặt đỡ. Test đo hai nhóm tiếp xúc gót/mũi của đế giày và ghi ảnh góc thấp `feet-####.png` cho cả ba người. Xem `BOAT-FEET-STONE-FIX-VI.md` để biết chi tiết bản sửa mới và động tác ném đá khi ngồi.

Kiểm thử `Map02ArrivalTests` kiểm tra luồng thường, bản lưu hoàn thành và skip; Hùng không có súng đang bật trong cinematic; thân thuyền đi cùng hành khách; cổ chân bám điểm đỡ; đế giày không xuyên khối bậc quá 2 cm; giữ nguyên máu, đạn, vật phẩm, aim và bắn sau bàn giao. Video và báo cáo mới nằm trong thư mục `deliverables/boat-feet-stone` của chat, dựng từ ảnh render thực tế của Unity theo thời gian cinematic và không có âm thanh.

Phần chuyển map nằm trong `Map02Arrival.cs` và test tương ứng. Bản sửa ném đá khi ngồi được mô tả riêng trong `BOAT-FEET-STONE-FIX-VI.md`. Cảnh Map 1, cảnh Map 2, các animation đang có và bản sửa đi vào hầm Map 1 được giữ nguyên. Khi nhận bản sửa: dừng Play, chờ Unity import và biên dịch xong rồi chạy lại từ Map 1 hoặc bản lưu hoàn thành; một cinematic đang chạy giữ trạng thái cũ sẽ không tự dựng lại đúng đạo cụ sau hot reload.
