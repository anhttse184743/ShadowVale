# Đêm 2 — ba hướng kiểm tra sơ tán

Cập nhật 27/10/2026 theo bản đồ người dùng đánh dấu. Thay thế riêng luồng N2_04 kiểm tra tuần tự bốn điểm. Quan sát và tiếp cận hầm kho N2_01–03 giữ luật hiện tại.

## Luồng nhiệm vụ

1. Kiểm tra kệ hầm kho xong, leo lên mặt đất.
2. Tới ghim bắt đầu gần hầm. Danh sách hiện ba lựa chọn: “Kiểm tra đường ruộng”, “Kiểm tra đường qua sông”, “Kiểm tra đường từ trong làng”.
3. Chọn bằng chuột hoặc phím 1/2/3; ghim N2_04 chuyển tới điểm của hướng đó. Có thể nhấn N để đổi hướng khi đang khảo sát.
4. Đường ruộng: đứng trong bụi cây ở ghim (bán kính 1,5 m), đứng yên giữ F và nhìn rõ một lính trong toán tuần tra liên tục 1 giây. Mất đường nhìn, hạ ống nhòm hoặc ra ngoài bụi cây thì tiến độ chưa hoàn tất về 0. Đã quan sát đủ thì E báo cáo khi ở trong bụi cây.
5. Đường qua sông: tới ghim trên bờ, E báo cáo; không buộc Nam phải lội mới biết tuyến cần qua nước.
6. Đường trong làng: tới ghim, E báo cáo. Nghe phản hồi chấp thuận xong là hoàn tất N2_04, mở ghim về điểm hẹn Hùng ở căn cứ. Không yêu cầu từng kiểm tra hai hướng còn lại.
7. Hai hướng bị từ chối được đánh dấu “Đã loại”, hiện lại danh sách để chọn hướng khác. Chỉ báo cáo, không tự động thất bại khi kiểm tra một hướng không phù hợp.

## Toán tuần tra đường ruộng

- Nhóm riêng `night2_evacuation_patrol`, không dùng lại ba lính tiếp cận kho.
- Năm lính chờ theo đội hình ở trung điểm 0 m. Chỉ khi Nam chọn đường ruộng và tới trong 3 m quanh ghim bụi cây, nhóm mới bắt đầu đi bộ 1,6 m/s. Tuyến qua lại dài 20 m: 0 → +10 → -10 → +10… Cả đội tới mỗi đầu thì dừng quét đúng 3 giây gameplay rồi đảo hướng; pause/menu/hội thoại không tính vào 3 giây. Các cặp theo sau được bố trí nằm trong hai đầu tuyến khi quay lại.
- Mỗi đèn là cone đủ 60°, tầm 10 m. Chỉ phát hiện khi có đường nhìn thật; tường và khói chặn tia. Nằm xuống (Z) tránh được phát hiện bằng đèn của toán đường ruộng; đứng/ngồi vẫn có thể bị soi. Nằm không dùng ống nhòm tích lũy quan sát; phải đứng hoặc ngồi, giữ yên F đủ 1 giây.
- Bị đèn soi trúng khi đứng/ngồi: vào cutscene bị bắt, không thất bại ngay. Camera đi từ góc hiện tại về phía địch trong 3 giây; hai lính gần nhất đi theo NavMesh tới Nam rồi khống chế 2 giây và trói 3 giây. Sau đó hiện thất bại và nút “Thử lại checkpoint”. Không dùng cảnh bắn chết của đoạn tiếp cận kho.
- Nhóm xuất hiện khi N2_04 mở, ẩn sau khi hoàn tất. Không trùng sinh khi refresh/load. Không thay đổi sát thương hoặc đèn của các nhóm địch khác.
- Trong danh sách chọn hướng, hội thoại báo cáo, pause hoặc menu, gameplay của nhóm tạm dừng cùng các thao tác bị khóa.

## Các điểm bố trí

Tọa độ X/Z trong `Map02ChapterLayout.asset`, có thể chỉnh bằng Inspector; cao độ lấy từ NavMesh thật:

- Bắt đầu gần hầm: (-4, -42).
- Bụi cây đường ruộng: (25, -56.5).
- Bờ của hướng qua sông: (-8, -57).
- Điểm kiểm tra đường làng: (-53, -39).
- Tuần tra ruộng: trung điểm (25, -66.5), cách ghim bụi cây 10 m. Hai đầu (-10 m / +10 m) tương ứng (15.861885, -70.561386) và (34.138115, -62.438614). Đã đối chiếu đoạn này với mask mặt đường Roadside_Rounded_Lanes, thay cho đường thẳng cũ chưa bám đường cong.

