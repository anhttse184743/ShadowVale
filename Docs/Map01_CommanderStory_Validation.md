# Kiểm chứng Map 1 — tuyến nhiệm vụ chỉ huy

Ngày: 15/10/2026. Unity 6000.3.24f1, project `G:\game\ShadowVale`.

Đã biên dịch và chạy các kiểm thử tích hợp được chọn, không chạy toàn bộ test suite lịch sử.

| Phạm vi | Đạt / Tổng | Log |
|---|---:|---|
| Luồng nhiệm vụ mới, save/load, camera Nam | 3/3 | `Logs/Map01Story/story-results.json` |
| Radio chạy hết và skip; aim/controller/vũ khí | 2/2 | `Logs/Map01Story/radio-results.json` |
| Chuyển checkpoint đã hoàn thành sang Map 2 | 1/1 | `Logs/Map01Story/map2-transition-results.json` |
| Nam vào hầm trước, thưởng đúng một lần | 1/1 | `Logs/Map01Story/shelter-results.json` |

Đã xem ảnh render trong địa hình thực: hội thoại trong hầm, vị trí tiếp tế, vị trí đài liên lạc, radio tại Nam, báo động tại Nam. Đã kiểm tra cả Game View có giao diện hội thoại, hiệu chỉnh góc nhìn tránh khóa lên trần hầm.

- New Game không tự chạy cảnh chào/giao nhiệm vụ cũ; người chơi E nói chuyện với chỉ huy và xác nhận riêng.
- C1 và C2 hoàn thành qua vật tương tác khi lính còn sống; lưu/tải không cộng lại vật tư hoặc nhân đôi boss.
- Giữ controller/Avatar, máu, đạn, trạng thái cúi và aim khi rời thoại/cinematic.
- Bến/camera/boarding Map 2 không có thay đổi source; kiểm tra chuyển cảnh đã đạt.
- Bảng trên ghi kiểm thử trước lượt thu âm mới. Đã bổ sung 22 lượt thoại có giọng; xem `Map01_CommanderVoices.md` và kết quả kiểm thử riêng. Nam nghe sạch và đầu đàm chỉ huy có lọc.
- Tutorial do thành viên khác đảm nhiệm; chưa tạo tutorial mới.

Ảnh và log kiểm thử ở `Logs/Map01Story`. Hướng dẫn chỉnh script/marker ở `Docs/Map01_CommanderStory.md`.

## Sau khi bổ sung lồng tiếng

Tám câu còn thiếu đã được gắn phát, tổng lượt mới 13 credits. Năm kiểm thử liên quan đạt 5/5; log `Logs/Map01RemainingVoice/voice-results.json`. Góc cận đầu game được chạy lại riêng: `Logs/Map01StoryVoice/close-camera-results.json`. Độ phủ hiện tại và giới hạn nghe duyệt phát âm ở `Map01_DialogueVoiceCoverage.md`.
