# Kiểm chứng cứu Hùng và cảnh kéo vào bụi

Ngày: 14/10/2026. Dự án kiểm thử lấy từ nhánh Khoa 1f36340, giữ model Nam 58 xương hiện tại của nhánh này.

- Nhiệm vụ/cutscene Map 1: 15/15 passed (`rescue-khoa-complete.xml`). Gồm intro/camera/thoại, ngắm và gục quỳ trói trước màn thất bại, Nam/Hùng tới hầm trước, timing/pause/retry, ám sát, báo động, save, ba đợt hộ tống và phần thưởng.
- Nhánh bụi: thắng ba lượt với một lỗi; lính sống tới mốc gục, không đâm dao; thua hai lượt thì buông tay và tách nhau, tay về clip gameplay trước bàn giao. Đã so khớp tay Nam và tay phải lính với pose gameplay thật; tay trái lính kiểm tra tiếp xúc foregrip và giới hạn quay cổ tay 35°, kiểm tra khôi phục Avatar, renderer, input và vật phẩm.
- Hồi quy Map 2: 2/2 passed (`rescue-khoa-complete.xml`). Runtime, scene, model và Avatar gameplay Map 2 không sửa trong task này.
- Đã xem ảnh thực tế ba góc tại các mốc Pull/Struggle/Finish/Escape. Hai nắm tay Nam khép; điểm ôm dưới vai; không bịt miệng và không xuyên đầu. Tay thoát trở về tư thế cầm dao/súng nguyên gốc.

Mesh cận tăng từ khoảng 7,7 nghìn lên 30,8 nghìn đỉnh/tay trong Blender, dùng rig và vật liệu cũ. Chỉ bật renderer cận trong nhánh bụi. Mesh/Avatar/controller gameplay gốc được giữ và khôi phục; không tăng độ chi tiết toàn scene.

Video là từng nhánh capture GameView, không phải một lượt chơi liên tục toàn nhiệm vụ. Hai video Close là góc chẩn đoán tạm tắt lá để xem khớp; GameView giữ bụi mờ nhẹ thực tế. Âm thoại ghép bằng WAV đã nhập theo thời điểm DialogueVoice phát, không phải ghi âm loopback. Không tuyên bố đã chơi tay mọi hướng hoặc đo FPS sản phẩm.

Kiểm tra triển khai tại dự án chính ghi riêng trong `main-import-verification.json`. Source Blender, script và hướng dẫn chỉnh ở thư mục cha.
