# Nam – preview nằm trườn

## Khôi phục file Blender lớn (không dùng LFS)

Hai file `Nam_Crawl_Mixamo_Review_v4.blend` và `.blend1` được lưu đầy đủ
trong `BlendParts/`, mỗi phần tối đa 50 MiB. Sau khi clone/pull/merge, chạy
từ thư mục gốc repository (cần Python 3):

```powershell
python SourceArt/Animation/NamCrawl/blend_parts.py restore
```

Script kiểm tra SHA-256 từng phần và file ghép, giữ nguyên file có sẵn nếu
đúng dữ liệu, và từ chối ghi đè file đã chỉnh sửa. Hai file gốc được ignore.
Sau khi chỉnh sửa file Blender, chạy `blend_parts.py split` với cùng đường
dẫn trên, rồi commit các phần và manifest đã thay đổi. Dùng `verify` để
kiểm tra có thể ghép lại đúng dữ liệu. Các phần vẫn là dữ liệu nhị phân;
nếu hai nhánh sửa cùng file Blender, cần chọn một phiên bản đầy đủ (manifest
và toàn bộ các phần) rồi ghép lại, không trộn các phần giữa hai phiên bản.

## Preview thay đạn khi nằm

### Bản Mixamo thật v2 (nguồn đầy đủ trước khi cắt động tác lên đạn)

`Nam_Prone_Reload_Mixamo_Review_v2.blend` dùng clip **Prone Reloading** mới tải
từ Mixamo, 193 khung/30 fps (6,4 giây). Tay theo quỹ đạo và góc cổ tay của
chuyển động gốc, chỉnh theo tỉ lệ Nam và tiếp xúc súng/băng đạn. Giữ chân nằm,
súng thấp thêm 5 cm; các ngón tay khép thành nắm chắc theo yêu cầu duyệt mới.
Bản dựng và GIF nằm riêng tại `Logs/AnimationPreview/NamCrawl/ReloadMixamoPreview/`.
`build_prone_reload_mixamo.py` dựng lại; `verify_prone_reload_mixamo.py` kiểm tra
phát từ file đã lưu. Chưa thay asset hoặc cơ chế reload đang chạy trong Unity.

### Bản dựng thử v1 (trước khi tải clip mới)

`Nam_Prone_Reload_Review_v1.blend`: bản duyệt 4,2 giây. Giữ nền Mixamo Prone
Idle đã tải, tham chiếu vai từ `Nam_Rifle_Reload.fbx` có sẵn; chuyển động tay và
băng đạn được chỉnh trong Blender. Chưa tải được clip Mixamo Prone Reloading
mới do quyền Browser báo bị tắt. Chưa thay cơ chế reload trong Unity.

Preview nằm trong `Logs/AnimationPreview/NamCrawl/ReloadPreview/`.
`build_prone_reload.py` dựng animation; `refine_reload_fingers.py` khép ngón tay;
`render_reload_saved.py` render từ file đã lưu; `inspect_reload_preview.py`
kiểm tra băng đạn, bàn tay và tiếp xúc mặt đất. Nam vẫn nằm suốt động tác.

## Bản dùng chuyển động Mixamo v4 — bản xem trước mới nhất

`Nam_Crawl_Mixamo_Review_v4.blend` có sáu scene `Mixamo unarmed`, `Mixamo rifle`,
`Mixamo aim`, `Mixamo knife`, `Mixamo grenade`, `Mixamo stone` và scene chuyển
`Mixamo transition`. Chọn scene, Numpad 0, Space để xem trong Blender.

Chuyển động lấy từ FBX Mixamo đã tải: **Prone Forward**, **Prone Idle**,
**Crawl Forward**. Source FBX/rig/action được giữ làm tham chiếu. Các biến thể
ghép chuyển động vào dáng tham chiếu của Nam, sửa tiếp xúc tay, nhịp nối vòng
và tiếp xúc mặt đất để phù hợp model/kit của dự án. Dáng giữ súng vẫn nằm ngang,
có đoạn nâng lên ngắm và hạ xuống. Không thay model hoặc bind/rest pose gốc.

