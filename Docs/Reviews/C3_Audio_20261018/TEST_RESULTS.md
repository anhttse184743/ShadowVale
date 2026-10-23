# Kết quả kiểm thử — 18/10/2026

**44/44 bài có kết quả mới nhất đạt.** Lượt đầy đủ đầu: 40/44; chạy lại các bài lỗi sau khi sửa setup test. Không khẳng định đã chạy lại toàn bộ 44 bài lần thứ hai.

## Lịch sử chạy

| File XML | Đạt / tổng |
|---|---|
| Tests_AfterDev.xml | 40/44 |
| Tests_SetupFix.xml | 3/4 |
| Tests_SupplyInput.xml | 0/1 |
| Tests_SupplyHeldInput.xml | 0/1 |
| Tests_SupplyInputState.xml | 0/1 |
| Tests_SupplyDeviceState.xml | 0/1 |
| Tests_SupplyBothBuffers.xml | 1/1 |

Lượt trước đồng bộ dev bị dừng, không tính vào các kết quả trên.

## Nguyên nhân và xử lý

- Hai bài New Game giả định phải có DialogueVoice ngay khi chưa có ai nói; sửa kiểm tra null hợp lệ và mục tiêu E hiện tại.
- Bài radio dùng RestoreStage, API này cố ý bỏ qua radio khi tải save; fixture chuyển sang nhánh vào radio trực tiếp.
- Bài tiếp tế đặt Nam ở spawn cũ thay vì gần chỉ huy; đưa Nam tới đúng transform của chỉ huy sau cập nhật dev.
- Bài giữ ngắm dùng một sự kiện chuột giả, state bị mất ở ngữ cảnh Editor. Thêm bàn phím test khi thiếu và cập nhật toàn MouseState ở cả buffer Dynamic/Editor sau mỗi update. Không dùng API ghi riêng bitfield vì phiên Input System không hỗ trợ cách đó.
- Các lần thử fixture chưa đạt được giữ trong XML và JSON, gồm lần dùng API bitfield gây exception. Không bỏ assertion khôi phục ngắm, đạn, trang bị, phần thưởng; không sửa PlayerCombat để làm test qua.

## Từng bài, lấy kết quả mới nhất

