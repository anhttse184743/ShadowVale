# Di chuyển tự do khi vận chuyển / dẫn người và thao tác giữ

Ngày sửa: 22/10/2026, theo yêu cầu mới của người dùng; thay thế giới hạn đi chậm và khóa trang bị lúc hộ tống của các đợt trước.

- Nam không bị giảm tốc hoặc cấm chạy do đang mang thùng, cuộn dây hay dẫn Tâm. Thể lực chung vẫn hoạt động. Giới hạn do vết thương trong cao trào Đêm 3 thuộc cơ chế riêng, không bị thay đổi trong lượt này.
- Tâm đi theo Nam theo NavMesh, không tự đi về đích và không dịch chuyển tức thời. Bỏ điều kiện đứng chờ khi cách Nam hơn 7 m. Vẫn cần cả hai đến vùng đích để hoàn thành nhiệm vụ.
- Hộ tống không còn đánh dấu tay Nam đang bận, nên có thể đổi trang bị và dùng súng như khi cứu Hùng ở Map 1. Mang hàng vẫn chiếm tay nhưng không khóa di chuyển hoặc camera.
- AK, K54 và Mosin dùng giữ chuột phải để ngắm, thả để hạ; áp dụng qua PlayerCombat chung cho cả hai map. Không còn chốt trạng thái ngắm bằng một lần nhấn. Tutorial và mô tả vũ khí đã đổi hướng dẫn theo cơ chế giữ.
- Map 2 dùng giữ F để quan sát bằng ống nhòm, thả F hạ xuống. Có thể di chuyển khi đang dùng; tiến độ ghi nhận mục tiêu vẫn yêu cầu đứng yên và nhìn đúng hướng. Mở bảng hoặc vào đoạn khóa điều khiển sẽ hạ ống nhòm.

Các cảnh kể chuyện và thao tác cố định như sửa máy, căn ăng-ten vẫn giữ cơ chế điều khiển riêng; thay đổi này tập trung vào di chuyển khi mang hàng/hộ tống và thao tác ngắm/ống nhòm.

## Kiểm thử

Unity **7/7 đạt**, báo cáo `Logs/Map02Chapter/free-movement-results.xml`:

- Input WASD thật theo bốn hướng, chạy, tham số Speed và chuyển động chân khi mang thùng, mang dây và dẫn Tâm.
- Tâm tiếp tục đi từ khoảng cách trên 7 m theo đường hợp lệ, không teleport; khôi phục hộ tống qua save/load.
- Ngắm AK/K54/Mosin theo trạng thái giữ/thả; ngắm và ống nhòm trong Map 2 không chặn bước chân. Thả F hạ ống nhòm và mở bảng ngắt quan sát.
- Các ca trạng thái/cuộn dây/checkpoint/lưu-tải Ngày 2/Đêm 2 hiện có.

Dừng Play rồi chạy lại Map 2 để kiểm tra bằng mã mới. WASD di chuyển, giữ chuột phải ngắm, giữ F dùng ống nhòm. Phím chạy vẫn theo thiết lập hiện có của dự án.
