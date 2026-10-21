# Ghép dev vào thinh — 20/10/2026

## Phiên bản và mục tiêu

- Thinh gốc: `e44dab1e4be14f14d3524dc731486508b8677242`.
- Dev đã fetch riêng: `a1e2b9a9b0e71b42316fd9ecf9174c279c6304d1`.
- Không fetch hay sửa `origin/dev2`.
- Kiểm thử dev trước; ghép trong worktree riêng; giữ các thay đổi chưa commit của thinh; chỉ đưa bản đã kiểm tra về dự án chính.

## Các lựa chọn người dùng đã chốt

1. Dùng AI chỉ huy mới của dev; giữ biên C3 và quân số hiện tại.
2. Nhận các thay đổi scene của dev; giữ nhà trại C3 đã duyệt.
3. Dùng cân bằng chiến đấu mới của dev; giữ quân số C3. `reinforce_count` vẫn 3, không thêm 4 lính mới từ ngoài quân số hiện có.
4. Các lựa chọn từ công việc trước vẫn giữ: K54 thường, Mosin bắn xa không tự lộ vị trí, dao cạnh giường im lặng, ngủ/thức, nhận lệnh bằng E cạnh chỉ huy, giữ ô lưu thủ công.

## Phạm vi file và cách xử lý

- `BossRelocation.cs`, `BossBehaviour.cs`: giữ chiến thuật mới (giữ hướng, lùi sau đồng đội) cùng bộ lọc `AllowedPosition`.
- `Map01ConfinedBody.cs`: chuyển tiếp `Velocity` và `FacesManually` mà giao diện dev mới thêm; mọi đường đi và vị trí né vẫn qua giới hạn C3.
- `Map01Quest.cs`, `Map01C3Mission.cs`: tiếp tục dùng `Map01C3Reinforcements` và lực lượng sẵn có; không thay thân hàm `CallReinforcements` hay chính sách kẻng/đường dây.
- `Map01Story.cs`: giữ hàm gọi phản ứng hiện tại, thêm phần reset vòng mạch điện thất bại của dev.
- `Map01Rescue.cs` và ba catalog/manifest giọng: nhận lời cứu Hùng đã khớp giọng thu mới của dev.
- `EnemyFeedback.cs`: giữ âm súng lính riêng của thinh; nhận xử lý đường đạn/nòng súng và hiệu ứng mới của dev.
- Ghép ba chiều 40 file đang sửa và chép nguyên tài sản chưa commit vào worktree thử. Báo cáo chi tiết ở `local_overlay.json`.
- Giải quyết riêng các giao điểm ở HUD, chế tạo/túi đồ, động tác tay, thoại và combat. Không bỏ cơ chế Mosin, K54, dao hay vật ném hiện tại.
- Scene mới: đối chiếu toàn bộ 2.583 block dữ liệu dưới `C3_MainGarrison_Art`, không block nào khác bản chính. Xem `C3_scene_audit.json`.

## Sao lưu và rủi ro

- Sao lưu 4.058 file, 5.757.716.976 byte tại `Temp/MergeDev_20261020_Before`, kiểm tra SHA256 sau chép và kiểm lại nguồn. Có bản sao các file save tìm thấy.
- Rủi ro chính: giao diện AI mới, controller hoạt ảnh chung, đường đi quanh đồ tương tác mới, thoại mới, mất dữ liệu do test ghi save. Test dùng thư mục lưu riêng trong worktree.
- Không sửa cửa sổ Blender đang mở. Không tắt Unity của người dùng. Hai tiến trình test dùng hai worktree khác nhau.
- Chưa commit hoặc push. Chỉ hỏi duyệt commit sau khi có bản ghép cụ thể và kết quả kiểm tra.

## Kiểm thử

- Dev: biên dịch và 122/122 kiểm thử logic đã đạt (`dev-pure.xml`).
- Dev: đang chạy C3, chỉ huy, thao tác nhiệm vụ, tuần tra, tiếp tế, rút quân, lưu slot và mở đầu.
- Bản ghép: lần biên dịch đầu tìm thấy lớp giới hạn C3 thiếu hai thuộc tính giao diện mới; đã sửa lớp chuyển tiếp, đang chạy lại.
- Sau đó chạy hồi quy Mosin, dao cạnh giường/ngủ-thức, K54, đồ ném, E nhận lệnh và save; ghi kết quả thật vào `test_results.json`.
- Chưa kết luận FPS hay hiệu năng của bản ghép qua các test chức năng.

## Tài liệu đã đọc

| Nguồn | Điều áp dụng |
|---|---|
| [Git: git merge](https://git-scm.com/docs/git-merge) | Dừng trước commit, không làm mất thay đổi chưa commit; kiểm tra trên worktree riêng trước. |
| [Unity 6000.3: Smart Merge](https://docs.unity3d.com/6000.3/Documentation/Manual/SmartMerge.html) | Scene và prefab cần kiểm tra cấu trúc/tham chiếu; không coi tự gộp văn bản là bằng chứng scene hoạt động. |

Lượt này nghiên cứu tích hợp code; không có video animation mới được xem và không dựng lại model.
