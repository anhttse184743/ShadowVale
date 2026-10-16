# Bản ghép dev và Thịnh — 16/10/2026

Bản ghép giữ Map 1 với C1 kho tiếp tế, C2 trạm liên lạc, C3 đồn chỉ huy chính và hệ thống túi đồ, chế tạo, trang bị của Thịnh; tích hợp nhiệm vụ, kỹ năng, minimap, thoại và Map 2 từ dev. Phạm vi và quân số đã được người dùng duyệt trước khi áp dụng.

## Nội dung tích hợp

- C1: chìa khóa của lính giữ kho mở cửa đôi của nhà kho mới. Khóa mã và túi tiếp tế nằm trong kho, có chỗ đứng tương tác và lối đi xuyên cửa. Hai cánh cửa xoay riêng; gỡ đúng phần cửa bị gộp trong mesh để không còn cửa tĩnh chặn phía sau.
- C2: câu đố mạch điện dùng chính đài liên lạc trong mô hình mới. Bỏ bộ bàn đài trùng và chuyển ghế khỏi chỗ thao tác. Nam đứng ngoài vật cản của bàn, cúi người và đặt hai tay lên đài; sai số hai tay trong kiểm thử lần lượt khoảng 1,3 cm và 3,6 cm.
- C3: giữ đồn chỉ huy lớn đã dựng, đặt chỉ huy tại hiên sở chỉ huy và các điểm quan sát trên sườn đồi phía nam. Đổi vị trí quan sát để nhìn được đồn, giữ quy tắc nhận diện bằng ống nhòm của dev.
- Giữ quân số C1/C2/C3 là 4/5/5; C2 gọi thêm đúng 3 lính phản ứng. Tắt hai lính cũ chồng lên cụm trại, giữ nhóm bảo vệ bến cứu Hùng.
- Bake NavMesh riêng cho bản ghép. Kiểm tra đường đến các mục tiêu, tuần tra, điểm xuất hiện quân phản ứng và hộ tống từ bến B về A; kiểm tra chỗ đứng tương tác không lọt vào vật cản.
- Hợp nhất dữ liệu túi đồ, vật phẩm thả, nhiệm vụ, câu đố, kỹ năng và vùng đã khám phá trong cùng checkpoint. Chìa khóa kho và kiện tiếp tế được bảo vệ như vật phẩm nhiệm vụ.
- Giữ túi đồ mới và tích hợp bảng kỹ năng. Ưu tiên bàn chế tạo khi người chơi đứng tại bàn, tránh bị hội thoại chỉ huy ở gần chiếm phím E.
- Sửa tiếp xúc bến ngay khung hình đầu khi cứu Hùng; giải phóng callback trang bị của Map 1 khi chuyển sang Map 2.

## Kiểm chứng

70 bài kiểm tra riêng biệt đã đạt, tổng hợp theo kết quả mới nhất của từng tên test trong `validation_summary.json`. Các lượt thử đầu có lỗi; kết quả kiểm tra lại sau sửa nằm trong `tests_retry.xml`, `tests_motion.xml`, `tests_transfer.xml` và `tests_isolation.xml`. Đây là bộ test được chọn theo phạm vi ghép, không phải toàn bộ test của dự án.

Đã kiểm tra lưu/tải hỗn hợp, điều kiện C1/C2/C3, báo động không sinh quân lặp, tiếp xúc tay tại đài, cứu Hùng, ném lựu đạn/bom khói, minimap, chuyển cảnh tự nhiên bằng thuyền, giữ nhân vật/máu/đạn/vật phẩm sau chuyển cảnh và luồng vật phẩm, hội thoại, kết thúc Map 2. Kiểm tra đường đi và va chạm không ghi nhận lỗi ở 19 vị trí agent có sẵn trong scene cùng các điểm tương tác; đây không phải phép chứng minh mọi tình huống AI trong toàn bộ lượt chơi.

Hai ô lưu thủ công hiện có giữ nguyên SHA-256 và thời gian sửa từ 22/09/2026. Bằng chứng: `default_slots_isolation_proof.json`. Lượt test đầu đã ghi vào ô tự động trước khi bổ sung cô lập; người dùng đã xác nhận chỉ cần giữ các ô thủ công. Các lượt kiểm tra sau dùng thư mục save riêng; kiểm tra mã băm xác nhận cả ba tệp mặc định không đổi trong lượt thử cô lập cuối.

## Bản chạy và hiệu năng

Bản Windows đầy đủ build thành công, 0 lỗi, khoảng 854 MB theo BuildReport. Chạy `SourceArt/MergeDev_20261016_Build/ShadowVale.exe` và giữ nguyên cả thư mục đi kèm. Có đủ Boot, menu, Map 1 và Map 2; đây là bản chơi, không phải scene benchmark.

Đo trên RTX 3050 Laptop 4 GB, i7-11800H, RAM khoảng 16 GB, Unity 6000.3.24f1, Quality PC, 1920×1080. Bản standalone không Development, VSync tắt, FPS không giới hạn; mỗi góc làm nóng 3 giây và lấy mẫu 8 giây, cửa sổ game có focus trong toàn bộ lượt được dùng. AI và HUD hoạt động. Không chạy build/test khác trong lượt đo cuối. FrameTimingStats bật để thu CPU/GPU timing. Editor và ứng dụng khác vẫn mở, điều kiện nguồn điện/nhiệt độ/tác vụ nền không được kiểm soát.

