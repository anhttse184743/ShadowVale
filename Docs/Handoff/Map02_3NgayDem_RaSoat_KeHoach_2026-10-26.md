# Map 2 — Rà soát và kế hoạch hoàn thiện 3 ngày, 3 đêm

Ngày lập: **26/10/2026**. Mốc rà soát: **`origin/dev` — `3fd2b9a092268c93ead39f8614d0217e2f202aae`**, theo lựa chọn của Thịnh.

**Cập nhật trong lượt:** dev có thêm `cf9d9090` (thẩm vấn Map 1; không thay file Map 2 đang rà). Thịnh cho tiếp nhận/commit/push phần Claude và chốt giữ giọng dev, lưu bộ Claude chờ duyệt. Dự án chính đã cập nhật và ghép sửa nhanh; 83/83 ca tích hợp đạt, thêm lượt catalog 8/8 đạt (87 ca riêng biệt qua hai lượt). [Báo cáo sửa nhanh](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/Map02_Audit_20261026/SUA_NHANH.md).

**Yêu cầu mới trong lượt làm:** ưu tiên sửa ngay những lỗi rõ ràng, sửa nhanh; giữ nguyên phần Claude đang dở. Thịnh đã xác nhận mọi tác nhân khác dừng sửa dự án. Bản này phân biệt kết quả sửa nhanh với công việc thiết kế/animation còn phải duyệt.

## 1. Nguồn và cách đọc trạng thái

- Kịch bản gốc: [ShadowVale Map 2 – Kịch bản chi tiết 3 ngày đêm.docx](<C:/Users/Admin/Downloads/ShadowVale Map 2 – Kịch bản chi tiết 3 ngày đêm.docx>). Không chỉnh tài liệu gốc.
- [Bản trích xuất đánh số B001–B344](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/Map02_Audit_20261026/script-numbered.txt) và [biên nhận nguồn](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/Map02_Audit_20261026/source-receipt.json). `Bxxx` là khối trong Word, **không phải số trang**.
- [Danh mục 134 file code/dữ liệu ở đúng commit dev](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/Map02_Audit_20261026/dev-snapshot.json). Các trích dẫn `file:dòng` trong phần hiện trạng bên dưới dùng **bản chụp này**, tránh nhầm với code chưa commit trên laptop.
- [Bảng Ngày 1–Đêm 1 trước đây của Claude/Codex](D:/DO_AN_GAME/ShadowVale/Docs/Handoff/Map02_Ngay1Dem1_KeHoachSua_2026-10-23.md): dùng để kế thừa quyết định và tìm việc cục bộ, không coi mọi dòng “đã vào game” trong đó là đã có trên dev.
- [Quy tắc mục 0](D:/DO_AN_GAME/ShadowVale/Docs/Handoff/C3_ViecConLai_Prompts.md:14): giữ nguồn/rig, demo Blender trước khi xuất art mới; không tự đổi cân bằng, câu thoại hoặc kết cục. Không commit/push lượt này nếu chưa được yêu cầu.

Nhãn: **Có logic** = đã đọc đường chạy; **Thiếu nối** = code tính năng tồn tại nhưng không được gọi; **Thiếu asset** = tham chiếu trống; **Cục bộ** = chỉ thấy trong việc chưa commit ở laptop; **Cần xem** = chưa có bằng chứng hình ảnh đủ để kết luận xấu/tốt; **Cần chốt** = khác biệt thiết kế, không tự coi là bug.

## 2. Kết luận thiết kế

Map 2 đã có bộ khung đủ 43 mục tiêu, 7 giai đoạn, các mốc sơ tán và một kết cục cố định. Chưa nên làm lại tuyến nhiệm vụ. Việc cần trước là khôi phục các tính năng Ngày 2–Đêm 2 bị rời khỏi luồng chạy, rồi hoàn thiện các tương tác người–người và người–đạo cụ để câu chuyện đọc được bằng hình ảnh.

Phần Ngày 3–Đêm 3 có nhiều logic hơn hình thể hiện: một cờ “đã trao máy” không chứng minh bàn tay đã trao máy; Hùng đổi vị trí theo mương không chứng minh có animation bò. Phải nghiệm thu riêng **logic**, **hình/chuyển động**, **âm thanh** và **hiệu năng**.

### Những nền tảng nên giữ

