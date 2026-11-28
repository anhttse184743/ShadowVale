# Map 2 — Test lại các điểm rà soát 26/10 trên dev mới (25/11/2026)

Mốc: `thinh` = `origin/dev` `95437f16` (sau 39 commit của Tài/Khoa) + việc sửa dở chưa commit trên máy.
Đối chiếu: [Rà soát 26/10](../../Handoff/Map02_3NgayDem_RaSoat_KeHoach_2026-10-26.md) — Q1–Q5 và ma trận mục 8.

## Lượt 1 — dự án chính (dev + việc sửa dở)

13 nhóm test, Unity batch EditMode, `ShadowVale.Map02.Tests`: **75 ca, 71 đạt, 4 lỗi**, khoảng 10 phút.
Bộ lọc: `filter.txt`; kết quả: `results.xml`; log: `unity.log`.

Nhóm đã chạy: DayTwoState (Q1 ăng-ten, Q2 đèn), DayTwoRoute (hai đường Ngày 2), NightTwoRoute (Q3 kiểm kho/tuyến),
ReportAndNightPatrol (Q2), Performance (Q4), Finale (Q5), Chapter, ChapterMechanics, RoutePreview, CheckpointRetry,
Ending, SuppliesAndCast, WorldState.

## Lượt 2 — 4 ca lỗi chạy lại trên bản dev sạch

Worktree tách rời `D:\DO_AN_GAME\SV_devclean_20261125` ở đúng `origin/dev`, không có việc sửa dở: **4 ca, 1 đạt, 3 lỗi**.

| Ca | Dự án chính | Dev sạch | Kết luận |
|---|---|---|---|
| `Map02ReportAndNightPatrolTests.ScoutReportsWithoutMovingCommanderAndNightPatrolIsVisible` (Q2, Đêm 2) | Lỗi `:71` — cầm ống nhòm nhìn tổ tuần tra 5 s mà N2_01 không hoàn thành | Lỗi y hệt | **Lỗi trên dev.** Quan sát tính ở `Map02ChapterNightPatrol.cs:183–188`. Tài sửa script tuần tra 25/11 (`e5b66826`). |
| `Map02ChapterMechanicsTests.TankAndBoatsAreWhereTheNightThreeStoryPutsThem` (Đêm 3) | Lỗi `:257` — đường mương → điểm đặt bộc phá 32,87 m, ngưỡng < 30 m | Lỗi y hệt (32,87 m) | **Lỗi trên dev.** NavMesh/vị trí `ditch` hoặc `ChargePoint` lệch 2,9 m so với ngưỡng. |
| `Map02SuppliesAndCastTests.TheCastStandsAsTallAsNamWithTheirFeetOnTheGround` | Lỗi `:122` — Anh Thành cao 1,15 lần Nam (test sửa dở T028 mong 1,02) | Lỗi test gốc — Anh Thành 0,93 lần Nam (mong 1,00 ± 0,05) | **Lỗi trên dev**: model Anh Thành lệch tỉ lệ ở cả hai cách đo. |
| `Map02PerformanceTests.DayTwoReactionAndNightSleepRestoreTheirActorsOnFinishSkipAndCancel` (Q4, Đêm 2 ngủ) | Lỗi `:134` — đầu Hùng lệch gối 0,205 m, ngưỡng 0,16 m | **Đạt** | **Do việc sửa dở trên máy**, không phải dev. Nghi `Art/Map02DayOne/hung.prefab` (+142 dòng chưa commit) hoặc `Map02ChapterActor.cs` (đã stage). |

## Ghi chú

- Q1 (leo ăng-ten), Q3 (tuyến kiểm kho Đêm 2), Q5 (tua cảnh cuối), hai đường Ngày 2, checkpoint, vật tư, kết cục, save/load: các ca chạy đều đạt.
- Đây là test logic/vị trí; chưa chấm thẩm mỹ, chưa đo FPS, chưa chơi thời gian thật.
- Không sửa code nào trong lượt test này.

## Sửa 3 lỗi trên dev (cùng ngày, theo yêu cầu "sửa cả 3")

