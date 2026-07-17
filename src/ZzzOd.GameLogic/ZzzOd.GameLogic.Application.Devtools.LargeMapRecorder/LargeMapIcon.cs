using OneDragon.Core.Abstractions.Geometry;

namespace ZzzOd.GameLogic.Application.Devtools.LargeMapRecorder;

/// <summary>
/// 大地图图标。
/// </summary>
public sealed record LargeMapIcon(string IconName, string TemplateId, Point LargeMapPosition, Point? TeleportPosition = null);
