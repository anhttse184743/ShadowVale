# Map 1 — Ba điểm nhiệm vụ và hai mini game

## Luồng đã triển khai

Chỉ huy giao điểm 1 sau cứu Hùng → hạ bốn lính → lục xác lính giữ chìa → mở cửa kho → giải khóa hòm → Nam lấy túi bằng tay không → báo cáo bộ đàm → mang hàng về hầm → nói chuyện ngay với chỉ huy → đặt hàng xuống và nhận lệnh tiếp theo → điểm 2, hạ năm lính → đấu bảng mạch → báo cáo thành công → điểm 3, đứng quan sát bằng ống nhòm → báo cáo → bị phát hiện → hạ sĩ quan → rời giao tranh, báo cáo và nhận lệnh ra bến.

Mục tiêu 20–30 phút là mục tiêu cân bằng cho lần chơi đầu, chưa được xác nhận bằng lượt chơi thủ công đủ dài. Video/test tự động kiểm tra tiến trình không phải phép đo thời lượng người chơi.

## Dấu hiệu và thao tác dành cho người chơi

| Vật thể | Dấu hiệu | Thao tác |
|---|---|---|
| Lính giữ chìa | Mục tiêu dẫn tới xác, biểu tượng chìa; loot ghi rõ tên chìa và cửa sử dụng | E lục xác, mở Tab xem mô tả chìa |
| Kho | Ổ chìa trên cửa, biểu tượng và lý do chưa mở được; không đặt chữ lên model | Hạ nhóm bảo vệ, có chìa rồi E mở cửa |
| Hòm | Khóa ba vòng trên thân; marker chuyển từ cửa sang hòm; không đặt chữ lên model | E mở mini game; giải xong Nam tự mở nắp và lấy túi |
| Bảng đài | Icon E và nhãn BẢNG MẠCH TRẠM ĐÀI | Hạ nhóm bảo vệ rồi E thao tác |
| Bàn giao | Đánh dấu CHỈ HUY tại hầm, mục tiêu nói rõ mang túi về | E nói chuyện và bàn giao |

Nhãn gần vật thể hiện trong khoảng 7 m; biểu tượng xuất hiện trong 18 m và có kiểm tra che khuất. Chìa/cargo là vật phẩm nhiệm vụ, không dùng bằng nút hồi máu hoặc trang bị làm vũ khí.

### Khóa hòm

Ba vòng 0–5. A/D hoặc nhấp chuột chọn vòng; W/S/cuộn chuột đổi số. Bấm Thử mã. Khung kết quả báo rõ số đúng vị trí, đúng nhưng sai vị trí, và không thuộc mã. Không có số nào đúng sẽ báo nguyên câu; chọn số không mất lượt, chỉ nhấn KIỂM TRA MÃ mới tính. Lịch sử dùng ba cột có tên. Phản hồi phân biệt số đúng chỗ và số có trong mã nhưng sai chỗ; ví dụ 241 so với 214 có một đúng chỗ, hai sai chỗ. Sáu lần đoán mỗi lượt, không đếm thời gian. Hết lượt chọn Thử lại; cùng mã, giữ ghi nhớ. Esc tạm dừng, có thể tiếp tục hoặc quay về checkpoint trước tương tác.

### Bảng mạch

Lưới 5×4, gồm 20 đoạn thuộc một dây duy nhất. Chuột trái/phải xoay thuận/ngược. Nối NGUỒN ở A1 tới NGẮT ĐÀI ở E4 qua đủ 20 đoạn; không có dây thừa hoặc nhánh đánh lừa. Dây đã cấp điện sáng vàng và có bộ đếm đã nối. Đếm 90 giây sau trang hướng dẫn. Xoay thử không gây lỗi; xác nhận sai/hết giờ mới thất bại. Lần sai đầu duy nhất gọi ba lính; xử lý nhóm này mới thử lại. Nhóm không rơi loot. Lần sai sau có thể thử lại, không có quân vô hạn.

