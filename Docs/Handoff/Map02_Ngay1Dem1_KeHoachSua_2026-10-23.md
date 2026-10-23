# Map 2 – Ngày 1 / Đêm 1: rà soát và kế hoạch sửa (bản 2, 2026-10-23)

Bản này **thay bản 1** viết sáng cùng ngày. Bản 1 đọc nhầm code cũ, trước khi `thinh` nhận merge dev (`7c8118c6`, `a9119fcc`). Bản merge đó có phần Ngày 1/Đêm 1 của Khoa (commit `1331bab3`):
- thoại có giọng đọc;
- 16 clip hoạt ảnh;
- mini game kho / bến / chốt;
- ánh sáng trưa → chiều vàng → đêm;
- dàn cảnh nhà chị Lành.

Phần lớn những gì bản 1 ghi "chưa có" thì nay đã có. Bản này rà trên code mới nhất.

**Nguồn đối chiếu:** "ShadowVale Map 2 – Kịch bản chi tiết 3 ngày đêm.docx" (20/10).

**Cách rà:**
- Đọc code: `Map02ChapterDayOne*.cs`, `Map02DayOneState.cs`, `Map02ChapterDirector.cs`, `Map02ChapterActor.cs`, `Map02Mission.Inventory.cs`, `Map02DayOneBuilder.cs`, `Map02PresentationRepair.cs`, `Map02Chapter.json`, `voice-catalog.json`.
- Dựng hình 10 clip của Khoa trong Blender (dữ liệu `SourceArt/Map02DayOne/*.json` trên lưới của Nam). Ảnh nằm ở scratchpad.
- Xem 15 ảnh chụp trong game do bộ test tạo sáng 23/10, ở `Logs/Map02PlayableAudit_20261022`, `Logs/Map02Presentation_20261022` và `Logs/Map02DayOne/Visual`.
- Chạy lại nhóm test liên quan, kết quả ở mục 6.

---

## 0. Quyết định đã chốt (23/10)

| # | Câu hỏi | Chốt |
|---|---|---|
| 1 | Cảnh "Lời hứa" | **Làm theo doc** (xem P1): 2 người ở hiên; chị Lành nói vọng từ bếp; câu đùa "Ngon hơn nồi này nhiều"; camera qua khung cửa; giữ 3 giây với tiếng dế. |
| 2 | Mini game phủ màn hình (kho, bến, chốt) | **Giữ cả ba của Khoa.** Bến và chốt giữ nguyên, chỉ sửa lỗi (L1 mũi tên đèn). Kho giữ cơ chế xếp hình nhưng **làm lại giao diện cho đẹp, tham khảo game cùng loại**, xem P4a. |
| 3 | Tổ gác ở bờ tre | **Thêm tổ gác** (2 du kích). Anh Lực chỉ ở chốt đầu làng, xem P7. |
| 4 | Giọng Nam / Hùng / anh Thành | **Theo Map 2** (VieNeu-TTS: Nam "Adam", Hùng "Minh Quân", Thành "Minh Triết"). Thu lại các câu Map 1 của ba người bằng đúng preset đó, xem P9. |

### Tiến độ

**Bước 1 đã sửa (23/10), chưa commit:**

| Mục | Sửa gì | Ở đâu |
|---|---|---|
| L1 | Mũi tên đèn xoay bằng `Map02Navigation.RotateUi` | `LampBeamMatrix` trong `Map02ChapterDayOne.Gui.cs` |
| L2 | Súng đeo vai phải: nòng lên, báng ở hông. Hướng nòng lấy từ chính khẩu súng (`Weapon.Muzzle`), không phụ thuộc trục xương. | `Map02ChapterDayOne.Presentation.cs` |
| L3 | Mâm cơm chỉ hiện ở Đêm 1 | |
| L4 | Ông Sáu ngồi nghỉ, tay đặt trên đùi (`RestNam`) | |
| L6 | Bát cá trượt sau khi cả hai đã ngồi | |
| L7 | Con vịt ở giữa đường tới bờ tre (giữ vòng 9 m quanh chốt làm dự phòng) | |
| L13 | 2 câu trên thuyền đổi người nói thành "Anh Thành" | catalog, manifest, voice-plan, `BangThoai.md` |
| L14 | Anh Thành đứng đúng mặt bến cả khi bỏ qua cảnh thuyền lẫn khi xem hết cảnh. Mốc đất của slot đặt đúng mặt bến; model giữ nguyên chỗ cảnh thuyền đã dựng. Lần sửa đầu kéo cả model xuống, làm chân lún 10 cm trong cảnh hội thoại trên bờ. | `Map02ChapterDirector.cs` |
| — | Hằng số `EscortPace` | |
| — | Test `FreeMovement` theo tốc độ khiêng | |

**Kết quả test sau bước 1** (chỉ các nhóm liên quan):
- Presentation, Clarity, VoiceCoverage, DayOne*, Cast, ChapterTests, WeaponHoldCapture: qua hết.
- 7/7 test cảnh lên bờ (Arrival): qua.
- `Map02FreeMovementTests` hỏng ở đoạn Ngày 2 dẫn Tâm: test của Tài và code dev của Tuấn Anh mâu thuẫn. Đã xử lý ở bước 2 bên dưới.

**Bước 2 (23/10): đoạn dẫn Tâm chốt theo phương án của Tài.**
- Thêm `LeadingWounded` (đi ×0,55, súng đeo vai, không chạy) và `CompanionLocksGear` (khóa đổi trang bị) trong `Map02ChapterGuidance.cs`. Cả hai bỏ qua Tâm.
- Dùng hai thuộc tính này ở `Map02ChapterDirector.cs`, `Map02ChapterDayOne.Presentation.cs`, `Map02Mission.Character.cs`, `Map02Mission.Inventory.cs`.
- Dẫn Hùng ở Ngày 1–Đêm 1 và Đêm 3 không đổi.
- Test: FreeMovement (đã trả đoạn Tâm về đúng kỳ vọng của Tài), DayTwoState, Clarity, FootstepSync, WeaponHoldCapture, DayOnePlayableFlow — 18/18 qua.
- Tin nhắn cho Khoa, Tài, Tuấn Anh soạn sẵn ở `Docs/Handoff/Map02_TinNhan_Nhom_2026-10-23.md`, chờ Thịnh gửi.

