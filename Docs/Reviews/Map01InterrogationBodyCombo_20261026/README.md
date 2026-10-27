# Map 1 — Gài súng và hai cú đấm bụng

Lính gài súng ở cạnh sau ba lô, thả tay rồi bước tới: đấm trái, thu tay, đấm phải, thu tay và lùi ra trước khi lấy lại súng. Dùng clip toàn thân có sẵn trong project, bản trái được mirror bằng AnimationClipSettings.mirror, giữ Avatar/skeleton hiện có. Không kéo cổ tay đuổi theo mục tiêu khi ra đòn. Căn riêng hướng đứng và độ cúi cho hai cú vào bụng trên, phía trên đai túi hông. Hùng co bụng và hạ đầu sau từng điểm va chạm, tay vẫn bị trói.

Súng chuyển từ socket tay sang khung ngực sau khi dây đeo nhận trọng lượng, căn theo hướng vai thực tế thay vì hướng gốc model. Khi hủy/skip trả nguyên parent, controller, Avatar và điều khiển. Giữ cảnh xử bắn: 10 viên, gục xong mới nói câu cuối yếu, rồi phản ứng Nam và thất bại.

Ba ca Unity liên quan và sai số điểm chạm trong Delivery_Tests.xml/report.json. Có kiểm tra các giai đoạn gài súng, trái, phải, lấy súng, pause/cancel/skip và câu cuối sau gục. Video là ảnh camera Unity theo thời gian thực tế, ghép audio theo log cue; không phải thu âm hệ thống và không chứa toàn bộ ambience. Bản chậm không có tiếng. Không tuyên bố đã kiểm thử lại toàn bộ game. Không tạo audio trả phí, không đổi giọng trong đợt combo.

Nguồn clip FBX gốc không sửa. Editor/InterrogationMotionSetup.cs tạo clip copy runtime. Không có Blender source mới cho diễn xuất này.

Tham khảo API mirror chính thức: https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/master/Editor/Mono/Animation/AnimationClipSettings.bindings.cs
