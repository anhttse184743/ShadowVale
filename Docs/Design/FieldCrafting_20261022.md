# Đề xuất tiếp đạn AK và chế tạo dã chiến

Ngày 22/10/2026. Phương án đã được người dùng duyệt và triển khai vào mã nguồn. Xem [kết quả triển khai và giới hạn kiểm tra](../Reviews/FieldCrafting_20261022/KET_QUA.md). Chưa commit/push, chưa chạy playtest Unity vì Claude vẫn đang hoạt động. Các con số là mức khởi đầu cho playtest, không phải thông số được sao chép từ game tham khảo hoặc kết quả đã cân bằng bằng lượt chơi.

## Quyết định đề xuất

Nam mang sẵn **bộ dụng cụ dã chiến**; người chơi mở **Tab → Chế tạo dã chiến** để sử dụng. Bộ dụng cụ là trang bị thường trực, không phải bàn mini phải thả xuống đất, không chiếm một ô vật phẩm tiêu hao và không bị dùng hết. Tab là giao diện, bộ dụng cụ là lời giải thích cho thao tác của nhân vật.

Cho làm băng, bộ sửa chữa và lắp khói từ module đóng sẵn trên đường. Cho ráp một lượng nhỏ đạn AK khi có vật tư thu hồi và gói tiếp liệu đúng loại đã chuẩn bị sẵn. F1, đạn Mosin và sửa súng đầy đủ giữ tại bàn. Thế giới vẫn chạy khi Nam chế; thời gian và việc phải ở yên tạo rủi ro thay vì cho chế liên tục trong lúc chạy/bắn.

## Hiện trạng đã kiểm tra trong mã nguồn

- `Map01M16.RifleAt`: mỗi nhóm đủ 10 vị trí phân 7 M16 và 3 AK, có xáo theo seed. Đây không phải bảo đảm 30% trong một nhóm nhỏ người chơi vừa gặp.
- `Map01Logistics.GuardPack/MatchGuardAmmo` chuyển đạn súng dài trong túi lính về loại đạn khẩu lính đó cầm. Thiếu AK trong túi Nam không khiến lính M16 rơi AK. Điều này giải thích vì sao chơi tiếp có thể tích nhiều 5,56 nhưng cạn 7,62.
- AK vẫn có nguồn: thùng đầu căn cứ 60 viên, thưởng bàn giao C1 30, hòm C2 60, kho căn cứ bán theo phần 15 và túi lính AK. Đây là nguồn có điều kiện, không bảo đảm người chơi còn đạn trước C3.
- `Map01Inventory.TryCraft` chặn toàn bộ công thức khi xa bàn. Công thức AK ở bàn hiện là 2 đơn vị `gunpowder` + 1 `scrap_metal` → 10 viên trong 5 giây. Chưa có thu hồi đạn M16 thành vật tư.
- Hàng chế ở bàn được xếp trước và có thể hoàn thành khi Nam đi xa. Không thể chỉ bỏ kiểm tra khoảng cách rồi coi đó là chế dã chiến.
- Bản túi hiện tại có 40 ô, đạn 99/ô, các món thường 64/ô. Bộ dụng cụ nên nằm ở phần trang bị; hai vật tư mới chỉ cần hai chồng trong túi ở lượng mang thông thường.

## Vòng tài nguyên M16 → AK

Thêm hai vật phẩm game, không mô phỏng quy trình gia công đạn ngoài đời:

| Món | Vai trò | Nguồn |
|---|---|---|
| Vật tư đạn thu hồi (`ammo_recovered`) | Tài nguyên đạn tượng trưng thu được khi bỏ đạn không dùng | Thu hồi đạn M16 trong túi |
| Gói tiếp liệu AK (`ak_loading_pack`) | Vật tư đúng loại đã chuẩn bị sẵn ở căn cứ để ráp một phần đạn AK | Nguồn chắc chắn theo nhiệm vụ và kho |

Quy đổi đề xuất:

1. **10 viên M16 trong túi → 1 vật tư đạn thu hồi.** Chọn 10/20/30 viên, thao tác một đợt 3 giây; tối đa 30 viên/đợt.
2. **2 vật tư đạn thu hồi + 1 gói tiếp liệu AK → 10 viên AK.** Dã chiến 8 giây, ở bàn 5 giây, cùng sản lượng.