| Nền tảng | Bằng chứng ở dev | Giới hạn |
|---|---|---|
| 43 mục tiêu, điều kiện trước/sau, 7 phase | `Map02ChapterState.cs:85–100`; `Map02ChapterTests.cs:17–25,65–73` | Test state không chứng minh người chơi đi hết map được. |
| Ngày 1: thuốc, kho, bến được làm tự do sau khi dìu Hùng | `Map02Chapter.json`, D1_03–05; `Map02ChapterTests.cs:71` | Giữ ba minigame đã được chốt; không xóa vì Word mô tả ngắn hơn. |
| Ngày 3 đi lần lượt; cơm chiều bắt buộc trước Đêm 3 | `Map02Chapter.json`, D3_01–06; `Map02ChapterTests.cs:40–49` | Không tự mở tự do Ngày 3 khi chỉnh nhịp. |
| Sơ tán hiển thị 60/70/80/90%; giữ ngõ và đẩy lùi lính | `Map02ChapterLanes.cs:14–48` | Dưới nền vẫn tích lũy tiến độ theo thời gian/áp lực; không gọi đây là mô phỏng dân đi đủ số người. |
| Yểm trợ Hùng 60–90 giây; Nam bất tử riêng đoạn này | `Map02ChapterCover.cs:16,50–70` | Phải giữ khi sửa animation; không biến cái chết Hùng thành lỗi bắn kém. |
| Bỏ qua/tải lại giữ trạng thái máy đàm, xe tăng và kết cục | `Map02ChapterState.cs:94–99`; `Map02ChapterDirector.cs:429–471` | Cần kiểm thêm hình đạo cụ sau skip/load, không chỉ kiểm cờ bool. |
| C1 v4 đã tích hợp theo duyệt | [Báo cáo C1](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/C1_Unity_20261026/KET_QUA.md) | Không làm lại từ đầu. Những cờ tự va chạm/nhịp buông còn lại đã ghi riêng. |

## 3. Lỗi ưu tiên sửa nhanh

### Q1 — Có code leo ăng-ten nhưng chơi vẫn chỉ căn tín hiệu dưới đất

**Xác nhận trước sửa.** `Map02ChapterDayTwo.cs:70,138–172` đã có đường đi, cây, leo/dừng/cột/xuống; nhưng `Map02ChapterDirector.cs:149` không gọi dựng các phần này, `StartWork:219–222` không khởi động leo, `TickWork:307–312` vẫn dùng nhánh căn sóng cũ. Test thực tế ban đầu: chiều cao mong đợi 4 m, nhận 0 m.

Sửa: nối lại hàm dựng, marker chân cây, input leo, pose, kết thúc/hủy an toàn và hướng dẫn đúng. Tái sử dụng code hiện hữu từ lịch sử dev; không tự thêm clip hay đổi chiều cao/tốc độ. Nghiệm thu: leo → dừng → căn → cột → xuống mới hoàn thành; Esc đưa xuống chân cây và không hoàn thành; không lưu giữa giao dịch.

### Q2 — Đèn tuần tra Đêm 2 không khớp một tổ lính thật

**Xác nhận trước sửa.** `Map02ChapterNightPatrol.cs:20,34–52` cần site `nightPatrol` và bước tạo tổ 3 lính; Director thiếu site/Refresh, trong khi `TickPatrol:543–550` còn chiếu từ site Tâm. Test ban đầu gặp `KeyNotFoundException: nightPatrol`, hoặc 0/3 đèn.

Sửa: nối lại tổ tuần tra đã có, nguồn sáng từ đèn cầm tay, góc nhìn/đường bắn bị vật che chắn, điểm quan sát đúng tổ lính; giữ lính hộ tống xe tăng và AI tìm vị trí cuối đã thấy của dev. Nghiệm thu: đủ 3 lính và 3 đèn; nòng đèn/luồng sáng cùng hướng; tường chắn nhìn và sát thương; save/load không nhân đôi đèn; quan sát phải nhìn tổ lính, không nhìn vị trí Tâm cũ.

### Q3 — Kiểm kho/tuyến sơ tán Đêm 2 chưa được ràng buộc đúng tương tác

**Xác nhận bằng luồng code trước sửa.** `Map02ChapterNightRoute.cs:45–84` có kiểm hướng nhìn chốt cửa và bốn đoạn đi theo thứ tự; `Director.Interact:158–202` không gọi nó. Marker cũ trỏ thẳng tới đích.

Sửa: nối lại tương tác cửa, marker từng đoạn, tick tuyến, thoại bộ đàm khi vẫn điều khiển được, pause/cancel âm thanh. Nghiệm thu: đứng gần cửa chưa tính xong; nhìn mặt chốt rồi E mới được; đi tắt tới bến không hoàn thành; lưu giữa tuyến giữ số đoạn; không yêu cầu giết tuần tra.

### Q4 — Cảnh làm lành/ngủ/đắp áo Đêm 2 có code nhưng không phát

**Xác nhận trước sửa.** `Map02ChapterPerformance.cs:56–73,100–155` có các nhịp; `Director.BeginScene:336–349`, `Speak:351`, `LateUpdate:371–389` chưa gọi. Cần nối cả tạo, lấy mẫu, đoạn im lặng cuối, camera và hoàn trả animator.