| Lỗi | Nguyên nhân tìm được | Sửa |
|---|---|---|
| Q2 — quan sát tuần tra Đêm 2 | **Không tái hiện được.** Log từng khung: ống nhòm, đứng yên, tổ lính, bán kính 40 m, góc 0°, tầm nhìn đều đúng; quan sát xong sau 2,0 s. Chạy lại 5 lần (riêng lẻ và chung nhóm) đều đạt. Hai lần lỗi trước đều là lượt đầu sau khi chép Library/nhập lại asset. | Không đổi code. Ghi là ca **chập chờn**, theo dõi nếu lặp lại. |
| Đêm 3 — mương → bộc phá 32,87 m | Hàng rào làng + cổng của Khoa (`212a6dd7`, 23/11) chạy dọc x = −43 và khoét NavMesh. Đường bò của Hùng (`HungCanal`) cắt hàng rào ở z ≈ −78, cách cổng gần nhất (z = −70) 8 m. Hùng di chuyển theo đường vẽ sẵn (không theo NavMesh) nên **trong game Hùng bò xuyên hàng rào**. | Mở các đoạn hàng rào nằm trên đường mương: `Map02ChapterDayOne.NightFence.cs` (`OpenNightFenceAlong`, đo khoảng cách đoạn–đoạn trên mặt phẳng < 0,6 m), gọi khi dựng mương (`Map02ChapterCover.cs` `BuildCanal`, cả nhánh có điểm đặt tay) và khi xe tăng đã đỗ và quay xong ở bến (`Map02ChapterTank.cs`). |
| Anh Thành thấp | Đo lại cả lưới: Anh Thành 1,68 m, các người lớn khác 1,85–1,90 m; ngón chân→đầu = 0,937 lần Nam (5 người lớn khác trong ±5 %). Lần chỉnh tỉ lệ T028 (13/11, scale 1,1575) dường như dính lỗi đo nhân tỉ lệ hai lần: cách đo lưới thứ hai cho Lực/Thành ra đúng chiều cao thật × tỉ lệ gốc. | `AlliedCommanderVisual.prefab` scale gốc 1,1575 → **1,235** (= 1,1575 × 1,716/1,608). Dùng chung cho chỉ huy Map 1 và Anh Thành Map 2. Chưa duyệt bằng mắt. |

### Kiểm sau sửa

- **Bản dev sạch + 4 file sửa:** Map 2 gồm ChapterMechanics, SuppliesAndCast, ReportAndNightPatrol, AlliedCommanderConsistency, NightWatchModel, Finale: **20/20 đạt**. Map 1 `CommanderRowerTests` (chỉ huy chèo thuyền): **1/1 đạt**.
- **Dự án chính (dev + việc sửa dở), 15 nhóm:** **77 ca, 75 đạt, 2 lỗi**, cả hai do việc sửa dở chưa commit, không do dev:
  - `Map02PerformanceTests…NightSleep…`: đầu Hùng lệch gối 0,205 m (đạt trên dev sạch). Nghi `hung.prefab` / `Map02ChapterActor.cs` sửa dở.
  - `Map02SuppliesAndCastTests…`: bản test sửa dở T028 đo "chiều cao nhìn thấy" bằng cách đo dính lỗi nhân tỉ lệ, nay ra 1,309 (trên dev sạch, test gốc đạt).
- Kết quả: `results_sau_sua.xml`, `unity_sau_sua.log`. Worktree tạm `SV_devclean_20261125` đã xoá.
- Chưa xem bằng mắt: khoảng hở hàng rào ở Đêm 3, chỉ huy cao hơn trong cảnh mở đầu Map 1 (thuyền/mái chèo) và cảnh Map 2.

## Sửa 2 lỗi do việc sửa dở (26/11)

`hung.prefab` sửa dở **không phải đồ thừa**: đó là bản cài T042 (Hùng Meshy 62 xương, giám đốc duyệt 21/11 15:17, chờ commit gom). Giữ nguyên; sửa code cho khớp Hùng mới.

| Lỗi | Nguyên nhân | Sửa |
|---|---|---|
| Đêm 2 ngủ — đầu Hùng lệch gối 0,205 m | Cảnh ngủ (Tài, 25/11) đặt gối cứng ở z = 0,69 m theo Hùng cũ; Hùng mới thân ngắn (hông→đầu 0,497 m) nên đầu ở z = 0,485 m và thấp hơn mặt gối. Sửa gối xong thì lộ tiếp: chăn kéo gần trùm mặt (mép chăn cách đầu 9,8 cm, cần > 12 cm). | `Map02ChapterNightRest.cs`: gối đặt theo chiều dài cột sống thật (`NightPillowDistance`); cổ nâng đầu tới mặt gối (`RestHeadOnPillow`); chăn dừng cách đầu 16 cm (`ShapeNightBlanket`). Sau sửa: đầu lệch tâm gối 2,9 cm, cao 6,5 cm trên tâm gối. |
| Test chiều cao (bản sửa dở T028 đo sai) | Cách đo "chiều cao nhìn thấy" dính lỗi nhân tỉ lệ. | Trả `Map02SuppliesAndCastTests.cs` về bản dev (bản sửa dở còn ở nhánh `backup/thinh-wip-2026-11-25`). |
| Lộ thêm sau khi trả test: chân Hùng mới lơ 5,7 cm ở Ngày 3 | Tư thế khập khiễng ban ngày (`dayIdle`) đặt chân theo độ cao điểm NavMesh, ở đây cao hơn mặt đất thật 2,5 cm; các nhân vật khác đi nhánh có dò mặt đất. | `Map02ChapterActor.cs`: tách `RenderedFloor()` và dùng cho cả nhánh `dayIdle`. |

**Kết quả cuối trên dự án chính:** 18 nhóm, **102 ca, 101 đạt**. Ca còn lại là Q2 (quan sát tuần tra) — chập chờn: tổng cộng 3 lần lỗi / 9 lần chạy, chưa bắt được khung lỗi (lượt có log từng khung trong đúng chuỗi test dài lại đạt). Chưa sửa Q2.
