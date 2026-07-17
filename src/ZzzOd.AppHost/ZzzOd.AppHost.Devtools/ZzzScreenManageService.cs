using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using OneDragon.Core.Screen;
using OneDragon.Core.Template;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Config;

namespace ZzzOd.AppHost.Devtools;

public sealed class ZzzScreenManageService : IZzzScreenManageService
{
	private readonly Func<byte[]> _captureScreenshot;

	private readonly OneDragonEnvironment _environment;

	private readonly ScreenContext _screenContext;

	public ZzzScreenManageService(ZzzRunRoot runRoot, IZzzAppBackend backend)
		: this(runRoot, () => Capture(backend))
	{
	}

	internal ZzzScreenManageService(ZzzRunRoot runRoot, Func<byte[]> captureScreenshot)
	{
		_captureScreenshot = captureScreenshot;
		_environment = new OneDragonEnvironment(runRoot.Path);
		_screenContext = new ScreenContext(_environment, isDeveloperMode: true);
		_screenContext.Reload();
	}

	public IReadOnlyList<string> ListScreenNames()
	{
		return _screenContext.ScreenInfoList.Select((ScreenInfo screen) => screen.ScreenName).ToArray();
	}

	public ZzzScreenDocument LoadScreen(string screenName)
	{
		return ToDocument(_screenContext.GetScreen(screenName, copy: true));
	}

	public void SaveScreen(ZzzScreenDocument screen)
	{
		_screenContext.SaveScreen(ToScreenInfo(screen));
	}

	public void DeleteScreen(string screenId)
	{
		_screenContext.DeleteScreen(screenId);
	}

	public void RebuildMergedConfig()
	{
		_screenContext.RebuildMergedConfig();
	}

	public byte[] ReadImage(string filePath)
	{
		string fullPath = Path.GetFullPath(filePath);
		if (!File.Exists(fullPath))
		{
			throw new FileNotFoundException("图片不存在。", fullPath);
		}
		return File.ReadAllBytes(fullPath);
	}

	public byte[] CaptureScreenshot()
	{
		return _captureScreenshot();
	}

	private static byte[] Capture(IZzzAppBackend backend)
	{
		ZzzBackendResult<ZzzScreenshotDto> screenshot = backend.GetScreenshot();
		if (!screenshot.Success || (object)screenshot.Value == null)
		{
			throw new InvalidOperationException(screenshot.Error ?? "截图失败。");
		}
		return screenshot.Value.Bytes;
	}

	public ZzzImportedTemplateArea ImportTemplateArea(string templateConfigPath)
	{
		string fullPath = Path.GetFullPath(templateConfigPath);
		if (!File.Exists(fullPath) || !Path.GetExtension(fullPath).Equals(".yml", StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("请选择模板目录中的 YML 配置文件。");
		}
		DirectoryInfo directoryInfo = new DirectoryInfo(Path.GetDirectoryName(fullPath));
		DirectoryInfo parent = directoryInfo.Parent;
		if (parent == null)
		{
			throw new InvalidOperationException("模板目录结构无效。");
		}
		using TemplateInfo templateInfo = new TemplateInfo(_environment, parent.Name, directoryInfo.Name);
		templateInfo.UpdateTemplateShape("rectangle");
		Rect? templateRectByPoint = templateInfo.GetTemplateRectByPoint();
		ProjectConfig current = new YamlConfig<ProjectConfig>(_environment, "project").Current;
		int x = (templateRectByPoint.HasValue ? Math.Max(0, templateRectByPoint.Value.X1 - 10) : 0);
		int y = (templateRectByPoint.HasValue ? Math.Max(0, templateRectByPoint.Value.Y1 - 10) : 0);
		int x2 = (templateRectByPoint.HasValue ? Math.Min(current.ScreenStandardWidth, templateRectByPoint.Value.X2 + 10) : 0);
		int y2 = (templateRectByPoint.HasValue ? Math.Min(current.ScreenStandardHeight, templateRectByPoint.Value.Y2 + 10) : 0);
		return new ZzzImportedTemplateArea(templateInfo.TemplateName, x, y, x2, y2, parent.Name, directoryInfo.Name);
	}

	private static ZzzScreenDocument ToDocument(ScreenInfo screen)
	{
		return new ZzzScreenDocument(screen.OldScreenId, screen.ScreenId, screen.ScreenName, screen.AppId, screen.PcAlt, screen.AreaList.Select((ScreenArea area) => new ZzzScreenAreaDocument(area.AreaName, area.IdMark, area.X1, area.Y1, area.X2, area.Y2, area.Text, area.LcsPercent, area.TemplateSubDir, area.TemplateId, area.TemplateMatchThreshold, area.ColorRange, area.GotoList, area.GamepadKey)).ToArray());
	}

	private static ScreenInfo ToScreenInfo(ZzzScreenDocument document)
	{
		ScreenInfo screenInfo = new ScreenInfo
		{
			OldScreenId = document.OldScreenId,
			ScreenId = document.ScreenId,
			ScreenName = document.ScreenName,
			AppId = document.AppId,
			PcAlt = document.PcAlt
		};
		screenInfo.AreaList.AddRange(document.Areas.Select((ZzzScreenAreaDocument area) => new ScreenArea
		{
			AreaName = area.AreaName,
			IdMark = area.IdMark,
			PcRect = new Rect(area.X1, area.Y1, area.X2, area.Y2),
			Text = area.Text,
			LcsPercent = area.LcsPercent,
			TemplateSubDir = area.TemplateSubDir,
			TemplateId = area.TemplateId,
			TemplateMatchThreshold = area.TemplateMatchThreshold,
			ColorRange = area.ColorRange,
			GotoList = area.GotoList,
			GamepadKey = area.GamepadKey,
			PcAlt = document.PcAlt
		}));
		return screenInfo;
	}
}