Sửa: nối lại lớp diễn xuất hiện hữu; khôi phục menu xem diễn xuất bị mất trong `Editor/Map02RoutePreview.cs`. Đây là khôi phục chức năng, **chưa nghiệm thu thẩm mỹ lớp pose thủ tục/áo khoác hiện có**. Nghiệm thu: phản ứng câu “vướng”, đổi ca, Hùng ngủ thấp sát sàn, áo không che mặt; skip/cancel trả controller, vị trí, avatar và input.

### Q5 — Test tua cảnh cuối chờ tiếng thoại theo thời gian thật

Ba lỗi ban đầu của `Map02FinaleTests` nằm ở việc không tới cue sau trong vòng lặp tua đồng hồ. Helper `Frame:32` tăng thời gian cảnh liên tục trong cùng frame, trong khi `Director.TickScene:363` chờ âm thanh phát xong. Sửa helper kiểm thử để giải phóng chờ audio khi tua; vẫn giữ thời lượng câu đã thu và các cổng thời gian. **Không rút thoại hoặc đổi nhịp game để chiều theo test.** Lượt chơi/thu hình thời gian thật cần kiểm riêng.

## 4. Đối chiếu theo 3 ngày–3 đêm

| Giai đoạn / mục tiêu | Đối chiếu kịch bản | Việc tiếp theo, theo thứ tự |
|---|---|---|
| **Ngày 1**, D1_01–06, B021–059; đích 18–22 phút | Mật khẩu → dìu/thay băng → thuốc/kho/bến tự do → báo cáo. C1 đã duyệt. Dev còn bữa cơm/đạo cụ bản cũ; các bản chi tiết hơn nằm cục bộ. | Bảo toàn C1; chọn tiếp nhận đúng file cục bộ, không dựng lại. Kiểm Nam cất súng/đeo ba lô khi dìu, Lành tiếp cận rồi mới chạm băng, Tí nhìn máy đàm, hòm đặt xuống rồi tay mới rời. Dọn hướng dẫn/minigame, đo lại thời lượng ×1. |
| **Đêm 1**, N1_01–04, B060–101; 12–14 phút | “Lời hứa” ở hiên, Lành nói từ bếp, tay trái Hùng, vịt/chộp vai, hai chốt, Hùng chưa ngủ được. | Đối chiếu demo/bản cục bộ đã duyệt; kiểm cue đũa/bát/đẩy nồi/hích vai theo tiếng. Không nhập nhầm bản ghế đẩu cũ đè bản ngồi đã làm. Giữ im lặng 3 giây sau lời hứa; phân biệt đêm này trằn trọc với Đêm 2 ngủ được. |
| **Ngày 2**, D2_01–07, B102–137; 20–25 phút | Nhận lệnh/câu “vướng”; hai lối trống–tre; vết máu/Tâm, 3 tuần tra, không bắt buộc giết; lấy dây; leo dừa dựng ăng-ten; liên lạc pháo. | Q1 và Q4 trước. Sau đó kiểm cả hai đường bằng collision thực; không chỉ NavMesh. Làm reaction thân/ánh nhìn của Hùng thay vì chỉ xoay đầu. **Cách dìu Tâm cần chốt lại** vì code hiện có thiết kế khác Word. |
| **Đêm 2**, N2_01–05, B138–174; 10–12 phút | Quan sát → bộ đàm → chốt cửa kho → đi cả tuyến sơ tán → làm lành, ngủ, đắp áo; xích xe xa. | Q2/Q3/Q4. Sau sửa quay một lượt hoàn chỉnh không bắn, không teleport. Lời “đèn qua rồi” phải theo đèn thật. Kiểm tiếng xích tách khỏi radio, và việc áo xuất hiện có tay nhận/đặt. |
| **Ngày 3**, D3_01–06, B175–211; 12–15 phút | 2 chuyến vật tư; cất bộc phá ở đúng hốc; vá 2 xuồng; phân công 3 nhà; cơm chiều; nhận lệnh. | Logic kho/chuyển thùng đã có. Làm pickup/place hai đầu chuyến, chuyển trọng tâm khi mang, ngồi vá có điểm chạm, Hùng nhặt rau bằng tay trái/Nam bổ củi. Cải thiện ho của Sáu phải được đọc ra nhờ thuốc, không chỉ lời kể. |
| **Đêm 3 nhịp 1–2**, N3_00–05C, B221–238 | Vừa nằm → pháo sáng; nhà cháy; Sáu/Lành/Tí thoát; xuồng 1/2; ba tuyến lùi. | Kiểm cảnh đen/pháo sáng vẫn vào được khi skip/load. Thêm động tác cửa/đỡ người và hướng đi dân rõ ràng. Điểm lùi phải đọc được bằng bố cục/ánh sáng; không chỉ ghim vàng. Nồi canh cháy chỉ thoáng qua trên đường lùi. |
| **Đêm 3 nhịp 3–4**, N3_WOUND/CRAWL/06, B239–253 | Xe tăng bắn sập mép tường; Nam bị thương vai trái, bò theo input, Lực kéo/băng, Tâm yểm trợ; pháo chỉ bắn ngoài làng. | Ưu tiên clip Nam ngã sấp → bò → được kéo → ngồi, tay khỏe chịu lực. Liên kết pose bò với tiến độ C thay vì dùng người đứng dịch trên đất. Dọn cây có tay/điểm tựa; kiểm hướng tháp pháo/quãng xe rút; sửa gọi pháo sau khi chốt người nói. |
| **Đêm 3 nhịp 5**, N3_07–09, B257–280 | Xuồng cuối bị ghim; Hùng kéo Nam vào hốc, lấy bộc phá tay trái, trao đài, Nam níu áo; bò mương; đặt xích; rút vài mét, trúng đạn, 1 giây rồi nổ. | Bộ clip tương tác ưu tiên cao nhất sau Q1–4. Cần điểm chạm rõ, một chủ đạo cụ mỗi thời điểm; quay từ sau vai Nam/silhouette. N3_08 giữ bất tử và 60–90 giây, không gán kết cục cho khả năng bắn. |
| **Đêm 3 nhịp 6**, cuối N3_09, B281–299 | Nam ngã ngửa rồi ngồi lại, radio trong lòng; pháo ngoài làng, dân quân tràn qua, Nam không chiến đấu. | Gán đủ pose bị hất/ngồi máy và handoff đài; tránh chuyển từ đứng idle sang cảnh đang ngồi bằng lời. Chặn điều khiển/nhạc theo đúng giai đoạn. So sánh âm thanh thời gian thật; cảnh tua test không đủ nghiệm thu mix âm. |
| **Bình minh**, DAWN, B300–324; khoảng 2 phút | Thi thể gần xe, phủ mặt; Nam/Thành/Lực ngồi, Lành đặt bát; Nam hạ súng; dân dọn xa, không ăn mừng. | Hiện thiếu nhiều asset/clip. Dựng layout hình và điểm chạm trước; một thi thể được phủ, không để biến mất hoàn toàn. Cử chỉ vai khỏe và đặt bát; shot trống bên Nam, nắng chỉ ấm 10 giây cuối. Đây là một cắt cảnh kết game, không thêm nhiệm vụ mới. |

