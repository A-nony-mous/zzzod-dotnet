using System;
using OneDragon.Core.Runtime;

namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// E2E profile evidence 快照。
/// </summary>
public sealed class E2EEvidenceProfileSnapshot
{
	public bool Enabled { get; set; }

	public string PythonReferenceRoot { get; set; } = string.Empty;

	public string AssetsRoot { get; set; } = string.Empty;

	public string ConfigRoot { get; set; } = string.Empty;

	public string InstanceConfigRoot { get; set; } = string.Empty;

	public int InstanceIndex { get; set; }

	public string ScreenshotMethod { get; set; } = string.Empty;

	public string InputMode { get; set; } = string.Empty;

	public string? OcrProfile { get; set; }

	public string? ModelProfile { get; set; }

	public string EvidenceOutputDirectory { get; set; } = string.Empty;

	/// <summary>
	/// 从 profile 创建快照。
	/// </summary>
	/// <param name="environment">运行环境。</param>
	/// <param name="profile">E2E profile。</param>
	/// <returns>profile 快照。</returns>
	public static E2EEvidenceProfileSnapshot From(OneDragonEnvironment environment, E2EAutomationProfile profile)
	{
		ArgumentNullException.ThrowIfNull(environment, "environment");
		ArgumentNullException.ThrowIfNull(profile, "profile");
		return new E2EEvidenceProfileSnapshot
		{
			Enabled = profile.Enabled,
			PythonReferenceRoot = profile.ResolvePythonReferenceRoot(environment),
			AssetsRoot = profile.ResolveAssetsRoot(environment),
			ConfigRoot = profile.ResolveConfigRoot(environment),
			InstanceConfigRoot = profile.ResolveInstanceConfigRoot(environment),
			InstanceIndex = profile.InstanceIndex,
			ScreenshotMethod = profile.ScreenshotMethod,
			InputMode = profile.InputMode,
			OcrProfile = profile.OcrProfile,
			ModelProfile = profile.ModelProfile,
			EvidenceOutputDirectory = profile.ResolveEvidenceOutputDirectory(environment)
		};
	}
}