Bàn tay trống nắm thành nắm đấm: cả hai tay khi không cầm đồ, tay trái khi
cầm dao/lựu đạn/đá. Giữ nguyên cổ tay, cánh tay và cách nắm vật ở tay còn lại.
Góc cổ tay trống được giữ cố định so với cẳng tay trong toàn bộ vòng trườn;
không lấy chuyển động gập/xoắn cổ tay từ Mixamo. Cánh tay vẫn trườn bình thường.
Giữ nhịp chân gốc Mixamo với biên độ mềm hơn, bỏ bản co–duỗi luân phiên tự dựng.
Giữ độ cao bàn chân và giảm dao động thân để tránh nâng toàn thân khi
chân vung xuyên xuống đất. Tư thế ngắm đứng yên vẫn giữ dáng đã duyệt.
`preview_fists.py` xuất hình cận bàn tay từ rig Nam để kiểm tra dáng ngón tay.

Preview: `Logs/AnimationPreview/NamCrawl/MixamoPreview/`.
FBX đã bake để review: `SourceArt/Animation/NamCrawl/MixamoDerivedFBX/`.
Các FBX này chưa được đưa vào Assets, controller hay điều khiển Unity.

```text
blender -b -t 4 --python SourceArt/Animation/NamCrawl/build_mixamo_preview.py
blender -b -t 4 --python SourceArt/Animation/NamCrawl/verify_mixamo.py
python SourceArt/Animation/NamCrawl/package_preview.py --mixamo
python SourceArt/Animation/NamCrawl/package_rifle_preview.py --mixamo
```

Nguồn và thiết lập tải ghi trong `MixamoSource/README.md`. Kiểm tra phát từ file
đã lưu và SHA-256 các source nằm trong `MixamoPreview/saved-mixamo-validation.json`.
Bản v3 phía dưới là bản dựng trung gian; v4 bổ sung chuyển động Mixamo và sửa
đường chuyển tư thế qua mặt đất.

## Bản dựng theo mốc góc tham chiếu v3

`Nam_Crawl_Prone_Reference_v3.blend` giữ các bản trước và có scene mặc định
`Crawl transition`. Đây là bản tạo dáng cho nhân vật hư cấu theo các khoảng
góc người dùng cung cấp; số đo là kiểm tra rig, không phải xác nhận tiêu chuẩn
huấn luyện thực tế.

`prone_reference_fit.json` lưu tham số tạo dáng. Chỉnh thân, vai và hai điểm
nắm đồng thời; giữ chiều dài xương, bind/rest pose và các model gốc.
Đầu vẫn được nâng, súng giữ vị trí tiến thêm tổng cộng 20 cm trên trục tiến
của studio; nhân vật dịch và chếch thân để khớp tư thế mới.

Render/kiểm tra/đóng gói:

```text
blender -b -t 4 --python SourceArt/Animation/NamCrawl/build_crawl.py -- --reference-fit
blender -b -t 4 --python SourceArt/Animation/NamCrawl/verify_blend.py -- --reference-fit
python SourceArt/Animation/NamCrawl/package_rifle_preview.py --reference-fit
```

Preview và kiểm tra số đo nằm trong `Logs/AnimationPreview/NamCrawl/ProneReference/`.
Chưa có binding hoặc chỉnh sửa gameplay Unity.

## Bản sửa tư thế súng v2

Mở `Nam_Crawl_Rifle_Review_v2.blend`. Scene mặc định `Crawl transition`
phát một vòng 4,8 giây: giữ súng nằm ngang trước cẳng tay → đưa súng lên vai
→ giữ tư thế nằm ngắm, chân duỗi ra sau → hạ súng trở lại.
Hai tay được giải tiếp xúc từ cùng một transform súng; tay trái trượt dọc
ốp súng trong lúc chuyển tư thế. Tư thế ngắm giữ chân yên, có nhịp thở nhẹ.
Theo phản hồi mới nhất, đầu được nâng rõ hơn qua cổ và lưng trên, súng ở tư thế ngắm được
đưa ra trước tổng cộng 20 cm qua hai lần chỉnh (trục tiến của súng), tay trái đỡ đúng phần ốp gỗ
dưới nòng. Vai được đưa ra trước và hạ qua khớp xương đòn để giảm khoảng
hở dưới khuỷu tay mà không kéo dài xương cánh tay hay sửa rest pose.

