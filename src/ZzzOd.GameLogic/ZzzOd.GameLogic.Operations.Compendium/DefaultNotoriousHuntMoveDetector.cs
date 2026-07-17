using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Yolo;
using OpenCvSharp;
using ZzzOd.GameLogic.Application.HollowZero.LostVoid;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations.Compendium;

/// <summary>
/// 默认恶名狩猎移动检测器。
/// </summary>
public sealed class DefaultNotoriousHuntMoveDetector : INotoriousHuntMoveDetector, IDisposable
{
	private const string DistanceLabel = "0001-距离";

	private readonly ZContext _context;

	private bool _initialized;

	/// <summary>底层 YOLO 检测器。</summary>
	public YoloDetector CoreDetector => SharedDetector.CoreDetector;

	private LostVoidDetector SharedDetector => _context.LostVoid.Detector ?? throw new InvalidOperationException("迷失之地检测模型尚未初始化。");

	/// <summary>
	/// 初始化默认距离提示检测器。
	/// </summary>
	public DefaultNotoriousHuntMoveDetector(ZContext context)
	{
		_context = context;
	}

	/// <inheritdoc />
	public void Initialize()
	{
		if (!_initialized)
		{
			_context.LostVoid.InitLostVoidDetectorModel();
			_initialized = SharedDetector.InitModel();
		}
	}

	/// <inheritdoc />
	public NotoriousHuntDistanceHint? DetectDistanceHint(Mat screen)
	{
		ArgumentNullException.ThrowIfNull(screen, "screen");
		if (!_initialized)
		{
			Initialize();
		}
		YoloDetector coreDetector = CoreDetector;
		IReadOnlyList<string> labelList = new string[] { "0001-距离" };
		YoloDetectObjectResult yoloDetectObjectResult = coreDetector.Run(screen, 0.6f, 0.5f, null, labelList).Results.FirstOrDefault();
		if (yoloDetectObjectResult == null)
		{
			return null;
		}
		return new NotoriousHuntDistanceHint(new OneDragon.Core.Abstractions.Geometry.Point(yoloDetectObjectResult.Center.X, yoloDetectObjectResult.Center.Y), -1.0);
	}

	/// <inheritdoc />
	public void Dispose()
	{
	}
}
