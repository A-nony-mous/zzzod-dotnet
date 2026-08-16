using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using OneDragon.Core.Operation;
using OneDragon.Core.Runtime;
using OneDragon.Core.Template;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.AutoBattle;

public sealed class AgentStateCheckerTests
{
	[Fact]
	public void GetTemplateId_UsesPythonPositionRules()
	{
		AgentStateDef stateDef = new AgentStateDef("能量", AgentStateCheckWay.COLOR_RANGE_EXIST, "energy");
		Assert.Equal("energy", AgentStateChecker.GetTemplateId(stateDef));
		Assert.Equal("energy_3_1", AgentStateChecker.GetTemplateId(stateDef, 2, 1));
		Assert.Equal("energy_2_2", AgentStateChecker.GetTemplateId(stateDef, 2, 2));
		Assert.Equal("energy_3_3", AgentStateChecker.GetTemplateId(stateDef, 3, 3));
	}

	[Fact]
	public void FilterByColor_RgbRangeAndEmptyRulesMatchPython()
	{
		using Mat mat = new Mat(2, 2, MatType.CV_8UC3, new Scalar(10.0, 20.0, 30.0));
		mat.Set(0, 0, new Vec3b(20, 20, 200));
		AgentStateDef stateDef = new AgentStateDef("红色", AgentStateCheckWay.COLOR_RANGE_EXIST, "", new int[3] { 180, 0, 0 }, new int[3] { 255, 60, 60 });
		using Mat mat2 = AgentStateChecker.FilterByColor(mat, stateDef);
		using Mat mat3 = AgentStateChecker.FilterByColor(mat, new AgentStateDef("全部"));
		Assert.Equal(255, mat2.At<byte>(0, 0));
		Assert.Equal(0, mat2.At<byte>(1, 1));
		Assert.Equal(255, mat3.At<byte>(0, 0));
		Assert.Equal(255, mat3.At<byte>(1, 1));
	}

	[Fact]
	public void FilterByColor_HsvRangeHandlesHueWrap()
	{
		using Mat mat = new Mat(1, 2, MatType.CV_8UC3, new Scalar(0.0, 255.0, 0.0));
		mat.Set(0, 0, new Vec3b(0, 0, byte.MaxValue));
		AgentStateDef stateDef = new AgentStateDef("红色", AgentStateCheckWay.COLOR_RANGE_EXIST, "", null, null, new int[3] { 0, 255, 255 }, new int[3] { 10, 20, 20 });
		using Mat mat2 = AgentStateChecker.FilterByColor(mat, stateDef);
		Assert.Equal(255, mat2.At<byte>(0, 0));
		Assert.Equal(0, mat2.At<byte>(0, 1));
	}

	[Fact]
	public void CountByColorRange_UsesConnectedComponentsAndAreaThreshold()
	{
		// 对齐 Python dilate：2×2 全一核单次膨胀，3×3 方块膨胀为 4×4=16，单像素膨胀为 2×2=4
		using Mat mat = new Mat(20, 20, MatType.CV_8UC3, new Scalar(0.0, 0.0, 0.0));
		Cv2.Rectangle(mat, new Rect(1, 1, 3, 3), new Scalar(0.0, 0.0, 255.0), -1);
		Cv2.Rectangle(mat, new Rect(12, 12, 3, 3), new Scalar(0.0, 0.0, 255.0), -1);
		mat.Set(10, 1, new Vec3b(0, 0, byte.MaxValue));
		AgentStateDef stateDef = new AgentStateDef("红块", AgentStateCheckWay.COLOR_RANGE_EXIST, "", connectCnt: 10, lowerColor: new int[3] { 200, 0, 0 }, upperColor: new int[3] { 255, 50, 50 });
		int actual = AgentStateChecker.CountByColorRange(mat, stateDef);
		Assert.Equal(2, actual);
	}