Test mới / sửa:
- `PostLampBeamStartsAtTheLamp…`;
- các assert mới trong `Map02PresentationTests` (mâm, ông Sáu, súng, bát, vịt);
- `Map02DayOneVoiceCoverageTests` (tên người nói).

**Tiếp nhận đoạn Claude dừng (23/10, Codex; chưa commit):**
- Người dùng chọn **chỉ hoàn thiện đoạn đang dở**: máy đàm Hùng, dàn cảnh Lời hứa và tổ gác. Chưa tiếp nhận toàn bộ lộ trình phía dưới.
- Sửa lỗi chuỗi C# ngắt dòng ở test chụp máy đàm; chờ render cập nhật trước khi chụp. Giữ phần đặt máy đàm Claude vừa sửa. Kiểm tra 12 mẫu tư thế/4 hướng quay trên mesh thực, hở mặt áo ít nhất khoảng 7,8 mm tại vùng lấy mẫu; có ảnh 1920×1080.
- Người dùng chốt tổ gác nói **“Được. Qua chốt đầu làng kiểm tra nốt đi.”**, dùng bản thu cũ, đổi tên người nói sang **Tổ gác**. Không dùng câu “Êm re…” chưa có bản thu; script, catalog, manifest và bảng thoại đã đồng bộ.
- Bổ sung câu `m2v3_promise_05b` **“…Ngon hơn nồi này nhiều.”**, VieNeu 3.8.0/preset Minh Quân/seed 2049/48 kHz/1,2 giây. Người dùng đã nghe duyệt và cho đưa vào game. Bộ thoại nay có 50 cue có tiếng.
- 62 test hồi quy ban đầu: 60 đạt, 2 lỗi do thiếu cue trên. Sau sửa chạy lại 28 test: 28 đạt. Lấy kết quả mới nhất theo từng ca: **63/63 ca riêng biệt đạt**. Có test nhịp tự kết thúc, giữ cuối câu ≥3 giây và tải lại trạng thái hoàn thành.
- Không sửa scene Map 1/Map 2, mô hình Nam hay art C3; hash các file lưu hiện có không đổi. Chưa commit/push. Báo cáo và từng ca: `Docs/Reviews/ClaudeContinuation_20261023/KET_QUA.md`, `verification.json`.
- **Không đánh dấu art hoàn tất:** vẫn dùng ghế đẩu và clip MealNam/MealHung; máy đàm là prop khối đơn giản. Ngồi xếp bằng, mũ/ba lô, lớp tay/đũa/hích vai/đẩy nồi, âm nền/nhạc và UI kho vẫn là các bước tiếp theo, cần quy trình duyệt riêng.

---

## 1. Những gì đã đúng doc — giữ nguyên

- **Thứ tự nhiệm vụ** đủ 10 mục: D1_01–06, N1_01–04. D1_03/04/05 làm tùy thứ tự; D1_06 chỉ mở khi xong cả ba.
- **Thoại:** gần như nguyên văn doc, có giọng đọc TTS cho 49 câu. Xưng "tôi – cậu", Hùng gọi "u", dân làng giọng Nam Bộ. Câu "Thương tay chứ đâu phải thương bụng" được ghép vào đầu bữa cơm, hợp lý.
- **Nhịp di chuyển:**
  - Khi dẫn Hùng, Nam đi chậm (×0,55, khóa chạy).
  - Khi khiêng thùng, tốc độ ×0,85 và khóa vũ khí.
  - Hùng không bị kéo xuyên tường hay dịch chuyển tức thời.
- **Đóng cọc:** 2 cọc × 3 nhịp Space, đánh trượt không mất nhịp đã đạt. Đúng tinh thần "không có thất bại".
- **Ánh sáng:** trưa → chiều vàng khi D1_06 mở → đêm trăng lạnh, có đèn dầu ở mâm. Khớp doc.
- **Hệ thống:** save/load giữa chừng mọi việc, checkpoint, ghim nhiệm vụ, chặn biên bản đồ, cứu khi rơi khỏi bản đồ.

---

## 2. Lỗi rõ ràng (sửa nhanh, ai chơi cũng thấy)

