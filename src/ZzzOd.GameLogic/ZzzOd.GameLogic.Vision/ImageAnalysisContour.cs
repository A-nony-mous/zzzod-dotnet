using OpenCvSharp;

namespace ZzzOd.GameLogic.Vision;

/// <summary>
/// 图像分析得到的轮廓和绝对坐标。
/// </summary>
public sealed record ImageAnalysisContour(Point[] Points, Rect Rect, double Area);
