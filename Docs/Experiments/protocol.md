# Quy trình thí nghiệm: solver điều phối và AI trong game

Có hai cấp đo, ứng với các câu hỏi nghiên cứu RQ2–RQ4:

| Cấp | Đo gì | Dữ liệu | Tất định? |
|---|---|---|---|
| **A. Solver offline** | Chất lượng nghiệm, độ trễ, vùng khả thi (RQ2, RQ3) | Các instance QUBO mà game **thực sự** gặp (`research/coordination-*.jsonl`) cộng với instance sinh theo seed | Tuyệt đối: cùng instance, cùng seed |
| **B. Trong game** | Capture rate, escape time, coordination score, độ khó bị khai thác (RQ4) | Replay harness với exploit bot (`research/replay-*.jsonl`) | Theo từng run: bước thời gian cố định, seed cố định; so sánh bằng thống kê trên nhiều run |

## 0. Trước khi chạy số liệu chính thức

1. **Tuning công bằng** (`svl-bench tune`):
   - GA, SA, SQA và QIEA được thử *cùng số cấu hình* (30, gồm cả cấu hình mặc định), trên một
     **tập tuning riêng**: 8 bài sinh theo seed 901+ và khoảng ¼ số bài ghi từ game. Bài nào thuộc
     tập tuning được xác định bằng hash của `instance_id`; `svl-bench game` tự loại các bài này ra.
   - Mỗi cấu hình được chấm bằng độ lệch trung bình so với nghiệm tối ưu: dùng nghiệm `exact` nếu
     chứng minh được, nếu không thì dùng nghiệm tốt nhất từng thấy.
   - 3 cấu hình tốt nhất **cùng với cấu hình mặc định** được chạy lại 3 lần; cấu hình có trung bình
     tốt nhất thắng.
   - Báo cáo nằm ở `shadowvale-solver/experiments/tuning/` (`trials.csv`, `tuned.json`). Tham số
     thắng được đặt làm mặc định trong code solver.
2. **Hiệu chỉnh bot** (`ReplayHarnessTests.CalibrateBots`):
   - Mỗi bot có một "núm độ khó" là thói quen tự nhiên của người chơi: thời gian phản ứng trước khi
     bỏ chạy (`same_route`, `stone_lure`), hoặc số giây nấp trong bụi (`same_bush`).
   - Núm được hiệu chỉnh **trên greedy**, dùng seed 500+ (tách khỏi seed 1000+ của thí nghiệm), sao
     cho khoảng 50% số lượt bị bắt. Bot lần nào cũng bị bắt hoặc lần nào cũng thoát thì không thể
     cho thấy khác biệt giữa các solver.
   - Giá trị chọn được ghi vào `ReplayScenario.DefaultKnob` **trước khi** so sánh solver.
3. **Phiên bản code:**
   - Mỗi dòng CSV của bench có `code_version` (phiên bản package + commit của repo solver, thêm
     `.dirty` nếu có thay đổi chưa commit).
   - Mỗi dòng replay có `game_version` (commit của repo game) và `knob`.
   - Số liệu chính thức chỉ chạy trên commit sạch (không `.dirty`), có gắn tag, ví dụ `exp-v1` ở
     cả hai repo.
4. **Cùng một máy cho mọi số liệu về độ trễ.** Không chạy Unity song song với bench hay tuning:
   budget là thời gian thật, CPU bị chia thì số đo sai.

### Khi nào phải chạy lại

| Thay đổi | Chạy lại |
|---|---|
| Công thức QUBO (trọng số, hạng tử, λ) | Tất cả: tuning, cấp A, cấp B. Xuất lại fixture parity |
| Thuật toán hoặc tham số của một solver | Solver đó: cấp A, và cấp B cho variant đó |
| Thêm solver mới | Tune solver mới với cùng số lần thử, rồi chạy nó ở cấp A và B |
| AI trong game (FSM, perception, graph, map) hoặc bot | Hiệu chỉnh bot, rồi chạy lại cấp B cho mọi variant |
| Đổi máy | Mọi số liệu về độ trễ |
| Chỉ sửa thống kê hoặc biểu đồ | Không chạy lại, tính lại từ file kết quả |

Với nhiều solver, dùng `svl-bench compare --all greedy,ga,sa,sqa,qiea`: mỗi cặp được kiểm định
Wilcoxon và có p hiệu chỉnh Holm (`p_holm`), giữ sai số loại I của cả họ phép thử ở mức 5%.

## A. Solver offline

1. Thu instance: mỗi lần squad re-plan trong game (chơi thật hoặc qua harness), `CoordinationRecorder`
   ghi đủ dữ liệu đầu vào để dựng lại QUBO. Test `test_game_records.py` khẳng định Python dựng lại
   đúng QUBO mà C# đã giải.
2. Chạy:
   ```
   svl-bench game research/coordination-*.jsonl --solvers greedy,ga,sa,sqa,qiea --repeats 5 --budget-ms 120
   ```