| # | Lỗi | Bằng chứng | Nguyên nhân / chỗ sửa |
|---|---|---|---|
| L1 | Mũi tên hướng đèn ở màn kiểm tra chốt không nằm trong ô "ĐÈN". Nó thành một vạch vàng đè lên dòng hướng dẫn phía trên, nên người chơi không thấy đèn đang quay hướng nào. | `post-lamp.png` (1920×1080) | `DrawPost` gọi `GUIUtility.RotateAroundPivot` khi HUD đang co giãn từ lưới 1600×900. Pivot bị tính theo pixel màn hình nên lệch; chỉ đúng khi màn hình đúng 1600×900. Sửa: dùng `Map02Navigation.RotateUi(matrix, at, angle)` như bản đồ nhỏ (`Map02Mission.Hud.cs:123`) đang làm đúng. |
| L2 | Súng "đeo vai" khi khiêng thùng hoặc dẫn Hùng lại treo dọc bên đùi phải, nòng chúi xuống, trông như cầm tay. | `live-carry.png`, `cargo-front.png` | `UpdateWorkWeapon` gắn súng vào ngực rồi mỗi khung lại ghi đè vị trí thế giới (`Player.position + up·1,05 + right·0,3 − forward·0,28`, `Euler(90,0,15)`). Sửa: bỏ dòng ghi đè; dùng vị trí cục bộ trên Chest, đặt chéo lưng (báng ở hông trái, nòng qua vai phải). |
| L3 | Bộ mâm, ghế đẩu, nồi, bát có mặt suốt trưa Ngày 1 và cả Ngày 2. Cảnh thay băng diễn ngay cạnh mâm cơm dọn sẵn. | `bandage.png`, `family-day.png`, `05-day-two-handover.png` | `mealRoot` được tạo một lần, không bao giờ ẩn. Sửa: chỉ bật từ lúc mở N1_01 tới hết Đêm 1. |
| L4 | Ông Sáu ngồi ngoài sân cả ngày, tay duỗi về phía mâm. | `family-day.png` | `ArrangeDayFamily` cho ông dùng clip `MealNam` (Nam đẩy bát) làm tư thế nghỉ. Sửa: tư thế ngồi nghỉ riêng (hai tay trên gối / cầm quạt), và đặt ông trong buồng (xem P5). |
| L5 | Bữa cơm: chị Lành, ông Sáu, bé Tí và Nam cùng chạy một clip `MealNam`. Bốn người cùng duỗi tay trái về mâm, lặp 4 giây/lần, đồng bộ. | `meal.png` | `SampleFamilyMeal` dùng chung clip, chung thời gian. Nếu giữ người nhà ở mâm thì mỗi người cần clip riêng và lệch pha. Xem P1: doc không có họ ở mâm. |
| L6 | Bát cá "nhảy" sang phía Hùng ngay khi bắt đầu ăn, không trượt. | đọc code | `fishBowl` lerp theo `sceneClock/4`, nhưng lúc ngồi xong `sceneClock` đã quá 4 giây vì đi bộ tới chỗ. Sửa: dùng `sceneClock − dayApproachDuration`. |
| L7 | Con vịt (N1_02): | | |
| | – không có tiếng, vì `Resources/Map02DayOne/duck` không tồn tại; | đọc code | |
| | – không có con vịt / bụi lay; | | |
| | – Hùng không giật mình chộp vai; | | |
| | – sự kiện bật khi còn cách chốt 9 m, tức đã gần tới nơi, không phải giữa đường. | | Sửa: xem P6. |
| L8 | Ăn cơm vẫn đội mũ: Hùng đội mũ tai bèo; Nam đội mũ sắt, đeo ba lô. | `meal.png` | Thêm bước "tháo mũ / đặt ba lô / dựng súng vào vách" khi vào mâm, trả lại khi đứng dậy. |
| L9 | Hùng không đeo máy đàm sau lưng. Vậy mà chị Lành nói "Tay vầy mà còn vác máy", và doc cho bé Tí "nhìn máy đàm". Máy đàm cũng là chi tiết cài – trả cho Đêm 3 (Hùng đưa đàm cho Nam). | `bandage.png`, `04-night-start.png` | Gắn prop máy đàm lên lưng Hùng ở Ngày 1–Đêm 1. Hiện có sẵn slot "Hùng's radio — art slot" trong `Map02ChapterFinale.cs`, dùng lại. |
| L10 | Không có âm thanh môi trường nào ở Map 2: không gà gáy, tiếng chèo, trẻ con; không máy bay xa; đêm không dế, ếch, củi nổ. Không có nhạc chủ đề của Hùng. | `Map 2.unity` có 0 AudioSource; code Map 2 không phát ambience | Xem P8. |
| L11 | Giọng nhân vật đổi giữa hai map. Nam, Hùng, chỉ huy ở Map 1 dùng bản thu `Dialogue/VI`; sang Map 2 là giọng TTS khác (Nam "Adam", Hùng "Minh Quân", Thành "Minh Triết"). | `voice-catalog.json` | Cùng một người mà đổi giọng sau một cảnh thuyền. Xem P9. |
| L12 | Chú Bảy và ông Sáu dùng chung một giọng ("Thái Sơn"). Hai ông lại nói ở hai nhiệm vụ cạnh nhau. | `BangThoai.md` | Đổi một trong hai giọng. |
| L14 | Bỏ qua cảnh thuyền thì anh Thành đứng lơ lửng gần 10 cm trên bến. | test `SkippedArrivalUsesSameDrySpawn` | Căn chân Thành khi bàn giao từ `Map02Arrival.Finish` sang chapter, giống cách Khoa đã căn chân Hùng. |
| L13 | Cùng một người mà mang hai tên trong vài phút: "Chỉ huy" trên thuyền, "Anh Thành" trên bờ. | `voice-catalog.json`, `Map02Chapter.json` | Đổi caption 2 câu trên thuyền thành "Anh Thành". |

---

## 3. Lệch doc về cảm xúc và dàn cảnh (quan trọng nhất)

### P1 – Cảnh "Lời hứa" (N1_01) đang sai tinh thần doc

Doc là cảnh mấu chốt của cả Map 2. Mọi cài – trả về sau (Nhịp 5, Bình minh) đều quay về đây.

| Doc | Game hiện tại |
|---|---|
| Hai người ở hiên nhà, ngồi mâm thấp | Mâm 5 người (Nam, Hùng, Lành, Sáu, Tí) trên ghế đẩu giữa sân, trên lối đi |
| Đèn dầu che thấp, chỉ sáng phần mâm | Có đèn, nhưng cả sân sáng đều |
| Camera xa, nhìn qua khung cửa | Góc cao 3/4 trên đầu (chọn "hướng A nhìn đủ năm người") |
| Hùng cầm đũa tay trái, lúng túng; Nam lặng lẽ đẩy chén cá | `MealHung` = ngồi ôm tay, đứng yên 90 giây; không có đũa |
| Hùng hích vai Nam bằng vai trái | Không có |
| Hùng hạ giọng "…Ngon hơn nồi này nhiều." → chị Lành trong bếp "Nghe hết đó nha." → Hùng rụt cổ, Nam bật cười đấm nhẹ vai trái Hùng | Câu đùa bị thay bằng "Bữa đó cả nhà chị Lành cùng ra chơi nữa nhé". Lành ngồi cùng mâm nói "Mời thì nhớ giữ lời. Tí, ngồi gọn lại cho ông ăn". Mất nhịp hài và mất cái đấm vai. |
| "Quê tôi… không còn ai đợi." → Hùng đặt đũa xuống, đẩy nồi canh về phía Nam → giữ hình 3 giây với tiếng dế | Không có đặt đũa, đẩy nồi, giữ hình hay tiếng dế |
| Nhạc solo nhẹ (chủ đề Hùng); tiếng cười và câu "Đợi tới lúc đó…" là file riêng để dùng lại ở Nhịp 5–6 | Không có nhạc; không có file cười riêng |

