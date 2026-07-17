using System;
using System.Collections.Generic;
using System.Threading;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;

namespace ZzzOd.GameLogic.Operations;

/// <summary>
/// 读取 key_sim 配置并执行模拟按键。
/// </summary>
public sealed class KeySimRunner : ZOperation
{
	private sealed class KeySimConfig
	{
		[YamlMember(Alias = "operations", ApplyNamingConventions = false)]
		public List<KeySimOperationDef> Operations { get; set; } = new List<KeySimOperationDef>();
	}

	private sealed class KeySimOperationDef
	{
		[YamlMember(Alias = "op_name", ApplyNamingConventions = false)]
		public string OpName { get; set; } = string.Empty;

		[YamlMember(Alias = "pre_delay", ApplyNamingConventions = false)]
		public double PreDelay { get; set; }

		[YamlMember(Alias = "post_delay", ApplyNamingConventions = false)]
		public double PostDelay { get; set; }

		[YamlMember(Alias = "way", ApplyNamingConventions = false)]
		public string? Way { get; set; }

		[YamlMember(Alias = "press", ApplyNamingConventions = false)]
		public double? Press { get; set; }

		[YamlMember(Alias = "repeat", ApplyNamingConventions = false)]
		public int Repeat { get; set; } = 1;

		[YamlMember(Alias = "seconds", ApplyNamingConventions = false)]
		public double Seconds { get; set; }
	}

	private readonly string _configName;

	private readonly Action<TimeSpan> _delay;

	private IReadOnlyList<KeySimOperationDef> _operations = Array.Empty<KeySimOperationDef>();

	/// <summary>
	/// 初始化模拟按键操作。
	/// </summary>
	public KeySimRunner(ZContext context, string configName, Action<TimeSpan>? delay = null)
		: base(context, "模拟按键 " + configName)
	{
		_configName = configName;
		_delay = delay ?? new Action<TimeSpan>(Thread.Sleep);
	}

	[OperationNode("加载配置", IsStartNode = true, ScreenshotBeforeRound = false)]
	private OperationRoundResult LoadConfig()
	{
		OneDragonEnvironment environment = base.ZContext.Environment;
		string configName = _configName;
		IReadOnlyList<string> subDirectories = new string[] { "key_sim" };
		YamlConfig<KeySimConfig> yamlConfig = new YamlConfig<KeySimConfig>(environment, configName, null, null, subDirectories, sample: true);
		_operations = yamlConfig.Current.Operations;
		return RoundSuccess();
	}

	[NodeFrom("加载配置")]
	[OperationNode("执行按键", ScreenshotBeforeRound = false)]
	private OperationRoundResult RunKeySim()
	{
		if (!(base.ZContext.Controller is ZPcController controller))
		{
			return RoundFail("控制器不支持按键模拟");
		}
		foreach (KeySimOperationDef operation in _operations)
		{
			OperationRoundResult operationRoundResult = ExecuteOperation(controller, operation);
			if (operationRoundResult.IsFail)
			{
				return operationRoundResult;
			}
		}
		return RoundSuccess("执行完成");
	}

	private OperationRoundResult ExecuteOperation(ZPcController controller, KeySimOperationDef operation)
	{
		if (string.IsNullOrWhiteSpace(operation.OpName))
		{
			return RoundFail("非法的指令");
		}
		DelaySeconds(operation.PreDelay);
		if (string.Equals(operation.OpName, "等待秒数", StringComparison.Ordinal))
		{
			DelaySeconds(operation.Seconds);
			DelaySeconds(operation.PostDelay);
			return RoundSuccess();
		}
		if (!operation.OpName.StartsWith("按键-", StringComparison.Ordinal))
		{
			return RoundFail("非法的指令 " + operation.OpName);
		}
		(string ActionName, bool Press, bool Release) tuple = ResolveButtonAction(operation);
		string item = tuple.ActionName;
		bool item2 = tuple.Press;
		bool item3 = tuple.Release;
		TimeSpan? pressTime = ((item2 && operation.Press.HasValue) ? new TimeSpan?(TimeSpan.FromSeconds(operation.Press.Value)) : ((TimeSpan?)null));
		int num = Math.Max(1, operation.Repeat);
		for (int i = 0; i < num; i++)
		{
			if (!controller.RunNamedAction(item, item2, pressTime, item3))
			{
				return RoundFail("非法的指令 " + operation.OpName);
			}
		}
		DelaySeconds(operation.PostDelay);
		return RoundSuccess();
	}

	private static (string ActionName, bool Press, bool Release) ResolveButtonAction(KeySimOperationDef operation)
	{
		string text = operation.OpName;
		bool item = string.Equals(operation.Way, "按下", StringComparison.Ordinal);
		bool item2 = string.Equals(operation.Way, "松开", StringComparison.Ordinal);
		if (text.EndsWith("-按下", StringComparison.Ordinal))
		{
			string text2 = text;
			text = text2.Substring(0, text2.Length - 3);
			item = true;
			item2 = false;
		}
		else if (text.EndsWith("-松开", StringComparison.Ordinal))
		{
			string text2 = text;
			text = text2.Substring(0, text2.Length - 3);
			item = false;
			item2 = true;
		}
		return (ActionName: text, Press: item, Release: item2);
	}

	private void DelaySeconds(double seconds)
	{
		if (!(seconds <= 0.0))
		{
			_delay(TimeSpan.FromSeconds(seconds));
		}
	}
}