### Móc câu chuyện phải kiểm xuyên ngày

| Gieo → trả | Kiểm nghiệm cụ thể |
|---|---|
| Hứa về quê Đêm 1 → vọng lại sau nổ → câu Nam bình minh | Nội dung/giọng cùng một ý, cue vọng riêng, không phát hai bản chồng nhau; không tiết lộ hy sinh trên HUD trước đó. |
| Thuốc Ngày 1 → Sáu đỡ ho Ngày 3 → cứu Đêm 3 | Quyền sở hữu thuốc phải chốt; trạng thái trao chỉ một lần; không làm Sáu vẫn diễn đau như chưa có chuyển biến. |
| Sửa bến Ngày 1 → vá xuồng Ngày 3 → sơ tán | Dùng cùng nơi/vật; vật liệu sửa có kết quả nhìn thấy, collision đi được; không dùng ghim ở một bến khác. |
| Kho Ngày 1 → chuyển vật tư Ngày 3 → lấy đồ Đêm 3 | Ghi sổ số lượng nguồn/đích; không nhân đôi sau load, không tặng thêm vô điều kiện. Con số đang có phải giữ tới khi được duyệt cân bằng. |
| Dây/đài Ngày 2 → hướng dẫn Đêm 2 → trao đài Đêm 3 | Cùng bộ đạo cụ nhận diện được; mốc trao có tay chạm; skip/load đài theo Nam, không vẫn ở lưng Hùng. |
| Kiểm mương Đêm 2 → Hùng bò Đêm 3 | Đối chiếu đường mương thật trên map; hiện fallback `BuildCanal` sinh một cung đường, chưa chứng minh đó chính là tuyến người chơi đã kiểm. |
| Hùng đau tay phải → cơm/rau/đài/bộc phá | Kiểm **mọi idle, walk, transition và clip tái dùng**, không chỉ cảnh chính; tay phải không vô tình chịu lực hoặc giơ súng. |
| Nam được dẫn về nhà → ghế trực trống | Không để Hùng nói câu nền hoặc xuất hiện lại sau hy sinh; dân/Thành không diễn ăn mừng. |

## 5. Animation: danh mục công việc có thể giao

