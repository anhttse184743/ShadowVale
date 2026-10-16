# Chỉnh lính giám sát và độ khó áp sát — 08/10/2026

Bản chính: `G:\game\ShadowVale`.

## Cách chơi

Cúi người, cầm dao, vòng ra sau và tấn công trong 1,6 m. Góc phía sau vẫn từ 130 độ. Tiếng đá làm lính điều tra ngay. Khi lính giám sát còn sống, nghe súng hoặc xác nhận thấy Nam vẫn xử bắn Hùng; sau khi ám sát lính giám sát mới được dùng súng.

## Thông số chỉnh được

Mở prefab `Assets/Resources/Rescue/Map01RescueLayout.prefab`, component Map01RescueLayout:

| Thông số | Giá trị | Tác dụng |
|---|---:|---|
| Crouched Footstep Scale | 0,55 | Tiếng bước chân cúi 3 m còn 1,65 m đối với nhóm cứu Hùng |
| Footstep Reaction Seconds | 1,0 s | Cửa sổ phản ứng trước khi quay đi điều tra; tiếng liên tục không kéo dài vô hạn |
| Close Detection Seconds | 1,6 s | Hệ số tăng nghi ngờ khi thấy Nam; gần sát mất khoảng 1,15 s để xác nhận |

Không áp dụng thay đổi này cho AI các nhiệm vụ sau. Đứng quá sát vẫn bị nhận biết; chạy, đi thẳng vào tầm nhìn hoặc dùng súng không trở thành lén lút an toàn.

## Tay và súng

Map01OverseerGrip chỉ gắn vào actor lính giám sát tại runtime. Súng theo tay phải với góc tham chiếu đứng ổn định cả sau load, tay trái được giải theo điểm foregrip, khuỷu hướng ra ngoài và xuống. Không thay model/skeleton hoặc mount súng chung của các cutscene.

Muốn chỉnh chi tiết: trong Play mode chọn Animator của `patrol_0`, xem Map01OverseerGrip. `Local Gun Euler` chỉnh góc súng theo bàn tay; `Support Weight` chỉnh độ bám tay trái (0–1). Thay đổi Play mode không được lưu, hãy đưa giá trị mong muốn vào mặc định trong Map01OverseerGrip.cs rồi kiểm tra lại đứng, tìm kiếm, aim và bắn.

Bản sao file trước triển khai nằm trong `work/before-guard-polish` của workspace Codex. Kết quả kiểm thử và ảnh xem ở `Verification/GuardPolish`.
