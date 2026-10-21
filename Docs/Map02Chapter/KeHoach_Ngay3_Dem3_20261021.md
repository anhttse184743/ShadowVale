# Kế hoạch triển khai Ngày 3 – Đêm 3 theo kịch bản mới — 21/10/2026 (bản đã rà soát)

Nguồn kịch bản: *ShadowVale Map 2 – Kịch bản chi tiết 3 ngày đêm* (bản sau 20/10). Đối chiếu với `Map02Chapter.json` v3, `Map02ChapterDirector*.cs`, `Map02ChapterEnemy.cs`, `Map02ChapterState.cs`.

Phạm vi: Ngày 3 (phase 4) và Đêm 3 (phase 5). Bình minh chỉ được đụng tới ở những chỗ Đêm 3 bàn giao trạng thái sang. Ngày 1, Ngày 2 và Đêm 2 chỉ sửa những dòng mà Đêm 3 dựa vào (mục 3, GĐ1).

Quy ước: **[KB]** là bám đúng kịch bản. **[DIỄN GIẢI]** là kịch bản chưa nói rõ, đây là cách hiểu đề xuất. **[LỆCH]** là cố ý làm khác kịch bản, cần tác giả đồng ý.

---

## 1. Hướng chọn

**Giữ khung 43 bước và toàn bộ ID nhiệm vụ. Sửa logic và thoại trước. Dựng sẵn chỗ để gắn đồ họa và âm thanh, để nhóm art làm song song.**

1. **Save và test bám vào ID.** `Map02ChapterState.Validate()` từ chối save có ID lạ hoặc sai thứ tự `requires`. `Map02M16SaveCompatibilityTests`, `Map02ChapterTests`, `Map02ChapterMechanicsTests` và `Map02FlowMovieTests` đều gọi thẳng `D3_06`, `N3_07`, `N3_08`, `N3_09`.
2. **Khung nhiệm vụ của kịch bản khớp với khung hiện tại:** M2_D3_01…06 ứng với D3_01…06; M2_N3_01…09 ứng với N3_01…N3_09, trong đó M2_N3_05 được tách thành 05A/B/C + WOUND + CRAWL. Phần lệch nằm ở thứ tự thoại, vị trí xe tăng và dàn cảnh.
3. **Đồ họa là phần đi chậm nhất.** Mở rộng `Map02ChapterLayout` ngay từ đầu để nhóm art làm song song, còn code chạy được với hình tạm.
4. **Không thêm trường mới vào save, trừ khi bắt buộc.** Trạng thái xe tăng, thuyền và máy đàm suy ra được từ danh sách nhiệm vụ đã xong.

---

## 2. Các quyết định thiết kế (chờ duyệt)

| # | Vấn đề | Đề xuất | Loại |
|---|---|---|---|
| Q1 | D3_06 "Báo cáo anh Thành" sau khi lệnh giao ngõ đình đã nằm trong cảnh Bữa chiều | Giữ D3_06 là **nhiệm vụ riêng** như kịch bản liệt kê, nhưng chuyển ghim sang **sân nhà chị Lành**, nơi anh Thành vừa bước vào. Nhấn E là mờ màn hình sang Đêm 3. Không có thoại mới vì kịch bản không viết | [KB] khung nhiệm vụ, [DIỄN GIẢI] vị trí |
| Q2 | Giữ ngõ "không tính giờ" | Bỏ đồng hồ giây. % sơ tán **tăng khi Nam đứng trong vùng 12 m**, tăng ở 40% tốc độ khi còn lính sống. Đợt lính thứ hai xuất hiện ở giữa chặng. Muốn qua chặng phải đạt mốc **và** không còn lính sống. HUD chỉ hiện % và số lính | [DIỄN GIẢI] |
| Q3 | Xe tăng | Chạy theo **đường định sẵn**, không có AI. Chỉ **xuất hiện ở Nhịp 3 và Nhịp 5**, như bảng Địch của kịch bản. Giữa hai lần thì khuất. Trạng thái suy ra từ nhiệm vụ đã xong | [KB] |
| Q4 | Thuyền | Hai chuyến đầu và chuyến cuối đều dùng **thuyền chạy theo kịch bản** (kinematic) và **cùng model với hai chiếc Nam đã vá ở Ngày 3**. Thuyền vật lý đang neo được thay bằng bản chạy theo kịch bản từ Đêm 3 | [KB] (cài – trả) + [DIỄN GIẢI] kỹ thuật |
| Q5 | Quay chậm sau tiếng nổ | Không đổi `Time.timeScale` toàn cục. Làm chậm riêng từng thứ trong cảnh (thuyền, khói, camera) và lọc âm | [DIỄN GIẢI] kỹ thuật |
| Q6 | Giọng Hùng vọng lại | Dùng **tệp riêng** câu "Đợi tới lúc đó, cậu ra nhà tôi…" mà kịch bản Đêm 1 yêu cầu ghi riêng ("dùng lại trong đoạn hồi tưởng ở Nhịp 5 và 6"). Dòng thoại trỏ tới tệp này qua trường mới `recording` | [KB] |
| Q7 | Thoại qua đàm và tiếng hét trong lúc chơi | Thêm **câu nói nền**: phụ đề ở góc, không khóa điều khiển | [KB] (kịch bản ghi "qua đàm" và "trong lúc chơi") |
| Q8 | Cảnh then chốt đang phải đi tới ghim rồi nhấn E | `N3_WOUND` chạy ngay khi đạt 90%. `N3_07` chạy khi Nam vào vùng bờ đất cạnh bến. `N3_09` chạy khi Hùng tới xe tăng | [KB] (các cảnh này xảy ra *với* Nam) |
| Q9 | Tên đơn vị pháo | "Rạch Tre" ở mọi nơi. Sửa thêm các dòng gọi máy ở Ngày 2 | [KB] |
| Q10 | Độ dài đoạn yểm trợ | Hùng tới xe tăng sau **60–90 giây** tùy mức áp chế. Nếu tác giả muốn "tốp lính bị áp chế thì chạy tiếp ngay" kể cả trước 60 giây, chỉ cần hạ mức sàn | [DIỄN GIẢI], câu chữ kịch bản mơ hồ |

---

## 3. Lộ trình

Kích cỡ ước lượng cho một lập trình viên: **S** ≤ nửa ngày, **M** khoảng 1 ngày, **L** khoảng 2 ngày. Làm theo thứ tự GĐ1 → GĐ8. Gửi danh sách ở mục 5 cho nhóm art ngay khi bắt đầu GĐ1.

### GĐ1 — Thoại và dữ liệu (S)

Tệp: `Assets/_Project/Map01/Resources/Story/Map02Chapter.json`, `Docs/Map02Chapter/Dialogue_Review.csv`.