3. So sánh từng cặp: `svl-bench compare experiments/results/game.csv --a sqa --b sa --metric gap`.
   Dùng Wilcoxon ghép cặp theo (instance, repeat) và khoảng tin cậy 95% bằng bootstrap.
4. **So sánh cùng mức tính toán (matched compute):** mọi solver chạy cùng budget wall-clock và cùng
   số luồng CPU. Báo cáo kèm `samples_evaluated` để biết mỗi solver đánh giá năng lượng bao nhiêu lần.
5. **QAOA** chỉ chạy được khi instance ≤ 18 qubit. Lớn hơn thì solver từ chối kèm lý do và tính vào
   "vùng không khả thi". Đây là dữ liệu trả lời RQ3.

## B. Trong game: replay harness

Harness nằm ở `Assets/_Project/Map01/Tests/Replay`. Bot điều khiển Nam thay cho bàn phím, đi trên
NavMesh và phát tiếng bước chân y như người chơi.

| Bot | Chiêu khai thác |
|---|---|
| `same_route` | Lần nào cũng đi thẳng qua trại theo cùng một lối; bị phát hiện thì chạy tiếp theo lối đó |
| `same_bush` | Lộ mặt ở rìa trại, rồi lần nào cũng chạy vào cùng một bụi và nấp |
| `stone_lure` | Ném đá dụ lính sang một bên, rồi lén đi qua phía bên kia; bị phát hiện thì bỏ chạy |

- Seed chỉ thay đổi rất ít: lệch hướng ±6° và chờ 0–6 s trước khi xuất phát, để lính đang ở điểm
  khác nhau trên đường tuần tra. **Bản thân chiêu khai thác không đổi.**
- Run thứ *i* dùng **cùng seed cho mọi variant**, nên các variant được so sánh theo cặp.
- Chạy nước rút tối đa 8 s, đúng bằng thể lực của Nam.
- Bước thời gian cố định (`Time.captureFramerate`, mặc định 30 fps). AI không dùng RNG không seed.
- Lời giải từ solver về vào frame nào thì không cố định tuyệt đối: solver chạy trên luồng phụ,
  remote solver đi qua HTTP. Vì vậy cấp B so sánh bằng thống kê trên nhiều run, còn cấp A mới là
  phép so sánh tất định.

Chạy (cần sidecar cho các variant khác greedy, ví dụ `svl-api --port 8001`):
```
SVL_REPLAY_RUNS=30 SVL_REPLAY_VARIANTS=greedy,qiea,sqa \
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -assemblyNames ShadowVale.Map01.Tests \
      -testFilter ShadowVale.Map01.Tests.Replay.ReplayHarnessTests.ExploitBotMatrix
svl-bench replay <persistentDataPath>/research/replay-*.jsonl --a qiea --b greedy
```

Các biến môi trường khác:
- `SVL_REPLAY_BOTS`: bot nào chạy (mặc định cả ba).
- `SVL_REPLAY_CAMP`: tấn công trại nào (mặc định 1 = C2).
- `SVL_REPLAY_SEED`: seed đầu tiên (mặc định 1000).
- `SVL_REPLAY_FPS`: bước thời gian (mặc định 30).
- `SVL_REPLAY_OUT`: file kết quả.

Mỗi run cũng ghi thêm instance vào `coordination-*.jsonl`, nên chạy harness đồng thời thu dữ liệu cho cấp A.

### Chỉ số (mỗi run, tính trong game bởi `TelemetrySession`)

- **Kết quả run:**
  - `captured`: Nam chết.
  - `escaped`: bị phát hiện nhưng sống sót.
  - `undetected`: không bị phát hiện lần nào.
- **Capture rate:** tỉ lệ run có kết quả `captured`, kèm khoảng tin cậy Wilson 95%.
- **Escape time:** từ lúc bị phát hiện đến lúc không còn lính nào ở TakeCover, Engage hoặc Retreat.
- **HP còn lại:** đo "suýt bị bắt" bằng một số liên tục; nhạy hơn capture khi capture bão hoà ở 0% hoặc 100%.
- **Coordination score:** tỉ lệ escape node được ít nhất một vị trí được giao nhìn thấy, lấy trung bình qua các lần re-plan.
- **Độ khó bị khai thác:**
  - (a) *plan entropy*: entropy của plan và của vị trí từng lính qua các lần re-plan;
  - (b) capture rate trước từng exploit bot.
- **`valid_for_comparison`** là false khi run bị đổi variant giữa chừng, hoặc có hơn 20% plan phải fallback
  về greedy. Khi đó run đo greedy chứ không đo variant đang xét, nên bị loại khỏi so sánh (trừ khi
  chạy với `--include-invalid`).

### Kiểm định

- `svl-bench replay` so cặp theo (bot, seed) bằng Wilcoxon signed-rank trên `captured`, `player_hp`
  và `escape_time_mean`, kèm khoảng tin cậy bootstrap 95% của hiệu số.
- Mỗi tổ hợp bot × variant cần **≥ 30 run**.
- Báo cáo đủ p-value và khoảng tin cậy, không chỉ báo "có ý nghĩa".
