# Đèn pin tuần tra Đêm 2

- Tư thế đã duyệt: ảnh `Logs/Map02Chapter/FlashlightPreview/hand-detail-wrist-left90.png` (người dùng chọn lại ảnh này sau các bản thử theo nét vẽ).
- Asset: `Assets/Resources/Equipment/Map02PatrolFlashlight.prefab`, ba material đen/kim loại/thấu kính. Không có collider gây cản đường hoặc chắn tia nhìn.
- `Map02PatrolFlashlight` tạo một đèn cho mỗi lính của nhóm `night2_patrol`, chạy sau bước giữ súng. Tay trái cầm đèn ngang mặt, tay phải hạ súng. Khi giao chiến đèn chuyển sang dây vai, giải phóng hai tay để ngắm/bắn. Khi chết đèn tắt.
- Luồng sáng và tia phát hiện lấy gốc ở `Emitter` tại đầu đèn. Không còn hình trụ đèn nổi ở đầu NPC. Tải lại save khôi phục đèn từ roster, không ghi thêm vật phẩm vào save.
- Lính tuần tra dùng tầm nhìn thông thoáng ở gần và luồng đèn để phát hiện Nam, sau đó dùng hành vi truy đuổi/bắn hiện có. Tường và khói vẫn chặn tầm nhìn. Hệ thống cảnh báo/thử lại tuyến vẫn tồn tại.
- `ShadowVale > Map 2 > Chạy thử Đêm 2` bắt đầu ở vị trí báo cáo cạnh anh Thành cuối Ngày 2, N2_01 chưa hoàn thành; người chơi tự đi tới cầu.
- Tổ tuần tra dùng site riêng `nightPatrol` ở cánh đồng phía đông nam cầu (bên trái khi nhìn về nam), khoảng 34 m từ điểm quan sát. Không thay site `scout` của nhiệm vụ cứu Tâm. Tầm đèn 28 m, để đường từ chỉ huy tới điểm quan sát nằm ngoài vùng phát hiện. Save cũ còn đặt lính sát chỗ Tâm được đưa sang vị trí mới khi tải.
- Băng cứu thương trong Map 2 gọi trực tiếp `Map01NamActions.Bandage` qua `CharacterHudSession`: cùng animation, thời gian, ẩn súng và cơ chế di chuyển/ngắm để hủy như Map 1. Không trừ thêm băng khi động tác còn chạy; tải save hủy động tác và trả điều khiển.

## Kiểm tra thủ công

1. Thoát Play nếu đang chạy. Chọn menu chạy thử Đêm 2.
2. Xác nhận Nam ở khu chỉ huy, đi tới cầu, giữ F quan sát.
3. Xem lính từ bên hông: đèn nằm trong tay trái, luồng sáng bắt đầu ở thấu kính.
4. Đi ra trước mặt lính hoặc vào luồng sáng: lính chuyển sang giao chiến; nấp sau vật che thì không bị bắn xuyên.
5. Tải lại save: vẫn có ba lính và mỗi lính một đèn.

Tư thế là IK trên rig hiện có, chưa có animation cầm đèn được tác giả animate riêng.

## Kết quả xác minh

Unity EditMode + EnterPlayMode: 10/10 kiểm thử đạt, báo cáo `Logs/Map02Chapter/night2-phase3-results.xml` (22/10/2026). Bao gồm điểm spawn chạy thử, hoàn thành tuyến Đêm 2, cảnh báo/thử lại, đèn xuất phát tại thấu kính, lính bắn khi Nam đứng trước mặt, tường chặn phát hiện và sát thương, tải lại không tạo trùng đèn. Ảnh runtime: `Logs/Map02Chapter/night2-held-flashlight-runtime.png`.

Sau khi chuyển vị trí tuần tra và đồng bộ băng bó: lượt kiểm 12 ca đạt 11; ca còn lại dừng ở nhãn hướng `NAM` trong hướng dẫn đã đổi chữ thường. Đã sửa nhãn và chạy riêng ca quan sát thực tế, đạt 1/1 (`Logs/Map02Chapter/report-patrol-results.xml`, 22/10/2026). Đường NavMesh từ chỉ huy tới cầu được lấy mẫu kiểm tra ngoài tầm đèn; ca băng bó Map 1 và Map 2 đều đạt, bao gồm state `Act_Bandage`, ẩn/trả súng, số băng/số đạn và trả điều khiển khi tải save.