> **Trạng thái 21/10 (nhánh `feat/map2-day3-night3`):** đã làm phần chạy được ngay với code hiện tại.
> - Ngày 3: `demolition_02`, `lastorder_*` gộp vào Bữa chiều, `D3_06` về sân nhà chị Lành. `D3_05` = 54 giây, tính theo thời gian đọc 12 dòng cộng 6 giây kết, thay cho con số ước lượng 80.
> - Đêm 3: tiêu đề `N3_02` và `N3_08`, `coordinates_01` và `_04`, `farewell_03`, `sacrifice_03`.
> - Ngày 2: `antenna_02` và `_03`, `artillery_01`.
> - Code: cú nháy trắng gắn theo ID `m2v3_wound_04`. Anh Thành đứng ở sân nhà chị Lành từ lúc bắt đầu Bữa chiều tới khi báo cáo, kể cả khi tải save. Bản đi bộ vào sân để GĐ2.
> - Test: thêm `DayThreeAndNightThreeFollowTheRevisedScript`. CSV đã tạo lại; 15 dòng đổi nội dung đều ở `pending_approval`.
>
> **Dời sang giai đoạn khác**, vì cần cơ chế chưa có. Làm ngay bây giờ thì các dòng này sẽ không phát được:
> - Sang GĐ2 (cần câu nói nền và trường `recording`): `boatsafe_01` thành câu nói nền và bỏ `scene` của `N3_04`; `wound_01`/`_02` sang `m2v3_fallback` và bỏ `_03`; `sacrifice_01` thành câu nói nền; `recording` của `sacrifice_02`.
> - Sang GĐ6 (khi có trục thời gian mới): `N3_09` `seconds` = 70.

**Ngày 3**

| Dòng / nhiệm vụ | Thay đổi |
|---|---|
| `m2v3_demolition_02` (Hùng "To cỡ nào hả anh?") | `radio: false`. Hùng có mặt, nhìn gói vải |
| `m2v3_lastorder_01…07` | Đổi `conversation` → `m2v3_supper`, nối sau `supper_05`. `_05` và `_07` chuyển thành `radio: false` |
| `D3_05` | `seconds` 45 → khoảng 80 (cảnh gộp dài hơn) |
| `D3_06` | `site: "family"`, `scene: ""`, tiêu đề giữ "Báo cáo anh Thành" (Q1). Cảnh rỗng thì bước tự hoàn thành, code hiện tại đã xử lý được |

**Đêm 3**

| Dòng / nhiệm vụ | Thay đổi |
|---|---|
| `N3_02` | Tiêu đề "Phá cửa sau, đưa ông Sáu và chị Lành ra ngõ" |
| `m2v3_boatsafe_01` (Hùng "Chuyến một đi rồi…") | Thành câu nói nền **khi chuyến 1 rời bến** (khoảng 30% của `N3_04`), không còn là cảnh khóa điều khiển lúc 60%. `N3_04` bỏ `scene` |
| `m2v3_wound_01`, `_02` | Chuyển sang câu nói nền `m2v3_fallback`, phát **lúc bắt đầu Nhịp 3** (bắt đầu `N3_05A`). `_02` đổi thành "Lùi về gốc me! Nam, giữ ngõ cho dân qua!". Bỏ `_03` ("Rõ!") vì kịch bản không có |
| `m2v3_wound` còn lại | `_04` (anh Lực "Xe tăng! Nằm xuống!"), `_05` (Hùng gọi qua đàm). **Đồng thời sửa `DrawOverlay`:** cú nháy trắng đang gắn với `lineIndex==3`, phải chuyển sang gắn theo ID `m2v3_wound_04` |
| Tiếng Hùng gọi lặp lại khi bò | **Dùng lại `m2v3_wound_05`** làm câu nói nền, khoảng mỗi 4 giây trong `N3_CRAWL`. Không thêm dòng mới |
| `m2v3_coordinates_01` | "Rạch Tre báo: cần tọa độ cuối, dân phải ra khỏi bảy mươi thước quanh ngõ đình." |
| `m2v3_coordinates_04` | Người nói "Rạch Tre", câu "Rạch Tre nhận tọa độ. Ba phút." |
| `m2v3_farewell_03` | "Không sao… vết cũ thôi." |
| `N3_08` | Tiêu đề "Yểm trợ Hùng" |
| `m2v3_sacrifice_01` ("Đi đi! Tôi che cho!") | Thành câu nói nền mở đầu `N3_08` |
| `m2v3_sacrifice_02` | Đổi chữ thành "Đợi tới lúc đó, cậu ra nhà tôi. U tôi nấu canh cua rau đay…", thêm `"recording": "m2v3_echo_promise"` (tệp riêng, Q6) |
| `m2v3_sacrifice_03` | Người nói "Rạch Tre", câu "Rạch Tre. Bắn.", `radio: true` |
| `N3_09` | `seconds` 40 → khoảng 70 (Phần 3 khoảng 30 giây + Nhịp 6 khoảng 40 giây theo kịch bản) |

**Phụ thuộc ở các buổi khác (chỉ sửa dòng, không sửa cơ chế)**

| Dòng | Thay đổi | Lý do |
|---|---|---|
| `m2v3_antenna_02` | "Bông Súng gọi Rạch Tre. Bông Súng gọi Rạch Tre. Nghe rõ trả lời." | Cài tên Rạch Tre |
| `m2v3_antenna_03` | Người nói "Rạch Tre", câu "Rạch Tre nghe. Nói đi." | Cài tên Rạch Tre |
| `m2v3_artillery_01` | "Bên kia có pháo. Nhưng gọi tới lúc bắn được là mất mấy phút. Cần tọa độ, cần biết dân đã ra khỏi chỗ chưa." | Đây là câu cài trước cho "pháo không bắn vào đây được" ở Nhịp 4. Mục kiểm thử của kịch bản đo đúng điểm này |
| Đêm 1, tệp `m2v3_echo_promise` | Ghi riêng câu hứa (theo phần Âm thanh Đêm 1) | Nguồn cho Q6 |

Giữ nguyên "Đừng giành!" (chính tả đúng; bản docx gõ "dành").

Ảnh hưởng tới bản thu: cả 135 dòng trong `Dialogue_Review.csv` đều đang `pending_approval`, nên sửa chữ không làm mất bản thu nào. Sau khi sửa phải tạo lại CSV.

**Xong khi:** `Map02ChapterTests.EveryTaskHasValidDependenciesUniqueIdentityAndNoOrphanedDialogue` đạt. Không còn người nói "Đơn vị pháo". Có test mới kiểm tra: `lastorder_*` thuộc `m2v3_supper`, các dòng của Hùng trong cảnh này không có `radio`, cú nháy trắng gắn theo ID chứ không theo vị trí dòng.

### GĐ2 — Câu nói nền, cảnh tự chạy, dàn cảnh theo câu (M)

Tệp: `Map02ChapterDirector.cs`, `Map02DialogueCatalog.cs`, `Map02Mission.Hud.cs`.

