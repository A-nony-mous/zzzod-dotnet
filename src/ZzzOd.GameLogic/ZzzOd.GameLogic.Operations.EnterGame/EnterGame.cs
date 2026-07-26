using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations.EnterGame;

/// <summary>
/// Handles login and transition into the normal world.
/// </summary>
public sealed class EnterGame : ZOperation
{
	/// <summary>Game data updated status.</summary>
	public const string StatusGameDataUpdated = "游戏数据已更新";

	/// <summary>Login success status.</summary>
	public const string StatusLoginSuccess = "登录成功";

	/// <summary>Loading status.</summary>
	public const string StatusLoading = "加载中";

	private readonly Func<DateTimeOffset> _now;

	private readonly TimeSpan _maxResourceDownload;

	private readonly bool _forceLogin;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _waitDelay;

	private bool _alreadyLogin;

	private bool _afterFirstEnterClick;

	private bool _afterSecondEnterClick;

	private readonly List<string> _interactIgnoreWords = new List<string>();

	/// <summary>
	/// Resource download start time.
	/// </summary>
	public DateTimeOffset? ResourceDownloadStartTimeUtc { get; private set; }

	/// <summary>
	/// Initialize the operation.
	/// </summary>
	public EnterGame(ZContext context, bool switchAccount = false, Func<DateTimeOffset>? now = null, TimeSpan? maxResourceDownload = null, TimeSpan? retryDelay = null, TimeSpan? waitDelay = null)
		: base(context, "进入游戏")
	{
		OneDragonConfig oneDragonConfig = LoadOneDragonConfig(context);
		bool runAllInstances = string.Equals(oneDragonConfig.InstanceRun, "全部实例", StringComparison.Ordinal);
		int activeInOneDragonCount = oneDragonConfig.InstanceList.Count((OneDragonInstanceConfigItem item) => item.ActiveInOneDragon);
		bool forceLoginBeforeRun = context.ForceLoginBeforeRun;
		bool requestedForceLogin = switchAccount || (runAllInstances && activeInOneDragonCount > 1) || forceLoginBeforeRun;
		if (!switchAccount && requestedForceLogin && !context.GameAccountConfig.HasLoginInfo)
		{
			context.Logger.Warning("登录信息未配置完整，跳过强制重新登录，将使用游戏当前登录状态");
		}
		_forceLogin = ShouldForceLogin(switchAccount, context.GameAccountConfig, runAllInstances, activeInOneDragonCount, forceLoginBeforeRun);
		_now = now ?? ((Func<DateTimeOffset>)(() => DateTimeOffset.UtcNow));
		_maxResourceDownload = maxResourceDownload ?? TimeSpan.FromSeconds(1200L);
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_waitDelay = waitDelay ?? TimeSpan.FromSeconds(1L);
	}

	/// <summary>
	/// 读取一条龙配置，用于在构造时判断是否需要强制重新登录。
	/// </summary>
	private static OneDragonConfig LoadOneDragonConfig(ZContext context)
	{
		IReadOnlyList<string> subDirectories = Array.Empty<string>();
		return new YamlConfig<OneDragonConfig>(context.Environment, "one_dragon", null, null, subDirectories).Current;
	}

	/// <summary>
	/// Resolve whether this login flow should force account login.
	/// </summary>
	public static bool ShouldForceLogin(bool switchAccount, GameAccountConfig accountConfig, bool runAllInstances = false, int instanceCount = 1, bool forceLoginBeforeRun = false)
	{
		ArgumentNullException.ThrowIfNull(accountConfig, "accountConfig");
		if (switchAccount)
		{
			return true;
		}
		return ((runAllInstances && instanceCount > 1) || forceLoginBeforeRun) && accountConfig.HasLoginInfo;
	}

