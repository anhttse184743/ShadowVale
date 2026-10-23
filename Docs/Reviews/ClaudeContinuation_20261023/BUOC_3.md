# Hoàn tất bước 3 — Map 2 Ngày 1/Đêm 1, 23/10/2026

Người dùng yêu cầu hoàn tất bước 3 trong kế hoạch rồi ghép lên `dev`.

## Kết quả trong game

- “Lời hứa”: Nam và Hùng ở hiên; gia đình không ngồi chung mâm, chị Lành nói từ bếp; camera qua khung cửa và hai góc cận. Câu đùa đã được người dùng nghe duyệt; bộ Ngày 1/Đêm 1 có đủ 50 cue có tiếng.
- Nhịp cảnh tự kết thúc, giữ sau câu cuối ít nhất 3 giây và trả điều khiển. Tải lại trạng thái hoàn thành không diễn lại cảnh.
- Tiếng dế stereo 11,452 giây, 184.393 byte chỉ phát nhỏ sau câu cuối; fade vào/ra, tạm dừng/tiếp tục cùng game; kết thúc, bỏ qua hoặc tải lại đều dừng. Nguồn và giấy phép: [AUDIO_CREDITS.md](../../Map02Chapter/DayOne/AUDIO_CREDITS.md).
- Tổ gác bờ tre có hai người; anh Lực giữ chốt đầu làng. Lời tổ gác dùng lại đúng bản thu cũ theo lựa chọn người dùng.
- Máy đàm Hùng đã kiểm trên mesh và ảnh thật ở lượt trước. Giữ các sửa bước 1–2 của Claude đã được duyệt: mũi tên đèn, súng đeo vai, mâm cơm theo đêm, bát trượt, điểm vịt giữa đường, chân Thành lên bờ và luật dẫn Tâm.
- Gỡ nhãn `pendingVoice` còn sót ở các câu đã có WAV được duyệt; không thu lại hoặc đổi câu thoại đã chốt.

## Bằng chứng kiểm thử

Lượt trước: `regression.xml` có 62 ca (60 đạt, 2 thiếu WAV 05b); sau bổ sung WAV, `final-voice-staging.xml` đạt 28/28. Kết quả mới nhất theo từng tên ca của lượt đó: 63/63.

Lượt bước 3: `step3-final.xml` đạt 29/29 sau bổ sung tiếng dế. Sau đó `step3-cleanup.xml` đạt 5/5, kiểm riêng pause/resume, tự kết thúc, tải lại và metadata thoại. Tổng hợp kết quả mới nhất theo tên ca: **65/65 ca riêng biệt đạt**, không cộng trùng các lần chạy. Chi tiết từng ca nằm trong [step3-verification.json](step3-verification.json). Hash 20 file lưu hiện có không đổi; hai scene không có diff; 50 WAV khớp manifest và hai catalog giống nhau.

Test timeline dùng game-clock; test giọng đọc riêng phát hết 50 WAV theo thời gian thực. Không dùng thời gian chạy fixture để suy ra thời lượng chơi toàn Ngày 1–Đêm 1 hoặc 60 FPS. Các test mới được viết trong `Map02DayOneCompletionTests.cs`, dùng save riêng của `ForestSceneTestBase`.

## Phạm vi art và âm thanh còn lại

Đây là hoàn tất bước 3 về code/dàn cảnh. Cảnh vẫn dùng ghế đẩu và clip MealNam/MealHung; máy đàm và đạo cụ vẫn là model đơn giản. Bộ hoạt ảnh mới (ngồi xếp bằng, đũa, hích vai, đặt/đẩy nồi), bỏ mũ/ba lô, ambience cả map, nhạc Hùng, tiếng cười và UI kho là bước 4–6; chưa nghiệm thu art và vẫn phải qua duyệt Blender. Không đổi scene Map 1/Map 2, model Nam, art C3 hoặc cân bằng chiến đấu trong lượt này.

## Ghép nhánh

Đã lấy lại trạng thái `origin/dev` và `origin/thinh`: lần kiểm này vẫn lần lượt là `7abc29af` và `a9119fcc`. `origin/dev` là tổ tiên của `thinh`, nên có thể ghép bằng fast-forward, giữ toàn bộ lịch sử và không phải chọn bỏ bên nào. Chỉ stage code/assets/test/tài liệu liên quan, giữ riêng hai ảnh M16 đã sửa từ trước và các thư mục nguồn art không liên quan. `testCraftingKit=false` trước khi đẩy. Kết quả SHA và push cuối cùng ghi trong biên nhận local `step3-push-receipt.json` và trả lời cuối cho người dùng.
