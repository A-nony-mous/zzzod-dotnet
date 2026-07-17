# IntelBoard real-game fixtures

These PNG files are immutable foreground `PrintWindow` captures copied from `D:\zzz-od-dotnet\real-game-debug`. Fixed-asset tests load the production `ScreenContext`, `TemplateMatcher`, `DefaultIntelBoardOperationServices`, and the real `v6-small` OCR model.

| File | Source | Source timestamp | SHA-256 | Regression use |
| --- | --- | --- | --- | --- |
| `intel-board-list.png` | `real-game-debug/20260708-140810-intel_board-intel-board-accept-commission-01-before.png` | 2026-07-08 14:08:10 +08:00 | `813A1B13F6BAC3A50F023306DBC64F0CA34E71C1A6BB03ECB378185197DDD27E` | Commission OCR ordering, production `Star` no-match path, and selected commission click. |
| `intel-board-accept.png` | `real-game-debug/20260709-111101-intel_board-intel-board-accept-commission-before.png` | 2026-07-09 11:11:01 +08:00 | `9960C04A348B8F9ABC750918FD86892AE235ADEB5B1AE4F87611628F9940F1ED` | Production OCR and click for `接取委托`. |
| `intel-board-running.png` | `real-game-debug/20260709-111108-intel_board-intel-board-accept-commission-before.png` | 2026-07-09 11:11:08 +08:00 | `B9D29016D80484AE04792F33DB9B9329D8B859E4C2F4BCD34F6992F5CE6F3BDA` | Production OCR priority for `前往` while a commission is running. |
| `intel-board-accept-failed.png` | `real-game-debug/20260708-144018-intel_board-intel-board-accept-commission-01-after.png` | 2026-07-08 14:40:22 +08:00 | `401BC530528577356BD5A86195F03B01D740F494E2E1848EBFD577F25BAAED53` | Production detection of `接取失败` followed by OCR click on `确认`. |

The fixture contract forbids replacing OCR or template results with `FakeOcrMatcher`, scripted OCR text, or generated images. Missing `下一步` and settlement screenshots remain outside this fixture set.

Python `IntelBoardApp.find_commission()` and .NET `DefaultIntelBoardOperationServices.FindCommissionAsync()` both match the full screenshot with `intel_board/Star`, the template mask, `threshold=0.8`, and all matches enabled. `intel-board-list.png` contains no orange own-post `Star`, so its expected match count is zero. The fixed-asset test separately verifies that the production `raw.png` and `mask.png` load through `TemplateLoader` and match at the same threshold.
