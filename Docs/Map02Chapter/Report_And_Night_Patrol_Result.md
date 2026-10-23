# Điểm báo cáo Tâm và quan sát Đêm 2

## Nhân vật sau hộ tống

Đối chiếu model nguồn: Tâm mặc xanh đậm, Lực mặc xanh nhạt. Hai NPC trước đây cùng đứng tại điểm nhận ở chốt nên dễ nhìn thành hai Tâm. Runtime chỉ có một actor Tâm và một mesh Tâm hiển thị sau hộ tống; mesh nằm bị thương được ẩn khi Follow bắt đầu.

Đưa Tâm về vị trí báo cáo gốc (`command`). Bỏ lệnh cho Thành đi ra chốt và quay về. Cảnh báo cáo bố trí Nam đối diện Thành; sau báo cáo Tâm nghỉ ở `rest` lệch phải 2 m, cách Lực hơn 8 m. Restore save Ngày 2/Đêm 2 giữ Thành tại điểm báo cáo và đưa Tâm đã cứu về chỗ nghỉ, tránh giữ bố trí cũ từ snapshot.

## Quan sát đầu Đêm 2

Tạo nhóm `night2_patrol` gồm ba lính dùng model/controller địch Map 1. Nhóm ở phía nam cầu, tại khu vực tìm thấy Tâm lúc sáng, cách ghim đầu cầu khoảng 12 m. Một người giữ vị trí quét đèn, hai người đi tuần gần đó. Nhóm được lưu/khôi phục cùng snapshot địch và không tạo thêm khi load.

Đèn quét hình sin trái/phải rồi trở lại, một vòng mặc định 15 giây, không nhảy đột ngột giữa cuối và đầu vòng. Luồng sáng trong không khí dùng mesh/shader trong suốt riêng; Spotlight chiếu và đổ bóng lên cảnh. Hướng mesh, đèn và góc phát hiện dùng cùng hướng quét.

HUD chỉ rõ hướng nam cầu, khoảng cách, người quét đèn và cách giữ F. Khi nhìn lệch, có chỉ dẫn quay trái/phải/phía sau; nhãn trên tổ tuần tra cần tầm nhìn thông thoáng. Quan sát vẫn phải đứng xa hơn 8 m, giữ ống nhòm hướng đúng mục tiêu và không bị vật che trong hai giây.

Trong đoạn đầu chỉ quan sát, chưa áp dụng cảnh báo phát hiện. Sau báo cáo N2_02 mới dùng cơ chế cảnh báo lần một / retry đầu cầu lần hai. Nhóm này không tự bắn Nam để vượt qua cơ chế cảnh báo của tuyến. Sau N2_05, tổ tuần tra và hiệu ứng đèn được ẩn.

Sửa thêm cache minimap: bỏ qua địch đã bị hủy/ẩn/chết khi snapshot vừa được tải lại, tránh MissingReferenceException trong lúc cache chưa đến lượt làm mới.

## Kiểm tra

`Map02ReportAndNightPatrolTests`: Thành không di chuyển trong/sau hộ tống; Tâm chỉ có một model hiển thị; ba lượt save/load vẫn giữ bố trí đúng; ba lính Đêm 2 có animator, có di chuyển; hướng đèn quét hai phía; vật che chặn quan sát; quan sát thật bằng ống nhòm hoàn tất; load không nhân đôi tổ lính; rời Đêm 2 tắt đèn và nhóm tuần tra.

Kiểm tra tổng hợp đạt **10/10**, gồm các test trên, DayTwoState (cảnh báo/retry/save/load), DayTwoRoute và Cast. Báo cáo giữ tại `Logs/Map02Chapter/report-patrol-regression-results.xml`; lượt kiểm tra riêng mới nhất ở `Logs/Map02Chapter/report-patrol-results.xml`.

Ảnh render: `Logs/Map02Chapter/day2-report.png`, `Logs/Map02Chapter/night2-patrol.png`.

Đây là phần sửa điểm báo cáo và làm rõ nhiệm vụ quan sát; các phần còn lại của kế hoạch Đợt 3 vẫn theo kế hoạch riêng.
