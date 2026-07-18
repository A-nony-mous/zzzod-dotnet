using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneDragon.Core.Runtime;
using OneDragon.Core.Yolo;
using ZzzOd.GameLogic.Const;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地检测器业务封装。
/// </summary>
public sealed class LostVoidDetector : IDisposable
{
	public const string ModelCategory = "lost_void_det";

	public const string ClassInteract = "0000-感叹号";

	public const string ClassDistance = "0001-距离";

	public const string ClassEntry = "xxxx-入口";

	public const double DefaultKeepResultSeconds = 2.0;

	private readonly OneDragonEnvironment _environment;

	public string ModelName { get; }

	public string BackupModelName { get; }

	public string ModelDownloadUrl { get; }

	public double KeepResultSeconds { get; }

	public bool UseGpu { get; }

	public bool IsShutdown { get; private set; }

	public YoloDetector CoreDetector { get; }

	public string ModelParentDirectoryPath => Path.Combine(GameConst.GetModelPath(_environment), "lost_void_det");

	public string ModelDirectoryPath => Path.Combine(ModelParentDirectoryPath, ModelName);

	public YoloDetectFrameResult? LastRunResult => CoreDetector.RunResultHistory.LastOrDefault();

	public LostVoidDetector(ZContext context)
	{
		_environment = context.Environment;
		ModelName = context.ModelConfig.LostVoidDet;
		BackupModelName = context.ModelConfig.LostVoidDetBackup;
		UseGpu = context.ModelConfig.LostVoidDetGpu;
		ModelDownloadUrl = "https://github.com/OneDragon-Anything/OneDragon-YOLO/releases/download/zzz_model";
		KeepResultSeconds = 2.0;
		CoreDetector = new YoloDetector(CreateCoreConfig())
		{
			EventBus = context.EventBus,
			OverlayDebugBus = context.OverlayDebugBus,
		};
	}

	public bool InitModel(string? proxyUrl = null, string? ghProxyUrl = null, bool skipIfExisted = true, Action<double, string>? progressCallback = null)
	{
		return CoreDetector.InitModel(proxyUrl, ghProxyUrl, skipIfExisted, progressCallback);
	}

	public (bool WithInteract, bool WithDistance, bool WithEntry) IsFrameWithAll(YoloDetectFrameResult? frameResult = null)
	{
		if (frameResult == null)
		{
			frameResult = LastRunResult;
		}
		if (frameResult == null)
		{
			return (WithInteract: false, WithDistance: false, WithEntry: false);
		}
		bool item = false;
		bool item2 = false;
		bool item3 = false;
		foreach (YoloDetectObjectResult result in frameResult.Results)
		{
			if (string.Equals(result.DetectClass.ClassName, "0000-感叹号", StringComparison.Ordinal))
			{
				item = true;
			}
			else if (string.Equals(result.DetectClass.ClassName, "0001-距离", StringComparison.Ordinal))
			{
				item2 = true;
			}
			else
			{
				item3 = true;
			}
		}
		return (WithInteract: item, WithDistance: item2, WithEntry: item3);
	}

	public bool IsFrameWith(YoloDetectFrameResult? frameResult, string targetType)
	{
		return IsFrameWith(frameResult, new string[] { targetType });
	}

	public bool IsFrameWith(YoloDetectFrameResult? frameResult = null, IReadOnlyCollection<string>? targetTypes = null)
	{
		if (frameResult == null)
		{
			frameResult = LastRunResult;
		}
		if (frameResult == null || targetTypes == null || targetTypes.Count == 0)
		{
			return false;
		}
		return frameResult.Results.Any((YoloDetectObjectResult result) => targetTypes.Contains(result.DetectClass.ClassName));
	}

	public YoloDetectObjectResult? GetResultByX(YoloDetectFrameResult? frameResult = null, string? targetType = null, bool byMaxX = true)
	{
		if (frameResult == null)
		{
			frameResult = LastRunResult;
		}
		if (frameResult == null || string.IsNullOrWhiteSpace(targetType))
		{
			return null;
		}
		return byMaxX ? frameResult.Results.Where((YoloDetectObjectResult result) => result.DetectClass.ClassName == targetType).MaxBy((YoloDetectObjectResult result) => result.Center.X) : frameResult.Results.Where((YoloDetectObjectResult result) => result.DetectClass.ClassName == targetType).MinBy((YoloDetectObjectResult result) => result.Center.X);
	}

	public void Shutdown()
	{
		IsShutdown = true;
		CoreDetector.Dispose();
	}

	public void Dispose()
	{
		Shutdown();
	}

	private YoloModelConfig CreateCoreConfig()
	{
		return new YoloModelConfig(_environment, ModelName, UseGpu, ModelDownloadUrl, BackupModelName, KeepResultSeconds, requireLabelsFile: true, ModelParentDirectoryPath);
	}
}
