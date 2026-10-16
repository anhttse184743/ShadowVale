# Map 1 — Cứu Hùng và hộ tống

## Luật chơi

Nam có thể tiếp cận từ ba hướng quanh bến. Bốn SaveId cũ được giữ nguyên. `patrol_0` canh Hùng; ba tên còn lại tuần tra riêng. Khi tên canh còn sống, xác nhận nhìn thấy Nam hoặc nghe súng sẽ lập tức chốt báo động và xử bắn Hùng. Đá, tiếng chân và tiếp đất chỉ gây điều tra. Sau khi ám sát tên canh, có thể dùng súng với ba tên còn lại.

Trang bị dao, tới sau lưng trong 1,6 m, góc ít nhất 130°, không bị vật cản và địch chưa giao chiến: nhấn tấn công để kết liễu. Đâm phía trước vẫn là đánh thường. Những lính khác dừng trong cả động tác kết liễu; góc cận xuất hiện ngắn ở nhịp ra dao. Không thêm phát hiện xác chết.

Hạ đủ bốn tên, tới Hùng và nhấn E. Không cần thảo dược hoặc băng cứu thương. Chờ Nam tháo dây và Hùng đứng lên rồi hộ tống. Có ba đợt truy kích, hai lính mỗi đợt. Cả hai tới vùng an toàn trước hầm sẽ ngừng truy kích. Tới cửa hầm sẽ tự chạy vào và bàn giao tiếp tế, chuyển sang stage 2. Không cần trả nhiệm vụ tại thùng. Thùng tiếp tế vẫn dùng sau đó.

## Marker và thông số Inspector

Mở prefab `Assets/Resources/Rescue/Map01RescueLayout.prefab` bằng Prefab Mode. Các vị trí lưu theo tọa độ thế giới của Map 1; khi chạy, prefab được tạo một lần. Có thể đặt một instance trong scene để xem marker với địa hình, nhưng không được đặt hai instance. Muốn lưu chỉnh sửa, thực hiện ngoài Play Mode.

| Trường | Mặc định | Ý nghĩa |
|---|---:|---|
| `guardPosts` | 4 | Điểm đứng gốc và hướng nhìn của bốn lính |
| `patrolRoutes` | 3 vòng, mỗi vòng 4 điểm | Thứ tự waypoint con là thứ tự tuần tra; tên canh không có vòng |
| `patrolVision / patrolAngle` | 20 m / 95° | Tầm nhìn và góc nhìn của nhóm cứu Hùng |
| `patrolSpeed / patrolPause` | 1,94 m/s / 2,5 s | Tốc độ và thời gian dừng nhìn tại waypoint |
| `pursuitSites` | 23 | Điểm spawn trên đất, có đường NavMesh tới tuyến hộ tống |
| `spawnClearance` | 18 m | Khoảng cách tối thiểu tới cả Nam và Hùng; thêm kiểm tra ngoài tầm camera |
| `waveThresholds` | 0,80 / 0,55 / 0,30 | Tỉ lệ quãng đường NavMesh còn lại so với lúc bắt đầu hộ tống |
| `safeEntry / safeRadius` | 8 m | Cả hai phải tới vùng này mới dừng truy kích |
| `shelterDoor / doorRadius` | 3,5 m | Cả hai tới gần cửa sẽ bắt đầu cảnh vào hầm |
| `shelterRun` | 4 điểm | Theo lối dốc thật từ ngoài cửa vào sàn hầm |
| `reportPoint` | Trong hầm | Vị trí kết thúc của Nam; Hùng dừng lệch sang bên |
| `backstabRange / backstabAngle` | 1,6 m / 130° | Điều kiện ám sát trước khi gây sát thương |
| `killCameraSeconds` | 0,9 s | Góc cận ở nhịp kết liễu; bị che sẽ giữ camera gameplay |
| `shelterSeconds` | 8 s | Thời lượng chạy vào hầm, gồm tăng/giảm tốc và dừng |
| `namRunSpeed / hungRunSpeed` | 4,34 / 6 m/s | Tốc độ gốc của clip chạy; dùng để đồng bộ bước chân với đường đi |
| `shelterCameraOffset` | (3; 2,1; −4) | Góc ngoài cửa, tính theo hướng chạy của Nam |
| `interiorCameraOffset` | (−2; 1,8; −4) | Góc trong hầm, tính từ điểm bàn giao; tránh NPC ở bàn chỉ huy |
| `shelterCameraFov / cameraDamping` | 54° / 0,22 s | Độ rộng và độ mềm khi chuyển camera |

Sau khi dịch waypoint, dùng Unity Navigation để kiểm tra từng điểm nằm trên đất, có path hoàn chỉnh và không cắt nước/công trình. `ShadowVale > Rescue > Prepare stealth rescue markers` tạo lại bố trí chuẩn từ script `RescueMissionSetup.cs`; sao lưu prefab đã chỉnh trước khi chạy lại vì lệnh này dựng lại marker. Bản `.anim` đã tồn tại được giữ lại để không mất chỉnh sửa thủ công.

## Chỉnh animation thủ công

