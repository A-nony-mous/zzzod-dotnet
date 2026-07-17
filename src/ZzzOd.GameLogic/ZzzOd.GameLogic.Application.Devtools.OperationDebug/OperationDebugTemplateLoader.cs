using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OneDragon.Core.Operation;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.Devtools.OperationDebug;

/// <summary>
/// 指令调试模板加载器。
/// </summary>
public sealed class OperationDebugTemplateLoader
{
	private readonly OneDragonEnvironment _environment;

	private readonly IDeserializer _yamlDeserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();

	/// <summary>
	/// 初始化模板加载器。
	/// </summary>
	public OperationDebugTemplateLoader(OneDragonEnvironment environment)
	{
		_environment = environment;
	}

	/// <summary>
	/// 加载并展开 auto_battle_operation 模板。
	/// </summary>
	public IReadOnlyList<OperationDef> LoadOperations(string templateName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(templateName, "templateName");
		return ExpandOperationTemplate(templateName, new HashSet<string>());
	}

	private List<OperationDef> ExpandOperationTemplate(string templateName, HashSet<string> operationTemplates)
	{
		if (!operationTemplates.Add(templateName))
		{
			throw new InvalidOperationException("指令模板循环引用 " + templateName);
		}
		Dictionary<string, object> data = LoadYamlConfig(templateName);
		List<OperationDef> list = new List<OperationDef>();
		foreach (Dictionary<string, object> dictionary in GetDictionaryList(data, "operations"))
		{
			OperationDef operationDef = new OperationDef(dictionary);
			if (string.IsNullOrWhiteSpace(operationDef.OperationTemplate))
			{
				list.Add(operationDef);
			}
			else
			{
				list.AddRange(ExpandOperationTemplate(operationDef.OperationTemplate, operationTemplates));
			}
		}
		operationTemplates.Remove(templateName);
		return list;
	}

	private Dictionary<string, object?> LoadYamlConfig(string templateName)
	{
		string text = ResolveYamlPath(templateName);
		if (!File.Exists(text))
		{
			throw new FileNotFoundException("未找到指令模板 auto_battle_operation/" + templateName, text);
		}
		object value = _yamlDeserializer.Deserialize<object>(File.ReadAllText(text));
		return NormalizeDictionary(value);
	}

	private string ResolveYamlPath(string templateName)
	{
		string pathUnderWorkDir = _environment.GetPathUnderWorkDir("config", "auto_battle_operation");
		string text = Path.Combine(pathUnderWorkDir, templateName + ".yml");
		return File.Exists(text) ? text : Path.Combine(pathUnderWorkDir, templateName + ".sample.yml");
	}

	private static IReadOnlyList<Dictionary<string, object?>> GetDictionaryList(IReadOnlyDictionary<string, object?> data, string key)
	{
		if (!data.TryGetValue(key, out object value) || value is string || !(value is IEnumerable enumerable))
		{
			return Array.Empty<Dictionary<string, object>>();
		}
		List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
		foreach (object item in enumerable)
		{
			Dictionary<string, object> dictionary = NormalizeDictionary(item);
			if (dictionary.Count > 0)
			{
				list.Add(dictionary);
			}
		}
		return list;
	}

	private static Dictionary<string, object?> NormalizeDictionary(object? value)
	{
		if (value is IDictionary dictionary)
		{
			Dictionary<string, object> dictionary2 = new Dictionary<string, object>(StringComparer.Ordinal);
			foreach (DictionaryEntry item in dictionary)
			{
				string key = Convert.ToString(item.Key, CultureInfo.InvariantCulture) ?? string.Empty;
				dictionary2[key] = NormalizeValue(item.Value);
			}
			return dictionary2;
		}
		return new Dictionary<string, object>();
	}

	private static object? NormalizeValue(object? value)
	{
		if (value is IDictionary)
		{
			return NormalizeDictionary(value);
		}
		if (value is IEnumerable enumerable && !(enumerable is string))
		{
			List<object> list = new List<object>();
			foreach (object item in enumerable)
			{
				list.Add(NormalizeValue(item));
			}
			return list;
		}
		return value;
	}
}
