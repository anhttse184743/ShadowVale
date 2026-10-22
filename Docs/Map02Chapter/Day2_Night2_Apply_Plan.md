# Phân tích và kế hoạch áp dụng Ngày 2 và Đêm 2

Ngày đối chiếu: 21/10/2026. Nguồn: tài liệu `C:/Users/Tai Nguyen/Downloads/ShadowVale Map 2 – Kịch bản chi tiết 3 ngày đêm.docx` và source hiện tại trong project này. Phạm vi là phân tích và lên kế hoạch; chưa thay đổi gameplay và chưa chạy Unity trong lượt này. Những chỉ dẫn dàn cảnh trong tài liệu được xem là yêu cầu nội dung để đối chiếu, không phải lệnh thực thi công cụ.

## Kết luận

Đã có 7 bước Ngày 2 và 5 bước Đêm 2 trong Map02Chapter.json, được Map02ChapterDirector thực thi. Nên hoàn thiện trên tuyến này, giữ ID và các phụ thuộc hiện có thay vì xây lại một hệ nhiệm vụ. Phần còn thiếu tập trung ở hành động thực, dàn cảnh và trạng thái khi retry/load; không phải thiếu khung chương.

## Ý nghĩa của hai tuyến

Ngày 2 đưa xung đột Nam–Hùng ra ngoài hành động: Nam cho rằng tay bị thương khiến Hùng thành gánh nặng; việc Hùng sửa máy và hướng dẫn dựng ăng-ten chứng minh cậu vẫn có vai trò thiết yếu. Cứu Tâm giới thiệu mối đe dọa địch rà từng bến. Liên lạc pháo cài ba điều kiện cho đêm cuối: cần tọa độ, cần thời gian chờ, cần dân ra khỏi vùng bắn.

Đêm 2 trả lại nhận định buổi sáng: Nam đi ngoài đồng nhưng cần Hùng làm mắt qua bộ đàm. Lần đầu người chơi học bờ mương và nhịp đèn, cũng là con đường Hùng dùng ở cao trào. Tiếng xích xa phải xuất hiện trước khi xe tăng lộ diện ở Đêm 3. Cảnh làm lành, lời nhắc Map 1 và việc Nam đắp áo cho Hùng tạo một nhịp nghỉ có ý nghĩa trước Ngày 3.

Mục tiêu thời lượng trong tài liệu là Ngày 2 khoảng 20–25 phút, Đêm 2 khoảng 10–12 phút. Đây là mục tiêu thiết kế cần đo bằng chơi thử; không kéo dài bằng thời gian chờ hoặc suy ra từ số bước nhiệm vụ.

## Đối chiếu Ngày 2

### D2_01 Nhận lệnh

Đã có đủ các câu chính về Tâm quá hẹn, Hùng xin đi, Nam nói “Vướng”, anh Thành giao Hùng sửa máy. Cần dựng khoảng ngừng, hướng nhìn và phản ứng của Hùng/Bé Tí. Cảnh hiện dùng hệ camera và thoại chung; chưa chứng minh được diễn xuất riêng của đoạn này.

### D2_02 Đi tới cầu

Đã có điểm bridge và sinh nhóm tuần tra 3 lính khi bước này khả dụng. Địch có tuần tra và phát hiện Nam bằng đường ngắm. Chưa có cấu hình riêng cho hai tuyến ruộng nhanh nhưng trống và tre chậm nhưng kín. Cần khảo sát scene thật, đặt hai đường có vật che và lối về hợp lệ cho Tâm; không coi một đường NavMesh bất kỳ là đã đáp ứng lựa chọn chiến thuật.

### D2_03 Tìm Tâm

Hiện đến marker scout rồi mở thoại. Chưa thấy cơ chế dấu máu/dấu chân trong handler của tuyến này. Cần đặt chuỗi dấu vết đọc được, Tâm nấp sau trụ cầu, âm thanh gợi hướng và chỉ hiện tương tác khi thực sự tiếp cận. Không bắt buộc giết 3 lính; một lượt né hoàn toàn phải hoàn thành được.

### D2_04 Đưa Tâm về