> **Trạng thái 21/10:** đã làm trong file mới `Map02ChapterStaging.cs`.
> - Câu nói nền: hàng đợi, bỏ trùng, một cảnh bắt đầu thì xóa hàng đợi. Phụ đề ở `y=690`, không khóa input. `HasBark` dùng cho test.
> - Cảnh tự chạy (Q8), cả khi tải save ở giữa.
> - Trường `recording` / `RecordingId`.
> - **Diễn viên bước vào cảnh:** `SceneDriven` cho phép diễn viên đi trong lúc cảnh khóa input. Người ở xa xuất phát từ sau camera; câu của họ chờ tối đa 7 giây. Áp dụng cho anh Thành (Bữa chiều), Hùng (cất bộc phá, phân công, Bữa chiều), ông Sáu (phân công). Hùng tự về nhà ăn tối khi xong bốn việc.
> - Khoảng lặng 2 giây trước `lastorder_07`.
> - Câu hồi tưởng chỉ phát sau tiếng nổ (`EchoAt`). `wound_05` lặp lại khoảng mỗi 4 giây khi bò.
> - Dữ liệu dời từ GĐ1: `N3_04` bỏ `scene`; `wound_01`/`_02` sang `m2v3_fallback`; bỏ `wound_03` (còn 134 dòng); `sacrifice_01` sang `m2v3_cover`; `sacrifice_02` có `recording`.
> - Test: `WoundBankAndSacrificeRunWithoutPressingEAndRadioCallsKeepControl` (mới). Mechanics không còn `Interact(N3_09)`.
>
> Chưa làm: clip "ném cọng rau" (chờ asset, GĐ7).
>
> **Lưu ý test:** `RepairAndAntenna…` lỗi khi chạy thiếu nhóm `Map02ArrivalTests`, kể cả trên commit GĐ1. Đây là lỗi phụ thuộc thứ tự có từ trước, không phải hồi quy. Luôn so kết quả bằng bộ lọc đầy đủ.

1. **Câu nói nền** `Bark(string lineId)`: xếp hàng, có ưu tiên. Hiện phụ đề không chồng lên ghim, phát bản thu nếu đã duyệt, không khóa input. Cảnh khóa điều khiển luôn chặn câu nói nền. Dùng ở: chuyến 1 rời bến, đầu Nhịp 3, lúc bò, đầu `N3_08`, tiếng hét của Hùng lúc lao lên.
2. **Cảnh tự chạy (Q8):** `N3_WOUND` chạy khi đạt 90%. `N3_07` chạy khi Nam vào trong 6 m quanh `ditch`. `N3_09` chạy khi Hùng tới `ChargePoint`. Bỏ lời nhắc E ở ba ghim này.
3. **Trường `recording` của dòng thoại:** `HasApprovedRecording` và `Speak()` tra bản thu theo `recording ?? id`.
4. **Dàn cảnh theo từng câu** (bảng tra theo ID dòng, giống cách code đang xử lý riêng `N3_09`):
   - Bữa chiều: trước `lastorder_01` thì anh Thành `GoTo` sân, câu chờ anh Thành tới (tối đa 6 giây). Có khoảng lặng khoảng 2 giây trước `lastorder_07` ("nhìn Nam một lúc lâu").
   - Bữa chiều: ở `supper_05`, Hùng ném cọng rau về phía Nam (chỗ gắn clip).
   - `demolition`: Hùng đi tới hốc bờ đất, câu `_02` chờ Hùng tới (tối đa 6 giây).

**Xong khi:** Câu nói nền không làm `Busy` = true. `N3_09` chạy mà không cần `Interact`. Sửa `Map02ChapterMechanicsTests` để bỏ bước `Interact(N3_09)`.

### GĐ3 — Giữ ngõ theo % sơ tán (M)

Tệp: `Map02ChapterDirector.cs` (vòng `defend`), `Map02ChapterGuidance.cs`, `Map02Mission.Hud.cs`.

> **Trạng thái 21/10:** đã làm trong file mới `Map02ChapterLanes.cs`.
> - Các chặng 0→60 / 60→70 / 70→80 / 80→90; `taskProgress` là tỉ lệ 0–1.
> - % tăng ×0,4 khi còn lính. Đợt 2 (3 lính, cho phép ít nhất 1) xuất hiện ở 50%.
> - Qua chặng khi đạt 100% của chặng, đợt 2 đã xuất hiện và không còn lính.
> - **Bắt đầu giữ ngõ khi bước vào vùng 12 m, không cần E.**
> - HUD: "Sơ tán X% · Còn N lính…", không còn chữ "giây".
> - "Chuyến một đi rồi" ở mức sơ tán 30% của `N3_04`.
> - **Đạn rơi:** 5–8 viên mỗi xác, tự nhặt khi đi qua (1,8 m), có nhãn "◇ đạn" trong vòng 15 m, lưu cờ `looted`. Hùng nhắc qua đàm khi còn dưới 10 viên (`m2v3_ammo_01`, 135 dòng thoại).
> - Save cũ lưu giây: tự đổi sang tỉ lệ khi giá trị > 1.
> - Test `LaneHoldsFollowEvacuationAndTheFallenDropRounds`, gồm phép tính ngân sách đạn theo sát thương thật của súng. Bộ test 46/46.
>
> **Cần xem ở GĐ4:** các vị trí giữ ngõ chỉ cách nhau 9–10 m, nhỏ hơn bán kính 12 m. Vì vậy xong chặng trước là chặng sau tự bắt đầu ngay tại chỗ. Cần đặt lại `lane2` (gốc me, đi ngang nhà chị Lành) và `lane3` cho xa hơn.

- Các chặng: `N3_04` 0→60 (chuyến 1 và 2), `N3_05A` 60→70, `N3_05B` 70→80, `N3_05C` 80→90. `seconds` trong JSON đổi nghĩa thành "thời lượng chặng khi đường đã trống".
- `taskProgress` (đã có trong save) tăng `dt / seconds` khi Nam ở trong vùng 12 m, nhân với 0,4 khi còn lính sống. Ra khỏi vùng thì dừng tăng nhưng không mất tiến độ.
- Đợt 1 xuất hiện khi bắt đầu chặng. Đợt 2 (3 lính) xuất hiện ở 50%. Qua chặng khi `taskProgress ≥ 1` và không còn lính sống. Kịch bản cho mỗi tốp 3–5 lính.
- Trong `N3_04`: ở 30% chuyến 1 rời bến kèm câu nói nền của Hùng; ở 60% chuyến 2 rời bến (GĐ4 lo phần thuyền).
- HUD: "Sơ tán 74% · Còn 2 lính chặn ngõ" / "Trở lại ngõ để che cho dân". Bỏ mọi chỗ hiện "còn X giây".
- Đạt 90% ở `N3_05C` thì ngừng sinh lính và chạy ngay `N3_WOUND` (GĐ6).
- **Ngân sách đạn, tránh kẹt game.** Đạn Đêm 3 hữu hạn: 90 viên cất ở Ngày 1 cộng số đang mang (`N3_01` không nạp đầy). Không có nguồn đạn nào khác. Hiện có 20 lính phải hạ (`initial_attack` + 4 chặng × 4). Thêm đợt 2 thì lên khoảng 32. Vì muốn qua chặng phải hết lính, hết đạn là **kẹt cứng**. Chọn một trong hai:
  - (a) Lính chết rơi đạn, mỗi người khoảng 5–8 viên. Nhặt bằng E hoặc tự nhặt khi đi qua.
  - (b) Thêm một thùng đạn tại chỗ trú bến nam: dân làng chuyển ra, nhận một lần.

  Đề xuất (a), vì không cần thêm điểm ghim. Kèm lưới an toàn: còn dưới 10 viên mà vẫn còn lính thì Hùng nhắc qua đàm ("Đạn ở xác tụi nó, lấy mà dùng").
- Nam chết trong lúc giữ ngõ thì là thất bại bình thường, quay lại checkpoint đầu chặng. % sơ tán của chặng đó về lại mức của checkpoint.

