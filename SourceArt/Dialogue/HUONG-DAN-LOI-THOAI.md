# Bộ thoại tiếng Việt

26 câu thoại bổ sung cho Map 1 và đoạn cập bến Map 2. Giọng Nam, Hùng, chỉ huy và lính địch được phân riêng. Giữ nguyên script mở đầu; giọng chỉ huy và Nam trong phần mở đầu đã đổi sang cùng giọng ở phần sau. Không đọc thành tiếng các thông báo lưu game, lỗi kỹ thuật, inventory hoặc tên phím.

`voice-catalog.json` chứa câu nói, người nói, ID, thời lượng và đường dẫn Resources. `Raw` giữ MP3 gốc và WAV giọng rõ để chỉnh lại. Unity phát WAV trong `Assets/Resources/Dialogue/VI`. Prefab mở đầu dùng m1_open_commander.wav và m1_open_reply.wav; hai MP3 cũ được giữ để dự phòng.

## Giọng trực tiếp và bộ đàm

Chỉ `m1_radio_order` và `m1_radio_reply` có hiệu ứng bộ đàm: giới hạn dải giọng, nén nhẹ và tiếng rè nhỏ. Các câu khác giữ giọng rõ. `voice-radio-preview.wav` chỉ phát lệnh chỉ huy qua bộ đàm một lần, rồi Nam trả lời qua bộ đàm. Giọng rõ gốc được giữ riêng trong Raw, không ghép vào đoạn này.

## Thay giọng hoặc chỉnh âm

Thay WAV tại đúng tên trong Resources; giữ mono 48 kHz. Không thay ID vì script gọi theo ID. Sau thay đổi chạy **ShadowVale → Audio → Prepare Vietnamese dialogue** để cập nhật catalogue. Runtime lấy độ dài clip thực tế để căn thời gian nói.

Muốn xử lý lại MP3 gốc, dùng Python với các gói trong `requirements.txt`, chạy `rebuild_voice_audio.py` rồi chạy menu Prepare trong Unity. Độ rè nằm ở biến noise (mặc định 0.0026); tăng từng chút. Bộ lọc chỉ áp dụng khi `radio=true`. Script này làm lại WAV từ MP3 đã có, không tạo giọng mới hoặc gọi dịch vụ ngoài.

## Kiểm tra trong game

Mở game mới để nghe giới thiệu. Cứu Hùng bằng từng loại vật phẩm để nghe hai nhánh Nam và câu Hùng trả lời; về căn cứ, nhận trinh sát, báo cáo ba trại, nhận lệnh hạ chỉ huy. Nghe lệnh radio và câu trả lời; đến thuyền và rời bến. Map 2 phát ba câu theo tiếp cận, cập bến và lên bờ. Giữ Esc để skip: giọng phải dừng. Pause: giọng phải tạm dừng rồi tiếp tục, không phát chồng.

## Không nhắc lại giới thiệu

Cutscene mở đầu giữ nội dung giới thiệu, phát bộ giọng đã đồng bộ. Sau khi chạy hết hoặc skip, dừng phụ đề và không xếp câu tự nhắc nhiệm vụ của Nam vào hàng đợi. Mục tiêu cứu Hùng vẫn nằm trong HUD nhiệm vụ. Kiểm thử hai đường kết thúc: Verification/opening-no-repeat.xml.

## Bộ giọng mở đầu đồng bộ

Chỉ huy: Tom. Nam: Brodie. Cùng bộ giọng tiếng Việt với phần sau; đoạn mở đầu là lời nói trực tiếp, không lọc bộ đàm. Câu Nam: **Rõ! Tôi sẽ hoàn thành nhiệm vụ!**. Bản đáp Nam dùng nhịp 0.9 để phát âm rõ hơn.

Raw/m1_open_*.mp3 giữ audio mới. Chạy rebuild_opening_audio.py để làm lại WAV mono PCM 48 kHz, âm lượng và fade giống thoại sau, ghép ba phần chỉ huy và xuất opening-voice-timing.json. Sau đó chạy ShadowVale → Audio → Prepare Vietnamese dialogue. Cả menu Prepare opening cũng ưu tiên bộ giọng mới, không tự trở về hai MP3 cũ.

opening-voice-preview.wav phát chỉ huy rồi Nam để nghe kiểm tra dấu và ngữ điệu. Phụ đề chuyển theo các mốc được đo từ từng phần audio. Kiểm chứng runtime trong Verification/opening-aligned.xml; XML kiểm tra tích hợp, không xác nhận chất lượng phát âm bằng người nghe.
