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
| Coordinator, sidecar (cách nhận/xếp hàng request) hoặc harness | Cấp B cho mọi variant. Cấp A không đổi: nó gọi thẳng solver, không qua sidecar |
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
- **Độ trễ lockstep** (`SquadCoordinator.LockstepLatency`, harness luôn bật). Bước cố định làm game
  chạy nhanh gấp ~4–5 lần thời gian thật. Nếu để nguyên, một lần giải 110 ms thật sẽ dài gần một
  giây trong game, và solver từ xa bị phạt bởi một độ trễ mà người chơi thật không bao giờ thấy.
  Ở chế độ lockstep, game dừng (theo thời gian thật) để chờ các lời giải bắt đầu trong frame đó,
  rồi áp từng plan sau đúng độ trễ thật của nó, tính bằng thời gian game. Solver chạy trong Unity
  (greedy) xong ngay nên không đổi gì.
- **Nhóm đối chứng `none`:** lính vẫn báo nhau qua radio nhưng không ai lập plan (chỉ FSM). Nhờ
  nhóm này mới trả lời được câu hỏi "điều phối có giúp lính không", chứ không chỉ "solver nào hơn".

Chạy (cần sidecar cho các variant khác greedy, ví dụ `svl-api --port 8001`):
```
SVL_REPLAY_RUNS=30 SVL_REPLAY_VARIANTS=none,greedy,qiea,sqa \
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -assemblyNames ShadowVale.Map01.Tests \
      -testFilter ShadowVale.Map01.Tests.Replay.ReplayHarnessTests.ExploitBotMatrix
svl-bench replay <persistentDataPath>/research/replay-*.jsonl --vs greedy,none
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
- **Chẩn đoán** (mỗi run): `plans_discarded` và `discard_reasons` (plan bị bỏ vì người chơi đã chạy
  xa hoặc lính thôi đánh), `fallbacks` và `fallback_reasons`, `first_plan_delay_s` (từ lúc bị phát
  hiện đến plan đầu tiên, giây game), `lockstep_latency`.
- **`valid_for_comparison`** là false khi run bị đổi variant giữa chừng, hoặc có hơn 20% plan phải fallback
  về greedy.
- **Phân tích chính lấy mọi run** (intention-to-treat). Một solver hay timeout khi chơi thì *đúng là*
  kém hơn khi chơi; bỏ các run xấu của nó sẽ thiên vị có lợi cho nó. Chỉ lấy run hợp lệ
  (`--valid-only`) là phân tích độ nhạy.
- `svl-bench replay` in **WARNING** khi file có run ghi không ở chế độ lockstep, hoặc khi một variant
  gặp lính mà không có plan nào được áp nhiều hơn greedy quá 10 điểm phần trăm.

### Kiểm định

- `svl-bench replay --vs greedy,none` so mọi variant với greedy và với `none`, ghép cặp theo
  (bot, seed) bằng Wilcoxon signed-rank trên `captured`, `player_hp`, `escape_time_mean` và
  `plan_entropy_bits`, kèm khoảng tin cậy bootstrap 95% của hiệu số và p hiệu chỉnh Holm trên cả họ
  phép so sánh của mỗi bot và chỉ số.
- Mỗi tổ hợp bot × variant cần **≥ 30 run**.
- Báo cáo đủ p-value và khoảng tin cậy, không chỉ báo "có ý nghĩa".

## Nhật ký sai lệch

### exp-v1, replay lần đầu (04/10/2026): bị loại, chạy lại

**Hiện tượng.** 135/450 run có `plans = 0`: variant từ xa gặp lính nhưng không plan nào được áp, và
tất cả các run đó đều bị bắt. Vì vậy qiea "bắt được 96%" so với greedy 43%. Độ trễ p50 của remote
solver là ~160 ms, đúng bằng mức timeout. Có 173 run bị loại vì fallback > 20%, nên phần còn lại
lệch về những run đẹp.

**Nguyên nhân.**
1. Harness chạy game nhanh gấp ~4,5 lần thời gian thật. Plan về muộn gần một giây game nên bị bỏ
   ("người chơi đã chạy xa"), và lính đánh không có plan.
2. Sidecar giải song song nhiều request trên các luồng chung GIL và tính budget từ lúc bắt đầu
   giải. Hai request cùng lúc mất 130–225 ms với budget 120 ms.

**Sửa.**
- Coordinator có `LockstepLatency`.
- Sidecar giải lần lượt trong một tiến trình worker riêng, budget tính từ lúc nhận request:
  3–4 request cùng lúc nay chậm nhất ~140 ms.
- Sidecar nhận `--parent-pid` và tự thoát khi game thoát; trước đây bản exe để lại server mồ côi.
- Harness ghi chẩn đoán; phân tích thêm `audit`, nhóm `none` và kiểm định họ có Holm.

**Ảnh hưởng.** Chỉ cấp B. File cũ giữ lại thành `replay-no-lockstep-9bec9e8.jsonl` để minh bạch,
không dùng cho kết luận. Hiệu chỉnh bot vẫn giữ: nó chạy trên greedy, solver chạy trong Unity nên
không bị ảnh hưởng. Kết quả cấp A (game, medium, small, full, Moving AI, QAPLIB) giữ nguyên: cấp A
gọi thẳng solver, không qua sidecar.

### exp-v1, suite `full` (04/10/2026): chạy lại

SQA trả về `None` khi deadline đã qua trước sweep đầu tiên (bài 640 biến, máy bận), làm bench dừng
ở dòng 13.586/20.250. Đã sửa ở solver `d59e5aa`: luôn hoàn thành ít nhất một sweep. Suite `full`
được chạy lại từ đầu; file dở dang giữ thành `full-partial-515b977.csv`.