Một vòng đầy đủ: **20 M16 + 1 gói tiếp liệu AK → 10 AK**. Hao 50% số viên, là đường tận dụng tiếp tế khác loại khi cần, không thay nguồn AK trực tiếp. Ví dụ mang 60 M16 và 3 gói tiếp liệu có thể nhận tối đa 30 AK, không phải 60. Chỉ đạn dự trữ được thu hồi; đạn đang trong băng phải tháo ra bằng thao tác riêng. Màn xác nhận ghi rõ số còn lại.

Không cho AK biến ngược thành vật tư trong bản đầu. Không thu hồi từ vỏ đạn đã bắn, không lấy thuốc từ đạn rồi tạo vô hạn đạn/bom. Không tăng sát thương hay xác suất kẹt của đạn AK chế được: đầu vào và thời gian đã là cái giá, không thêm may rủi làm hỏng trải nghiệm.

Giữ công thức AK tại bàn đang có như một đường sản xuất trong xưởng. Công thức đó không được mở ra dã chiến: ngoài đường chỉ có đường ráp từ gói tiếp liệu đúng loại. Khi đủ tài nguyên đã thu hồi, giao diện chọn ráp AK; khi chưa có, có thể có nút rõ ràng “Thu hồi 20 M16 rồi ráp 10 AK”, tổng 11 giây, với đầy đủ đầu vào hiển thị. Không tự tháo đạn khi người chơi chưa chọn thao tác đó.

## Gói tiếp liệu phải được cấp chắc chắn

Không đặt một vật tư hiếm mới vào giữa đường cứu thiếu đạn:

| Mốc | Đề xuất cấp gói tiếp liệu AK |
|---|---:|
| Bộ dụng cụ ban đầu của Nam | 2 |
| Thưởng bàn giao C1 | 2 |
| Hòm C2 | 2 |
| Tiếp tế trước C3 | 2 |

Thu đủ bốn mốc có 8 gói, tương ứng tối đa 80 AK nếu hy sinh 160 M16. Kho cấp thêm gói với giá thử 3 điểm/gói; mua đạn AK trực tiếp vẫn là lựa chọn có lợi khi đang ở căn cứ. Không tạo đường mua M16 rồi tái chế để bán lãi: 20 M16 đang có giá trị kho 20, cộng gói 3, chỉ thành 10 AK có giá trị 10.

Đồng thời nên thêm **một băng AK 30 viên chắc chắn ở điểm tiếp tế trước C3**. Vòng tái chế là phương án bổ trợ; người đã cạn cả đạn lẫn nguyên liệu vẫn cần một nguồn tiếp tế có thể tiếp cận. Với save đã lấy điểm cũ, dùng ID túi đạn bổ sung riêng, chỉ lấy một lần; không reset hòm/điểm đã nhặt. Bộ dụng cụ được bổ sung cho save cũ một lần, không bắt quay về đầu màn.

## Chế ở đâu và vì sao

| Món / thao tác | Tab dã chiến | Bàn chế tạo |
|---|---|---|
| Băng cứu thương | Có, 4 giây; dùng công thức vải/thảo dược hiện có | Có |
| Bộ sửa chữa nhỏ | Có, 8 giây; giữ nguyên liệu hiện có | 6 giây |
| Dùng bộ sửa chữa lên súng | Có; giữ giới hạn sửa dã chiến hiện tại | Có thể sửa đầy đủ theo hệ đang có |
| Khói | Có, 6 giây, chỉ lắp từ module khói đã chuẩn bị | 4 giây |
| Đạn AK từ vật tư thu hồi | Có, 8 giây/10 viên, cần gói tiếp liệu AK | 5 giây/10 viên |
| AK từ nguyên liệu thô theo công thức hiện tại | Không | Có |
| Đạn Mosin | Không ở bản đầu; giữ giá trị chuẩn bị trước của súng một phát/200 m | Có |
| F1 | Ngoài đường mang và sử dụng quả hoàn chỉnh | Chế ở bàn |
| Rã súng, sửa đầy đủ, thay bộ phận | Dùng hệ sửa dã chiến có giới hạn; chưa mở sản xuất/nâng cấp lớn | Có |

Kích thước món đồ không phải tiêu chí duy nhất. Băng và bộ sửa chữa là nhóm gom/lắp đồ có sẵn; gói đạn cần vật tư đúng loại; F1 cần phần chuẩn bị tại trạm. Không thêm động tác chế tạo mô phỏng đúc vỏ hay sản xuất vật liệu nổ bên đường. Nguồn F1/khói hoàn chỉnh theo nhiệm vụ và kho đã có vẫn được giữ.