| Gói | Chuỗi cần xem/làm | Dùng lại được | Chứng cứ thiếu / tiêu chí |
|---|---|---|---|
| A0 | C1 hụt chân, Nam đỡ | V4 đã duyệt/tích hợp | Giữ nguồn, không quay về demo cũ. Chỉ xử lý cờ còn lại khi có lượt polish riêng. |
| A1 | Dìu Hùng, thay băng, trao thuốc, hòm, bến | Nhóm A/B cục bộ đã có file `.blend`, mocap và review | Kiểm đúng bản/asset và trạng thái duyệt trước khi gộp. Tự phê bình tay–vai–áo–sàn và quyền điều khiển sau cảnh. |
| A2 | Cơm “Lời hứa”, vịt/chộp vai, nằm chưa ngủ | Bản cục bộ; dev có Meal/Rest cũ | Cần xem liên tục và so nguồn. Các động tác người–người phải đồng bộ hai nhân vật, không ghép hai clip độc lập rồi ép cổ tay. |
| A3 | Hùng khựng lại, quay về đài; Nam ngập ngừng | Lớp `Map02ChapterPerformance.cs:79–96` | Khôi phục trước; sau đó kiểm tư thế/nhịp bằng Blender. Chuyển trọng tâm và vai phải cùng phản ứng, không chỉ đầu. |
| A4 | Leo/cột/xuống dừa + Hùng hướng dẫn | Cơ chế ở `Map02ChapterDayTwo.cs:174–195` | Đây còn là pose thủ tục dự phòng. Dựng tay/chân bám đúng điểm; dừng W không trượt; buộc dây không làm hai tay xuyên nhau. |
| A5 | Đổi ca → nằm nghiêng ôm tay phải → ngủ → Nam cởi/đắp áo | Code thủ tục ở `Performance.cs:112–155` | Các slot clip mới trong `ChapterLayout.cs:21` chưa được gán ở asset. Cần clip gốc/đạo cụ áo phù hợp; không coi mặt phẳng áo hiện tại là hoàn thiện. |
| A6 | Ngày 3 rau/củi/vá xuồng/giúp Sáu xuống bậc | Bến/kho hiện có, có thể tái dùng một số nhịp | Phải kiểm vết thương đúng bên; Nam mang vật có nhịp nhấc/đặt và chân chịu tải. Không tự thêm minigame. |
| A7 | Nam trúng pháo, ngã sấp, bò, Lực kéo/băng, đứng dọn cây | Cắt dựng cho đoạn kéo được kịch bản cho phép | `ChapterLayout.asset:116` namProne trống; `Director:265` đang di chuyển bằng C. Cần kiểm **pose**, không chỉ root đi. |
| A8 | Hùng kéo Nam → lấy bộc phá trái → trao đài → gỡ tay níu | Mô hình bộc phá đã gán; radio hiện là slot dự phòng | Choreography hai người + prop chuyển chủ; tay khỏe Nam chống đỡ. `Finale.cs:164–175` mới gắn radio theo root, không phải quá trình trao. |
| A9 | Bò mương → đứng chạy → đặt xích → rút → trúng đạn | `hungFallClip` đã gán | `hungPlantRetreatClip` trống (`asset:111`); `Cover:67` mới gọi SceneMove. Kiểm chân, tay trái, vị trí bộc phá, 1 giây ngã–nổ. |
| A10 | Nam hất ngửa → ngồi máy đàm; dân quân chạy | Khung thời gian/xe tăng/hiệu ứng đã có | `asset:117–118` clip Nam trống. Dân quân có thể tái dùng Lực, không cần model mới; tốc độ bước phải khớp root. |
| A11 | Thi thể phủ, ngồi tưởng niệm, đặt bát, chạm vai, hạ súng | Có logic DAWN và model NPC | `asset:109–110,114` trống. Chưa nghiệm thu hình. Nhìn xa vẫn đọc được mất mát; không cần cận mặt hoặc thêm xương mặt. |

### Quy trình bắt buộc cho mỗi gói mới

1. Chọn intent và tham chiếu động tác, kiểm rig/clip thật; không coi một kết quả tìm kiếm Mixamo là asset đã tải hoặc phù hợp.
2. File `.blend` riêng theo ngày/phiên bản. Giữ rig hiện hữu; nếu cần rig/clip ngoài phạm vi đã cho phép, báo phương án/ảnh hưởng trước.
3. Blocking với môi trường đo từ map, đánh dấu khung chạm/nhấc/thả; người dùng duyệt hướng diễn xuất.
4. Spline/polish: hông/vai dẫn, tay có cung và chậm dần, ngón theo kiểu cầm thật; mỗi prop chỉ có một chủ. Không xoay cẳng tay bù lỗi vị trí vai.
5. Render 1080p, nhiều góc + cận khung lỗi; bảng khung–khớp–độ/xuyên mm, trượt chân theo điểm tiếp xúc. Ngưỡng nghiệm thu cụ thể phải chốt theo bộ đo/clip, không dùng một con số chung giả bảo đảm sạch mọi lưới.
6. Sau duyệt mới export; kiểm Unity cả vào cảnh, bỏ qua, hủy, load và trả gameplay. Clip 60 fps/render 60 fps **không chứng minh game 60 fps**.

## 6. Các khác biệt cần chốt, không tự sửa như bug

