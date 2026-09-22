# RBL — demo điều phối địch trong Map 1

Áp dụng từ `F:/RBL.docx`: **QUBO Modeling and Quantum-Inspired Algorithmic Framework for Multi-Agent Tactical Coordination in the ShadowVale System**.

## Chơi demo

- Mở `Assets/_Project/Scenes/Maps/Map 1.unity`, nhấn Play.
- 12 lính, tăng từ 9. Hai tổ 4 người có chỉ huy tại tuyến tuần tra gần cầu và trại phía đông; hai chốt phía bắc vẫn có 2 người/chốt.
- Chỉ huy được ghi **CHỈ HUY** trên nhãn nhân vật. Khi nhận báo cáo phát hiện, chỉ huy phân công các vị trí bắn và vòng sườn. Hạ chỉ huy làm dừng lệnh mới; lính còn sống vẫn tự chiến đấu và tìm kiếm.
- **F7** bật bảng nghiên cứu; **F6** chuyển Greedy / QIEA cho những lần lập kế hoạch tiếp theo. Chuyển giữa trận là trình diễn, không phải phép thử A/B có kiểm soát.
- Địch bắn loạt 3 viên, cách viên 0,19 s; nghỉ loạt ít nhất 1,05 s, thay băng sau 18 viên. Thông số và sát thương điều chỉnh trong `Assets/_Project/Map01/Map01Research.json`.
- Cây có collider thân, đá, nhà, hàng rào có collider và địa hình chặn cả đạn người chơi lẫn địch. Lá trang trí không được coi như tường kín. Đạn hitscan: vệt sáng kết thúc ở va chạm gần nhất, không gây sát thương xuyên vật cản.
- Địch chỉ báo cáo vị trí nhìn thấy; lệnh vô tuyến dùng vị trí cuối cùng đó. Mất dấu đủ lâu thì tìm kiếm rồi quay lại tuần tra. Chỉ huy không cung cấp tọa độ người chơi xuyên tường.

## Phần nghiên cứu đã triển khai

Biến `x[a,n]` chọn vị trí n cho lính a. Ma trận lưu theo hàng, tam giác trên:

`E = -Σ reward[a,n] x[a,n] + μΣ overlap[i,j]x[i]x[j] + λΣa(Σn x[a,n] - 1)²`.

Giữ cả hằng số `A*λ` khi báo cáo năng lượng. Reward gồm đường bắn thoáng đến vị trí được báo cáo, góc vòng sườn, cự ly chiến đấu và chiều dài đường NavMesh. Phạt các vị trí quá gần nhau và đường không thể đi tới. Ngoài one-hot, bộ giải mã áp dụng sức chứa một lính/vị trí cho cả hai solver. Đây là ràng buộc bổ sung của demo; khi so sánh tối ưu chính xác cũng dùng cùng không gian khả thi này.

Điểm chiến thuật sinh từ hai vòng quanh vị trí cuối đã thấy và vị trí hiện tại của lính; được chiếu xuống địa hình, kiểm tra NavMesh. Không tuyên bố đây là bộ trích xuất đồ thị đường thoát hoặc hệ thống cover đầy đủ. Squad thực tế 4 lính, tối đa khoảng 20 điểm; benchmark tổng hợp gồm 3×5, 4×20, 8×40 và 12×100.