## Thông số và Inspector

Mở scene `Assets/_Project/Scenes/Maps/Map 1.unity`, chọn component **Map01OperationsLayout**. Các mảng Supply/Radio/Commander Guards chứa nhóm 4/5/5, không gồm sĩ quan hoặc nhóm phản ứng. Key Keeper là một actor cố định trong nhóm điểm 1; giữ tên và đường dẫn actor để giữ SaveId.

| Thông số | Giá trị |
|---|---:|
| Interaction Range | 2,4 m |
| Khóa hòm | 3 vòng, 0–5, 6 lần/lượt |
| Bảng mạch | 5×4, 20 đoạn, 90 s |
| Nhóm phản ứng | 3 người, một lần |
| Response Clearance | Tối thiểu 18 m; ngoài khung hình, có đường đi |
| Observation Distance | 18–120 m, nhìn vào khu doanh trại trong góc 35° |
| Observation Seconds | Tối đa 1,2 s, đứng yên và giữ F; không cần chĩa đúng sĩ quan |
| Cảnh thao tác hàng | 6,8 s, cộng thời gian bước tới marker |
| Bàn giao combat sau báo cáo | Ít nhất 1,5 s trước phát bắn |

Các marker chỉnh được: Door/Chest/Radio Approach, Handoff, Response Sites, Observation Sites. `Map01SupplyProps` chứa pivot cửa/nắp, collider/obstacle cửa, khóa và túi. Tường kho và hòm có NavMeshObstacle để lính không đi xuyên. Mở cửa gỡ obstacle cửa, giữ nguyên các tường.

**Lệnh Prepare là công cụ dựng lại bố trí, sẽ áp lại roster và tuyến tuần tra mặc định.** Sau khi chỉnh marker/patrol thủ công, lưu scene trực tiếp; không chạy Prepare nếu muốn giữ các chỉnh tay đó.

## Blender và chỉnh animation/camera

- Nguồn prop: `SourceArt/Map01Operations/Map01Operations.blend`; dựng lại bằng `build_props.py`.
- Bàn đài: `SourceArt/Map01Operations/RadioWorkbench.blend`, dựng bằng `build_radio_bench.py`. Đài đặt trên bàn cao 0,75 m; bảng kế hoạch chuyển ra cạnh lều để chừa chỗ thao tác. NavMesh mới chỉ áp dụng scene Map 1 qua `OperationsNavigation.asset`.
- Nguồn animation: `SourceArt/Map01Operations/Nam_SupplyInteractions.blend`; dựng lại bằng `author_supply_animation.py`.
- Clip bảng đài nằm trong `Nam_RadioPanel.blend`; tái tạo bằng `author_supply_animation.py -- --radio-only`. Marker Radio Right Contact/Radio Left Contact xác định tiếp xúc đầu ngón và lòng bàn tay; giữ tư thế thân nghiêng nhẹ, không kéo giãn cánh tay.
- Rig Nam giữ nguyên **62 xương thực tế trong Player.fbx**, gồm ngón tay; script kiểm tra rest skeleton không đổi. Ba action riêng: Nam_SupplyCollect, Nam_SupplyHandoff và Nam_RadioPanel, 30 fps, 6,8 s. Không ghi đè clip dùng dao, chạy, cứu Hùng hoặc Map 2.
- Trong Blender chọn Nam → Dope Sheet → Action Editor → chọn action. Chỉnh key vai/khuỷu/cổ tay và ngón tay, xem đồng thời hòm tham chiếu. Đánh dấu nhịp chạm nắp, nắm quai và thả túi; giữ cổ tay gần trung tính, chuyển động tại vai và khuỷu.
- `author_supply_animation.py` tái tạo lại action nên sẽ thay chỉnh tay. Để giữ chỉnh tay, xuất riêng action đang chọn bằng các tham số FBX ở cuối script, với frame 1–205, 30 fps, chỉ armature, không thêm leaf bones.
- Import clip: menu **ShadowVale/Story/Prepare three operation puzzles** có bước import animation. Nếu chỉ cập nhật clip, gọi `Map01OperationAnimationSetup.Import()` để giữ marker chỉnh tay.
- Unity references: `Assets/Resources/OperationsAnimation/Nam_SupplyCollect.anim` và `Nam_SupplyHandoff.anim`. `SupplyHands.mask` phải giữ transform curves của ngón tay; bỏ mask sẽ làm clip body chạy nhưng bàn tay duỗi. Điểm cầm quai cao 0,43 m tính từ pivot túi. Session tương tác khôi phục controller, layer, root motion, vũ khí, aim, camera và chặn click lọt sang bắn.
- Góc máy tương tác nằm ở `Map01OperationSession.Sample`: vị trí tương đối với Nam, điểm nhìn giữa tay/hòm và thân, FOV 68. Giữ kiểm tra spherecast chống xuyên tường khi chỉnh.
- Prop FBX có transform bù trục và đơn vị. Các wrapper Unity giữ đơn vị mét; không đặt lại rotation/scale của visual FBX con về mặc định. Pivot quay theo trục thế giới đã hiệu chỉnh trong Map01SupplyProps.

