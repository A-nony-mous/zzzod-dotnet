using System.Collections.Generic;
using OneDragon.Core.Configuration;
using OneDragon.Core.Ocr;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Config;

/// <summary>
/// 绝区零业务模型配置。
/// </summary>
public sealed class ModelConfig
{
	private const string DefaultFlashClassifier = "yolov8n-640-flash-20250921";

	private const string BackupFlashClassifier = "yolov8n-640-flash-20250906";

	private const string DefaultHollowZeroEvent = "yolov8s-736-hollow-zero-event-0126";

	private const string BackupHollowZeroEvent = "yolov8s-736-hollow-zero-event-1130";

	private const string DefaultLostVoidDet = "yolov26n-736-lost-void-det-20260630";

	private const string BackupLostVoidDet = "yolov8n-736-lost-void-det-20250921";

	private string _flashClassifier = "yolov8n-640-flash-20250921";

	private string _hollowZeroEvent = "yolov8s-736-hollow-zero-event-0126";

	private string _lostVoidDet = "yolov26n-736-lost-void-det-20260630";

	[YamlMember(Alias = "ocr_profile", ApplyNamingConventions = false)]
	public string? OcrProfile { get; set; }

	public string? Ocr { get; set; }

	[YamlMember(Alias = "ocr_use_gpu", ApplyNamingConventions = false)]
	public bool OcrUseGpu { get; set; }

	[YamlMember(Alias = "flash_classifier", ApplyNamingConventions = false)]
	public string FlashClassifier
	{
		get
		{
			return _flashClassifier;
		}
		set
		{
			_flashClassifier = (string.IsNullOrWhiteSpace(value) ? "yolov8n-640-flash-20250921" : value.Trim());
		}
	}

	[YamlMember(Alias = "flash_classifier_gpu", ApplyNamingConventions = false)]
	public bool FlashClassifierGpu { get; set; }

	/// <summary>闪光分类器备用模型。</summary>
	public string FlashClassifierBackup => "yolov8n-640-flash-20250906";

	[YamlMember(Alias = "hollow_zero_event", ApplyNamingConventions = false)]
	public string HollowZeroEvent
	{
		get
		{
			return _hollowZeroEvent;
		}
		set
		{
			_hollowZeroEvent = (string.IsNullOrWhiteSpace(value) ? "yolov8s-736-hollow-zero-event-0126" : value.Trim());
		}
	}

	[YamlMember(Alias = "hollow_zero_event_gpu", ApplyNamingConventions = false)]
	public bool HollowZeroEventGpu { get; set; }

	/// <summary>空洞事件检测备用模型。</summary>
	public string HollowZeroEventBackup => "yolov8s-736-hollow-zero-event-1130";

	[YamlMember(Alias = "lost_void_det", ApplyNamingConventions = false)]
	public string LostVoidDet
	{
		get
		{
			return _lostVoidDet;
		}
		set
		{
			_lostVoidDet = (string.IsNullOrWhiteSpace(value) ? "yolov26n-736-lost-void-det-20260630" : value.Trim());
		}
	}

	[YamlMember(Alias = "lost_void_det_gpu", ApplyNamingConventions = false)]
	public bool LostVoidDetGpu { get; set; }

	/// <summary>迷失之地检测备用模型。</summary>
	public string LostVoidDetBackup => "yolov8n-736-lost-void-det-20250921";

	public bool UsingOldModel()
	{
		return FlashClassifier != "yolov8n-640-flash-20250921" || HollowZeroEvent != "yolov8s-736-hollow-zero-event-0126" || LostVoidDet != "yolov26n-736-lost-void-det-20260630";
	}

	public OcrModelResolution ResolveOcrProfile(OcrModelRegistry? registry = null)
	{
		string selection = (string.IsNullOrWhiteSpace(OcrProfile) ? Ocr : OcrProfile);
		return OcrModelResolver.Resolve(selection, registry);
	}

	public static IReadOnlyList<ConfigItem> GetOcrProfileOptions(OcrModelRegistry? registry = null)
	{
		return OneDragon.Core.Configuration.ModelConfig.GetOcrProfileOptions(registry);
	}
}
