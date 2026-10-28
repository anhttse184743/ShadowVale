# Thân thuyền và chuyển động tay/súng Nam — 2026-10-28

## Kết quả
- Cuối Map 1: Nam cầm một khẩu khi bước xuống thuyền, ngồi và bắn yểm trợ. Đầu Map 2: vẫn một khẩu, chuyển sang đeo sát ba lô.
- Lượt cuối: **3/3 kiểm thử Play Mode đạt**, gồm toàn tuyến một súng qua hai map, quay cảnh có lính sống, bắn yểm trợ hạ lính mà không tiêu đạn/sinh loot phụ.
- Kiểm tra bổ sung: **2/2 đạt**, gồm giới hạn lật cổ tay trong dữ liệu quay và Esc/báo cáo/checkpoint.
- Bản quay mới có 6 phát bắn. Sai lệch nòng lớn nhất tại phát bắn là 0,0198°; bản trước khoảng 7,7–8,3°.
- Mức nhảy cổ tay lớn nhất giữa hai tư thế ghi nhận trong ba pha bước xuống/ngồi/bắn: tay phải 24.983°, tay trái 33.154°. Bản trước từng có 93,6–123,1° ở tay phải và 77,5–108,1° ở tay trái khi bước/ngồi.

## Nguyên nhân và thay đổi
1. Động tác tay giữ súng trước đây còn phụ thuộc clip chân chạy/nhảy/ngồi, nên thay clip hoặc bỏ mục tiêu có thể đổi tư thế ngay. Bộ giữ súng liên tục dùng vị trí/rotation tương đối theo nhân vật, giới hạn tốc độ chuyển, giữ mặt phẳng khuỷu và khung xoay xương ổn định. Tay trái bám nòng, ngón tay giữ tư thế cầm, chân/root tiếp tục dùng clip cũ. Giữ chiều dài xương.
2. Đồng hồ Map 1 từng tiến cả khoảng trễ khung hình; bản ghi có khoảng trễ 0,8–1,4 giây làm bỏ nhiều tư thế. Nay bước hoạt ảnh giới hạn 0,05 giây và lịch phát đạn/recoil/đổi mục tiêu cùng dùng đồng hồ cảnh. Ở FPS dưới 20, cảnh sẽ tiến chậm hơn để tránh nhảy tư thế; đây không phải tối ưu tăng FPS. Giữ Esc dùng thời gian thực.
3. Hướng súng trước đây dùng giả định trục/điểm gốc. Nay đo trục từ tay cầm tới đầu nòng và đặt tâm tay cầm theo đúng hình học model, đồng nhất hướng nòng với đích bắn.
4. Thuyền trước chỉ dùng MaterialPropertyBlock để tránh đường GPU. Bản sửa gắn đúng component loại trừ GPU cho thuyền trước khi bật renderer, ở cả nguồn Map 1 và renderer mới trong Map 2. Cờ GPU của thân thuyền trong dữ liệu sau sửa là False; mesh ở đúng scene, bounds đúng vị trí, renderer bật. Dùng [cơ chế loại trừ từng object được Unity mô tả](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/make-object-compatible-gpu-rendering.html). Component thuộc Unity.RenderPipelines.GPUDriven.Runtime trong SDK đang cài; link.xml giữ type cho build có stripping.

## Phạm vi và giới hạn xác minh
- Ảnh người dùng cho thấy mất mesh. Lượt baseline tự nhiên của máy hiện chưa tái hiện được đúng lần mất đó; không coi renderer.enabled/isVisible là bằng chứng đủ. Bản sửa được kiểm tra bằng cờ renderer, ảnh và chuỗi hình thật trong cảnh có chuyển scene. Hai lượt cuối thấy thân thuyền hiện đủ.
- Bản ghi chính có hai lính sống đặt ở bờ để Nam thực sự bắn; đã đo hướng nòng trong khung phát đạn. Kiểm thử toàn tuyến riêng chạy đến gameplay Map 2; bản quay tập trung kết thúc sau cập bến rồi bỏ qua phần lên bờ.
- Video không thu âm. Video ghép từ ảnh thực của Unity, căn theo đồng hồ cutscene để rà tư thế, bỏ đoạn tải scene. Không nội suy hình, không phải phép đo FPS. Bỏ ảnh cũ còn sót theo thời điểm bắt đầu lượt quay.
- Các kết quả giới hạn ở các cảnh và tình huống trên; không tuyên bố toàn game hết lỗi.

## Bằng chứng và chạy lại
- Nam_LenThuyen_BanYemTro.mp4, Map2_CapBen_Thuyen.mp4: video rà chuyển động.
- Nam-len-thuyen.png, Nam-ban-yem-tro.png, Map2-thuyen.png: ảnh lượt cuối.
- Motion-before.csv, Motion-after.csv, Motion-summary.txt, Map2-render.txt: dữ liệu gốc.
- StableTests.xml, Checks.xml: kết quả native cuối.
- Trong Unity Test Runner chạy BoatMotionHullTests.NaturalLiveCoverAndArrivalMotionReview với biến môi trường SV_BOAT_REVIEW_TAKE=After, rồi RecordedReviewHasNoLargeWristFlips. SourceArt/BoatMotionHull20261028 có script kiểm tra dữ liệu/xuất video.

Bản sửa chưa push.