	[Fact]
	public void ResolveJaneSahofuJump_MatchesReferenceParameters()
	{
		AgentStateDef resolved = AgentStateChecker.ResolveStateDef(new AgentStateDef("简-萨霍夫跳"));
		Assert.Equal(AgentStateCheckWay.COLOR_RANGE_EXIST, resolved.CheckWay);
		Assert.Equal("jane_attack", resolved.TemplateId);
		Assert.Equal(5, resolved.ConnectCnt);
		Assert.Equal(new int[3] { 0, 255, 255 }, resolved.HsvColor);
		Assert.Equal(new int[3] { 10, 100, 150 }, resolved.HsvColorDiff);
		Assert.Null(resolved.LowerColor);
		Assert.Null(resolved.UpperColor);
	}

	[Fact]
	public void ExistsByColorRange_JaneSahofuJumpHitInHsvRange()
	{
		// 纯红 BGR(0,0,255) → HSV(0,255,255)，落在 jane_attack (0,255,255)±(10,100,150) 内
		using Mat mat = new Mat(10, 10, MatType.CV_8UC3, new Scalar(0.0, 0.0, 0.0));
		Cv2.Rectangle(mat, new Rect(2, 2, 3, 3), new Scalar(0.0, 0.0, 255.0), -1);
		AgentStateDef stateDef = AgentStateChecker.ResolveStateDef(new AgentStateDef("简-萨霍夫跳"));
		Assert.Equal(1, AgentStateChecker.ExistsByColorRange(mat, stateDef));
	}

	[Fact]
	public void ExistsByColorRange_JaneSahofuJumpConnectCnt5Boundary()
	{
		// 2×2 膨胀后面积：单像素→4(<5 不触发)，两相邻像素→6(≥5 触发)
		AgentStateDef stateDef = AgentStateChecker.ResolveStateDef(new AgentStateDef("简-萨霍夫跳"));
		using Mat single = new Mat(10, 10, MatType.CV_8UC3, new Scalar(0.0, 0.0, 0.0));
		single.Set(5, 5, new Vec3b(0, 0, byte.MaxValue));
		Assert.Equal(0, AgentStateChecker.ExistsByColorRange(single, stateDef));
		using Mat pair = new Mat(10, 10, MatType.CV_8UC3, new Scalar(0.0, 0.0, 0.0));
		pair.Set(5, 5, new Vec3b(0, 0, byte.MaxValue));
		pair.Set(6, 5, new Vec3b(0, 0, byte.MaxValue));
		Assert.Equal(1, AgentStateChecker.ExistsByColorRange(pair, stateDef));
	}

	[Fact]
	public void CountByColorRange_JaneSahofuJumpColorRangeFiltersBySaturationAndValue()
	{
		// 纯红 3×3 命中(S=255,V=255)；灰色 S=0、深红 V=100 均越出 (0,255,255)±(10,100,150)
		AgentStateDef stateDef = AgentStateChecker.ResolveStateDef(new AgentStateDef("简-萨霍夫跳"));
		using Mat mat = new Mat(10, 10, MatType.CV_8UC3, new Scalar(0.0, 0.0, 0.0));
		Cv2.Rectangle(mat, new Rect(1, 1, 3, 3), new Scalar(0.0, 0.0, 255.0), -1);
		mat.Set(8, 8, new Vec3b(100, 100, 100));
		mat.Set(7, 8, new Vec3b(0, 0, 100));
		Assert.Equal(1, AgentStateChecker.CountByColorRange(mat, stateDef));
	}

	[Fact]
	public void LengthByForegroundColor_UsesBoundingWidthAndMaxLength()
	{
		using Mat mat = new Mat(1, 10, MatType.CV_8UC3, new Scalar(60.0, 60.0, 60.0));
		Cv2.Rectangle(mat, new Rect(0, 0, 5, 1), new Scalar(0.0, 0.0, 255.0), -1);
		AgentStateDef stateDef = new AgentStateDef("彩色长度", AgentStateCheckWay.COLOR_RANGE_EXIST, "", new int[3] { 200, 0, 0 }, new int[3] { 255, 50, 50 });
		int actual = AgentStateChecker.LengthByForegroundColor(mat, stateDef);
		Assert.Equal(50, actual);
	}

