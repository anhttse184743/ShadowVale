# C3 audio và thoại Hùng — 18/10/2026

## Trạng thái

Khoa đã fast-forward từ `e5ead218` lên `origin/dev` tại `5bf8d639`. Không xung đột, không tạo commit mới, chưa push. Đối chiếu SHA-256 trước/sau: 102 file tracked đang sửa được giữ nguyên. Không stash hoặc khôi phục file dở. Thư mục `SourceArt/Map1_ArtRefresh_20261016/` có sẵn không bị xử lý.

Lượt test trước đồng bộ đã dừng để cập nhật theo yêu cầu, không tính là một lượt đạt. Kết quả mới nhất: 44/44 bài đạt sau khi sửa setup/input của các bài lỗi và chạy lại có mục tiêu; không có lượt đầy đủ thứ hai. Xem `TEST_RESULTS.md` và các XML giữ nguyên bên cạnh.

## Nội dung tích hợp

- 16 câu đã được người dùng nghe duyệt; giữ Tom/Chỉ huy, Brodie/Nam, Clint/Hùng, Martin/Lính.
- Hùng bị bắt khi quay về báo cáo sau trinh sát hàng ở cầu gỗ. Không còn nói mình đang mang hàng hoặc đã bàn giao hàng ngay khi về hầm.
- Hội thoại E: Nam báo cứu Hùng → chỉ huy hỏi → Hùng báo → chỉ huy giao thu hồi thuốc → Nam đáp Rõ bản đã duyệt.
- Thoại điểm danh/báo động C3 và chỉ thị Mosin có giọng. Bản bộ đàm chỉ phát file radio; Nam đáp tại chỗ dùng file clean.
- Hai lời Nam tháo dùi/cắt dây phát sau động tác, không cắt ngang thoại đang phát. Câu cuối đã duyệt: “Đường dây đã ngắt.”
- Kẻng mẫu C đã được người dùng nghe chọn, nhập thủ công theo xác nhận riêng. Một clip ba tiếng, vị trí 3D tại thanh sắt thật và giữ cờ chống phát lặp.

## File chính

- `Assets/_Project/Map01/Runtime/Map01C3Dialogue.cs`: caption chính xác của các cue C3 và chuỗi lệnh/đáp.
- `Map01C3Mission.cs`: điểm phát, chờ kết thúc thao tác, vị trí âm kẻng.
- `Map01OperationLines.cs`, `Map01Rescue.cs`, `Map01Quest.cs`: lời theo flow đã duyệt.
- `Map01OperationAudio.cs`: tham số spatialBlend tùy chọn, giữ .8 cho caller cũ.
- `SourceArt/Dialogue/C3_20261018/voice-manifest.json`: lời, giọng, task, hash và duyệt; `Previous/` lưu các bản có trước khi thay.
- `SourceArt/Audio/C3_Kieng_20261018/`: nguồn CC0, bản thu nghe công khai, script, hash và xác nhận nhập thủ công.

Catalog source/runtime được đồng bộ qua `DialogueVoiceSetup.Run` (menu Prepare Vietnamese dialogue). Menu có sẵn cũng cấu hình lại metadata bank WAV thành mono PCM 48 kHz/preload; không sửa PCM của các file thoại không liên quan. Không chỉnh scene Map 1, model/rig, clip animation, AI chiến đấu chỉ huy, phần đồ ném hoặc Map 2.

## Kiểm chứng tĩnh đã hoàn tất

Catalog 76 ID không trùng; source/runtime bằng nhau. Cả 76 file WAV được tham chiếu có header mono 48 kHz, dài hơn 0.3 giây. 16 cue mới/thu lại khớp caption và metadata. Đây là kiểm tra file, không thay cho kiểm tra Unity runtime hoặc nghe trong game.

## Phạm vi kiểm thử Unity

Các nhóm yêu cầu: DialogueVoiceTests, C3DialogueVoiceTests, Map01OperationsTests, C3MissionTests, Map01StoryVoiceSupplementTests. Chạy EditMode, có Play Mode cho kiểm tra tích hợp; không dùng -nographics, không chạy toàn bộ bộ test.

Các test thoại cũ được cập nhật theo flow E và ID hiện tại: không tự phát cutscene mở đầu, 76 cue/14 cue radio, ngưỡng clip ngắn >0.3 giây, ba lượt radio rời bến hiện có. Test mới kiểm tra cặp thoại C3 nối đúng thứ tự, Nam đáp sau lệnh Mosin, source/runtime khớp và cue có audio đúng kênh. Test kẻng kiểm tra nguồn 3D thật, rolloff và gọi lại không nhân đôi tiếng.

Chưa có video gameplay bàn giao hoặc kiểm tra nghe gần/xa do người dùng trực tiếp thực hiện trong scene. Người dùng đã duyệt audio ngoài game. Không coi việc file đã nhập là toàn bộ nghiệm thu đã xong.
