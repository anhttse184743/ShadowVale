# Triển khai nhiệm vụ Map 2 theo kịch bản đã cung cấp

Nguồn: F:/ShadowVale_Map2_KichBan_va_HeThongHoiThoai.docx. Phạm vi là Phần A và hệ thống hội thoại Map 2 trong Phần B; không dùng các ghi chú Map 1 cũ để thay những thay đổi Map 1 đã được chốt sau đó.

**Thay đổi mới được người dùng xác nhận:** Hùng hy sinh tại bến nam khi yểm trợ chuyến thuyền cuối, thay kết cục cùng đoàn thoát ở tài liệu gốc. Xem Map02_Ending_Revision.md; hàng “Cảnh cuối” trong bảng dưới là nội dung nguồn cũ, không được phát lại đoạn Hùng chèo/nói trên thuyền sau hi sinh.

Đã chốt trong chat: giữ nguyên cutscene cập bến hiện có; rút cuối chương qua bến nam. Kiểm tra code cho thấy cutscene hiện tại đã cập ở tọa độ z âm, bến nam; chế độ mở riêng Map 2 đặt nhân vật ở bến bắc. Cần đồng nhất cách vào map khi tích hợp, không đổi cảnh cập bến đã ổn.

## Tuyến nhiệm vụ và điều kiện chuyển

| Nhiệm vụ | Người chơi thực hiện | Xác nhận hoàn thành và nội dung thoại |
|---|---|---|
| 1 Bắt liên lạc | Tới căn nhà liên lạc, E trao đổi với nhóm chỉ huy và phụ trách địa phương | Xác nhận cuối hội thoại mới nhận việc; mở kho kín hữu hạn. m2_contact: 4 câu từ tài liệu |
| 2 Việc của làng | Giao thuốc cho gia đình cha già; chuyển một kiện vật tư tới chỗ cất mới; nghe người chèo giới thiệu tuyến bến | Cả thuốc và kiện hàng đã bàn giao. m2_family_medicine: 4 câu; m2_boat_route chưa có câu chữ trong tài liệu |
| 3 Người chưa trở về | Nhận lệnh tại nhà liên lạc; tìm trinh sát ở ruộng gần cầu; đưa về và báo cáo | Tìm thấy chưa đủ: actor phải tới điểm trú trước khi xác nhận báo cáo. Không bắt giết sạch tuần tra; không dùng luật xử bắn Hùng. m2_scout_order và m2_scout_found, mỗi cuộc 3 câu |
| 4 Chuẩn bị trong im lặng | Tập kết người yếu; chuyển thuốc/đạn xuống bến nam; kiểm tra thuyền, thứ tự tự chọn | Đủ ba việc mới mở cảnh địch khép vòng. m2_prepare: 3 câu |
| 5 Giữ ngõ đưa dân đi | Cùng lực lượng địa phương giữ ngõ trong hai đợt đầu, mỗi đợt 4 lính theo bản mẫu của tài liệu | Dân phải đi qua tuyến nguy hiểm và tới điểm an toàn; không chỉ đếm giờ hoặc chỉ diệt lính. Sau đó lùi vào chốt trong. m2_encirclement và m2_hold_lane, mỗi cuộc 3 câu |
| 6 Lời hẹn ở cuối xóm | Mở lối, sơ cứu, giúp đứng dậy; cứu hai nhóm, mỗi nhóm 2 người; được chọn thứ tự | Dân tới điểm tập kết phía nam; không tính ngay khi bấm E. Hùng tới đón nhóm cuối. Không thêm cơ chế cõng/cáng. m2_family_rescue: 3 câu |
| 7 Mở một cửa thoát | Vòng theo bờ ruộng xử lý tổ hỏa lực; phối hợp lực lượng giữ cầu; đưa các chuyến dân rời bến | Chỉ mở cảnh Nam lên chuyến cuối khi đoàn dân và lực lượng đã rút; không yêu cầu diệt hết địch. m2_breakout: 3 câu |
| Cảnh cuối | Hùng chèo, Nam và chỉ huy quan sát/yểm trợ loạt ngắn có mục tiêu an toàn | m2_departure: 4 câu; đoạn tiếp nhận xác nhận đoàn an toàn mới ghi hoàn thành. Skip cùng kết quả; không mở Map 3 |

Cảnh chuyển không đổi toàn bộ làng sang đổ nát ngay: áp sát và lục soát trước; hỏa lực tăng, nhà ven tuyến và chốt mới hư hại theo sự kiện. Không thêm nội gián hoặc lịch sử cụ thể. Riêng Hùng hy sinh là thay đổi mới người dùng đã chốt.

## Những phần đã chuẩn bị trong source