Preview mới: `Logs/AnimationPreview/NamCrawl/nam-rifle-rest-to-aim-v2.gif`.
Đây vẫn là bản duyệt Blender, chưa gắn vào gameplay.

Tạo lại riêng preview súng:

```text
blender -b -t 4 --python SourceArt/Animation/NamCrawl/build_crawl.py -- --rifle-review
blender -b -t 4 --python SourceArt/Animation/NamCrawl/verify_blend.py -- --rifle-review
python SourceArt/Animation/NamCrawl/package_rifle_preview.py
```

## Bộ preview ban đầu

Mở `Nam_Crawl_Review.blend` bằng Blender. Chọn một scene `Crawl unarmed`,
`Crawl rifle`, `Crawl aim`, `Crawl knife`, `Crawl grenade` hoặc `Crawl stone`.
Bấm Numpad 0 để nhìn qua camera, Space để phát. Mỗi vòng dài 2,4 giây,
trườn tại chỗ để xem dáng trước khi ghép với vận tốc gameplay.

Sáu action gốc `Nam_ProneCrawl_*` có keyframe trên rig Nam và được giữ trong
Action Editor. Các scene review có vũ khí/vật cầm gắn vào xương bàn tay phải.
Texture được đóng gói trong file. Các vật cầm dùng model nguồn của dự án;
viên đá là mesh phụ để kiểm tra tư thế.

Đây là bản nháp duyệt dáng, chưa phải animation đã nghiệm thu trong Unity.
Chưa thay controller, input, collider, animation hay nhiệm vụ hiện có của game.
Không sửa rest pose của model nguồn. Các động tác vào/ra tư thế nằm, bắn,
chém dao, ném lựu đạn/đá không nằm trong sáu vòng trườn này.

Preview tổng và GIF riêng: `Logs/AnimationPreview/NamCrawl/`.
`saved-playback-validation.json` kiểm tra sáu scene sau khi mở lại file Blender:
vòng chuyển động khép kín, chân có chuyển động, vật cầm giữ nguyên điểm gắn.

Tạo lại bằng Blender 5.2:

```text
blender -b -t 4 --python SourceArt/Animation/NamCrawl/build_crawl.py
blender -b -t 4 --python SourceArt/Animation/NamCrawl/verify_blend.py
python SourceArt/Animation/NamCrawl/package_preview.py
```
## Preview thay băng đạn khi nằm v3

`Nam_Prone_Reload_MagazineOnly_v3.blend` dùng đoạn đổi băng từ Mixamo,
bỏ đoạn thao tác khóa lên đạn và chuyển mềm về tư thế ngắm. Hai tay giữ
dáng nắm đấm đã duyệt. Preview cơ bản dài 2,4 giây. Take `reload` đã được
thêm vào resource dùng chung Map 1/Map 2 qua `export_prone_reload.py` và
`NamProneSetup`. Xem `Docs/Nam_Prone_Integration.md` để biết phạm vi gameplay.

Runtime lấy tiến độ từ `PlayerCombat.ReloadProgress`, không tạo bộ đếm
riêng cho tư thế nằm. Bộ đếm hiện có đã áp dụng `PlayerSkills.ReloadMultiplier`
theo cấp Shooting; đứng, ngồi và nằm phải kết thúc cùng tiến độ thay đạn thực tế.
2,4 giây chỉ là thời lượng preview cơ bản, không phải thời gian cố định khi chơi.

Tạo lại bằng `shorten_prone_reload.py`, sau đó chạy
`package_reload_preview.py --magazine-only`. GIF nằm trong
`Logs/AnimationPreview/NamCrawl/ReloadMagazineOnlyPreview/`.

