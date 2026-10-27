# Kế hoạch cập nhật cốt truyện và trải nghiệm Ngày 2 – Đêm 2

Ngày lập: 27/10/2026. Trạng thái: đề xuất triển khai, chưa thay đổi gameplay hoặc thoại trong game.

Cập nhật sau yêu cầu người dùng: riêng N2_04 đã được triển khai theo thiết kế ba lựa chọn trong `Night2_Evacuation_Choices.md`, thay thế đề xuất kiểm tra tuyến tuần tự ở mục F. Các đề xuất thay đổi truyện/thoại Ngày 2 và tiếp cận kho trong kế hoạch này vẫn chưa triển khai.

## 1. Kết quả cần đạt

Người chơi lần đầu hiểu mục đích của mỗi việc ngay khi nhận việc; nhận ra kết quả sau khi hoàn thành; biết ai đang hỗ trợ mình và người đó biết thông tin từ đâu.

Mạch nhân vật: Nam lo cho Hùng nhưng nói lời làm tổn thương → Hùng tiếp tục đảm nhiệm liên lạc và quan sát → Nam trực tiếp được Hùng hỗ trợ → Nam nhận lỗi, hai người nối lại sự tin tưởng.

Mạch tình thế: Tâm mất liên lạc → cứu Tâm và nhận tin địch rà các bến → khôi phục liên lạc chi viện → kiểm tra vật tư và đường sơ tán → xác nhận khả năng chuẩn bị cho đêm cuối.

Không biến toàn bộ thông tin thành hội thoại giải thích. Nguyên nhân và mục tiêu phải rõ; cảm xúc có thể thể hiện bằng hành động, khoảng lặng và cách đáp lời.

## 2. Phạm vi và nguyên tắc

- Giữ cấu trúc cứu Tâm, sửa liên lạc, quan sát, kiểm tra hầm, kiểm tra tuyến bến và gặp Hùng. Ưu tiên giữ ID nhiệm vụ hiện có để giảm rủi ro save.
- Giữ bản đồ và địa hình chính. Chỉ bổ sung vị trí đứng, đạo cụ và tương tác nhỏ sau khi kiểm tra NavMesh, tầm nhìn và collider thật.
- Anh Thành giao lệnh; Hùng phụ trách chuyên môn liên lạc và quan sát, không thay vai chỉ huy.
- Không thay kết quả Đêm 3, không tiết lộ trước số phận Hùng trên HUD.
- Các câu thoại bên dưới là nội dung đề xuất. Chốt bản chữ trước khi cập nhật phụ đề, danh mục giọng và thu âm.
- Quy tắc mới về lính rời vị trí là thay đổi thiết kế so với yêu cầu 27/10 hiện tại (lính đứng nguyên và ngừng quét tại cửa kho). Triển khai thành một hạng mục riêng; không âm thầm thay toàn bộ luật tuần tra.

## 3. Luồng trải nghiệm đích

Nhận lệnh tìm Tâm → thấy Hùng đau tay, xảy ra bất đồng → tìm thấy Tâm cùng dấu vết rút lui → dìu Tâm về báo cáo → Nam và Hùng khôi phục máy → nghe đơn vị bạn xác nhận liên lạc → nhận nhiệm vụ kiểm tra dự phòng sơ tán → thấy Hùng vào chốt quan sát → Nam quan sát và báo cáo → Hùng hướng dẫn Nam qua vùng quét → tới vị trí khuất ở cửa hầm, xác nhận tuần tra đi tiếp → xuống kiểm tra vật tư → kiểm tra bờ mương, lối xóm, bờ nam, bến → về báo kết quả với Hùng → làm lành.

## 4. Các cảnh cần cập nhật

### A. Giao lệnh đầu Ngày 2

**Vấn đề:** chưa rõ Tâm đi đâu; lời Nam quá nặng khi thiếu tình huống dẫn tới.

**Thay đổi:**
- Anh Thành nói Tâm đi trinh sát các bến phía ngoài, đã quá giờ trở về; vị trí liên lạc cuối ở khu cầu.
- Hùng xin đi cùng, thử nâng trang bị rồi khựng vì đau tay. Dùng tư thế/clip phù hợp, không dịch chuyển tay hoặc đạo cụ xuyên người.
- Nam đang sốt ruột, nói rõ nỗi lo trước khi lỡ lời. Ví dụ: “Tay còn vậy, ra đó có chuyện tôi lo cho ai trước?” rồi “Ở lại đi. Đi theo chỉ vướng thêm.”
- Hùng đáp ngắn, không biến thành tranh cãi dài. Anh Thành giao Hùng sửa máy và yêu cầu Nam đưa Tâm về nếu tìm thấy.

