# Test manifest — 21/10/2026

Nguồn là NUnit XML thực tế. Mỗi fullname chỉ tính một lần, lấy lượt mới nhất trong thứ tự liệt kê. Các lượt tái hiện lỗi và kết quả trước merge không được cộng vào số đạt bản ghép.

## dev_preflight

18 ca riêng biệt: {'Passed': 18}.

| XML | Kết quả lượt chạy | Đạt | Lỗi | Bỏ qua |
|---|---|---:|---:|---:|
| dev-latest.xml | Failed(Child) | 17 | 1 | 0 |
| dev-balance-fixed.xml | Passed | 1 | 0 | 0 |

| Lớp test (kết quả mới nhất) | Đạt | Lỗi | Bỏ qua |
|---|---:|---:|---:|
| ShadowVale.Map01.Tests.ForestFlowTests | 1 | 0 | 0 |
| ShadowVale.Map01.Tests.LootRollerTests | 10 | 0 | 0 |
| ShadowVale.Map01.Tests.M16IntegrationTests | 5 | 0 | 0 |
| ShadowVale.Map01.Tests.ReloadMagazineTests | 2 | 0 | 0 |

## merged

113 ca riêng biệt: {'Passed': 113}.

| XML | Kết quả lượt chạy | Đạt | Lỗi | Bỏ qua |
|---|---|---:|---:|---:|
| merged-weapons-fixed.xml | Passed | 10 | 0 | 0 |
| merged-regression.xml | Passed | 92 | 0 | 0 |
| fresh-weapons-before.xml | Failed(Child) | 21 | 1 | 0 |
| map02-m16-fixed.xml | Passed | 13 | 0 | 0 |

| Lớp test (kết quả mới nhất) | Đạt | Lỗi | Bỏ qua |
|---|---:|---:|---:|
| ShadowVale.Map01.Tests.BodyDiscoveryFollowupTests | 7 | 0 | 0 |
| ShadowVale.Map01.Tests.C3BedAnimationTests | 4 | 0 | 0 |
| ShadowVale.Map01.Tests.C3MissionTests | 9 | 0 | 0 |
| ShadowVale.Map01.Tests.ForestFlowTests | 1 | 0 | 0 |
| ShadowVale.Map01.Tests.ForestHudTests | 1 | 0 | 0 |
| ShadowVale.Map01.Tests.ForestSaveSlotsTests | 8 | 0 | 0 |
| ShadowVale.Map01.Tests.HungVisualTests | 1 | 0 | 0 |
| ShadowVale.Map01.Tests.K54IntegrationTests | 1 | 0 | 0 |
| ShadowVale.Map01.Tests.LootRollerTests | 10 | 0 | 0 |
| ShadowVale.Map01.Tests.M16IntegrationTests | 5 | 0 | 0 |
| ShadowVale.Map01.Tests.M16MergeCompatibilityTests | 2 | 0 | 0 |
| ShadowVale.Map01.Tests.Map01OperationsTests | 18 | 0 | 0 |
| ShadowVale.Map01.Tests.Map01OperationsTests.LockExplainsFeedbackInPlainLanguage(241,214,"1 số đúng vị trí.","2 số đúng nhưng sai vị trí | 1 | 0 | 0 |
| ShadowVale.Map01.Tests.Map01OperationsTests.LockExplainsFeedbackInPlainLanguage(241,241,"Đúng cả 3 số","Khóa đã mở | 1 | 0 | 0 |
| ShadowVale.Map01.Tests.Map01OperationsTests.LockExplainsFeedbackInPlainLanguage(241,355,"Không có số nào đúng.","Cả 3 số đều không thuộc mã | 1 | 0 | 0 |
| ShadowVale.Map01.Tests.Map02M16SaveCompatibilityTests | 1 | 0 | 0 |
| ShadowVale.Map01.Tests.Map02ProgressTests | 7 | 0 | 0 |
| ShadowVale.Map01.Tests.Map02RuntimeTests | 1 | 0 | 0 |
| ShadowVale.Map01.Tests.Map02WorldStateTests | 4 | 0 | 0 |
| ShadowVale.Map01.Tests.MosinIntegrationTests | 3 | 0 | 0 |
| ShadowVale.Map01.Tests.MosinReloadTimingTests | 3 | 0 | 0 |
| ShadowVale.Map01.Tests.MosinStealthTests | 11 | 0 | 0 |
| ShadowVale.Map01.Tests.QuickRescueTests | 2 | 0 | 0 |
| ShadowVale.Map01.Tests.ReloadMagazineTests | 2 | 0 | 0 |
| ShadowVale.Map01.Tests.StoneSiteTests | 2 | 0 | 0 |
| ShadowVale.Map01.Tests.StoneThrowTests | 4 | 0 | 0 |
| ShadowVale.Map01.Tests.WeaponAnimationIntegrationTests | 3 | 0 | 0 |
