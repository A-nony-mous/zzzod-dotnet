using System;
using System.IO;
using OneDragon.Core.Runtime;
using OneDragon.Core.Yolo;
using ZzzOd.GameLogic.Const;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Yolo;

/// <summary>
/// 空洞事件检测器业务封装。
/// </summary>
public sealed class HollowEventDetector
{
	public const string DefaultModelCategory = "hollow_zero_event";

	public const double DefaultKeepResultSeconds = 2.0;

	private readonly OneDragonEnvironment _environment;

	public string ModelName { get; }

	public string BackupModelName { get; }

	public string ModelCategory { get; }

	public string ModelDownloadUrl { get; }

	public double KeepResultSeconds { get; }

	public bool UseGpu { get; }

	public bool IsShutdown { get; private set; }

	public YoloDetector CoreDetector { get; }

	public string ModelParentDirectoryPath => Path.Combine(GameConst.GetModelPath(_environment), ModelCategory);

	public string ModelDirectoryPath => Path.Combine(ModelParentDirectoryPath, ModelName);

	public HollowEventDetector(ZContext context)
	{
		_environment = context.Environment;
		ModelName = context.ModelConfig.HollowZeroEvent;
		BackupModelName = context.ModelConfig.HollowZeroEventBackup;
		UseGpu = context.ModelConfig.HollowZeroEventGpu;
		ModelCategory = "hollow_zero_event";
		ModelDownloadUrl = "https://github.com/OneDragon-Anything/OneDragon-YOLO/releases/download/zzz_model";
		KeepResultSeconds = 2.0;
		CoreDetector = new YoloDetector(CreateCoreConfig())
		{
			EventBus = context.EventBus
		};
	}

	public bool InitModel(string? proxyUrl = null, string? ghProxyUrl = null, bool skipIfExisted = true, Action<double, string>? progressCallback = null)
	{
		return CoreDetector.InitModel(proxyUrl, ghProxyUrl, skipIfExisted, progressCallback);
	}

	public void Shutdown()
	{
		IsShutdown = true;
		CoreDetector.Dispose();
	}

	private YoloModelConfig CreateCoreConfig()
	{
		return new YoloModelConfig(_environment, ModelName, UseGpu, ModelDownloadUrl, BackupModelName, KeepResultSeconds, requireLabelsFile: true, ModelParentDirectoryPath);
	}
}
