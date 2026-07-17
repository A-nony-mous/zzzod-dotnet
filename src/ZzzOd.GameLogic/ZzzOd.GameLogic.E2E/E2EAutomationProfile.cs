using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// 实机 E2E 自动化配置。
/// </summary>
public sealed class E2EAutomationProfile
{
	/// <summary>
	/// 默认 BaselineParity 参考仓路径。
	/// </summary>
	public const string DefaultPythonReferenceRoot = "C:\\Users\\Anonymous\\IdeaProjects\\ZenlessZoneZero-OneDragon";

	/// <summary>
	/// 默认复制资产目录。
	/// </summary>
	public const string DefaultAssetsRoot = "assets";

	/// <summary>
	/// 默认复制配置目录。
	/// </summary>
	public const string DefaultConfigRoot = "config";

	/// <summary>
	/// 默认截图方式。
	/// </summary>
	public const string DefaultScreenshotMethod = "auto";

	/// <summary>
	/// 默认输入模式。
	/// </summary>
	public const string DefaultInputMode = "keyboard";

	/// <summary>
	/// 默认 evidence 输出目录。
	/// </summary>
	public const string DefaultEvidenceOutputDirectory = "evidence\\e2e";

	private List<string> _applicationIds = new List<string>();

	/// <summary>
	/// 是否显式启用实机 E2E。
	/// </summary>
	[YamlMember(Alias = "enabled", ApplyNamingConventions = false)]
	public bool Enabled { get; set; }

	/// <summary>
	/// BaselineParity 参考仓根目录。
	/// </summary>
	[YamlMember(Alias = "python_reference_root", ApplyNamingConventions = false)]
	public string PythonReferenceRoot { get; set; } = "C:\\Users\\Anonymous\\IdeaProjects\\ZenlessZoneZero-OneDragon";

	/// <summary>
	/// 工作区内复制后的资产目录。
	/// </summary>
	[YamlMember(Alias = "assets_root", ApplyNamingConventions = false)]
	public string AssetsRoot { get; set; } = "assets";

	/// <summary>
	/// 工作区内复制后的配置目录。
	/// </summary>
	[YamlMember(Alias = "config_root", ApplyNamingConventions = false)]
	public string ConfigRoot { get; set; } = "config";

	/// <summary>
	/// 配置实例编号。
	/// </summary>
	[YamlMember(Alias = "instance_index", ApplyNamingConventions = false)]
	public int InstanceIndex { get; set; }

	/// <summary>
	/// 截图方式。
	/// </summary>
	[YamlMember(Alias = "screenshot_method", ApplyNamingConventions = false)]
	public string ScreenshotMethod { get; set; } = "auto";

	/// <summary>
	/// 输入模式。
	/// </summary>
	[YamlMember(Alias = "input_mode", ApplyNamingConventions = false)]
	public string InputMode { get; set; } = "keyboard";

	/// <summary>
	/// OCR profile。
	/// </summary>
	[YamlMember(Alias = "ocr_profile", ApplyNamingConventions = false)]
	public string? OcrProfile { get; set; }

	/// <summary>
	/// 模型 profile。
	/// </summary>
	[YamlMember(Alias = "model_profile", ApplyNamingConventions = false)]
	public string? ModelProfile { get; set; }

	/// <summary>
	/// Evidence 输出目录。
	/// </summary>
	[YamlMember(Alias = "evidence_output_directory", ApplyNamingConventions = false)]
	public string EvidenceOutputDirectory { get; set; } = "evidence\\e2e";

	/// <summary>
	/// 本次允许执行的应用 id 列表。
	/// </summary>
	[YamlMember(Alias = "application_ids", ApplyNamingConventions = false)]
	public List<string> ApplicationIds
	{
		get
		{
			return _applicationIds;
		}
		set
		{
			_applicationIds = (from item in value
				where !string.IsNullOrWhiteSpace(item)
				select item.Trim()).Distinct<string>(StringComparer.Ordinal).ToList();
		}
	}

	/// <summary>
	/// 从工作目录加载 E2E profile。
	/// </summary>
	/// <param name="environment">运行环境。</param>
	/// <returns>E2E profile。</returns>
	public static E2EAutomationProfile Load(OneDragonEnvironment environment)
	{
		ArgumentNullException.ThrowIfNull(environment, "environment");
		YamlConfig<E2EAutomationProfile> yamlConfig = new YamlConfig<E2EAutomationProfile>(environment, "e2e_profile");
		return yamlConfig.Current;
	}

	/// <summary>
	/// 解析 BaselineParity 参考仓绝对路径。
	/// </summary>
	/// <param name="environment">运行环境。</param>
	/// <returns>绝对路径。</returns>
	public string ResolvePythonReferenceRoot(OneDragonEnvironment environment)
	{
		ArgumentNullException.ThrowIfNull(environment, "environment");
		return ResolvePath(environment.WorkDirectory, PythonReferenceRoot);
	}

	/// <summary>
	/// 解析资产目录绝对路径。
	/// </summary>
	/// <param name="environment">运行环境。</param>
	/// <returns>绝对路径。</returns>
	public string ResolveAssetsRoot(OneDragonEnvironment environment)
	{
		ArgumentNullException.ThrowIfNull(environment, "environment");
		return ResolvePath(environment.ResourceDirectory, AssetsRoot);
	}

	/// <summary>
	/// 解析配置目录绝对路径。
	/// </summary>
	/// <param name="environment">运行环境。</param>
	/// <returns>绝对路径。</returns>
	public string ResolveConfigRoot(OneDragonEnvironment environment)
	{
		ArgumentNullException.ThrowIfNull(environment, "environment");
		return ResolvePath(environment.WorkDirectory, ConfigRoot);
	}

	/// <summary>
	/// 解析实例级配置目录绝对路径。
	/// </summary>
	/// <param name="environment">运行环境。</param>
	/// <returns>绝对路径。</returns>
	public string ResolveInstanceConfigRoot(OneDragonEnvironment environment)
	{
		return Path.Combine(ResolveConfigRoot(environment), InstanceIndex.ToString("00"));
	}

	/// <summary>
	/// 解析 evidence 输出目录绝对路径。
	/// </summary>
	/// <param name="environment">运行环境。</param>
	/// <returns>绝对路径。</returns>
	public string ResolveEvidenceOutputDirectory(OneDragonEnvironment environment)
	{
		ArgumentNullException.ThrowIfNull(environment, "environment");
		return ResolvePath(environment.WorkDirectory, EvidenceOutputDirectory);
	}

	private static string ResolvePath(string baseDirectory, string path)
	{
		string text = (string.IsNullOrWhiteSpace(path) ? "." : path.Trim());
		return Path.GetFullPath(Path.IsPathRooted(text) ? text : Path.Combine(baseDirectory, text));
	}
}
