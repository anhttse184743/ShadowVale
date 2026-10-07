# Chỉnh bước đi và động tác chào

## Bước lên bến Map 2

Mở `Map02_Arrival.blend`, chọn rig Nam hoặc Hung. Trong Dope Sheet → Action Editor, chọn `Nam_Walk_Ashore`, `Hung_Walk_Ashore`, `Nam_Jetty_StepUp` hoặc `Hung_Jetty_StepUp`. Các action có hậu tố `_Static_Backup` là bản cũ bị đứng hình, chỉ giữ để đối chiếu.

Clip đi bộ dài 36 frame ở 30 fps. Bật Auto Key hoặc nhấn I để đặt key rotation cho xương đùi, cẳng chân và bàn chân trong Pose Mode. Kiểm tra cả góc bên và chính diện. Frame đầu và cuối phải khớp để loop không giật. Không đổi rest pose, tên xương hoặc scale rig.

Khi lấy mẫu lại từ `Source_Walk`, phải bật hiển thị collection và armature nguồn trong View Layer trước khi bake. Nếu rig nguồn bị ẩn, evaluated pose có thể đứng yên dù frame vẫn tăng. `rebuild_locomotion.py` đã mở hiển thị nguồn và kiểm tra độ thay đổi của xương chân sau bake. Chạy script này sẽ tạo lại bốn action đi bộ/lên bậc và ghi đè FBX, vì vậy hãy lưu bản sao trước khi sửa key thủ công.

Xuất FBX: chỉ chọn armature tương ứng, Selected Objects, Bake Animation, Sampling Rate 1, Simplify 0, tắt All Actions, NLA Strips và Add Leaf Bones. Giữ trục -Z Forward/Y Up và 30 fps. Dùng cùng scale xuất trong `rebuild_locomotion.py`, không đổi kích thước actor trong scene.

Sau khi xuất, chạy menu `ShadowVale → Cutscene → Prepare Map 2 arrival clips` (hàm `Map02ArrivalSetup.Run`) để cập nhật clip Resources. Kiểm tra loop bật cho `Walk_Ashore`, tắt cho `Jetty_StepUp`.

Root di chuyển theo địa hình do `Map02Arrival.ExitPassenger` điều khiển; tốc độ clip được tính từ quãng đường và chu kỳ bước. `PlantStairFeet` đặt chân theo các bậc thật; `PlantShoreFeet` giữ chân trong pha chịu lực trên mặt bến. Nếu chỉnh nhịp trong Blender, kiểm tra lại các pha tiếp xúc này khi Play, vì runtime có thể điều chỉnh vị trí chân sau animation.

## Chào ở đầu Map 1

Trong prefab `Assets/_Project/Map01/Resources/Cutscenes/Map01Opening.prefab`, component `Map01OpeningCutscene` có các thông số:

- `Salute Raise Seconds`: thời gian nâng tay trước câu trả lời.
- `Salute Lower Seconds`: thời gian hạ tay sau khi câu trả lời kết thúc.
- `Salute Hold Time`: thời điểm giữ trong clip mới, mặc định 0.7 giây.
- `Salute Lower Start`: thời điểm bắt đầu hạ trong clip mới, mặc định 0.9 giây.

Opening hiện dùng clip `Nam_Salute_Briefing` mới được dựng riêng trên rig Nam. Animation Salute gốc được giữ làm tham chiếu; không còn ép tay trong LateUpdate. Mở `SourceArt/Map01_Opening/Nam_Salute_Briefing.blend` để chỉnh cổ tay hoặc khuỷu tay trong Pose Mode. Xem hướng dẫn đầy đủ tại `SourceArt/Map01_Opening/README.md`. Dùng menu `Prepare Nam salute` để nhập lại clip; menu cũng căn độ duỗi/khép ngón trên avatar gameplay thật của Nam.

Kiểm tra bằng New Game để xem opening, và hoàn thành Map 1 để xem Map 2. Test Runner: `OpeningCutsceneTests`, `Map02ArrivalTests`. Xem cả chạy hết và giữ Esc một giây; kiểm tra aim, súng và input sau khi trả điều khiển.
