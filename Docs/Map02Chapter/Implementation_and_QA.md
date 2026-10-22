# Map 2 — Triển khai chương ba ngày đêm

Nguồn kịch bản: ShadowVale Map 2 – Kịch bản chi tiết 3 ngày đêm.docx, người dùng cung cấp ngày 20/10/2026. Project đang sửa: G:\game\Capstone\ShadowVale. Đây là bản triển khai logic với model nhân vật phụ và xe tăng để nhóm đồ họa bổ sung.

## Tiến trình đang được dùng

43 bước, 7 khoảng thời gian: Ngày 1 → Đêm 1 → Ngày 2 → Đêm 2 → Ngày 3 → Đêm 3 → Bình minh. Đêm chỉ mở sau khi xong công việc và báo cáo/đổi ca; người chơi không mất nhiệm vụ do một đồng hồ tự chạy hết ngày. Những việc Ngày 1 và phần chuẩn bị Ngày 3 có thể làm khác thứ tự.

Ngày 1: đối thoại với anh Lực, đưa Hùng đi thay băng, giao thuốc, 3 chuyến vật tư, 5 nhịp sửa bến, báo cáo anh Thành. Đêm 1: bữa cơm/lời hứa, cùng Hùng kiểm tra chốt bờ tre và chốt đầu làng, đổi ca.

Ngày 2: nhận lệnh tìm Tâm, đến khu cầu, tìm và đưa Tâm về, lấy dây, căn tín hiệu ăng-ten, báo cáo liên lạc pháo. Đêm 2: dùng F quan sát, báo đèn pin, kiểm tra kho và bến, về điểm hẹn; cảnh làm lành và nhớ lần cứu ở Map 1.

Ngày 3: 2 chuyến thuốc/đạn, cất bộc phá ở bờ đất, sửa 2 thuyền, phân công 3 nhà, bữa chiều và nhận phân công giữ ngõ đình.

Đêm 3: lấy lại trang bị, cứu ông Sáu/chị Lành và bé Tí, giữ tuyến, ba lần lùi ngõ theo 70–80–90%, Nam bị thương, bò vào chỗ nấp, dọn cây, chia tay Hùng, yểm trợ 75 giây, hy sinh/diệt xe tăng/phản công, bình minh khép chương. Cảnh cuối không đưa Nam đi Map 3.

## Các điểm đã chỉnh để nối truyện

- Giữ cập bến hiện có, theo lựa chọn trước của người dùng; dùng bến nam cho các chuyến sơ tán.
- Ba thùng Ngày 1 đóng góp hữu hạn 30 viên/thùng (90 viên, chỉnh được trong Inspector), không tịch thu đạn Nam đang mang. Khi địch đánh chỉ nhận thêm đúng 90 viên từ số thùng đã cất, không nạp đầy vô hạn. Đạn trong băng và máu không bị đổi qua hội thoại/save/load.
- Giao việc và chỉ thị quân sự qua anh Thành; Nam đáp “Rõ.”. Hùng chỉ hướng dẫn chuyên môn/liên lạc và kể chuyện.
- Sửa lỗi đánh máy “tui trẻ” thành “tụi trẻ”. Câu của Nam sau khi bị thương dùng “vết thương thôi”, tránh gọi một vết mới là “vết cũ”.
- Dùng “trạm làng” và “đơn vị bạn” trong thoại vô tuyến, không thêm tên chiến dịch hoặc biệt danh chiến dịch.
- Cài đơn vị pháo ở Ngày 2, nói rõ yêu cầu dân ra khỏi vùng bắn và việc pháo không bắn vào xe tăng đang sát dân. Lời hứa bữa cơm được cài ở Đêm 1, nhắc ở Đêm 2/Ngày 3 và trả ở đoạn kết.
- Đoạn yểm trợ bảo vệ Nam khỏi chết; không chấm việc Hùng sống/chết theo độ chính xác của người chơi. Skip và chạy hết cảnh đều ra cùng trạng thái.
- Lời nhờ sửa bến, hướng dẫn ăng-ten và phân công hộ dân phát trước thao tác; lời xác nhận kết quả phát sau.
- Giữ nguyên vị trí hai đồng đội sau cảnh cập bến, chỉ bố trí lại khi bắt đầu tương tác chương; tránh làm hỏng bàn giao cutscene thuyền.
- Sau hy sinh tự vào cảnh bình minh, không để một khoảng chiến đấu ngoài ý muốn; load tại mốc này tiếp tục cùng kết quả.
- Hoàn thành chương chỉ sau cảnh bình minh. Save sau hy sinh giữ cờ Hùng chết, xe tăng hỏng, chuyển máy đàm và pháo đã bắn; retry trước mốc đó khôi phục đúng trạng thái.

## File để chỉnh