Có actor đi theo NavMesh, Nam giảm tốc và không chạy nước rút; cả hai phải đến đích. Tuy nhiên đây là đi theo, chưa phải động tác dìu tiếp xúc. Hệ hands occupied đang khóa sử dụng vũ khí khi hộ tống; khác tài liệu yêu cầu súng ngắn một tay. Bộ vũ khí phù hợp vẫn là phụ thuộc cần xử lý.

Đích hiện là command, trong khi bảng nhiệm vụ nguồn ghi chốt đầu làng. Đề xuất đặt điểm tiếp nhận Tâm ở chốt và dàn anh Thành tới đó để nhận báo cáo; không buộc đi thêm một vòng chỉ để nghe thoại. Cần kiểm tra vị trí chốt trong scene trước khi chốt tọa độ. Giữ Tâm ở chỗ cứu chữa sau bàn giao và qua load; hiện ApplyState chuyển vị trí mặc định của Tâm sang bamboo khi qua pha, cần làm rõ bằng một chuyển động hoặc điểm nghỉ được dựng sẵn.

### D2_05 Lấy và mang dây

Handler wire hiện hoàn thành ngay khi tương tác ở kho. Cần có trạng thái lấy dây, mang tới trạm và bàn giao Hùng; chỉ bàn giao mới mở dựng ăng-ten. Giữ ID D2_05, thêm bước con trong trạng thái. Save/load không sinh lại cuộn dây hoặc làm mất dây đang cầm.

### D2_06 Dựng ăng-ten

Đã có A/D chỉnh tín hiệu, giảm âm rè theo độ lệch và giữ vùng đúng 2 giây; đứng chờ không tự hoàn thành. Còn thiếu chuỗi leo cây theo điểm bám, vị trí làm việc trên cao, buộc dây và xuống cây. Cần đặt cây/dây/điểm bám và kiểm tra camera, collider, input, chỗ đáp. Hùng hướng dẫn từ dưới bằng tay trái; Bé Tí quan sát cạnh máy.

### D2_07 Báo cáo pháo

Đã có thoại điều kiện gọi pháo và phụ thuộc D2_06; hoàn tất mở Đêm 2. Giữ nội dung nhân quả này. Bản game hiện dùng “trạm làng/đơn vị bạn”, khác mật danh trong DOCX; ghi nhận là khác biệt cần giữ theo quyết định nội dung hiện có, chưa tự đổi ngược. Chuyển ánh sáng sương sáng sang nắng chiều trong Ngày 2 cần bổ sung theo mốc truyện; hiện chiếu sáng chủ yếu phân theo ngày/đêm.

## Đối chiếu Đêm 2

### N2_01 Quan sát cầu

Có phím F, kiểm tra đứng yên, góc nhìn và vật che, tích lũy 2 giây quan sát. Tuy nhiên mục tiêu kiểm tra là điểm scout; chưa ràng buộc trực tiếp vào đèn hoặc actor tuần tra. Điều kiện cũng chưa yêu cầu đứng trong vùng quan sát bridge. Cần vùng quan sát hợp lý và mục tiêu đèn thực, tránh hoàn thành từ nơi không nhìn thấy đèn. Tài liệu nói hai hoặc ba đèn; hiện hệ quét có một Spot Light, cần dựng số nguồn sáng phù hợp hoặc điều chỉnh lời thoại sau duyệt.

### N2_02 Báo và né đèn

Có thoại qua đàm và vòng quét mặc định 15 giây, kiểm tra góc/đường ngắm và thời gian phơi sáng. Sau báo đàm mới bật tính phát hiện. Lần đầu hiện cảnh báo; lần hai gọi Retry. Ngồi thấp chỉ tăng ngưỡng phản ứng, chưa chứng minh tư thế nằm sát như kịch bản.

Hiện thoại “đang quay” và “qua rồi” chạy theo nhịp hội thoại chung; cần đồng bộ với hướng đèn thực và giữ quyền điều khiển Nam trong đoạn né. Lần phát hiện đầu nên có phản ứng đèn dừng/soi thêm; code hiện chỉ tăng cảnh báo. Tiếng xích xa cần cue riêng đúng sau đoạn né, trước câu “Tiếng gì thế?”, phát một lần cho mỗi lượt chạy tuyến.

