# Cutscene cứu Hùng và phục kích trong bụi — Map 1

## Các file chính

- `Assets/_Project/Map01/Runtime/Map01Rescue.cs`: luật báo động, giới thiệu một lần, cửa hầm và snapshot tương thích.
- `Map01RescueCinematic.cs`: camera, thoại theo ID, quỳ trói/gục và đi vào hầm theo đường thật.
- `Map01BushTakedown.cs`: kiểm tra vị trí và vòng đời kéo–giằng co–kết liễu/thoát.
- `Map01BushTiming.cs`: ba nhịp đúng, cho phép một lỗi, lỗi thứ hai thì thoát.
- `Map01FoliageReveal.cs`: vật liệu transparent riêng trên các LOD của bụi/cỏ, cache theo ô, không đổi collider/tầm nhìn.
- `Map01CinematicPose.cs`: Playables dùng một đồng hồ, giải phóng graph và trả controller/transform/layer.
- `SourceArt/Map01_Rescue/Map01_Rescue_Cinematics.blend`: rig gốc, tham chiếu, action và guide chỉnh tay.
- `SourceArt/Map01_Rescue/author_rescue_cinematics.py`: tái tạo và bake 30 fps.
- `Assets/_Project/Art/Characters/Animations/RescueStory/`: FBX và các Avatar tham chiếu trung tính.
- `Assets/Resources/Rescue/Story/`: clip được Unity dùng lúc chạy.
- `Assets/Resources/Dialogue/VI/m1_rescue_*.wav`: thoại trực tiếp tiếng Việt, không lọc bộ đàm.

## Chỉnh thông số trong Unity

Mở prefab `Assets/Resources/Rescue/Map01RescueLayout.prefab`. Chỉnh trực tiếp các marker `guardPosts`, `patrolRoutes`, `shelterDoor`, `shelterRun` và `reportPoint` trong Inspector. Giữ marker trên NavMesh và lối đi hiện có; không đổi tên/SaveId của bốn lính.

| Tham số | Mặc định | Tác dụng |
|---|---:|---|
| `introRadius` | 28 m | Nam tiếp cận yên lặng thì mở cảnh giới thiệu lần đầu |
| `introSeconds` | 12 s | Thời lượng tối thiểu; chờ hết thoại nếu thoại dài hơn |
| `executionCameraSeconds` | 3 s | Camera tiến tới Hùng sau lời báo động |
| `executionAimSeconds` | 0,8 s | Nhịp ngắm trước một phát súng |
| `executionHoldSeconds` | 2 s | Giữ cảnh sau khi gục xong, rồi mới hiện thất bại |
| `shelterSeconds` | 8 s | Thời lượng tối thiểu; chờ người theo sau đi hết đường |
| `shelterFollowGap` | 1,4 m | Khoảng cách đi thành hàng |
| `shelterWalkSpeed` | 1,35 m/s | Đi bộ; pha bước chân chạy theo quãng đường đã đi |
| `overseerHostageRadius` | 1,25 m | Giám sát phải còn ở vị trí canh mới có thể xử bắn |

Các giá trị timing nằm trong `Map01BushTiming`: lượt 2 giây, cửa sổ 24%, nghỉ 0,35 giây, ba thành công/hai lỗi. Không chỉnh thành một lần click giữ vì sẽ mất kiểm tra nhấn mới.

## Chỉnh animation thủ công trong Blender

