# Âm thanh Map 2 · Ngày 1 – Đêm 1 (bước 5 / P8, 25/10/2026)

Mọi nguồn là bản ghi thật **CC0 1.0** trên Freesound (bản xem trước HQ MP3 công khai, không cần đăng nhập), giấy phép kiểm tra trên trang từng âm ngày 25/10/2026. Không dùng âm tạo bằng AI. Câu `m2v3_echo_promise` cắt từ bản thu TTS đã duyệt `m2v3_promise_05`.

- Nguồn, trang, link tải, SHA-256: `sources.json`; tệp gốc giữ trong `source/`.
- Dựng lại: `blender -b --factory-startup -P build_audio.py` (audaspace + numpy của Blender). Kết quả và SHA-256: `built.json`.
- Unity: `ShadowVale.Map01.Editor.Map02AudioImport.Run` (không normalize; nền dài nén trong bộ nhớ).

| Freesound | Tác giả | Bản ghi | Dùng cho |
|---|---|---|---|
| [719107](https://freesound.org/people/OwennewO/sounds/719107/) | OwennewO | Duck Flapping Wings | duck.wav (vỗ cánh) |
| [418509](https://freesound.org/people/Mikes-MultiMedia/sounds/418509/) | Mikes-MultiMedia | single quack from a duck | duck.wav (tiếng kêu) |
| [178615](https://freesound.org/people/Montacue/sounds/178615/) | Montacue | The Rustle of a Bush | duck.wav (bụi lay) |
| [530127](https://freesound.org/people/FullMetalJedi/sounds/530127/) | FullMetalJedi | A Hundred Year Old Man's Cough.wav | cough.wav |
| [32655](https://freesound.org/people/greysound/sounds/32655/) | greysound | FrogsAndCrickets_ExcerptB_JMA_24Bit_48k.wav | n1_frogs_loop.wav |
| [532191](https://freesound.org/people/mcmikai/sounds/532191/) | mcmikai | Fire in the stove | n1_fire_loop.wav |
| [435508](https://freesound.org/people/BenjaminNelan/sounds/435508/) | BenjaminNelan | Rooster Crow 1 | d1_rooster.wav |
| [406938](https://freesound.org/people/kyles/sounds/406938/) | kyles | village crowd day active under mango trees children kids playing shouting close short Putti, Uganda, Africa 2016.wav | d1_children_loop.wav |
| [588308](https://freesound.org/people/Fenodyrie/sounds/588308/) | Fenodyrie | rowing boat rear wave.wav | d1_oars_loop.wav |
| [335891](https://freesound.org/people/Yoyodaman234/sounds/335891/) | Yoyodaman234 | Bamboo Creaking in Wind | d1_bamboo_loop.wav |
| [348595](https://freesound.org/people/kingsrow/sounds/348595/) | kingsrow | PlaneFlyoverDistant.wav | d1_plane_far.wav |
| [720133](https://freesound.org/people/JotrainG/sounds/720133/) | JotrainG | Wholesome Genuine Laugh (male) | hung_laugh.wav |
| [372072](https://freesound.org/people/merkahz/sounds/372072/) | merkahz | Sad-Huqin.wav 95bpm | hung_theme.wav |
| [339357](https://freesound.org/people/CarlosCarty/sounds/339357/) | CarlosCarty | DIZI FLUTE 02.wav | review_hung_theme_dizi.wav (bản thay thế để nghe chọn) |
| [648959](https://freesound.org/people/IENBA/sounds/648959/) | IENBA | Heavy Book | crate_place.wav |
| [742356](https://freesound.org/people/NoisyRedFox/sounds/742356/) | NoisyRedFox | SingleKnock_Wood | crate_wrong.wav |

## Phát trong game

- **Ngày 1** (phase 0): tre kẽo kẹt trong gió (nền), trẻ con gần nhà dân, mái chèo ở bến (đặt trong thế giới), gà gáy xa 45–110 s một lần, một tiếng máy bay rất xa ở phút thứ 8.
- **Đêm 1** (phase 1): ếch + dế (nền), lửa bếp nhà chị Lành (trong nhà, sau cửa).
- Không có nhạc chiến đấu ở phase 0–1.
- **"Lời hứa"**: tiếng cười Hùng sau cú đấm vai (43,1 s), nhạc chủ đề Hùng từ câu "Tôi hả…" (49,7 s) tới hết; dừng khi bỏ qua/tải lại.
- **Vịt** (N1_02): vỗ cánh + kêu, từ bụi bên phải. **Ông Sáu ho** trong buồng trước câu đầu ở D1_03.
- **Kho**: tiếng hòm đặt xuống khi đặt đúng, tiếng "cộc" khi bị từ chối.

## Chờ người dùng nghe duyệt

- Nhạc chủ đề: đặt sẵn bản huqin (`hung_theme.wav`, 17,6 s); bản sáo dizi để so sánh: `review_hung_theme_dizi.wav`.
- Câu nhớ lại `m2v3_echo_promise.wav` (trạng thái trong manifest: `derived_20261025_awaiting_listen`).
- Tiếng cười Hùng là giọng người khác (bản ghi CC0), không phải giọng TTS của Hùng.

| Tệp | Giây | Kênh | SHA-256 |
|---|---|---|---|
| `Assets/Resources/Map02DayOne/n1_frogs_loop.wav` | 60.0 | 2 | `95f18a0409c65d5f…` |
| `Assets/Resources/Map02DayOne/n1_fire_loop.wav` | 45.0 | 1 | `047fd0388c84323a…` |
| `Assets/Resources/Map02DayOne/d1_bamboo_loop.wav` | 75.0 | 2 | `b225f5278084d9a2…` |
| `Assets/Resources/Map02DayOne/d1_children_loop.wav` | 30.0 | 1 | `3d74bf342a969396…` |
| `Assets/Resources/Map02DayOne/d1_oars_loop.wav` | 30.0 | 1 | `cfbbe6c4d2a00c9a…` |
| `Assets/Resources/Map02DayOne/d1_rooster.wav` | 2.157 | 1 | `1a573c807a2fe265…` |
| `Assets/Resources/Map02DayOne/d1_plane_far.wav` | 84.284 | 1 | `92e684cd98018e99…` |
| `Assets/Resources/Map02DayOne/duck.wav` | 1.9 | 1 | `fc5e108aae080b06…` |
| `Assets/Resources/Map02DayOne/cough.wav` | 2.4 | 1 | `9a1c46cf75fcf059…` |
| `Assets/Resources/Map02DayOne/crate_place.wav` | 0.75 | 1 | `e8e5529e39b1a103…` |
| `Assets/Resources/Map02DayOne/crate_wrong.wav` | 0.203 | 1 | `14fca6d3d1b394b2…` |
| `Assets/Resources/Map02DayOne/hung_theme.wav` | 17.6 | 2 | `670214ce4d32f1ae…` |
| `SourceArt/Audio/Map02_DayOne_20261025/review_hung_theme_dizi.wav` | 20.2 | 2 | `cb6415da13b43122…` |
| `Assets/Resources/Map02DayOne/hung_laugh.wav` | 1.88 | 1 | `5890200e60e2149f…` |
| `Assets/Resources/Dialogue/Map02DayOneFree/m2v3_echo_promise.wav` | 4.65 | 1 | `a251547742bd6fb9…` |
