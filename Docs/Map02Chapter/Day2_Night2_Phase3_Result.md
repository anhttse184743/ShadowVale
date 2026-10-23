# Đợt 3 — Luật chơi Đêm 2

## Phạm vi đã triển khai

- **N2_01:** phải đứng trong vùng đầu cầu (bán kính 16 m), cách tổ tuần tra hơn 8 m, giữ F và nhìn đúng đèn/người tuần tra không bị vật che trong hai giây. Có ba nguồn đèn trắng lạnh trên ba lính; đèn dầu trong làng giữ màu ấm. Hướng luồng sáng và hướng kiểm tra phát hiện dùng chung dữ liệu. Nếu tổ lính đã bị hạ, khu vực còn dấu vết vẫn có thể được quan sát, không làm kẹt tuyến.
- **N2_02:** nhấn E báo đàm tại đầu cầu; không mở cutscene khóa Nam. Hùng chỉ nói câu đèn đang quay tới khi góc quét thực tế đang tiếp cận; câu đã qua đợi hướng quét rời Nam hoặc Nam ra ngoài tầm đèn. Các câu tiếp theo và cue xích chạy nối tiếp trong lúc người chơi vẫn đi được.
- **Tuần tra:** có vật che thì tia phát hiện không tới Nam. Lần phát hiện đầu giữ hướng soi tại vị trí vừa thấy Nam trong ba giây; HUD hiển thị đang soi thêm. Lần hai khôi phục baseline đầu cầu đã có từ Đợt 1, không lấy checkpoint mới nhất ở kho/bến. Save/load giữ cảnh báo và thời gian soi thêm, đồng thời có khoảng an toàn lúc vừa tải.
- **N2_03:** đến gần kho chưa hoàn tất. Phải đứng trước nắp hầm, nhìn chốt và nhấn E. Nắp vẫn đóng; có phản hồi chốt còn chắc, âm khóa và chuyển động chốt ngắn. Dùng vật liệu gỗ/kim loại opaque của dự án để nhìn rõ hình cửa trong đêm.
- **N2_04:** kiểm tra lần lượt bờ mương → lối qua xóm → bờ nam → bậc bến. Ba điểm đầu xác nhận theo vị trí; điểm cuối cần E. Ghim và đường dẫn đổi theo điểm kế tiếp. Đi tắt bến hoặc đi sai thứ tự không hoàn tất. Save/load giữ đúng các đoạn đã kiểm.
- **Cue xích:** thêm `OperationsAudio/night2_chain.wav`, dài 3,8 giây, phát xa ngoài đồng trước câu “Tiếng gì thế?”. Cờ đã phát được lưu; tải lại không phát lặp. Retry cả tuyến cho phép phát lại một lần. Cue và tiếng rè tạm dừng cùng menu/pause. Cùng clip được gọi lại ở sự kiện xe tăng xuất hiện Đêm 3, tại vị trí xe, không phát lại chỉ vì load.
- **N2_05:** Hùng ở trạm hỗ trợ; Nam quay về không có thời hạn. Nếu còn đoạn nghe xích/đàm, có thể tiếp tục điều khiển Nam trong lúc nghe, rồi E bắt đầu cảnh làm lành đã có. Không bắt buộc giết lính ở bất kỳ bước kiểm tra nào.

## Dữ liệu và source

Giữ 43 ID nhiệm vụ và chapter version 3. Đổi kind của N2_02/N2_03/N2_04 để các bước này không rơi vào handler talk/reach cũ. Thêm trường mặc định tương thích: giai đoạn hướng dẫn bộ đàm, hướng và thời gian soi thêm. Dùng các trường đã chuẩn bị `night2RouteVisited`, `night2ChainCuePlayed`. Save cũ đã hoàn thành nhiệm vụ vẫn giữ hoàn thành.

Các điểm tuyến nằm trong ChapterLayout. Logic kho, đường bến, cue và phụ đề đàm tách vào `Map02ChapterNightRoute.cs`; logic ánh đèn ở `Map02ChapterNightPatrol.cs`.

Clip xích là hiệu ứng kim loại tổng hợp tạm, có source tạo lại ở `SourceArt/Audio/build_night_chain.py`; không phải giọng nói hoặc bản thu foley hoàn thiện. Các câu đàm dùng đúng ID kịch bản; chỉ phát giọng khi đã có bản ghi được duyệt, còn lại hiển thị phụ đề. Hoạt ảnh nằm sát/diễn xuất kết đêm và thu/ghép giọng thuộc Đợt 4; tư thế thấp hiện dùng C/Ctrl sẵn có.

## Cách thử trong Unity

1. Dừng Play, chọn **ShadowVale > Map 2 > Chạy thử Đêm 2**.
2. Tại đầu cầu, nhìn về phía nam cầu/chỗ tìm thấy Tâm lúc sáng; giữ F nhìn đèn hai giây.
3. E báo đàm, sau đó đi tự do theo lời Hùng và vật che. Thử đứng phơi sáng để thấy phản ứng soi thêm; lần hai phải trở lại đầu cầu.
4. Theo ghim tới kho, nhìn chốt cửa rồi E. Theo từng ghim xuống bến; E xác nhận bậc bến. Có thể save/load giữa các đoạn.
5. Về ghim ở trạm Hùng. Không có đồng hồ bắt trở về. Nghe hết cue/đàm nếu còn, E vào cảnh kết đêm.

## Kiểm chứng

`Map02NightTwoRouteTests`: save v3/migration, báo đàm không khóa điều khiển, câu hướng dẫn chờ đúng trạng thái đèn, cue chỉ phát một lần qua load và tạm dừng cùng pause, Hùng ở trạm, cửa cần nhìn/E, tuyến bến cần đúng thứ tự và xác nhận cuối, giữ tiến độ qua ba lượt load, hoàn tất Đêm 2 với ba lính còn sống.

Các đoạn kho → bến được khảo sát NavMesh và đi bằng capsule cùng kích thước/layer của Nam. `Map02DayTwoStateTests` kiểm tra thêm vật che, soi thêm, lưu cảnh báo và retry đầu cầu giữ tài nguyên; chạy lại cả ReportAndNightPatrol và RoutePreview.

Lượt cuối ngày 22/10/2026 đạt **9/9**, không có test lỗi; đã xem lại ảnh render cửa hầm và ba đèn tuần tra. Báo cáo: `Logs/Map02Chapter/night2-phase3-results.xml`. Ảnh cửa: `Logs/Map02Chapter/night2-door.png`. Điểm tuyến: `Logs/Map02Chapter/night2-route-survey.txt`.
