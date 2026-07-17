using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;

namespace ZzzOd.GameLogic.Application.BattleAssistant;

/// <summary>
/// 自动战斗指令模板配置。
/// </summary>
public sealed class OperationTemplateConfigProvider
{
	private readonly OneDragonEnvironment _environment;

	/// <summary>
	/// 初始化模板配置 provider。
	/// </summary>
	public OperationTemplateConfigProvider(OneDragonEnvironment environment)
	{
		_environment = environment;
	}

	/// <summary>
	/// 获取 auto_battle_operation 下的模板列表，支持子目录。
	/// </summary>
	public IReadOnlyList<ConfigItem> GetOperationTemplateConfigList()
	{
		string pathUnderWorkDir = _environment.GetPathUnderWorkDir("config", "auto_battle_operation");
		if (!Directory.Exists(pathUnderWorkDir))
		{
			return Array.Empty<ConfigItem>();
		}
		SortedSet<string> sortedSet = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string item in Directory.EnumerateFiles(pathUnderWorkDir, "*.yml", SearchOption.AllDirectories))
		{
			string text = TryGetOperationTemplateName(pathUnderWorkDir, item);
			if (!string.IsNullOrWhiteSpace(text))
			{
				sortedSet.Add(text);
			}
		}
		return sortedSet.Select((string name) => new ConfigItem(name, name)).ToArray();
	}

	private static string? TryGetOperationTemplateName(string root, string path)
	{
		string relativePath = Path.GetRelativePath(root, path);
		string text2;
		if (relativePath.EndsWith(".sample.yml", StringComparison.OrdinalIgnoreCase))
		{
			string text = relativePath;
			int length = ".sample.yml".Length;
			text2 = text.Substring(0, text.Length - length);
		}
		else
		{
			if (!relativePath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}
			string text = relativePath;
			int length = ".yml".Length;
			text2 = text.Substring(0, text.Length - length);
		}
		return text2.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
	}
}