### N2_03 Kiểm tra kho

Hiện kind reach, đến gần là hoàn thành. Đổi sang thao tác kiểm tra cửa hầm: tới đúng mặt cửa, E xác nhận chốt/cửa còn kín, có phản hồi hình/âm thanh. Không cần biến thành câu đố mới.

### N2_04 Kiểm tra đường bến

Hiện chỉ đến điểm dock. Cần kiểm tra các đoạn trên tuyến sơ tán bằng trigger theo thứ tự rồi xác nhận bậc bến; nếu người chơi đi tắt thẳng đến bến, chưa được tính đã kiểm tra cả đường. Đường này phải nối được với bờ mương đã học và tuyến sơ tán đêm cuối.

### N2_05 Về điểm hẹn

Đã có thoại làm lành và câu hỏi về Map 1, không có đồng hồ buộc trở về. Hiện các câu được gom vào một cinematic 48 giây với dàn cảnh chung. Cần tách thành các đoạn diễn xuất trong cùng ID: gặp lại ở chốt → làm lành → đổi ca tại chỗ nghỉ → Hùng hỏi → ngủ nghiêng ôm tay đau → Nam đắp áo → chuyển Ngày 3. Không dùng thời lượng 48 giây làm bằng chứng đã có đủ hành động.

## Rủi ro cần sửa trước khi gắn mỹ thuật

1. Checkpoint Đêm 2 chưa đúng hợp đồng tài liệu. Finish lưu checkpoint ở mọi bước; vì vậy sau kiểm tra kho/bến, phát hiện lần hai có thể quay về mốc mới nhất thay vì đầu cầu. Cần tách checkpoint retry tuần tra khỏi checkpoint lưu tiến trình thường.
2. patrolWarnings có trong snapshot nhưng scanClock, detectionClock và scannedAt là biến runtime. Cần quy định reset hoặc khôi phục nhất quán; retry phải cho một lượt né mới và không báo động lần hai ngay vì cảnh báo cũ. Kiểm tra cả load lúc đã bị phát hiện một lần.
3. Snapshot actor chỉ có role và position. Restore gọi StopAt cho actor; tuyến hộ tống đang dở cần khôi phục Follow cùng activeTask và trang bị, không chỉ đặt lại vị trí rồi để Tâm đứng yên. Đây là nguy cơ cần xác nhận bằng test, chưa kết luận từ một lượt chơi.
4. Marker wire, reach và cinematic có thể làm tiến độ đúng nhưng trải nghiệm thiếu hành động. Điều kiện xong phải bám sự kiện thực: giao dây, kiểm tra cửa, đi hết tuyến, kết cảnh ngủ.

## Kế hoạch triển khai

### Đợt 1 Khóa logic và dữ liệu tuyến

Giữ 12 ID D2/N2 và phụ thuộc; thêm trạng thái bước con cho giao dây, tuyến bến, hộ tống và cue đã phát. Tách checkpoint đầu cầu và sửa reset phát hiện. Bổ sung migration/default cho save version 3: bước đã hoàn tất giữ hoàn tất, bước đang dở có đường tiếp tục hợp lệ. Chưa tự nâng version nếu không cần phá schema.

File chính: Map02ChapterState.cs, Map02ChapterDirector.cs, Map02Mission.cs và Map02Chapter.json. Điều kiện xong: save/load/retry không kẹt, không nhân đôi dây/địch, không đổi máu/đạn ngoài hành động được thiết kế.

### Đợt 2 Làm một tuyến Ngày 2 chơi được

Khảo sát hai đường tới cầu và đường hộ tống; dựng dấu vết, vị trí Tâm, tuần tra hữu hạn và điểm nhận ở chốt. Hoàn thiện mang dây. Tiếp đó gắn tương tác leo/dựng ăng-ten theo điểm bám. Tách hành vi theo tuyến vào partial/controller phù hợp để tránh tiếp tục dồn mọi loại việc vào Director.