**Sửa:**
- Trả cảnh về đúng doc: 2 người ngồi bệt trên chiếu / bộ ván ở hiên. Chị Lành nói vọng từ bếp (chỉ tiếng). Ông Sáu và Tí đã ăn xong, trong nhà.
- Trả lại câu đùa và câu của chị Lành theo doc. Câu 05 và 06 phải thu âm lại.
- Dựng thành chuỗi nhịp có thời điểm cố định (timeline đơn giản bằng code là đủ), mỗi nhịp gắn một clip ngắn hoặc một lớp tay (giống `Map02UpperBodyPose`):
  - đũa tay trái;
  - đẩy chén;
  - hích vai;
  - rụt cổ;
  - đấm vai;
  - đặt đũa;
  - đẩy nồi.
- Camera: 1 shot chính tĩnh, đặt ngoài nhà nhìn qua khung cửa. Giữ đúng một trục 180° qua mâm. Thêm 2 shot cận ở "hích vai" và "không còn ai đợi". Kết bằng 3 giây giữ hình với tiếng dế.
- Âm thanh: nhạc solo chỉ trong cảnh này; tiếng cười Hùng và câu "Đợi tới lúc đó…" xuất file riêng.

### P2 – Hùng đi khập khiễng nhưng vết thương ở tay phải

- **Hiện tại:** Khoa đã làm clip `InjuredWalk` "ôm tay phải". Sau đó `Map02PresentationRepair` thay bằng `Hung_DiKhapKhieng` của Map 1, vì clip ôm tay bị lệch hướng thân.
- **Hệ quả:** người chơi thấy Hùng què chân. Doc dặn vết tay phải phải được nhắc liên tục: ăn tay trái, không leo cây, gài bộc phá bằng tay trái.
- **Sửa:** dùng cách khiêng thùng đang chạy tốt, tức chân đi thường + lớp tay (`Map02UpperBodyPose`) với mask chỉ 2 tay:
  - tay phải gập sát bụng;
  - tay trái đỡ khuỷu.

  Không cần clip toàn thân mới, không còn lỗi hướng thân.
- **"Nam dìu":** Khoa đã bỏ vì `AssistWalk` xấu. Đề xuất chỉ dìu ở 2 đoạn ngắn (lúc rời bến, lúc tới hiên), cài bằng IK tay trái của Nam lên khuỷu Hùng. Không dìu suốt đường.

### P3 – Cảnh thay băng (D1_02)

Doc:
- Nam đỡ Hùng ngồi.
- Chị Lành thay băng; Hùng nhăn mặt, tay trái bám mép ghế.
- Bé Tí nấp sau cột nhìn máy đàm, cười khúc khích rồi chạy mất.

Game (`bandage.png` và hình dựng `BandageHung`/`BandageLanh`):
- Hùng ngồi, hai nắm tay chìa ra trước ngực. `BandageHung` y hệt `MealHung`.
- Chị Lành đứng thẳng, hai tay buông trước hông, cách cánh tay Hùng khoảng 0,5 m. Không chạm, không quấn băng.
- Cuộn băng gắn tay phải chị nhưng tay không làm động tác quấn.
- Bé Tí đứng thẳng trên hiên. Ông Sáu đứng ngay sau lưng Hùng.

Sửa:
- `BandageLanh`: cúi người hoặc ngồi xổm ngang tay Hùng, hai tay quấn vòng quanh cẳng tay phải Hùng (IK bám vào xương tay Hùng, không phải điểm cố định).
- `BandageHung`: tay phải đặt lên đùi cho chị quấn, tay trái bám mép ghế, vai co lên ở nhịp "nhăn mặt" (ví dụ giây 6–8).
- Bé Tí: đứng nấp sau cột hiên (đặt điểm `childPeek`). Ở câu 3 ("Nhẹ thì đưa Tí xách thử coi") phát tiếng cười rồi chạy theo NavMesh ra sau nhà.
- Ông Sáu: không có mặt trong cảnh này (ông ở trong buồng, xem P5).

### P4 – Ba mini game toàn màn hình (D1_04 kho, D1_05 bến, N1_02/03 chốt)

Doc chỉ yêu cầu:
- "khiêng 3 thùng từ bến vào hầm dưới bụi chuối";
- "đóng 2 cọc (vòng 3 nhịp lúc chú Bảy giữ cọc) và kê 3 tấm ván, không có trường hợp thất bại";
- "đến chốt và hỏi tổ gác";
- "đến chốt và giao gói thuốc cho anh Lực".

Khoa thêm:
- **Kho:** xếp kiểu Tetris 3 thùng 3×2 / 2×2 / 1×3 trên lưới 6×4, có ô ẩm và lối đi. Trong khi 3 thùng thật trong làng giống hệt nhau, nên câu đố không khớp vật thật.
- **Bến:** sau đóng cọc là chọn 1 trong 5 ván (2 ván bẫy: nứt, ngắn), căn lệch ±7 cm bằng A/D. Nút R xoay luôn cho kết quả sai. Đặt sai bị từ chối kèm lý do.
- **Chốt:** mỗi chốt 3 hạng mục (xoay chụp đèn, khép cửa + cài then, cất bó củi) × 2 chốt.

