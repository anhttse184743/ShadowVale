# Tiếp cận cứu Hùng, kéo vào bụi và nhặt đá

- Cutscene giới thiệu bắt đầu ở bán kính 40 m quanh Hùng khi chưa xem. Lính cứu Hùng kiểm tra cue này trước khi xét tầm nhìn. Save đã xem không phát lại.
- Phím **2** trang bị dao; **C/Ctrl** ngồi xuống và ẩn trong bụi đủ rộng. Giữ **Q** để ngắm, thả **Q** để ném đá dụ lính.
- Khi lính chưa phát hiện Nam, ở cách tối đa **2 m**, Nam ở bên hông hoặc phía sau, và có lối kéo/chỗ cho hai người: **icon dao + lá sáng xanh**. Bấm **chuột trái** để kéo. Nhánh kéo được ưu tiên khi hợp lệ; ngoài bụi vẫn dùng đâm sau lưng.
- Khi vào giằng co, nhả chuột sau thao tác mở đầu, rồi bấm khi kim vào vùng sáng. Cần ba lần thành công, được hụt một lần; hụt lần hai thì lính thoát.
- Đá nằm thành cụm trên các lối tiếp cận và quanh khu giam. Đến gần, nhấn **E** nhặt: mỗi cụm **3 viên**. Bố trí mặc định trên NavMesh hiện tại có **10 cụm / 30 viên**, không đặt dưới nước hoặc sát vị trí lính. Cụm đã nhặt ẩn đi, save/load giữ trạng thái và không nhân đôi.

Chỉnh trong Inspector của `Assets/Resources/Rescue/Map01RescueLayout.prefab`: `introRadius`, `stonesPerPile`, `stoneOffsets`. Offset là X/Z tính từ nơi Hùng bị giam. Giữ nguyên thứ tự slot sau khi có save vì chỉ số là ID của cụm đá. Marker vật phẩm được tạo lúc Play; không sửa địa hình, collider hoặc NavMesh.

Kiểm chứng: năm ca hồi quy/nhánh kéo/icon/Map 2 trong `rescue-approach-final.xml` đã qua; ca intro/nhặt/save-load được sửa tham chiếu scene trong bài test và kiểm tra riêng qua `rescue-approach-cached.xml`. Runtime của ca còn lại không thay đổi. Đã xem ảnh GameView `Bush-Ready.png`, lời nhắc và icon không bị cắt.
