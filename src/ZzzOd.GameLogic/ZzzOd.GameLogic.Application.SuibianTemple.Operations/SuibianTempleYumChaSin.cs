using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.SuibianTemple.Operations;

public sealed class SuibianTempleYumChaSin : SuibianTempleSubOperation
{
	private readonly bool _submitOnly;

	private readonly List<string> _doneProcurement = new List<string>();

	private readonly List<string> _doneMaterials = new List<string>();

	private readonly List<OneDragon.Core.Screen.ScreenArea> _doneMaterialPositions = new List<OneDragon.Core.Screen.ScreenArea>();

	private bool _doneCraft;

	private bool _skipAdventure;

	public SuibianTempleYumChaSin(ZContext context, SuibianTempleConfig config, bool submitOnly = false)
		: base(context, config, "随便观 饮茶仙")
	{
		_submitOnly = submitOnly;
	}

	[OperationNode("前往饮茶仙", IsStartNode = true)]
	public OperationRoundResult GoToYumChaSin()
	{
		return GoToScreenByText("随便观-饮茶仙", "邻里街坊", "饮茶仙");
	}

	[NodeFrom("前往饮茶仙")]
	[OperationNode("前往定期采办")]
	public OperationRoundResult GoToRegularProcurement()
	{
		return FindAndClickArea("随便观-饮茶仙", "按钮-定期采办");
	}

	[NodeFrom("前往定期采办")]
	[OperationNode("定期采办提交", NodeMaxRetryTimes = 2)]
	public OperationRoundResult RegularProcurementSubmit()
	{
		OperationRoundResult operationRoundResult = ClickText("确认", "已达上限");
		if (operationRoundResult.IsSuccess)
		{
			return (operationRoundResult.Status == "已达上限") ? RoundSuccess(operationRoundResult.Status) : RoundWait(operationRoundResult.Status, null, SuibianTempleSubOperation.OneSecond);
		}
		if (IsButtonAvailable("按钮-定期采办-提交"))
		{
			OperationRoundResult operationRoundResult2 = FindAndClickArea("随便观-饮茶仙", "按钮-定期采办-提交");
			if (operationRoundResult2.IsSuccess)
			{
				return RoundWait(operationRoundResult2.Status, null, SuibianTempleSubOperation.OneSecond);
			}
		}
		if (base.Config.YumChaSinPeriodRefresh && IsButtonAvailable("按钮-定期采办-刷新"))
		{
			OperationRoundResult operationRoundResult3 = FindAndClickArea("随便观-饮茶仙", "按钮-定期采办-刷新");
			if (operationRoundResult3.IsSuccess)
			{
				return RoundWait(operationRoundResult3.Status, null, SuibianTempleSubOperation.OneSecond);
			}
		}
		return RoundRetry("未发现可提交委托", null, SuibianTempleSubOperation.ShortDelay);
	}

