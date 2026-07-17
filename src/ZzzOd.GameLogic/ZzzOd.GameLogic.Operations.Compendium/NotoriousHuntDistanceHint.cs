using OneDragon.Core.Abstractions.Geometry;

namespace ZzzOd.GameLogic.Operations.Compendium;

/// <summary>
/// 恶名狩猎战斗前移动时识别到的距离提示。
/// </summary>
public sealed record NotoriousHuntDistanceHint(Point Position, double Distance);