Vấn đề:
- Cả ba đều phủ panel đen 88% toàn màn hình, nên người chơi rời khỏi làng đúng lúc doc muốn họ "tự đi qua con đường sơ tán lúc còn yên bình" và làm quen người. Thiết kế UI "diegetic" (Fagerholt & Lorentzon, *Beyond the HUD*) khuyên giữ thao tác trong thế giới khi mục tiêu là nhập vai.
- Không thấy chú Bảy giữ cọc (chỉ có dòng chữ "Chú Bảy giữ thấp thân cọc").
- Búa 32 cm cầm một tay, vung nhẹ (hình dựng `Hammer`), không khớp lúc bấm Space.
- Ván bẫy và lệch ±7 cm là trường hợp thất bại trá hình, trái doc.
- Đêm 1 doc chỉ 12–14 phút, không trận đánh. 6 mục kiểm tra UI làm đêm đầu thành checklist.

Đề xuất. Đây là quyết định của nhóm, vì là công Khoa, nên cần anh/chị chốt:
- **Kho:** bỏ lưới Tetris. Thùng thứ 3 đặt xuống là xong; hoạt ảnh đặt thùng `PlaceCrate` đã có sẵn. Nếu muốn giữ chút "suy nghĩ", để 3 chỗ đặt cố định trong hầm, có nhắc E tại chỗ.
- **Bến:** giữ phần đóng cọc nhưng diễn trong thế giới:
  - camera gần;
  - chú Bảy ngồi xổm giữ cọc (clip mới);
  - Nam cầm vồ hai tay;
  - mỗi lần Space là một nhát vồ khớp ngay lúc bấm;
  - vòng nhịp nhỏ hiện trên đầu cọc thay panel.

  Kê ván: E ba lần, mỗi lần một tấm tự vào chỗ, không có ván bẫy.
- **Chốt:**
  - Bờ tre: đến nơi, hỏi tổ gác (2 NPC mới, 1–2 câu bark), có thể giữ một việc nhỏ trong thế giới, ví dụ hạ chụp đèn.
  - Đầu làng: giao thuốc cho anh Lực, chạy 4 câu.
  - Bỏ panel kiểm tra 3 hạng mục.

**Đã chốt (23/10): giữ cả ba bảng của Khoa.**
- Bến và chốt: giữ nguyên, chỉ sửa lỗi mũi tên đèn (L1).
- Kho: giữ cơ chế, làm lại giao diện (P4a).
- Các đề xuất bỏ/rút gọn ở trên không làm.

### P4a – Làm lại giao diện kho kín (D1_04)

