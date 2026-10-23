# C3 kẻng — mẫu C đã duyệt ngày 18/10/2026

Runtime: `Assets/Resources/OperationsAudio/c3_kieng.wav`.
SHA-256: `14cbbf11cea49daabe418cae0703b4da46a408937dbe0135104ff21e53d8df95`.
Mono PCM16, 48 kHz, 3.7 giây. Một clip chứa ba nhịp tại 0 / 0.65 / 1.30 giây.

Nguồn bản thu, đều CC0 1.0 theo trang tác giả:

- Department64, **MetalGong 12.wav**: https://freesound.org/people/Department64/sounds/547296/
- BenDrain, **Metal_Pipe_04.wav**: https://freesound.org/people/BenDrain/sounds/487837/
- Giấy phép: https://creativecommons.org/publicdomain/zero/1.0/

Dùng bản MP3 nghe công khai được liên kết trên trang nguồn, không phải WAV gốc cần đăng nhập. Đây là bản phối từ các bản thu kim loại, không phải bản thu kẻng lịch sử Việt Nam và không dùng AI tạo âm. URL nguồn, MP3 đầu vào, hash và công thức xử lý được giữ cạnh tài liệu này. Không lưu HTML website vì có token nhúng không liên quan tới asset. Người dùng đã nghe chọn C và cho phép tích hợp thủ công với nguồn/hash do máy chưa có game-dev CLI. Đây không phải chứng nhận package của CLI đó.

Âm phát từ vị trí thanh sắt do `BuildKieng` tạo lúc chạy. `Map01OperationAudio.Play` dùng spatial blend 1 riêng cho kẻng; các âm khác giữ mặc định .8. Dùng mức âm .5, linear rolloff, min 2 m và max 45 m của dịch vụ âm thanh hiện có. Đây là thông số kế thừa, chưa phải kết quả đo nghe ngoài hiện trường. Gameplay noise 45 m và cơ chế báo động không đổi. Cờ `kiengRung` hiện có ngăn phát lại.