	[Fact]
	public void LengthByForegroundColor_UsesBgrScreenshotForYeShunGuangMingXinJing()
	{
		using Mat hsv = new Mat(1, 182, MatType.CV_8UC3, new Scalar(113.0, 75.0, 255.0));
		using Mat screenshot = new Mat();
		Cv2.CvtColor(hsv, screenshot, ColorConversionCodes.HSV2BGR);
		AgentStateDef stateDef = new AgentStateDef("叶瞬光-明心境", AgentStateCheckWay.FOREGROUND_COLOR_RANGE_LENGTH, "yeshunguang_mingxinjing", null, null, new int[3] { 113, 75, 255 }, new int[3] { 10, 50, 50 }, null, null, 120);

		int actual = AgentStateChecker.LengthByForegroundColor(screenshot, stateDef);

		Assert.Equal(120, actual);
	}

	[Fact]
	public void LengthByForegroundGray_RemovesSplitColorRange()
	{
		using Mat mat = new Mat(1, 10, MatType.CV_8UC3, new Scalar(60.0, 60.0, 60.0));
		Cv2.Rectangle(mat, new Rect(0, 0, 5, 1), new Scalar(200.0, 200.0, 200.0), -1);
		Cv2.Rectangle(mat, new Rect(5, 0, 2, 1), new Scalar(10.0, 10.0, 10.0), -1);
		IReadOnlyList<int> lowerColor = new int[] { 100 };
		IReadOnlyList<int> upperColor = new int[] { 255 };
		IReadOnlyList<int> splitColorRange = new int[2] { 0, 30 };
		AgentStateDef stateDef = new AgentStateDef("灰度前景", AgentStateCheckWay.COLOR_RANGE_EXIST, "", lowerColor, upperColor, null, null, null, splitColorRange, 120);
		int actual = AgentStateChecker.LengthByForegroundGray(mat, stateDef);
		Assert.Equal(75, actual);
	}

	[Fact]
	public void LengthByForegroundGray_UsesBgrScreenshotColorContract()
	{
		using Mat screenshot = new Mat(1, 1, MatType.CV_8UC3, new Scalar(255.0, 0.0, 0.0));
		AgentStateDef stateDef = new AgentStateDef("蓝色灰度", AgentStateCheckWay.FOREGROUND_GRAY_RANGE_LENGTH, "", new int[] { 20 }, new int[] { 40 });

		int actual = AgentStateChecker.LengthByForegroundGray(screenshot, stateDef);

		Assert.Equal(100, actual);
	}

	[Fact]
	public void LengthByBackgroundGray_InfersForegroundFromBackgroundRange()
	{
		using Mat mat = new Mat(1, 10, MatType.CV_8UC3, new Scalar(20.0, 20.0, 20.0));
		Cv2.Rectangle(mat, new Rect(0, 0, 6, 1), new Scalar(200.0, 200.0, 200.0), -1);
		AgentStateDef stateDef = new AgentStateDef("灰度背景", AgentStateCheckWay.COLOR_RANGE_EXIST, "", new int[] { 0 }, new int[] { 30 });
		int actual = AgentStateChecker.LengthByBackgroundGray(mat, stateDef);
		Assert.Equal(60, actual);
	}