**Người chơi cần hiểu:** Tâm đang gặp nguy hiểm; Hùng muốn giúp; Nam lo nhưng cư xử sai; Hùng có công việc hữu ích ở lại.

### B. Tìm và cứu Tâm

**Thay đổi:**
- Dẫn người chơi tới chỗ nấp bằng dấu vết sẵn có; bảo đảm dấu vết phù hợp đường di chuyển và vị trí Tâm.
- Tâm giải thích ngắn: thấy địch dò các bến, bị phát hiện lúc rút, chân bị thương nên mắc lại tại khu cầu.
- Hướng dẫn hộ tống nêu rõ điểm đến và tình trạng Tâm. Tâm chỉ kể thông tin ngắn khi an toàn, không phát lời dài trong lúc giao tranh.
- Khi về, Tâm báo trực tiếp cho anh Thành. Đánh dấu hướng địch tiến và bến nam trên bản đồ/đạo cụ giao việc; không khẳng định thời điểm tấn công khi chưa có bằng chứng.

**Người chơi cần hiểu:** vì sao Tâm mất liên lạc, vì sao cần cứu, tin anh mang về làm thay đổi chuẩn bị của làng như thế nào.

### C. Sửa máy và liên lạc chi viện

**Thay đổi:**
- Hùng chỉ ra máy thiếu dây ăng-ten; ghim kho và cuộn dây tương ứng với lời giao việc.
- Tín hiệu nghe được thay đổi theo thao tác căn ăng-ten; thành công có tiếng trả lời và xác nhận rõ.
- Xác lập ai đang nói ở đầu máy bên kia. Rà thoại trả lời hiện gán cùng vai Anh Thành để tránh cảm giác một người vừa đứng cạnh vừa ở đầu dây xa. Có thể dùng vai “Trực đài đơn vị bạn” với tài nguyên giọng riêng phù hợp.
- Thống nhất cách xưng hô và định danh vô tuyến giữa nội dung hiện tại, phụ đề và audio; không tự thêm tên chiến dịch.
- Chốt ba điều kiện chi viện: có tọa độ, có thời gian triển khai, dân rời vùng bắn. Không giảng giải lại nhiều lần.

**Người chơi cần hiểu:** máy đã hoạt động nhờ cả hai người; có khả năng chi viện nhưng chưa thể giải quyết nguy cơ ngay.

### D. Giao việc và bố trí đầu Đêm 2

**Thay đổi:**
- Trước khi xuất phát, Anh Thành chỉ kho dự phòng và bến nam: cần xác nhận vật tư cùng đường sơ tán trước khi tình hình xấu đi.
- Nói rõ hầm kho bên ruộng khác kho ban ngày; HUD, bản đồ và thoại dùng cùng tên “Hầm kho dự phòng”.
- Hùng nhận nhiệm vụ giữ máy tại chốt quan sát đầu cầu; người chơi thấy anh đi tới vị trí bằng đường hợp lệ.
- Khảo sát chốt thật trước khi đặt tọa độ: nhìn được đoạn tiếp cận và tổ tuần tra; có vật che; không nhìn xuyên nhà/cây.
- Hùng nói giới hạn tầm quan sát. Nếu chốt không nhìn toàn tuyến, anh chỉ hướng dẫn đoạn thấy được; phần còn lại dựa vào Nam báo về.
- Cập nhật bản đồ hướng dẫn theo bố trí đã xác minh. Ảnh SVG phác thảo trước chỉ là tham khảo, không dùng làm tọa độ triển khai.

**Người chơi cần hiểu:** vì sao phải ra ngoài, hai mục tiêu cần xác nhận, Hùng ở đâu và hỗ trợ bằng cách nào.

### E. Quan sát và tiếp cận hầm — N2_01 đến N2_03

**Thay đổi:**
- Giữ quan sát qua F, điều kiện khoảng cách và vật che hiện có; lời nhắc nêu rõ đối tượng cần nhìn. Ghim checkpoint phải có tên dễ hiểu.
- Sau báo cáo, Hùng hướng dẫn một lượt tránh đèn thực tế: báo hướng quét → Nam nằm xuống → đèn đi qua → báo có thể đi tiếp.
- Câu hướng dẫn căn theo trạng thái đèn và đường nhìn thật, không chạy theo đồng hồ thoại độc lập. Không bắt người chơi bị soi để kích hoạt đoạn chứng minh Hùng hữu ích.
- Giữ luật phản ứng 1,5 giây/lần phát hiện và retry hiện có trong đợt sửa nội dung; tránh chồng thoại Hùng với thoại nghi ngờ của lính.
- Tại cửa hầm, điểm khuất phải được kiểm tra với các hướng quét. Không coi việc đứng trong bán kính là bằng chứng đã được che chắn.

