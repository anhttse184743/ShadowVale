# Âm thanh miễn phí — Khoa, 23/10/2026

Đã cập nhật từ dev `473a7940` trước khi sửa. Không gọi Runway/Narakeet/ElevenLabs hoặc dịch vụ trả phí.

## Phân vai

Các độ tuổi dưới đây là chỉ đạo giọng theo yêu cầu và vai trò trong truyện, không phải bổ sung tuổi chính thức cho nhân vật.

| Vai | Hướng giọng | Preset |
|---|---|---|
| Nam | Nam thanh niên khỏe, rõ chữ, gọn; không hạ trầm thành người già | Adam |
| Hùng | Nam trung niên miền Bắc, ấm, tiết chế; chậm nhẹ vì bị thương tay | Thanh Bình |
| Anh Thành / chỉ huy phe ta | Nam trung niên, chắc câu, ra lệnh ngắn | Minh Triết |
| Anh Lực | Nam trưởng thành, thẳng, hơi cộc | Đức Trí |
| Chị Lành / người chăm cha | Phụ nữ trưởng thành miền Nam, chăm sóc, gần gũi | Thục Đoan |
| Ông Sáu | Nam cao tuổi, nhịp chậm, yếu; ho bằng hiệu ứng riêng | Thái Sơn |
| Chú Bảy | Nam lớn tuổi nhưng còn khỏe làm việc, khác ông Sáu | Đức Trí, chỉnh nhịp và cao độ nhẹ |
| Tâm / các vai trinh sát | Nam thanh niên, lời báo cáo ngắn; chân đau, không giọng già | Mạnh Dũng |
| Lính gác / lính địch | Nam trẻ trưởng thành, cảnh giác | Xuân Vĩnh |
| Chỉ huy địch / sĩ quan / giám sát | Nam trung niên, cứng | Quang Sơn |
| Bé Tí | Giả lập giọng trẻ em từ preset; cần nghe lại cảm nhận tuổi và giới | Mỹ Duyên, tăng cao độ nhẹ |

Không khẳng định các preset có tuổi thật tương ứng. TTS miễn phí chưa thay được diễn xuất của diễn viên, đặc biệt trẻ em và tiếng đau/hốt hoảng; bản này được tích hợp để nghe kiểm tra, chưa ghi là đã được người dùng duyệt giọng mới.

## Nguồn và quyền dùng

- Thoại: [VieNeu-TTS v3 Turbo](https://huggingface.co/pnnbao-ump/VieNeu-TTS-v3-Turbo), model/preset Apache-2.0, chạy offline; không clone giọng người thật từ tệp ngoài.
- Dế: [FreethinkerAnon — crickets](https://freesound.org/people/FreethinkerAnon/sounds/129678/), CC0.
- Gà: [BenjaminNelan — Rooster Crow 3](https://freesound.org/people/BenjaminNelan/sounds/435506/), CC0.
- Chèo: [craigsmith — G54-27-Oars Rowing](https://freesound.org/people/craigsmith/sounds/438846/), CC0.
- Củi: [samarobryn — Crackling Fire](https://freesound.org/people/samarobryn/sounds/414767/), CC0.
- Ho: [ezwa — Cough 2](https://commons.wikimedia.org/wiki/File:Cough_2.ogg), public domain.
- Ếch: [Michael G. Swanson — Minnesota Frog Sounds](https://swanson.run/frogs/), CC0; cắt ngắn và phối nhẹ với dế. Đây là nguồn thay thế, không phải ghi âm xác thực sinh cảnh Việt Nam.
- Cười ngắn: [Supergeek — Do you remember laughter?](https://opengameart.org/content/do-you-remember-laughter), CC0, cắt riêng; cần nghe duyệt chất cười vì khác nguồn preset lời Hùng.
- Gió/lá, cánh vịt, động cơ máy bay xa, thùng đặt xuống: tổng hợp/foley bằng mã nguồn tự viết, không phải ghi âm lịch sử. Nhạc chủ đề: giai điệu độc tấu gảy thưa tự soạn bằng mã, không dùng nhạc thương mại.
- Tiếng trẻ gọi nhau: câu môi trường mới “Chờ với! Qua đây nè!”, TTS offline, không dùng đoạn ghi âm trẻ nói tiếng nước ngoài.

Các WAV cất tại `Assets/Resources/Map02DayOne`. `hung_invitation.wav` tách riêng câu “Đợi tới lúc đó, cậu ra nhà tôi.”; `hung_laugh.wav` tách riêng tiếng cười. Theme chỉ có trong bữa cơm Lời hứa. Máy bay xa phát một lần sau 480 giây chơi Ngày 1; thời gian và cờ đã phát được lưu cùng trạng thái chương. Nền tự hạ khi có thoại; âm ở bếp và bến có vị trí riêng.

## Nghe thử

File `BoGiong_9Vai_NgheThu.mp3` là chuỗi mẫu từ chính các câu đã nhập vào game. Bảng dưới ghi thời điểm bắt đầu:

| Vai | Giây | Preset | Câu |
|---|---:|---|---|
| Nam | 0.00 | Adam | Tài liệu cũ của đơn vị tuần tra — cất đi, có thể còn hữu ích. |
| Hùng | 4.36 | Thanh Bình | Tôi trúng đạn rồi... Nam, yểm hộ tôi! |
| Anh Thành | 7.95 | Minh Triết | Đường về bị chặn rồi. Mình ở lại đây, chờ tin. |
| Anh Lực | 11.89 | Đức Trí | Nước đã lên chưa? |
| Chị Lành | 14.41 | Thục Đoan | Tay vầy mà còn vác máy. Ngồi xuống đi. |
| Ông Sáu | 18.21 | Thái Sơn | Thuốc hả? Để dành cho tụi trẻ, tui già rồi. |
| Chú Bảy | 22.41 | Đức Trí | Cái bậc này mà gãy là ông Sáu xuống sông bằng đầu đó. |
| Tâm | 27.58 | Mạnh Dũng | Trời... tưởng bị bỏ rồi chớ. |
| Bé Tí | 30.14 | Mỹ Duyên | Chú Hùng đâu? |


Cập nhật dev tiếp theo: 473a7940. Giữ cách dàn cảnh ăn cơm/chốt bờ tre của dev; thu lại Hùng 05, Hùng 05b, chị Lành 06 và vai Tổ gác bằng giọng mới. Tổ gác: Phạm Tuyên, nam trưởng thành khỏe và cảnh giác. Giữ dế cuối cảnh của Wolfgang_/Ted Kerr từ dev; lớp nền mới dùng tên village_crickets để không trùng resource.