	[Fact]
	public void ChannelMaxAndEqualRange_ChecksMatchPythonReturnShape()
	{
		using Mat mat = new Mat(5, 5, MatType.CV_8UC3, new Scalar(0.0, 0.0, 0.0));
		mat.Set(2, 2, new Vec3b(200, 20, 20));
		// 对齐 Python 2×2 膨胀：单像素膨胀为 2×2=4，connect_cnt 需 ≤4
		AgentStateDef stateDef = new AgentStateDef("通道最大值", AgentStateCheckWay.COLOR_RANGE_EXIST, "", new int[] { 150 }, new int[] { 255 }, null, null, 4);
		using Mat mat2 = new Mat(3, 3, MatType.CV_8UC3, new Scalar(10.0, 20.0, 30.0));
		mat2.Set(0, 0, new Vec3b(40, 40, 40));
		mat2.Set(0, 1, new Vec3b(40, 40, 40));
		mat2.Set(1, 0, new Vec3b(40, 40, 40));
		mat2.Set(1, 1, new Vec3b(40, 40, 40));
		AgentStateDef stateDef2 = new AgentStateDef("三通道相等", AgentStateCheckWay.COLOR_RANGE_EXIST, "", null, null, null, null, 4);
		Assert.Equal(1, AgentStateChecker.ExistsByColorChannelMaxRange(mat, stateDef));
		Assert.Equal(1, AgentStateChecker.CountByColorChannelEqualRange(mat2, stateDef2));
	}

	[Fact]
	public void CheckTemplateFoundAndNotFound_UsesInMemoryTemplate()
	{
		// TM_CCOEFF_NORMED 对零方差模板退化（结果恒 1），与 Python 一致使用带纹理模板
		using Mat template = new Mat(3, 3, MatType.CV_8UC3, new Scalar(255.0, 255.0, 255.0));
		template.Set(2, 2, new Vec3b(0, 0, 0));
		using Mat mat = new Mat(8, 8, MatType.CV_8UC3, new Scalar(0.0, 0.0, 0.0));
		Cv2.Rectangle(mat, new Rect(4, 4, 3, 3), new Scalar(255.0, 255.0, 255.0), -1);
		mat.Set(6, 6, new Vec3b(0, 0, 0));
		using Mat source = new Mat(8, 8, MatType.CV_8UC3, new Scalar(0.0, 0.0, 0.0));
		Assert.Equal(1, AgentStateChecker.CheckTemplateFound(mat, template, 0.99));
		Assert.Equal(1, AgentStateChecker.CheckTemplateNotFound(source, template, 0.99));
	}

	[Fact]
	public void CheckTemplateFound_UniformBrightRegionIsNotAMatch()
	{
		// 回归：CCorrNormed 下亮色模板对任意亮色区域给出 ~0.94 的假命中（叶瞬光-常态 恒真问题），
		// CCoeffNormed（Python TM_CCOEFF_NORMED）下均值中心化后相关性为 0
		using Mat template = new Mat(3, 3, MatType.CV_8UC3, new Scalar(255.0, 255.0, 255.0));
		template.Set(2, 2, new Vec3b(0, 0, 0));
		using Mat source = new Mat(8, 8, MatType.CV_8UC3, new Scalar(200.0, 200.0, 200.0));
		Assert.Equal(0, AgentStateChecker.CheckTemplateFound(source, template, 0.8));
	}