**Hai mức hoàn thiện đoạn kết tiếp cận:**
1. Bản tối thiểu: Hùng xác nhận Nam đã khuất sau bờ đất; luồng quét chuyển ra ngoài đoạn tiếp cận và không gây thất bại trong vùng an toàn đã kiểm tra. Không để ba đèn đồng loạt biến mất vô cớ.
2. Bản đầy đủ: tổ tuần tra đổi hướng, đi tiếp theo NavMesh ra khỏi khu vực; Hùng xác nhận khi sự kiện thực sự xảy ra. Chỉ dùng nếu đủ đường đi, hoạt ảnh và xử lý save. Đây là phương án ưu tiên về kể chuyện, có chi phí cao hơn.

**Trong hầm:** E kiểm tra kệ trả kết quả cụ thể: vật tư còn đủ, chưa có dấu bị lục. Lấy loot không thay thế kiểm tra. Nam báo ngắn qua máy; mục tiêu chuyển sang đường sơ tán.

**Người chơi cần hiểu:** thông tin của Hùng giúp mình vượt nguy hiểm; đã thoát vùng quét vì điều gì; kiểm tra kho mang lại kết luận gì.

### F. Qua sông và kiểm tra tuyến bến — N2_04

**Thay đổi:**
- Chỉ đường về bờ làng bằng tuyến thực sự đi được trên đáy sông. Xác minh cao độ, độ sâu và hình ảnh trước khi viết câu “nước cạn”; không viết thoại trái địa hình.
- Giữ quy tắc hiện tại chỉ mở lội sau kiểm tra kho trong bản tối thiểu. Khi chặn, dùng lời nhắc theo mục tiêu còn dang dở; không giả vờ sông vừa thay đổi vật lý. Bản đầy đủ cần rà riêng trải nghiệm chặn sông, vì bổ sung lời thoại không xóa được cảm giác biên vô hình.
- Bờ mương: kiểm tra độ thông của đoạn dẫn; kết quả là xác nhận người đi bộ/người được dìu có thể qua.
- Lối xóm: chọn một hành động nhỏ có kết quả nhìn thấy, như mở cổng hoặc dọn vật nhẹ. Chọn theo đạo cụ thật, không đặt vật cản vào luồng AI mà chưa kiểm tra.
- Bờ nam: xác nhận lối xuống không bị chặn, hướng dẫn tiếp tới bến.
- Bậc bến: E kiểm tra bậc/dây neo, xác nhận tuyến sẵn sàng. Phân biệt với nhiệm vụ sửa thuyền Ngày 3.
- Giữ bốn đoạn trong tiến độ hiện có. Cân nhắc E xác nhận tại điểm cần thao tác; tránh thêm bốn lần giữ nút giống nhau.
- Những thay đổi vật lý nhỏ phải lưu và còn hiện ở Đêm 3, phù hợp đường hộ tống dân.

**Người chơi cần hiểu:** mình đang chuẩn bị đường cho dân đi; mỗi điểm kiểm tra giải quyết một vấn đề cụ thể.

### G. Báo kết quả và làm lành — N2_05

**Thay đổi:**
- Nam về vị trí Hùng đang đứng; nếu cảnh cần đổi vị trí, cho hai người di chuyển có nối cảnh thay vì teleport lộ thiên.
- Nam báo “Kho còn nguyên. Đường xuống bến đi được.” trước khi bắt đầu chuyện riêng.
- Lời xin lỗi nhận đúng lỗi: Nam lo cho Hùng nhưng đã nói như thể Hùng không còn làm được gì. Giữ cách nói ngắn và phù hợp nhân vật.
- Giữ câu đùa nhắc “vướng”, lời hứa bữa canh và nhắc lại lần cứu Map 1 nếu phù hợp bản thoại đã duyệt.
- Tiếng kim loại chỉ giữ khi nối được với diễn biến Đêm 3. Hai người nghe và báo là âm thanh chưa xác định; không tự khẳng định có xe tăng khi chưa nhận diện. Nếu không có nhịp trả lại sau đó, bỏ cue cùng mọi điều kiện chờ liên quan để tránh kẹt N2_05.

**Người chơi cần hiểu:** nhiệm vụ đã có kết quả; Nam nhận ra giá trị của Hùng; hai người đã làm lành, nguy cơ bên ngoài vẫn còn.

