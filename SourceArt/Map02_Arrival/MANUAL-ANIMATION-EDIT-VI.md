# Chỉnh animation và xem lại đoạn cập bến Map 2

Để xem nhanh trong Unity, chọn **ShadowVale → Cutscene → Preview Map 2 arrival**. Công cụ mở Map 1, chuyển nhanh qua extraction và chạy đoạn cập bến Map 2 bằng đúng ba nhân vật. Bản lưu preview nằm riêng trong `Temp/Map02ArrivalPreviewSaves`; không ghi đè bản lưu chơi thật. Dừng Play rồi sửa asset để thay đổi được giữ lại.

## Chỉnh trong Blender

Mở `SourceArt/Map02_Arrival/Map02_Arrival.blend`. File giữ mesh, texture và rig gốc, các action Map 1, nguồn Mixamo và tham chiếu bến phía nam từ Map 2 hiện tại.

1. Chọn armature **Nam** hoặc **Hung**, vào **Pose Mode**.
2. Mở **Dope Sheet → Action Editor** và chọn action cần chỉnh:
   - `Boat_Stand`: frame 0–90, đứng dậy khỏi ghế.
   - `Jetty_StepUp`: 0–102, bước lên bậc.
   - `Walk_Ashore`: 0–36, đi về bờ, loop.
   - `Oar_Stow`: 0–120, hạ tay/cất chèo.
   - `Standing_Guard`: 0–90, đứng quan sát và thở, loop.
   Tên đầy đủ có tiền tố `Nam_` hoặc `Hung_`.
3. Chọn xương, sửa pose rồi **I → Location & Rotation**. Kiểm tra cả góc bên và phía trước; sửa cổ tay, vai, đầu gối bằng các thay đổi nhỏ.
4. Giữ **30 fps** và nguyên rest pose/tên/thứ tự 28 xương. Không thêm xương, không Apply Pose as Rest Pose.
5. Bật collection **REFERENCE — Map 2 southern landing** để nhìn bến, thuyền và bậc. Năm mặt bậc trong Unity cao **0.41 / 0.62 / 0.83 / 1.04 / 1.25 m**. Bến xoay khoảng **−14.1°**; đừng ép đường bước thành thẳng theo trục thế giới.
6. Lưu bản sao `.blend` trước mỗi thay đổi lớn.

## Xuất clip đã sửa

Mở `export_selected_action.py` trong Blender Text Editor. Đổi `CHARACTER` và `ACTION_NAME`, sau đó **Run Script**. Script đưa rig về hệ tọa độ xuất, bake 30 fps, tắt leaf bones và khôi phục vị trí trưng bày sau khi xuất.

Trong Unity chạy **ShadowVale → Cutscene → Prepare Map 2 arrival clips**. Lệnh cập nhật các clip Humanoid `.anim` ở `Assets/Resources/Cutscenes/Map02`. Runtime đọc những `.anim` này, nên chỉ thay FBX mà không chạy lệnh sẽ chưa thay animation đang phát.

`author_arrival.py` tái tạo bộ clip ban đầu và file nguồn. **Không chạy lại sau khi chỉnh tay** nếu muốn giữ các sửa đổi: nó tạo lại action, FBX và `.blend`.

## Chỉnh chuyển động trong Unity

`Assets/_Project/Map01/Runtime/Map02Arrival.cs` điều khiển đoạn này:

- `Route`, `Docked`, `DockRotation`: đường kênh và tư thế cập bến.
- `BuildLandingSteps`: kích thước bậc gỗ; các bậc đứng yên khi thuyền đến.
- `ExitPassenger`: nhịp đứng dậy, chuyển về lối giữa, lên bậc và đi về bờ. Nam trước, chỉ huy sau, Hùng cuối.
- `PlantStairFeet`: giữ chân trên từng bậc, hiệu chỉnh chiều cao bàn chân từ skin ở tư thế đứng. Sửa tọa độ bậc thì phải sửa các điểm tiếp xúc này cùng lúc.
- `LateUpdate`: tiếp xúc tay với súng/chèo; `Map01Extraction` cung cấp các hàm contact dùng chung.
- `SpawnPoints`: lấy trên NavMesh khô ở bờ phía tây, không dùng mặt nước làm điểm spawn.

Nhịp mặc định: tiến thuyền **12 s**, cập bến/cất chèo **4 s**, lên bờ **26 s**. Clip không có root motion; runtime di chuyển root theo địa hình. Vì vậy sửa độ dài bước trong Blender cần đi cùng nhịp và khoảng cách trong Unity để tránh trượt chân.

Kiểm tra lại từ preview: playback tự nhiên; giữ Esc một giây khi đang tiến thuyền hoặc lên bậc; sau bàn giao thử giữ aim, nhả aim và di chuyển. Hai cách phải đưa cả nhóm tới cùng điểm trên bờ.

Hiệu chỉnh độ nổi: `WaterlineRoot = 0.24 m`; mặt nước 0.045 m, sàn trong thuyền −0.15 m theo local. Không hạ root dưới mức sàn khô. Các bậc giữ nguyên độ cao world khi thay độ nổi. `ArrivalStowContacts` và `ArrivalRestHands` pha hai tay riêng: tay trái thả trước, tay phải đặt chèo xuống sau. `Boat_Stand` dùng tư thế ngồi đi thuyền → cúi người lấy đà → đứng; không đảo clip xoay/ngồi.