**Xong khi:** Có test: đứng trong vùng mà không bắn thì không qua được chặng. Có test tính đạn: chơi từ `N3_01` với số đạn tối thiểu hợp lệ (chỉ 90 viên đã cất) vẫn đủ hạ hết lính khi trúng khoảng 60% số phát. Ra vào vùng vẫn giữ nguyên %. Lưu/tải giữa chặng giữ đúng % và số lính. `Map02FlowMovieTests` vẫn chạy hết 43 bước.

### GĐ4 — Xe tăng, thuyền, vị trí nhân vật (L)

Tệp: phần partial mới `Map02ChapterTank.cs`, `Map02ChapterLayout.cs`, `Map02ChapterLayout.asset`, `Map02ChapterCinematics.cs`.

1. **Đặt vị trí thật trên bản đồ** (làm tay trong editor, có ảnh chụp). Thêm vào `Map02ChapterLayout.sites`:
   - `lane3Wall` (bức tường Nam nấp ở vị trí thứ ba), `tankBreach` (rào tre ngay sau bức tường đó), `tankTurn` (chỗ xe rẽ vào đường làng).
   - `tankHide` (bóng tre đầu đường bến) và `tankDock` (đầu đường bến, nhìn ra sông, cách `ditch` 15–25 m).
   - `hungCanal1..n`: bờ mương từ `ditch` tới `ChargePoint`. Nếu được, nên là cùng con mương ở Đêm 2 hoặc trông giống, để câu "Tôi thuộc" có sức nặng.
   - `boatDock`, `boatMid` (giữa dòng, trước mũi súng), `boatFar` (bờ bên kia).
   - `tamCover` (bờ tre gần `fallen`), `artyImpact1..3` (rìa làng), `militiaEntry` (phía đồng), `lanhKitchen` (bếp nhà chị Lành, nằm trên đường từ `lane1` sang `lane2`).
2. **Trạng thái xe tăng (Q3)**:

   | Thời điểm | Xe tăng | Kịch bản |
   |---|---|---|
   | Trước `N3_WOUND` | Ẩn | — |
   | Trong `N3_WOUND` | Phá rào ở `tankBreach`, nòng quay chậm về phía Nam, bắn trúng bức tường | Nhịp 3 |
   | Cảnh `alive` (sau khi bò) | Rẽ ở `tankTurn` vào đường làng, khuất dần | "Xe tăng không đuổi theo Nam. Nó rẽ vào đường làng, hướng ra phía bến." |
   | Trong `N3_06` | Khuất ở `tankHide`, chỉ nghe tiếng xích xa | Bảng Địch: không xuất hiện ở Nhịp 4 |
   | Mở đầu `N3_07` | Trồi khỏi bóng tre ra `tankDock`, tốp lính lom khom theo sau. Súng máy quét xuống nước trước mũi thuyền | Nhịp 5 phần 1 |
   | Từ `N3_09` tới Bình minh | Cháy, đứng im ở `tankDock`; Bình minh chỉ còn khói mỏng | Nhịp 5 phần 3, Bình minh |

   Tải save thì xe đặt luôn ở trạng thái đích. `ChargePoint` tự lấy theo `tankDock`, nên Hùng không còn chạy ngược lên ngõ đình. Bắn vào xe chỉ ra tia lửa và tiếng "keng", không trừ máu.
3. **Thuyền (Q4):** chuyến 1 rời `boatDock` ở 30% của `N3_04`, chuyến 2 ở 60%. Vào `N3_07`, chuyến cuối ở `boatMid` chở chị Lành, bé Tí, ông Sáu, 3 dân làng, chú Bảy chèo. Súng máy bắn thì thuyền chòng chành. Trong `N3_09`, thuyền trôi chậm về `boatFar`, bé Tí ngoái lại. Lúc Bình minh, dân đã về lại bờ làng.
4. **Vị trí nhân vật trong Đêm 3**:
   - Anh Lực ở chỗ nghỉ để nhận ca ở `N3_00`. Sau đó giữ chốt ngoài, rồi lùi về gốc me lúc bắt đầu Nhịp 3. Có mặt ở `cover` khi Nam bò tới, kéo Nam vào sau đống gạch và băng vội (chỗ gắn clip) trước câu "...Còn sống."
   - Tâm ở `tamCover` trong `N3_06`.

**Xong khi:** Có test: ở các checkpoint `N3_05C`, `N3_CRAWL`, `N3_06`, `N3_07`, `N3_09`, `DAWN`, xe tăng và thuyền ở đúng trạng thái. Đường từ `ditch` tới `ChargePoint` đầy đủ và dài dưới 30 m. Ảnh 1920×1080 cho thấy từ bờ đất nhìn được cả xe tăng lẫn thuyền.

### GĐ5 — Yểm trợ Hùng, N3_08 (M)

Tệp: `Map02ChapterDirector.cs`, `Map02ChapterEnemy.cs`, `Map02ChapterGuidance.cs`.

- **Tốp lính đi kèm xe tăng** (4 lính) xuất hiện từ đầu `N3_07`, đứng nguyên chỗ phía sau và bên hông `tankDock` (`holdPosition`), bắn về phía Nam (chỉ là hiệu ứng vì Nam bất tử). Bị trúng hoặc có đạn bay sát trong 1,5 m thì cúi xuống 2,5 giây (nghe `Health.Damaged` và raycast đường đạn).
- **Hùng ôm bộc phá lăn xuống bờ mương rồi bò dọc `hungCanal`** (`hungCrawlClip`). Tốc độ cơ bản khoảng 90 giây cho cả quãng, nhanh gấp 1,5 khi một nửa tốp đang cúi hoặc đã chết, đi nốt ×2 khi hết lính. Thời lượng kẹp trong 60–90 giây (Q10). Tới `ChargePoint` thì chạy `N3_09`.
- **Nam tì súng lên bờ đất bằng tay lành** (khóa di chuyển như hiện tại, chỗ gắn tư thế `namBraceClip`). Máy đàm Hùng để lại nằm cạnh Nam.
- **Camera:** ở 1/3 và 2/3 quãng đường, camera lia sang bóng Hùng khoảng 1,5 giây rồi trả lại, người chơi vẫn bắn được.
- **Âm thanh:** không nhạc. Chỉ tiếng súng, tiếng xích và tim đập.
- HUD thay "còn X giây" bằng thanh "Hùng đang bò dọc bờ mương" theo quãng đường. Giữ quy tắc không thất bại, kể cả khi tải save.

**Xong khi:** Sửa test Mechanics hiện có: Hùng đi dọc `hungCanal`, không đi đường tắt; tải save giữa chừng không đưa Hùng về đầu. Thêm 2 test: không bắn thì kết thúc ở 90 giây; hạ hết lính ngay thì kết thúc ở 60 giây.

### GĐ6 — Dàn cảnh cho bị thương, chia tay, hy sinh, Nhịp 6 (L)

Tệp: `Map02ChapterCinematics.cs` và phần partial mới `Map02ChapterFinale.cs`.