**Tham khảo:**
- **Hòm đồ Resident Evil 4 (attaché case):**
  - đồ vật hiện bằng hình thật chiếm đúng số ô;
  - xoay để vừa chỗ;
  - kích thước tỉ lệ với vật thật.

  ([RE Wiki](https://residentevil.fandom.com/wiki/Attache_Case), [Gfinity](https://www.gfinityesports.com/guides/resident-evil-4-remake-inventory-system), [Bloody Disgusting](https://bloody-disgusting.com/video-games/3658221/resident-evil-4-perfected-inventory-system-resident-evil-25/))
- **Túi đồ kiểu lưới (DayZ / Tarkov / mod Tetris inventory):**
  - bóng xem trước tô xanh / đỏ theo ô sẽ chiếm, trước khi bấm;
  - phím R xoay;
  - đặt sai thì trả vật về chỗ cũ.

  ([itch.io design diary](https://itch.io/devlog/1084465/design-diary-tile-based-inventory-system-3.amp), [cfx forum](https://forum.cfx.re/t/opinion-grid-inventory-ui-dayz-style-spatial-tetris/4823453), [Vintage Story – ghost preview](https://www.vintagestory.at/forums/topic/13651-when-placing-down-an-objectholding-not-full-block-placeable-object-display-a-ghost-of-how-it-would-look-placedrotated-to-avoid-placing-stuff-wrongly/))

**Hiện tại** (`02-packing.png`):
- Ô màu phẳng; chữ "LỐI ĐI" lặp 8 lần, "≈ ẨM" lặp 2 lần.
- Thùng là khối nâu ghi "THÙNG 1 3×2".
- Bấm là đặt ngay, không xem trước được.
- Sai thì chỉ báo ở dòng dưới cùng.
- Muốn sửa một thùng phải "Xếp lại" toàn bộ.
- Ba thùng thật trong làng giống hệt nhau, không khớp 3 cỡ trong câu đố.

**Thiết kế mới** (mockup đã gửi trong chat 23/10). Giữ nguyên luật trong `Map02DayOneState` (lưới 6×4, lối đi x<2, ô ẩm, chồng thùng), chỉ đổi phần vẽ và thao tác trong `DrawPacking` / `TickDayKeyboard`:

1. **Tiêu đề:** "Hầm dưới bụi chuối · cất vật tư", đúng địa điểm trong doc. Dòng phụ ghi tiến độ và luật trong một câu.
2. **Sơ đồ hầm nhìn từ trên:**
   - khung tre/đất bao quanh, nền đất tối, đường lưới mảnh;
   - 2 cột lối đi gộp thành một dải "Lối xuống — giữ trống", có bậc thang và mũi tên cửa hầm, nhãn ghi một lần;
   - ô ẩm vẽ vũng nước xanh, nhãn ghi một lần.
3. **Thùng có nội dung, khớp với Ngày 3** (`Map02ChapterDressing`: Ngày 3 lấy đạn + thuốc ra hầm nam và lựu đạn ra hốc bờ đất):

   | Thùng | Cỡ | Loại hòm |
   |---|---|---|
   | Đạn 7,62 | 3×2 | hòm lớn |
   | Thuốc | 2×2 | hòm vuông |
   | Lựu đạn | 1×3 | hòm dài |

   Vẽ ván gỗ nhìn từ trên kèm nhãn hàng.
4. **Bóng xem trước:** rê chuột hoặc mũi tên thì hiện bóng thùng đang cầm. Xanh nếu đặt được; đỏ kèm lý do ngay cạnh bóng nếu không ("nền ẩm ✕", "chắn lối", "chồng thùng").
5. **Thao tác:**
   - Click đặt; chuột phải hoặc R xoay.
   - Click thùng đã đặt để nhấc lên (thay cho "Xếp lại" toàn bộ). Backspace nhấc lên khi dùng phím.
   - Đặt xong tự chọn thùng chưa đặt tiếp theo.
   - Phím cũ giữ nguyên: 1–3, mũi tên, E, Enter.
6. **Cột phải:** 3 thẻ thùng (tên, cỡ, trạng thái đã đặt / chưa / đang cầm). Gợi ý phím gọn 4 dòng. Nút "Cất xong · Enter (n/3)" chỉ sáng màu khi cả 3 hợp lệ.
7. **Thùng thật trong làng khớp cỡ:**
   - đổi kích thước 3 prop trong `BuildDayProps` theo 3×2 / 2×2 / 1×3 (ô ≈ 0,3 m, tức khoảng 0,9×0,6 / 0,6×0,6 / 0,3×0,9 m);
   - `SyncDayProps` đặt thùng trong hầm đúng ô và hướng đã xếp;
   - thùng đang khiêng cũng là đúng hòm đó.
8. **Âm thanh:** tiếng thùng gỗ đặt xuống khi đặt đúng, tiếng "cộc" nhẹ khi đặt sai. Hiện `PlayDaySound` gọi file không tồn tại.
9. **Kích thước chữ / giao diện:** dùng chung style HUD Map 1 / túi đồ Map 2 (`HudPlate`, màu vàng đất); không dùng nút xám mặc định của IMGUI.

**Test:**
- Đặt hợp lệ / sai (ẩm, lối đi, chồng) cho đúng lý do.
- Nhấc lên rồi đặt lại.
- Xoay.
- Save/load giữa chừng giữ vị trí.
- Bóng xem trước đúng ô ở 1600×900 và 1920×1080 (cùng loại lỗi với L1).
- Prop trong hầm khớp ô.

### P5 – Ông Sáu "nói từ trong buồng" (D1_03)

- **Doc:** ông ho và nói vọng ra. Người nhận thuốc là chị Lành.
- **Game:** ông ngồi ghế ngoài sân, đối mặt Nam.
- **Sửa:**
  - Ông ở trong buồng, không hiện hoặc chỉ thấy bóng qua vách.
  - Phát tiếng ho trước câu nói.
  - Nam trao thuốc cho chị Lành (clip `Medicine` đã có). Chị Lành cần một động tác nhận.

### P6 – Con vịt và nỗi sợ của Hùng (N1_02)

- Đặt trigger ở khoảng giữa quãng đường tới bờ tre, cạnh một bụi rậm. Đặt bằng điểm authored trong layout, không tính theo khoảng cách tới chốt.
- Khi Nam và Hùng đi qua:
  - tiếng vịt vỗ cánh + bụi lay;
  - Hùng chạy clip giật mình (mới, dùng lại được cho tiếng động đêm sau), tay trái chộp vai Nam (IK);
  - 2 câu bark không khóa điều khiển.
- Đây là chi tiết duy nhất của Đêm 1 cho thấy Hùng còn sợ sau lần bị bắt. Cài cho "Hùng mất ngủ" ở Đêm 2 và Bình minh.

### P7 – Anh Lực có mặt ở cả hai chốt (N1_02, N1_03)

- **Doc:** bờ tre có "tổ gác" riêng; anh Lực trực chốt đầu làng.
- **Game:** sau bữa cơm, Lực đi ra bờ tre, rồi lại đi sang đầu làng trước Nam.
- **Đã chốt: thêm tổ gác.**
  - 2 du kích cố định ở bờ tre, dùng lại model du kích (`Du kích Lực` đổi vật liệu / khăn, hoặc art `militiaArt` của Đêm 3).
  - Một người ngồi gác cạnh đèn che, một người đứng.
  - Khi Nam tới: 2–3 câu hỏi – đáp ngắn (cần viết thêm vì doc chỉ ghi "hỏi tổ gác", sẽ gửi anh/chị duyệt chữ trước khi thu giọng).
  - Thay câu báo cáo `postbamboo` đang gán cho anh Lực.
  - Lực đứng yên ở chốt đầu làng suốt Đêm 1.

### P8 – Âm thanh môi trường

Thêm `Map02Ambience` theo phase:
- **Ngày 1:** gà gáy xa, chèo khua nước gần bến, trẻ con gọi nhau gần nhà dân, gió tre. Một tiếng máy bay rất xa, một lần, hẹn khoảng phút thứ 8.
- **Đêm 1:** dế, ếch, củi nổ gần bếp nhà chị Lành.
- **Không có nhạc chiến đấu ở phase 0–1.**

Nguồn: thư viện miễn phí (Freesound CC0, Sonniss GDC bundle), ghi giấy phép vào `SourceArt/Audio/Map02/LICENSES.md`.

### P9 – Giọng đọc thống nhất giữa Map 1 và Map 2

**Đã chốt: theo Map 2.**

- Thu lại các câu Map 1 đang dùng `Dialogue/VI` của ba người: khoảng 24 câu Nam, 8 câu Hùng, 13 câu "Chỉ huy".
- Dùng VieNeu-TTS v3 Turbo, đúng preset và seed như Map 2, theo `SourceArt/Dialogue/Map02DayOneFree/generate_offline.py` + `voice-manifest.json`.
- Ghi hash vào manifest.
- Đổi `resource` trong `voice-catalog.json`, cả hai bản `Assets/Resources/Dialogue` và `SourceArt/Dialogue`.
- Giữ nguyên câu chữ và caption.
- Test: `DialogueVoiceTests`, `Map01StoryVoiceTests` phải phát đủ câu, đúng người nói.
- Cần cài model (tải từ HuggingFace) nên sẽ hỏi trước khi tải.

### P10 – Những điểm còn thiếu khác

- **Cập bến:** thiếu nhịp "Hùng bước lên, hụt chân. Nam đặt ba lô xuống trước rồi mới đỡ". Thêm vào `Map02Arrival` (pha Disembarking), khoảng 4 giây.
- **Câu thêm khi cập bến:** anh Thành có thêm câu giao việc dài ("Trong lúc chờ, giao thuốc, cất vật tư…"). Doc không có, nhưng giúp người chơi biết việc, có thể giữ. Doc có "Nam: Rõ." sau câu đầu của Thành, nay chỉ còn ở cuối.
- **Đổi ca (N1_04):**
  - Nam ngồi bằng tư thế "ngồi ghế thuyền" cạnh chiếu (hình dựng `RestNam`), tức ngồi trên không nếu không có bậc.
  - Doc: Nam ngồi ở ngưỡng cửa, quay lưng vào nhà.
  - Hùng nằm co gối (hình dựng `RestHung`).
  - Sửa: đặt điểm `doorSill` ở bậc cửa nhà chị Lành; clip ngồi bậc (chân thả xuống bậc dưới); camera trong nhà nhìn ra lưng Nam và ánh trăng; Hùng nằm thẳng, mắt mở (không cần mặt, đầu hướng lên mái lá).
- **Gói thuốc:** doc nói "từ túi đồ của Map 1". Game dùng 2 gói thuốc nhiệm vụ riêng (không lấy medkit của Map 1). Chấp nhận được; cần chốt.
- **Đạo cụ:** bát là hình cầu xanh, nồi là trụ đen, ghế là khối hộp. Ở cảnh cận "Lời hứa" nên thay bằng mô hình thật (mâm gỗ, nồi đất, chén sành, đũa tre, đèn dầu có chụp).
- **Còn phải xem trong game khi chạy được:**
  - Một khối hộp tối cao khoảng 1 m nằm giữa đường lúc bắt đầu Đêm 1 (`04-night-start.png`).
  - Nền chữ nhật nâu phẳng không texture dưới chân Nam lúc lên bờ (`00-arrival.png`).

---

## 4. Danh sách hoạt ảnh

Đánh giá clip hiện có (hình dựng Blender):

| Clip | Đánh giá |
|---|---|
| `InjuredIdle` | Ôm tay phải đúng ý. Dùng được. |
| `MealHung` = `BandageHung` | Một tư thế ngồi ôm tay, đứng yên 4 giây. Không có đũa, không có động tác nào. |
| `MealNam` | Tay trái duỗi thẳng ra trước suốt clip, lặp 4 giây. |
| `BandageLanh` | Đứng thẳng, tay buông trước hông, gần như không động. |
| `Hammer` | Một tay, biên độ nhỏ, thân không tham gia. |
| `RestNam` | Tư thế ngồi ghế thuyền. |
| `RestHung` | Ngả ra nằm nhưng gối vẫn co. |
| `Carry` | Phần tay trong dữ liệu gốc không ôm thùng. Trong game thùng gắn giữa hai tay nên tạm ổn (`carry-slung.png`). |

Cần làm (theo luật working-copy: .blend riêng, không ghi đè master/FBX game tới khi duyệt):

| Ưu tiên | Clip / lớp | Dùng ở |
|---|---|---|
| A | Lớp tay Hùng "ôm tay phải" chạy trên clip đi thường (thay khập khiễng) | D1_02, N1_02–04, các ngày sau |
| A | Bộ nhịp "Lời hứa". Hùng: cầm đũa tay trái, hích vai, rụt cổ, đặt đũa, đẩy nồi. Nam: đẩy chén, đấm nhẹ vai. Cả hai: ngồi bệt xếp bằng | N1_01 |
| A | Hùng giật mình chộp vai (dùng lại được) | N1_02, đêm sau |
| B | Chị Lành quấn băng (cúi/xổm, IK theo tay Hùng); Hùng tay trái bám ghế + co vai | D1_02 |
| B | Chú Bảy ngồi xổm giữ cọc; Nam vồ hai tay khớp nhịp | D1_05 |
| B | Nam ngồi bậc cửa; Hùng nằm thẳng | N1_04 |
| B | Ông Sáu ngồi nghỉ (thay `MealNam`); chị Lành nhận gói thuốc | D1_03, nền Ngày 1 |
| C | Hùng hụt chân + Nam đặt ba lô đỡ | Cập bến |
| C | Bé Tí nấp cột, cười, chạy | D1_02 |
| C | Tháo mũ / đặt ba lô / dựng súng khi vào mâm | N1_01 |

---

## 5. Thứ tự làm đề xuất

**Cập nhật lượt hoàn tất bước 3 (23/10):** người dùng yêu cầu làm xong bước 3 rồi ghép lên `dev`. Giữ nguyên các quyết định đã duyệt ở trên. Đã bổ sung bản thu dế CC0 cho đoạn giữ hình sau câu cuối, fade vào/ra, dừng khi kết thúc/bỏ qua/tải lại và theo tạm dừng. Xóa nhãn chờ thu âm còn sót ở các câu đã có bản thu được duyệt. Kết quả kiểm tra và phạm vi ghép được ghi tại `Docs/Reviews/ClaudeContinuation_20261023/BUOC_3.md`.

Bước 3 ở đây là dàn cảnh bằng code, camera, nhịp thoại/giữ hình và tổ gác. Các clip ngồi xếp bằng, đũa/hích vai/đẩy nồi và art chi tiết vẫn thuộc bước 4 trở đi; giữ nguyên yêu cầu duyệt Blender trước khi xuất. Phần này không phải nghiệm thu art cuối hoặc toàn bộ Ngày 1–Đêm 1.

1. **Lỗi rõ ràng, chỉ code, khoảng 1 buổi:**
   - L1 mũi tên đèn;
   - L2 súng đeo lưng;
   - L3 ẩn mâm ngoài Đêm 1;
   - L4 tư thế ông Sáu;
   - L6 bát cá;
   - L7 trigger vịt giữa đường (tạm dùng câu thoại có sẵn);
   - L13 tên "Anh Thành";
   - L14 chân anh Thành lơ lửng sau khi bỏ qua cảnh thuyền;
   - sửa test `FreeMovement` theo tốc độ khiêng ×0,85.

   Test kèm (chỉ đúng phần đổi):
   - mũi tên đèn nằm trong ô ở 1920×1080 và 1600×900;
   - súng ở sau lưng (vị trí cục bộ so với Chest);
   - `mealRoot` tắt ở phase 0 và phase 2;
   - bát trượt liên tục;
   - vịt bật đúng một lần ở điểm giữa đường.
2. **Thiết kế đã chốt 23/10** (mục 0). Báo cho Khoa biết các thay đổi trên phần của Khoa: cảnh "Lời hứa" về 2 người, kho làm lại giao diện, thêm tổ gác.
3. **Dàn cảnh lại "Lời hứa"** theo doc: vị trí hiên, camera qua khung cửa, nhịp thời gian, thoại 05–06, tiếng dế + giữ 3 giây. **Thêm tổ gác bờ tre** (P7).
4. **Hoạt ảnh nhóm A**, gồm lớp tay Hùng ôm tay phải, bộ nhịp "Lời hứa" và giật mình. Duyệt bằng ảnh trước khi đưa vào game.
5. **Âm thanh:** ambience theo phase, nhạc chủ đề Hùng, tiếng vịt, tiếng ho, tiếng cười Hùng tách file.
6. **Làm lại giao diện kho kín** (P4a) và đổi cỡ 3 thùng thật; hoạt ảnh nhóm B (thay băng, đóng cọc, đổi ca, ông Sáu).
7. **Nhóm C và đạo cụ thật cho cảnh cận.** Thu âm:
   - các câu đổi chữ (promise 05–06);
   - câu tổ gác;
   - 45 câu Map 1 của Nam / Hùng / chỉ huy theo giọng Map 2 (P9).
8. **Chơi thử cả Ngày 1–Đêm 1 không tua** (lượt test hiện chạy ở timescale 2), đo thời lượng với mục tiêu doc 18–22 phút + 12–14 phút.

---

## 6. Kết quả test (23/10, code sau merge `a9119fcc`)

**Phạm vi:** 13 nhóm test liên quan Ngày 1/Đêm 1:
- DayOne*
- Presentation
- ArrivalTests
- ChapterTests
- ChapterMechanics
- Clarity
- Cast
- BridgeCollision
- FreeMovement
- SuppliesAndCast
- StartSaveExport
- BoundaryGuidance
- WeaponHoldCapture

**Kết quả:** 69 test, 67 qua, 2 hỏng. Lượt chạy không làm thay đổi file nào đang được git theo dõi.

| Test hỏng | Lỗi | Đánh giá |
|---|---|---|
| `Map02ArrivalTests.SkippedArrivalUsesSameDrySpawn` | Bỏ qua cảnh thuyền thì đế giày **anh Thành** (actor 1) lơ lửng 9,8 cm trên mặt bến (giới hạn 2 cm). | **Lỗi thật**, người chơi thấy ngay ở cảnh lên bờ. Thêm vào nhóm sửa bước 1 (L14). |
| `Map02FreeMovementTests.CargoEscortAndHeldOpticsKeepRealPlayerMovementAndAnimation` | Test đòi tốc độ ×1 và cho chạy khi đang khiêng / dẫn người; code dev đặt ×0,85 khi khiêng (commit `47ece602`). | **Test cũ lệch thiết kế.** Doc muốn Nam chậm lại khi khiêng, nên sửa test theo `CarryPace` (và ×0,55 khi dẫn Hùng), không sửa game. |

Ba test Cast và test Bé Tí cạnh mẹ hỏng ở lượt sáng (chạy giữa lúc đang merge) nay **đã qua**.

---

## 7. Tham khảo

- Escort/đồng hành:
  - Irrational giữ Elizabeth ở phía trước người chơi, cho cô quay lại nhìn người chơi và tự tương tác với đồ vật có hẹn giờ, để không thành "người dẫn đường" máy móc ([Game Informer](https://gameinformer.com/b/news/archive/2014/03/18/by-your-side-irrational-discusses-how-it-kept-players-connected-with-elizabeth); [GDC Vault](https://gdcvault.com/play/1020831/Bringing-BioShock-Infinite-s-Elizabeth)).
  - Escort khó chịu nhất khi người được hộ tống quá chậm hoặc người chơi mất quyền chỉnh nhịp ([Game Developer – escort mission design](https://www.gamedeveloper.com/design/can-we-fix-escort-mission-game-design-); [Game Wisdom](https://game-wisdom.com/critical/escort-mission)).
  - Game hiện khóa chạy và cho Nam đi ×0,55, đúng hướng này.
- UI trong thế giới vs. UI phủ màn hình: [Fagerholt & Lorentzon, *Beyond the HUD* (Chalmers 2009)](https://odr.chalmers.se/handle/20.500.12380/111921).
- Rõ mục tiêu / bước tiếp theo: [Xbox Accessibility Guideline 109](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/109). Khoa đã dùng cho HUD nhiệm vụ.
- Quy tắc trục 180° cho cảnh hội thoại / bàn ăn: [180 degree rule](https://www.showmeshorts.co.nz/mini-movie-making-7180degreerule).
