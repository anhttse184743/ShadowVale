# Đêm 2 — chặn sông, tổ tuần tra và cửa kho

Điều chỉnh theo yêu cầu mới ngày 27/10/2026; thay thế các mô tả cũ về khóa hành lang và sức chịu đạn.

- Trước khi kiểm tra kho xong, chỉ chặn diện tích sông thật theo `RiverWater` và dấu chân capsule. Đường khô, hai bờ làng/ruộng và các lối vòng trên đất không bị khóa nữa. Cầu vẫn đi được; không được chui dưới cầu hoặc nhảy qua vùng sông để né.
- Quan sát/báo bộ đàm vẫn là điều kiện mở nắp hầm; không dùng giới hạn di chuyển để giữ người chơi tại điểm quan sát.
- N2_01: đi vào bán kính 3 m quanh ghim checkpoint ở đầu cầu một lần (không cần mở ống nhòm), lưu `night2ObservationCheckpointReached`. Sau đó có thể quan sát từ mọi hướng, tại vị trí bất kỳ cách tâm tổ tuần tra tối đa 40 m theo mặt phẳng ngang; không còn bắt đứng trên cầu. Giữ F, đứng yên và nhìn rõ tổ tuần tra liên tục 2 giây. Ra ngoài 40 m hoặc bị vật cản che thì tiến độ quan sát về 0, nhưng không mất checkpoint đã đi qua.
- Chưa hoàn thành quan sát thì không báo bộ đàm được; chưa báo bộ đàm thì không mở kho/kiểm tra kho được, dù đã đi tới ghim nhiệm vụ sau. Save/load giữ checkpoint đã đi qua; retry tuyến sau N2_02 giữ nhiệm vụ quan sát đã hoàn tất.
- Sau N2_03, Nam được lội qua sông trên địa hình đáy sông thật ở mọi vị trí trong bản đồ chơi được. Đã bỏ toàn bộ dải nền/đường băng qua nước, renderer và collider nâng đáy. Chỉ giữ liên kết NavMesh không có hình ảnh để chỉ đường sang tuyến sơ tán.
- Hệ thống chống rơi chấp nhận vị trí lội sông sau mở khóa nếu `RiverWater` xác nhận nằm trong kênh và mesh mặt đất thật còn bên dưới chân. Không còn đẩy Nam về bờ chỉ vì đáy sông thấp hơn -0.65 m. Rơi dưới đáy, mất collider hoặc ra ngoài bản đồ vẫn được phục hồi. Load khi đang lội cũng giữ đúng vị trí; retry trước N2_03 khóa sông lại.
- Riêng nhóm `night2_patrol`: mỗi phát trúng gây 50% máu tối đa hiện tại của Nam. Giữ xác suất trúng 90% khi bắn không bị che, và nằm xuống vẫn cắt phát hiện.
- Mỗi lính có 1.440 HP. Một viên đạn hợp lệ gây 24 HP sau bộ điều chỉnh riêng: 59 viên còn 24 HP, viên 60 hạ lính. Áp dụng cho các loại súng để súng có sát thương cao hoặc chính sách one-shot không phá yêu cầu này. Đòn hạ gục lén tức thì không áp dụng cho tổ tuần tra.
- Các nhóm địch khác giữ sát thương và chính sách đạn hiện có. Không tăng máu/sát thương toàn bộ game.
- Khi đã báo bộ đàm và Nam tới trong 3 m quanh điểm đứng ở cửa hầm, cùng cao độ mặt đất (lệch tối đa 1,2 m), lưu `night2ReachedStore=true`.
- Ngay tại cửa kho: tắt cả ba đèn và luồng sáng, đóng băng đồng hồ quét, xóa cảnh giác/khóa mục tiêu và dừng bắn. Các model lính vẫn ở vị trí cũ đến hết Đêm 2.
- Đây là kết thúc đoạn tiếp cận lén. N2_03 vẫn yêu cầu E mở nắp, Space dùng thang, S xuống và E kiểm tra kệ vật tư. W leo trở lại.
- Trạng thái ngừng quét được lưu/load; checkpoint thử lại tuyến tuần tra xóa trạng thái tới kho và khởi động lại cuộc tiếp cận.
- Bộ đàm/tiếng xích vẫn tiếp tục sau khi ngừng quét để không khóa nhiệm vụ trở về Hùng.
- Save cũ có lính 100 HP được đổi theo tỷ lệ máu còn lại sang 1.440 HP; lính đã chết không hồi sinh, save mới không bị đổi máu lần nữa.

