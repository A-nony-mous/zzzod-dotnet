using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using OneDragon.Core.Runtime;

namespace ZzzOd.GameLogic.E2E;

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
		ArgumentNullException.ThrowIfNull(environment, "environment");
		ArgumentNullException.ThrowIfNull(profile, "profile");
		string path = profile.ResolveAssetsRoot(environment);
		string path2 = profile.ResolveConfigRoot(environment);
		string localPath = profile.ResolveInstanceConfigRoot(environment);
		string path3 = profile.ResolvePythonReferenceRoot(environment);
		string path4 = Path.Combine(path3, "assets");
		string path5 = Path.Combine(path3, "config");
		int num = 8;
		List<E2EResourceValidationItem> list = new List<E2EResourceValidationItem>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<E2EResourceValidationItem> span = CollectionsMarshal.AsSpan(list);
		span[0] = RequireDirectory("assets.models", "assets/models", Path.Combine(path, "models"), Path.Combine(path4, "models"));
		span[1] = RequireDirectory("assets.template", "assets/template", Path.Combine(path, "template"), Path.Combine(path4, "template"));
		span[2] = RequireDirectory("assets.screen_info", "assets/game_data/screen_info", Path.Combine(path, "game_data", "screen_info"), Path.Combine(path4, "game_data", "screen_info"));
		span[3] = RequireYamlDirectory("config.auto_battle", "自动战斗配置", Path.Combine(path2, "auto_battle"), Path.Combine(path5, "auto_battle"));
		span[4] = RequireYamlDirectory("config.dodge", "dodge 配置", Path.Combine(path2, "dodge"), Path.Combine(path5, "dodge"));
		span[5] = RequireYamlDirectory("config.lost_void", "LostVoid 配置", Path.Combine(path2, "lost_void_challenge"), Path.Combine(path5, "lost_void_challenge"));
		span[6] = RequireYamlDirectory("config.hollow_zero", "HollowZero 配置", Path.Combine(path2, "hollow_zero_challenge"), Path.Combine(path5, "hollow_zero_challenge"));
		span[7] = RequireYamlDirectory("config.instance", "实例级应用配置", localPath, Path.Combine(path5, profile.InstanceIndex.ToString("00")));
		List<E2EResourceValidationItem> items = list;
		return new E2EResourceValidationResult(items);
	}

	private static E2EResourceValidationItem RequireDirectory(string id, string displayName, string localPath, string pythonSourcePath)
	{
		return Directory.Exists(localPath) ? Present(id, displayName, localPath, pythonSourcePath, "目录存在。") : Missing(id, displayName, localPath, pythonSourcePath, "目录不存在。");
	}

	private static E2EResourceValidationItem RequireYamlDirectory(string id, string displayName, string localPath, string pythonSourcePath)
	{
		if (!Directory.Exists(localPath))
		{
			return Missing(id, displayName, localPath, pythonSourcePath, "目录不存在。");
		}
		return Directory.EnumerateFiles(localPath, "*.yml", SearchOption.TopDirectoryOnly).Any() ? Present(id, displayName, localPath, pythonSourcePath, "目录存在且包含 YAML 配置。") : Missing(id, displayName, localPath, pythonSourcePath, "目录内没有 YAML 配置。");
	}

	private static E2EResourceValidationItem Present(string id, string displayName, string localPath, string pythonSourcePath, string message)
	{
		return new E2EResourceValidationItem(id, displayName, localPath, pythonSourcePath, E2EResourceStatus.Present, message);
	}

	private static E2EResourceValidationItem Missing(string id, string displayName, string localPath, string pythonSourcePath, string message)
	{
		return new E2EResourceValidationItem(id, displayName, localPath, pythonSourcePath, E2EResourceStatus.Missing, message);
	}
}
