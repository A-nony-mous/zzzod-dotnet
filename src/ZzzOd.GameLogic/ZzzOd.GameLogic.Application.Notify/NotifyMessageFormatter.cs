using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.Notify;

/// <summary>
/// 通知消息格式化器。
/// </summary>
public sealed class NotifyMessageFormatter
{
	/// <summary>
	/// 格式化通知消息。
	/// </summary>
	public NotifyMessage Format(ZContext context, DateTimeOffset now)
	{
		int valueOrDefault = context.RunContext.CurrentInstanceIndex.GetValueOrDefault();
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		string text = null;
		foreach (OneDragonApplicationConfigItem groupApplication in GetGroupApplications(context, valueOrDefault))
		{
			if (!context.RunContext.IsAppRegistered(groupApplication.AppId))
			{
				continue;
			}
			IApplicationRunRecord runRecord = context.RunContext.GetRunRecord(groupApplication.AppId, valueOrDefault);
			if (!(runRecord is ZApplicationRunRecord zApplicationRunRecord) || !IsWithinTime(zApplicationRunRecord.RunTime, now))
			{
				continue;
			}
			if (zApplicationRunRecord is ChargePlanRunRecord chargePlanRunRecord)
			{
				int estimatedChargePower = chargePlanRunRecord.GetEstimatedChargePower();
				if (estimatedChargePower >= 0)
				{
					text = $"当前体力：{estimatedChargePower}/{240}";
				}
			}
			string applicationName = context.RunContext.GetApplicationName(groupApplication.AppId);
			if (zApplicationRunRecord.RunStatusUnderNow == 1)
			{
				list.Add(applicationName);
			}
			else if (zApplicationRunRecord.RunStatusUnderNow == 2)
			{
				list2.Add(applicationName);
			}
		}
		int num = 1;
		List<string> list3 = new List<string>(num);
		CollectionsMarshal.SetCount(list3, num);
		CollectionsMarshal.AsSpan(list3)[0] = "一条龙运行完成：";
		List<string> list4 = list3;
		if (text != null)
		{
			list4.Add(text);
		}
		bool flag = list2.Count > 0;
		bool flag2 = list.Count > 0;
		if (flag)
		{
			list4.Add("❌ 失败指令：" + string.Join(", ", list2));
		}
		else if (flag2)
		{
			list4.Add("全部成功✅");
		}
		if (flag2)
		{
			list4.Add("✅ 成功指令：" + string.Join(", ", list));
		}
		else if (!flag)
		{
			list4.Add("全部失败❌");
		}
		return new NotifyMessage(string.Join(Environment.NewLine, list4), flag);
	}

	private static IReadOnlyList<OneDragonApplicationConfigItem> GetGroupApplications(ZContext context, int instanceIndex)
	{
		string item = context.RunContext.CurrentGroupId ?? "one_dragon";
		YamlConfig<OneDragonApplicationGroupConfig> yamlConfig = new YamlConfig<OneDragonApplicationGroupConfig>(context.Environment, "_group", null, instanceIndex, new string[] { item });
		return yamlConfig.Current.AppList ?? new List<OneDragonApplicationConfigItem>();
	}

	/// <summary>
	/// 判断运行时间是否在最近三小时内。
	/// </summary>
	public static bool IsWithinTime(string? timeText, DateTimeOffset now)
	{
		if (string.IsNullOrWhiteSpace(timeText) || !DateTime.TryParseExact(timeText, "MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			return false;
		}
		DateTimeOffset dateTimeOffset = now;
		DateTimeOffset dateTimeOffset2 = dateTimeOffset.AddHours(-3.0);
		int[] array = new int[2]
		{
			dateTimeOffset.Year,
			dateTimeOffset.Year - 1
		};
		foreach (int year in array)
		{
			DateTime dateTime;
			try
			{
				dateTime = new DateTime(year, result.Month, result.Day, result.Hour, result.Minute, 0);
			}
			catch (ArgumentOutOfRangeException)
			{
				continue;
			}
			DateTimeOffset dateTimeOffset3 = new DateTimeOffset(dateTime, dateTimeOffset.Offset);
			if (dateTimeOffset3 >= dateTimeOffset2 && dateTimeOffset3 <= dateTimeOffset)
			{
				return true;
			}
		}
		return false;
	}
}
