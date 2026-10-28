# Tích hợp ba lô tiếp tế và Hùng nghỉ trên cáng — 19/10/2026

Đã tích hợp vào project G:/game/ShadowVale, nhánh Khoa. Không commit hoặc push.

- Nam tháo ba lô, đưa ra trước, cất/lấy túi, đóng và đeo lại. Nhân vật, túi, nắp ba lô và quai vải dùng chung đồng hồ 16 giây. Ghi nhận hàng tại nhịp 10,1 giây; hàng nằm trong ba lô khi chơi bình thường.
- Bản sửa cổ tay chỉ dùng trong model tương tác riêng. Khi kết thúc, phục hồi actor, vũ khí, controller và camera gameplay.
- Sau khi hộ tống về hầm, Hùng nằm nghỉ trên cáng ở các stage 2–7, kể cả hội thoại bàn giao tiếp tế. Camera thoại nhìn Hùng nằm. Retry hoặc chuyển sang đoạn rút lui bàn giao lại actor gốc.
- Không sửa scene Map 1/Map 2, skeleton gốc, nội dung thoại hoặc ID nhiệm vụ.

## Kiểm tra

3/3 bài kiểm tra Unity EditMode/PlayMode đạt trong tests.xml:
1. FBX giữ đủ ba lô, nắp và chuyển động túi.
2. Trong game: tay khớp túi ở nhịp lấy hàng, phục hồi điều khiển, Hùng nói khi nằm, retry giải phóng tư thế nghỉ.
3. Độ dài clip và dữ liệu quai vải đủ 481 khung hình, chỉ số mesh hợp lệ.

Đã xem ảnh Full HD trong game ở đoạn tháo ba lô, giữ trước người, lấy túi và Hùng nói trên cáng. Đây là kiểm tra đoạn tương tác; chưa chơi lại toàn bộ chiến dịch hoặc đo hiệu năng toàn map. Không khẳng định đã kiểm tra mọi va chạm ngón tay/mesh ở mọi khung hình.

Các lỗi phát hiện và khắc phục trong quá trình nhập: bỏ sót geometry khi prop bị ẩn ở frame đầu; thiếu khung quai vải; chỉ số topology bị Unity chia đỉnh; màu ba lô; camera quá sát Nam hoặc quá xa Hùng.

## Chỉnh sửa

Nguồn Blender và script export được ghi trong SourceArt/Map01Operations/ShelterSupplies/README.md. Import Unity bằng ShadowVale.Map01.Editor.ShelterSuppliesSetup.Import. Presentation.asset giữ tham chiếu clip/model và dữ liệu quai. Không cần chỉnh scene hoặc controller gameplay.