1. Mở `.blend`, chọn armature `Nam`, `Hung` hoặc `Enemy`. Không đổi rest pose, tên xương, số xương hay scale của rig.
2. Chuyển sang **Animation → Dope Sheet → Action Editor**, chọn action đúng tên. Mỗi cặp Nam–Enemy có tên cùng hậu tố: `Bush_GrabPull`, `Bush_StruggleLoop`, `Bush_Finish`, `Bush_Escape`.
3. Chỉnh **Pose Mode**. Bật Auto Key nếu cần, ưu tiên sửa khuỷu tay/cổ tay/đầu và vai trước; tránh kéo bàn tay quá tầm với.
4. Các empty `*_HandContact` và `*_FootContact` là guide tiếp xúc. IK đang **mute** để clip bake phát đúng. Có thể unmute để chỉnh, nhưng phải bake kết quả trước khi xuất; không để Unity phụ thuộc constraint Blender.
5. Khi chỉnh cặp, phát cả hai action tại cùng frame và kiểm tra hai bàn tay ôm giữ thân, khuỷu, cổ tay và chân. Lính phải còn sống trong `GrabPull` và `StruggleLoop`; chỉ gục trong `Finish`. `Escape` không được dùng clip chết.
6. Với Hùng, kiểm tra cổ tay luôn ở sau lưng từ lúc quỳ tới tư thế gục. Dây trói trong Unity bám hai cổ tay thật, không phải một vị trí cố định.
7. Chọn riêng armature cần xuất, export FBX **Selected Objects, Armature, Bake Animation, 30 fps, Simplify 0, Add Leaf Bones OFF, NLA Strips OFF, All Actions OFF**. Ưu tiên script `export_rescue_cinematics.py` để xuất bản chỉnh tay mà không tạo lại source. Script bake các constraint trước, đưa rig về gốc export ổn định rồi trả mọi action/constraint về trạng thái đang chỉnh; không lưu đè `.blend`. Không đưa offset bố trí cặp trong viewport vào gốc FBX. Khi xuất tay phải giữ nút `Armature`, trục Forward -Z, Up Y và scale gốc.
8. Trong Unity chạy menu **ShadowVale → Rescue → Import rescue cinematics and bush actions**. Menu nhập FBX bằng Avatar trung tính, cập nhật `.anim` mà không sửa model hoặc Avatar gốc.
9. Kiểm tra trên sàn bến và bụi thật ở góc cận. Đừng chỉ xem preview FBX: retarget, vũ khí và ground contact được áp dụng lúc chạy.

Chạy script `author_rescue_cinematics.py` sẽ **tạo lại** các action từ tham số trong script. Nếu đã chỉnh tay trong `.blend`, hãy lưu một bản riêng trước và không chạy script đè lên bản đó. Muốn tái tạo ổn định thì đưa thay đổi vào script.

## Điều khiển và lưu

- Trong bụi, ngồi + dao + vị trí sau/bên hông hợp lệ: icon dao/lá sáng, chuột trái kéo vào bụi.
- Nhả click ban đầu, sau đó bấm mới khi kim ở vùng sáng. Giữ chuột không tự thắng.
- Esc trong giằng co chỉ tạm dừng. Có nút tiếp tục hoặc về checkpoint; không skip thành công.
- Intro, xử bắn và vào hầm: giữ Esc một giây để tới cùng trạng thái cuối.
- Không lưu khi có cinematic/giằng co. Cờ đã xem intro được giữ qua retry; xác chết trong bụi có loại riêng trong snapshot.
- Cảnh vào hầm mở khi một người chạm cửa, nhưng chỉ bàn giao/trao thưởng khi cả hai đã vào hoặc skip.

## Phạm vi

Không sửa địa hình, model, skeleton gốc hoặc logic nhiệm vụ Map 2. Các clip gục quỳ và xác trong bụi tách khỏi clip chết đứng dùng khi hộ tống.

## Camera và bàn tay trong bụi

Cảnh giới thiệu bắt đầu gần bến (28 m): tra hỏi chiếu vào Hùng, máy quay chuyển về góc nhìn người chơi trong giây 5–6,4 rồi mới phát câu Nam thì thầm. Intro chờ hết thoại và tối thiểu 12 giây.

`Map01BushTakedown` chọn góc bên có đường nhìn rõ. Nam đứng hơi lệch sau vai trái lính, nắm cả hai bàn tay và vòng hai cánh tay ôm giữ thân để kéo vào bụi. Hai tay lính chống vào hai cẳng tay Nam. Không bịt miệng, không rút hoặc đâm dao trong nhánh này. Thắng mini game thì Nam ghì giữ, lính mất sức và gục; ghi nhận hạ gục ở giây 1,02 của đoạn kết thúc 2,4 giây. Dao chỉ hiện lại khi trả gameplay. Ám sát sau lưng thông thường vẫn giữ nguyên một nhát vào cổ, 2,2 giây.

Ngón tay Nam được bake thành nắm tay và nhập bằng mask generic cho toàn bộ 30 xương ngón; không sửa Avatar hay animation gameplay gốc. Khi chỉnh các clip nhánh bụi, giữ bàn tay khép và cổ tay gần thẳng; sửa vai/khuỷu để đạt tiếp xúc.
Trong source Blender, `solve()` giới hạn độ lệch cổ tay từ tư thế tham chiếu ở 25°. Nếu sửa điểm tiếp xúc, ưu tiên đổi vị trí vai, khuỷu và thân; không kéo bàn tay vượt tổng chiều dài cánh tay. Giữ cùng frame cho cả hai action và xem cả hai phía.

