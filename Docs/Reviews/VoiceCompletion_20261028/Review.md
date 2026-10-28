# Đồng bộ voice — 28/10/2026

## Kết quả tích hợp

- Nam: toàn bộ 95 cue trong catalog hai map dùng Minh Quân, theo bộ Nam C đã chọn. Ba câu báo cáo tuyến sơ tán được thu lại offline; hai câu xác nhận dùng lại bản Nam C phù hợp rồi áp dụng lọc bộ đàm.
- Anh Thành: câu đáp mới dùng Minh Đức, công thức chỉ huy B: tempo 0,92, pitch 0,94. Không đổi bộ giọng nhân vật khác.
- D3_06: thêm báo cáo ngắn về vật tư, hai thuyền và các gia đình; anh Thành xác nhận theo phân công; Nam chỉ nói Rõ ở cuối. Giữ toàn bộ mệnh lệnh tối trong cảnh bữa chiều; không đọc lại mệnh lệnh.
- Địch Đêm 2: tái sử dụng nguyên file Runway đã có, không tạo giọng địch mới: m1_ops_enemy_spotted và m1_ops_enemy_order. Phụ đề đổi sang đúng tiếng Việt của bản thu.
- SourceArt/Dialogue/EvacuationChoices20261027/build_voice.py đã đổi preset/cache để các lần tạo câu sơ tán sau không ghi đè Nam C bằng Adam.
- Chi phí: **0**. Chạy model/cache VieNeu offline có sẵn. Không gọi Runway/Narakeet trả phí.

## Kiểm chứng và giới hạn

272 mục catalog có file giải mã được, thời lượng và SHA-256 được cập nhật trong integrated-manifest. Map 2 có 151 cue kịch bản có lời và ba cue cập bến; các đoạn chủ ý im lặng vẫn giữ. Ngoài catalog kịch bản có hai clip địch tuần tra phát trực tiếp.

263 resource audio không thuộc đợt sửa đã được đối chiếu hash trước/sau và giữ nguyên. Không tự đặt userApproved=true cho các bản mới chưa được người dùng nghe; cờ duyệt bộ giọng tách khỏi duyệt từng bản thu.

Kết quả Unity sẽ được bổ sung sau lượt kiểm thử. Bản nghe thử gồm các câu thay đổi và địch được dùng lại, có khoảng nghỉ giữa câu; không phải toàn bộ thoại trò chơi.

## Tái tạo

complete_voices.py dùng môi trường Python có VieNeu ONNX, model v3 Turbo đã cache, và ffmpeg. Các biến SVL_AUDIO_WORKSPACE/SVL_GAME_ROOT cho phép đổi workspace và project. Quy trình một đợt: prepare → render → install; không chạy lại prepare/install trên catalog đã cài nếu chưa lập bản sao/bản kế hoạch mới. Giữ raw/clean/radio của đợt này kèm integration.json để đối chiếu.

Các script FullGameFree/VoicePreparation cũ là hồ sơ lịch sử. Cast đang dùng phải lấy từ Assets/Resources/Dialogue/voice-catalog.json và bộ kiểm thử NamCVoiceTests, không lấy các preset cũ trong hồ sơ đó.

## Lượt kiểm tra rộng

VoiceTests.xml: 35 ca, 33 đạt và 2 chưa đạt. Các ca về load voice, cast Nam C hai map, khớp caption, thời lượng cử chỉ bữa cơm, phát toàn bộ 95 câu Nam, không chồng tiếng, pause và báo cáo Ngày 3 đều đạt.

Hai ca chưa đạt ngoài âm thanh:
- Map02ChapterTests.LiveMapHasReachableObjectivesAndSkipSaveRestorePreserveChapter: kiểm tra exposure sau tải checkpoint mong <0,15 nhưng nhận 0,3145.
- Map02RoutePreviewTests.PreviewStartsAtTheSelectedRouteAndCreatesItsOwnRetryPoint: kiểm tra preview mong 90 viên, trong khi cấu hình preview hiện dùng TestRounds=495. Đây là chênh lệch kỳ vọng test/cấu hình cần rà riêng.

Các con số trên không được coi là toàn bộ game đã qua mọi kiểm thử. Không thay thiết lập ánh sáng hoặc đạn trong đợt voice này.

## Bản nghe thử

[Nghe 10 cue đã đồng bộ / tái sử dụng](NgheThu_DongBo.mp3) · [Thứ tự từng câu](preview-timeline.txt). Các bản mới đã tích hợp, userApproved vẫn phản ánh việc chưa nghe duyệt từng bản thu.

## Lượt chốt

FinalVoiceTests.xml: **3/3 đạt**. Toàn bộ 151 cue kịch bản khớp bản thu; hai clip địch đúng từng byte so với bản Runway cũ; D3_06 phát tự nhiên đủ ba lượt thoại qua phím E rồi trả điều khiển/sang Đêm 3; lời địch tạm dừng và tiếp tục đúng khi mở/đóng pause.

Đã thêm pause/resume cho AudioSource địch phát riêng và Việt hóa nhãn phụ đề. Lượt phát 95 cue Nam trước đó dùng pitch 2 để kiểm tra vòng đời audio, không dùng để đánh giá diễn xuất. Các câu mới chưa được gắn cờ người dùng đã nghe duyệt.
