using System.Runtime.InteropServices;
using OneDragon.Core.Runtime;
using ZzzOd.AppHost.Resources;
using ZzzOd.GameLogic.E2E;

namespace ZzzOd.AppHost.E2E;

/// <summary>
/// E2E 资源定位与校验。
/// </summary>
public sealed class E2EResourceValidator
{
	/// <summary>
	/// 校验 profile 指向的实机 E2E 资源。
	/// </summary>
	/// <param name="environment">运行环境。</param>
	/// <param name="profile">E2E profile。</param>
	/// <returns>校验结果。</returns>
	public E2EResourceValidationResult Validate(OneDragonEnvironment environment, E2EAutomationProfile profile)
	{
		ArgumentNullException.ThrowIfNull(environment);
		ArgumentNullException.ThrowIfNull(profile);
		ZzzAssetManifestValidationResult manifestValidation = new ZzzAssetManifestValidator().Validate(
			environment.WorkDirectory,
			RuntimeInformation.RuntimeIdentifier);
		if (!manifestValidation.IsValid)
		{
			return AttachManifestMetadata(new E2EResourceValidationResult(
				manifestValidation.Issues.Select(issue => new E2EResourceValidationItem(
					$"manifest.{issue.Code}",
					"资源清单",
					issue.Path,
					string.Empty,
					E2EResourceStatus.Missing,
					$"{issue.Code}: {issue.Message}"))
				.ToArray()), manifestValidation);
		}

		string assetsRoot = profile.ResolveAssetsRoot(environment);
		string configRoot = profile.ResolveConfigRoot(environment);
		string instanceConfigRoot = profile.ResolveInstanceConfigRoot(environment);
		string pythonReferenceRoot = profile.ResolvePythonReferenceRoot(environment);
		string pythonAssetsRoot = Path.Combine(pythonReferenceRoot, "assets");
		string pythonConfigRoot = Path.Combine(pythonReferenceRoot, "config");
		return AttachManifestMetadata(new E2EResourceValidationResult(
		[
			RequireDirectory("assets.models", "assets/models", Path.Combine(assetsRoot, "models"), Path.Combine(pythonAssetsRoot, "models")),
			RequireDirectory("assets.template", "assets/template", Path.Combine(assetsRoot, "template"), Path.Combine(pythonAssetsRoot, "template")),
			RequireDirectory("assets.screen_info", "assets/game_data/screen_info", Path.Combine(assetsRoot, "game_data", "screen_info"), Path.Combine(pythonAssetsRoot, "game_data", "screen_info")),
			RequireYamlDirectory("config.auto_battle", "自动战斗配置", Path.Combine(configRoot, "auto_battle"), Path.Combine(pythonConfigRoot, "auto_battle")),
			RequireYamlDirectory("config.dodge", "dodge 配置", Path.Combine(configRoot, "dodge"), Path.Combine(pythonConfigRoot, "dodge")),
			RequireYamlDirectory("config.lost_void", "LostVoid 配置", Path.Combine(configRoot, "lost_void_challenge"), Path.Combine(pythonConfigRoot, "lost_void_challenge")),
			RequireYamlDirectory("config.hollow_zero", "HollowZero 配置", Path.Combine(configRoot, "hollow_zero_challenge"), Path.Combine(pythonConfigRoot, "hollow_zero_challenge")),
			RequireYamlDirectory("config.instance", "实例级应用配置", instanceConfigRoot, Path.Combine(pythonConfigRoot, profile.InstanceIndex.ToString("00"))),
		]), manifestValidation);
	}

	private static E2EResourceValidationResult AttachManifestMetadata(
		E2EResourceValidationResult result,
		ZzzAssetManifestValidationResult manifestValidation)
	{
		result.RunRoot = manifestValidation.RunRoot;
		result.ManifestSchemaVersion = manifestValidation.Manifest?.SchemaVersion;
		result.ManifestRid = manifestValidation.Manifest?.Rid ?? string.Empty;
		result.ManifestSourceSummary = manifestValidation.SourceSummary;
		return result;
	}

	private static E2EResourceValidationItem RequireDirectory(string id, string displayName, string localPath, string pythonSourcePath) =>
		Directory.Exists(localPath)
			? Present(id, displayName, localPath, pythonSourcePath, "目录存在。")
			: Missing(id, displayName, localPath, pythonSourcePath, "目录不存在。");

	private static E2EResourceValidationItem RequireYamlDirectory(string id, string displayName, string localPath, string pythonSourcePath)
	{
		if (!Directory.Exists(localPath))
		{
			return Missing(id, displayName, localPath, pythonSourcePath, "目录不存在。");
		}

		return Directory.EnumerateFiles(localPath, "*.yml", SearchOption.TopDirectoryOnly).Any()
			? Present(id, displayName, localPath, pythonSourcePath, "目录存在且包含 YAML 配置。")
			: Missing(id, displayName, localPath, pythonSourcePath, "目录内没有 YAML 配置。");
	}

	private static E2EResourceValidationItem Present(string id, string displayName, string localPath, string pythonSourcePath, string message) =>
		new(id, displayName, localPath, pythonSourcePath, E2EResourceStatus.Present, message);

	private static E2EResourceValidationItem Missing(string id, string displayName, string localPath, string pythonSourcePath, string message) =>
		new(id, displayName, localPath, pythonSourcePath, E2EResourceStatus.Missing, message);
}
