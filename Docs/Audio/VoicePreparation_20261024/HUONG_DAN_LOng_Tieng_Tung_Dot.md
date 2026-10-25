# Chuẩn bị lồng tiếng theo đợt — 24/10/2026

Project: G:\game\Capstone\ShadowVale. Nhánh Khoa, dev đã nhận: `5e4a0d46`.

Không thu hàng loạt mới trong bước chuẩn bị này. Không chi tiền. Bản giọng đang có là ứng viên nghe kiểm tra, chưa được coi là người dùng duyệt từng vai. Các bản thu và thay đổi trước đó được giữ nguyên.

## Thứ tự làm

1. Nghe và chốt Nam, Hùng, chỉ huy trước để thống nhất xuyên game.
2. Map 1 + tutorial: làm từng cuộc thoại, kiểm tra kênh bộ đàm, thời lượng và skip.
3. Map 2 Ngày 1–Đêm 1: gia đình, bến, bữa cơm, chốt; duyệt riêng chị Lành, ông Sáu, chú Bảy và bé Tí.
4. Ngày 2–Đêm 2: Tâm, tổ gác và lời đàm.
5. Ngày 3–Đêm 3: báo động, sơ tán, hy sinh, bình minh; chỉnh nhịp theo hoạt ảnh sau khi giọng ổn.

Mỗi đợt nên 5–10 câu; nghe chữ, tuổi/giới, sắc thái sức khỏe; đạt mới thay vào game và test cảnh đó. Không thu lại cả bộ chỉ vì sửa một câu. Không tự gắn tiếng thở/ho vào mọi câu của người bị thương. Không đọc chữ hướng dẫn HUD như thoại nhân vật.

## Bộ giọng ứng viên miễn phí

| Vai | Preset | Chỉ đạo |
|---|---|---|
| Nam | Adam | young adult, healthy, energetic |
| Hùng | Thanh Bình | middle aged, right arm injured; restrained pain, not elderly |
| Chỉ huy | Minh Triết | middle aged, clear concise authority |
| Anh Thành | Minh Triết | middle aged, clear concise authority |
| Anh Lực | Đức Trí | adult guerrilla, direct, alert |
| Chị Lành | Thục Đoan | adult woman, caring, southern |
| Ông Sáu | Thái Sơn | elderly man, frail; short breath |
| Chú Bảy | Đức Trí | older man, sturdy boatman; distinct from frail Sau |
| Tâm | Mạnh Dũng | young adult scout, injured leg; breath restrained |
| Tổ gác | Phạm Tuyên | adult guard, healthy and alert |
| Người trinh sát | Mạnh Dũng | young adult scout |
| Bé Tí | Mỹ Duyên | child boy; proxy preset, requires listening review |

Bé Tí hiện chỉ có preset giả lập tuổi trẻ, chưa đủ cơ sở coi là giọng bé trai phù hợp; cần nghe chọn lại trước khi duyệt. Hùng trung niên, Nam trẻ khỏe theo yêu cầu mới nhất. Các độ tuổi là chỉ đạo diễn giọng, không tự thêm tuổi canon.

## Danh sách đợt

- `01_Map1_Tutorial.csv`: 93 câu.
- `02_Map2_Ngay1_Dem1.csv`: 50 câu.
- `03_Map2_Ngay2_Dem2.csv`: 32 câu.
- `04_Map2_Ngay3_Dem3.csv`: 60 câu.

CSV UTF-8 có BOM mở được trong Excel. Giữ ID để thay đúng clip; cột approved mặc định False. Không tự gộp các alias thành thêm lượt nói. Nguồn model/preset: https://huggingface.co/pnnbao-ump/VieNeu-TTS-v3-Turbo (Apache-2.0); chạy offline, không clone giọng người từ tệp ngoài.

Kiểm thử toàn game ở lượt trước chưa tạo XML hoàn tất; không báo đạt hoặc đã duyệt hết. Cần chạy lại sau khi ổn định bản đồng bộ.
