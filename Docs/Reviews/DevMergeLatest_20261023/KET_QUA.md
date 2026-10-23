# Bổ sung dev 7abc29af → thinh — 23/10/2026

Sau commit merge `7c8118c6`, remote dev có thêm đợt cập nhật tại `7abc29af`: 190 file, gồm ngày 3/đêm 3 Map 2, mô hình M41, thùng tiếp tế, bố trí/chiều cao nhân vật, sửa thuyền, tay giữ súng và bước chân. Đã ghép tiếp đợt này vào thinh.

## Kết quả

**102 ca khác nhau: kết quả mới nhất của mỗi ca là 101 đạt, 1 lỗi, không bỏ qua.** Đây là kiểm tra hồi quy có chọn lọc. Không cộng với 123 ca của [đợt merge trước](../DevMerge_20261023/KET_QUA.md) vì có nhiều ca trùng và hai snapshot mã khác nhau.

| Lượt | Đạt / tổng | Ghi chú |
| --- | --- | --- |
| latest-01 | 91/93 | Một lỗi test bước chân ở đoạn đảo hướng; một lỗi pose ngón cái cầm đá |
| followup-02 | 13/15 | Bước chân, boss, M16, 5 ca giáp/metadata đạt; hai ca Mosin gặp lỗi callback của fixture và ảnh báo cáo bị khóa |
| final-03 | 6/6 | Chạy lại toàn bộ MosinBalance và hai ca QIEA, đều đạt |

Các phần đã xác minh:

- Chế dã chiến **4/4 đạt** trên bản dev mới, gồm quy đổi đạn, C3 hữu hạn, hủy/hoàn vật tư và save/load Map 1/2.
- Cảnh xe tăng, hy sinh, phản công, bình minh, skip, checkpoint, thứ tự telemetry, sơ tán/giữ ngõ, đạn rơi, tiếp tế, chiều cao nhân vật và tay giữ súng qua các ngày/save đều đạt trong các ca đã chọn. Không chạy các ca quay video/performance Explicit thủ công.
- Mosin một viên hạ địch khi trúng đầu/thân ở 35–199 m, kể cả chỉ huy thật có giáp; đạn chờ xử lý sau đổi súng vẫn giữ đúng loại vũ khí. Tường, tầm tối đa và bảo vệ cinematic vẫn có hiệu lực; tâm scope thực sự giao với mục tiêu.
- Hai ca QIEA **2/2 đạt** trên bản cuối, dùng solver tại `D:\DO_AN_GAME\shadowvale-solver-main`, cổng 8011. Dịch vụ thử do lượt này mở đã được dừng.

## Các chỗ kết hợp/sửa

- Bố trí gia đình: dùng phần sửa mới của dev để độ lệch chỗ ngồi chỉ áp dụng ngày 1/đêm 1. Bé Tí ở đêm 3 cách điểm tìm trẻ 0,8 m, trong tầm tương tác; sau cứu về cạnh mẹ.
- Test hộ tống giữ cả kiểm tra tên hai người, khoảng cách và câu hướng dẫn mới. Khôi phục lại vị trí Lành sau phép thử; dòng khôi phục của một phía trước đó nằm sau dấu comment.
- Giáp mới dùng `Health.TakeBulletDamage`, còn phát bắn chờ xử lý của Nam dùng `TakeBulletDamageFromWeapon`. Đã đưa cả hai về cùng đường xử lý giáp, giữ metadata vũ khí và chỉ áp dụng modifier một lần. Vũ khí có chính sách hạ địch tức thì vẫn đảm bảo sát thương chí tử sau giáp, với điều kiện mục tiêu là địch; bảo vệ cinematic được kiểm tra trước. Súng thường vẫn chịu giáp. Có 5 test mới và ca Mosin bắn chỉ huy thật kiểm tra tương tác này.
- Test bước chân vẫn kiểm tra độ thấp của chân ngay lúc phát âm cho mọi bước. Điều kiện chân tiếp tục thấp sau 0,15 giây chỉ áp dụng khi đoạn đi đã ổn định, ngoài vùng chuyển hướng W/S; không đổi ngưỡng đặt chân trong game.
- Callback thử giáp của Mosin được tạo trong helper sau EnterPlayMode để không mất qua domain reload. Ảnh/save thử Mosin dùng thư mục riêng của lượt chạy khi có `SVL_REGRESSION_REPORT`, tránh ghi vào ảnh đang được mở.

## Lỗi còn lại

`StoneMouseInputTests.SlotFourAndMouseThrowWithoutFiringOrLosingCancelledStones` vẫn lỗi tại pose ngón cái: khoảng **20,005°**, test nguyên gốc yêu cầu **trên 30°**. Không sửa test này để biến kết quả thành đạt. Runtime/asset chuyển động v3 được giữ từ thinh; chi tiết chẩn đoán ở báo cáo đợt trước. Bộ kiểm tra hiện chưa hoàn toàn xanh.

## Bằng chứng và workspace

`results.json` chứa từng ca và lần chạy mới nhất; ba XML `*-results.xml` là cây kết quả đầy đủ từ Unity. Bộ lọc test được lưu cạnh báo cáo. Unity 6000.3.24f1, EditMode với các ca vào PlayMode; dùng save thử riêng.

Đã gỡ observer tạm khỏi Assets, khôi phục các ảnh test ghi đè và giữ nguyên hash hai ảnh dirty có sẵn trong `Tools/M16Reports`. Không đưa ảnh dirty, backup, log và save thử vào commit. Bản chụp cuối Mosin và dữ liệu thô vẫn còn trong thư mục lượt chạy trên máy.
