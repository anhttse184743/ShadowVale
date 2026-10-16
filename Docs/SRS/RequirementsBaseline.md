# ShadowVale — Phạm vi chuẩn theo proposal RBL

Ngày đối chiếu: 20/09/2026. Trạng thái: baseline yêu cầu và đối chiếu mã nguồn; không phải chứng nhận nghiệm thu.

## 1. Căn cứ và cách áp dụng

Nguồn chính: **ShadowVale_RBL_Capstone_Proposal.docx**, các mục 3.1.b–d, 3.2.a–c, 4 và Research Information A–E. Nguồn ý tưởng: **Idea_DoAn.docx**, các mục III–VII. Các số mục là vị trí trong tài liệu, không phải số trang.

Proposal chính thức quyết định phạm vi sản phẩm và nghiên cứu. Idea chỉ bổ sung nội dung phù hợp; không dùng Idea để tăng khối lượng bắt buộc hoặc biến chức năng bắt buộc thành tùy chọn. Kịch bản Map 1 Nam–Hùng do người dùng cung cấp vẫn được giữ vì proposal không ấn định tên map, nhân vật hoặc diễn biến truyện.

Chỉ dẫn hiện tại của người dùng là tập trung Map 1, để asset nhân vật/animation thêm sau và chưa triển khai flow web. Đây là thứ tự thực hiện, không phải xóa web, telemetry hoặc AI nghiên cứu khỏi đồ án. Những câu hướng dẫn đăng ký, dán nội dung lên biểu mẫu hay nộp hồ sơ trong tài liệu không phải yêu cầu thao tác của phiên làm việc này.

## 2. Những điểm cần cắt giảm hoặc sửa từ Idea

| Nội dung | Idea hoặc tài liệu dự án cũ | Chuẩn áp dụng từ proposal | Xử lý |
| --- | --- | --- | --- |
| Số map | 6 map; chiến dịch 3–5 giờ | Demo chơi được 2–3 map (3.2.b, mục 4) | Không đặt 6 map/3–5 giờ làm điều kiện hoàn thành; map thêm là stretch |
| Vũ khí | Khoảng 10–12 loại | FR ghi 5–6 weapon classes; mục kiểm soát phạm vi ghi 5–6 weapons (3.1.c, mục 4) | Giữ hai cách diễn đạt của nguồn; lập danh mục 5–6 vũ khí đại diện các lớp, không mở rộng lên 10–12 |
| Boss | Hai boss, bố trí tại Map 5 và Map 6 | Có human boss đổi vị trí, ném lựu đạn phá cover, gọi viện khi HP < 50% (3.1.c) | Giữ yêu cầu boss; không bắt buộc hai boss hoặc Map 5/6 |
| Stealth/noise | Có chỗ ghi optional | Noise theo hành động, LOS, silent takedown là FR bắt buộc (3.1.c) | Bỏ nhãn optional đối với ba chức năng này |
| FSM | Gọi là sáu state nhưng bảng liệt kê chín | Patrol → Investigate → Spot Player → Take Cover → Suppress/Engage → Retreat (3.1.c) | Chuẩn đúng sáu state hành vi; nghe súng là sự kiện, flank/viện quân là hành vi phối hợp/boss, không bắt buộc tách thêm state |
| Survival | Có mục HUD đề cập thanh đói; mô tả chỉ số không nhất quán | HP và stamina (3.1.c) | Không thêm hunger/thirst vào baseline; đồ ăn chỉ là lựa chọn nội dung nếu cần |
| Nhiệm vụ | Danh sách tám loại nhiệm vụ, nhiều địa danh/cảnh truyện cố định | Quests dạng dữ liệu; proposal không quy định đủ tám loại hoặc tên địa danh | Dùng danh sách Idea như kho ý tưởng; Map 1 dùng nhận hàng, vượt tuần tra, lấy tài liệu, rút lui |
| Craft | Nhiều công thức và sản phẩm cụ thể | Recipe-based crafting, durability và repair (3.1.c) | Giữ hệ thống; không bắt buộc tất cả IED/spike trap/silencer hoặc số lượng công thức trong Idea |
| Asset | Phân công Game Artist, tự làm sprite/animation | Dùng art/audio pack có license/CC và attribution; nhóm tập trung kỹ thuật (3.1.b, mục 4) | Model/animation hiện tại là placeholder; không thêm yêu cầu tự sản xuất asset |
| Engine/level | Tilemap được ghi như hướng triển khai cố định | Unity LTS URP, 2.5D isometric | 3D + orthographic + NavMesh hiện có phù hợp; không bắt buộc làm thêm Tilemap |
| Backend | README repository ghi ASP.NET Core | Python FastAPI + PostgreSQL (3.2.a) | Chuẩn lại mô tả kiến trúc; chưa thay đổi service ngoài repository hoặc port triển khai |
| Tiến độ | Ba giai đoạn khoảng năm tháng | Bảy package trong 15 tuần (3.2.c) | Dùng các package của proposal để lập backlog |

