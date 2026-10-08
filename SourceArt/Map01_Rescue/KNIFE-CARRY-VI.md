# Đi và lén lút với dao; lính giám sát nghe đá

## Phạm vi

Chỉ thay hai nhóm chuyển động cầm dao: đứng/đi/chạy và cúi nghỉ/bước lén. Tái sử dụng chân, hông và nhịp bước từ các clip Nam sẵn có; tạo tay giữ dao riêng thay cho khung hình động tác đâm bị đóng băng. Giữ nguyên nhấn tấn công, animation đâm, cặp ám sát, thông số damage, tốc độ di chuyển, súng và các cutscene.

Lính giám sát nghe đá sẽ đi tới điểm NavMesh an toàn gần chỗ đá rơi, tối đa 9 m từ điểm canh, tìm kiếm rồi quay về đúng vị trí và hướng ban đầu. Các bước chân trong khi đang theo đá không hủy ngay lần điều tra; nhìn thấy Nam hoặc nghe súng vẫn gây báo động. Không đi xuống sông theo đá.

## Chỉnh animation thủ công trong Unity

1. Các clip chỉnh được nằm tại `Assets/_Project/Art/Characters/Animations/KnifeCarry`. Sao chép clip trước khi thử để có bản dự phòng.
2. Chọn Nam trong scene, mở **Window > Animation > Animation**, chọn clip tương ứng. Bật Preview, tắt Record khi chỉ muốn xem. Trong Curves, chọn các thuộc tính Right/Left Arm, Forearm và Hand để chỉnh tay; hạn chế sửa RootT hoặc chân nếu không muốn đổi nhịp bước.
3. Chỉnh cả đầu và cuối chu kỳ giống nhau để không giật khi lặp. Xem lại ở mặt trước, bên hông và phía sau; dao không xuyên thân, cổ tay thẳng theo cẳng tay và khuỷu không ép xuyên hông.
4. Animation controller chỉ thêm `Locomotion_Knife` và `Sneak_Knife`. Dáng thường blend theo Speed: 0 / 2,5 / 6 m/s. Dáng lén blend 0 / 1,2 m/s. Các timeScale khớp nhịp chân nguồn, không thay tốc độ gameplay.
5. Sau khi chỉnh, thử đứng, đi, chạy, cúi nghỉ, cúi đi, đổi dao/súng và đâm rồi trở về dáng cầm dao. Chọn New Game để kiểm tra đoạn cứu Hùng.

## Tái tạo

Menu **ShadowVale > Characters > Update knife walk and sneak** chạy `KnifeLocomotionSetup.Build`. Script nguồn giữ clip FBX gốc, chỉ tạo các bản `.anim` mới và thêm các state dành cho dao. Trong `PoseArm/Bake`, vị trí mục tiêu hai tay được tính theo hông, có dao động nhỏ theo nhịp bước. Sửa script nếu muốn tái tạo bằng thông số khác.

Chạy lại menu này sẽ ghi đè các clip KnifeCarry; hãy sao lưu chỉnh sửa thủ công trước. Nếu đã chạy Build Player Animator, chạy menu Update knife walk and sneak sau đó để đưa hai state dao trở lại controller.

Kết quả kiểm thử và ảnh/video nằm trong `Verification/KnifeCarry`. File trước triển khai được sao lưu trong workspace Codex: `work/before-knife-carry`.


## Bổ sung: cổ tay và khoảng tiếp cận sau đá

Dáng dao đứng/đi/chạy lấy hướng xoay cục bộ của vai, cánh tay và cổ tay từ tư thế nghỉ trung tính. Giải cánh tay tới vị trí giữ dao nhưng giữ cổ tay theo cẳng tay, tránh cách ép riêng hướng ngón tay gây xoắn. Thân trên giữ phần lớn tư thế nghỉ, vẫn còn độ lắc theo bước. Dáng cúi, clip đâm, cặp ám sát và mount dao không thay trong lần sửa này.

Lính giám sát có khoảng kiểm tra tập trung **9 giây**, góc nhìn thân chỉ quét khoảng **±8 độ** quanh hướng đá rơi. Tiếng bước chân cúi không hủy lần điều tra đá, còn đi/chạy ồn, nhìn thấy Nam và tiếng súng vẫn được xử lý. Ném đá sang phía khác rồi cúi vòng sau; không ném ngay vào chân Nam. Điểm đến phải có đường NavMesh trên đất, không đi xuống sông.

Các số chỉnh trong Map01RescueLayout: `Overseer Stone Radius = 9`, `Overseer Stone Focus Seconds = 9`, `Overseer Stone Sweep Degrees = 8`. Không thay AI ba lính tuần tra hoặc địch nhiệm vụ sau.

Menu **ShadowVale > Characters > Polish standing knife wrists** chỉ tạo lại ba clip đứng/đi/chạy, giữ nguyên hai clip cúi và controller. Đây là menu dành cho lần sửa cổ tay này. Nếu đã sửa thủ công clip `.anim`, sao lưu trước khi tái tạo.

Kết quả thử và ảnh cận ở `Verification/KnifeWristLure`. Kiểm tra đi vòng sau dùng đá bay thật, input đi cúi thật và đường NavMesh; không dịch Nam vào sát lưng sau khi ném đá.


## Bổ sung: cổ tay khi cúi cầm dao

Dáng nghỉ cúi và đi lén cũng sử dụng hướng cổ tay trung tính, không ép ngón tay về hướng cố định trong không gian. Giữ nguyên toàn bộ chuyển động thân, hông và chân nguồn của dáng cúi; chỉ sửa đường cong vai và hai tay. Menu **ShadowVale > Characters > Polish sneaking knife wrists** chỉ tái tạo hai clip Sneak_Idle/Sneak_Walk. Các clip đứng/đi/chạy và controller không được ghi lại bằng menu này. Kết quả kiểm tra toàn bộ chu kỳ và ảnh cận nằm tại `Verification/KnifeSneakWrists`.