| Bài kiểm tra | Kết quả | XML |
|---|---|---|
| `C3DialogueVoiceTests.EveryApprovedRetakeHasExactCaptionChannelAndImportedAudio` | Passed | Tests_AfterDev.xml |
| `C3DialogueVoiceTests.GateCheckInPlaysBothTransmittedVoicesOnce` | Passed | Tests_AfterDev.xml |
| `C3DialogueVoiceTests.MosinOrderFinishesBeforeLocalNamAcknowledgement` | Passed | Tests_AfterDev.xml |
| `C3DialogueVoiceTests.SourceAndRuntimeCatalogStayIdenticalAfterPrepare` | Passed | Tests_AfterDev.xml |
| `C3MissionTests.ACutLineMeansNoRunners` | Passed | Tests_AfterDev.xml |
| `C3MissionTests.ASilentPatrolAtTheCheckInPutsTheCampOnGuard` | Passed | Tests_AfterDev.xml |
| `C3MissionTests.NestASeesTheTowerPlatformBeyondTheMosinsReport` | Passed | Tests_AfterDev.xml |
| `C3MissionTests.NobodyChasesNamPastTheLeash` | Passed | Tests_AfterDev.xml |
| `C3MissionTests.TheGarrisonHoldsItsPostsAndTheCommanderIsHomeFromTheStart` | Passed | Tests_AfterDev.xml |
| `C3MissionTests.TheKiengWakesTheSleepersWhenTheCampIsAlarmed` | Passed | Tests_AfterDev.xml |
| `C3MissionTests.TheKnifeFromBehindWoundsTheCommanderAndNeverKillsHim` | Passed | Tests_AfterDev.xml |
| `C3MissionTests.TheSaveKeepsThePostAsItWas` | Passed | Tests_AfterDev.xml |
| `C3MissionTests.WithoutTheStrikerOneSleeperAnswersAndTheLineBringsRunners` | Passed | Tests_AfterDev.xml |
| `DialogueVoiceTests.BriefingAssetsRemainAlignedAndHungReportsTutorialCapture` | Passed | Tests_AfterDev.xml |
| `DialogueVoiceTests.EveryStoryLineHasVietnameseAudioAndOnlyRadioIsFiltered` | Passed | Tests_AfterDev.xml |
| `DialogueVoiceTests.NewGameWaitsForCommanderConversationWithoutLegacyOpening` | Passed | Tests_SetupFix.xml |
| `DialogueVoiceTests.PauseAndNewSpeakerDoNotOverlap` | Passed | Tests_AfterDev.xml |
| `DialogueVoiceTests.RadioCueDoesNotReplayOrSelectDryVoice` | Passed | Tests_AfterDev.xml |
| `DialogueVoiceTests.RadioReplyWaitsForOrderAndSkipStopsSpeech` | Passed | Tests_SetupFix.xml |
| `DialogueVoiceTests.SkippingDormantOpeningCannotAcceptRescueOrder` | Passed | Tests_SetupFix.xml |
| `Map01OperationsTests.AllOperationDialogueHasExactCaptionAndCorrectAudioChannel` | Passed | Tests_AfterDev.xml |
| `Map01OperationsTests.CircuitMigratesOldBoardWithoutAcceptingItAsSolved` | Passed | Tests_AfterDev.xml |
| `Map01OperationsTests.CircuitRoutesToDummyLoadAndRejectsBrokenSource` | Passed | Tests_AfterDev.xml |
| `Map01OperationsTests.EveryRadioCellCanRotateAndAllChangesSurviveRestore` | Passed | Tests_AfterDev.xml |
| `Map01OperationsTests.FailedLockCanRetryWithoutLosingCodeOrNotebook` | Passed | Tests_AfterDev.xml |
| `Map01OperationsTests.LockExplainsFeedbackInPlainLanguage(241,214,"1 số đúng vị trí.","2 số đúng nhưng sai vị trí.")` | Passed | Tests_AfterDev.xml |
| `Map01OperationsTests.LockExplainsFeedbackInPlainLanguage(241,241,"Đúng cả 3 số","Khóa đã mở.")` | Passed | Tests_AfterDev.xml |
| `Map01OperationsTests.LockExplainsFeedbackInPlainLanguage(241,355,"Không có số nào đúng.","Cả 3 số đều không thuộc mã.")` | Passed | Tests_AfterDev.xml |
| `Map01OperationsTests.LockScoresRepeatedDigitsWithoutDoubleCounting(111,122,1,0)` | Passed | Tests_AfterDev.xml |
| `Map01OperationsTests.LockScoresRepeatedDigitsWithoutDoubleCounting(112,121,1,2)` | Passed | Tests_AfterDev.xml |
| `Map01OperationsTests.LockScoresRepeatedDigitsWithoutDoubleCounting(123,450,0,0)` | Passed | Tests_AfterDev.xml |
| `Map01OperationsTests.LockScoresRepeatedDigitsWithoutDoubleCounting(241,214,1,2)` | Passed | Tests_AfterDev.xml |
| `Map01OperationsTests.LockScoresRepeatedDigitsWithoutDoubleCounting(555,555,3,0)` | Passed | Tests_AfterDev.xml |
| `Map01OperationsTests.OfficerNeedsDistantBinocularsAndRestoresCombatBeforeDiscovery` | Passed | Tests_AfterDev.xml |
| `Map01OperationsTests.OnlyOriginalSupplyKeeperDropsKeyEvenWhenClonedForExtraction` | Passed | Tests_AfterDev.xml |
| `Map01OperationsTests.PowerSabotageNeedsThreeAccurateNewPullsAndCanTimeout` | Passed | Tests_AfterDev.xml |
| `Map01OperationsTests.RadioClockAndRoundGateCannotBeSkipped` | Passed | Tests_AfterDev.xml |
| `Map01OperationsTests.RadioFailureCallsOnlyOneSavedSquadAndSuccessDoesNotExtract` | Passed | Tests_AfterDev.xml |
| `Map01OperationsTests.RandomRadioRoundsAreSolvableVariedAndRestoreExactly` | Passed | Tests_AfterDev.xml |
| `Map01OperationsTests.SupplyCodesVaryButRemainStableForSavedSeed` | Passed | Tests_AfterDev.xml |
| `Map01OperationsTests.SupplyRequiresKeyPuzzleReportAndPhysicalHandoff` | Passed | Tests_SupplyBothBuffers.xml |
| `Map01StoryVoiceSupplementTests.EightSupplementLinesHaveMatchingCleanAudioAndExistingCharacterVoices` | Passed | Tests_AfterDev.xml |
| `Map01StoryVoiceSupplementTests.RemainingGameplayAndBoatDialogueSpeakInOrderBeforeCompletion` | Passed | Tests_AfterDev.xml |
| `Map01StoryVoiceSupplementTests.SkippingBoardingStopsSupplementDialogueAndCompletesOnce` | Passed | Tests_AfterDev.xml |