| Mã | Word / hiện trạng | Đề xuất để duyệt |
|---|---|---|
| D01 — Tâm | B106/B131 muốn dìu vai, Nam chỉ cầm lục, về cổng. `Guidance.cs:62–65` ghi rõ thiết kế Tài được chọn 23/10: Tâm đi theo, Nam tự do trang bị/tốc độ; `Director:413` trả về khu Thành. | **Giữ quyết định hiện có trong lượt sửa nhanh.** Chỉ đổi sang dìu vai/lục khi Thịnh chốt lại; cần thêm clip và khóa trang bị tương ứng. |
| D02 — Thuốc | B025 nói thuốc từ Map 1. `Mission.Inventory.cs:28,39` tạo gói thuốc nhiệm vụ ảo theo mask; `DayOneState.cs:74` đánh dấu trao, không trừ medkit Map 1. Bảng Claude cũng đang để mở việc này. | Chọn thuốc nhiệm vụ riêng hay vật phẩm thật chuyển map. Chỉ sau lựa chọn mới thiết kế chống thiếu thuốc/khóa nhiệm vụ và migration save. |
| D03 — Người gọi pháo | B252/B282 là nhân viên vô tuyến. Dữ liệu/test dev chủ đích dùng Anh Thành qua bộ đàm (`Map02ChapterTests.cs:50–53`). | Xác nhận bản thoại muốn lấy làm chuẩn; không tự đổi speaker rồi dùng nhầm giọng đã thu. |
| D04 — Vai Nam | B014 ghi thương vai là chi tiết mới cần chốt; kịch bản sau dùng vai trái, `State.wounded` là cờ toàn thân. | Dùng vai trái như Word nếu được duyệt; cần lớp đau/reload/chống tay nhất quán. Giữ số liệu .7 tốc độ/1.35 nạp hiện có trong lượt này. |
| D05 — Thời lượng | B335 tổng 97–118 phút. Bảng Claude đo bot ×1 ngày 25/10: D1 7,2 phút, N1 4,2 phút. | Đo lại bản hợp nhất. Nếu vẫn ngắn, chọn tăng hoạt động/cảnh phụ có ý nghĩa hoặc sửa mục tiêu thời lượng; không kéo lê tốc độ hay tăng thời gian chờ để đủ phút. |
| D06 — Đích chất lượng | RTX 3050 laptop, 1920×1080/60 fps là mục tiêu đã nêu. Chưa có phép đo build đại diện cho cả chương. | Chốt preset/nguồn điện/CPU/RAM khi làm benchmark. Chưa tăng map, texture hay quân số. |
| D07 — Âm thanh/giọng | Bảng Claude còn duyệt nhạc/echo/58 mục dùng giọng đề xuất (56 ID gốc); dev có các lần đồng bộ giọng khác. | **Đã chốt giữ giọng dev**, lưu bộ Claude ngoài Resources để duyệt sau. Khôi phục đủ 261 mục catalog, không chỉ danh sách Ngày 1. Nhạc/echo cục bộ vẫn chờ nghe riêng. |

## 7. Kế hoạch triển khai theo cổng nghiệm thu

| Đợt | Công việc | Phụ thuộc / file chính | Xong khi |
|---|---|---|---|
| **0 — sửa nhanh đang thực hiện** | Q1–Q4, tooling Q5; bảo toàn việc cục bộ | Director, Enemy, Guidance, Cinematics, RoutePreview, FinaleTests | Các ca lỗi trước sửa qua lại; không mất C1/Day1/Day3; ghi rõ nhánh/thư mục thực có bản sửa. |
| **1 — ổn định bản làm việc** | So sánh dev với Day1Night1 cục bộ; ghép đúng gói đã duyệt, tránh ghi đè giọng | Diff/GUID/manifest, bảng Claude, backup | Project biên dịch; đủ nguồn/asset/meta; WIP được giữ, danh sách duyệt rõ. Không merge/push ngoài phạm vi cho phép. |
| **2 — lượt chơi Ngày 1–Đêm 2** | Chơi liên tục, kiểm hai đường Ngày 2, đèn/kiểm cửa/tuyến Đêm 2, đo ×1 | Bộ test DayOne/DayTwo/NightTwo và A1–A5 | Không chỉ dùng teleport/Complete để chứng minh hoàn thành; lưu giữa tuyến, skip và đầu vào thật đều đúng. |
| **3 — định vị Ngày 3–Đêm 3** | Đo hốc/cây đổ/mương/xích/tuyến xuồng và vùng pháo; bảng prop ownership | ChapterLayout, Dressing, Tank, Cover; A6–A9 | Vị trí được chụp/đo; bộc phá đúng hốc và xích, đường Hùng đọc được, không xuyên bờ/xe. |
| **4 — ba demo cảm xúc** | (a) đêm ngủ/áo; (b) bị thương/chia tay/đặt bộc phá; (c) bình minh | Blender working copies, rig hiện tại, A5/A7–A11 | Blocking rồi polish đều có duyệt; chuyển động mượt và chạm thật; không tự sửa rig. |
| **5 — Unity và save** | Gắn clip/prop theo sự kiện, cleanup mọi đường thoát | Director, Cinematics, Performance, Finale, State | Chơi/skip/load có cùng kết quả; Hùng không sống lại; không mất súng/input; không nhân đôi đạo cụ/đạn. |
| **6 — âm thanh/nhịp/hiệu năng** | Mix theo nhịp, benchmark build, QA lại chương | Soundscape/voice catalog, Profiler, test + playthrough | Không lỗi Console lặp; báo median/P95/P99 CPU/GPU, RAM/VRAM; ảnh/video thật có giới hạn ghi rõ; các quyết định D01–D07 được chốt. |