Bụi cây dùng prefab BushB đã có, tham chiếu từ Layout. Các điểm và đường tuần tra đã kiểm tra với NavMesh Map 2; đường đi sát mép đường, độ rõ hình ảnh và kích thước bụi cây vẫn phải duyệt trong scene đầy đủ. Điểm hẹn Hùng giữ nguyên căn cứ, không chuyển cảnh làm lành sang địa điểm mới.

## Thoại và lưu trạng thái

- Giữ nguyên tám lượt thoại người dùng cung cấp trong `Story/Map02Chapter.json`, các cảnh `m2v3_evac_field`, `m2v3_evac_river`, `m2v3_evac_village`.
- Hiển thị theo thứ tự Nam → Anh Thành → Nam (đường làng chỉ hai lượt). E tiếp tục hoặc tự chuyển khi đủ thời gian đọc. Chỉ phát audio nếu tìm được bản thu có caption/radio trùng; đã bổ sung đủ 8 bản thu ngày 27/10/2026 bằng bộ giọng hiện có (Nam: Adam; Anh Thành: Minh Đức), có xử lý bộ đàm. Từng câu vẫn chờ nghe duyệt phát âm/diễn xuất.
- Không hoàn tất nhiệm vụ khi vừa tới ghim hoặc vừa bắt đầu báo cáo; chỉ xác nhận sau phản hồi chấp thuận cuối cùng.
- Lưu lựa chọn, hai hướng đã loại, kết quả quan sát, cờ đã tới ghim đường ruộng, chiều di chuyển và thời gian quét còn lại của pha tuần tra. Tiến độ quan sát dở dưới 1 giây không lưu.
- Không lưu giữa danh sách/hội thoại. Save trước báo cáo có thể thực hiện lại báo cáo sau load; save sau báo cáo bị từ chối nhớ hướng đã loại.
- Save cũ đã hoàn tất N2_04 giữ kết quả; save cũ đang đi bốn điểm chuyển về ghim lựa chọn mới, không tự coi là đã tìm đúng đường.

## Kiểm tra đã thực hiện

- Biên dịch assembly gameplay Map01 và assembly kiểm thử bằng compiler Unity thành công; có các warning cũ không liên quan.
- Play Mode trong project biệt lập, dùng NavMesh Map 2 thật và NPC/collider thật: cả ba nhánh trạng thái, chọn đường làng đầu tiên, lưu/tải, reset tuyến, năm lính không sinh trùng, đường đi hợp lệ, kết thúc đội hình, năm đèn đúng thông số, quan sát đủ 1 giây trong bụi, chặn quan sát/phát hiện xuyên vật cản, thất bại ngay và ẩn nhóm khi xong.
- Mã kiểm tra: `SourceArt/Map02NightBankFlow/evacuation_choices_checks.cs` và `evacuation_patrol_walk_checks.cs`. Bài thứ hai cho NavMeshAgent đi thật qua ba lần tới đầu tuyến, không teleport để chứng minh chuyển động.
- Báo cáo: `Tools/EvacuationChoiceReports/checks.txt` và `patrol-walk-checks.txt`. Có bổ sung unit test và cập nhật kịch bản test scene cũ; chưa chạy lại toàn bộ test scene Map 2.
- `git diff --check` đạt. Chưa có lượt chơi trực quan toàn cảnh để nghiệm thu hình ảnh UI, grip đèn khi di chuyển, bụi cây và trải nghiệm cả đoạn kho → lựa chọn → báo cáo → về Hùng.

## Cách duyệt trong Unity

Đi tới hết N2_03, lên hầm và tới ghim gần kho. Lần thử đầu chọn 3, tới ghim làng rồi E: phải hoàn thành sau phản hồi, không cần hai đường kia. Retry/load trước N2_04 rồi thử 1: chưa F đủ 1 giây thì E chưa báo cáo, đủ thì nghe ba lượt và chọn lại. Thử 2: nghe ba lượt và chọn lại. Cuối cùng thử cố đi vào vùng đèn: phải thất bại và retry vẫn giữ kho đã kiểm tra, không làm lại N2_01–02. Kiểm tra thêm pause, lưu/tải và chuyển Ngày 3 sau cảnh Hùng.

