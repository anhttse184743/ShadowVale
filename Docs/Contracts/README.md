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

### Map 1: tên mục tiêu từ bản ngày 17/10/2026

`objective_completed.payload.objective` lấy từ `Map01Quest.StageIds`; `payload.index` vẫn là số stage.
Sáu tên đã đổi theo nhiệm vụ C1/C2/C3. Đây là thay đổi **ý nghĩa mục tiêu**, không chỉ đổi nhãn hiển thị:

| `index` | Tên trước | Tên hiện tại | Mục tiêu hiện tại |
|---|---|---|---|
| 3 | `scout_camps` | `op1_supplies` | Lấy hàng tiếp tế ở C1 |
| 4 | `report_scouting` | `report_supplies` | Báo cáo và bàn giao tiếp tế |
| 5 | `destroy_camps` | `op2_radio` | Vô hiệu đài liên lạc C2 |
| 6 | `report_camps` | `report_radio` | Báo cáo đã vô hiệu đài |
| 7 | `defeat_commander` | `op3_commander` | Thực hiện nhiệm vụ chỉ huy C3 |
| 8 | `report_commander` | `report_retreat` | Báo cáo và nhận đường rút |

Tên ở các index 0/1/2/9/10/11 vẫn lần lượt là `rescue_hung`, `escort_hung`, `briefing`,
`complete`, `extraction`, `boarding`. Thứ tự chơi cuối map là **8 → 10 → 11 → 9**;
không sắp thứ tự hoàn thành chỉ bằng giá trị số. Event được phát cho stage vừa hoàn thành;
khôi phục checkpoint không phát `objective_completed`.

Định dạng JSON/DTO vẫn giữ nguyên trong lượt đồng bộ tài liệu ngày 18/10. Bên nhận cần chấp nhận
cả tên cũ và mới khi đọc dữ liệu lịch sử; có thể dùng `map_id` + `index` để đối chiếu vị trí,
nhưng không gộp hai mục tiêu khác nghĩa vào cùng chỉ số nghiên cứu mà bỏ qua `objective`.
Chưa xác minh backend/dashboard bên ngoài repo đã cập nhật cách phân nhóm. Bảng này ghi nhận
hành vi client hiện tại, không xác nhận việc triển khai phía server.

## Content bundle: `enemy_archetypes[].boss` (thêm ở M8)

Archetype có `is_boss: true` được phép kèm một object `boss` (định nghĩa `boss_profile` trong
`Assets/StreamingAssets/Content/content.schema.json`). Object này chứa các tham số hành vi boss (A04):

| Nhóm | Trường |
|---|---|
| Chỉ số | `health_multiplier`, `damage_multiplier`, `vision_multiplier` |
| Đổi vị trí | `relocate_interval_seconds`, `relocate_damage_fraction`, `relocate_min_distance`, `relocate_max_distance`, `relocate_min_player_distance` |
| Lựu đạn | `grenade_camp_seconds`, `grenade_camp_radius`, `grenade_cooldown_seconds`, `grenade_range`, `grenade_radius`, `grenade_damage`, `grenade_fuse_seconds`, `grenade_noise_radius` |
| Gọi viện | `reinforce_hp_threshold` (0..1), `reinforce_count`, `reinforce_radius` |

- Mọi trường đều tuỳ chọn. Trường nào thiếu thì client dùng giá trị mặc định đã chỉnh cho Map 1 (`BossProfile.cs`).
- Nếu `boss` nằm trên archetype không phải boss, hoặc tỉ lệ nằm ngoài khoảng [0,1], `SchemaValidator` sẽ từ chối bundle.
- Web editor và schema phía backend cần thêm định nghĩa `boss_profile` này.