## Trải nghiệm thao tác

Mở Tab, chọn “Chế tạo dã chiến”. Mỗi dòng hiển thị số sẽ nhận, đầu vào, thời gian và điều kiện. Ví dụ: “10 đạn AK · 8 giây · 2 vật tư thu hồi + 1 gói AK”. Món cần bàn vẫn hiện với lý do “Cần bàn chế tạo”, tránh người chơi tưởng công thức biến mất.

Nam đứng yên/khom, cất vũ khí và dùng bộ dụng cụ; chỉ làm một việc mỗi lần, không xếp hàng sản xuất nền. Không bắt thả một bàn vật lý, chọn vị trí đặt và nhặt bàn lại. Chạy, nhảy, bơi, đổi vũ khí, bị thương hoặc đóng thao tác sẽ hủy. Địch và thời gian vẫn chạy, không có bất tử; người chơi có thể thử làm sau cover mà không bị một cờ giao chiến dai dẳng khóa cả hệ thống.

Nguyên liệu được giữ riêng khi bắt đầu, thành phẩm chỉ cấp khi hoàn tất. Hủy trả lại đúng phần đang giữ một lần. Nếu túi thay đổi và đầy, vật liệu hoàn trả/thành phẩm đi qua cơ chế nhận hoặc rơi dưới chân đang có. Pause chỉ dừng tiến độ; tải lại không được nhận cả nguyên liệu lẫn thành phẩm. Đối với bản đầu, kết thúc/hoàn nguyên việc dã chiến trước khi chụp save, không lưu một queue đang tự chạy khi Nam rời đi.

## Tham khảo và giới hạn xác minh

- [Naughty Dog: cách dùng hệ chế tạo](https://feedback.naughtydog.com/hc/en-us/articles/360059856052-How-do-I-use-the-crafting-system-How-do-I-craft): xác nhận chọn món trong menu, giữ nút để chế và một số món có thể chế từ weapon cross. Tham khảo để đưa hành động vào UI sẵn có, không thêm một vật thể bàn phải đặt.
- [Ubisoft: Ghost Recon Breakpoint Beta Survival Tips](https://news.ubisoft.com/en-us/article/5IY1i3398BPcn4wUZyoyxR/ghost-recon-breakpoint-beta-survival-tips): tài liệu chính thức mô tả bivouac là nơi chuẩn bị, chế vật tư và các vật phẩm chiến đấu; đồng thời nhấn mạnh thời gian xử lý thương tích khiến người chơi dễ bị tấn công. Tham khảo vai trò chuẩn bị ở trạm và cái giá phải dừng tay.
- [Video series chính thức Inside The Last of Us Part II](https://blog.playstation.com/?p=224416), gồm tập Inside the Gameplay, và video nhúng trong trang Ubisoft ở trên là nguồn video để đối chiếu thêm. Đã kiểm tra trang công bố/nhúng; trình đọc web không lấy được nội dung/transcript YouTube, nên không tuyên bố đã xem hết hoặc gán thông số cụ thể cho các video. Những nhận định cơ chế ở đây dựa trên tài liệu chính thức đọc được và mã dự án.

Các tỉ lệ, thời gian, nguồn gói và quyết định F1/Mosin thuộc đề xuất riêng cho ShadowVale. Chưa chạy playtest phương án này.

## Phạm vi triển khai sau khi chọn phương án

Thêm định nghĩa vật tư, hình/tooltip, nguồn cấp hữu hạn; bổ sung phần chế dã chiến trong túi và một job có nguyên liệu giữ riêng. Tách job này khỏi queue bàn đang có. Gắn điều kiện truy cập vào recipe thay vì mở tất cả `TryCraft` khi xa bàn. Giữ cùng nền inventory cho Map 1/2; thêm default/migration rõ ràng cho save cũ.

Kiểm tra cần thiết khi triển khai: chỉ trừ đạn dự trữ; 20 M16 tạo đúng lượng ráp 10 AK; hủy/bị thương/túi đầy/lưu tải không nhân vật tư; nhận kit/gói không lặp; các nguồn AK hiện có vẫn đúng; hoạt động ở cả Map 1 và Map 2. Playtest một lượt ưu tiên AK và một lượt ưu tiên M16 để điều chỉnh tỉ lệ 2:1, thời gian và số gói theo đạn thực sự còn trước C3. Không giảm thêm sức mạnh M16 để bù cho thiếu nguồn đạn AK.
