# Sửa cổ tay khi nhấc túi tiếp tế — 16/10/2026

## Lỗi và bản sửa

Đo bản trước sửa trên Avatar Nam: cổ tay có góc tối đa 179,44°, một bước giữa hai frame 178,61° quanh 3,4–3,7 giây. Blender nguồn cũng có một bước 52,62° do giới hạn cổ tay được áp ngay khi bắt đầu IK, không trộn theo trọng số tiếp cận.

- Trộn giới hạn cổ tay theo trọng số tiếp cận trong nguồn Blender, chỉ cho nhánh nhấc túi.
- Clip nhấc túi dùng transform animation trên đúng skeleton Nam, phát với Avatar Generic được dựng từ chính hierarchy hiện tại để tránh đổi qua muscle Humanoid làm lật cổ tay. Không sửa model, skin weights, rest pose hoặc 62 xương gốc.
- Chỉ đổi Avatar khi bắt đầu clip Nam_SupplyCollect, sau đoạn bước đến hòm; trả Avatar/controller Humanoid cũ khi kết thúc hoặc hủy session. Cơ chế này không áp vào cầm súng, ném đá, radio, bàn giao hàng hoặc Map 2.
- Điểm gắn túi vẫn theo bàn tay; truy cập xương bằng tên gốc trong nhánh exact-skeleton để không gọi GetBoneTransform khi Animator đang Generic. Chest vẫn là Spine1 như Avatar gốc.

## Kiểm tra

`Logs/SupplyArm-tests.xml`: **2/2 đạt**, Unity thoát mã 0:

1. Sample 205 frame ở kho thật, chụp 9 góc cận theo thời điểm nhấc túi; kiểm tra tay thực sự chuyển động, không chấp nhận clip đứng yên/unbound; Avatar/controller, điều khiển và camera được trả đúng.
2. Chạy lại lấy chìa → mở kho/giải hòm → nhấc túi → giữ aim qua bàn giao cinematic → báo cáo → giao hàng và thưởng một lần; đạn và trang bị giữ nguyên.

Đo trong game: cổ tay lúc giữ/nhấc túi tối đa **18,00°**, bước đổi góc lớn nhất toàn clip **14,04°/frame**, biên độ chuyển động tay **0,893 m**. Góc tối đa toàn clip 80,71° nằm ở động tác mở hòm trước lúc nắm túi; không dùng nó làm số đo độ xoắn khi giữ quai.

Đã xem các ảnh cận 3,3/3,63/4,2 giây và contact sheet của 9 mốc. Video chẩn đoán `Logs/Map01Operations/video/supply-wrist-repair.mp4` được quay bằng fixture sample từng frame trên địa hình thật, không có track audio. Không phải video chơi thủ công toàn nhiệm vụ.

## Nguồn chỉnh sửa và tái tạo

- Blender mới: `SourceArt/Map01Operations/Nam_SupplyCollect_Wrist.blend`; giữ file Nam_SupplyInteractions.blend trước đó.
- Chọn rig Nam, action Nam_SupplyCollect. Đoạn nắm/nhấc ở 3,3–5,4 giây; bake 30 fps. Không đổi tên xương, trục hoặc rest pose.
- Tái tạo: Blender `--background --python SourceArt/Map01Operations/author_supply_animation.py -- --collect-only`. Chỉ xuất lại Nam_SupplyCollect.fbx và blend/receipt riêng, không dựng lại Handoff/RadioPanel.
- Nhập Unity: `ShadowVale.Map01.Editor.Map01OperationAnimationSetup.Import`. Tạo bản Generic dẫn xuất Nam_SupplyCollect_ArmSource.fbx và Nam_SupplyCollectAvatar.asset; giữ GUID clip Resources hiện tại. Clip nguồn Humanoid vẫn có thể kiểm tra trong importer, runtime pickup dùng clip exact-skeleton đã bake.
- Đo lại: `ShadowVale.Map01.Editor.Map01SupplyArmReview.Measure`, hoặc chạy SupplyPickupTests.
- SHA-256 các asset/source hiện tại: `SourceArt/Map01Operations/asset-receipt.json`; rest/bone receipt: collect-wrist-receipt.json.

Các lượt thử mask/Generic trên Avatar Humanoid cho tay không chuyển động đã bị loại bỏ; bản cuối có kiểm tra biên độ tay và ảnh trong scene, không dựa riêng vào số góc bằng 0.