Không ấn định số ngày làm khi chưa duyệt số clip và xem chất lượng nguồn. Đợt 0 là khôi phục code; A7–A11 là các gói animation đáng kể, không gộp thành một câu “sửa tay”.

## 8. Test và chứng cứ phải lưu

### Lượt rà soát ban đầu trên dev sạch

**26 ca: 17 đạt, 9 lỗi, 0 bỏ qua.** [XML trước sửa](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/Map02_Audit_20261026/before-tests.xml). Gồm cơ chế, tuyến/diễn xuất Ngày 2–Đêm 2, kết thúc và vật tư/cast. Hai ca explicit trong cohort là thu hình/probe, không phải khẳng định hình đẹp hay FPS đạt. Unity đã ghi XML nhưng trả mã crash khi thoát; phải ghi riêng tình trạng tiến trình, không che bằng số test.

**Sau sửa nhanh trên dev riêng:** 27/27 ca riêng biệt đạt ở lượt mới nhất của từng ca, gồm 26 ca ban đầu và một ca preview bổ sung. Đây là tổng hợp các lượt kiểm/chạy lại, không phải một lượt sạch chạy cả 27. Dự án chính đã kiểm riêng với phần Claude và lựa chọn giọng: 83/83 ca tích hợp đạt, Unity exit 0. Không dùng số 72/72 ngày 25/10 làm số cho code hôm nay.

**Catalog dự án chính:** 8/8 ca đạt, Unity exit 0; [XML](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/Map02_Audit_20261026/main-catalog.xml). Đã bắt và sửa việc bản cục bộ thiếu 134 mục thoại của dev; test mới kiểm mọi câu có tiếng trong bảy giai đoạn. Giữ toàn bộ casting/metadata dev ngoài chỉnh sửa echo cục bộ đã ghi riêng. Lượt tích hợp có chơi/input/cleanup đạt **83/83**, Unity exit 0; tổng hai lượt là **87 ca riêng biệt đạt** (bốn ca trùng). [Tổng hợp từng ca](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/Map02_Audit_20261026/main-test-summary.json). Luồng đầu vào thật đã hoàn tất đủ 10 mục tiêu Ngày 1–Đêm 1 và sang Ngày 2; [nhật ký](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/Map02_Audit_20261026/main-player-input-steps.txt). Các runner đã dừng không tính là đạt.

### Ma trận bắt buộc khi hoàn thiện

| Nhóm | Tình huống |
|---|---|
| Nhiệm vụ | 43 ID duy nhất; prerequisite; ba việc D1 đổi thứ tự; D3 tuần tự; báo cáo không bị hoàn tất hai lần. |
| Di chuyển | Hai đường Ngày 2 đi về bằng CharacterController thật; hộ tống không kéo qua tường; cầu/ruộng/mương/bến không mắc collision. |
| Ăng-ten | Không input không xong; W/S/dừng; hướng sai không cột; căn chưa cột không xong; xuống mới trả thoại; Esc/hủy/load an toàn. |
| Đêm 2 | Ba đèn thật; tường/khói/cúi/bò; cảnh báo lần 1 dừng soi, lần 2 về checkpoint cầu; cửa nhìn đúng hướng; bốn đoạn theo thứ tự; có thể đi hết không chiến đấu. |
| Diễn xuất | Phản ứng câu “vướng”; chuyển đổi sleep/awake giữa hai đêm; áo không che mặt; vai thương đúng bên trong mọi idle/transitions. |
| Vật tư | Thuốc/carry/đạn/kho không nhân đôi sau load; nhặt xác một lần; lượng trả ở Đêm 3 đúng sổ; không tự bù đạn khi skip cảnh. |
| Đêm 3 | Điểm lùi 70/80/90; không hoàn tất khi còn lính chặn; cover 60–90 dù không bắn, bắn ghim hay hạ hết; Nam không chết riêng đoạn đã quy định. |
| Kết cục | Đặt bộc phá → rút → trúng đạn → một giây → nổ; radio chuyển chủ; thử skip trước/sau từng mốc; tải sau hy sinh không hồi sinh/bark Hùng. |
| Trả điều khiển | Controller/avatar/layer/input/camera/weapon/renderer được trả sau finish, cancel, skip, load, đổi scene; không spam Animator warning. |
| Âm thanh | Thoại khớp speaker/text; pause đúng; không hai câu radio đè nhau; echo đúng cue; nhạc ngắt đúng nhịp; kiểm ở thời gian thật. |
| Hiệu năng | Build 1080p trên RTX 3050: bến ban ngày, đèn Đêm 2, giao tranh ngõ 3, cháy/khói/xe, pháo+dân quân; warm-up rồi đo nhiều lượt có cùng tuyến. |

