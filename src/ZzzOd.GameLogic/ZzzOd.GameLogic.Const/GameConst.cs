using System;
using OneDragon.Core.Runtime;
using OpenCvSharp;

namespace ZzzOd.GameLogic.Const;

/// <summary>
/// 绝区零共享常量。
/// </summary>
public static class GameConst
{
	public const string ChineseWindowTitle = "绝区零";

	public const string GlobalWindowTitle = "ZenlessZoneZero";

	public const string AssetsRootRelativePath = "assets";

	public const string TemplateRootRelativePath = "assets/template";

	public const string ModelRootRelativePath = "assets/models";

	public const string GameDataRootRelativePath = "assets/game_data";

	public const string ScreenInfoRootRelativePath = "assets/game_data/screen_info";

	public static readonly Scalar YoloDefaultColor = new Scalar(114.0, 114.0, 114.0);

	public static string ResolveWindowTitle(GameRegionEnum region, string? customWindowTitle = null)
	{
		if (!string.IsNullOrWhiteSpace(customWindowTitle))
		{
			return customWindowTitle;
		}
		bool flag = (uint)region <= 1u;
		return flag ? "绝区零" : "ZenlessZoneZero";
	}

	public static string GetTemplatePath(OneDragonEnvironment environment)
	{
		ArgumentNullException.ThrowIfNull(environment, "environment");
		return environment.GetResourcePath("assets", "template");
	}

	public static string GetModelPath(OneDragonEnvironment environment)
	{
		ArgumentNullException.ThrowIfNull(environment, "environment");
		return environment.GetResourcePath("assets", "models");
	}

	public static string GetGameDataPath(OneDragonEnvironment environment)
	{
		ArgumentNullException.ThrowIfNull(environment, "environment");
		return environment.GetResourcePath("assets", "game_data");
	}

	public static string GetScreenInfoPath(OneDragonEnvironment environment)
	{
		ArgumentNullException.ThrowIfNull(environment, "environment");
		return environment.GetResourcePath("assets", "game_data", "screen_info");
	}
}