FMOD, voice acting, lip-sync, combat của Hùng, snap-to-cover animation, thêm map/boss/vũ khí và các hiệu ứng điện ảnh không phải điều kiện nghiệm thu được proposal nêu. Không xóa nội dung đã có chỉ vì proposal không liệt kê; các chi tiết hỗ trợ Map 1 được giữ nếu không làm tăng phạm vi bắt buộc.

## 3. Baseline bắt buộc của sản phẩm

Các mã dưới đây là mã đối chiếu được đặt trong lần chuẩn hóa này, không phải mã gốc của proposal.

| Mã | Yêu cầu chuẩn | Căn cứ |
| --- | --- | --- |
| G01 | Single-player 2.5D isometric trên Windows 10+, movement có collision; HUD HP, stamina, crosshair | 3.1.b–d |
| G02 | Save/load local JSON của full game state | 3.1.c |
| G03 | Combat, 5–6 lớp vũ khí; đạn theo loại; cover; durability và repair | 3.1.c |
| G04 | Noise radius theo hành động, line-of-sight detection và silent takedown | 3.1.c |
| G05 | Inventory theo slot, stack đạn, drag-and-drop | 3.1.c |
| G06 | Craft theo recipe; loot phân tier và xác suất | 3.1.c |
| G07 | Skill tăng theo sử dụng: shooting, engineering, stealth | 3.1.c |
| G08 | Safe Camp có quest NPC, crafting, storage, save và trade | 3.1.c |
| G09 | Startup lấy published content bundle; khi backend lỗi vẫn chơi bằng cached config | 3.1.c–d |
| G10 | Session end batch/upload telemetry đã ẩn danh, có retry khi lỗi | 3.1.c–d |
| G11 | Demo chơi được 2–3 map, có nội dung data-driven | 3.2.b |
| A01 | FSM đúng sáu trạng thái, pathfinding, alert theo noise | 3.1.c |
| A02 | Khi phát hiện người chơi, squad coordinator giải QUBO/Ising assignment cho cover, escape routes, flank angles | 3.1.b–c |
| A03 | Classical và quantum-inspired/simulator solvers qua một interface; chọn variant lúc runtime | 3.1.b–c |
| A04 | Boss người: đổi vị trí, lựu đạn phá cover, gọi viện khi HP dưới 50% | 3.1.c |
| P01 | React authoring map/enemy/loot/recipe/quest/weapon; JWT; validation; version history, compare/rollback/publish | 3.1.c, 3.2.a |
| P02 | Admin quản lý role Designer/Analyst/Admin, review/approval và configuration | 3.1.c |
| T01 | API ingest event ẩn danh; heatmap độ khó, funnel hoàn thành, phân bố vũ khí và stealth/combat | 3.1.c |
| T02 | Dashboard so sánh solver theo content version: capture rate, escape time, coordination score | 3.1.c |
| N01 | Mục tiêu 60 FPS ổn định trên PC tầm trung; replanning trong latency budget, có cache/asynchronous replan | 3.1.d |
| N02 | Không publish bundle sai schema; content tách khỏi code; web RBAC; telemetry không chứa dữ liệu cá nhân | 3.1.d |
| N03 | Web tương thích Chrome/Firefox/Edge 100+; backend FastAPI + PostgreSQL | 3.1.d, 3.2.a |