## 9. Hướng tối ưu giữ hình ảnh vừa phải

- Ngân sách một khung 60 fps là **16,67 ms**, đây là mục tiêu toán học, chưa phải kết quả đo. Ghi preset URP, render scale, bóng, AA, vsync/cap và trạng thái nguồn điện khi đo.
- Ưu tiên clip bake và point light có giới hạn; không thêm mô phỏng vải/physics nhiều nhân vật để chỉ kể một cảnh. Cảnh đỡ/kéo có thể cắt dựng như Word cho phép.
- Kiểm chi phí `PerformanceActor.Ground` bake lưới (`Performance.cs:36–41`), các pose/IK cập nhật mỗi khung và cấp phát mesh đèn (`NightPatrol.cs:117–122`) bằng Profiler **sau khi được nối lại**. Chỉ tối ưu chỗ thực sự nóng, giữ nguyên điểm chạm.
- Tái dùng dân quân/NPC/props; chi tiết tập trung trong shot. LOD/culling cho đám nền; nhân vật đang tương tác phải giữ animation hoạt động khi camera đổi góc.
- Đừng coi video MP4 1080p60 hoặc test `PerformanceProbe` không assert là chứng nhận 60 fps. Cần build đo CPU/GPU, percentile và các đoạn rớt khung.

## 10. Tài liệu đã tham khảo và giới hạn

| Nguồn | Đã tiếp cận | Áp dụng cho kế hoạch |
|---|---|---|
| [Unity 6.3 — Root Motion](https://docs.unity.com/en-us/engine/6000.3/manual/animation-section/animation-mecanim/avatar-creationand-setup/root-motion) | Đã đọc trang manual | Tách root đi theo đường với pose clip; kiểm bake Y/XZ và điểm chân khi chuyển nằm/ngồi. Không để cả code lẫn clip cùng kéo root. |
| [Unity Animation Rigging — Two Bone IK 1.3](https://docs.unity.cn/Packages/com.unity.animation.rigging@1.3/manual/constraints/TwoBoneIKConstraint.html) | Đã đọc tài liệu/search extract chính thức | Target bàn tay và hint khuỷu, blend weight khi chạm/thả. Đây là nguyên lý tham khảo, **không tự cài package hoặc đổi rig**. |
| [Blender — Child Of](https://docs.blender.org/manual/en/latest/animation/constraints/relationship/child_of.html) | Đã đọc bản trích xuất nội dung manual; lần mở trực tiếp bị lỗi công cụ | Chuyển chủ áo/bát/đài/bộc phá; giữ transform lúc đổi chỗ bám. Cần kiểm animation thật trên bản sao. |
| [GDC — Motion Matching in The Last of Us Part II](https://gdcvault.com/play/1027118/Motion-Matching-in-The-Last) | Chỉ xác minh trang tìm kiếm; mở bị chuyển sang bảo trì, **chưa xem video** | Chỉ là nguồn cần học tiếp, không rút kết luận về kỹ thuật hay timecode, không đề xuất chuyển ShadowVale sang motion matching chỉ vì tên AAA. |
| [Skill character-animation](C:/Users/Admin/.codex/skills/character-animation/SKILL.md) và các mục 6,10,15–17 | Đã đọc phần quy trình game, QA, khớp/tay/tương tác | Blocking → duyệt → polish; giữ xương; tay bám có lực; tự chê và đo. Các video liệt kê trong skill không có nghĩa đã xem lại ở lượt này. |

**Chưa làm trong lượt rà soát:** chưa chấm thẩm mỹ tất cả 43 mục tiêu bằng video thời gian thật; chưa đo toàn chương 97–118 phút; chưa benchmark build đủ cảnh; chưa xác nhận mọi nguồn/mocap của Claude đã được duyệt. Các hạng mục này nằm trong công việc tiếp theo, không được đánh dấu hoàn tất nhờ test state.


**Đối chiếu Git sau giao bản:** commit code `be7ea870` đã push thành công lên cả `thinh` và `dev`, hai remote cùng mã sau lần push; nhánh local vẫn là `thinh`, local `dev` đã đồng bộ. [Biên nhận tích hợp](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/Map02_Audit_20261026/PUSH_RECEIPT.json). Commit tiếp theo chỉ ghi tài liệu kết quả này. Các thay đổi cục bộ ngoài phạm vi vẫn được giữ trên máy.