	[Fact]
	public void CheckStateValue_WithContextSupportsTemplateFoundAndNotFound()
	{
		string text = CreateTempRoot();
		try
		{
			WriteAgentStateTemplate(text, "probe");
			using ZContext ctx = new ZContext(new OneDragonEnvironment(text));
			using Mat mat = new Mat(10, 10, MatType.CV_8UC3, new Scalar(0.0, 0.0, 0.0));
			Cv2.Rectangle(mat, new Rect(2, 2, 3, 3), new Scalar(255.0, 255.0, 255.0), -1);
			mat.Set(4, 4, new Vec3b(0, 0, 0));
			using Mat screen = new Mat(10, 10, MatType.CV_8UC3, new Scalar(0.0, 0.0, 0.0));
			double? templateThreshold = 0.99;
			AgentStateDef stateDef = new AgentStateDef("模板存在", AgentStateCheckWay.TEMPLATE_FOUND, "probe", null, null, null, null, null, null, 100, null, templateThreshold);
			templateThreshold = 0.99;
			AgentStateDef stateDef2 = new AgentStateDef("模板不存在", AgentStateCheckWay.TEMPLATE_NOT_FOUND, "probe", null, null, null, null, null, null, 100, null, templateThreshold);
			Assert.Equal(1, AgentStateChecker.CheckStateValue(ctx, mat, stateDef));
			Assert.Equal(1, AgentStateChecker.CheckStateValue(ctx, screen, stateDef2));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ResolveStateDef_UsesPythonCommonAgentStateParameters()
	{
		AgentStateDef agentStateDef = AgentStateChecker.ResolveStateDef(CommonAgentStateEnum.ENERGY_31.Value);
		AgentStateDef agentStateDef2 = AgentStateChecker.ResolveStateDef(CommonAgentStateEnum.SWITCH_BAN.Value);
		Assert.Equal("energy_3_1", agentStateDef.TemplateId);
		Assert.Equal(AgentStateCheckWay.FOREGROUND_GRAY_RANGE_LENGTH, agentStateDef.CheckWay);
		Assert.Equal(120, agentStateDef.MaxLength);
		Assert.Equal("switch_ban", agentStateDef2.TemplateId);
		Assert.Equal(0, agentStateDef2.MinValueTriggerState);
		Assert.Equal(AgentStateCheckWay.COLOR_RANGE_EXIST, agentStateDef2.CheckWay);
	}

	[Fact]
	public void CheckAgentRelatedState_ConvertsCheckerValueToStateRecord()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		AutoBattleAgentContext autoBattleAgentContext = new AutoBattleAgentContext(ctx);
		using Mat mat = new Mat(10, 10, MatType.CV_8UC3, new Scalar(0.0, 0.0, 0.0));
		Cv2.Rectangle(mat, new Rect(1, 1, 3, 3), new Scalar(0.0, 0.0, 255.0), -1);
		AgentStateDef stateDef = new AgentStateDef("测试状态", AgentStateCheckWay.COLOR_RANGE_EXIST, "", new int[3] { 200, 0, 0 }, new int[3] { 255, 50, 50 }, null, null, 10);
		StateRecord stateRecord = autoBattleAgentContext.CheckAgentRelatedState(mat, stateDef, 3.0);
		Assert.NotNull(stateRecord);
		Assert.Equal("测试状态", stateRecord.StateName);
		Assert.Equal(3.0, stateRecord.TriggerTime);
		Assert.Equal(1, stateRecord.Value);
		Assert.False(stateRecord.IsClear);
	}

	[Fact]
	public void CheckAgentRelatedState_RespectsMinValueTriggerState()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		AutoBattleAgentContext autoBattleAgentContext = new AutoBattleAgentContext(ctx);
		using Mat image = new Mat(10, 10, MatType.CV_8UC3, new Scalar(0.0, 0.0, 0.0));
		IReadOnlyList<int> lowerColor = new int[3] { 200, 0, 0 };
		IReadOnlyList<int> upperColor = new int[3] { 255, 50, 50 };
		int? minValueTriggerState = 1;
		AgentStateDef stateDef = new AgentStateDef("测试状态", AgentStateCheckWay.COLOR_RANGE_EXIST, "", lowerColor, upperColor, null, null, null, null, 100, minValueTriggerState);
		StateRecord stateRecord = autoBattleAgentContext.CheckAgentRelatedState(image, stateDef, 3.0);
		Assert.Null(stateRecord);
	}