	/// <summary>
	/// Match status text after clicking enter game.
	/// </summary>
	public static string? MatchEnterClickStatusText(IEnumerable<string> ocrTexts, bool includeEnterClick = false)
	{
		ArgumentNullException.ThrowIfNull(ocrTexts, "ocrTexts");
		int num = 5;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		span[0] = "加载配置数据中";
		span[1] = "版本校对中";
		span[2] = "登录游戏服务器中";
		span[3] = "登录成功";
		span[4] = "资源下载中";
		List<string> list2 = list;
		if (includeEnterClick)
		{
			list2.Insert(0, "点击进入游戏");
		}
		List<string> source = ocrTexts.Where((string text) => !string.IsNullOrWhiteSpace(text)).ToList();
		foreach (string targetWord in list2)
		{
			if (source.Any((string text) => StringUtils.FindByLcs(targetWord, text, 0.5)))
			{
				return targetWord;
			}
		}
		return null;
	}

	/// <summary>
	/// Detect a low-saturation loading frame.
	/// </summary>
	public static bool IsGrayLoadingScreen(Mat screen)
	{
		ArgumentNullException.ThrowIfNull(screen, "screen");
		if (screen.Empty() || screen.Channels() < 3)
		{
			return false;
		}
		int num = screen.Width / 10;
		int num2 = screen.Height / 10;
		int val = Math.Max(1, screen.Width * 8 / 10);
		int val2 = Math.Max(1, screen.Height * 8 / 10);
		using Mat mat = new Mat(screen, new OpenCvSharp.Rect(num, num2, Math.Min(val, screen.Width - num), Math.Min(val2, screen.Height - num2)));
		using Mat mat2 = new Mat();
		Cv2.CvtColor(mat, mat2, ColorConversionCodes.RGB2HSV);
		Mat[] array = Cv2.Split(mat2);
		using Mat mat3 = array[1];
		using Mat mat4 = array[2];
		array[0].Dispose();
		using Mat mat5 = new Mat();
		Cv2.Threshold(mat4, mat5, 20.0, 255.0, ThresholdTypes.Binary);
		int num3 = Cv2.CountNonZero(mat5);
		if (num3 == 0)
		{
			return true;
		}
		using Mat mat6 = new Mat();
		Cv2.Threshold(mat3, mat6, 40.0, 255.0, ThresholdTypes.Binary);
		using Mat mat7 = new Mat();
		Cv2.BitwiseAnd(mat6, mat5, mat7);
		double num4 = (double)Cv2.CountNonZero(mat7) / (double)num3;
		return num4 < 0.03;
	}

	/// <summary>
	/// Wait for resource download, failing after the configured timeout.
	/// </summary>
	public OperationRoundResult WaitResourceDownload()
	{
		DateTimeOffset dateTimeOffset = _now();
		DateTimeOffset? resourceDownloadStartTimeUtc = ResourceDownloadStartTimeUtc;
		DateTimeOffset valueOrDefault = resourceDownloadStartTimeUtc.GetValueOrDefault();
		if (!resourceDownloadStartTimeUtc.HasValue)
		{
			valueOrDefault = dateTimeOffset;
			DateTimeOffset? resourceDownloadStartTimeUtc2 = valueOrDefault;
			ResourceDownloadStartTimeUtc = resourceDownloadStartTimeUtc2;
		}
		if (dateTimeOffset - ResourceDownloadStartTimeUtc.Value < _maxResourceDownload)
		{
			return RoundWait("资源下载中", null, TimeSpan.FromSeconds(2L));
		}
		ResourceDownloadStartTimeUtc = null;
		return RoundFail("资源下载超时");
	}