Bụi vẫn hiện: `minimumOpacity = 0,62`, vùng cục bộ `revealRadius = 0,85 m`; vào/ra 0,35/0,45 giây. Đây là làm mờ nhẹ, không phải xóa bụi. Trong ảnh QA cận, cỏ có thể được tạm tắt riêng để nhìn rõ khớp; video GameView giữ bụi thực tế.

Bộ nguồn giữ đầy đủ rig Nam trên nhánh Khoa (58 xương, gồm đủ 30 xương ngón tay), Hùng/lính (28 xương), bake 30 fps. Texture có trong Blender được pack cùng nguồn. Clip và Avatar gốc của gameplay không thay đổi; Avatar riêng cho cảnh bụi được khôi phục khi bàn giao.

## Unity CLI

CLI đã cài ở `C:\Users\dangk\AppData\Local\Unity\bin\unity.exe`. Mở CMD mới sau khi sửa PATH:

```bat
where unity
unity --version
unity open G:\game\ShadowVale
```

Nếu cửa sổ CMD đang mở còn dùng PATH cũ, chỉ trong cửa sổ đó:

```bat
set "PATH=C:\Users\dangk\AppData\Local\Unity\bin;%PATH%"
```

Bản Editor dự án dùng là `6000.3.24f1`, ở `D:\6000.3.24f1\Editor\Unity.exe`. Để chỉ rõ Editor khi cần: `unity open G:\game\ShadowVale --editor-path D:\6000.3.24f1\Editor\Unity.exe`.

Kết quả kiểm thử và video đặt trong `Verification_2026-10-14`. Video thoại dùng chính WAV đã nhập và thời điểm DialogueVoice phát; GameView được chụp theo thời gian thực, không thay đổi time scale.
## Chi tiết động tác giằng co

| Đoạn | Nam | Lính |
|---|---|---|
| Chộp/kéo (2,6 s) | Cất dao; nắm hai bàn tay, vòng tay dưới vai và ôm thân kéo vào | Hạ trọng tâm, hai tay chống vào cẳng tay Nam |
| Giằng co | Giữ hai điểm trên áo/thân, khuỷu gập tự nhiên; không chạm miệng | Chống kéo nhẹ bằng thân và hai tay |
| Thắng (2,4 s) | Ôm ghì rồi buông tay dần về hông, không đâm dao | Mất sức, gục sau mốc kết liễu 1,02 s; hai tay về thân khi ngã |
| Thua (1,8 s) | Buông giữ, lùi nửa bước, đưa tay về hoạt ảnh dao gameplay | Gỡ tay, bước tách ra, chuyển về cầm súng, còn sống và báo động sau bàn giao |

Các điểm ôm giữ lấy theo hai vai và bề mặt thân; tay chống trả bám cẳng tay Nam đã retarget. Không kéo dài xương hoặc dùng xoắn cổ tay để bù khoảng cách. Camera không quyết định điểm tiếp xúc. Xem mặt trước và hai bên cùng frame khi chỉnh.

`export_rescue_cinematics.py` đưa armature về gốc khi xuất rồi trả bố trí viewport; không đưa offset dựng cặp vào gốc FBX.

## Mesh tay cận và skinning

Mesh mới nằm trong `Assets/_Project/Art/Characters/Animations/RescueStory/Meshes/Nam_HugHands.fbx`. Hai mesh dùng lúc chạy là `Assets/Resources/Rescue/Story/Nam_Hug_LeftHand.asset` và `Nam_Hug_RightHand.asset`.

Bản Blender giữ mesh gốc, tạo bản riêng, hàn các đỉnh trùng trong 0,15 mm, tái tạo quad, subdivision một cấp và làm mượt nhẹ trọng số quanh khớp. UV và màu da được giữ. Bản Blender khoảng 30,8 nghìn đỉnh/tay; Unity tách thêm đỉnh ở đường UV, giới hạn 45 nghìn/tay. Chỉ bật renderer tay cận trong cảnh bụi, dùng lại bones và material hiện tại. Renderer/mesh gốc không đổi kích thước buffer; khi thoát, tắt bản cận và bật lại renderer gốc.

Tư thế ngón được bake vào clip; không dùng ép ngón mỗi frame trong gameplay. Ngón cái có trục đối diện các ngón còn lại, nên cần chỉnh riêng động tác đối ngón để khép lên nắm tay. Các điểm đầu ngón được căn theo bề mặt lòng bàn tay thật, không tăng góc đồng loạt tới khi xuyên mesh.

