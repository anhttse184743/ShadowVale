# Map 1 — tiếp tế tức thời, tầm bắn tháp canh và mẫu giọng Nam

26/10/2026, nhánh Khoa. Chưa push đợt này.

## Nhận/trả tiếp tế

Sau khi giải đúng khóa hòm, cấp đúng một m1_supply_cargo vào túi, mở cờ đã thu hồi và chuyển nhiệm vụ ngay trong callback kết thúc minigame. Bỏ lượt đi tới hòm, mở hòm, tháo ba lô và cất túi khỏi luồng campaign. Save cũ đã mở khóa nhưng chưa lấy vẫn nhận tức thời bằng E; guard Progress.supplies chặn cấp lặp. Khi về chỉ huy giữ đoạn thoại nhận hàng/giao nhiệm vụ, bỏ pha mở ba lô lấy túi. Cờ bàn giao, trừ đúng một túi và thưởng vẫn commit ở cuối thoại; không đổi phần Hùng nằm trên cáng. Tài sản hoạt ảnh cũ chỉ còn phục vụ preview, không được gọi trong nhận/trả nhiệm vụ.

Test giải khóa thực: cancel không được hàng; retry giải đúng nhận ngay, không Cinematic/Operating hoặc model “Nam supply interaction”; nhận lại không có túi thứ hai; hoàn thành thoại bàn giao không di chuyển ngang, túi bị trừ đúng một, có thưởng và vào điểm 2.

## Vì sao tháp canh khó trúng

Đã kiểm tra vị trí capsule lính với xương đầu/ngực và tia từ bệ Mosin: capsule vẫn trùng nhân vật, không tìm thấy lỗi damage bằng 0 hoặc độ tản quá lớn ở tư thế ngắm đứng yên. Lỗi tia scope đã sửa ở commit f30ee07a được giữ nguyên và kiểm thử lại.

Ở bệ cũ (106,22), tia giữa thông nhưng lệch sang bên bị SpreadingTree/BroadleafTall_A chặn trước lính khoảng 5–12 m tính từ Nam. Khảo sát thêm mesh gỗ cho thấy có cành/thân cây thật trên đường đạn, nên không giữ cách bỏ collider hoặc cho bắn xuyên cây. Mái ROOF / C3 vốn đã có luật xuyên mái mỏng và không phải vật cản chính ở các tia này.

Khảo sát điểm đứng lân cận tìm được bệ (104,22), cách bệ cũ 2 m về tây, vẫn ở ngoài bán kính tiếng Mosin 30 m. Dịch đồng bộ cache từ (106.8,22.7) sang (104.8,22.7), giữ nguyên hướng “Đông Nam”, cây và vật cản. Kiểm tra đường ngắm đầu/ngực từ giữa và hai bên ±1.5 m theo hướng nhìn: 12/12 tia qua cả trạng thái bình thường/báo động đều tới lính. Không phóng hitbox, không cho đạn xuyên thân cây/tường. Tia ngắm rõ mới có thể gây sát thương.

Final_Tests.xml: 5/5 đạt — minigame/cargo/bàn giao tức thời; các tia tháp; click scoped giữ tia khi camera hạ; Mosin thật hạ lính gác chòi qua mái mỏng; vị trí Nest A nhìn chòi và giữ cự ly thiết kế. Đây là kiểm tra các phần liên quan, không phải toàn bộ game.

## Giọng Nam

Người dùng muốn trẻ hơn, nhanh gọn và có quyết tâm. Đã xuất hai mẫu trẻ mới: Trẻ 1 = Minh Quân, Trẻ 2 = Xuân Vĩnh, đều VieNeu offline và nhịp 1.12, giữ nguyên hai câu để so. Mẫu C/D chưa tích hợp, đang chờ chọn; hai mẫu Adam A/B trước đó không còn là đề xuất cuối. Giữ mọi thoại/giọng đang có trong catalog; không đổi giọng địch hoặc các vai khác, không tốn phí. Tên preset không phải bằng chứng về tuổi người thật; người dùng chọn theo âm sắc nghe được.
