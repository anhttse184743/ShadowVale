# Đồng bộ giọng Map 1 và Map 2

Nguồn phân vai duy nhất là `Assets/Resources/Dialogue/voice-cast.json`. Hai map, bộ đàm và hồi tưởng dùng chung
giọng nhân vật trong bảng này. Alias chỉ đổi tên vai, ví dụ Chỉ huy/Anh Thành; không đổi người lồng tiếng.
Anh Lực và chú Bảy hiện cố ý cùng preset Đức Trí, có xử lý diễn xuất khác nhau.

Mỗi mục trong `voice-catalog.json` phải có `speaker`, `voice`, `provider`, `resource`, `sha256` và thời lượng
khớp file thật. `DialogueVoice` từ chối bản thu sai hoặc thiếu phân vai, báo lỗi theo ID một lần.
`Map02DialogueCatalog` kiểm tra thêm người nói của bản thu được alias. Các câu tuần tra địch dùng lại ID
Runway chung thay vì chọn một file rời không được xác minh.

Sau mỗi đợt tạo hoặc thay âm thanh, chạy:

```sh
python SourceArt/Dialogue/verify_voice_cast.py
```

Lệnh kiểm tra cả catalog, hash/thời lượng, hai kịch bản Map 2, các bản bộ đàm, câu mở đầu Map 1 và clip xuất
riêng của Hùng. Sau đó chạy `DialogueCastingTests`, các test dialogue/voice và test cảnh bị ảnh hưởng trong Unity.
Không đổi giọng chỉ bằng cách sửa nhãn metadata; phải dùng đúng nguồn ghi âm và cập nhật hash/provenance.

Câu hồi tưởng của Hùng được dựng lại bằng:

```sh
python SourceArt/Dialogue/HungEcho20261101/rebuild_echo.py
```

Script lấy toàn bộ bản Anh Khôi có đúng câu chữ `m2v3_sacrifice_02`, giữ vang và thời lượng 4,65 giây,
cập nhật các manifest liên quan, rồi xác nhận 272 bản thu khác không đổi. Không gọi TTS hoặc dịch vụ trả phí.
Các script cũ trong thư mục có ngày là hồ sơ lịch sử; không dùng bảng CAST cũ trong chúng để thay phân vai hiện tại.

Kiểm tra kỹ thuật không thay thế nghe duyệt phát âm/diễn xuất. `castingApproved` là duyệt bộ giọng;
`userApproved` chỉ được bật khi người dùng đã nghe duyệt chính bản thu đó.
