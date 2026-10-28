# Súng trên thuyền và âm nền Map 2 — 2026-10-28

## Phạm vi
Cuối Map 1 → cập bến/đỡ Hùng/lên bờ Map 2, âm nền Ngày 1–Đêm 1.

## Nguyên nhân và bản sửa
- Trước đây quyền hiển thị súng chỉ quét dưới Animator, trong khi PlayerCombat có súng khác nằm ngoài nhánh đó. Một model có thể vừa đeo vừa cầm súng.
- Khi đổi sang model C1, code tắt hẳn súng rồi bật lại sau cảnh. Nay một bản hiển thị không có script vũ khí bám theo đúng thân Nam đang được render; tất cả súng gameplay dưới gốc player được che trong thời gian đeo. Khi bắn yểm trợ, chỉ hiện một súng trong tay. Khi trả gameplay, trả socket và quyền hiển thị cho PlayerCombat.
- Hai hệ âm nền cùng tạo tiếng chèo/nước và nhạc bữa cơm. Đã bỏ hệ phát lặp trùng, giữ hiệu ứng ho và đặt thùng.
- Tiếng nước Ngày 1 giảm từ gain 0.60 cộng lớp 0.15 xuống một lớp 0.065, nghe trong bán kính 18 m thay vì 24–26 m. Khi có thoại còn 25% gain nền. Không sửa âm lượng thoại.
- Giảm tiếng mái chèo trong cutscene, giữ nguyên tiếng súng yểm trợ; giảm các lớp gió/trẻ em/ếch/bếp lửa và nhạc Lời hứa.
- Máy bay dùng cờ save chung để tránh phát hai lần.

## Kiểm thử
- FinalTests.xml: 5/5 ca đạt trong Unity Editor 6000.3.24f1, chạy EditMode → Play Mode thực tế.
- AK: 3.399 khung hình đeo súng, trong đó 621 khung hình model C1; Mosin: 2.949 khung hình, trong đó 596 model C1. Mỗi khung ghi nhận đúng một súng hiển thị, bám gần ngực của model hiện hành. Khi kết thúc cả hai ca, chỉ còn một súng gameplay hiện và chuỗi nhiệm vụ trả điều khiển.
- Kiểm tra một lớp nước duy nhất, gain 0.065 khi không thoại, 0.01625 khi có thoại; pause/resume đúng.
- Kiểm tra pha ngày/đêm, máy bay một lần, dừng nhạc khi bỏ cảnh, ho trước thoại và hiệu ứng thay băng hiện có.
- Ca SeatedCoverKillsExistingGuardsWithoutSpendingAmmoOrAddingLoot đạt ở lượt trước (Tests.xml): Nam vẫn bắn yểm trợ, không tiêu đạn/tạo loot thêm.
- Lượt đầu có hai bài test chuyển map chưa chạy đúng Play Mode vì đặt lệnh EnterPlayMode trong IEnumerator lồng. Đã sửa cấu trúc bài test và chạy lại đầy đủ; kết quả cuối ở FinalTests.xml.
- Có kiểm tra trực tiếp ảnh ở bậc thuyền và bến Map 2. Đây là kiểm thử phạm vi cảnh thuyền/âm nền; không phải cam kết toàn bộ game không còn lỗi.

## Tệp bằng chứng
- Mosin-Map1-Seating.png: hai tay trống, một súng đeo sau vai khi xuống thuyền.
- Mosin-Map2-Disembarking-C1.png: súng duy trì khi chuyển model ở bến.
- AK-audit.txt, Mosin-audit.txt: tổng mẫu theo giai đoạn và số súng thực tế.
- audio.txt: gain và lớp âm thanh khi có thoại.
- FinalTests.xml, InitialTests.xml: kết quả test native.

Không tạo lại hay thay giọng thoại. Không tạo chi phí dịch vụ. Chưa push.