	/// <inheritdoc />
	protected override Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		_alreadyLogin = false;
		_afterFirstEnterClick = false;
		_afterSecondEnterClick = false;
		ResourceDownloadStartTimeUtc = null;
		_interactIgnoreWords.Clear();
		return base.OnInitializeAsync(cancellationToken);
	}

	[NodeFrom("国服-输入账号密码")]
	[NodeFrom("国服-输入账号密码-新")]
	[NodeFrom("B服新-选择登录过的账号")]
	[NodeFrom("国际服-换服")]
	[NodeFrom("点击进入游戏", Status = "游戏数据已更新")]
	[NodeFrom("点击进入游戏", Status = "切换账号确定")]
	[NodeFrom("画面识别", Status = "B服新-同意隐私政策")]
	[OperationNode("画面识别", IsStartNode = true, NodeMaxRetryTimes = 60)]
	private OperationRoundResult CheckScreen()
	{
		if (base.LastScreenshot == null)
		{
			return RoundRetry("未获取到游戏截图", null, _retryDelay);
		}
		OperationRoundResult operationRoundResult = CheckGameDataUpdated(base.LastScreenshot, backToCheckScreen: false);
		if (operationRoundResult != null)
		{
			return operationRoundResult;
		}
		string text = ReadEnterClickStatusText(base.LastScreenshot);
		if (text != null)
		{
			return RoundSuccess("点击进入游戏", null, _waitDelay);
		}
		OperationRoundResult operationRoundResult2 = CheckLoginRelated(base.LastScreenshot);
		if (operationRoundResult2 != null)
		{
			return operationRoundResult2;
		}
		OperationRoundResult operationRoundResult3 = MatchLoginError(base.LastScreenshot);
		if (operationRoundResult3 != null)
		{
			return operationRoundResult3;
		}
		OperationRoundResult operationRoundResult4 = RoundByFindArea(base.LastScreenshot, "打开游戏", "国服-账号密码进入游戏-新");
		if (operationRoundResult4.IsSuccess)
		{
			RoundByClickArea("打开游戏", "国服-返回按钮");
			return RoundRetry("返回重试", null, _retryDelay);
		}
		return RoundRetry("未知画面", null, _retryDelay);
	}

	[NodeFrom("画面识别", Status = "国服-账号密码")]
	[OperationNode("国服-输入账号密码")]
	private OperationRoundResult InputAccountPassword()
	{
		return InputAccountPasswordCore("国服-账号输入区域", "国服-密码输入区域", "国服-同意按钮", "国服-账号密码进入游戏");
	}

	[NodeFrom("画面识别", Status = "国服-账号密码-新")]
	[OperationNode("国服-输入账号密码-新")]
	private OperationRoundResult InputAccountPasswordNew()
	{
		return InputAccountPasswordCore("国服-账号输入区域-新", "国服-密码输入区域-新", "国服-同意按钮-新", "国服-账号密码进入游戏-新");
	}

	[NodeFrom("画面识别", Status = "B服新-登录记录")]
	[OperationNode("B服新-点击下拉菜单")]
	private OperationRoundResult ClickBilibiliDropButton()
	{
		if (string.IsNullOrWhiteSpace(base.ZContext.GameAccountConfig.BilibiliAccountName))
		{
			return RoundFail("未配置B服用户名, 无法切换已登录的B服账号");
		}
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = TimeSpan.FromMilliseconds(800L);
		return RoundByFindAndClickArea(lastScreenshot, "打开游戏", "B服新-切换账号", null, successDelay);
	}

	[NodeFrom("B服新-点击下拉菜单")]
	[OperationNode("B服新-选择登录过的账号")]
	private OperationRoundResult SwitchBilibiliAccount()
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("打开游戏", "B服新-账号列表");
		if (area == null || base.LastScreenshot == null || base.ZContext.Controller == null)
		{
			return RoundRetry("未找到已登录的用户");
		}
		string text = base.ZContext.GameAccountConfig.BilibiliAccountName.Trim();
		IReadOnlyDictionary<string, MatchResultList> ocrResultMap = base.ZContext.OcrService.GetOcrResultMap(base.LastScreenshot, null, area.Rect);
		foreach (var (target, matchResultList2) in ocrResultMap)
		{
			if (matchResultList2.Max == null || !StringUtils.FindByLcs(text, target, 0.7))
			{
				continue;
			}
			base.ZContext.Controller.Click(matchResultList2.Max.Center);
			_alreadyLogin = true;
			Mat? screen = Screenshot();
			TimeSpan? successDelay = TimeSpan.FromSeconds(5L);
			TimeSpan? retryDelay = _retryDelay;
			return RoundByFindAndClickArea(screen, "打开游戏", "B服-登录", null, successDelay, retryDelay);
		}
		object obj;
		if (text.Length < 2)
		{
			obj = "*";
		}
		else
		{
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 3);
			defaultInterpolatedStringHandler.AppendFormatted(text[0]);
			defaultInterpolatedStringHandler.AppendFormatted(new string('*', Math.Max(text.Length - 2, 1)));
			defaultInterpolatedStringHandler.AppendFormatted(text[text.Length - 1]);
			obj = defaultInterpolatedStringHandler.ToStringAndClear();
		}
		string text3 = (string)obj;
		return RoundRetry("未找到已登录的用户: " + text3, null, _retryDelay);
	}

	[NodeFrom("画面识别", Status = "国际服-密码输入区域")]
	[OperationNode("国际服-输入账号密码")]
	private OperationRoundResult InputAccountPasswordIntl()
	{
		return InputAccountPasswordCore("国际服-账号输入区域", "国际服-密码输入区域", null, "国际服-账号密码进入游戏");
	}

	[NodeFrom("国际服-输入账号密码", Status = "国际服-账号密码进入游戏")]
	[OperationNode("国际服-换服")]
	private OperationRoundResult CheckServer()
	{
		TimeSpan? successDelay = _waitDelay;
		RoundByClickArea("打开游戏", "国际服-换服", clickLeftTop: false, null, successDelay);
		string gameRegion = base.ZContext.GameAccountConfig.GameRegion;
		if (1 == 0)
		{
		}
		string text = gameRegion switch
		{
			"eu" => "国际服-换服-欧洲", 
			"us" => "国际服-换服-美国", 
			"asia" => "国际服-换服-亚洲", 
			_ => "国际服-换服-港澳台", 
		};
		if (1 == 0)
		{
		}
		string areaName = text;
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("打开游戏", "国际服-换服-美国");
		if (area != null && base.ZContext.Controller != null)
		{
			base.ZContext.Controller.DragTo(area.Center + new OneDragon.Core.Abstractions.Geometry.Point(0, 200), area.Center);
		}
		Mat? screen = Screenshot();
		successDelay = _waitDelay;
		return RoundByFindAndClickArea(screen, "打开游戏", areaName, null, successDelay);
	}

	[NodeFrom("画面识别", Status = "点击进入游戏")]
	[OperationNode("点击进入游戏", NodeMaxRetryTimes = 15)]
	private OperationRoundResult CheckEnterClickStatus()
	{
		if (base.LastScreenshot == null)
		{
			return RoundRetry("未获取到游戏截图", null, _retryDelay);
		}
		OperationRoundResult operationRoundResult = CheckGameDataUpdated(base.LastScreenshot, backToCheckScreen: true);
		if (operationRoundResult != null)
		{
			return operationRoundResult;
		}
		if (_forceLogin && !_alreadyLogin)
		{
			OperationRoundResult operationRoundResult2 = RoundByFindAndClickArea(base.LastScreenshot, "打开游戏", "切换账号确定", null, null);
			if (operationRoundResult2.IsSuccess)
			{
				_afterFirstEnterClick = false;
				_afterSecondEnterClick = false;
				ResourceDownloadStartTimeUtc = null;
				return RoundSuccess(operationRoundResult2.Status, null, TimeSpan.FromSeconds(5L));
			}
			OperationRoundResult operationRoundResult3 = RoundByFindAndClickArea(base.LastScreenshot, "打开游戏", "切换账号", null, null);
			if (operationRoundResult3.IsSuccess)
			{
				_afterSecondEnterClick = false;
				ResourceDownloadStartTimeUtc = null;
				return RoundWait(operationRoundResult3.Status, null, _retryDelay);
			}
			return RoundRetry("等待切换账号", null, _retryDelay);
		}
		string text = ReadEnterClickStatusText(base.LastScreenshot, includeEnterClick: true);
		if (text != null)
		{
			if (text == "资源下载中")
			{
				return WaitResourceDownload();
			}
			ResourceDownloadStartTimeUtc = null;
			if (text == "点击进入游戏")
			{
				OperationRoundResult operationRoundResult4 = RoundByClickArea("打开游戏", "点击进入游戏");
				if (!operationRoundResult4.IsSuccess)
				{
					return operationRoundResult4;
				}
				if (_afterFirstEnterClick)
				{
					_afterSecondEnterClick = true;
				}
				else
				{
					_afterFirstEnterClick = true;
				}
				return RoundWait(text, null, TimeSpan.FromSeconds(2L));
			}
			if (text == "加载配置数据中")
			{
				_afterFirstEnterClick = true;
			}
			if (text == "登录游戏服务器中" || text == "登录成功")
			{
				_afterSecondEnterClick = true;
			}
			return (text == "登录成功") ? RoundSuccess(text, null, _waitDelay) : RoundWait(text, null, _waitDelay);
		}
		if (_afterSecondEnterClick)
		{
			OperationRoundResult operationRoundResult5 = MatchLoginError(base.LastScreenshot);
			return operationRoundResult5 ?? RoundSuccess("加载中");
		}
		return RoundRetry("进入游戏点击后等待", null, _retryDelay);
	}

	[NodeFrom("点击进入游戏", Status = "登录成功")]
	[NodeFrom("点击进入游戏", Status = "加载中")]
	[OperationNode("登录成功", TimeoutSeconds = 180.0)]
	private OperationRoundResult WaitLoading()
	{
		if (base.LastScreenshot == null)
		{
			return RoundRetry("未获取到游戏截图", null, _retryDelay);
		}
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "加载中", "加载中");
		if (operationRoundResult.IsSuccess)
		{
			return RoundWait(operationRoundResult.Status, null, TimeSpan.FromSeconds(2L));
		}
		OperationRoundResult operationRoundResult2 = CheckScreenToInteract(base.LastScreenshot);
		if (operationRoundResult2 != null)
		{
			return operationRoundResult2;
		}
		OperationRoundResult operationRoundResult3 = IsInBigWorld(base.LastScreenshot);
		if (operationRoundResult3 != null)
		{
			return operationRoundResult3;
		}
		if (IsGrayLoadingScreen(base.LastScreenshot))
		{
			return RoundWait("加载中", null, TimeSpan.FromSeconds(2L));
		}
		OperationRoundResult operationRoundResult4 = RoundByClickArea("菜单", "返回");
		return operationRoundResult4.IsSuccess ? RoundRetry("登录成功后等待加载中或大世界", null, TimeSpan.FromSeconds(2L)) : operationRoundResult4;
	}

	private OperationRoundResult InputAccountPasswordCore(string accountArea, string passwordArea, string? agreeArea, string submitArea)
	{
		if (string.IsNullOrWhiteSpace(base.ZContext.GameAccountConfig.Account) || string.IsNullOrWhiteSpace(base.ZContext.GameAccountConfig.Password))
		{
			return RoundFail("未配置账号密码");
		}
		if (base.ZContext.Controller == null)
		{
			return RoundRetry("控制器未初始化", null, _retryDelay);
		}
		OperationRoundResult operationRoundResult = RoundByClickArea("打开游戏", accountArea);
		if (!operationRoundResult.IsSuccess)
		{
			return operationRoundResult;
		}
		base.ZContext.Controller.DeleteAllInput();
		base.ZContext.Controller.InputText(base.ZContext.GameAccountConfig.Account);
		OperationRoundResult operationRoundResult2 = RoundByClickArea("打开游戏", passwordArea);
		if (!operationRoundResult2.IsSuccess)
		{
			return operationRoundResult2;
		}
		base.ZContext.Controller.DeleteAllInput();
		base.ZContext.Controller.InputText(base.ZContext.GameAccountConfig.Password);
		if (!string.IsNullOrWhiteSpace(agreeArea))
		{
			OperationRoundResult operationRoundResult3 = RoundByClickArea("打开游戏", agreeArea);
			if (!operationRoundResult3.IsSuccess)
			{
				return operationRoundResult3;
			}
		}
		_alreadyLogin = true;
		Mat? screen = Screenshot();
		TimeSpan? successDelay = TimeSpan.FromSeconds(5L);
		TimeSpan? retryDelay = _retryDelay;
		return RoundByFindAndClickArea(screen, "打开游戏", submitArea, null, successDelay, retryDelay);
	}

	private OperationRoundResult? CheckGameDataUpdated(Mat screen, bool backToCheckScreen)
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(screen, "打开游戏", "游戏数据更新提示");
		if (!operationRoundResult.IsSuccess)
		{
			return null;
		}
		TimeSpan? retryDelay = _retryDelay;
		OperationRoundResult operationRoundResult2 = RoundByFindAndClickArea(screen, "打开游戏", "游戏数据更新-确定", null, null, retryDelay);
		if (!operationRoundResult2.IsSuccess)
		{
			return operationRoundResult2;
		}
		_afterSecondEnterClick = false;
		ResourceDownloadStartTimeUtc = null;
		return backToCheckScreen ? RoundSuccess("游戏数据已更新", null, TimeSpan.FromSeconds(3L)) : RoundWait("游戏数据已更新", null, TimeSpan.FromSeconds(3L));
	}

	private OperationRoundResult? CheckLoginRelated(Mat screen)
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(screen, "打开游戏", "标题-退出登录");
		if (operationRoundResult.IsSuccess)
		{
			OperationRoundResult operationRoundResult2 = RoundByFindAndClickArea(screen, "打开游戏", "按钮-退出登录-确定");
			if (operationRoundResult2.IsSuccess)
			{
				return RoundWait(operationRoundResult2.Status, null, _retryDelay);
			}
		}
		OperationRoundResult operationRoundResult3 = RoundByFindArea(screen, "打开游戏", "B服新-登录记录");
		if (operationRoundResult3.IsSuccess)
		{
			return RoundSuccess(operationRoundResult3.Status);
		}
		OperationRoundResult operationRoundResult4 = RoundByFindArea(screen, "打开游戏", "点击进入游戏");
		if (operationRoundResult4.IsSuccess)
		{
			ResourceDownloadStartTimeUtc = null;
			return RoundSuccess("点击进入游戏", null, _waitDelay);
		}
		OperationRoundResult operationRoundResult6 = RoundByFindAndClickArea(screen, "打开游戏", "国服-账号密码");
		if (operationRoundResult6.IsSuccess)
		{
			return RoundSuccess(operationRoundResult6.Status, null, _waitDelay);
		}
		OperationRoundResult operationRoundResult7 = RoundByFindAndClickArea(screen, "打开游戏", "国服-账号密码-新");
		if (operationRoundResult7.IsSuccess)
		{
			return RoundSuccess(operationRoundResult7.Status, null, _waitDelay);
		}
		OperationRoundResult operationRoundResult8 = RoundByFindAndClickArea(screen, "打开游戏", "按钮-登陆其他账号");
		if (operationRoundResult8.IsSuccess)
		{
			return RoundWait(operationRoundResult8.Status, null, _waitDelay);
		}
		OperationRoundResult operationRoundResult9 = RoundByFindArea(screen, "打开游戏", "B服新-手机号登录");
		if (operationRoundResult9.IsSuccess)
		{
			return RoundFail(operationRoundResult9.Status);
		}
		OperationRoundResult operationRoundResult10 = RoundByFindArea(screen, "打开游戏", "B服新-隐私政策提示");
		if (operationRoundResult10.IsSuccess)
		{
			return RoundByFindAndClickArea(screen, "打开游戏", "B服新-同意隐私政策");
		}
		string gameRegion = base.ZContext.GameAccountConfig.GameRegion;
		if ((!(gameRegion == "cn") && !(gameRegion == "cn_b")) || 1 == 0)
		{
			return CheckScreenIntl(screen);
		}
		return null;
	}

	private OperationRoundResult? CheckScreenIntl(Mat screen)
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(screen, "打开游戏", "国际服-点击登录");
		if (operationRoundResult.IsSuccess)
		{
			// 已登录状态下也可能闪现"点击登录"文字，先等待再确认是否真的需要点击。
			Thread.Sleep(TimeSpan.FromSeconds(2L));
			operationRoundResult = RoundByFindAndClickArea(screen, "打开游戏", "国际服-点击登录");
			if (operationRoundResult.IsSuccess)
			{
				return RoundWait(operationRoundResult.Status, null, _retryDelay);
			}
		}
		OperationRoundResult operationRoundResult2 = RoundByFindArea(screen, "打开游戏", "国际服-密码输入区域");
		return operationRoundResult2.IsSuccess ? RoundSuccess(operationRoundResult2.Status, null, _waitDelay) : null;
	}

	private OperationRoundResult? CheckScreenToInteract(Mat screen)
	{
		IReadOnlyList<string> readOnlyList = new string[24]
		{
			"取消", "确认", "领取01", "已领取01", "领取02", "已领取02", "领取03", "已领取03", "领取60", "已领取60",
			"领取120", "已领取120", "01", "02", "03", "04", "05", "06", "07", "领取",
			"已领取", "待领取", "今日到账", "惊喜补给"
		};
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal)
		{
			"已领取", "待领取", "已领取01", "已领取02", "已领取03", "已领取60", "已领取120", "01", "02", "03",
			"04", "05", "06", "07"
		};
		foreach (string interactIgnoreWord in _interactIgnoreWords)
		{
			hashSet.Add(interactIgnoreWord);
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultList(screen);
		foreach (string targetWord in readOnlyList)
		{
			if (hashSet.Contains(targetWord))
			{
				continue;
			}
			OcrMatchResult ocrMatchResult = ocrResultList.FirstOrDefault((OcrMatchResult item) => StringUtils.FindByLcs(targetWord, item.Text, 0.5));
			if (ocrMatchResult == null || base.ZContext.Controller == null)
			{
				continue;
			}
			if (targetWord.Contains("领取", StringComparison.Ordinal))
			{
				_interactIgnoreWords.Add(targetWord);
			}
			base.ZContext.Controller.Click(ocrMatchResult.Center);
			return RoundWait(targetWord, null, _waitDelay);
		}
		OperationRoundResult operationRoundResult = RoundByFindArea(screen, "菜单", "返回");
		if (operationRoundResult.IsSuccess)
		{
			RoundByClickArea("菜单", "返回");
			return RoundWait(operationRoundResult.Status, null, _waitDelay);
		}
		return null;
	}

	private OperationRoundResult? MatchLoginError(Mat screen)
	{
		IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultList(screen);
		string[] array = new string[2] { "确定", "重试" };
		foreach (string targetWord in array)
		{
			OcrMatchResult ocrMatchResult = ocrResultList.FirstOrDefault((OcrMatchResult item) => StringUtils.FindByLcs(targetWord, item.Text, 0.5));
			if (ocrMatchResult != null && base.ZContext.Controller != null)
			{
				base.ZContext.Controller.Click(ocrMatchResult.Center);
				return RoundWait(targetWord, null, _waitDelay);
			}
		}
		return null;
	}

	private OperationRoundResult? IsInBigWorld(Mat screen)
	{
		string text = CheckAndUpdateCurrentScreen(screen, new string[2] { "大世界-普通", "大世界-勘域" });
		bool flag = ((text == "大世界-普通" || text == "大世界-勘域") ? true : false);
		return flag ? RoundSuccess("大世界", null, _waitDelay) : null;
	}

	private string? ReadEnterClickStatusText(Mat screen, bool includeEnterClick = false)
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("打开游戏", "进入游戏点击后状态");
		IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultList(screen, area?.ColorRange, area?.Rect);
		return MatchEnterClickStatusText(ocrResultList.Select((OcrMatchResult result) => result.Text), includeEnterClick);
	}
}
