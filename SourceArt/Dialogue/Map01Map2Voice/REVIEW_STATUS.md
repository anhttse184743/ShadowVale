# Bộ giọng Claude — lưu để duyệt, chưa kích hoạt

Ngày 26/10/2026, Thịnh chọn **giữ bộ giọng dev phát trong Map 1**, lưu bộ Claude để nghe duyệt sau.

- `voice-manifest.json`: 56 ID câu gốc Nam/Hùng/chỉ huy của bản đề xuất. Catalog dùng bộ này tại 58 mục: thêm hai alias `m2v3_contact_02/04` dùng lại câu chỉ huy. Cả 58 mục đang dùng lại bản dev theo lựa chọn của Thịnh.
- `Audio/`: WAV và metadata gốc đã chuyển khỏi `Assets/Resources` để không đưa bộ chưa dùng vào build.
- `runtime-catalog-proposal.json`, `source-catalog-proposal.json`: bản catalog trước khi áp dụng lựa chọn, chỉ để đối chiếu; không phải catalog đang chạy.
- `ProposalTests/`: bản test của đề xuất; test active trong `Assets` dùng bộ dev.
- `generate.py`: nguồn tạo đã có của Claude, chưa chạy lại trong lượt này.

Không tự chạy script cài lại catalog hoặc đưa Audio trở vào Resources. Chỉ kích hoạt sau khi được duyệt giọng và kiểm lại các test thoại, kể cả cue mới của cảnh thẩm vấn dev.