**`N3_WOUND` + `N3_CRAWL` (Nhịp 3)**
1. Mặt đất rung, tiếng xích sắt ngay sau bức tường → xe phá rào tre → nòng quay chậm → anh Lực: "Xe tăng! Nằm xuống!"
2. Phát đạn trúng tường: nháy trắng, rung, cắt sang góc nhìn Nam nằm sấp giữa gạch vụn (`namProneClip`), màn hình nhòe đỏ, âm chìm. Không dựng cảnh tường sập.
3. Hùng (đàm): "Nam! Nam! Trả lời tôi!" Người chơi giữ phím để bò, tiếng gọi lặp lại.
4. Tới chỗ nấp: anh Lực kéo vào và băng vội → "...Còn sống." → Hùng im một nhịp → "Về bến. Ngay. Đừng cãi." → xe tăng rẽ đi (GĐ4).

**`N3_07` (Nhịp 5 phần 1, khoảng 2 phút)**

| Thứ tự | Sự kiện |
|---|---|
| 1 | Cắt nhạc chiến đấu đúng lúc súng máy mở lửa vào thuyền. Thuyền chòng chành. Chú Bảy: "Quay về không kịp! Nó bắn nữa là chìm!" |
| 2 | Nam đứng dậy bắn trả, đạn kêu "keng" vào thép. Vết thương toác ra, Nam khuỵu xuống |
| 3 | Cắt cảnh (theo gợi ý kịch bản): khung sau Nam đã nằm sau bờ đất, Hùng dùng tay trái kéo cổ áo. "Nam, xuống!" … "Pháo không bắn vào đây được…" |
| 4 | Hùng kéo gói vải khỏi hố bằng tay trái (đạo cụ Ngày 3 biến mất). Nam định bật dậy, khuỵu nửa chừng: "Để tôi đi!" Hùng ấn vai lành của Nam xuống |
| 5 | "Cậu có thuộc đường bờ mương đâu…" |
| 6 | **Hùng tháo máy đàm đặt vào lòng Nam**: gắn đạo cụ máy đàm vào Nam, bật `radioTransferred` từ thời điểm này |
| 7 | "Này Nam... việc bảo vệ mọi người…" → Nam nắm áo Hùng: "Đừng, Hùng!…" → Hùng gỡ tay, camera sau lưng Nam, Hùng ngược sáng → "Giờ mà không đi…" (một nhịp) "Thế nhá, người anh em." |

Checkpoint ngay sau cảnh (code đã lưu sau mỗi nhiệm vụ).

**`N3_09` (Nhịp 5 phần 3 + Nhịp 6, khoảng 70 giây)**

| Giây | Sự kiện | Ghi chú |
|---|---|---|
| 0–3 | Bóng Hùng bật dậy khỏi bờ mương, hét lớn, lao tới chân xe | Camera xa sau vai Nam, nhìn qua khói. Tiếng hét là câu nói nền hoặc âm riêng |
| 3–6 | Cúi xuống chỗ bánh xích, gài bộc phá bằng tay trái, giật ngòi | `hungPlantRetreatClip`. Chưa có clip thì dùng bóng tạm |
| 6–8 | Quay lại, chạy được vài bước | |
| 8 | Lửa nòng sau xe, Hùng ngã sấp cách xe vài mét | `hungFallClip`, không ragdoll |
| **9** | **Nháy trắng toàn màn hình, rung mạnh, tắt âm. Xe chuyển sang trạng thái cháy** | Đúng "một giây" sau cú ngã |
| 9–19 | Ù tai, bảng màu nhạt dần trong 10 giây. Xe cháy là nguồn sáng cam chính. Nam nằm ngửa sau bờ đất (`namLieBackClip`). Thuyền trôi chậm, bé Tí ngoái lại. Nam chống tay, vươn tay về phía khói | Q5 |
| 13–19 | Giọng Hùng vọng lại (tệp `m2v3_echo_promise`). Nam gục đầu, rồi ngẩng lên với tư thế dứt khoát | Không cận mặt, quyết tâm thể hiện bằng tư thế |
| ~30 | Bắt đầu Nhịp 6. Tiếng rè, máy đàm trong lòng Nam: "Rạch Tre. Bắn." | Không nhạc |
| 31–60 | Tiếng rít trên đầu, pháo nổ ở `artyImpact1..3`. Bóng du kích (dùng lại model du kích) ngược sáng từ `militiaEntry` tràn vào ngõ. Anh Thành chạy qua: "Phản công!" Nam không đứng dậy, ngồi tựa bờ đất ôm máy đàm, nhìn về phía xe đang cháy (`namSitRadioClip`), quay xa | |
| 60–70 | Tiếng súng xa dần, màn hình tối, sang `DAWN` | |

- **Rung màn hình:** `ThirdPersonCamera` hiện chưa có hàm rung, chỉ có `AddRecoil` và `SetCinematicView`. Thêm hàm `Shake(amplitude, seconds)` kiểu cộng dồn vào vị trí camera. Camera này dùng chung với Map 1, nên không đổi hành vi nào đang có. Thêm 1 test để Map 1 không bị ảnh hưởng.
- **Phím bò:** `PlayerController` đang dùng nhấn C (và Ctrl) để bật/tắt đi rón rén. `N3_CRAWL` lại giữ C để bò. Phải đảm bảo khi bò xong Nam không bị kẹt ở chế độ rón rén: đặt lại `IsSneaking` khi kết thúc `N3_CRAWL`, hoặc đổi phím bò. Kiểm tra cả trên tay cầm nếu game có hỗ trợ.
- **Skip (giữ Esc 1 giây)** ở bất kỳ cảnh nào từ `N3_07` tới `N3_09` phải ra cùng trạng thái cuối: Hùng đã chết, xe cháy, máy đàm trên người Nam, Nam còn băng vai, thuyền đã sang bờ bên kia, không còn bóng du kích, đã tắt lọc âm và trả màu. Dọn dẹp gom vào `ReleaseSceneAction()` và `Cancel()`.
- Khung hình cho cả chương: không cận mặt. Gần nhất là ngang vai, từ sau lưng hoặc nghiêng.

**Xong khi:** Có test: skip ở bất kỳ giây nào của `N3_07` và `N3_09` đều ra đúng trạng thái cuối. Tải save sau `N3_09` thì Hùng không xuất hiện ở đâu, kể cả trong thoại nền. Không có thay đổi nào với `Time.timeScale`.

### GĐ7 — Dàn cảnh phụ của Ngày 3 và Nhịp 1–4 (M)