| Góc đo | FPS trung bình | Khung hình P95 (ms) |
|---|---:|---:|
| C1 warehouse | 52.3 | 25.7 |
| C2 signal station | 53.0 | 24.2 |
| C3 arrival | 48.1 | 26.9 |
| C3 courtyard | 47.9 | 25.9 |
| B jetty | 46.1 | 27.7 |
| Forest route | 45.9 | 28.0 |
| C2 alarm response | 45.6 | 29.7 |
| C3 courtyard repeat | 64.2 | 21.0 |

**Chưa đạt 1080p/60 FPS ổn định.** Sáu góc chính đạt khoảng 46–53 FPS, C2 báo động khoảng 46 FPS; C3 đo lặp đạt khoảng 64 FPS nhưng P95 vẫn hơn ngân sách 16,7 ms của 60 FPS. CPU main thread khoảng 16–20 ms ở sáu góc đầu, GPU khoảng 7–11 ms; dữ liệu gợi ý cần profile phần CPU trước khi giảm chất lượng hình ảnh, chưa đủ để quy nguyên nhân cho một hệ thống cụ thể. Tạm dừng AI ở góc chẩn đoán cuối chỉ đổi FPS C3 từ khoảng 64,2 thành 65,6; không lấy số khi tắt AI để công bố hiệu năng gameplay.

20 agent hoạt động đều ở trên NavMesh trước báo động; sau khi gọi đúng 3 lính phản ứng là 23/23. Số này gồm NPC đồng đội/chỉ huy căn cứ A, không phải số lính của riêng C1/C2/C3. Lượt C2 báo động là tải tổng hợp: gọi cơ chế sinh quân thật khi các lính gốc còn sống. Phép đo dùng camera cố định, dịch chuyển người chơi, bất tử để tránh ngắt benchmark, bỏ intro; không đại diện trọn lượt chơi, giao tranh nặng hoặc hộ tống/rút lui. Hai lượt thử bị ảnh đen/mất focus bị loại, không trộn vào bảng.

Số đo gốc ở `Benchmark/Measurements/results.json`; chín ảnh 1920×1080 nằm cùng thư mục dưới tên `view_1.png` đến `view_9.png`. Ảnh `view_3.png` cho thấy C3 với quân địch và HUD đang chạy. Benchmark ở `SourceArt/MergeDev_20261016_Benchmark` là bản riêng, dùng save cô lập; không dùng executable đó để chơi bình thường. Nếu tiếp tục mục tiêu 60 FPS, cần một lượt profile CPU/draw submission/HUD và mức chi tiết môi trường với cùng lộ trình đo; không giảm quân số hoặc đổi luật nhiệm vụ để làm đẹp số đo.


## Mở bản ghép trong Unity

Mở `Assets/_Project/Scenes/Maps/Map 1.unity`. Dùng menu `ShadowVale > Merged Map > Inspect C1 warehouse`, `Inspect C2 radio station`, hoặc `Inspect C3 main garrison` để chuyển góc Scene xem từng cụm. Bản chơi đầy đủ bắt đầu từ `Assets/_Project/Scenes/00_Boot.unity` hoặc executable Windows được ghi ở phần bản chạy.

Các mẫu K54/Mosin và ô trang bị của nhánh Thịnh được giữ nguyên. Lượt ghép này không bổ sung animation/bắn mới cho hai súng đó; phần hiển thị vũ khí hiện có vẫn tập trung AK/dao. Không đổi độ khó, giá chế tạo hoặc luật thắng ngoài phần dev đã được duyệt.

## Sao lưu và lịch sử Git

- dev được lấy ở `800a26b7c1fd3cf189eb559c517f77898d357a88`.
- Mốc Thịnh trước thao tác: `b53e334e9be4e26230793604e0a9d6560b198167`.
- Checkpoint giữ các thay đổi art/code có sẵn: `ce9ea0585e5e0ff2984860b4c44f59c2f8607851`.
- Sao lưu cục bộ: `SourceArt/MergeDev_20261016_Safety/Files`, manifest SHA-256 gồm 5.840 tệp. Bản sao các ô save hiện tại nằm riêng tại `ObservedPlayerSaves` trong cùng thư mục an toàn.
- Windows từ chối thay thế một số tệp prefab/texture khi merge thông thường. Đã giữ tệp tại chỗ, áp nội dung đã kiểm tra, và dùng index riêng để ghi merge có hai parent vào nhánh `thinh`. SHA kết quả được ghi cục bộ tại `merge_complete.json` và có trong `git log thinh`. Không đổi ACL, không reset/checkout cưỡng bức, không push.

## Tài liệu đã đối chiếu

- [Git merge](https://git-scm.com/docs/git-merge) và [merge-tree](https://git-scm.com/docs/git-merge-tree/2.43.0.html): kiểm tra cây hợp nhất và xử lý xung đột trước khi ghi bản cuối.
- [Unity 6.3 Smart Merge](https://docs.unity3d.com/6000.3/Documentation/Manual/SmartMerge.html): hợp nhất scene/prefab YAML.
- [Unity Learn — Create branches and resolve merge conflicts](https://learn.unity.com/course/collaborate-with-github-desktop/tutorial/create-branches-and-resolve-merge-conflicts?version=6.2): bài hướng dẫn chính thức về branch và xung đột trong dự án Unity.
- [Animator.Update](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Animator.Update.html), [AnimatorCullingMode](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AnimatorCullingMode.html), [LateUpdate](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.LateUpdate.html): đối chiếu thời điểm pose và tiếp xúc nhân vật.

Video Brackeys về GitHub với Unity đã được xác định nhưng không tải được nội dung để xem; không dùng làm bằng chứng đã xem hoặc căn cứ duy nhất cho thay đổi.
