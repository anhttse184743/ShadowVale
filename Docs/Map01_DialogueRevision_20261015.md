# Thoại và bàn giao nhiệm vụ Map 1 — 15/10/2026

Luồng đang dùng: nhận lệnh cứu Hùng → giải cứu/hộ tống → báo cáo tại căn cứ → điểm 1 lấy thuốc và vật tư → báo cáo → điểm 2 ngắt đài → báo cáo → điểm 3 xác nhận và hạ sĩ quan → báo cáo/rút ra bến.

## Những thay đổi

- Đoạn tiếp cận khu cứu Hùng chỉ còn ba câu. Nam đứng yên, hạ vũ khí, đưa tay tới tai; giọng Nam sạch, chỉ giọng chỉ huy ở đầu bên kia có lọc bộ đàm.
- Báo cáo tại căn cứ ngắn lại còn bốn lượt. Khi Hùng nói, camera chuyển mềm tới vị trí thật của Hùng.
- Báo cáo điểm 1 và điểm 2 đều ba lượt. Không có lời rút ra bến trong các nhóm này. Xác nhận mục tiêu điểm 3 xong mới nhận lệnh hạ mục tiêu.
- Nếu chỉ huy địch chết sớm, vẫn phải tới điểm 3 để xác nhận. Không hồi sinh hoặc phát lại động tác chết của xác cũ. Chỉ huy địch giữ khu vực quanh chốt, không đuổi xuyên bản đồ tới điểm 2.
- Súng NPC hiệu chỉnh một lần theo tư thế ngắm của Avatar, có tiếp xúc tay phải/tay trái; không ép cổ tay quay theo hướng nòng súng. Tư thế tiếp xúc không chạy khi NPC đang bị khống chế hoặc chết.
- Mỗi lần bắn thật vào Nam hoặc Hùng đều phát âm thanh từ vị trí muzzle bằng audio pool dùng chung. Dùng lại tiếng rifle hiện có, không tạo hoặc mua SFX mới.
- Giữ stage ID, quest ID và các snapshot cũ; snapshot story revision 2 thêm cờ đã tới điểm 3. Save đã tới giai đoạn rút lui vẫn tiếp tục được.

## Audio

17 bản thu mới, dùng lại câu Nam “Rõ.” ở hai nhóm khác. Nam/Brodie, Hùng/Clint, chỉ huy/Tom. Số dư được báo 360 → 337: dùng 23 trong trần 23 credits đã duyệt.

Kịch bản cuối: `SourceArt/Dialogue/Map01Revision20261015/SCRIPT-DUYET.md`.
Nguồn MP3 và bản WAV sạch: `Raw/`. Bản Unity 48 kHz mono PCM16 và bản radio: `VI/`.
Các cue cũ đã rời catalog runtime; nguồn cũ vẫn được lưu để truy vết. Bản sạch và bản radio không phát cùng lúc.

`rebuild_audio.py` dựng lại từ MP3 cục bộ và `approved-request.json`, không dùng mạng hoặc tạo thoại mới. Cần numpy, soundfile, imageio-ffmpeg trong Python. Sau khi chỉnh âm thanh, sao chép WAV tương ứng từ `VI/` sang `Assets/Resources/Dialogue/VI/`, giữ tên và metadata. Cập nhật caption, resource và thời lượng trong cả hai `voice-catalog.json` nếu thay câu hoặc clip. Không tự thay giọng nhân vật.

Kiểm tra tài nguyên và Unicode không thay thế việc nghe duyệt dấu/phát âm bằng tai; manifest giữ trạng thái này rõ ràng.

## Code và kiểm chứng

- `Map01Story`, `Map01Quest`: thứ tự điểm 1/2/3 và điều kiện rút lui.
- `Map01StoryDialogue`: camera theo người nói ở hội thoại trực tiếp; radio giữ phía Nam.
- `Map01ArmContact`: điểm tới tai và mặt phẳng khuỷu tay. Chỉnh tương đối theo trục actor, không thêm roll cổ tay.
- `Map01EnemyGrip`: mount theo clip `Enemy_Rifle_AimIdle` hiện có; tay trái dùng foregrip của `Map01Rifle`.
- `Map01EnemyController`: phát audio/tracer/muzzle flash cho viên đạn thật.
- `Map01RevisionTests`: catalog mới, chặn bỏ qua điểm 3, súng/âm thanh và camera Hùng.
- `RescueRadioIntroTests`: chạy hết/skip, giữ aim xuyên bàn giao, vũ khí, crouch và đạn.
- `Map01StoryVoiceSupplementTests`: thoại bổ sung, thứ tự trên thuyền và skip.

Ảnh và kết quả kiểm tra nằm trong `Logs/Map01Revision/`; log được lưu riêng từng lượt vì Unity thay domain khi vào/ra Play Mode.
Không sửa scene, địa hình hoặc script Map 2 trong lần này. Không đổi model/rig và không phát sinh asset trả phí ngoài thoại đã duyệt.


## Kết quả kiểm chứng

12/12 kiểm tra có mục tiêu đã qua:

| Nhóm | Qua |
|---|---:|
| Cue mới, marker/khóa điểm 3, tay/súng/âm thanh, camera Hùng | 4/4 |
| Bộ đàm chạy hết và skip; giữ aim/crouch/dao/đạn | 2/2 |
| Thoại bổ sung, thứ tự trên thuyền, skip boarding | 3/3 |
| Chuyển checkpoint hoàn thành Map 1 sang Map 2, thuyền hiện và Hùng không cầm súng | 1/1 |
| Tương tác điểm 1/2, save/load không lặp vật tư, xác nhận chỉ huy sống ở điểm 3 | 1/1 |
| 22 cue chính: caption, giọng trực tiếp/bộ đàm, bản sạch và thời lượng | 1/1 |

Đã mở xem trực tiếp các ảnh tư thế đứng/đi/ngắm/bắn, Nam dùng đàm ở hai góc, Hùng nói và thuyền trong Map 2. Số đo socket lòng bàn tay–foregrip dưới 3,5 cm ở cả bốn tư thế; số đo này không thay thế kiểm tra mesh bằng mắt. Không chạy toàn bộ bộ test cũ của repository. Phát âm từng câu vẫn cần nghe duyệt bằng tai; không đánh dấu đã duyệt chỉ dựa vào test tài nguyên.

Báo cáo máy đọc: `Logs/Map01Revision/validation-report.json`.


Cập nhật chữ Rõ: thêm một lần thu có ngữ cảnh, người dùng nghe duyệt đúng dấu rồi mới thay clip dùng chung ở ba cảnh. Giữ GUID f2a32d89d00704442bf9cfda8b8e2cf5, clip 0,38 giây, 48 kHz mono. Bản cũ được giữ trong Retakes/Ro20261015. Kiểm tra 22 cue chính sau khi thay: 1/1 test qua. Các test gameplay trước đó không được tính là đã chạy lại trong lần chỉ thay audio này. Tổng chi phí 23/23 credits.
