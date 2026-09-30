# Hợp đồng dữ liệu giữa game và backend

## Telemetry session — `POST /api/v1/telemetry/sessions`

Mẫu đầy đủ: [`telemetry_session.sample.json`](telemetry_session.sample.json). File này được sinh ra từ test
`TelemetrySessionTests.TheJsonMatchesTheBackendContract`. Nếu JSON mà client gửi lệch khỏi file này (thêm,
bớt hoặc đổi tên trường), test sẽ đỏ.

Client gửi một batch cho mỗi session. Một session là một lần chơi một map, từ lúc vào map đến lúc
rời map (chơi lại, ra menu, thoát game).

- **JSON snake_case**, giống content bundle. `schema_version` sẽ tăng khi một trường đổi nghĩa hoặc bị bỏ.
- **Idempotent:** header `Idempotency-Key` bằng `session_id`. Backend nên upsert theo `session_id`;
  trả `409` cho session đã có cũng được, client coi đó là đã gửi.
- **Mã trả về:**
  - `2xx` hoặc `409`: client xoá batch khỏi hàng đợi.
  - `400`, `413`, `422`: client coi batch là hỏng, chuyển sang `rejected/` và không gửi lại.
  - Mọi mã khác, **kể cả `404`** (khi endpoint chưa làm xong): client giữ batch trong hàng đợi và
    thử lại sau 5 s, 10 s, 20 s… tối đa 5 phút một lần.
- **Không có dữ liệu cá nhân:** `session_id` là GUID ngẫu nhiên tạo cho mỗi session, không gắn với
  người chơi hay máy. Không gửi tên người dùng, tên máy hay IP (có test kiểm tra).
- **Kích thước:** tối đa 4000 event mỗi session. Bước chân lấy mẫu tối đa 1 lần/giây, vị trí người
  chơi 5 giây/lần. `summary.event_counts` vẫn đếm đủ mọi event, kể cả những event đã bị lấy mẫu bỏ đi.

Các khối dữ liệu:

| Trường | Nội dung |
|---|---|
| `solver` | `SolverMetrics`: p50/p95/max độ trễ, số timeout, số fallback, điểm điều phối |
| `summary` | Chỉ số nghiên cứu tính sẵn ở client: capture rate, escape time, coordination score, độ đa dạng plan, `valid_for_comparison` |
| `encounters[]` | Mỗi trận giao tranh: kết quả (`captured` / `escaped` / `cleared` / `unresolved`), thời lượng, số lần re-plan |
| `events[]` | `{type, t, map_id, payload}`; danh sách type nằm trong `TelemetryEventDto` |

Chưa có backend thì batch nằm trong `persistentDataPath/telemetry/pending/`. Bản sao cho nghiên cứu
luôn được ghi vào `persistentDataPath/research/telemetry-sessions.jsonl`.

Muốn đổi hợp đồng thì sửa DTO, chạy test với biến môi trường `SVL_WRITE_CONTRACT=1` để sinh lại file
mẫu, rồi báo cho cả nhóm.