	[NodeFrom("定期采办提交", Success = false)]
	[NodeFrom("返回定期采办")]
	[OperationNode("检查定期采办委托", NodeMaxRetryTimes = 2)]
	public OperationRoundResult CheckRegularProcurement()
	{
		if (_submitOnly)
		{
			return RoundSuccess("跳过缺失材料判断");
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("随便观-饮茶仙", "区域-任务列表");
		IReadOnlyList<OcrMatchResult> areaOcrResults = GetAreaOcrResults("随便观-饮茶仙", "区域-任务列表");
		string[] targetWords = new string[3] { "[随便观货品]", "精粹货品", "[随便观货品]精粹货品" };
		foreach (OcrMatchResult item in areaOcrResults)
		{
			if (StringUtils.FindBestMatchByDifflib(item.Text, targetWords).HasValue || StringUtils.FindBestMatchByDifflib(item.Text, _doneProcurement).HasValue)
			{
				continue;
			}
			_doneProcurement.Add(item.Text);
			_doneMaterialPositions.Clear();
			base.ZContext.Controller?.Click(item.Center);
			return RoundSuccess(item.Text, null, SuibianTempleSubOperation.OneSecond);
		}
		if (area != null)
		{
			base.ZContext.Controller?.DragTo(area.Center + new OneDragon.Core.Abstractions.Geometry.Point(0, -400), area.Center);
		}
		return RoundRetry("未发现新委托", null, TimeSpan.FromMilliseconds(500L));
	}

	[NodeFrom("检查定期采办委托")]
	[OperationNode("检查缺少的素材", NodeMaxRetryTimes = 2)]
	public OperationRoundResult CheckLackOfMaterial()
	{
		IReadOnlyList<IReadOnlyList<int>> colorRange = new IReadOnlyList<int>[2]
		{
			new int[3] { 220, 70, 30 },
			new int[3] { 230, 140, 110 }
		};
		foreach (OcrMatchResult result in GetAreaOcrResults("随便观-饮茶仙", "区域-材料数量", colorRange))
		{
			if (!SuibianTempleSubOperation.ExtractPositiveDigits(result.Text).HasValue || _doneMaterialPositions.Any((OneDragon.Core.Screen.ScreenArea position) => (double)Math.Abs(position.Center.Y - result.Center.Y) < (double)Math.Min(position.Height, result.Height) * 0.3))
			{
				continue;
			}
			for (int num = 1; num <= 3; num++)
			{
				OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("随便观-饮茶仙", $"区域-材料-{num}");
				if (area != null && !((double)Math.Abs(area.Center.Y - result.Center.Y) >= (double)Math.Min(area.Height, result.Height) * 0.3))
				{
					_doneMaterialPositions.Add(area);
					base.ZContext.Controller?.Click(area.Center);
					return RoundSuccess(null, null, SuibianTempleSubOperation.OneSecond);
				}
			}
		}
		return RoundRetry("未发现缺少的素材", null, TimeSpan.FromMilliseconds(500L));
	}

	[NodeFrom("检查缺少的素材")]
	[OperationNode("前往制作")]
	public OperationRoundResult GoToCraft()
	{
		_doneCraft = false;
		foreach (OcrMatchResult areaOcrResult in GetAreaOcrResults("随便观-饮茶仙", "区域-材料名称"))
		{
			if (StringUtils.FindBestMatchByDifflib(areaOcrResult.Text, _doneMaterials).HasValue)
			{
				return RoundSuccess("材料已处理过");
			}
			_doneMaterials.Add(areaOcrResult.Text);
		}
		return FindAndClickArea("随便观-饮茶仙", "按钮-制造");
	}

	[NodeFrom("前往制作", Status = "按钮-制造")]
	[OperationNode("制造派驻")]
	public OperationRoundResult CraftDispatch()
	{
		OperationResult result = new SuibianTempleCraftDispatch(base.ZContext, base.Config, fromCraft: false, new List<string>()).ExecuteAsync().GetAwaiter().GetResult();
		int doneCraft;
		if (result.IsSuccess)
		{
			object data = result.Data;
			doneCraft = ((data is bool && (bool)data) ? 1 : 0);
		}
		else
		{
			doneCraft = 0;
		}
		_doneCraft = (byte)doneCraft != 0;
		return RoundSuccess(result.Status);
	}

	[NodeFrom("制造派驻")]
	[OperationNode("前往游历")]
	public OperationRoundResult GoToAdventure()
	{
		if (_doneCraft || _skipAdventure)
		{
			return RoundSuccess("无需前往游历");
		}
		return FindAndClickArea("随便观-饮茶仙", "按钮-游历");
	}

	[NodeFrom("前往游历")]
	[OperationNode("派遣游历小队")]
	public OperationRoundResult DoAdventure()
	{
		OperationResult result = new SuibianTempleAdventureDispatch(base.ZContext, base.Config, base.Config.AdventureDuration).ExecuteAsync().GetAwaiter().GetResult();
		if (result.Status == "无法完成派遣")
		{
			_skipAdventure = true;
		}
		return RoundByOperationResult(result);
	}

	[NodeFrom("派遣游历小队")]
	[OperationNode("从游历返回材料菜单")]
	public OperationRoundResult BackToMaterialMenu2()
	{
		OperationRoundResult operationRoundResult = FindAndClickArea("随便观-饮茶仙", "按钮-返回");
		if (operationRoundResult.IsSuccess)
		{
			Thread.Sleep(TimeSpan.FromMilliseconds(500L));
			OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("随便观-饮茶仙", "按钮-返回");
			if (area != null)
			{
				base.ZContext.Controller?.MouseMove(area.RightBottom + new OneDragon.Core.Abstractions.Geometry.Point(50, 50));
			}
			return RoundWait(operationRoundResult.Status, null, TimeSpan.FromMilliseconds(500L));
		}
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? retryDelay = SuibianTempleSubOperation.ShortDelay;
		OperationRoundResult operationRoundResult2 = RoundByFindArea(lastScreenshot, "随便观-饮茶仙", "按钮-制造", null, retryDelay);
		return operationRoundResult2.IsSuccess ? RoundSuccess(operationRoundResult2.Status, null, SuibianTempleSubOperation.OneSecond) : RoundRetry("未找到返回按钮", null, SuibianTempleSubOperation.OneSecond);
	}

	private bool IsButtonAvailable(string areaName)
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("随便观-饮茶仙", areaName);
		if (base.LastScreenshot == null || base.LastScreenshot.Empty() || area == null)
		{
			return false;
		}
		using Mat buttonPart = CvImageUtils.Crop(base.LastScreenshot, area.Rect);
		return IsButtonAvailable(buttonPart);
	}

