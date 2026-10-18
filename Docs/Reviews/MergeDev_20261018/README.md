# Đồng bộ dev vào Khoa — 18/10/2026

- Remote: origin (ShadowVale).
- Nhánh bắt đầu: dev tại 9c31c93; Khoa cũng ở 9c31c93. Chuyển sang Khoa giữ nguyên working tree.
- Lưu phần nhiệm vụ/cutscene đang làm: 6434bce.
- File tool_version.txt cục bộ và origin/dev có cùng blob 3cacc0b93c9c9c03a72da624ca28a09ba5c1336f. Đưa vào theo dõi ở 61225b0, không thay đổi nội dung.
- Dev mới nhất khi fetch: e61f283, mới hơn gốc 37 commit.
- Merge vào Khoa: 9b93199. Kiểm tra origin/dev là tổ tiên HEAD: thành công.
- Xung đột duy nhất: Map01ArtStage.cs. Giữ điều kiện thư mục của dev và try/catch vô hiệu hóa watcher khi lỗi của Khoa.
- Map 1 auto-merge: 28.196 YAML objects, không trùng fileID và không còn conflict marker. Giữ thay đổi scene từ dev cùng prop phá nguồn/đài của Khoa.
- Thư mục SourceArt/Map1_ArtRefresh_20261016 cục bộ không đưa vào commit, không xóa.

## Tay Hùng trong cảnh xử bắn

Trước đây director giữ Hung_Captive_Recoil ở frame 0 trước phát bắn rồi đổi sang Hung_Bound_Execution khi bắn. Bản sửa dùng Hung_Bound_Execution từ đầu cảnh, giữ time=0 trước phát bắn, sau đó mới chạy thời gian gục. Không đổi clip hay binding cổ tay tại nhịp bắn. Giữ clip tra khảo riêng cho intro. Không thêm xoay cưỡng ép cổ tay hoặc sửa rig chung.

## Kiểm chứng

Unity 6000.3.24f1 đã biên dịch bản hợp nhất. RecordDevSync: 3/4 qua (cảnh xử bắn/tay/camera, luật nguồn điện, các vòng đài ngẫu nhiên); test tích hợp đài–nguồn thất bại ở lúc tự canh kim timing. Sau khi bổ sung trạng thái chẩn đoán trong bài kiểm tra, chạy riêng cùng test đài–nguồn: 1/1 qua, gồm thử sai, ba nhịp ngắt nguồn, khói, save/load và báo cáo chỉ huy. Không thay luật hoặc độ khó mini game để làm test qua; nguyên nhân timing không tái hiện trong lượt riêng, không coi toàn bộ suite đã sạch lỗi ổn định.

Đã xem ảnh phía sau Hùng trước bắn (Contacts/0000-1.png, 0006-1.png), sau bắn (0012-1.png), và ảnh gameplay cảnh xử bắn. Cả hai tay hiện ngoài thân ở tư thế trói từ đầu; cùng clip được giữ trước và sau phát bắn. Test cũng xác nhận nòng súng hướng Hùng, Hùng không đứng dậy và camera quay về Nam trước khi thất bại.

Kết quả: Logs/Map01Operations/video/dev-sync-test-cases.txt và radio-results.txt. Ảnh: Logs/RescueStory/Execution. Đây là kiểm thử có mục tiêu sau merge, chưa phải toàn bộ lượt chơi Map 1–2. Không push remote trong đợt này.