## Sửa tuần tra theo đồ thị người dùng

Bản cũ đã cho toán lính đi sớm từ lúc N2_04 mở rồi dừng vĩnh viễn ở đích, nên khi Nam tới khảo sát có thể chỉ thấy lính đứng. Bản mới chờ tại 0 m cho tới khi Nam tới ghim; sau đó lặp đi–quét–quay lại. Save của bản một chiều được chuyển về đội hình giữa tuyến một lần, tránh giữ lính tại đích cũ ngoài tuyến 20 m.

## Quét bên hông và cảnh bị bắt — yêu cầu tiếp theo

Trong lúc đi, riêng lính thứ hai quét phía bên hông có bụi quan sát; bốn lính còn lại chỉ bật luồng quét khi dừng ở đầu tuyến. Hướng quét bên hông dựa vào phía bụi cây, có dao động, không khóa mục tiêu Nam qua vật cản. Đèn và tia phát hiện dùng cùng vị trí/hướng emitter sau khi hoạt ảnh tay cập nhật; luồng sáng trong sương hiển thị cùng pha. Nam nằm tránh đèn nhưng phải đứng/ngồi để quan sát.

Khi bị phát hiện, khóa điều khiển và lưu trong toàn bộ cảnh. Lính đi tới vị trí khống chế thật, không teleport. Nam được bảo vệ máu; dùng dữ liệu tư thế nằm hiện có và tiếp xúc tay để dàn cảnh ghì xuống/trói cổ tay. Clip thao tác nút dây hiện dùng lại chuyển động Untie cho bản dàn cảnh đầu. Cần duyệt hình ảnh/tiếp xúc trong scene đầy đủ trước khi coi hoạt ảnh trói là hoàn thiện. Camera, pose, dây và trạng thái bảo vệ được dọn khi retry/load/cancel.

## Kiểm tra bản bị bắt và độ rõ của đèn

- Đèn đường ruộng tăng intensity từ 4 lên 6; alpha của luồng sáng từ 0,035/0,003 lên 0,08/0,012 (gốc/mép). Giữ góc 60° và tầm phát hiện 10 m.
- Kiểm tra biệt lập đã đạt: đứng/ngồi quan sát; nằm không quan sát và tránh đèn; đúng một lính quét khi đi; dùng hướng emitter hiển thị; phát hiện mở cutscene thay vì thất bại ngay.
- Chạy tiếp cả cutscene bằng Nam/lính và NavMesh thật: camera bắt đầu đúng góc hiện tại, có vị trí trung gian, lính đi tới trước khi ghì/trói, hiện dây ở hai cổ tay rồi mới thất bại, không làm Nam chết, cancel trả pose và bảo vệ máu.
- Báo cáo `Tools/EvacuationChoiceReports/capture-checks.txt`; ảnh pose `capture-pose.png` là bản kiểm tra bằng vật liệu màu phẳng trong scene biệt lập, không phải hình hoàn thiện của Map 2. Mã `evacuation_capture_checks.cs` nằm trong `SourceArt/Map02NightBankFlow`.
- Khung hình, ánh sáng thật của scene và độ tự nhiên của tiếp xúc tay/trói vẫn cần duyệt trong lượt chơi toàn cảnh. Thay đổi độ đậm đèn cuối cùng đã biên dịch, chưa chạy lại kiểm tra hình ảnh toàn cảnh.

## Cây chặn tầm nhìn

Bổ sung Map02SightCover trên phiên Map 2. Tia đèn kiểm tra cả hình học mesh cây (LOD gần nhất), không chỉ collider; thân/tán thực sự nằm giữa đèn và Nam sẽ chặn phát hiện, kể cả collider bị thiếu hoặc nhỏ. Chỉ cây gỗ/dừa/đa/phượng/tre được xét, không biến bụi nhiệm vụ thành miễn phát hiện khi đang đứng. Không thêm collider vật lý, không đổi NavMesh. Mesh không đọc được dùng fallback thân cây hẹp; không dùng toàn bộ AABB tán làm tường vô hình.

Kiểm tra biệt lập đạt: cây không collider vẫn che Nam; ra khỏi cây thì có thể bị thấy; cây phía sau Nam không chặn tia trước Nam; khe hở thật trong bounds cây vẫn nhìn xuyên được. Đã biên dịch runtime và chạy hồi quy bằng Play Mode. Vẫn cần thử vị trí cây cụ thể trong scene đầy đủ.
