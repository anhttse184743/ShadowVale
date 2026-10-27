# Đêm 2 — tuyến bờ mương tới hầm kho

N2_04 đã chuyển sang chọn một trong ba hướng theo `Night2_Evacuation_Choices.md` ngày 27/10/2026. Mô tả kiểm tra lần lượt bốn đoạn dưới đây là lịch sử của N2_04 cũ, không còn là điều kiện hoàn thành.

Quy tắc mới nhất về chặn đường, sát thương, máu lính và thời điểm ngừng quét nằm trong `Night2_Stealth_Balance.md`: chỉ chặn sông, không khóa hành lang; tới cửa kho dừng tuần tra. Các mô tả khóa hành lang phía dưới là lịch sử thiết kế đã được thay thế.

Áp dụng theo ảnh đánh dấu `codex-clipboard-0f9a7615-e167-44ab-b324-560a664497b6.png`:
đường đỏ là tuyến Nam, vùng vàng là tổ tuần tra, vùng xanh là nắp hầm mới.
Các tọa độ được quy đổi từ camera tổng quan đã lưu và kiểm tra lại trên NavMesh/bờ sông của scene Map 2.

## Luồng nhiệm vụ

1. N2_01: đến cầu giữa, dùng ống nhòm quan sát tổ tuần tra phía ruộng.
2. N2_02: báo bộ đàm tại đầu cầu thật, sau đó đi men bờ mương phía đông sông.
3. N2_03: E mở nắp, đứng đúng điểm Space để dùng thang; S xuống hầm, đến kệ vật tư rồi E kiểm tra mới hoàn tất nhiệm vụ.
4. Hoàn tất N2_03 mở giới hạn di chuyển: lội trên đáy sông tự nhiên sang bờ làng, không tạo dải đường/nền mới trên nước.
5. N2_04: kiểm tra lần lượt bờ mương, lối xóm, bờ nam, rồi nhấn E xác nhận bậc bến.
6. N2_05: trở về điểm hẹn với Hùng.

Hầm hiện dùng model Blender và có nội thất, thang cùng cơ chế chống nhảy xuống. Xem `UndergroundStore_Blender_Integration.md`.
Kho ban ngày tại site `store` giữ vị trí cũ; hầm kiểm tra Đêm 2 dùng site riêng `nightHatch`.

## Giới hạn và bố trí

- Nắp hầm mới: X=-3.96654, Z=-51.858; cao độ NavMesh khoảng 0.809 m. Dời đúng 30 m so với vị trí (-3.971, -21.858), tiếp tục về phía nam xa cầu, nằm trong cụm `Rice_Young` thật. Cụm lúa gần nhất cách khoảng 0.17 m.
- Hàng lính: (36.2474, -18.8017), (31.6410, -27.6776), (27.0346, -36.5534) theo X/Z. Ba lính thẳng hàng, hai khoảng liền kề đúng 10 m; lính đầu cách mép đường 20 m theo mask đường thật.
- Khoảng cách tới bờ sông thật lần lượt 40 m, 41.25 m, 40 m. Chênh 1.25 m ở lính giữa là do bờ cong trong khi đội hình giữ hàng thẳng.
- Mỗi lính quét về đoạn bờ mương gần mình nhất; giữ góc quét 120°, tốc độ 0.5, tầm đèn/phát hiện 40 m và khoảng tối chung.
- Trước N2_02: chỉ mở đường từ làng tới cầu; chưa được rời cầu đi theo tuyến kho. Phải quan sát N2_01 rồi báo bộ đàm N2_02 trước.
- Sau N2_02 và trước N2_03: chỉ cầu quan sát và hành lang bờ mương rộng khoảng 6 m được phép đi.
- Kênh sông lấy từ `RiverWater` thật, kiểm tra cả dấu chân capsule bất kể độ cao nhảy. Không dùng collider vô hình chắn đạn/đèn.
- Giới hạn được áp dụng trước `CharacterController.Move`, kiểm tra cả đoạn di chuyển và cho phép trượt dọc biên. Đi, chạy, bò và nhảy không thể vượt biên bằng một bước dài.
- Sau N2_03: bỏ hoàn toàn bộ lọc di chuyển qua sông. Nam đi trên mesh đáy sông thật (khoảng -0.8 m), được hệ thống chống rơi nhận diện riêng trong vùng nước có mặt đất đỡ chân. Không thêm renderer, nền cao hay collider đường băng qua sông.
- Chỉ NavMeshLink chỉ đường mở khi N2_03 đã hoàn tất, không giới hạn người chơi phải băng qua đúng liên kết này. Load/retry checkpoint trước hầm sẽ khóa nước và tắt liên kết lại.
- Liên kết chỉ đường giữ hai đầu (-3.971, -23.858) và (-28, -26). Người chơi có thể chọn chỗ lội khác rồi đi theo ghim bờ làng.
- Hướng dẫn và ghim N2_01–02 dùng mặt cầu thật; N2_03 dùng nắp hầm; N2_04 chuyển ghim theo đoạn kiểm tra hiện tại.

## Kiểm tra

Biên dịch Gameplay và Map01 thành công bằng compiler Unity. Báo cáo native/Play Mode được lưu tại
`SourceArt/Map02NightBankFlow/night-bank-route-checks.txt`; mã kiểm tra tại `validate.cs`.

Đã xác nhận trên NavMesh và mesh collider mặt đất thật:

- Cầu nối tới toàn bộ hành lang và điểm đứng kiểm tra hầm; đường native không vượt vùng cấm.
- Cấm xuống sông, vòng ngoài ruộng và quay sang làng sau báo bộ đàm trước khi kiểm tra hầm.
- Các điểm trên hành lang đều có lượt quét đi qua, đồng thời có khoảng tối chung cho cả tuyến.
- Ba vị trí lính hợp lệ, cách nhau 10 m, hướng tâm quét về sông.
- Lính đầu cách đường 20 m; hàng lính cách bờ cong khoảng 40–41.25 m; nắp hầm dời đủ 30 m, nằm giữa lúa non.
- Hoàn thành quan sát mà chưa báo bộ đàm vẫn chưa mở lối vào tuyến kho.
- Hoàn tất hầm mở giới hạn; trạng thái lưu/load giữ kết quả mở khóa.
- Play Mode: CharacterController đi qua đáy sông thật ở ba đoạn khác nhau, thấp hơn -0.65 m mà không bị phục hồi/teleport; không có dải đường hoặc collider nền bổ sung.
- NavMeshLink nối qua sông theo đường khoảng 24.13 m; đầu ra nối tới tuyến kiểm tra sơ tán.
- Retry trước hầm đóng lại lối lội; các pha khác không bị bộ lọc Đêm 2 khóa hướng.

Đây là kiểm tra native và Play Mode trong project xác thực tách biệt; chưa chạy lại toàn bộ chiến dịch trong scene đầy đủ.
