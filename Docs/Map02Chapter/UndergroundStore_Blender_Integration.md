# Hầm kho Blender — Map 2

Đoạn tiếp cận hiện chỉ chặn sông; tới cửa kho sẽ dừng quét/bắn trước khi mở nắp. Xem `Night2_Stealth_Balance.md` cho cấu hình mới nhất.

## Model và kích thước

Hầm đặt tại site `nightHatch` trong ruộng lúa non, X=-3.96654, Z=-51.858. Cao độ mặt đất lấy từ NavMesh tại vị trí này.

- Một nắp gỗ kích thước danh nghĩa 2 × 3 m, có núm tròn, nẹp sắt, chốt bản lề. Mở một cánh quanh cạnh dài.
- Sàn nằm dưới mặt đất 7 m. Phòng thông thủy dài 8 m, rộng 5 m, cao 4 m; trần cách mặt đất 3 m.
- Thang bám vách xuống tận sàn, gồm hai thanh dọc và 24 bậc.
- Vách bằng ván gỗ đứng, cột và thanh giằng kiểu hầm chỉ huy Map 1. Sàn/trần là đất, có bề mặt không phẳng nhẹ.
- Ba kệ sát ba mặt tường, chừa chân thang và lối đi giữa. Có thùng gỗ, nhãn, vải gấp, bao và hộp kim loại.
- Hai đèn dầu có bình dầu, chụp, móc và dây treo; hai nguồn sáng Point màu ấm tương ứng.

Model được dựng bằng Blender 5.2.1, lưu nguồn tại `SourceArt/Map02UndergroundStore/NightStore.blend`.
FBX, texture và prefab chạy game ở `Assets/_Project/Map01/Resources/Story/NightStore/`.
Prefab chứa phần mỹ thuật; `Map02UndergroundStore` tạo collider, ánh sáng và điều khiển khi khởi tạo.

## Tương tác

1. Sau N2_01 và N2_02, đến kho. **E** mở/đóng nắp khi đứng gần điểm bên miệng hầm.
2. Chờ nắp mở xong. Đứng trên mặt đất, không nằm, tại điểm đánh dấu **Space · Dùng thang** (bán kính 0.8 m).
3. **Space** nhận điều khiển thang trước xử lý nhảy. Ở dưới hầm, đứng ngay sát chân thang hoặc trong vùng tiếp cận rộng 2.08 × 1.7 m để bám; không cần đứng chính xác tại điểm bước ra. Một chuyển tiếp ngắn đưa Nam vào vị trí bám thang; không tự xuống sàn.
4. Giữ **S** xuống, giữ **W** lên; thả phím thì dừng. Hai phím cùng lúc cũng dừng.
5. Tới cuối thang, Nam bước vào phòng hoặc bước ra đúng điểm trên mặt đất. Đổi vũ khí/tư thế thông thường được khôi phục.
6. Xuống phòng, đến kệ ở ghim nhiệm vụ và **E** kiểm tra mới hoàn tất N2_03. Mở nắp hay chỉ xuống thang không hoàn tất nhiệm vụ.
7. Ghim N2_04 chỉ về chân thang khi còn ở dưới; lên mặt đất mới chỉ tiếp tuyến sơ tán.

Nam cất vũ khí khi leo; tư thế tay/chân lấy rig humanoid đang có, tay được IK tới các bậc thang. Camera riêng giữ Nam và vách thang trong khung hình.

## Chống nhảy/rơi và lưu tiến trình

- Miệng hầm có collider chống rơi luôn bật, kể cả khi nắp mở. Collider này không bị tắt để cho người chơi rơi tự do.
- Chỉ giao dịch Space hợp lệ mới tạm tắt CharacterController của Nam và di chuyển có kiểm soát trên thang; tới hai đầu mới bật lại capsule.
- Bộ khóa di chuyển bờ mương chỉ được bỏ qua trong thể tích hầm khi người chơi đã vào hợp lệ bằng thang.
- Hệ thống chống rơi Map 2 có ngoại lệ giới hạn trong hầm cho người chơi có trạng thái vào hầm hợp lệ. Bên ngoài hầm vẫn giữ kiểm tra an toàn cũ.
- Không lưu giữa thang. Sau khi tới sàn/mặt đất có checkpoint; trạng thái nắp và người chơi ở trong hầm được lưu bằng các trường bổ sung tương thích save cũ.
- Retry tuyến tuần tra xóa trạng thái nắp/vào hầm. Hủy chuyển cảnh hoặc chết giữa thang đưa về đầu an toàn của lần vào thang đó và khôi phục capsule.

## Địa hình

Khi chạy, cắt đúng hình chữ nhật 2 × 3 m trên bản sao mesh mặt đất, giữ UV, màu đất và các tam giác bên ngoài. Không sửa asset địa hình gốc hoặc NavMesh dùng chung.
Chỉ dọn các bụi lúa/cỏ nằm trong nắp và ngay điểm đứng; lúa non quanh đó được giữ lại. Khi hủy hầm, trả lại mesh và mảng cây gốc.

## Kiểm chứng

Mã kiểm tra: `SourceArt/Map02UndergroundStore/validate.cs`. Chạy Play Mode trong project kiểm thử tách biệt với mesh mặt đất, NavMesh và cấu hình humanoid Nam gốc.
Kiểm tra gồm: kích thước/hướng FBX, nắp và collider, lỗ đất và UV, hai đèn/ba kệ, nhảy/rơi bị chặn, khóa nhiệm vụ, điểm Space, chờ nắp mở, S/W/dừng, pose không cộng dồn góc, tay chạm thang, cấm lưu giữa thang, load trong hầm, ngoại lệ chống rơi, lên mặt đất và khôi phục địa hình/cây.

Project kiểm thử dùng vật liệu Standard để render vì không chứa bộ shader URP. Vật liệu đưa vào project game được ghi theo mẫu URP/Lit sẵn có và giữ GUID của texture/prefab.
Chưa chạy lại toàn bộ chiến dịch trong scene chính; không can thiệp phiên Unity đang mở.
