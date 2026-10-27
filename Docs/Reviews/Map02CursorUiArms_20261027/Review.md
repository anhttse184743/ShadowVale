# Map 2 — Con trỏ và giao diện sửa bến (27-10-2026)

## Phản hồi được xử lý

UI kê ván bị dồn vào một dải nhỏ, hình thanh đỡ khó đọc, không rõ thứ tự thao tác. Con trỏ có thể bị khóa về giữa màn hình khi UI cần dùng chuột; người chơi báo không bấm lưu được.

## Tham khảo và quyết định

- [Unity Cursor.lockState](https://docs.unity.com/en-us/engine/7000.0/script-reference/unityengine/cursor/lockstate): khóa chuột thay đổi vị trí/khả năng tương tác; cần có chủ sở hữu input rõ ràng. Áp dụng: chỉ camera đang hiển thị nhận look/cursor; camera làng bị thay thế cũng tắt driver.
- [Xbox Accessibility Guideline 114: Context](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/114): cung cấp ngữ cảnh để người chơi biết chức năng và tác dụng của điều khiển. Áp dụng: mỗi bước có tiêu đề hành động, trạng thái đang chọn, sơ đồ hai thanh đỡ và lý do chưa kê được.
- [Xbox Accessibility Guideline 112: UI navigation](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/112): thứ tự hiển thị và cách tương tác nhất quán. Áp dụng: chọn vật liệu bên trái → chọn bậc/căn ở giữa → xác nhận bên phải; giữ phím thay thế.
- [Game Accessibility Guidelines](https://gameaccessibilityguidelines.com/full-list/): mục tiêu bấm lớn/cách nhau, ổn định, không bắt buộc chỉ kéo/giữ nhiều nút, chỉ rõ vật tương tác. Áp dụng: thẻ ván lớn, click bậc, hai nút dịch từng bước cùng slider tùy chọn, nút xác nhận riêng.
- [Unpacking — trang nhà phát triển](https://www.unpackinggame.com/): tham khảo cách biến bố trí vật thành câu đố không gian, vật thể và chỗ đặt phải đọc được. Chỉ lấy hướng tiếp cận; sơ đồ/luật hai thanh đỡ là thiết kế riêng của ShadowVale.

## Bản sửa

1. Camera không render không đọc chuột hoặc khóa cursor khi Start/LateUpdate. Khi thuyền chuyển map, tắt driver của camera làng đã bị thay thế.
2. Map 2 chặn input gameplay khi ForestMenu/overlay mở. Các thao tác pause/đóng túi/đóng bàn chế tạo giữ chuột tự do nếu một UI khác vẫn cần chuột.
3. Thay phần kê ván bằng bảng lớn: năm thẻ vật liệu có hình, ba bậc có trạng thái, hai thanh đỡ trên sơ đồ, xem trước ván theo offset/xoay. Hướng dẫn theo bước và phản hồi bằng chữ.
4. Cọc vẫn giữ thử thách nhịp bấm; không đổi số cọc/ván hoặc giảm độ khó của luật. Ván nứt/ngắn vẫn có thể xem, không tự chọn đáp án.
5. Giữ tiến độ khi đóng bảng; save của người dùng không bị ghi đè trong kiểm thử (test dùng thư mục save cô lập).

## Cách kiểm tra

Bổ sung sự kiện Input System chuột (position/delta/button) đồng thời sự kiện GameView/IMGUI: lớp thứ nhất đi qua script camera, lớp thứ hai đi qua UI thật. Lượt trước chỉ kiểm tra phần click UI nên không đủ phát hiện giành con trỏ.

Ca camera ẩn phải hỏng trước sửa và đạt sau sửa. Ca runtime bấm Esc → Lưu game → chọn Ô 01 → Lưu/xác nhận ghi đè → đọc bản lưu từ đĩa. Thử chọn ván/slider tại 1200×675, 1600×900, 1920×1080; kiểm tra camera không đổi yaw, cursor vẫn tự do. Chạy lại chuỗi 10 nhiệm vụ và các trường hợp ván nứt/ngắn, xoay/căn sai, hoàn tất bến.

## Tay, tư thế và chuyển map

- Giữ nguyên skeleton Nam/Hùng. Nam có xương ngón nhưng Avatar gameplay không ánh xạ chúng; lấy hiệu chỉnh ngón từ Avatar Nam C1 hiện có, chỉ áp dụng các xoay ngón, không thay Avatar chơi game. Hùng chỉ có xương kết thúc bàn tay nên giữ nắm tay của mesh gốc.
- Giải tiếp xúc cánh tay với hướng khuỷu rõ, hạn chế vặn cổ tay; đũa theo điểm kẹp ngón, lòng bàn tay đỡ chén. Chuyển cầm/thả đồ theo đường bao mềm. Dịch ghế Nam/Hùng để chân không chạm mâm.
- Nam ngồi trực đặt bàn tay úp trên đầu gối. Hùng nằm có tay trái đỡ cẳng tay phải; điểm nghỉ tính cả độ dày túi áo.
- Lỗi đúng ảnh người dùng: khôi phục checkpoint sau khi thuyền đã chuẩn bị gọi lại controller đứng/đi và neo mesh Hùng xuống mặt đất. Trước sửa, hông từ **y=0,39 xuống −1,21 m** trong hệ tọa độ thuyền. Sau sửa, bộ phận extraction giữ quyền điều khiển tư thế ngồi, y trước/sau đều **0,39 m**; dây trói không xuất hiện lại.
- Chuyến đi liên tục từ Map 1 qua Map 2 được chạy tự nhiên sau khi đặt fixture ở mốc rút lui: Nam lên thuyền, chỉ huy chèo, Hùng giữ tay bị thương, cập bến rồi trả gameplay. Không đổi người chèo hoặc thoại.

## Kiểm thử

- **Release.xml: 35/35 đạt, Unity 6000.3.24f1, DX12.** Gồm kiểm tra camera ẩn; menu lưu thật; click/slider ở 1200×675, 1600×900, 1920×1080; kho; sửa bến; checkpoint; sáu thứ tự việc ban ngày; tính thuốc/thưởng một lần; các chốt; bữa cơm; đổi ca; chuyển Map 1→2.
- Ca input chuột đi đồng thời qua Input System và GameView/IMGUI. Theo dõi **128 khung cập nhật chuột thật**, không bị giành cursor khi UI mở; bấm Lưu ở ô 01 và đọc lại file thành công. Test dùng thư mục save riêng.
- Lượt đủ **10 mục tiêu từ cập bến đến Ngày 2** dùng di chuyển và phím tương tác; driver không gọi hoàn thành nhiệm vụ. Chạy timeScale 2 và nhấn tiếp thoại, nên không dùng để báo thời lượng chơi.
- Chụp cận hai nhân vật ở 13 mốc ăn/nghỉ, đo cổ tay và xem ảnh. Ảnh cận tay có đèn chẩn đoán riêng để nhìn rõ; ánh sáng bản chơi không bị thay.
- Bản sửa cuối hướng lòng bàn tay và kích thước băng được kiểm tra tập trung riêng; kết quả kèm XML.

Giới hạn: đây là kiểm thử tự động và xem ảnh trong các tuyến nêu trên, chưa phải nghiên cứu người chơi mới hoặc bảo đảm toàn bộ game không còn lỗi. Ca tải thuyền gọi đúng đường khôi phục tư thế của SaveSystem; không giả định đã kiểm tra mọi file save cũ ngoài thực tế.


## Lượt chốt hình ảnh

- Polished.xml: **2/2 đạt** sau sửa hướng lòng bàn tay và quyền điều khiển checkpoint thuyền.
- FinalVisual.xml: **2/2 đạt** sau thu băng theo mesh bắp tay. Kiểm tra tiếp xúc tay chị Lành/băng ở 6–17 giây, băng đúng bắp tay phải và được dựng lại sau load; kiểm tra 13 mốc tay ăn/nghỉ.
- Băng trước đó lấy lẫn mesh ngực/túi áo; hiện lọc theo trọng số xương cánh tay và giới hạn bán kính theo chiều dài xương. Không thay mesh/skeleton nhân vật.

Ảnh kiểm chứng: [tới bến Map 2](Map2-Approaching.png), [rời bến Map 1](Map1-Departing.png), [Nam cầm chén/đũa](Meal-Nam.png), [Hùng cầm chén](Meal-Hung.png), [Nam ngồi trực](Rest-Nam.png), [Hùng nằm](Rest-Hung.png), [thay băng](Bandage.png), [UI sửa bến](Dock-1920.png), [lưu game](Saved-slot.png).

- UiFinal.xml: **1/1 đạt** ở lượt chốt, lưu game và thao tác chuột ở cả ba độ phân giải; nhãn bậc đã rút gọn để không cắt chữ.