- Map02Progress.cs: bộ điều kiện chuyển bước độc lập với stage ID Map 1; cờ công việc, hai đợt giữ ngõ, hai nhóm cứu hộ, thuyền rút và tiếp nhận. Chặn hoàn thành lặp và tiến trình sau thất bại.
- Map02ProgressTests.cs: 7/7 đạt trong Logs/Map02-progress-tests.xml. Đây là kiểm tra logic/JSON; chưa phải một map có NPC và gameplay hoàn chỉnh.
- SourceArt/Dialogue/Map02Story/dialogue-plan.json: 33 câu lấy nguyên văn từ tài liệu, 10 cuộc có câu chữ. Ghi rõ kênh, người nói, nguồn và trạng thái chưa thu âm; không có lệnh tạo audio trả phí.
- Map02DialogueCatalog và Resources/Dialogue/Map02Story/script.json: bản thoại dùng được từ runtime, ID riêng Map 2. Chỉ chấp nhận bản thu khớp nguyên văn caption và loại trực tiếp/bộ đàm, không lấy audio Map 1 thay câu mới.
- Map02ConversationSession: phiên nhận chỉ thị, E riêng ở cuối mới xác nhận; hủy hoặc thất bại không bàn giao nhầm. Nói chuyện không thay cho sự kiện giao thuốc, dân tới vùng an toàn hoặc thuyền thực sự cập nơi tiếp nhận. Map02Mission đã nối input/caption và khóa input camera trong hội thoại; diễn xuất NPC và audio Map 2 chưa tích hợp.
- Kiểm thử mới: 13/13 đạt, Logs/Map02-rules-tests.xml (7 tiến trình + 6 hội thoại), Unity thoát bình thường. Lần chọn bộ test theo tiền tố rộng trước đó đã kéo cả bài render vào chế độ không đồ họa và làm Unity crash; không tính lượt đó là đạt. Bộ chạy lại chỉ gồm hai lớp kiểm tra logic, không chứng minh animation, HUD hoặc gameplay thực tế.

Các API chuyển bước phải do sự kiện thực từ AI, kho, hội thoại, vùng an toàn hoặc chuyến thuyền gọi; không tự đánh dấu vì thời gian đã trôi qua.

## Bản object tương tác hiện tại

Theo chỉ thị mới nhất, tạm bỏ model dân làng và tập trung logic. Map02Mission được tạo khi vào scene Map 2, chờ cảnh cập bến kết thúc rồi nhận Nam đang có; không tạo Map01Mission giả. Nhân vật khám phá có sẵn trong scene bị tắt khi đoàn từ Map 1 tới để tránh hai Nam nhận input.

Map02WorldState tách nhặt và giao thuốc/kiện/vật tư xuống bến. Map02ObjectiveObject có SaveId riêng, bán kính thao tác và điểm đứng chỉnh được. Map02ObjectLayout tạo prefab Resources/Story/Map02QuestObjects với 16 tương tác tại các nhà và bến thực; tất cả có đường NavMesh hoàn chỉnh, kiểm tra thêm khoảng trống capsule. Không lưu lại hoặc sửa terrain của scene. Object hiện là hộp đại diện để thử luật, chưa phải prop mỹ thuật hoàn chỉnh.

Đã nối runtime: nhận chỉ thị liên lạc, lấy/giao thuốc, lấy/giao kiện vật tư, nhận lệnh tìm trinh sát; khóa điều khiển khi nói chuyện, E riêng cuối cuộc mới nhận nhiệm vụ. Cờ cargo lưu riêng, không cộng đạn hoặc vật tư thường, không nhặt/giao lặp. Trinh sát, nhóm dân, các đợt địch và thuyền cần controller sự kiện vật lý; nhấn E trên marker không được tự xác nhận dân đã được cứu. Các đoạn sau chưa chơi trọn tuyến và chưa nghiệm thu.

Logs/Map02-object-final-tests.xml: **23/23 đạt** trên bản hiện tại, gồm logic, cargo, hội thoại, kết cục, luồng gameplay object Map 2 và tải scene thật qua menu chung; hồi quy menu lưu/tải Map 1 và skip cảnh cập bến Map 2. Bản lưu giữ thuốc đang cầm, kiện đã giao, máu 61, băng 9 và dự trữ 13; không nhân đôi menu hoặc 16 điểm tương tác.

Ảnh đầu phát hiện camera nằm trong tán cây LOD không có collider. Đã đổi thuật toán chọn vị trí object: ngoài NavMesh và capsule, chấm điểm góc camera tránh bounds cây/nhà, giữ nguyên môi trường. Đã xem trực tiếp Logs/Map02ObjectFlow/gameplay.png ở 1920×1080: Nam và sân/kiện hiện rõ ở điểm giao vừa sửa; camera.txt không còn nằm trong bounds LOD cây đó. Đây là kiểm tra một điểm và luồng object, không thay cho kiểm thử toàn map hoặc animation cảnh cuối.

## Menu và HUD cần đưa sang nguyên chức năng

ForestMenu đã nhận phiên Map02Mission để dùng cùng menu, thao tác pause và lưu/tải. Pause UI cũ của Map02Arrival nhường cho menu này sau cập bến. Khi gọi Chơi mới hoặc tải Map 1 vẫn dùng Map01SaveSystem; slot có sceneName Map 2 đi tới Map02Mission.BeginGame. Không thêm Map01Mission giả vào Map 2.

