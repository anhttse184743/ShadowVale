# Chỉnh bộ chuyển động cập bến hiện tại

Mở `Map02_Arrival.blend`. Bộ đang dùng nằm trong các action không có hậu tố
`BeforePolish` hoặc `Static_Backup`. Các bản có hậu tố là tham chiếu cũ.

Trong Pose Mode → Dope Sheet → Action Editor, chọn rig Nam hoặc Hung và action:

- `Arrival_Row_Loop`: 0–72, chèo khi tới Map 2; giữ đầu thẳng và chuyển động nhỏ.
- `Arrival_Travel`: 0–90, ngồi quan sát khi thuyền tiến vào bến.
- `Oar_Stow`: 0–120, theo dõi chèo bằng mắt/đầu rồi trở về hướng trước.
- `Boat_Stand`: 0–90, chống tay, đưa trọng tâm ra trước, đứng và thả tay.
- `Boat_Turn`: 0–20, bước nhỏ khi đổi hướng trong lòng thuyền.
- `Jetty_StepUp`: 0–132, lên bậc và thu cả hai chân về mặt bến.
- `Walk_Ashore`: 0–36, đi bộ có pha chân trụ và pha nhấc chân rõ ràng.
- `Standing_Guard`: 0–90, đứng cân bằng, đầu hướng trước.

Giữ 30 fps, tên và rest pose của 28 xương. Chỉnh key rotation trong Pose Mode,
không Apply Pose as Rest Pose. Xem góc bên để kiểm tra đầu gối và vị trí đế giày;
xem góc trước để kiểm tra vai/cổ tay. Khung đầu/cuối của clip loop phải khớp.
Frame −1 giữ tư thế trung tính để đối chiếu thủ công; không đưa vào khoảng
export 0–frame cuối. Unity dùng `Nam_Arrival_Bind.fbx` và
`Hung_Arrival_Bind.fbx` làm tham chiếu xương trung tính chung, không dùng tư thế
ngồi ở frame đầu clip làm T-pose. `export_bind_references.py` xuất hai FBX
tham chiếu từ mesh và skeleton gốc, không có animation; chỉ chạy lại khi cần.
Menu nhập clip tạo các `*.avatar.asset` từ cùng tư thế trung tính, giữ trục
cổ/đầu và kích thước cơ thể nhất quán. Không chọn Create From This Model trực
tiếp trên FBX đang ngồi/chèo; Unity có thể tính lại kích thước từ chân gập.

`polish_arrival.py` tái tạo bộ clip này từ các rig và nguồn có sẵn. Nó dùng chiều
dài khớp thực để giải chân, hạ hông vừa đủ trong tầm với, và kiểm tra sai số tiếp
xúc/góc đầu sau bake. Chạy lại script sẽ ghi đè chỉnh sửa thủ công, nên hãy lưu
bản sao trước. Không chạy `rebuild_locomotion.py` để ghi đè bộ đi bộ mới.

Để xuất riêng action đã chỉnh, dùng `export_selected_action.py` và đổi CHARACTER,
ACTION_NAME. Chỉ xuất rig chọn, bake sampling 1, simplify 0; tắt leaf bones, All
Actions và NLA Strips. Sau đó chạy menu Unity **ShadowVale → Cutscene → Prepare
Map 2 arrival clips**. Runtime phát các `.anim` trong
`Assets/Resources/Cutscenes/Map02`, nên chỉ đổi FBX chưa đủ.

`Map02Arrival.cs` giữ các điểm tiếp xúc thật:

- Sàn trong thuyền: local Y −0.15; gốc thuyền nổi ở world Y 0.24.
- Bậc: world Y 0.41 / 0.62 / 0.83 / 1.04 / 1.25.
- `StairContact`: tám bước gồm bước lên mạn, năm bậc và thu chân trên bến.
- `WalkStride`: quãng đường của một chu kỳ hai chân, hiện 1 m. Đổi độ dài bước
  trong Blender phải đổi giá trị này để tốc độ chân khớp tốc độ di chuyển.
- `LaneDuration`: 2.4 s gồm đổi hướng, đi tới lối lên và quay về phía bậc.
- `paddlePark`: vị trí đặt chèo dọc mạn phải; giữ chèo trong thuyền, xa lối lên.
- Đế giày được đo từ mesh ở tư thế đứng; runtime giữ chân trụ trên bậc và
  dùng mặt va chạm đất thực khi bàn giao. Không thêm bù Y cố định cho Hùng.

Kiểm tra bằng **Preview Map 2 arrival**: đi hết và giữ Esc một giây. Sau khi lên
bờ thử aim, bắn, reload, di chuyển và đổi súng. Bài `Map02ArrivalTests` quay cảnh,
kiểm tra tiếp xúc chân/góc đầu và thử bắn sau chuyển map; `ShotTracerLifecycleTests`
kiểm tra việc pool tia đạn theo Nam và không tăng số đối tượng khi bắn liên tục.
