# Map 1 — cảnh thất bại giải cứu Hùng

## Dàn cảnh được chốt

Áp dụng khi giám sát còn cạnh Hùng và báo động đã xác nhận. Giữ nguyên nhánh giám sát đã bị dụ đi xa: địch tập hợp đánh Nam, không dựng cảnh xử bắn giả. Đây là kết quả thất bại có thể retry, không thay cốt truyện thành công hoặc cảnh cuối Map 2.

1. Camera gameplay giữ Nam trong nhịp bị lộ: bước lùi ngắn, hạ người; tiếng cảnh báo hiện có và nhịp tim.
2. Khi giám sát bắt đầu ngắm, cắt sang góc trung thấy Hùng đang quỳ trói và lính giám sát. Vị trí lens chọn từ các ứng viên tránh vật cản; không dịch chuyển nhân vật hay bay camera qua địa hình. Tiến lens nhẹ 18 cm.
3. Một phát súng thật, nòng hướng ngực Hùng. Hùng gục bằng clip chết quỳ trói hiện có. Giữ góc này 3,3 giây sau phát bắn để đọc rõ chuyển động.
4. Cắt về góc gần Nam, duy trì chuyển động thở và phản ứng cơ thể hiện có trong 1,7 giây. Không thêm câu thoại chưa thu hoặc phụ đề địch.
5. Fade 0,8 giây rồi hiện thất bại. Không fade ngay lúc Hùng đang gục. Giữ Esc 1 giây cho cùng trạng thái thất bại, không trao thưởng.

Tổng thời lượng phụ thuộc đoạn cảnh báo hiện có + 1,6 giây ngắm + 5,8 giây sau bắn. Không slow motion toàn game. Giữ cơ chế ownership/khôi phục controller, camera, vũ khí và dây trói đang dùng.

## Tham khảo và cách vận dụng

- Clint Hocking / Ubisoft Montreal, GDC, *The Interesting Thing About Bishops: Simulation Boundaries in Splinter Cell*: https://media.gdcvault.com/gdc04/slides/interesting_thing_bishops.pdf — đối chiếu cách làm rõ giới hạn nhiệm vụ và hậu quả mất con tin. Không nhập thêm cơ chế cứu phút cuối hoặc đổi luật thất bại đã chốt.
- Naughty Dog, *The Last of Us: Joel and Ellie Truck Ambush Cinematic*: https://www.naughtydog.com/blog/the_last_of_us_joel_and_ellie_truck_ambush_cinematic — tư liệu cảnh phục kích tham khảo cho nhịp nguy hiểm nối sang phản ứng nhân vật; không sao chép thoại hoặc cảnh.
- Alfred Hitchcock, *My own methods*, Sight and Sound/BFI: https://www.bfi.org.uk/sight-and-sound/features/alfred-hitchcock-my-own-methods — dựng bằng chi tiết hành động rồi phản ứng để truyền đạt cảm xúc, tránh một khung hình đứng kéo dài.

- BFI / Sight and Sound, bài phân tích Dunkirk: https://www.bfi.org.uk/sight-and-sound/reviews/film-week-dunkirk-christopher-nolans-victorious-retreat — tham khảo cách giữ góc nhìn ở từng con người và giữ/cắt khung hình theo nhịp sự kiện. Không mượn bối cảnh lịch sử hoặc nhân vật của phim.

Đây là cách vận dụng thiết kế cho ShadowVale, không khẳng định các nguồn có cùng tình huống Hùng hoặc cùng luật gameplay. Trang video tham khảo và bài viết được tra cứu; không khẳng định đã xem trọn phim/game.

## Kiểm chứng

Đã kiểm tra trên Unity 6000.3.24f1, ngày 18/10/2026. Test cuối ExecutionShowsBoundDeathThenReturnsToNam: 1 passed, 0 failed (Logs/Map01Operations/video/execution-results.txt). Trước khi kéo dài nhịp giữ cảnh, test đầy đủ IntroAndExecutionStayBoundAndPresentDeathBeforeFailure cũng đã qua. Test cuối xác nhận: góc quay Hùng và góc trở lại Nam đều xuất hiện; đầu nhân vật ở trong khung; nòng súng ngắm Hùng dưới 5 độ sai lệch; Nam có bước lùi; Hùng không đứng dậy, dây trói còn và tư thế cuối gục thấp; nhiệm vụ thất bại sau khi cảnh hoàn tất. Đã xem các ảnh gameplay lúc ngắm, bắn, gục và phản ứng Nam. Cảnh mẫu dài khoảng 10,1 giây. Clip hiện tại kết thúc bằng Hùng gục về trước khi vẫn quỳ, không phải chết đứng hoặc ngã ngửa.

Ảnh kiểm chứng: Logs/RescueStory/Execution/GameView, mốc thời gian tại frames.csv. Ảnh được lấy thưa khoảng 0,5 giây để hạn chế chi phí đọc ảnh từ GPU; đây không phải FPS gameplay. Lượt thử trước có lỗi góc cây che và kiểm tra trước camera cập nhật; đã sửa camera dự phòng và đồng bộ cut trong LateUpdate rồi chạy lại. Kiểm tra này tập trung một tình huống báo động ở khu bến, chưa phải lượt chơi toàn Map 1–2 hoặc kiểm tra mọi hướng tiếp cận. Không thêm audio trả phí hoặc sửa Map 2.