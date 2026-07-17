using OneDragon.Core.Abstractions.Geometry;

namespace ZzzOd.GameLogic.Application.Devtools.LargeMapRecorder;

/// <summary>
/// 小地图图标。
/// </summary>
public sealed record MiniMapIcon(string TemplateId, Point Position);