- **Greedy**: baseline hiện có trong project, chọn theo năng lượng biên.
- **QIEA cục bộ**: qubit biểu diễn bằng góc α=cos θ, β=sin θ; lấy mẫu Bernoulli theo β², sửa nghiệm thành phân công khả thi, xoay góc về incumbent tốt nhất. Khởi tạo incumbent bằng Greedy. Vì vậy so sánh này là **Greedy so với Greedy + QIEA refinement**, không phải hai thuật toán khởi tạo độc lập.
- Ngân sách mặc định 4 ms/solve, giới hạn thêm 80 thế hệ × 8 cá thể. Kiểm tra deadline ở mỗi cá thể: một cá thể hoặc baseline có thể vượt thời gian, được ghi lại. Ngân sách không phải bảo đảm thời gian thực cứng.
- Solver chạy trên worker với dữ liệu số; NavMesh, vật lý và lệnh di chuyển chạy trên main thread. Xây cost từng lính qua nhiều frame; kế hoạch quá cũ hoặc chỉ huy đã chết sẽ bị loại.
- Không có QAOA, PIQMC/SQA, QPU, Python sidecar hay dashboard máy chủ mới trong bản demo này. QIEA là phương pháp lấy cảm hứng lượng tử chạy bằng CPU; kết quả không chứng minh lợi thế lượng tử.

Lưu ý khi sửa bản thảo: `dwave-neal / SimulatedAnnealingSampler` là simulated annealing cổ điển, không phải PIQMC/SQA. Nguồn: https://docs.dwavequantum.com/en/latest/quantum_research/classical_intro.html . Không nên gán kết quả SA thành SQA hoặc khẳng định ưu thế/scaling khi chưa đo.

## Số liệu và tái lập

Menu **ShadowVale → Research → Run seeded Greedy versus QIEA benchmark** tạo:

- `Tools/Map01OptimizedReports/research-benchmark.csv`: cùng 40 ma trận cho hai solver, xen kẽ thứ tự chạy, cùng ngân sách; 80 kết quả.
- `research-benchmark.txt`: kiểm tra năng lượng, nghiệm khả thi và tái lập fixed-seed/fixed-iteration. Với 3×5, liệt kê hết phân công khả thi để lấy tối ưu thật.

Dùng **absolute energy gap** trên các bài nhỏ; không dùng tỷ số năng lượng âm hoặc gần 0 để gọi approximation ratio. Các bài lớn không có nghiệm tối ưu chứng minh, nên để trống cột exact/gap. Deadline khiến số mẫu phụ thuộc tốc độ máy; seed chỉ bảo đảm tái lập hoàn toàn khi dùng số vòng cố định và không chạm deadline.

Khi chơi, dữ liệu nằm tại `Application.persistentDataPath/Research/<session>/`:

- `config.json`: cấu hình phiên.
- `events.csv`: solver, seed qua instance, build/solve/pipeline latency, energy, deadline, số mẫu, số mẫu thô khả thi, phát hiện, đạn bắn/trúng, thay đạn, chỉ huy chết và người chơi gục.
- `instances.jsonl`: tối đa 100 snapshot QUBO cùng seed và vị trí, giúp tái chạy bài toán đã xảy ra trong rừng. Không gửi dữ liệu qua mạng.

`feasible=1` là nghiệm sau giải mã/kiểm tra đường; `raw_feasible / samples` đo độ khả thi của mẫu Bernoulli trước sửa. Hai số này không được đánh đồng. Pipeline latency có cả chờ frame/worker; solve latency chỉ đo solver.

Để báo cáo khoa học: chạy các phiên mới với cùng seed, cùng lộ trình và solver cố định; lặp nhiều lần, báo median/p95, deadline rate, phân phối chất lượng và tỷ lệ người chơi bị hạ. Thử nghiệm ít nhất 5 người, đảo thứ tự Greedy/QIEA để giảm học thuộc đường. Chưa có kết quả human A/B, capture rate, heatmap hoặc kết luận về cảm nhận công bằng trong lần triển khai này.

## Kiểm tra tích hợp

Menu **ShadowVale → Research → Run combat and commander Play Mode checks** kiểm tra khởi tạo, lệnh QUBO, bắn loạt, vật cản và mất chỉ huy trong một khu thử tạm thời. Khu thử chỉ tồn tại trong Play Mode, không ghi vào map. Kết quả tại `research-playcheck.txt`.

Script setup kiểm tra toàn bộ vòng tuần tra mới bằng NavMesh trước khi lưu scene. Không tự chạy lại các builder môi trường, không thay mesh Blender hay vị trí chunk đã sửa.