| Hạng mục | Cách làm | Kịch bản |
|---|---|---|
| Mang thùng lựu đạn | Kịch bản: "Nam chuyển … một thùng lựu đạn và khối bộc phá … tới chỗ trú". Hiện `D3_02` chỉ là giữ Space tại chỗ. Đổi thành **mang 1 thùng** từ kho kín tới hốc bờ đất (dùng cơ chế `carry` có sẵn), rồi mới chạy cảnh anh Lực đặt gói vải. Giữ `kind: "demolition"` và thêm bước nhận/đặt thùng, không thêm ID |
| Đạo cụ bộc phá | Xong `D3_02` thì có thùng lựu đạn (Nam đặt) và gói vải buộc dây (anh Lực đặt) ở hốc bờ đất. Lấy ra ở `N3_07` | Ngày 3, Nhịp 5 |
| Phân công | Nhà đầu tiên của `D3_04` phải là **nhà ông Sáu** (chính là `family`). Hùng và ông Sáu có mặt khi nói. **Kiểm tra lại:** `family0` lấy từ `homePositions[0]`, có thể không trùng `family` | "Nam định đi qua nhà ông Sáu" |
| Kiểm tra thuyền | Chú Bảy vỗ mạn thuyền ở câu `boats_01` | Ngày 3 |
| Hùng dìu ông Sáu thử xuống bậc bến | Đoạn ngắn không khóa điều khiển sau `D3_04`. Dùng chung clip dìu với Nhịp 2 | Ngày 3 |
| Bữa chiều | Ánh sáng chiều vàng ấm nhất chương. Hùng nhặt rau tay trái, tay phải đặt yên trên đùi. Nam chẻ củi. Chị Lành đếm trên ngón tay | Ngày 3 |
| Nhịp 1 | `N3_00`: anh Lực có mặt để nhận ca → Nam nằm xuống → tối 1 giây → pháo sáng trắng bừng lên, mái lá đỏ rực, người chạy qua cửa, Hùng đã dậy khoác máy đàm | Nhịp 1 |
| Nhịp 2 | Nhà ông Sáu cháy mái sau, cửa sau đạp ra (dùng lại clip nếu có). Ông Sáu ho sặc sụa. Bé Tí ôm cổ Nam khi được tìm thấy | Nhịp 2 |
| Nhịp 3 | Đường từ `lane1` sang `lane2` đi ngang nhà chị Lành: nồi canh còn trên bếp, mái đang cháy. Không dừng, không thoại | Nhịp 3 |
| Nhịp 4 | Tâm ở `tamCover` bắn yểm trợ bằng hiệu ứng (lửa nòng, tiếng súng hướng ra đồng) | Nhịp 4 |
| Tiếng xích sắt | Một tệp duy nhất: Đêm 2 ("Tiếng gì thế?"), mỗi lần xe tăng sắp xuất hiện (Nhịp 3, Nhịp 5), và tiếng xa trong Nhịp 4 | Âm thanh Đêm 2, 3 |

### GĐ8 — Kiểm thử và tài liệu (S)

- Chạy lại toàn bộ bộ test Map02 ở batchmode với `-testCategory "!Replay"`. Hai test EditMode của Map01 đôi khi lỗi ngẫu nhiên do tải cảnh menu: chạy lại trước khi điều tra.
- **Sửa bộ chạy `Map02FlowMovieTests`:**
  - Bỏ bước `Interact` ở 3 cảnh tự chạy.
  - Vòng `defend` hiện có giới hạn 150 giây và cứ 4 giây diệt hết lính: kiểm tra lại với cách tính % và đợt 2.
  - Vòng `coverFire` chờ Hùng tới `ChargePoint`.
  - Câu chốt "43 tasks" giữ nguyên.
- Video `Map02FlowMovieTests` chỉ để kiểm tra thứ tự, không dùng làm bằng chứng nghiệm thu.
- **`Map02Stage`** dùng cho telemetry (commit "telemetry stage ids"). Ánh xạ trong `ApplyState` không đổi. Thêm assert rằng chuỗi stage của một lượt chơi trọn vẫn giống trước khi sửa.
- **Hiệu năng Đêm 3:** cộng dồn lửa nhà cháy, pháo sáng, xe cháy, khói, đèn điểm và 6–8 bóng du kích. Đo FPS ở Full HD tại 3 điểm (ngõ đình, bờ đất lúc xe cháy, Nhịp 6), so với Đêm 2. Đèn điểm mới đều tắt bóng đổ, như đèn dầu hiện tại.
- **J/M và HUD không tiết lộ cái chết của Hùng:** soát tiêu đề và `purpose` của `N3_07`–`N3_09` trong danh sách nhiệm vụ (yêu cầu từ `Flow_Critique_20261021.md`).
- Cập nhật `Implementation_and_QA.md`. Đánh dấu `Docs/Map02_Ending_Revision.md` là đã cũ.
- Chơi tay trọn Ngày 3 → Bình minh theo mục 6.

---

## 4. Tương thích save

| Trường hợp save cũ | Xử lý |
|---|---|
| Đang ở `D3_06` | Ghim chuyển sang sân nhà chị Lành, không cần chuyển đổi gì |
| Đang giữ chặng `defend` với `taskProgress` tính bằng giây | Đổi một lần: `taskProgress / seconds`, kẹp trong [0,1]. Nhận biết qua giá trị > 1 |
| Đang ở `N3_04` khi đã qua mốc 30% hoặc 60% | Thuyền 1/2 đặt sẵn ở bờ bên kia theo % hiện tại |
| Đang ở `N3_08` | Hùng đặt ở điểm `hungCanal` gần vị trí đã lưu nhất, đi tiếp |
| Sau `N3_CRAWL` | Xe tăng ở `tankHide`. Sau `N3_07` thì ở `tankDock` (suy ra, không cần lưu) |
| `version` | Giữ `3`. Không thêm trường bắt buộc |

Bổ sung các ca trên vào `Map02M16SaveCompatibilityTests`.

---

## 5. Danh sách gửi nhóm đồ họa và âm thanh

Xếp theo thứ tự ưu tiên ngân sách của kịch bản.

| Ưu tiên | Hạng mục | Dùng ở | Khi chưa có |
|---|---|---|---|
| 1 | Lồng tiếng Nam và Hùng: Bữa chiều, `N3_07`. **Tệp riêng** câu hứa và tiếng cười của Hùng (ghi ở Đêm 1) | Ngày 3, Nhịp 5–6, Bình minh | Phụ đề, thời lượng theo độ dài chữ |
| 2 | Âm thanh: tiếng xích sắt, ù tai, pháo rít, tim đập, rè máy đàm (có thể dùng lại `OperationsAudio/radio_off`), tiếng Hùng hét, ông Sáu ho, không khí Bữa chiều (dao thái rau, nồi sôi, trẻ con cười), nhạc chiến đấu Nhịp 1–4, chủ đề của Hùng (đủ ở Bữa chiều / bỏ lửng / thiếu nốt cuối) | Ngày 3 → Bình minh | Im lặng, giữ tiếng súng có sẵn |
| 3 | `hungPlantRetreatClip` (đã có chỗ), `hungCrawlClip`, `hungFallClip` (ngã sấp), Hùng kéo cổ áo Nam | Nhịp 5 | Bóng tạm, ẩn Hùng khi ngã như hiện tại |
| 4 | `tankArt` (đã có), vật liệu xe cháy, `boatArt` (cùng model thuyền đã vá), `explosivesArt` (thùng lựu đạn, gói vải), `radioPropArt`, `soupBowlArt` | Ngày 3 → Bình minh | Khối hình tạm |
| 5 | `namProneClip`, `namLieBackClip`, `namSitRadioClip`, `namBraceClip`, Nam đứng bắn rồi khuỵu, anh Lực băng cho Nam, clip dìu người già, nhặt rau, chẻ củi, ném cọng rau, bé Tí ôm cổ, bé Tí ngoái lại | Ngày 3, Nhịp 2–6 | Tư thế đứng tạm (giới hạn đã biết) |
| 6 | Model bé Tí và 3 dân làng trên thuyền (`civilianArt` đã có), bóng du kích Nhịp 6 (dùng lại model du kích) | Nhịp 5–6, Bình minh | Model dân làng chung |

Quy tắc kiểm tra cho mọi clip: **tay phải Hùng không được dùng** từ Ngày 1 tới Nhịp 5, kể cả tư thế chờ.

---

## 6. Kiểm thử tay trước khi duyệt

