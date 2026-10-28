# Kết quả kiểm chứng — cứu Hùng và hộ tống Map 1

Bộ nhiệm vụ/hồi quy: **22/22 kiểm tra Unity đạt**, không có kiểm tra bị bỏ qua. Sau bộ này, góc camera trong hầm được dịch để tránh chỉ huy che khung hình và phép kiểm tra vật cản loại collider nhân vật để không đẩy lens vào người. Hai kiểm tra video tự nhiên và skip/aim chạy lại đều đạt (**2/2**), thêm kiểm tra camera giữ khoảng cách với cả Nam và Hùng. Chạy trên bản sao dự án với Unity 6000.3.24f1; script/asset đã cập nhật vào `G:/game/ShadowVale` được đối chiếu SHA-256.

| Nhóm | Số kiểm tra | Nội dung |
|---|---:|---|
| RescueTests | 9 | Bốn lính/ba tuyến, đá và tiếng chân, súng trước sát thương, phát hiện, dao, cởi trói, ba đợt, hai người vào vùng an toàn, retry |
| RescueCompatibilityTests | 3 | Save v7 cũ không có trường rescue, dao trước/sau/vật cản, che khuất đường bắn Hùng |
| RescueMotionTests | 2 | Cởi trói/đứng lên bám mặt bến, save sau bàn giao không hồi sinh địch/lặp thưởng, dọn hook khi rời mission |
| RescueVisualTests | 2 | Video chuỗi thành công tự nhiên, giữ Esc và aim ở lúc bàn giao |
| OpeningCutsceneTests | 4 | Hồi quy opening, trạng thái không cầm súng và bàn giao gameplay |
| ShotTracerLifecycleTests | 2 | Hồi quy vòng đời tracer |

Video dài 166.70 giây, gồm bốn ám sát, cởi trói, Hùng đứng lên, hộ tống gặp sáu lính, hai người chạy xuống lối dốc thật vào hầm và bàn giao. Kết thúc có `Remaining=0`, `WaveMask=7`, `Phase=Delivered`, quest stage 2. Sáu lính thuộc ba đợt thật của runtime; fixture điều khiển Nam và dùng combat hiện có.

Đã xem các khung hình Unity từ camera nhiệm vụ và góc cận: tiếp xúc chân khi đứng lên, animation kết liễu, Hùng theo Nam, vũ khí khi chạy và chuyển góc trong lối vào hầm. Kiểm tra liên tục 253 khung cởi trói/đứng lên, sai lệch lớn nhất **1.89 cm** so với mặt bến thật.

Video là review do kiểm thử điều khiển, không phải lượt chơi bằng chuột/bàn phím của người chơi. Camera.Render không ghi HUD OnGUI hay audio; video này không dùng để đánh giá hiệu năng thực tế hoặc xác nhận âm thanh. Các hướng tiếp cận được kiểm tra NavMesh và che khuất bằng kiểm thử; chưa có lượt chơi thủ công liên tục từ cả ba hướng trên Editor chính đang mở.

`Unity-TestResults.xml` và `results.json` ghi từng test; `frames.csv`, `pose-metrics.csv`, `ground-contacts.csv` ghi timeline/tiếp xúc; `publication.json` ghi file đã cập nhật và hash các file bảo vệ. Backup bản cũ ở workspace `work/before-rescue`.

Để thử: dừng Play, chờ import/compile rồi New Game. Save hộ tống cũ vẫn giữ tiến trình và bỏ qua ngưỡng truy kích đã đi qua, vì vậy không dùng nó để kiểm tra đủ ba đợt mới.
