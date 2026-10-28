# Menu — kiểu chữ stencil và bảng kim loại

Giữ nguyên `Background.png`. Chỉ thay logo, chữ, bề mặt nút và bố cục theo demo được duyệt.
Các nút vẫn là control tương tác, không phải chữ được ghép lên toàn bộ ảnh nền.

## Asset và nguồn

- `Assets/_Project/Map01/Resources/Menu/Wordmark.png`: logo riêng, alpha trong suốt.
- `Assets/_Project/Map01/Resources/Menu/ButtonPlate.png`: bảng nút trống, alpha trong suốt.
- Hai PNG tạo bằng **built-in imagegen**, tham chiếu demo `exec-f6ea4a27-e4b3-4cd9-8782-abefca6e2001.png`.
- `Assets/_Project/Map01/Resources/Menu/Fonts/BlackOpsOne-Regular.ttf`: font Black Ops One từ
  [Google Fonts](https://github.com/google/fonts/tree/main/ofl/blackopsone), có bộ ký tự tiếng Việt
  trong [metadata chính thức](https://raw.githubusercontent.com/google/fonts/main/ofl/blackopsone/METADATA.pb).
  Giấy phép SIL Open Font License nằm cạnh font tại `Fonts/OFL.txt`. Không sửa font gốc.

Texture giữ nguyên file gốc; runtime dùng UV để bỏ khoảng alpha ngoài viền. Bảng nút chia
9 vùng khi vẽ để giữ hình tròn của đinh tán ở các kích thước nút khác nhau. Chọn/hover phủ
màu khaki, nhấn làm tối bề mặt và dịch chữ xuống nhẹ; nút vô hiệu hóa giảm tương phản.
Menu chưa có save chọn sẵn Chơi mới. Logo được vẽ từ PNG; chữ tương tác dùng font đóng gói
cùng game, không cần Georgia hoặc font cài trên máy người chơi. Chữ thông tin nhỏ vẫn dùng
font dễ đọc; giới hạn độ rộng nhãn tránh cắt chữ và dấu tiếng Việt.

## Kiểm tra trong Unity

12/12 test Map01 đạt sau khi thay skin, gồm kiểm tra font có các ký tự tiếng Việt trên nút.
Sau khi tăng cỡ chữ, test tích hợp menu/lưu/tải được chạy lại và đạt. Các ảnh dưới đây là
screenshot từ Game View, không phải ảnh mockup. Nền gốc giữ nguyên (đã so SHA-256).

- [Menu chính](Previews/Menu/main.png)
- [Lưu/tải](Previews/Menu/saves.png)
- [Tạm dừng](Previews/Menu/pause.png)
- [Xác nhận](Previews/Menu/confirm.png)

## Prompt đã dùng

### ButtonPlate

Use case: background-extraction / UI asset. Reference image is approved SHADOWVALE mockup.
Produce ONLY ONE standalone EMPTY military button face extracted/reconstructed from its first
dark green button. Remove every letter and all arrows. Very wide landscape image, rectangle
ratio about 5:1. Button fills canvas nearly edge-to-edge (2 percent padding maximum). Flat front
facing rectangular aged olive painted metal field-equipment plate, extremely shallow raised fine
double brass rim, tiny brass rivets at FOUR corners, subtle thin scratches, matte slightly mottled
army green center with good quiet dark contrast for ivory text to be added in engine. Preserve
reference palette and rustic painted texture. No perspective. No text, letters, numbers, labels,
symbols, background scenery, shadows outside the plate, extra objects. TRUE TRANSPARENT alpha
background outside the single plate, not checkerboard pattern. All four edges visible, no thick
frame, no rounded pill shape. Output one game-ready PNG texture.

### Wordmark

Use case: background-extraction / game logo asset. Extract/reconstruct ONLY the exact SHADOWVALE
wordmark from upper left of reference image. Bold tall condensed military stencil lettering,
controlled weathered paint flecks. Solid very dark olive #242a18 glyphs, legible stencil bridges,
text spelled exactly SHADOWVALE, a single horizontal line. TRUE TRANSPARENT alpha background,
no paper rectangle, no scenery, no border, no subtitle, no shadow, no other letters. Wordmark fills
wide canvas edge-to-edge leaving at most 2 percent clear margin. Preserve reference letter
proportions and rustic wartime print character. Deliver standalone game-ready wordmark PNG.
