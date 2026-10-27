# Chốt giọng địch và chỉ huy B — 25/10/2026

Project `G:\game\Capstone\ShadowVale`, nhánh Khoa.

- Người dùng duyệt bản địch Runway gốc và chỉ huy B.
- Địch: giữ đúng 15 file Martin/Runway gốc; hash trước/sau không thay đổi. 11 câu mới chưa có Runway gốc giữ phụ đề, không gán giọng offline.
- Chỉ huy B: Minh Đức, tempo 0,92, pitch 0,94. Cập nhật 49 cue: 27 Chỉ huy và 22 Anh Thành, áp dụng xuyên Map 1/Map 2, cả dữ liệu nhánh cũ còn trong catalog.
- Giữ nguyên nguyên văn thoại, ID, resource và kênh bộ đàm. Cập nhật WAV, clean master, metadata thời lượng và SHA-256.
- Clip mở đầu Map 1 được dựng lại bằng ba câu chỉ huy mới. Mốc phụ đề: [0.0, 4.076083333333333, 10.50175] giây. Giọng Nam đáp giữ nguyên.
- Tất cả giọng khác, gồm Hùng A và ông Sáu A, được giữ nguyên qua hash.
- 12/12 kiểm thử Unity đạt: nhập/độ dài toàn bộ 260 cue có audio, giọng địch chỉ dùng Runway gốc, giọng các vai, script Map 2, thoại Map 1 và clean/bộ đàm, prefab mở đầu, đủ Ngày 1/Đêm 1. Có lượt phát đến hết cả 49 cue chỉ huy ở tốc độ 3× trong cảnh test cô lập.
- Chi phí tạo mới: 0. Dùng VieNeu offline, không gọi Runway hay dịch vụ trả phí.

Kiểm thử kỹ thuật không thay cho nghe duyệt phát âm và diễn xuất từng câu. Mẫu giọng B đã được duyệt; các câu mới được tích hợp theo bộ giọng đó.

`before/` lưu bản sao trước lần tích hợp. Chưa commit/push trong bước này.