Không suy diễn 120 ms hoặc một cấu hình PC cụ thể là số bắt buộc của proposal: đó là tham số kỹ thuật cần cấu hình/đo kiểm. Không đánh dấu một requirement hoàn thành chỉ vì đã có DTO, enum, interface, scene trống hoặc trường JSON.

## 4. Phạm vi nghiên cứu phải giữ

- Classical baselines trong Research Information B.O2: greedy, genetic algorithm, classical simulated annealing. Phía quantum-inspired/simulator gồm QAOA simulator, simulated quantum annealing và quantum-inspired evolutionary algorithm như proposal mô tả; cần phân biệt variant thực chạy với fallback.
- Benchmark procedural có seed, khoảng 4–12 agent và 20–100 node, thay đổi kích thước/chokepoint; lặp có kiểm soát compute budget để tái lập.
- Đo approximation ratio so với brute-force optimum trên instance nhỏ; objective value trên instance lớn; wall-clock latency/percentiles, success rate, scalability và kiểm định thống kê.
- Có deterministic replay harness và các chỉ số capture rate, escape time, coordination score để đối chiếu in-game qua telemetry.
- Playtest với ít nhất 5 tester là công việc trong Package 7. Human A/B study là stretch riêng; không đồng nhất hai việc rồi bỏ yêu cầu playtest.
- Real-QPU demo, human A/B study, map thêm và second coordination task là stretch. Không yêu cầu dataset bên ngoài hoặc gán nhãn; large-scale RL và tuyên bố quantum-hardware speed-up ngoài phạm vi.

Đội ba lính ở tutorial Map 1 là lựa chọn thiết kế, không tự động vi phạm khoảng 4–12 agent của benchmark. Tuy nhiên không được dùng riêng tutorial ba lính để tuyên bố đã đáp ứng phạm vi thực nghiệm. Các khẳng định novelty/kết quả tốt hơn trong proposal là mục tiêu cần kiểm chứng, chưa phải kết quả của mã nguồn hiện tại.

## 5. Đối chiếu với source hiện tại

Phạm vi kiểm tra source: repository `G:/game/ShadowVale`, đặc biệt `Assets/_Project/Map01`, Content, Bootstrap, AI và Save DTO. Chưa kiểm tra source backend/web/solver ở repository khác; không kết luận các sản phẩm đó chưa được nhóm triển khai.

| Hạng mục | Bằng chứng hiện có | Khoảng thiếu / kết luận |
| --- | --- | --- |
| Map 1 và nội dung | Scene rừng, ba lối đi, căn cứ, Nam–Hùng, nhiệm vụ tài liệu | Giữ như thiết kế tutorial; không phải chiến dịch sáu map |
| G01 | ForestMission có movement/controller, HP/stamina, camera isometric | Chưa có crosshair HUD riêng; Play Mode chưa kiểm chứng |
| G02 | Checkpoint riêng của Map 1; SaveGameV1 DTO ở Data | Chưa là full-state save: checkpoint reset patrol sống và chưa giữ đầy đủ state AI, vũ khí, skill, storage, crafting; DTO không thay SaveService |
| G03 | Map 1 bắn một rifle và vật cản chặn tia; bundle có sáu weapon definition | Chưa có chọn đủ vũ khí/per-type ammo trong gameplay, durability/repair hoạt động; dữ liệu định nghĩa không bằng hệ thống hoàn chỉnh |
| G04 | Noise, LOS, crouch trong bụi, ném đá | Thiếu silent takedown; ném đá giữ như chi tiết hỗ trợ noise, không thay takedown |
| G05–G06 | Inventory dictionary, loot cố định, một công thức đọc từ bundle | Chưa slot/drag-drop; loot cố định không đáp ứng tiered probabilistic loot; không tăng số recipe tùy tiện để thay việc hoàn thiện hệ thống |
| G07–G08 | Có điểm nghỉ, bàn chế tạo và checkpoint | Chưa có usage skills; điểm nghỉ không phải Safe Camp hub đủ NPC/storage/trade |
| G09 / data-driven | ContentService, SchemaValidator, FallbackBundleProvider; Map 1 đọc TextAsset trực tiếp | Bootstrap chưa có backend fetch/cache source; Map 1 chưa qua shared content service; placement/quest/thoại còn gắn vào builder/runtime. source-layout.json là snapshot, không phải published bundle được gameplay tiêu thụ |
| G10 / T01–T02 | Telemetry DTO và endpoints; thư mục Telemetry chưa có uploader | Chưa có luồng batch/retry/ẩn danh/upload trong client đã kiểm tra; dashboard nằm ngoài phạm vi source đang đọc |
| A01 | Patrol, Investigate, SpotPlayer, TakeCover, Engage và Down | Thiếu Retreat. Down là vòng đời chết, không phải state thứ sáu thay Retreat; không tự động đổi số enum vì scene đang serialize |
| A02–A03 | ISquadSolver, GreedySolver, SolverFactory và enum variant | Map 1 chưa gọi coordinator; các variant non-greedy đang fallback về greedy. Không được báo đã chạy QAOA/SQA/QIEA |
| A04 | Bundle có boss archetype | Chưa có boss encounter hoạt động trong Map 1; đưa vào một map thuộc demo 2–3 map thay vì mặc định Map 5/6 |
| N01 | Có cấu hình latency và kiểm tra compiler/static map | Chưa có số đo FPS, latency percentile, replay hoặc playtest; không dùng kiểm tra compile để kết luận đạt performance |