Kiểm chứng Play Mode trong project tách biệt với NPC và NavMesh thật: `SourceArt/Map02NightBankFlow/stealth_checks.cs`, báo cáo `stealth-balance-checks.txt`.
Bao gồm sát thương hai viên với Nam có 200 HP, 60 viên cho sát thương gốc 22/24/250, chặn one-shot có sẵn trong hàng đợi bắn, hồi quy Health bình thường, đường khô/sông, bán kính tới kho, tắt đèn/ngừng bắn, load/refresh và hoàn thành bộ đàm khi quét đã dừng.

## Tiếp cận kho sau báo cáo Hùng — cập nhật 27/10/2026

- Phạm vi: từ khi hoàn thành N2_02 đến khi tới cửa hầm kho. Trong đoạn này bộ điều khiển nhiệm vụ quản lý phát hiện và thất bại, lính không bắn gây sát thương tự do trước cutscene.
- Lần đầu một đèn soi trúng Nam đang không nằm, với đường nhìn không bị vật cản/khói che: ghi một lần phát hiện ngay, dừng đồng hồ quét và giữ cả ba đèn hướng tới vị trí vừa phát hiện trong đúng 1,5 giây thực của gameplay. Pause/menu dừng bộ đếm.
- Hiện dấu `?` màu vàng trên đầu các lính trong cửa sổ 1,5 giây. Lính nói với nhau bằng tiếng Anh: “I think something moved. Keep a close watch.” Có phụ đề câu thoại; không hiện thông báo hướng dẫn phải nấp hoặc đếm ngược cho người chơi. File âm thanh hiện dùng giọng tổng hợp Microsoft David Desktop của Windows.
- Nằm xuống trong cửa sổ phản ứng giúp tránh cutscene, nhưng vẫn giữ một lần phát hiện. Nằm tiếp không tạo thêm lần phát hiện. Ra khỏi chùm đèn hoặc núp sau tường khi vẫn đứng không thay thế yêu cầu nằm xuống ở lần đầu.
- Hết 1,5 giây mà chưa nằm, hoặc đèn soi trúng khi đã có một lần phát hiện: khóa điều khiển/lưu và chạy cùng cutscene. Máy bắt đầu ở góc nhìn hiện tại của Nam, di chuyển mượt trong 3 giây ra sau lưng tổ địch; tới nơi mới hô khai hỏa và bắn liên tục, sau đó máy tiến về Nam, Nam diễn hoạt hy sinh, hiện thất bại rồi tự thử lại checkpoint tuyến tuần tra. Không phải làm lại quan sát và báo cáo Hùng; số lần phát hiện về 0.
- Tới cửa kho vẫn kết thúc tuyến tuần tra, chưa tự hoàn tất kiểm tra vật tư bên trong. Không cho vượt qua cảnh nghi ngờ đang chờ kết quả bằng cách chạy tới cửa kho.
- Thoại bộ đàm tạm dừng tiến độ trong lúc nghi ngờ/lính nói để không chồng lên câu thoại địch.

Kiểm tra: `SourceArt/Map02NightBankFlow/warning_checks.cs`, kết quả `warning-checks.txt`: 24 kiểm tra logic Unity với capsule, tia đèn và assembly runtime thật. Bao gồm hai nhánh phản ứng, lần phát hiện thứ hai, khóa input/save, nhập audio, trạng thái thất bại và reset tuyến. Đây là kiểm tra biệt lập; chưa xác minh hình ảnh icon, hoạt ảnh và đường máy bằng lượt chơi toàn cảnh Map 2.

- Thoại sau khi máy tới sau lưng tổ địch trong cutscene thất bại (cả quá 1,5 giây chưa nấp và phát hiện lần hai): “There's a little rat out there. Squad, open fire on that position!” Phát bằng giọng Microsoft David Desktop và hiển thị phụ đề đồng bộ, thay câu hoài nghi đang phát để không chồng thoại. Nấp kịp ở lần đầu vẫn chỉ nghe câu hoài nghi. Bốn kiểm tra bổ sung xác nhận nhập audio và chọn câu thoại đúng ở cả ba kết quả.

- Nhịp camera mới: 3 giây di chuyển từ vị trí/góc xoay/FOV đang hiển thị của người chơi đến góc sau lưng tổ địch bằng SmoothStep và Slerp, không cắt máy tức thì. Không phát lệnh khai hỏa hoặc bắn trong nhịp di chuyển này. Các mốc bắn, quay về Nam, hy sinh, thất bại và retry đều dời thêm 3 giây để giữ đủ thời lượng. Kiểm tra thêm điểm bắt đầu camera, vị trí trung gian, chưa bắn trước khi tới nơi và phát lệnh/bắn sau khi tới nơi ở cả hai nhánh thất bại.
