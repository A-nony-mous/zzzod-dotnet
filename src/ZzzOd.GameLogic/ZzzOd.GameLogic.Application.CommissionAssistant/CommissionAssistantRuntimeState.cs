using System;
using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.CommissionAssistant;

/// <summary>
/// 委托助手运行态。
/// </summary>
public sealed class CommissionAssistantRuntimeState
{
	/// <summary>对话模式。</summary>
	public const int DialogMode = 0;

	/// <summary>闪避模式。</summary>
	public const int DodgeMode = 1;

	/// <summary>自动战斗模式。</summary>
	public const int AutoBattleMode = 2;

	private readonly object _lock = new object();

	private int _runMode;

	/// <summary>当前运行模式。</summary>
	public int RunMode
	{
		get
		{
			lock (_lock)
			{
				return _runMode;
			}
		}
	}

	/// <summary>是否点击过对话。</summary>
	public bool DialogClicked { get; set; }

	/// <summary>上一次识别到的对话选项。</summary>
	public HashSet<string> LastDialogOptions { get; } = new HashSet<string>(StringComparer.Ordinal);

	/// <summary>当前锁定点击的对话选项。</summary>
	public string? ChosenOption { get; set; }

	/// <summary>第一次点击当前选项的时间。</summary>
	public DateTimeOffset ChosenOptionLastTime { get; set; }

	/// <summary>钓鱼长按中的按键。</summary>
	public string? FishingButtonPressed { get; set; }

	/// <summary>钓鱼是否结束。</summary>
	public bool FishingDone { get; set; }

	/// <summary>主线剧情按钮点击时间。</summary>
	public DateTimeOffset MainStoryClickTime { get; set; }

	/// <summary>处理热键。</summary>
	public int HandleKeyPress(string key, CommissionAssistantConfig config)
	{
		lock (_lock)
		{
			if (string.Equals(key, config.DodgeSwitch, StringComparison.Ordinal))
			{
				_runMode = ((_runMode == 0) ? 1 : 0);
			}
			else if (string.Equals(key, config.AutoBattleSwitch, StringComparison.Ordinal))
			{
				_runMode = ((_runMode == 0) ? 2 : 0);
			}
			return _runMode;
		}
	}

	/// <summary>重置为对话模式。</summary>
	public void ResetDialogMode()
	{
		lock (_lock)
		{
			_runMode = 0;
		}
	}
}
