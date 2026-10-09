# Giữ thuyền khi chuyển từ Map 1 sang Map 2

Bản sửa chỉ thay cơ chế chuyển hình học của thuyền trong Map02Arrival và bổ sung kiểm thử. Không đổi địa hình, đường thuyền, clip nhân vật, nhiệm vụ hoặc dữ liệu checkpoint.

## Thay đổi

- Mesh Sampan gốc có tâm tại tọa độ Map 1 (1.55, 0.05, 87). Bản sao dùng trong Map 2 được đưa về tâm mesh bằng 0; pivot và vị trí hiển thị được bù đồng thời, giữ nguyên hình dạng và vị trí thân thuyền.
- Thân, ba ghế và hai phần mái chèo có object hiển thị độc lập, không nằm bên dưới object nguồn có thể bị vô hiệu hóa khi Map 1 bị dỡ.
- Sau khi tải Map 2 và chuyển boat root vào scene đích, tạo renderer mới từ mesh/material đã giữ lại. Không tái sử dụng đăng ký render của scene cũ. Object render cũ được tắt và xóa; không vẽ trùng hình học.
- Dùng property block mới lấy màu từ material đã sao chép, không kế thừa culling/override từ scene cũ. Đây là cách dùng API công khai để các đạo cụ nhỏ này render ngoài GPU Resident Drawer; địa hình vẫn dùng cấu hình render hiện có. [Tài liệu Unity](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/make-object-compatible-gpu-rendering.html).
- Nếu không có mesh thân thuyền khi nhận party, báo lỗi rõ ràng thay vì bắt đầu một cutscene thiếu thuyền.

## Kiểm tra

Hai kiểm thử mới chạy toàn bộ boarding và departure Map 1, đợi completion/autosave, chuyển tự động sang Map 2 và chạy hết cập bến/lên bờ. Không gọi Skip để bỏ qua departure.

Một kiểm thử quay Game View bằng ScreenCapture, không dùng Camera.Render cho video. Kiểm thử còn lại quay camera trực tiếp để đối chiếu. Kiểm thử Game View cũng cố tình vô hiệu hóa renderer và object nguồn tại sự kiện dỡ Map 1, kiểm tra scene đích khôi phục thuyền độc lập.

Các kiểm thử cũ tiếp tục kiểm tra checkpoint hoàn tất, skip, chân bám bậc/đất, súng sau cinematic và giữ nguyên máu/đạn/vật phẩm. Báo cáo XML và video kiểm tra được bàn giao riêng.

Lưu ý: luồng thông thường trong Unity CLI trước bản sửa không tái hiện được lỗi mất thuyền. Bản sửa loại bỏ các phụ thuộc vào object/render state của scene cũ; bài thử vô hiệu hóa nguồn khi unload kiểm tra trực tiếp tình huống đó. Không coi việc object còn tồn tại là bằng chứng thuyền được vẽ: có kiểm tra ảnh Game View.

## Chạy lại

Unity Test Runner > EditMode > ShadowVale.Map01.Tests.Map02ArrivalTests.

FullGameViewDepartureKeepsBoatVisibleAfterSceneLoad kiểm tra luồng qua Game View; FullNaturalDepartureKeepsBoatVisibleAfterSceneLoad quay camera đối chiếu. Kiểm thử dùng thư mục save riêng trong Logs để không ghi đè save đang chơi.