	[Fact]
	public void CheckAgentRelatedState_ClearsSwitchBanAndGuardBreakWhenValueIsZero()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext ctx = new ZContext(environment);
		AutoBattleAgentContext autoBattleAgentContext = new AutoBattleAgentContext(ctx);
		using Mat image = new Mat(10, 10, MatType.CV_8UC3, new Scalar(0.0, 0.0, 0.0));
		string stateName = CommonAgentStateEnum.SWITCH_BAN.Value.StateName;
		IReadOnlyList<int> lowerColor = new int[3] { 200, 0, 0 };
		IReadOnlyList<int> upperColor = new int[3] { 255, 50, 50 };
		int? minValueTriggerState = 0;
		AgentStateDef stateDef = new AgentStateDef(stateName, AgentStateCheckWay.COLOR_RANGE_EXIST, "", lowerColor, upperColor, null, null, null, null, 100, minValueTriggerState);
		StateRecord stateRecord = autoBattleAgentContext.CheckAgentRelatedState(image, stateDef, 4.0);
		Assert.NotNull(stateRecord);
		Assert.Equal(0, stateRecord.Value);
		Assert.True(stateRecord.IsClear);
	}

	[Fact]
	public void ProductionTemplateIndex_DiscoversJaneAttack31Template()
	{
		OpenCvTestRuntime.RequireAvailable();
		string workspaceRoot = FindWorkspaceRoot();
		string runRoot = Path.Combine(Path.GetTempPath(), "zzzod-agent-state-template-tests", Guid.NewGuid().ToString("N"));
		try
		{
			CopyDirectory(Path.Combine(workspaceRoot, "config"), Path.Combine(runRoot, "config"));
			using ZContext zContext = new ZContext(new OneDragonEnvironment(runRoot, workspaceRoot));
			TemplateInfo template = Assert.IsType<TemplateInfo>(zContext.TemplateLoader.GetTemplate("agent_state", "jane_attack_3_1"));
			using (template)
			{
				Assert.False(template.Raw?.Empty() ?? true, "jane_attack_3_1 raw.png 必须可被模板加载器读取");
				Assert.False(template.Mask?.Empty() ?? true, "jane_attack_3_1 mask.png 必须可被模板加载器读取");
			}
		}
		finally
		{
			if (Directory.Exists(runRoot))
			{
				Directory.Delete(runRoot, recursive: true);
			}
		}
	}

	private static string FindWorkspaceRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (Directory.Exists(Path.Combine(directoryInfo.FullName, "assets")) && Directory.Exists(Path.Combine(directoryInfo.FullName, "zzzod-dotnet")))
			{
				return directoryInfo.FullName;
			}
		}
		throw new DirectoryNotFoundException("未找到 zzz-od-dotnet 工作区根目录。");
	}

	private static void CopyDirectory(string sourceDirectory, string targetDirectory)
	{
		foreach (string item in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
		{
			string relativePath = Path.GetRelativePath(sourceDirectory, item);
			string text = Path.Combine(targetDirectory, relativePath);
			Directory.CreateDirectory(Path.GetDirectoryName(text));
			File.Copy(item, text, overwrite: true);
		}
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static void WriteAgentStateTemplate(string rootDirectory, string templateId)
	{
		string[] buffer = new string[5];
		buffer[0] = rootDirectory;
		buffer[1] = "assets";
		buffer[2] = "template";
		buffer[3] = "agent_state";
		buffer[4] = templateId;
		string text = Path.Combine(buffer);
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "config.yml"), $"sub_dir: agent_state\ntemplate_id: {templateId}\ntemplate_name: {templateId}\ntemplate_shape: rectangle\nauto_mask: true\npoint_list:\n- 2, 2\n- 5, 5");
		// 模板需带纹理：TM_CCOEFF_NORMED 对零方差模板退化（结果恒 1）
		using Mat img = new Mat(3, 3, MatType.CV_8UC3, new Scalar(255.0, 255.0, 255.0));
		img.Set(2, 2, new Vec3b(0, 0, 0));
		using Mat img2 = new Mat(3, 3, MatType.CV_8UC1, Scalar.White);
		Cv2.ImWrite(Path.Combine(text, "raw.png"), img);
		Cv2.ImWrite(Path.Combine(text, "mask.png"), img2);
	}
}
