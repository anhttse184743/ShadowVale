# Kiểm chứng bản sửa độ nổi và chuyển động Hùng

Sàn thuyền local −0.15 m trước đây nằm dưới mặt nước Map 2 (0.045 m). Root mới 0.24 m, sàn lên 0.09 m, còn tối thiểu 0.07 m khi nghiêng. Vệt nước vẫn phát ở mặt kênh. Giữ nguyên bến và các bậc world cao 0.41/0.62/0.83/1.04/1.25 m; điều chỉnh lại điểm tiếp xúc chân theo root mới.

Cất chèo 4 giây: nhấc cán khỏi đầu gối, đưa qua lòng, đặt ngang mạn; tay trái thả trước rồi tay phải, pha dần về nghỉ tay. Đứng dậy 3 giây: clip mới từ tư thế ngồi đi thuyền đến đứng, có cúi người lấy đà và nâng hông, không đảo clip xoay/ngồi. Hùng đứng theo hướng ngồi, rồi bước và xoay hướng bậc. Nhóm lên bờ cách nhau 3 giây.

10 FBX, 28 xương gốc, bake 30 fps. File Blender đóng gói và script tạo/export cập nhật. Boat_Stand 90 frame, Oar_Stow 120 frame. Không thay scene Map 2 hoặc nhiệm vụ trước extraction.

Unity: map2-refine.xml 7/7 qua (5 Map 1, 2 Map 2). Sau hạ vệt nước về đúng mặt kênh: map2-refine-final.xml 2/2 qua. Sau tách xoay khỏi đứng: map2-stand-final.xml kiểm thử natural playback qua. Kiểm tra độ khô sàn thuyền, hull nằm trong mesh nước, cùng actor, HP/đạn/inventory, dry NavMesh spawn, skip và held aim. Đã xem trực tiếp các khung hình cận khi cất chèo, đứng dậy và bước lên bến.

Video map2-arrival-preview.mp4 dài 42 giây, ảnh camera Unity lấy mẫu trong kiểm thử và quy về 30 fps; không chứa audio/OnGUI. Menu Unity: ShadowVale → Cutscene → Preview Map 2 arrival.