## Save, retry và tương thích

Giữ LayoutVersion 7, toàn bộ stage ID và SaveId lính cũ. Snapshot story revision 3 thêm chìa, cửa/hòm, mã/ghi nhớ, cargo, báo cáo, bàn giao, thưởng, bảng mạch, nhóm phản ứng và nhịp điểm 3. Lính động được tạo trước khi áp snapshot. Không lưu giữa mini game/animation. Checkpoint giữ cả đồ, đạn, máu và trạng thái địch; Enter khi chết ở các điểm dùng checkpoint nhiệm vụ.

Save cũ ở stage 4 có thêm bước mang hàng về nhưng đánh dấu thưởng cũ đã trao; stage từ 5 đã bàn giao, từ 7 đã qua đài. Sĩ quan chết sớm không hồi sinh; vẫn phải quan sát/xác nhận và báo cáo, không nhảy thẳng ra bến. Không ép người chơi hạ hết lính bảo vệ điểm 3.

## Audio và tình trạng nghiệm thu

Các câu mới nằm trong Map01OperationLines và manifest ở SourceArt/Dialogue/Map01Operations. Kịch bản đã được duyệt qua kế hoạch; **20 câu đã được thu và nhập ngày 16/10/2026 với trần 30 credits được duyệt**. Giữ giọng hiện tại: Nam Brodie, Hùng Clint, chỉ huy Tom, lính Martin; tiếng Việt vi, tốc độ 0,9. Có bản sạch cho mọi câu và bản đàm riêng cho bốn câu từ chỉ huy. Mẫu nghe ở SourceArt/Dialogue/Map01Operations; phát âm vẫn cần nghe duyệt, kiểm tra tín hiệu không thay cho thẩm định phát âm. Nam dùng bản sạch, chỉ huy từ đầu đàm dùng bản radio; lời địch là tiếng trực tiếp tại actor. Chỉ tạo audio trả phí sau khi có mẫu giọng và ngân sách mới được duyệt.

Prop, icon và SFX ngắn được tạo cục bộ, có script và SHA-256 trong asset-receipt.json. Không dùng lại ngân sách audio cũ; lần bổ sung này dùng đúng trần 30 credits đã duyệt. Thiếu CLI game-dev nên kiểm chứng dùng Unity Test Runner, Blender và ảnh/video Unity; không có sealed bundle của game-dev.

Kết quả test và ảnh ở `Logs/Map01Operations`; bản bàn giao kết quả cuối được ghi riêng khi kiểm tra xong. Không coi test trạng thái là nghiệm thu bàn tay, bàn chân hoặc chất lượng giọng đọc.


Kết quả và giới hạn nghiệm thu: xem [Map01_ThreeOperations_Validation.md](Map01_ThreeOperations_Validation.md).
