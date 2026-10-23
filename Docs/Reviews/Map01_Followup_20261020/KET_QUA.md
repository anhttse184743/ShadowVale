# Map 1 và đồng bộ dev/thinh — 21/10/2026

Kết quả kiểm chứng: **113/113 ca riêng biệt trên bản ghép đạt; dev riêng 18/18 đạt**. Không cộng trùng các lượt chạy lại. Phạm vi người dùng đã duyệt: rà gameplay Map 1 còn dở, sửa lỗi có bằng chứng, kiểm dev mới, merge và push cả thinh/dev. Không đụng origin/dev2.

## Nội dung

- Commit `bcce17d2`: C3 đi bộ về chốt khi điểm danh thiếu; lệnh đổi chốt hủy pause cũ; phụ đề lời chưa có bản thu; hệ số cảnh giác đọc balance; Hùng có đúng pose khi ngoài màn hình và sau tải checkpoint; đá rơi trên mái được điều tra từ sàn có đường tới; cụm đá 17 nhặt được và bổ sung 34/35 để nối tuyến.
- Nhận dev `fbff9a6d013a75c425d38201df64710e165e1752`: M16/đạn 5,56/thay băng/loot/icon và model NPC. Giữ tích hợp K54 thường, Mosin PU/nạp từng viên, dao giường, ngủ/thức C3 và luật phát bắn xa không tự lộ Nam.
- Hợp nhất đổi súng chính: chỉ trả đạn magazine Rifle về đúng loại AK/M16, giữ riêng magazine K54 và Mosin khi đang cầm súng khác.
- Sửa thêm lỗi clone M16 mất baseline AK và lỗi đổi súng lính sau tải khi roster C3 có thêm dự bị. Snapshot mới ghi loại súng; snapshot cũ có fallback.
- Map 2 đọc loại đạn từ súng thực đang dùng, lưu loại súng vào snapshot và áp lại model khi load; HUD dùng cùng nguồn. Giữ tương thích save cũ bằng AK fallback. Không phụ thuộc đối tượng chuyển cảnh đã bị hủy.

## Kiểm thử và bằng chứng

- Preflight dev riêng: `dev-latest.xml` 17/18. Test cũ mặc định lính dùng AK; sau cập nhật kỳ vọng cho cả AK/M16, `dev-balance-fixed.xml` 1/1. Tổng mới nhất: 18 ca riêng biệt đạt. Không đổi sát thương để làm xanh test.
- Bản ghép vũ khí: `merged-weapons-before.xml` 8/10, hai lỗi runtime được tái hiện; `merged-weapons-fixed.xml` 10/10 sau sửa.
- Hồi quy bản ghép: `merged-regression.xml` 92/92 đạt, không lỗi, không bỏ qua; bộ lọc lưu ở `merged-regression-filter.txt`. Không lấy tổng test của lượt merge cũ để thay kết quả hiện tại.
- Phát hiện hai scene sandbox K54/Mosin ở máy là bản sao cũ (SHA256 B6E9CD5D...), đã giữ tại `BeforeSandboxRefresh/` rồi chép từ Map 1 hiện tại (C40FDFF3...). Hai sandbox giữ tên/GUID riêng. Không sửa scene gốc. `fresh-weapons-before.xml`: 21 test vũ khí đạt trên bản sao mới; ca mới Map 2 lỗi đúng kho đạn (13 viên AK thay vì 55 viên 5,56).
- Sau sửa Map 2: `map02-m16-fixed.xml` 13/13 đạt. Ca mới kiểm trừ đạn từ đúng kho, save file thật, load scene mới không có Arrival, model M16/số đạn đúng và JSON cũ thiếu field mới quay về AK. Kèm hồi quy runtime, tiến độ và trạng thái thế giới Map 2.
- Kết quả mới nhất từng fullname và SHA256 XML: `TEST_MANIFEST.json` / `TEST_MANIFEST.md`. Tổng bản ghép 113/113, không lỗi, không bỏ qua. Đây là nhóm được chọn theo thay đổi, chưa phải toàn bộ test của repo.
- Trước merge dev mới: C3 film `followup-r6.xml` đạt; Hùng `followup-r7.xml` đạt. XML đỏ/timeout/compile ở các lượt trước được giữ làm lịch sử, không cộng vào số đạt mới nhất.

