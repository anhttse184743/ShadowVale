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

## Khôi phục và bố trí ngày 26/10/2026

- Khôi phục các điểm nối bị thiếu: site `nightPatrol`, tạo tổ lính trong `ApplyState`, cập nhật luồng đèn từ tay lính, ngắm ống nhòm theo tổ lính và khôi phục roster khi tải save.
- Riêng tổ Đêm 2 dùng `Characters/EnemyCommanderVisual` như preview đã duyệt. Các nhóm địch khác giữ model đang được cấu hình trong layout.
- Ba vị trí tạo thành tam giác trên cung cách điểm N2_01 ở đầu cầu 35 m. Hai cạnh khoảng 10 m, cạnh còn lại khoảng 19,8 m; không phải hàng thẳng. Lính đứng tại chỗ khi chưa giao chiến.
- Mỗi lượt quét có góc 120 độ (±60 độ), hướng và chiều quét ngẫu nhiên theo từng lính và từng chu kỳ. Seed và số chu kỳ được lưu cùng checkpoint.
- Mỗi chu kỳ mặc định 15 giây có 4 giây cả ba đèn quay khỏi đoạn đường gần điểm quan sát. Tầm sáng vẫn 28 m, chùm sáng hẹp 24 độ. Khi đã giao chiến, lính dùng hành vi chiến đấu thay cho lịch tuần tra.
- Tường/vật cản và khói chặn phát hiện. Nằm trong cụm lúa thật chặn cả phát hiện của NPC lẫn bộ đếm cảnh báo của nhiệm vụ; đứng lên trong lúa không được che giấu.
- Kiểm tra biệt lập bằng NavMesh Map 2 thật, model humanoid và prefab đèn thật: `NightPatrolRestoreChecks.Run`. Báo cáo: `Tools/TamEscortReports/night-patrol-restore-checks.txt`. Đây không phải lượt chơi đầy đủ toàn bộ nhiệm vụ.

- Sửa hướng sau phản hồi preview trong game: lấy hướng từ từng lính về điểm nhiệm vụ làm trục giữa; toàn bộ hướng thân và đèn luôn nằm trong cung ±60 độ quanh trục đó. Chỉ nhịp/chiều quét ngẫu nhiên. Khoảng tối 4 giây giữ đèn ở mép cung để đường giữa không bị soi, không quay lưng ra hướng ngẫu nhiên 360 độ. Đã kiểm tra cả hướng thân và emitter trên model thực.

- Riêng khi N2_01 chưa hoàn thành: đồng hồ quét chạy 0,5 lần (chu kỳ 15 giây thành 30 giây thực, khoảng tối 4 giây thành 8 giây thực), tầm phát hiện/chùm sáng tăng từ 28 lên 40 m. Địch chỉ bắt đầu phát hiện bằng đèn, giữ vị trí và mỗi phát không bị vật cản chắn có 90% khả năng gây sát thương. Nằm xuống ở bất kỳ đâu xóa trạng thái báo động, vị trí nhớ và điều tra của tổ lính, ngăn phát hiện và ngừng bắn; đứng lên trong luồng đèn có thể bị phát hiện lại. Sau N2_01, các thay đổi tốc độ/tầm/ẩn nấp/xác suất này hết hiệu lực. Các nhóm địch khác không dùng quy tắc này.
- Kiểm tra chiến đấu bằng `NightPatrolRestoreChecks.BeginLive` trong Play Mode biệt lập trên NavMesh Map 2: 912/1000 phát không bị che trúng mục tiêu; không gây sát thương xuyên tường; nằm xuống dừng địch đã báo động; kiểm tra phạm vi N2_01 và trở lại hành vi bình thường đạt. Kết quả ở `Tools/TamEscortReports/night-observation-combat-checks.txt`.

- Sửa lỗi sau kiểm tra ngắm/ống nhòm: lịch quét chạy trong `Map02PatrolLightSync.Update`, độc lập với vòng xử lý hành động của Nam; pause/menu/cảnh hội thoại vẫn dừng như trước. Đèn chỉ khóa theo mục tiêu khi địch thực sự nhìn thấy mục tiêu; mất tầm nhìn sau vật cản thì đèn tiếp tục quét dù lính còn báo động.
- Các quy tắc 0,5 lần / 40 m / 90% / nằm xuống cắt dấu áp dụng cho cả đoạn tuần tra Đêm 2 đến khi xong N2_05, không kết thúc ngay sau quan sát N2_01. Điều này thay thế giới hạn N2_01 ở ghi chú trước.
- Kiểm tra Play Mode biệt lập: giữ ống nhòm và giữ trạng thái ngắm súng đều không đóng băng lịch/hướng đèn; sau N2_01 lính phát hiện và bắn trúng 91/100 phát ở 35 m; vật cản/nằm xuống và giới hạn theo đoạn tuần tra vẫn đạt. Báo cáo `Tools/TamEscortReports/night-patrol-aim-binocular-checks.txt`.