1. **Ngày 3:** làm bốn việc theo thứ tự bất kỳ. Lúc cất bộc phá, Hùng có mặt, câu của Hùng không có nhãn "(bộ đàm)". Phân công bắt đầu ở nhà ông Sáu. Bữa chiều có anh Thành bước vào sân. Báo cáo ngay tại sân rồi sang Đêm 3.
2. **Nhịp 1–2:** nhận ca với anh Lực, pháo sáng, cứu cả nhà ông Sáu, tìm bé Tí. Chuyến 1 rời bến kèm câu nói nền của Hùng mà vẫn điều khiển được.
3. **Giữ ngõ:** không bắn thì không qua được. Ra vào vùng vẫn giữ %. HUD không hiện giây. Bắt đầu Nhịp 3 nghe "Lùi về gốc me!". Đi ngang nhà chị Lành thấy nồi canh và mái đang cháy.
4. **Xe tăng:** phá rào ở vị trí thứ ba, Nam bị thương, bò tới anh Lực, xe rẽ đi. Trong Nhịp 4 không thấy xe. Ở Nhịp 5 xe trồi ra ở đầu đường bến, nhìn được cả xe lẫn thuyền.
5. **Yểm trợ:** Hùng bò dọc bờ mương. Đoạn này dài 60–90 giây tùy cách bắn. Không thể chết. Bắn vào xe chỉ kêu "keng".
6. **Cảnh hy sinh:** ngã rồi một giây sau mới nổ. Không cận mặt. Máy đàm nằm trên người Nam từ `N3_07`. Skip ở nhiều thời điểm đều ra cùng trạng thái cuối.
7. **Lưu/tải** ở `N3_04` (đã qua 30%), `N3_05B`, `N3_06`, `N3_08` và sau `N3_09`: xe tăng, thuyền, người dân ở đúng chỗ. Hùng không xuất hiện lại.
8. **Thời lượng:** đo trong buổi chơi thử. Mục tiêu theo kịch bản: Ngày 3 12–15 phút, Đêm 3 23–28 phút.
9. **Câu hỏi sau buổi chơi thử** (lấy từ kịch bản): "Hùng muốn làm gì sau chiến tranh?" / "Bạn có nghĩ pháo bắn sớm hơn thì Hùng đã sống không?" / "Bạn có nghĩ Hùng chết vì mình bắn yểm trợ kém không?"

---

## 7. Mâu thuẫn trong chính kịch bản — cần tác giả chốt

Kế hoạch đang theo phần **diễn biến từng nhịp**, vì đó là bản chi tiết nhất. Các chỗ sau trong kịch bản tự mâu thuẫn:

| # | Chỗ A | Chỗ B | Kế hoạch đang theo |
|---|---|---|---|
| K1 | Bảng nhân vật: anh Lực "lôi Nam rời khỏi xác Hùng để lên tuyến" | Nhịp 6: Nam ngồi tựa bờ đất ôm máy đàm, không đứng dậy. Không có cảnh nào kéo Nam đi | Nhịp 6. Anh Lực chỉ kéo và băng cho Nam ở Nhịp 3 |
| K2 | Bảng nhân vật: chị Lành "dắt bé Tí và ông Sáu xuống bến" | Nhịp 2 và bảng nhiệm vụ: Nam đưa ông Sáu và chị Lành đi, rồi Nam tìm bé Tí | Nhịp 2 (khớp với code hiện tại) |
| K3 | Bảng asset: "Nam đi với băng vai; tâm ngắm rung, nạp đạn chậm — Nhịp 6, Bình minh". Mục "Cần chốt": "Nhịp 6 cần thêm hoạt ảnh tay ngắm rung" | Nhịp 6 là cutscene 40 giây, Bình minh Nam chỉ ngồi | Cutscene, không có tay ngắm ở Nhịp 6. Hiệu ứng tâm ngắm rung áp dụng từ Nhịp 3 tới hết `N3_08` |
| K4 | "Xưng hô thống nhất tôi – cậu" | Câu giữ nguyên văn bản gõ tay: "Mình đi diệt nó đây!" | Giữ "Mình…" như kịch bản đã ghi, nhưng cần tác giả xác nhận cố ý |
| K5 | M2_D3_06 "Báo cáo anh Thành" là nhiệm vụ riêng | Không có thoại báo cáo, và lệnh của anh Thành đã nằm trong cảnh Bữa chiều | Q1: báo cáo tại sân, không thoại |
| K6 | Đoạn yểm trợ "60–90 giây" | "…hoặc khi tốp lính bị áp chế thì cảnh tự chạy tiếp" (có thể sớm hơn 60 giây) | Q10: sàn 60 giây |
| K7 | Mục kiểm thử: bộc phá "xuất hiện đúng vị trí người chơi đã đặt ở Ngày 3 (hoặc vị trí cố định nếu đặt sai chỗ)" | M2_D3_02 chỉ có một vị trí đặt cố định | Vị trí cố định ở hốc bờ đất |

---

## 8. Rủi ro

| Rủi ro | Giảm thiểu |
|---|---|
| Vị trí xe tăng, bờ mương, thuyền và tường ở ngõ thứ ba lệch địa hình thật | GĐ4 bắt đầu bằng việc đặt điểm tay trong editor kèm ảnh chụp. Test NavMesh cho mọi điểm |
| Tốc độ % sơ tán quá nhanh hoặc quá chậm | Chỉnh qua `seconds` trong JSON. Chốt sau buổi chơi thử |
| Thiếu clip làm cảnh hy sinh trông như bóng tạm | Kịch bản đã chọn camera xa, ngược sáng, khói che. Ưu tiên clip của Hùng |
| Câu nói nền chồng lên câu khác hoặc lên HUD | Một hàng đợi duy nhất có ưu tiên. Cảnh khóa điều khiển luôn chặn câu nói nền |
| Code đang gắn cú nháy trắng theo vị trí dòng thoại | Chuyển sang gắn theo ID ngay ở GĐ1 |
| Bộ test hiện có giả định phải `Interact(N3_09)` và hiện "còn X giây" | Sửa test ngay trong giai đoạn đổi hành vi, không dồn về cuối |

---

## 9. Giả định kỹ thuật đã kiểm tra trong code

| Giả định | Kết quả |
|---|---|
| Có sự kiện khi người chơi bắn, để tính đạn bay sát (GĐ5) | Có: `PlayerCombat.Attacked(WeaponKind, Vector3)`, `Health.Damaged` |
| Có hàm rung camera | **Không có**, phải thêm vào `ThirdPersonCamera` (GĐ6) |
| Có clip bò, nằm sấp, đạp cửa | **Không có** trong project. Tất cả đều là asset mới (mục 5) |
| Dựng được lửa và khói bằng code | Được: `Map01Fx.System`, vật liệu `Glow`/`Burn`/`Lit` (URP Particles, đạt test shader hiện có) |
| Có chỗ hiện câu nói nền | Có sẵn nền: `Map02Mission.Say` hiện 5 giây. Câu nói nền mở rộng từ đây thành hàng đợi |
| Có nguồn đạn trong Đêm 3 | **Không có**. Xem phần ngân sách đạn ở GĐ3 |
| Phím C còn trống | **Không**, C đang bật/tắt rón rén (GĐ6) |

---

## 10. Phạm vi Bình minh