Video chức năng C3: `Logs/BodyDiscovery/c3-check-in-walk.mp4`, 452 khung 1920×1080 ở 60 khung/s. Metrics: bốn lính đi về chốt, bước ngang tối đa 0,06 m; bước cao nền của chỉ huy 0,2025 m được đo riêng. Đây là ghi hình chức năng, chưa phải benchmark 1080p/60 FPS trên RTX 3050.

Các lượt test dùng slot riêng. Dev preflight cũ thiếu batch fallback từng ghi ô auto; đã giữ bản phát sinh và khôi phục từ backup có SHA256. Ô thủ công 0/1 giữ nguyên. Backup/save người dùng không đưa lên Git.

## Phần còn lại ngoài lượt sửa này

Lính quỳ kiểm xác; kéo/vác xác; tag thủ công từng lính; polish toàn bộ animation bằng Mixamo và benchmark Player build vẫn là công việc riêng. Không tự tạo/retarget clip hay thay rig Nam/lính trong lượt này; các model NPC mới nhận nguyên từ dev. Chi tiết rà hiện trạng và vị trí code ở RA_SOAT.md; phương án giải xung đột và nguồn Unity đã đọc ở MERGE_PLAN.md.

## Git và kiểm tra bảo toàn

- Merge nguồn dev `fbff9a6d` vào thinh sau commit sửa Map 1 `bcce17d2`; remote thinh trước cập nhật là `e44dab1e`. Không force push; đồng bộ hai ref bằng `git push --atomic origin HEAD:refs/heads/thinh HEAD:refs/heads/dev`.
- Hash cuối của scene Map 1, AC_Enemy và ba ô lưu 0/1/4 khớp trước sửa; số liệu và SHA đầy đủ ở `release-verification.json`. `testCraftingKit=false`. Tùy chọn shader của Editor trả lại 1 sau test. 4.264 GUID asset tracked đã quét không trùng.
- `PUSH_RECEIPT.txt` tại thư mục báo cáo trên máy ghi lệnh xuất bản và SHA remote kiểm lại sau push; file biên nhận sinh sau commit, không thuộc nội dung game. Lịch sử Git của báo cáo chứa merge commit được xuất bản.
- Các demo/recovery và thay đổi dở có sẵn ngoài phạm vi được giữ tại máy, không stage toàn bộ working tree. Save/backup/video/scene sandbox và log lớn ở local; XML và manifest gọn được đưa vào báo cáo.

## Vị trí code đã kiểm (từ gốc repo)

| Phần | File:dòng |
|---|---|
| Phụ đề khi thiếu bản thu | `Assets/_Project/Map01/Runtime/Map01Mission.cs:323` |
| C3 đi bộ sau điểm danh | `Assets/_Project/Map01/Runtime/Map01C3Mission.cs:426` |
| Hủy pause của chốt cũ | `Assets/_Project/Map01/Runtime/Map01EnemyController.cs:638` |
| Chọn sàn có đường đến tiếng đá | `Assets/_Project/Map01/Runtime/Map01EnemyController.cs:429` |
| Cưỡng bức đánh giá pose Hùng | `Assets/_Project/Map01/Runtime/Map01HungVisual.cs:111` |
| Khôi phục pose Hùng khi load | `Assets/_Project/Map01/Runtime/Map01SaveSystem.cs:514` |
| Baseline AK khi clone M16 | `Assets/_Project/Map01/Runtime/Map01RifleVisual.cs:10` |
| Giữ súng lính theo snapshot | `Assets/_Project/Map01/Runtime/Map01EnemyController.cs:463`, `:509` |
| Đạn của Rifle độc lập lục/nhắm | `Assets/_Project/Map01/Runtime/Map01M16.cs:44` |
| Đạn/model M16 sau load Map 2 | `Assets/_Project/Map01/Runtime/Map02Mission.cs:51`, `:222`, `:250` |