## 5. Thứ tự triển khai và đầu ra

### Đợt 1 — Chốt nội dung và khảo sát scene (ưu tiên cao nhất)
- Viết bản thoại hoàn chỉnh theo các cảnh A–G; phân biệt thoại trực tiếp, bộ đàm, lời nhắc và phụ đề.
- Kiểm tra thực địa chốt Hùng, cửa hầm, điểm lội, bốn đoạn đường bến và đường rời của tuần tra.
- Chọn mức tối thiểu/đầy đủ cho tuần tra dựa trên khảo sát; ghi rõ thay đổi so với luật 27/10.
- Đầu ra: kịch bản đã chốt, sơ đồ vị trí theo scene thật, danh sách đạo cụ/clip/audio cần bổ sung.
- Cửa nghiệm thu: mọi lời khẳng định về vị trí, tầm nhìn và địa hình đều có căn cứ trong scene.

### Đợt 2 — Bản chơi được với thoại chữ
- Sửa mục đích nhiệm vụ, HUD, lời giao việc, đúng người nói ở đầu vô tuyến.
- Bố trí Hùng và ghim; đồng bộ hướng dẫn với đèn thật; bổ sung báo kết quả kho và đường bến.
- Thực hiện mức tối thiểu của kết thúc tiếp cận, giữ checkpoint hoạt động.
- Cho phép đọc thoại chữ nếu audio mới chưa có; không phát câu audio cũ dưới phụ đề mới.
- Đầu ra: đi hết Ngày 2–Đêm 2 và hiểu được chuỗi nguyên nhân/kết quả mà không cần tài liệu ngoài.

### Đợt 3 — Tương tác và dàn cảnh hoàn chỉnh
- Làm phản ứng đau tay, dấu vết Tâm, thao tác kiểm tra, hành động nhỏ ở lối xóm.
- Triển khai tuần tra rời đi nếu chọn mức đầy đủ; bổ sung trạng thái lưu phù hợp.
- Hoàn thiện cảnh báo cáo/làm lành, cue kim loại nếu giữ và cách nối sang phần sau.
- Đầu ra: hành động, hình ảnh và thoại cùng mô tả một sự kiện; không còn điểm hoàn thành chỉ đổi cờ mà thiếu phản hồi.

### Đợt 4 — Âm thanh, hồi quy và chơi thử người mới
- Thu/cập nhật giọng sau khi thoại chữ ổn định; đồng bộ phụ đề và trạng thái bộ đàm.
- Kiểm thử các điểm chuyển nhiệm vụ, load/retry, skip và chuyển Ngày 3.
- Chơi thử với ít nhất ba người chưa đọc kịch bản; chỉnh cảnh gây hiểu nhầm rồi thử lại chỗ vừa sửa.
- Đầu ra: bản nội dung được kiểm chứng trong scene đầy đủ, kèm vấn đề còn lại và bằng chứng hình ảnh.

Không ước lượng số ngày công trước khảo sát clip, NavMesh và audio. Có thể bàn giao Đợt 2 để duyệt mạch truyện trước khi đầu tư Đợt 3–4.

## 6. Các khu vực mã và dữ liệu cần rà khi triển khai

- `Assets/_Project/Map01/Resources/Story/Map02Chapter.json`: nhiệm vụ, mục đích và thoại nguồn; giữ ID khi ý nghĩa vẫn tương thích.
- `Assets/_Project/Map01/Resources/Dialogue/Map02Story/script.json`, `Map02DialogueCatalog.cs`, danh mục giọng/CSV hiện có: xác định luồng nạp thực tế trước khi đồng bộ; tránh sửa một bản sao không được runtime sử dụng.
- `Map02ChapterDayTwo.cs`, `Map02ChapterScoutEscort.cs`: dấu vết, hộ tống và ăng-ten.
- `Map02ChapterNightRoute.cs`, `Map02ChapterNightPatrol.cs`, `Map02PatrolFlashlight.cs`: hướng dẫn bộ đàm, tuần tra, cửa hầm và tiến độ bến.
- `Map02ChapterLayout.asset`, `Map02ChapterLayout.cs`, `Map02ChapterCinematics.cs`, `Map02ChapterState.cs`, `Map02ChapterGuidance.cs`: vị trí, diễn cảnh, lưu trạng thái và lời hướng dẫn.
- Bộ kiểm thử sẵn có: `Map02DayTwoStateTests`, `Map02DayTwoRouteTests`, `Map02NightTwoRouteTests`, `Map02ReportAndNightPatrolTests`, cùng các kiểm tra biệt lập NightBankFlow. Chọn ca hồi quy liên quan, không dùng kết quả biệt lập thay cho chơi toàn cảnh.

