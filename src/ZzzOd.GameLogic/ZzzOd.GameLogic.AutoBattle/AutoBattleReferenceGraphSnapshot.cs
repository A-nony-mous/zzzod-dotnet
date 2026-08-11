using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OneDragon.Core.Operation;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.AutoBattle;

/// <summary>
/// 自动战斗独立 YAML 引用图的已验证快照。
/// </summary>
internal sealed class AutoBattleReferenceGraphSnapshot
{
	public AutoBattleReferenceGraphSnapshot(
		IReadOnlyDictionary<string, object?> configuration,
		IReadOnlyList<AutoBattleCondOpScene> scenes,
		string configurationYamlPath,
		IReadOnlyList<string> loadedYamlPaths)
	{
		Configuration = configuration;
		Scenes = scenes;
		ConfigurationYamlPath = configurationYamlPath;
		LoadedYamlPaths = loadedYamlPaths;
		SourceFingerprint = AutoBattleOperator.GetSourceFingerprint(loadedYamlPaths);
	}

	public IReadOnlyDictionary<string, object?> Configuration { get; }

	public IReadOnlyList<AutoBattleCondOpScene> Scenes { get; }

	public string ConfigurationYamlPath { get; }

	public IReadOnlyList<string> LoadedYamlPaths { get; }

	public string SourceFingerprint { get; }
}

/// <summary>
/// 读取并展开自动战斗独立 YAML 引用图。
/// </summary>
internal sealed class AutoBattleReferenceGraphLoader
{
	private readonly OneDragonEnvironment _environment;

	private readonly IDeserializer _yamlDeserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();

	private readonly HashSet<string> _loadedYamlPaths = new(StringComparer.OrdinalIgnoreCase);

	private string? _lastLoadedYamlPath;

	public AutoBattleReferenceGraphLoader(OneDragonEnvironment environment)
	{
		_environment = environment;
	}

	public AutoBattleReferenceGraphSnapshot Load(string subDir, string templateName)
	{
		_loadedYamlPaths.Clear();
		_lastLoadedYamlPath = null;
		Dictionary<string, object?> configuration = LoadYamlConfig(subDir, templateName);
		string configurationYamlPath = _lastLoadedYamlPath
			?? throw new InvalidOperationException("未记录自动战斗主策略路径。");
		List<AutoBattleCondOpScene> scenes = AutoBattleCondOpScene
			.GetDictionaryList(configuration, "scenes")
			.Select(scene => new AutoBattleCondOpScene(scene))
			.ToList();
		ValidateScenes(scenes, configurationYamlPath);
		ExpandTemplates(scenes, configurationYamlPath);
		return new AutoBattleReferenceGraphSnapshot(
			new Dictionary<string, object?>(configuration, StringComparer.Ordinal),
			scenes.ToArray(),
			configurationYamlPath,
			_loadedYamlPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray());
	}

	private void ExpandTemplates(IReadOnlyList<AutoBattleCondOpScene> scenes, string configurationYamlPath)
	{
		for (int sceneIndex = 0; sceneIndex < scenes.Count; sceneIndex++)
		{
			AutoBattleCondOpScene scene = scenes[sceneIndex];
			List<AutoBattleCondOpStateHandler> handlers = [];
			for (int handlerIndex = 0; handlerIndex < scene.Handlers.Count; handlerIndex++)
			{
				handlers.AddRange(ExpandStateHandler(
					scene.Handlers[handlerIndex],
					[],
					$"{configurationYamlPath} -> scenes[{sceneIndex}].handlers[{handlerIndex}].state_template"));
			}
			scene.SetHandlers(handlers);
		}
	}

	private List<AutoBattleCondOpStateHandler> ExpandStateHandler(
		AutoBattleCondOpStateHandler handler,
		List<string> stateHandlerTemplates,
		string referencePath)
	{
		if (!string.IsNullOrWhiteSpace(handler.StateTemplate))
		{
			if (stateHandlerTemplates.Contains(handler.StateTemplate, StringComparer.Ordinal))
			{
				throw new InvalidOperationException($"状态处理器模板循环引用: {string.Join(" -> ", stateHandlerTemplates.Append(handler.StateTemplate))}; {referencePath}");
			}
			stateHandlerTemplates.Add(handler.StateTemplate);
			Dictionary<string, object?> data = LoadYamlConfig("auto_battle_state_handler", handler.StateTemplate, referencePath);
			string sourcePath = _lastLoadedYamlPath ?? referencePath;
			List<AutoBattleCondOpStateHandler> result = [];
			List<Dictionary<string, object?>> handlers = AutoBattleCondOpScene.GetDictionaryList(data, "handlers");
			for (int handlerIndex = 0; handlerIndex < handlers.Count; handlerIndex++)
			{
				result.AddRange(ExpandStateHandler(
					new AutoBattleCondOpStateHandler(handlers[handlerIndex]),
					stateHandlerTemplates,
					$"{sourcePath} -> handlers[{handlerIndex}].state_template"));
			}
			stateHandlerTemplates.RemoveAt(stateHandlerTemplates.Count - 1);
			return result;
		}

		if (handler.SubHandlers.Count > 0)
		{
			List<AutoBattleCondOpStateHandler> subHandlers = [];
			for (int handlerIndex = 0; handlerIndex < handler.SubHandlers.Count; handlerIndex++)
			{
				subHandlers.AddRange(ExpandStateHandler(
					handler.SubHandlers[handlerIndex],
					stateHandlerTemplates,
					$"{referencePath} -> sub_handlers[{handlerIndex}].state_template"));
			}
			handler.SetSubHandlers(subHandlers);
		}
		else if (handler.Operations.Count > 0)
		{
			List<OperationDef> operations = [];
			for (int operationIndex = 0; operationIndex < handler.Operations.Count; operationIndex++)
			{
				operations.AddRange(ExpandOperation(
					handler.Operations[operationIndex],
					[],
					$"{referencePath} -> operations[{operationIndex}].operation_template"));
			}
			handler.SetOperations(operations);
		}

		return [handler];
	}