1. Cảnh vào hầm dùng bốn clip `.anim` chỉnh được trong `Resources/Rescue`: Nam/Hùng Run và Idle. Gán clip tại `Map01RescueLayout` trong Inspector. Những clip này là bản tái sử dụng từ animation hiện có, giữ rig/skeleton gốc.
2. Để chỉnh bước chạy, mở **Window > Animation > Animation**, chọn nhân vật cùng Avatar gốc, kéo bản `.anim` vào Animation Window. Chỉnh đường cong chân/hông theo frame rồi xem lại từ góc bên. Bật Foot IK của playable đã có; đừng tăng tốc di chuyển riêng mà giữ clip chậm, vì sẽ trượt chân.
3. Động tác kết liễu dùng hai state đang có: `Act_Takedown` của `AC_Player`, và state Silent/Death tương ứng trong controller địch. Hai clip `Nam_Takedown` và `Enemy_Takedown_Victim` phải giữ cùng số frame/thời lượng, cùng gốc chuyển động; IK tay tại `Map01TakedownIK` hỗ trợ tiếp xúc cổ/miệng. Không đổi riêng một clip rồi bỏ lệch clip kia.
4. Với clip trong FBX chỉ đọc: chọn clip con, **Ctrl+D** để tạo bản `.anim`, chỉnh bản đó rồi gán vào state trong Animator. Sao lưu controller trước khi thay. Không sửa xương hoặc thêm leaf bones. Nếu chỉnh ở Blender, bake 30 fps và xuất cùng rig gốc; Unity tiếp tục dùng Avatar cũ.
5. Cởi trói vẫn dùng `Act_Untie`; Hùng đứng lên bằng `DungDay`. Phần hộ tống chỉ bắt đầu sau khi hai động tác kết thúc. Nếu thay clip, giữ quá trình quỳ → đứng liên tục, kiểm tra đế giày trên bến và cả sàn căn cứ.

Tiếp xúc mặt đất được tính từ xương đang diễn hoạt, không đổi model hay bật Read/Write trên FBX. `SkinContactData.asset` lưu dữ liệu tham chiếu của hai model gốc. Chỉ khi thay **mesh hoặc bind pose**, chạy **ShadowVale > Rescue > Bake character ground contacts** ngoài Play Mode để cập nhật dữ liệu; sửa keyframe đơn thuần không cần bake lại.

## Chỉnh camera

`Map01RescueCinematic.cs` chọn góc trái/phải cho kết liễu, kiểm tra vật cản, chuyển mềm khoảng 0,9 giây ở nhịp ra dao. Camera chạy vào hầm chuyển từ góc bên sang nhìn dọc lối dốc, rồi tới góc bên trong. Chỉnh offset, FOV và damping tại prefab trong Inspector; `LateUpdate()` vẫn kiểm tra tường/mái bằng SphereCast. Các camera mở đầu/boss/radio/boarding hiện có được giữ nguyên.

## Save và retry

Stage ID giữ nguyên: Rescue=0, Escort=1, Briefing=2 và các stage sau. Dữ liệu bổ sung là tùy chọn trong checkpoint v7: pha cứu Hùng, máu Hùng, bốn lính, mask ba đợt, snapshot lính truy kích, cờ vùng an toàn và cờ nhận thưởng. Load dựng lính động trước khi áp snapshot, theo SaveId duy nhất.

Checkpoint đầu giải cứu và đầu hộ tống chứa cả inventory, ammo và trạng thái địch; retry tải lại đúng checkpoint, tránh lặp loot/thưởng. Save cũ giữ các lính đã chết; save hộ tống cũ đánh dấu bỏ qua các ngưỡng đã đi qua để tránh spawn dồn. Không lưu khi ám sát, cởi trói, đứng dậy, xử bắn hoặc chạy vào hầm.

Save hộ tống cũ chưa có checkpoint đầu đoạn sẽ tạo điểm thử lại tại vị trí vừa khôi phục, giữ những đợt đã bỏ qua. Để thử đầy đủ cả ba đợt, chọn **New Game** và cứu Hùng từ đầu; không dùng save đã ở gần căn cứ. Không xóa save đang chơi để kiểm thử.

Giữ Esc một giây trong cảnh xử bắn hoặc vào hầm tới cùng trạng thái như xem hết. Ám sát không bỏ qua để hai clip tiếp xúc kết thúc đồng bộ. Các cutscene khác tiếp tục dùng cơ chế bàn giao controller/aim đã có.

## Chạy kiểm tra

Trong Unity Test Runner, chạy `RescueTests` cho luật nhiệm vụ/save/retry, `RescueCompatibilityTests` cho save cũ/dao/vật che, `RescueMotionTests` cho tiếp xúc mặt đất và bàn giao, `RescueVisualTests` cho giữ Esc/aim cùng camera review có điều khiển bằng fixture. Video review là playback thật từ camera Unity với thao tác do kiểm thử sắp xếp; không phải video người chơi tự điều khiển toàn bộ nhiệm vụ. Video không thu HUD OnGUI hoặc âm thanh. `frames.csv` và `pose-metrics.csv` lưu mốc pha và vị trí cơ thể để đối chiếu.

Sau khi nhận bản cập nhật: dừng Play, chờ Unity nhập script/asset xong, rồi **New Game** để xem đủ bố trí mới. Dao chỉ kết liễu ngay khi đủ điều kiện sau lưng; đâm chính diện vẫn gây sát thương thường. Chết do báo động dùng nút thử lại nhiệm vụ để phục hồi checkpoint, không reload một save sau khi đã tiêu hao đồ.