## 7. Save, retry và các nhánh phải xử lý

- Load sau cứu Tâm: Tâm vẫn ở điểm trở về đúng; không kể lại cảnh đã xong.
- Load giữa sửa máy: trạng thái dây, ăng-ten và xác nhận liên lạc thống nhất; không cấp thành công hai lần.
- Retry tiếp cận hầm: giữ quan sát/báo cáo đã xong theo hành vi hiện tại; reset phát hiện và hướng dẫn thích hợp.
- Load ở cửa hầm/sau kho: không bật lại đoạn đe dọa đã kết thúc. Nếu tuần tra đang rời đi, khôi phục đúng pha và đích tới, không sinh trùng lính.
- Load trong đường sơ tán: nhớ điểm đã kiểm tra cùng trạng thái cổng/vật cản; không để HUD báo xong nhưng đường lại đóng.
- Loot giữ tính nhận một lần; mở nắp/đi thang/lấy loot không tự hoàn tất kiểm tra kệ.
- Bỏ/skip thoại kim loại hoặc làm lành: không mắc điều kiện chờ cũ; chuyển pha đúng một lần.
- Save cũ chưa có trạng thái mới: suy ra từ nhiệm vụ đã hoàn thành; không ép chơi lại hoặc ghi đè kết quả Đêm 3.
- Pause/menu dừng các bộ đếm gameplay; các kênh thoại không chồng câu và không bỏ lỡ chỉ dẫn cần thiết.

## 8. Tiêu chí nghiệm thu

### Hiểu cốt truyện
Ở lần chơi đầu, ít nhất hai trong ba người thử trả lời đúng, không được người quan sát gợi ý:
1. Sau giao lệnh: Tâm đi làm gì, vì sao Nam phải tìm?
2. Sau báo cáo Tâm: làng cần chuẩn bị điều gì và vì sao?
3. Sau sửa máy: đã liên lạc được với ai, chi viện cần điều kiện gì?
4. Đầu Đêm 2: vì sao kiểm tra kho và bến, Hùng đang ở đâu?
5. Sau tiếp cận: Hùng đã giúp bằng thông tin nào, vì sao Nam đã an toàn ở cửa hầm?
6. Sau kiểm tra bến: công việc của Nam có ích gì cho dân?
7. Sau làm lành: Nam xin lỗi vì điều gì?

Đặt câu hỏi ở điểm nghỉ tự nhiên; ghi câu trả lời nguyên văn và vị trí họ bắt đầu hiểu sai. Nếu lỗi nằm ở thông tin cần để hành động, sửa lời giao việc hoặc dàn cảnh trước khi thêm lời giải thích dài.

### Gameplay và trình bày
- Mục tiêu, ghim và thoại chỉ cùng một nơi; kho ban ngày và hầm dự phòng phân biệt được.
- Hùng không cung cấp thông tin vượt tầm nhìn/nguồn tin đã xác lập.
- Không có đèn đồng loạt tắt hoặc NPC dịch chuyển mà thiếu nguyên nhân nhìn thấy được.
- Mọi tương tác có phản hồi kết quả; trạng thái còn đúng sau load.
- Chơi toàn bộ Ngày 2–Đêm 2 trên scene thật, sau đó kiểm tra việc chuyển Ngày 3 và các đường sơ tán dùng lại ở Đêm 3.
- Bản thoại mới, phụ đề và audio khớp nhau; không coi test logic đạt là đã nghiệm thu cảm xúc/hoạt ảnh.

## 9. Điều kiện hoàn tất

Hoàn tất khi các tiêu chí trên đạt, không còn lỗi chặn tiến trình hoặc mâu thuẫn vị trí/nguồn thông tin, tài liệu phản ánh đúng hành vi mới và có biên bản chơi thử. Các hạng mục hình ảnh/audio chưa hoàn thành phải ghi rõ; không gọi bản thoại chữ là bản trình bày cuối cùng.

Nguồn đối chiếu: `Implementation_and_QA.md`, `Night2_Stealth_Balance.md`, `Night2_BankRoute_Integration.md`, `Night2_Store_Loot.md`, CSV thoại Ngày 2–Đêm 2 ngày 24/10 và mã runtime hiện tại. Khi xung đột, dùng hành vi đã xác minh trong scene và quyết định thiết kế được chốt ở Đợt 1 để cập nhật lại tài liệu.
