using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;

namespace ZzzOd.GameLogic.Application.BattleAssistant;

/// <summary>
/// 自动战斗配置 provider。
/// </summary>
public sealed class AutoBattleConfigProvider
{
	private readonly OneDragonEnvironment _environment;

	/// <summary>
	/// 初始化自动战斗配置 provider。
	/// </summary>
	public AutoBattleConfigProvider(OneDragonEnvironment environment)
	{
		_environment = environment;
	}

	/// <summary>
	/// 获取指定 CondOp 目录下的配置列表。
	/// </summary>
	public IReadOnlyList<ConfigItem> GetAutoBattleOpConfigList(string subDir)
	{
		string pathUnderWorkDir = _environment.GetPathUnderWorkDir("config", subDir);
		if (!Directory.Exists(pathUnderWorkDir))
		{
			return Array.Empty<ConfigItem>();
		}
		SortedSet<string> sortedSet = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string item in Directory.EnumerateFiles(pathUnderWorkDir, "*.yml", SearchOption.TopDirectoryOnly))
		{
			string fileName = Path.GetFileName(item);
			string text = TryGetAutoBattleTemplateName(fileName);
			if (!string.IsNullOrWhiteSpace(text))
			{
				sortedSet.Add(text);
			}
		}
		return sortedSet.Select((string name) => new ConfigItem(name, name)).ToArray();
	}

	/// <summary>
	/// 获取指定配置文件路径。
	/// </summary>
	public string GetAutoBattleConfigFilePath(string subDir, string templateName)
	{
		return _environment.GetPathUnderWorkDir("config", subDir, templateName + ".yml");
	}

	/// <summary>
	/// 按 BaselineParity unlink(missing_ok=True) 语义删除普通配置文件。
	/// </summary>
	public void DeleteAutoBattleOpConfig(string subDir, string templateName)
	{
		if ((!(subDir == "auto_battle") && !(subDir == "dodge")) || 1 == 0)
		{
			throw new ArgumentException("只允许删除自动战斗或闪避配置。", "subDir");
		}
		if (string.IsNullOrWhiteSpace(templateName) || !string.Equals(Path.GetFileName(templateName), templateName, StringComparison.Ordinal) || templateName.IndexOfAny(new char[2]
		{
			Path.DirectorySeparatorChar,
			Path.AltDirectorySeparatorChar
		}) >= 0)
		{
			throw new ArgumentException("配置名称无效。", "templateName");
		}
		string fullPath = Path.GetFullPath(_environment.GetPathUnderWorkDir("config", subDir));
		string fullPath2 = Path.GetFullPath(GetAutoBattleConfigFilePath(subDir, templateName));
		string value = (fullPath.EndsWith(Path.DirectorySeparatorChar) ? fullPath : (fullPath + Path.DirectorySeparatorChar));
		if (!fullPath2.StartsWith(value, StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException("配置路径超出允许目录。", "templateName");
		}
		if (File.Exists(fullPath2))
		{
			File.Delete(fullPath2);
		}
	}

	private static string? TryGetAutoBattleTemplateName(string fileName)
	{
		if (fileName.EndsWith(".sample.yml", StringComparison.OrdinalIgnoreCase))
		{
			string text = fileName;
			int length = ".sample.yml".Length;
			return text.Substring(0, text.Length - length);
		}
		if (fileName.EndsWith(".merged.yml", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		object result;
		if (!fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
		{
			result = null;
		}
		else
		{
			string text = fileName;
			int length = ".yml".Length;
			result = text.Substring(0, text.Length - length);
		}
		return (string?)result;
	}
}
