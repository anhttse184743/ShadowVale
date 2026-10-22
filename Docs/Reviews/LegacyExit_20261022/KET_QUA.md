# Gỡ tương tác giao hàng cũ ở bến — 22/10/2026

Ảnh người dùng chỉ tới `river_exit`, một ForestPoint loại Exit còn được lưu trong scene Map 1 với nhãn “Giao hàng và rời rừng”. Điểm này thuộc luồng giao hàng/thoát map cũ: handler hiện chỉ hiện câu báo chưa xong nhiệm vụ, không bàn giao hàng hoặc hoàn tất nhiệm vụ. C1 hiện bàn giao cho chỉ huy ở hầm; rút lui hiện do Map01Extraction điều khiển sau báo cáo C3.

Đã loại các điểm Exit cũ khỏi đăng ký gameplay và tắt component tương tác khi mission khởi tạo. Chúng không còn vào danh sách điểm, chiếm lời nhắc E hoặc trả lời giao hàng khi code cũ gọi trực tiếp. Giữ ID enum để scene và save cũ không bị đổi nghĩa. Không xóa nhà kho, thùng gỗ, bến, địa hình hoặc ghi lại scene. Không thay nhiệm vụ C1, phần thưởng, kho cấp phát hoặc luồng lên thuyền sau C3.

Kết quả: **4 ca chức năng đạt**, gồm lời nhắc/tương tác tại vị trí cũ cùng kho căn cứ; bàn giao C1 qua chìa/khóa/hội thoại; phím E báo cáo C3; lên thuyền, rời bến và hoàn tất map theo luồng tự nhiên. `tests.xml` ghi lượt đầu 3/4; `boarding.xml` ghi bài lên thuyền tự nhiên đạt 1/1, Unity thoát 0. Scene và dữ liệu vật phẩm không đổi.

Một ca khác, `GroundedMatchingActorsAndBoardingWithAllEnemiesAlive`, dừng ở assertion tỉ lệ chiều cao Nam–Hùng trước khi kiểm phần lên thuyền: sai lệch 10,23% so với ngưỡng 2%. Đã đối chiếu bằng ba file runtime nguyên bản tại commit `b78dcaab0dd827135ac7e19d3dc701c9984effce`; cùng ca vẫn lỗi ở phép đo đó, sai lệch 11,19% (`baseline.xml`). Đây là lỗi đã tồn tại trên bản trước sửa, không sửa/nới assertion hoặc thay animation trong đợt gỡ tương tác này. Ba file runtime của task được sao lưu và khôi phục có kiểm tra SHA-256 (`baseline-files.json`) trước lượt kiểm rời bến tự nhiên.

Tổng cộng 5 ca riêng biệt trên runtime đã sửa: 4 đạt, 1 lỗi đã xác nhận tồn tại trên baseline; không bỏ qua ca nào. Không tuyên bố toàn bộ test suite đạt. `test-summary.json` ghi tên và phân loại từng ca. Biên nhận push hai nhánh được lưu ở `PUSH_RECEIPT.json` sau khi kiểm tra remote.
