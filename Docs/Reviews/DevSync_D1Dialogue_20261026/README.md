# Đồng bộ dev và kiểm tra thoại Ngày 1 / sửa tia Mosin

Dev đã fetch: ee1bbadb; hợp nhất vào Khoa ở ca9a1a4f. Phần đổi chỉ huy chèo/Hùng ôm tay được lưu riêng ở 9882d27a trước merge. Xung đột Map02Arrival giữ vai chèo mới, đồng thời giữ LoadMisstep, MisstepPassenger, Lane, quyền tay của C1 và EndMisstep từ dev. 27 file review cũ đã staged được khôi phục nguyên patch; không đưa chúng vào commit công việc này.

## Thoại Ngày 1 Map 2

25 cue có đủ catalog, WAV, hash, nội dung caption và thời lượng hợp lệ: 3 cập bến; 4 nói chuyện trên bờ với anh Thành; 5 mật khẩu/làm quen anh Lực; 3 thay băng; 2 giao thuốc; 3 nhận việc sửa bến; 2 xác nhận bến sửa xong; 3 báo cáo cuối ngày. Danh sách nguyên văn và giọng trong Day1-dialogue-audit.md/json. Không sửa lời hoặc tạo audio mới. Không phát hiện câu nhân vật nào ngoài bảng dữ liệu trong luồng Ngày 1; chỉ dẫn mang thùng và mục tiêu HUD là thông báo gameplay.

Ba câu “Cậu ăn phần này đi. Cả ngày vác thùng.” / “Cậu bị thương mà.” / “Thương tay chứ đâu phải thương bụng.” được gắn với m2v3_promise ở Đêm 1 theo kế hoạch đã chốt, không bị bỏ sót. D1_04 không có đoạn đối thoại riêng trong kịch bản; có phản hồi thao tác bằng HUD. Đã rà các nhánh arrivalbank/repairdone và thời gian chờ ho của ông Sáu. Test Unity kiểm tra toàn bộ 50 cue Ngày 1–Đêm 1 đều có clip nhập khớp lời, cùng integration kho/bến/chốt. Đây là kiểm tra độ phủ dữ liệu và tích hợp, không phải xác nhận đã nghe lại từng chữ bằng tai hoặc chơi đủ mọi thứ tự nhiệm vụ ở lần này.

## Mosin không ăn sát thương

Tái hiện trên scene Map 1 với Mosin thực và lính thật: trước click, tia qua tâm PU trúng thân lính; magazine 5→4, damage 200, lethalAgainstEnemies=true, nhưng HP 100→100. Event Attacked đặt shotAt khiến Map01Mosin.Update đóng scope để lên đạn. ThirdPersonCamera.LateUpdate trả camera ra sau vai; GunfireFrame (order 10000) sau đó dùng camera mới để raycast. Lần tái hiện camera đổi từ (0,81.62,0.15) sang (0.58,81.65,-1.86), đủ lệch khỏi capsule lính. Đó là phát bắn trượt do đổi tia, không phải thông số damage bằng 0.

Sửa PlayerCombat giữ Ray của PU ở thời điểm nhận phát bắn, trước event lên đạn, trong PendingGunShot. Khi xử lý muộn, dùng ray đó cho viên sniper đã ngắm; muzzle/flash/tracer vẫn lấy vị trí nòng sau animation. Các súng và phát không qua scope vẫn giữ xử lý hiện tại. Giữ damage 200 và luật sát thương lethal lên enemy: một viên thực sự trúng lính gây chết, không tăng damage giả hoặc cho xuyên tường. Health vẫn giữ giáp, bảo vệ cutscene, sự kiện chết và phân biệt phe.

Before_Tests.xml: test mới tái hiện thất bại đúng vì HP vẫn 100. After_Tests.xml: 8/8 đạt, gồm click scoped sau camera transition; thân/đầu ở 35,80,120,199m, nhiều mức HP và chỉ huy có giáp; 201m, tường và bảo vệ cutscene; lính gác chòi C3 dưới mái; độ phủ thoại và integration Ngày 1. Mẫu tái hiện sau sửa: HP 100→0, magazine 5→4.

Không gọi dịch vụ có phí. Không đổi thoại, giọng, model gốc hoặc tăng tỷ lệ bắn xuyên vật cản. Chưa push đợt đồng bộ/sửa này.
