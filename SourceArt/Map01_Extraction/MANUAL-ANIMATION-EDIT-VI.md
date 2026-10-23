# Chỉnh animation Map 1 thủ công

## 1. Chọn đúng phần cần chỉnh

- Chạy cầm súng của Nam: `Assets/_Project/Art/Characters/Animations/Nam/Nam_Rifle_Run.fbx`, clip cùng tên trong `AC_Player` → `Locomotion_Rifle`. Extraction lấy đúng clip này từ controller. Không sửa `Board_Boat_StepDown` để thay tư thế chạy: đoạn chạy mới không dùng clip đó.
- Nam nhảy ngắn: `Assets/_Project/Map01/Resources/Cutscenes/Nam_Urgent_Board_Jump.anim`, được sao từ Jump.fbx. Công cụ `UrgentBoardingSetup.Prepare` sẽ tạo lại bản sao; không chạy lại nếu bạn đã chỉnh tay bản `.anim`.
- Hùng xoay: action `Hung_Boat_Seated_Travel` đang làm tư thế nền; Unity xoay actor và hiệu chỉnh hai tay gần đùi.
- Hùng bắt đầu/đang/dừng chèo: `Hung_Row_Start`, `Hung_Row_Loop`, `Hung_Row_Stop`.
- Ngồi và bắn: `Nam_Seated_Rifle_Ready`, `Nam_Seated_Rifle_Fire`, `Nam_Seated_Rifle_Reload`; Hùng có các action tương ứng.

## 2. Chỉnh trong Blender

1. Sao lưu `Map01_Extraction.blend`, rồi mở bản sao. Không chạy lại `author_extraction.py`: script này tạo lại nhiều action và sẽ ghi đè phần sửa tay.
Nếu chỉnh `Nam_Rifle_Run`, clip này không nằm trong bộ action extraction của file `.blend`: mở file Blender mới, **File → Import → FBX**, nhập `Nam_Rifle_Run.fbx`, chọn armature vừa nhập và action chạy, rồi lưu một `.blend` riêng. Xuất lại vào đúng FBX đó để gameplay và cutscene cùng nhận thay đổi. Không cần tạo animation chạy mới.

2. Chọn armature nhân vật đích (`Nam` hoặc `Hung`; có thể tìm bằng Outliner). Không chọn rig `Source_*` của Mixamo.
3. Đổi khu vực dưới sang **Dope Sheet → Action Editor**, chọn action cần sửa. Bấm số người dùng cạnh tên action để tạo bản riêng nếu action được chia sẻ; giữ một bản gốc.
4. Đặt FPS 30. Bật **Pose Mode**. Chọn xương `LeftArm/LeftForeArm/LeftHand`, `RightArm/RightForeArm/RightHand`, `Spine/Spine1`, `Hips` hoặc chân theo chi tiết cần chỉnh. Tên thực tế xem trong Outliner/Pose Bone, không đổi tên/xóa/thêm xương.
5. Chọn frame. Dùng **R** xoay, **G** di chuyển các control nếu action có IK. Chèn **I → Location & Rotation** hoặc **I → LocRotScale**. Với xương nối liền, chủ yếu xoay; tránh kéo dài xương tay/chân.
6. Ở Graph Editor, chỉnh chuyển tiếp bằng Bezier và **Auto Clamped** để giảm giật, tránh vượt quá góc đã đặt. Đặt ít nhất key ở đầu, trước/sau điểm nắm đạo cụ và cuối đoạn.
7. Kiểm tra góc trước, bên và trên. Xương bàn tay không luôn nằm tại tâm nắm tay; nhìn trực tiếp mesh. Giữ khuỷu hơi gập, cổ tay gần thẳng với cẳng tay, không để bàn tay xoay 90° tại thời điểm đổi hướng.
8. Khi đổi dáng chân, kiểm tra chân bám ghế/sàn với reference jetty/hull trong file. Chỉ chỉnh pose/control; quỹ đạo di chuyển actor trong Unity được quản lý riêng.
9. Nếu dùng constraint mới, **Pose → Animation → Bake Action**, bật Visual Keying, bước 1 frame, chỉ bones đã chọn nếu phù hợp. Giữ bản có constraint trong `.blend` để còn sửa tiếp.

## 3. Xuất một clip

- Chọn đúng armature, chọn đúng action, đặt Start/End đúng khoảng action. Với action loop, pose đầu/cuối phải khớp.
- File → Export → FBX: **Selected Objects**, chỉ **Armature**; Forward **-Z**, Up **Y**; tắt **Add Leaf Bones**.
- Bake Animation: bật; tắt **All Actions** và **NLA Strips**; Sampling Rate 1, Simplify 0. Không Apply Transform tùy tiện lên rig gốc.
- Xuất vào đúng file FBX của clip trong thư mục Extraction. Giữ nguyên tên file và `.meta` Unity.
- Chạy script `export_selected_action.py` từ Blender Text Editor là lựa chọn khác: chỉnh `ACTION_NAME`, `CHARACTER`, `OUTPUT_FBX` đầu file. Script giữ topology và chỉ xuất action được chọn.

## 4. Đưa clip đã sửa vào Unity

Các prefab extraction tham chiếu bản `.anim` đã lấy từ FBX. Reimport FBX thôi có thể chưa thay bản `.anim` đang dùng.

Cách sửa thủ công an toàn:
1. Reimport FBX, Rig → Humanoid, kiểm tra Avatar hợp lệ; Animation → preview.
2. Mở prefab `Assets/_Project/Map01/Resources/Cutscenes/Map01Extraction.prefab`.
3. Gán sub-clip của FBX vừa sửa vào trường tương ứng: `Row Start`, `Row Loop`, `Row Stop`, `Seated Fire`… Với các clip Hùng được chọn qua `Hung Clips`, thay phần tử có tên `Hung_...` tương ứng. Giữ clip names nhất quán để chọn variant đúng.
4. `Gameplay Rifle Run`: để trống sẽ lấy chính xác `Nam_Rifle_Run` từ controller; hoặc kéo đúng clip gameplay vào để chỉ định rõ.
5. `Hung Turn Seconds` chỉnh thời gian xoay (mặc định 0,85 giây, khoảng 26 frame), `Paddle Reach Seconds` chỉnh thời gian đưa tay tới chèo (0,75 giây, khoảng 23 frame). Đồng hồ này bắt đầu ở pha Boarding; khoảng đầu dành cho xoay, khoảng tiếp theo dành cho đưa tay tới cán.
6. `Apply Hand Contacts`: bật để Unity giữ tay tại súng/chèo. Tạm tắt khi xem riêng animation vừa sửa; bật lại để kiểm tra cảnh thực tế. Khi bật, tay chịu thêm IK trong `Map01Extraction`, nên sửa key trong Blender có thể chưa thay được điểm nắm cuối cùng.
7. Để chỉnh vị trí tay chèo cuối cùng, sửa các tọa độ trong `LateUpdate` của `Map01Extraction.cs`: target trái/phải hiện nằm trong tọa độ boat root. `reach` là độ đưa tay trước/sau; không tăng quá sâu vào bụng. Điểm súng nằm trong `HoldSeatedRifle`/`SupportRifle`. Sao lưu trước mỗi lần sửa.

Chạy lại Map 1 và xem từ trước khi Hùng xoay tới sau khi Nam ngồi. Kiểm tra cả phát tự nhiên và giữ Esc một giây. Không chỉnh asset trong Play Mode nếu muốn lưu thay đổi: các thay đổi scene lúc Play thường bị mất khi Stop.
