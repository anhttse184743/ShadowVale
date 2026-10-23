# Rà soát và tiếp tục bộ animation Nam — 22/10/2026

Đã đọc lịch sử chat “Thiết kế hệ thống craft vũ khí”: người dùng duyệt blocking v1 ngày 21/10, AI tiếp tục polish v2 rồi dừng vì giới hạn sử dụng; hai lượt tiếp theo lỗi trước khi thực hiện công việc. Không có bằng chứng người dùng đã duyệt xuất Unity. Tiếp tục polish trên bản riêng trong SourceArt/AnimationRefresh_20261022; nguồn v1/v2 giữ nguyên.

## Kết quả rà đầu lượt

- `crouch_clearance_tail_v2.py` dừng tại `AssertionError: Unedited frame channels changed`, nên không lưu bản sửa cuối.
- Script cập nhật cả location của tay bằng thành phần quaternion. Đồng thời việc thêm key vào đường cong thưa có thể đổi nội suy ngoài khoảng sửa. V3 phân biệt đúng loại kênh, bảo toàn mẫu gốc ngoài đoạn sửa và kiểm sai số.
- Audit v2 ghi tay xuyên thân ở 5 đoạn ném khom, độ sâu mẫu lớn nhất khoảng 20 mm; bước cổ tay lớn nhất ở nhảy tay không khoảng 185 mm/khung.
- Những đoạn khác có trong v2: K54 thấp/nâng/ngắm/bắn/thay băng/hạ/đi/chạy/khom/đi khi ngắm; dao chờ/đòn/rút/cất/đi/chạy/khom; F1/đá/khói chuẩn bị/lấy đà/ngắm/ném cả đứng và khom; nhảy tay không và giữ từng đạo cụ. Có tổng cộng 47 đoạn. Đòn dao giường v10 là mốc đã duyệt riêng, không thay trong bộ này.

## Nguồn mở và đọc trong lượt này

| Nguồn | Phần thực sự truy cập | Áp dụng và giới hạn |
|---|---|---|
| [Mixamo catalog pistol](https://www.mixamo.com/#/?page=1&query=pistol) | Catalog 38 kết quả; chọn Standing Pistol Jump, đọc các tên Pistol Walk, Pistol Idle, Pistol Aim. Viewer không cung cấp bằng chứng phát được clip đầy đủ. | Dùng sáu FBX đã nhập/retarget trong v1 làm nguồn chuyển động; đối chiếu hash gốc. Không tuyên bố đã xem toàn clip trong viewer web. |
| [Adobe Mixamo FAQ](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html) | Đọc FAQ về sử dụng asset và giới hạn humanoid. | Dùng nguồn chuyển động trên rig Nam; không auto-rig lại. |
| [Animation Mentor — How to Polish Your Animation](https://www.animationmentor.com/blog/tutorial-how-to-polish-your-animation/) | Đọc phần technical fixes, contact, spacing, arc. | Sửa xuyên lưới, bật khớp và trượt contact trước. Không lọc mượt toàn timeline. Không ghi đã xem video nhúng. |
| [Animation Mentor — Jump and Land](https://www.animationmentor.com/blog/tutorial-animate-a-jump-and-land/) | Đọc phần tham khảo, blocking, lệch pha chân và tiếp đất. | Giữ co–bật–bay–tiếp đất–hồi phục; kiểm trước và ngang; giữ trọng lực/độ cao game hiện có. |
| [Unity 6.3 — Root Motion](https://docs.unity3d.com/6000.3/Documentation/Manual/RootMotion.html) | Đọc Root Transform Position Y, Feet và Loop Pose. | Bản diễn Blender có quỹ đạo bay; khi đưa game phải tách khỏi chuyển động vật lý của controller, kiểm grounded thật. Chưa đổi controller. |
| Blender Manual IK | Truy cập bị lỗi công cụ. | Không dùng như bằng chứng đã đọc. Dùng helper IK đã có và phép đo thực tế của dự án. |

## Tiêu chí kiểm

Giữ 47 mesh, vật liệu/trọng số và 62 rest matrix; kiểm góc cổ tay/khuỷu, bước cổ tay từng khung, lấy mẫu BVH tay với thân, kiểm đạo cụ/nhịp thả và chân; render ảnh nhiều góc, video 1920×1080/60 mẫu mỗi giây. Lấy mẫu BVH không phải chứng minh liên tục mọi tam giác không xuyên. Bản xem chưa chứng minh hiệu năng hoặc chuyển state trong Unity.
