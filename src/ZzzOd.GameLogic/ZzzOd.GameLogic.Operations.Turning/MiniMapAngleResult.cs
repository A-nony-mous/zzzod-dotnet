namespace ZzzOd.GameLogic.Operations.Turning;

/// <summary>
/// 小地图朝向识别结果。
/// </summary>
public sealed record MiniMapAngleResult(bool PlayMaskFound, double? ViewAngle);