Mã prototype có thể được giữ để duyệt map, nhưng phải được tích hợp/refactor vào các module chung trước khi nghiệm thu. Không bổ sung một hệ thống content/save/solver song song rồi coi đó là kiến trúc chính thức.

## 6. Phạm vi tiếp theo cho Map 1

Giữ tuyến nhận hàng → lựa chọn vượt tuần tra → căn cứ → thu thập bằng chứng → rời map cùng Hùng. Giữ Nam/Hùng và bối cảnh quốc gia/phe phái hư cấu. Chưa thêm map 4–6, boss thứ hai, hunger/thirst hoặc combat riêng cho Hùng.

Thứ tự kỹ thuật đề xuất dưới đây là cách triển khai, không phải thêm requirement vào proposal:

1. Đưa cấu hình Map 1, placement/spawn/loot/objective và tuning cần publish vào content contract/JSON có validation; thống nhất ID và ContentService. Cho game chạy offline bằng cache/fallback.
2. Hoàn thiện movement/HUD/crosshair, combat/cover/ammo/durability/repair và full-state save/load theo Package 2; giữ actor placeholder.
3. Hoàn thiện sáu state với Retreat, LOS/noise/takedown, nav graph và coordinator gọi classical baseline theo Package 3. Không khóa lựa chọn stealth sau cổng bắt buộc giết lính.
4. Tích hợp slot inventory, loot xác suất, crafting, skills và Safe Camp theo Package 5; không coi điểm nghỉ của Map 1 là hub hoàn chỉnh.
5. Sau giai đoạn Map 1, tích hợp solvers, content platform và telemetry theo các package chính thức; giữ dữ liệu test có seed/content version/solver thực chạy.

Một phiên bản Map 1 chỉ được ghi là đáp ứng requirement tương ứng khi có test runtime chứng minh. Tiêu chí đề xuất: cả ba lối đều thông; LOS/cover chặn đúng; tiếng động tác động theo phạm vi; Retreat thực sự đạt được; loot không nhân đôi; reload khôi phục state đã cam kết; config không hợp lệ bị từ chối; backend mất kết nối không chặn gameplay. Các benchmark AI/performance được đo riêng theo protocol nghiên cứu.

## 7. Những điểm tài liệu cần xác nhận khi chốt SRS

- Header ghi 09/2026–03/2027 nhưng đồng thời ghi 15 tuần. Giữ kế hoạch package 15 tuần; chưa tự sửa lịch hành chính.
- Proposal dùng cả “5–6 weapon classes” và “5–6 weapons”. Không silently đổi thành 10–12; cần chốt danh mục/lớp khi thiết kế nội dung chi tiết.
- “Full game state”, “mid-range PC”, “coordination score”, latency budget và protocol thống kê cần tiêu chí nghiệm thu cụ thể trong SRS/Testing Strategy; proposal chưa cung cấp đủ trị số.

Bản chuẩn hóa này không sửa hai DOCX gốc, không công bố kết quả nghiên cứu và không tuyên bố đã hoàn thiện các chức năng còn thiếu.