Giữ hình ảnh, font và tài nguyên Menu/Background, Menu/ButtonPlate, Menu/Wordmark, Hud/Panel, Hud/Items và Hud/StartingWeapons. Dùng đúng quy mô UI 1600×900, Game View Full HD.

Phải có đủ Esc tiếp tục/lưu/tải/về menu/thoát; Tab túi đồ và kỹ năng; M bản đồ; J mục tiêu/nhật ký; máu, đạn, stamina, vật phẩm, marker và tình trạng nhóm dân. Minimap phải dựng từ Map 2 thật, không dùng ảnh survey Map 1.

Lưu/tải dùng chung vỏ slot ForestSaveSlots nhưng payload Map 2 riêng. Hiện lưu máu, băng đạn, dự trữ, trang bị, kỹ năng/career ID, vật tư nhiệm vụ, cờ tiến trình và checkpoint. Không lưu giữa giao tiếp chưa xác nhận hoặc cinematic. Snapshot dân/địch, chuyến thuyền và nhà hư hại phải được bổ sung cùng controller tương ứng; chưa gọi bản hiện tại là save toàn chương. HUD Map 2 hiện có máu/đạn, túi đồ, mục tiêu và sơ đồ vị trí nhà/object trên footprint thực; toàn bộ presentation Map 1, tab kỹ năng và minimap địa hình chưa chuyển xong.

## Asset và lời thoại còn phải chốt

Hiện tìm được model Nam, Hùng, lính và sĩ quan; chưa tìm thấy bộ model riêng của phụ trách địa phương, người chăm cha, cha già, trinh sát địa phương và người chèo thuyền. Không tự lấy lính địch đóng vai dân làng.

Người dùng đã từ chối bộ mẫu 12 NPC Blender vì trông như robot. SourceArt/Map02_Civilians/DO_NOT_INTEGRATE.md đánh dấu không tích hợp. Đã có một concept ảnh người liên lạc mặc đồ dân sự tại SourceArt/Map02_CivilianReference, nhưng theo chỉ thị mới đã dừng model, chưa chạy Meshy/Hunyuan và chưa phát sinh credit tạo 3D.

Cần xác nhận team cung cấp asset hay cho phép làm mới. Các animation tương tác cần đối chiếu với rig tương ứng: giao thuốc/kiện, đỡ đứng, dỡ gỗ, dẫn người chậm, lên xuống thuyền, ngồi và chèo. Giữ các asset nền, đường và nhà hiện có; mọi marker phải qua kiểm tra NavMesh và vật cản thực tế.

Giọng Nam/Hùng/chỉ huy phải thống nhất với Map 1. Chờ lựa chọn giữ giọng hiện có hay nghe lại mẫu; các vai mới phải duyệt mẫu. Ngân sách trước không được dùng lại cho Map 2. Chưa thu audio trả phí trước khi duyệt câu chữ, giọng và trần chi phí mới. Người tại chỗ dùng bản sạch; đầu đàm bên kia lọc nhẹ, không phát chồng bản sạch.

Câu nói chuyện với người chèo thuyền và các lời nối chưa có trong tài liệu phải soạn thành bản xem trước để người dùng duyệt; không tự ghi thành audio chính thức. Tên làng, tên riêng vai địa phương, thời lượng, tổ hỏa lực và số vật tư vẫn chưa chốt; không thêm tên hoặc thông số thành lore chính thức.

## Trình tự tích hợp và nghiệm thu

1. Chốt asset/giọng và lời còn thiếu; khảo sát scene thật, chọn nhà và tuyến đường hợp lệ.
2. Dựng vòng nhiệm vụ 1–2–6: gặp gia đình, giao thuốc rồi cứu lại đúng gia đình; xác nhận actor và cảm xúc trước khi nhân rộng.
3. Tích hợp menu/HUD, save/router và bàn giao Map 1 → Map 2, không tự refill.
4. Hoàn thiện trinh sát, chuẩn bị ba việc tự do, hai đợt giữ ngõ, cứu hai nhóm và mở bến; địch hữu hạn, bắn theo đường ngắm.
5. Dựng chuyển làng hoang tàn theo nhịp, thuyền theo sức chứa và đoạn tiếp nhận; natural/skip cùng trạng thái cuối.
6. Duyệt animation và audio, nhập bản cuối, chạy kiểm tra tích hợp thực sự.

Kiểm tra bắt buộc: tới map với ít/hết đạn; nhiều hướng tiếp cận và thứ tự cứu nhóm; vật che/đường tìm kiếm; dân bị thương/chết và retry; E liên tục, pause, skip, save/load giữa các mốc; không nhân đôi hàng, lính, thuyền, thưởng hoặc kỹ năng; giữ aim qua bàn giao; một camera/listener và một menu; mở save từ menu chính đúng map. Xem trực tiếp tay/chân, contact, đi bậc, ngồi và chèo trên scene thật; quay nhánh đầy đủ và skip. Chỉ kiểm thử trạng thái chưa đủ để xác nhận toàn tuyến hoàn tất.
