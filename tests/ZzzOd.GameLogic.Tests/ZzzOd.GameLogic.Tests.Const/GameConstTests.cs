using System.Collections.Generic;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Const;

namespace ZzzOd.GameLogic.Tests.Const;

public sealed class GameConstTests
{
	[Theory]
	[InlineData(new object[]
	{
		GameRegionEnum.CN,
		"绝区零"
	})]
	[InlineData(new object[]
	{
		GameRegionEnum.CNB,
		"绝区零"
	})]
	[InlineData(new object[]
	{
		GameRegionEnum.AMERICA,
		"ZenlessZoneZero"
	})]
	[InlineData(new object[]
	{
		GameRegionEnum.EUROPE,
		"ZenlessZoneZero"
	})]
	[InlineData(new object[]
	{
		GameRegionEnum.ASIA,
		"ZenlessZoneZero"
	})]
	[InlineData(new object[]
	{
		GameRegionEnum.TWHKMO,
		"ZenlessZoneZero"
	})]
	public void ResolveWindowTitle_ShouldMatchRegion(GameRegionEnum region, string expectedTitle)
	{
		Assert.Equal(expectedTitle, GameConst.ResolveWindowTitle(region));
	}

	[Fact]
	public void ResolveWindowTitle_ShouldPreferCustomWindowTitle()
	{
		Assert.Equal("My Custom Window", GameConst.ResolveWindowTitle(GameRegionEnum.CN, "My Custom Window"));
	}

	[Fact]
	public void ResourcePaths_ShouldMatchPythonStructure()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("D:/work", "D:/resource");
		Assert.Equal("assets/template", "assets/template");
		Assert.Equal("assets/models", "assets/models");
		Assert.Equal("assets/game_data", "assets/game_data");
		Assert.Equal("assets/game_data/screen_info", "assets/game_data/screen_info");
		Assert.Equal("D:\\resource\\assets\\game_data\\screen_info", GameConst.GetScreenInfoPath(environment));
	}

	[Fact]
	public void ApplicationIds_ShouldExposeKnownBusinessApplications()
	{
		Assert.Equal(29, ZzzApplicationIds.All.Count);
		Assert.Contains("auto_battle", (IEnumerable<string>)ZzzApplicationIds.All);
		Assert.Contains("coffee", (IEnumerable<string>)ZzzApplicationIds.All);
		Assert.Contains("lost_void", (IEnumerable<string>)ZzzApplicationIds.All);
		Assert.Contains("withered_domain", (IEnumerable<string>)ZzzApplicationIds.All);
		Assert.Contains("world_patrol", (IEnumerable<string>)ZzzApplicationIds.All);
	}

	[Fact]
	public void SharedConstants_ShouldExposeExpectedValues()
	{
		Assert.Equal(new Scalar(114.0, 114.0, 114.0), GameConst.YoloDefaultColor);
		Assert.Equal("unknown", "unknown");
		Assert.Equal("全配队通用", "全配队通用");
	}

	[Fact]
	public void YoloModelConfigConstants_ShouldExposeReleaseUrls()
	{
		Assert.Equal("zzz_model", "zzz_model");
		Assert.EndsWith("/zzz_model", "https://github.com/OneDragon-Anything/OneDragon-YOLO/releases/download/zzz_model");
		Assert.EndsWith("/zzz_model", "https://gitee.com/OneDragon-Anything/OneDragon-YOLO/releases/download/zzz_model");
	}
}
