using OneDragon.Core.Abstractions.Geometry;

namespace ZzzOd.GameLogic.Application.TrigramsCollection;

/// <summary>
/// 卦象集录 OCR 匹配结果。
/// </summary>
public sealed record TrigramOcrMatch(string Word, Point? Center = null);