	private static bool IsButtonAvailable(Mat buttonPart)
	{
		if (buttonPart.Empty())
		{
			return false;
		}
		using Mat mat = new Mat();
		Cv2.CvtColor(buttonPart, mat, ColorConversionCodes.BGR2HSV);
		Mat[] array = Cv2.Split(mat);
		try
		{
			using Mat mat2 = new Mat();
			Cv2.Threshold(array[2], mat2, 229.5, 255.0, ThresholdTypes.Binary);
			double num = (double)Cv2.CountNonZero(mat2) / (double)Math.Max(1, buttonPart.Rows * buttonPart.Cols);
			return num > 0.8;
		}
		finally
		{
			Mat[] array2 = array;
			foreach (Mat mat3 in array2)
			{
				mat3.Dispose();
			}
		}
	}

	[NodeFrom("检查缺少的素材", Success = false)]
	[NodeFrom("前往制作", Status = "材料已处理过")]
	[NodeFrom("前往游历", Status = "无需前往游历")]
	[NodeFrom("从游历返回材料菜单")]
	[OperationNode("返回定期采办")]
	public OperationRoundResult BackToRegularProcurement()
	{
		if (CheckAndUpdateCurrentScreen(base.LastScreenshot, new string[] { "随便观-饮茶仙" }) != null)
		{
			return RoundSuccess("随便观-饮茶仙");
		}
		OperationRoundResult operationRoundResult = ClickArea("随便观-饮茶仙", "按钮-返回");
		return operationRoundResult.IsSuccess ? RoundRetry("未识别当前画面", null, TimeSpan.FromSeconds(2L)) : RoundRetry("未识别当前画面", null, SuibianTempleSubOperation.OneSecond);
	}

	[NodeFrom("定期采办提交", Status = "已达上限")]
	[NodeFrom("检查定期采办委托", Success = false)]
	[NodeFrom("检查定期采办委托", Status = "跳过缺失材料判断")]
	[OperationNode("返回随便观")]
	public OperationRoundResult BackToEntryNode()
	{
		return BackToEntry();
	}
}
