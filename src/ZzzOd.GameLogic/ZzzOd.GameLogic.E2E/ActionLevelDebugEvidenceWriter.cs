using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using OneDragon.Core.Utils;
using OpenCvSharp;

namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// 写入实机调试阶段的单动作 evidence。
/// </summary>
public static class ActionLevelDebugEvidenceWriter
{
	/// <summary>
	/// Action evidence 输出目录环境变量。
	/// </summary>
	public const string DirectoryEnvironmentVariable = "ZZZOD_ACTION_DEBUG_DIR";

	/// <summary>
	/// 当前应用 id 环境变量。
	/// </summary>
	public const string AppIdEnvironmentVariable = "ZZZOD_ACTION_DEBUG_APP_ID";

	/// <summary>
	/// 控制常规动作截图的环境变量。targeted 时仅保留业务代码显式请求的异常现场截图。
	/// </summary>
	public const string CaptureModeEnvironmentVariable = "ZZZOD_ACTION_DEBUG_CAPTURE_MODE";

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		Converters = { (JsonConverter)new JsonStringEnumConverter() }
	};

	/// <summary>
	/// 是否已启用 action evidence。
	/// </summary>
	public static bool IsEnabled => !string.IsNullOrWhiteSpace(GetEvidenceDirectory());

	/// <summary>
	/// 当前 action evidence 输出目录。
	/// </summary>
	public static string? GetEvidenceDirectory()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("ZZZOD_ACTION_DEBUG_DIR");
		return string.IsNullOrWhiteSpace(environmentVariable) ? null : Path.GetFullPath(environmentVariable);
	}

	/// <summary>
	/// 当前应用 id。
	/// </summary>
	public static string GetApplicationId(string fallback = "unknown")
	{
		string environmentVariable = Environment.GetEnvironmentVariable("ZZZOD_ACTION_DEBUG_APP_ID");
		return string.IsNullOrWhiteSpace(environmentVariable) ? fallback : environmentVariable.Trim();
	}

	/// <summary>
	/// 写入截图。
	/// </summary>
	public static string? WriteScreenshot(string fileStem, string label, Mat? image)
	{
		if (IsTargetedCaptureMode())
		{
			return null;
		}
		return WriteScreenshotCore(fileStem, label, image);
	}

	/// <summary>
	/// 写入已确认异常节点的现场截图，不受常规截图模式限制。
	/// </summary>
	public static string? WriteTargetedScreenshot(string fileStem, string label, Mat? image)
	{
		return WriteScreenshotCore(fileStem, label, image);
	}

	private static string? WriteScreenshotCore(string fileStem, string label, Mat? image)
	{
		if (image == null)
		{
			return null;
		}
		string evidenceDirectory = GetEvidenceDirectory();
		if (evidenceDirectory == null)
		{
			return null;
		}
		Directory.CreateDirectory(evidenceDirectory);
		string text = Path.Combine(evidenceDirectory, fileStem + "-" + label + ".png");
		CvImageUtils.SaveImage(image, text);
		return text;
	}

	private static bool IsTargetedCaptureMode()
	{
		return string.Equals(Environment.GetEnvironmentVariable("ZZZOD_ACTION_DEBUG_CAPTURE_MODE"), "targeted", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// 写入 JSON evidence。
	/// </summary>
	public static string? Write(ActionLevelDebugEvidence evidence)
	{
		string evidenceDirectory = GetEvidenceDirectory();
		if (evidenceDirectory == null)
		{
			return null;
		}
		Directory.CreateDirectory(evidenceDirectory);
		string text = Path.Combine(evidenceDirectory, evidence.FileStem + ".json");
		File.WriteAllText(text, JsonSerializer.Serialize(evidence, JsonOptions));
		return text;
	}

	/// <summary>
	/// 生成以本地时间开头的文件名前缀。
	/// </summary>
	public static string CreateFileStem(string suffix)
	{
		string text = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
		return text + "-" + suffix;
	}
}