File/asset chính: Map02ChapterLayout.cs/.asset, Map02ChapterActor.cs, Map02ChapterEnemy.cs, Director và prop/dấu vết. Điều kiện xong: đi được cả hai đường; né hoặc xử lý địch đều cứu được Tâm; mang dây thực sự về trạm; chỉ tín hiệu ổn định mới nối máy.

### Đợt 3 Hoàn thiện Đêm 2

Đặt vùng quan sát và đèn/tuần tra cụ thể; đồng bộ thoại chỉ đường với vòng quét, phản ứng phát hiện lần một và reset lần hai. Thêm kiểm tra cửa kho, chuỗi điểm đường bến và cue xích sắt. Hùng tiếp tục ở trạm làm mắt qua đàm, Nam về điểm hẹn không tính giờ.

Điều kiện xong: vật che cản phát hiện; nhìn sai chỗ không xong quan sát; có thể qua đêm không giao tranh; lần hai luôn quay về đầu cầu với trạng thái đã định, không mất đồ hoặc giữ cảnh báo sai.

### Đợt 4 Gắn diễn xuất và âm thanh

Dựng phản ứng “Vướng”, dìu Tâm, leo/buộc dây, cử chỉ tay trái và các cảnh kết đêm. Chuẩn bị slot animation riêng cho từng cảnh, không sửa rig gốc dùng chung của Map 1. Đồng bộ radio cue, xích sắt và đoạn nhạc Hùng từ Đêm 1. Gắn bản thu sau khi duyệt, không tạo audio trả phí trong phạm vi kế hoạch này.

Phụ thuộc cần chốt khi triển khai: model/rig Tâm và Bé Tí; animation dìu, leo cây, nằm né, ngủ và đắp áo; súng ngắn một tay cùng luật chuyển vũ khí. Nếu chưa có súng ngắn, có thể dựng bản thử khóa súng để kiểm tra tuyến nhưng phải ghi rõ khác nguồn, chưa nghiệm thu cơ chế dìu cuối cùng.

### Đợt 5 Kiểm chứng và chơi thử

Chạy test logic có mục tiêu, sau đó kiểm tra scene thật và chơi tay liền từ cuối Đêm 1 tới đầu Ngày 3. Test dựng snapshot chỉ chứng minh một điều kiện, không chứng minh thời lượng và nhịp truyện.

Các ca bắt buộc:

- Hai đường tới cầu; né hết lính và xử lý lính; không bắt buộc clear địch.
- Tâm tụt lại, kẹt vật cản, tới đích sau Nam; save/load giữa hộ tống và sau bàn giao.
- Lấy dây rồi save/load; E lặp không lấy/giao hai lần; hủy chỉnh ăng-ten không mất dây.
- Chỉnh sai hoặc không nhập không xong; pause/cancel/skip trả lại camera và input.
- F nhìn sai hướng, có tường chắn hoặc ngoài vùng quan sát không hoàn tất.
- Đèn chiếu Nam sau vật che, ngồi/nằm ngoài trống, rời tia rồi quay lại; thử phát hiện lần hai trước và sau kho/bến.
- Load sau một cảnh báo không tự phát hiện ngay; retry reset đúng mốc và bộ đếm.
- Đi tắt ra bến chưa đủ điều kiện kiểm tra đường; đi đúng tuyến được xác nhận.
- Âm xích và thoại đi đúng thứ tự; Hùng ở đúng trạm; không hiện nội dung tiết lộ cái chết sớm.
- Kết cảnh ngủ tự nhiên và skip cùng mở Ngày 3 một lần, giữ đồ/máu/đạn, không nhân đôi actor/camera/menu.

## Tiêu chí bàn giao

Tuyến Ngày 2 → Đêm 2 → đầu Ngày 3 phải chơi liền bằng input thật. Người chơi hiểu vì sao cần cứu Tâm, vì sao Hùng hữu ích và vì sao pháo chưa thể bắn ngay. Họ nhận biết được bờ mương, đường bến và tiếng xích mà không cần đọc tài liệu thiết kế. Báo cáo cuối phân biệt rõ logic đã đạt, asset đã gắn và phần còn tạm; chỉ ghi thời lượng sau khi đo chơi thử.
