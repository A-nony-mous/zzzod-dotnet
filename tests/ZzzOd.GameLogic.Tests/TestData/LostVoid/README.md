# 迷失之地实机固定资产

这些 PNG 来自 `real-game-debug`，复制后作为不可变测试资产，仅用于固定截图回归。

| 文件 | 来源 | 原始时间 | SHA-256 | 回归用途 |
| --- | --- | --- | --- | --- |
| `lost_void-initial.png` | `real-game-debug/lost_void-initial.png` | 2026-07-14 01:54:02 +08:00 | `8BD1956AC14E6182112680CB9C830432006B759828806EA546C9663C48278101` | 识别普通大世界与进入零号空洞前置画面。 |
| `lost_void-before.png` | `real-game-debug/lost_void-before.png` | 2026-07-14 01:54:10 +08:00 | `62B307CE2FE8EB2F29938FF9681CD328164C1FCA763597F762CCC445D7C6A954` | 识别进入迷失之地前的普通大世界与入口导航状态。 |
| `lost_void-after.png` | `real-game-debug/lost_void-after.png` | 2026-07-14 01:58:55 +08:00 | `4626BEAC1FB0165426FD2FE53E54D9BB5479AA1EC33C8B6610157B97E40BBCC4` | 验证迷失之地大世界模板与真实 LostVoid YOLO 目标检测。 |

截图由前台实机 runner 于 2026-07-14 生成。测试必须加载生产 `ScreenContext`、`TemplateMatcher` 和 LostVoid 模型；使用 fake OCR 或 detector 代替识别结果会使这组固定资产证据失效。
