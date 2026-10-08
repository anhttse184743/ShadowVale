# Sửa thuyền, bàn chân Map 2 và ném đá khi ngồi

## Phạm vi

Sửa đoạn chuyển Map 1 → Map 2 và động tác ném đá khi Nam đang ngồi thấp/lén lút. Không chỉnh địa hình, model, skeleton, luật giải cứu, các đợt hộ tống hoặc thoại. Animation đi lên bến, cất chèo và đứng dậy đang có vẫn được tái sử dụng.

## Thuyền không còn phụ thuộc renderer của scene đã đóng

`Map02Arrival.PrepareBoatRendering` tạo renderer mới với bản sao mesh và material cho thân thuyền, ba ghế và phần gỗ mái chèo trước khi Map 1 bị unload. Các bản sao thuộc riêng thuyền chuyển map, không tham gia static batch của scene cũ và có MaterialPropertyBlock để tránh dữ liệu culling của GPU Resident Drawer. Renderer gốc được tắt để tránh vẽ trùng. Transform và pivot cũ vẫn giữ nguyên, vì vậy ghế, hành khách và điểm giữ mái chèo tiếp tục đi cùng thân thuyền. Mesh và material được giải phóng khi thuyền bị hủy.

Test checkpoint cố ý tắt renderer gốc và đặt `forceRenderingOff` trước khi chuyển map. Renderer mới phải hiện, ba ghế phải hiện, thuyền phải đi cùng hành khách và Hùng không có súng trong tay trong cinematic.

## Căn theo đế giày

Không dùng hướng xương cổ chân → xương ngón chân làm mặt phẳng đế giày: hai điểm xương này nằm ở độ cao khác nhau trong model gốc. `PrepareBindFeet` đọc bind pose của chính mesh nhân vật để lấy hướng chân và ngón trung tính. `PrepareShoeContacts` đo điểm đế giày từ mesh đã skin để tính độ cao cổ chân và hai nhóm tiếp xúc gót/mũi.

Khi đi, `LevelWalkingFoot` căn mặt đế theo pháp tuyến collider mặt đất cho cả chân chịu lực và chân đang vung. IK đùi/cẳng chân vẫn thực hiện nhấc, bước và đặt chân theo clip. Trên bậc, `StairContact` giữ lộ trình bước và khoảng hở bậc hiện có; không chỉnh rig hoặc áp dụng hiệu chỉnh này cho locomotion chung của Map 1.

## Ném đá khi ngồi

Hai clip mới, 30 fps:

- `Assets/_Project/Art/Characters/Animations/StoneThrow/Nam_Crouch_ThrowAim.anim`: giữ tay lấy đà khi giữ Q; clip riêng tránh phụ thuộc `cycleOffset` của state có speed bằng 0.
- `Assets/_Project/Art/Characters/Animations/StoneThrow/Nam_Crouch_Throw.anim`: lấy đà, ném, theo đà và thu tay. Phần ngồi/lén lút của chân vẫn do base layer điều khiển.

`AC_Player` chỉ bổ sung hai state `Act_ThrowAimCrouch` và `Act_ThrowCrouch` vào `ActionsUpper`, chọn bằng `Sneaking`; không đổi các state đi bộ, dao, rifle, ám sát hoặc cutscene khác. `Map01NamActions` tạm ẩn renderer của vũ khí trong động tác và hiện viên đá trong tay, sau đó khôi phục đúng renderer trước đó. Loại vũ khí đang trang bị, đạn và máu không đổi.

`Map01StoneThrow` lấy điểm đầu quỹ đạo từ bàn tay thật, chờ 0,18 giây sau khi thả Q để đá rời tay đúng nhịp. Vật phẩm vẫn trừ đúng một lần khi thả Q; hủy bằng chuột phải không trừ đá. Luật tiếng đá và điều tra của lính được giữ nguyên.

## Chỉnh thủ công

1. Dừng Play. Trong Unity Project, chọn một trong hai file `.anim` ở thư mục `StoneThrow`, mở **Window → Animation → Animation** và bật Preview trên model Nam.
2. Clip `Throw` dài 2 giây. Khoảng 0,84 giây là tư thế lấy đà; 1,16 giây là lúc tay đưa ra trước; đoạn sau thu tay. Giữ nguyên các curve Root/Leg; chỉnh các muscle curve của Shoulder, Arm, Forearm, Hand và Chest. Kiểm tra tư thế ở camera bên và phía trước.
3. `ThrowAim` là bản giữ của tư thế ở 0,84 giây. Nếu chỉnh tư thế lấy đà trong `Throw`, cần cập nhật clip `ThrowAim` cùng tư thế để chuyển tiếp không giật.
4. Muốn tái tạo từ script: menu **ShadowVale → Characters → Polish crouched stone throw** chạy `CrouchStoneAnimationSetup.Build`. Script này sẽ ghi lại hai clip, nên sao lưu clip đã chỉnh tay trước khi chạy.
5. Đổi nhịp văng đá thì chỉnh `Map01NamActions.StoneReleaseDelay` và xem lại cả viên đá lẫn cánh tay. Muốn chỉnh vị trí bước lên bến thì chỉnh `StairX`, `StairY`, `StairDuration` trong `Map02Arrival.cs`, kiểm tra lại cả đế giày và mép bậc; không chỉnh bind pose hoặc scale skeleton.

## Nhận bản sửa

Dừng Play, chờ Unity import và compile xong rồi mở lại lượt chơi mới hoặc tải bản lưu hoàn thành Map 1. Một cinematic đã được tạo trước khi thay code không tự dựng lại bộ render mới sau hot reload.

Kết quả kiểm chứng và video được ghi riêng trong báo cáo bàn giao. Video là các frame render thực tế từ Unity, không có âm thanh.