	private List<OperationDef> ExpandOperation(OperationDef operation, List<string> operationTemplates, string referencePath)
	{
		if (string.IsNullOrWhiteSpace(operation.OperationTemplate))
		{
			return [operation];
		}
		if (operationTemplates.Contains(operation.OperationTemplate, StringComparer.Ordinal))
		{
			throw new InvalidOperationException($"指令模板循环引用: {string.Join(" -> ", operationTemplates.Append(operation.OperationTemplate))}; {referencePath}");
		}
		operationTemplates.Add(operation.OperationTemplate);
		Dictionary<string, object?> data = LoadYamlConfig("auto_battle_operation", operation.OperationTemplate, referencePath);
		string sourcePath = _lastLoadedYamlPath ?? referencePath;
		List<OperationDef> result = [];
		List<Dictionary<string, object?>> operations = AutoBattleCondOpScene.GetDictionaryList(data, "operations");
		for (int operationIndex = 0; operationIndex < operations.Count; operationIndex++)
		{
			result.AddRange(ExpandOperation(
				new OperationDef(operations[operationIndex]),
				operationTemplates,
				$"{sourcePath} -> operations[{operationIndex}].operation_template"));
		}
		operationTemplates.RemoveAt(operationTemplates.Count - 1);
		return result;
	}

	private Dictionary<string, object?> LoadYamlConfig(string subDir, string templateName, string? referencePath = null)
	{
		string path = Path.GetFullPath(AutoBattleOperator.ResolveYamlPath(_environment, subDir, templateName));
		_lastLoadedYamlPath = path;
		_loadedYamlPaths.Add(path);
		if (!File.Exists(path))
		{
			string reference = string.IsNullOrWhiteSpace(referencePath) ? string.Empty : $"; 引用: {referencePath}";
			throw new FileNotFoundException("未找到配置文件 " + subDir + "/" + templateName + reference, path);
		}
		return NormalizeDictionary(_yamlDeserializer.Deserialize<object>(File.ReadAllText(path)));
	}

	private static Dictionary<string, object?> NormalizeDictionary(object? value)
	{
		if (value is not IDictionary dictionary)
		{
			return new Dictionary<string, object?>(StringComparer.Ordinal);
		}

		Dictionary<string, object?> result = new(StringComparer.Ordinal);
		foreach (DictionaryEntry item in dictionary)
		{
			string key = Convert.ToString(item.Key, CultureInfo.InvariantCulture) ?? string.Empty;
			result[key] = NormalizeValue(item.Value);
		}
		return result;
	}

	private static object? NormalizeValue(object? value)
	{
		if (value is IDictionary)
		{
			return NormalizeDictionary(value);
		}
		if (value is IEnumerable enumerable && value is not string)
		{
			List<object?> result = [];
			foreach (object? item in enumerable)
			{
				result.Add(NormalizeValue(item));
			}
			return result;
		}
		return value;
	}

	private static void ValidateScenes(IReadOnlyList<AutoBattleCondOpScene> scenes, string sourcePath)
	{
		Dictionary<string, List<int>> triggerLocations = new(StringComparer.Ordinal);
		List<int> normalScenes = [];
		for (int sceneIndex = 0; sceneIndex < scenes.Count; sceneIndex++)
		{
			AutoBattleCondOpScene scene = scenes[sceneIndex];
			if (scene.Triggers.Count == 0)
			{
				normalScenes.Add(sceneIndex);
				continue;
			}

			foreach (string trigger in scene.Triggers.Where(trigger => !string.IsNullOrWhiteSpace(trigger)))
			{
				if (!triggerLocations.TryGetValue(trigger, out List<int>? locations))
				{
					locations = [];
					triggerLocations.Add(trigger, locations);
				}
				locations.Add(sceneIndex);
			}
		}

		List<string> errors = triggerLocations
			.Where(pair => pair.Value.Count > 1)
			.Select(pair => $"重复 trigger '{pair.Key}': {string.Join(", ", pair.Value.Select(index => $"{sourcePath} -> scenes[{index}].triggers"))}")
			.ToList();
		if (normalScenes.Count > 1)
		{
			errors.Add($"多个无 trigger 场景: {string.Join(", ", normalScenes.Select(index => $"{sourcePath} -> scenes[{index}].triggers"))}");
		}
		if (errors.Count > 0)
		{
			throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
		}
	}
}