- Assets/_Project/Map01/Resources/Story/Map02Chapter.json: 43 nhiệm vụ và 135 lượt thoại, ID riêng m2v3_.
- Assets/_Project/Map01/Resources/Story/Map02ChapterLayout.asset: các vị trí trên NavMesh, thông số đêm và chỗ gắn prefab.
- Map02ChapterDirector: tương tác, nhịp thoại, chuyển ngày, nhóm địch, checkpoint và cảnh truyện.
- Map02ChapterState: điều kiện nhiệm vụ, bộ đếm từng việc và snapshot.
- Map02ChapterActor: điểm gắn nhân vật và đường hộ tống.
- Map02ChapterEnemy: nhóm địch riêng Map 2, sát thương phải có đường ngắm.

Không chỉnh các scene địa hình hoặc cutscene thuyền cũ. Menu và HUD Map 2 tiếp tục dùng binding hiện có; ghim mục tiêu đọc trạng thái chương mới.

## Gắn đồ họa và âm thanh

Gắn prefab vào danh sách nhân vật của Map02ChapterLayout bằng đúng tên: Anh Thành, Anh Lực, Hùng, Chị Lành, Ông Sáu, Bé Tí, Chú Bảy, Tâm. Hai người đầu đoàn có thể dùng model đang có; model phụ được thêm sau. Gắn tankArt và enemyArt khi có. Giữ tỉ lệ mét và skeleton/Avatar của model, không thay root sở hữu logic.

Model/animation hoàn thiện vẫn cần: thay băng, dìu Tâm/người già, sinh hoạt bữa cơm, leo cây, bị thương vai, kéo Nam vào chỗ nấp, Hùng gài bộc phá bằng tay trái, gục và thân người phủ khăn ở bình minh. Các cảnh hiện có nhịp camera/thoại và các mốc trạng thái để nhóm đồ họa gắn vào. Khối xe tăng hiện là đạo cụ tạm cho logic, không phải model cuối.

135 lượt thoại mới hiện dùng chữ và thời lượng đọc dự phòng. Chỉ phát audio khi tìm được bản thu có ID, caption và cờ bộ đàm khớp. Chưa tạo audio trả phí hoặc tự tái sử dụng lời cũ sai nội dung. Giọng Hùng miền Bắc và các vai Nam Bộ cần duyệt mẫu riêng trước thu.

Project hiện chỉ có tay không, dao và súng trường. Chi tiết “chỉ dùng súng ngắn khi dìu Tâm” chưa có vũ khí/súng ngắn tương ứng; cần chốt bộ vũ khí hoặc bổ sung sau. Không coi phần này đã nghiệm thu.

## Phạm vi kiểm chứng

Kiểm thử tự động đồ thị nhiệm vụ, phụ thuộc, ngày đêm, save/load, skip và đường NavMesh; kiểm thử cảnh Map 2 thật, menu/inventory/checkpoint và các mốc kết thúc. Những test nạp snapshot để đi qua các khoảng thời gian không thay thế buổi chơi người thật toàn bộ chương.

Chưa xác nhận thời lượng 90–120 phút, chất lượng lồng tiếng, động tác của các model chưa có hoặc mức độ cảm xúc với người chơi mới. Chỉ nghiệm thu các phần này sau khi nhóm đồ họa/âm thanh gắn asset và chạy buổi chơi thử đủ chương.

## Kết quả kiểm thử ngày 20–21/10/2026

- Hồi quy Map 2: 33/33 đạt, 0 lỗi, 0 bỏ qua. Bao gồm chuyển Map 1 → Map 2 tự nhiên/skip, vị trí cập bến, khôi phục chiến đấu, save cũ và HUD/menu.
- Sau các chỉnh sửa cuối: 7/7 kiểm thử chương và thao tác đạt; kiểm tra phụ thuộc nhiệm vụ, thực thi trên scene thật, checkpoint, trạng thái kết thúc, timing sửa bến, căn ăng-ten, giữ điều khiển súng trong cảnh yểm trợ và tự mở bình minh. Hai bộ có phần trùng, không cộng thành 40 test độc lập.
- Lần đầu hồi quy có 5 lỗi do đồng đội bị bố trí lại quá sớm sau cập bến; đã sửa và toàn bộ 33 test chạy lại đạt.
- Shader bầu trời thêm Exposure mặc định 1, chỉ bản material runtime của chương đổi theo đêm; material gốc/Map 1 giữ mặc định. Kiểm tra đêm xác nhận exposure < 0,15.
- Ảnh kiểm tra đã phát hiện và sửa khói xe tăng màu hồng: dùng material khói URP có sẵn, giới hạn 80 hạt, lên cao và mờ dần; kiểm tra shader hạt trong scene thật.
- git diff --check chạy sạch. Không commit/push trong đợt này.

Đây là nghiệm thu logic và luồng tự động, chưa thay thế chơi thử toàn chương bằng người thật. Chưa đo được mục tiêu 90–120 phút. Cần chạy thêm sau khi nhóm model/animation gắn asset, đặc biệt các cảnh tương tác hai người và hy sinh.