Muốn chỉnh animation: sửa đồng thời hai action Nam/Enemy trong Blender, giữ các ngón Nam khép trong chộp/kéo, giằng co và ghì gục; ở nhánh thoát, thả tay rồi chuyển về tư thế gameplay; export và chạy menu import rescue như trên. Muốn chỉnh topology: sửa `Hug_LeftHand` / `Hug_RightHand`, giữ UV, vertex groups và rig, export riêng hai mesh + armature ở tư thế trung tính vào FBX trên rồi chạy **ShadowVale → Rescue → Import refined hug hands only**. Menu căn mesh, trọng số và bind pose về đúng renderer Nam hiện có; không thay Avatar gốc.

## Hồi phục sau vùng thoát

Trong 0,75 giây đầu, hai người gỡ và buông giữ, bước tách ra trên NavMesh; không giữ các điểm ôm sau khi đã thả. Từ giây 0,75 đến 1,4, Playables chuyển mềm về `Nam_Knife_Sneak_Idle` và `Enemy_Rifle_AimIdle` đang dùng trong gameplay. Giữ tư thế bình thường tới giây 1,8 rồi bàn giao. Súng lính xuất hiện trong nhịp lấy lại súng; dao Nam trở lại khi tay đã về tư thế cầm dao.

Avatar gameplay Khoa có ánh xạ Humanoid cho ngón tay. `Nam_Bush_Avatar.asset` chỉ dùng trong tương tác bụi: thân vẫn Humanoid, ngón dùng curve khớp gốc đã bake. Sau thành công, thất bại hoặc retry, khôi phục Avatar gameplay, controller, layer, root motion, mesh, vũ khí và camera. Không chạy `NamModelSetup` để đổi Avatar/model toàn dự án.

Khi bắt đầu vùng thoát, director trả về Avatar gameplay trước khi chuyển mềm sang bản sao nguyên gốc `Nam_Knife_Sneak_Idle`; không đổi hệ trục hoặc ép khớp tay của clip gameplay. `Enemy_Bush_Recovery` giữ hoạt ảnh cầm súng thật. Khi chỉnh source, giữ đoạn cuối khớp pose gameplay và kiểm tra cả hai bên.

Tay trái giám sát tiếp tục bám foregrip bằng IK gameplay, nhưng góc quay thêm của cổ tay giới hạn 35° từ clip ngắm gốc. Không dùng xoay 90° cổ tay để bù khẩu súng. Khi blend kết thúc, bỏ hoàn toàn luồng Escape rồi phát riêng clip hồi phục; không giữ kênh generic ở mixer trọng số 0.


## Nhịp chộp và gục quỳ — bản sửa HumanMotion

- Chộp/kéo dài 2,6 giây: Nam dùng bản sao chuyển động ngồi đi hiện có để tiếp cận, nâng tay và ôm hai điểm trên thân. Lính giữ nguyên vị trí trong 1,2 giây đầu; sau khi có tiếp xúc, hai người mới cùng lùi vào bụi. Hai bàn tay Nam khép; không bịt miệng hoặc đâm dao trong nhánh này.
- `Map01GrappleFeet` chỉ hoạt động khi chộp/giằng co: luân phiên chân trụ và bước ngắn trên mặt đất thật, giữ chiều dài xương gốc. Không áp dụng vào gameplay hoặc Map 2.
- Hùng bị trói: lấy tư thế quỳ từ chính rig Hùng, mất trọng tâm rồi gục về trước. Không chép góc hông/thân từ rig lính; giữ hai cổ tay sau lưng và dây trói đến tư thế cuối.
- Chỉnh Blender: chọn action `Hung_Bound_Execution` hoặc đồng thời `Nam_Bush_GrabPull`/`Enemy_Bush_GrabPull`. Giữ nguyên rig, Avatar và 30 fps. Với GrabPull giữ đoạn bắt tiếp xúc trước frame 37, kéo từ frame 37 đến 79. Thay duration phải đồng bộ `PullSeconds`/`GripAt` và số frame trong `RescueStorySetup`.
- Có thể xuất riêng các clip bằng `--clips Hung_Bound_Execution Nam_Bush_GrabPull Enemy_Bush_GrabPull`, tránh nhập lại mesh tay hoặc các animation đã ổn.

Kiểm chứng mới nằm trong `Verification_2026-10-14/HumanMotion`: test XML, video và góc cận hai bên/toàn thân. Video dựng từ các frame playback Unity thật; audio thoại ghép theo cue WAV gốc, không phải thu âm loopback. Góc chẩn đoán trong thư mục ảnh tạm tắt foliage để thấy khớp; video GameView giữ foliage thật. Không mô tả các video này là một lượt chơi thủ công liên tục.
