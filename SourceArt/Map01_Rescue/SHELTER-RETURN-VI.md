# Đi bộ vào hầm sau khi cứu Hùng

`Map01RescueCinematic` lấy mẫu pose trong LateUpdate, sau lượt cập nhật Animator của Unity. Nhịp đi bộ lặp theo quãng đường thực, không bị controller đứng nghỉ/quỳ ghi đè. Clip dùng trong `Assets/Resources/Rescue` là `Nam_Shelter_Walk`, `Nam_Shelter_Knife_Walk` và `Hung_Shelter_Walk`, sao chép từ các clip đi bộ có sẵn. Animation gameplay giữ nguyên.

Nam đi trước, Hùng đi theo với khoảng cách mặc định 1,4 m. Hai người dùng cùng tốc độ thay vì hai timeline chuẩn hóa riêng khiến Hùng đuổi kịp Nam. Hùng dừng phía sau Nam trong phòng, cách điểm trả nhiệm vụ 1,4 m dọc tuyến đi. Nếu ban đầu Hùng đứng trước cửa hoặc quá gần Nam, anh ấy bước nhường ra phía ngoài theo hướng thật của lối xuống, ưu tiên phần đường bằng trước khi Nam tiến vào; không đổi vị trí tức thời. NavMeshAgent của Hùng tạm ngừng cập nhật transform trong cinematic để không kéo model vào Nam.

Camera cắt sang góc rộng bên cạnh phòng khi Nam vào cửa; không bay xuyên qua nhân vật giữa góc ngoài và góc trong. Điểm đặt camera nằm lệch tuyến đi, thấy rõ hai người bước vào. Inspector có thể chỉnh `Interior Camera Offset` (mặc định -4, 1.8, -2 tính từ điểm trả nhiệm vụ).

Sau cutscene, `Map01HungVisual.RestoreAfterCinematic()` khôi phục ngay tư thế theo stage. Khi đã bàn giao, Hùng đứng nghỉ; không trở lại state mặc định `BiTroi` kể cả trong frame bàn giao hoặc khi giữ Esc.

Trong prefab `Assets/Resources/Rescue/Map01RescueLayout.prefab`, chỉnh `Shelter Walk Speed` để đổi tốc độ đi (mặc định 1,35 m/s), `Shelter Follow Gap` để đổi khoảng cách (mặc định 1,4 m); các `Walk Cycle Speed` là tốc độ gốc của animation để khớp nhịp chân theo khoảng cách. Thời lượng theo đường đi thực và kết thúc sau khi cả hai tới điểm dừng. Marker `Shelter Run` vẫn giữ tên cũ; `Report Point` là điểm dừng của Nam trong hầm.

Menu **ShadowVale > Rescue > Update shelter walking clips** tạo bản sao của clip đi bộ và nối lại tham chiếu. Clip đã tồn tại không bị ghi đè, nên có thể chỉnh thủ công trong Animation Window. Không chạy lại menu sau khi đổi thông số mà chưa sao lưu prefab, vì nó đặt lại tốc độ/khoảng cách mặc định.

Kiểm thử đo chuyển động chân cả ngoài lối vào và trong hầm, tốc độ đi bộ và khoảng cách giữa hips của hai model ngay từ đoạn nhường lối và suốt đường vào; kiểm tra Hùng thẳng người, trạng thái kết thúc, vũ khí/đạn, Esc với aim đang giữ, save/load và phần thưởng không lặp. Ảnh, video và kết quả mới nhất nằm trong `Verification/ShelterWalk`.