Kế hoạch không làm lại Bình minh. Đêm 3 chỉ phải **bàn giao đúng trạng thái**: Hùng nằm ở bờ đất cạnh bến, xác xe tăng ở `tankDock` còn khói mỏng, dân đã về bờ làng, máy đàm ở chỗ Nam, Nam còn băng vai.

Phần kịch bản Bình minh còn thiếu so với code, để làm ở một đợt sau:

- Chị Lành bưng chén canh, đặt cạnh Hùng.
- Anh Thành đặt tay lên vai lành của Nam rồi đi.
- Anh Lực đến ngồi trực cùng, Nam đặt súng xuống. Một chỗ bên cạnh để trống.
- Bé Tí đứng xa, nắm tay chị Lành.
- Người dân thu dọn ở xa, không ai reo mừng.
- Ánh sáng xám lạnh, chỉ ấm dần trong 10 giây cuối.
- Tiếng cười của Hùng ở Đêm 1 vang lên rồi tắt. Chủ đề của Hùng thiếu nốt cuối.

---

## 11. Tiến độ, phân công, bàn giao

**Tổng ước lượng phần lập trình:** khoảng **9 ngày công** (GĐ1 0,5 + GĐ2 1 + GĐ3 1 + GĐ4 2 + GĐ5 1 + GĐ6 2 + GĐ7 1 + GĐ8 0,5). Chưa tính thời gian làm đồ họa, lồng tiếng và đặt điểm trong editor.

| Mốc | Sau giai đoạn | Chơi được gì | Ước lượng tích lũy |
|---|---|---|---|
| A | GĐ1–GĐ3 | Ngày 3 → hết Nhịp 3 đúng thứ tự thoại, giữ ngõ theo %, không kẹt đạn | khoảng 2,5 ngày |
| B | GĐ4–GĐ5 | Địa lý Nhịp 4–5 đúng (xe tăng, thuyền, bờ mương), đoạn yểm trợ mới | khoảng 5,5 ngày |
| C | GĐ6–GĐ8 | Trọn Ngày 3 → Bình minh với hình tạm, toàn bộ test Map02 đạt, sẵn chỗ để thả clip/âm thanh | khoảng 9 ngày |
| D | Khi có asset | Thay hình tạm, duyệt hình ảnh, chơi thử với người mới chưa từng chơi | Phụ thuộc nhóm art |

| Việc | Ai làm | Cần trước mốc |
|---|---|---|
| Duyệt Q1–Q10 | Trưởng nhóm / người phụ trách thiết kế | A |
| Trả lời K1–K7 (mục 7) | Tác giả kịch bản | C (không chặn A, B) |
| Đặt các điểm trong `Map02ChapterLayout` bằng editor (GĐ4) | Người dựng map | B |
| Clip và model (mục 5, ưu tiên 3–6) | Nhóm đồ họa | D |
| Lồng tiếng và âm thanh (mục 5, ưu tiên 1–2), gồm tệp ghi riêng câu hứa | Nhóm âm thanh | D. Riêng tệp câu hứa nên có trước C để duyệt nhịp cảnh hy sinh |

**Bàn giao:** làm trên một nhánh riêng tách từ `dev2`, commit theo từng giai đoạn để dễ xem lại, mở PR vào `dev` như các đợt Map 2 trước. Mỗi PR kèm kết quả test và ảnh Full HD của các khung đã đổi.

**Hoàn thành chung (trước mốc D):**

- Mọi dòng ở mục 3 GĐ1 khớp với kịch bản hoặc có ghi chú [LỆCH] được duyệt.
- Toàn bộ test Map02 đạt, không bỏ qua test nào.
- Lưu/tải và skip ở mọi checkpoint Đêm 3 cho đúng trạng thái.
- Không có đường nào dẫn tới kẹt game (đạn, giữ ngõ, hộ tống).
- Danh sách chơi tay ở mục 6 đi hết mà không phải dùng lệnh debug.

---

## 12. Lịch sử rà soát

**21/10, rà soát lần 1 so với kịch bản**, đã sửa:

- **Q1:** bản trước biến D3_06 thành bước tự chuyển. Kịch bản liệt kê nó là nhiệm vụ riêng, nên nay giữ là nhiệm vụ, đặt ghim ở sân.
- **Xe tăng:** bản trước cho xe hiện ra từ 85% và chạy công khai trong Nhịp 4. Kịch bản chỉ cho xe xuất hiện ở Nhịp 3 và Nhịp 5, và xe phá rào ngay trong cảnh bị thương.
- **Máy đàm:** bản trước trao máy đàm ở Nhịp 6. Kịch bản trao ở Nhịp 5 phần 1 ("Hùng tháo máy đàm đặt vào lòng Nam").
- **Câu "Lùi về gốc me":** bản trước phát lúc rời ngõ thứ nhất. Kịch bản đặt ở đầu Nhịp 3.
- **Thời lượng `N3_09`:** bản trước tính 40 giây cho cả cảnh. Riêng Nhịp 6 đã khoảng 40 giây, nên nay cả cảnh khoảng 70 giây.
- **Giọng vọng lại:** bản trước định dùng lại bản thu `promise_05`. Kịch bản yêu cầu tệp ghi riêng.
- **Tiếng gọi khi bò:** bản trước thêm dòng mới. Nay dùng lại `wound_05`.
- **Thêm phần còn thiếu:**
  - Thuyền: chuyến 1 và 2, thuyền chòng chành, dùng cùng model thuyền đã vá.
  - Nhịp 5 phần 1: tốp lính lom khom theo xe, Nam đứng bắn rồi khuỵu, Hùng kéo Nam.
  - Nhịp 1: nhận ca với anh Lực, người chạy qua cửa, Hùng khoác máy đàm.
  - Nhịp 3: anh Lực băng cho Nam. Nhịp 2: bé Tí ôm cổ Nam, ông Sáu ho.
  - Bữa chiều: ném cọng rau, khoảng lặng "nhìn Nam một lúc lâu".
  - Âm thanh: nhạc chiến đấu, tim đập, rè máy đàm, không khí Bữa chiều.
  - Kiểm tra nhà ông Sáu ở `D3_04`. Đo thời lượng buổi chơi.
- **Thêm câu cài trước ở Ngày 2:** `artillery_01`.
- **Lỗi kỹ thuật:** cú nháy trắng đang gắn với `lineIndex==3`.
- **Thêm mục 7** (mâu thuẫn trong kịch bản).

**21/10, rà soát lần 2 về độ đầy đủ**, đã bổ sung:

- **GĐ3:** ngân sách đạn và cách tránh kẹt game (đạn hữu hạn, khoảng 32 lính phải hạ, không có nguồn đạn). Quy tắc khi Nam chết lúc giữ ngõ.
- **GĐ6:** hàm rung camera còn thiếu, xung đột phím C với đi rón rén.
- **GĐ7:** `D3_02` phải mang thùng như kịch bản.
- **GĐ8:** sửa bộ chạy FlowMovie, kiểm tra telemetry `Map02Stage`, đo hiệu năng, soát J/M không lộ cái chết của Hùng.
- **Thêm mục 9** (giả định kỹ thuật đã kiểm), **mục 10** (ranh giới với Bình minh), **mục 11** (tiến độ, phân công, bàn giao, tiêu chí hoàn thành).
