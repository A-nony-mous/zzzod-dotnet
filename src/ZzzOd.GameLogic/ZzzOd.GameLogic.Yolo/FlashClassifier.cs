using System;
using System.IO;
using OneDragon.Core.Runtime;
using OneDragon.Core.Yolo;
using ZzzOd.GameLogic.Const;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Yolo;

/// <summary>
/// 闪光分类器业务封装。
/// </summary>
public sealed class FlashClassifier
{
	public const string DefaultModelCategory = "flash_classifier";

	public const double DefaultKeepResultSeconds = 2.0;

	private readonly OneDragonEnvironment _environment;

	public string ModelName { get; }

	public string BackupModelName { get; }

	public string ModelCategory { get; }

	public string ModelDownloadUrl { get; }

	public double KeepResultSeconds { get; }

	public bool UseGpu { get; }

	public bool IsShutdown { get; private set; }

	public YoloClassifier CoreClassifier { get; }

	public string ModelParentDirectoryPath => Path.Combine(GameConst.GetModelPath(_environment), ModelCategory);

	public string ModelDirectoryPath => Path.Combine(ModelParentDirectoryPath, ModelName);

	public FlashClassifier(ZContext context)
	{
		_environment = context.Environment;
		ModelName = context.ModelConfig.FlashClassifier;
		BackupModelName = context.ModelConfig.FlashClassifierBackup;
		UseGpu = context.ModelConfig.FlashClassifierGpu;
		ModelCategory = "flash_classifier";
		ModelDownloadUrl = "https://github.com/OneDragon-Anything/OneDragon-YOLO/releases/download/zzz_model";
		KeepResultSeconds = 2.0;
		CoreClassifier = new YoloClassifier(CreateCoreConfig());
	}

	public bool InitModel(string? proxyUrl = null, string? ghProxyUrl = null, bool skipIfExisted = true, Action<double, string>? progressCallback = null)
	{
		return CoreClassifier.InitModel(proxyUrl, ghProxyUrl, skipIfExisted, progressCallback);
	}

	public void Shutdown()
	{
		IsShutdown = true;
		CoreClassifier.Dispose();
	}

	private YoloModelConfig CreateCoreConfig()
	{
		return new YoloModelConfig(_environment, ModelName, UseGpu, ModelDownloadUrl, BackupModelName, KeepResultSeconds, requireLabelsFile: false, ModelParentDirectoryPath);
	}
}
