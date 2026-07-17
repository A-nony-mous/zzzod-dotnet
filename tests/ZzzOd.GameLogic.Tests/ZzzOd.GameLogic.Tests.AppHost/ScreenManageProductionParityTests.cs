using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Devtools;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ScreenManageProductionParityTests
{
	[Fact]
	public void ScreenServicePersistsReloadsDeletesAndRebuildsRealYaml()
	{
		string text = NewRoot();
		try
		{
			ZzzScreenManageService zzzScreenManageService = new ZzzScreenManageService(new ZzzRunRoot(text), () => new byte[3] { 1, 2, 3 });
			zzzScreenManageService.SaveScreen(new ZzzScreenDocument(string.Empty, "battle_main", "战斗主画面", string.Empty, PcAlt: true, new ZzzScreenAreaDocument[] { new ZzzScreenAreaDocument("确认", IdMark: true, 1, 2, 30, 40, "确认", 0.6, "battle", "button_ok", 0.8, new IReadOnlyList<int>[2]
			{
				new int[3] { 1, 2, 3 },
				new int[3] { 4, 5, 6 }
			}, new string[] { "下一画面" }, "A") }));
			ZzzScreenDocument zzzScreenDocument = zzzScreenManageService.LoadScreen("战斗主画面");
			Assert.Equal("battle_main", zzzScreenDocument.ScreenId);
			Assert.True(zzzScreenDocument.PcAlt);
			Assert.Equal(30, Assert.Single(zzzScreenDocument.Areas).X2);
			Assert.Equal<byte[]>(new byte[3] { 1, 2, 3 }, zzzScreenManageService.CaptureScreenshot());
			string path = Path.Combine(text, "assets", "game_data", "screen_info");
			Assert.True(File.Exists(Path.Combine(path, "battle_main.yml")));
			zzzScreenManageService.RebuildMergedConfig();
			Assert.Contains("battle_main", File.ReadAllText(Path.Combine(path, "_od_merged.yml")), StringComparison.Ordinal);
			zzzScreenManageService.DeleteScreen("battle_main");
			Assert.Empty(zzzScreenManageService.ListScreenNames());
			Assert.False(File.Exists(Path.Combine(path, "battle_main.yml")));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void TemplateConfigImportsRealRectangleWithPythonPadding()
	{
		string text = NewRoot();
		try
		{
			string[] buffer = new string[5];
			buffer[0] = text;
			buffer[1] = "assets";
			buffer[2] = "template";
			buffer[3] = "battle";
			buffer[4] = "button_ok";
			string text2 = Path.Combine(buffer);
			Directory.CreateDirectory(text2);
			string text3 = Path.Combine(text2, "config.yml");
			File.WriteAllText(text3, "template_name: 确认按钮\ntemplate_shape: rectangle\npoint_list:\n- 20, 30\n- 100, 120");
			ZzzScreenManageService zzzScreenManageService = new ZzzScreenManageService(new ZzzRunRoot(text), () => Array.Empty<byte>());
			ZzzImportedTemplateArea zzzImportedTemplateArea = zzzScreenManageService.ImportTemplateArea(text3);
			Assert.Equal("确认按钮", zzzImportedTemplateArea.AreaName);
			Assert.Equal((10, 20, 110, 130), (zzzImportedTemplateArea.X1, zzzImportedTemplateArea.Y1, zzzImportedTemplateArea.X2, zzzImportedTemplateArea.Y2));
			Assert.Equal("battle", zzzImportedTemplateArea.TemplateSubDir);
			Assert.Equal("button_ok", zzzImportedTemplateArea.TemplateId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ScreenManageAxamlContainsPythonControlContractAndNoDemoRows()
	{
		string text = FindRepositoryRoot();
		string[] buffer = new string[6];
		buffer[0] = text;
		buffer[1] = "src";
		buffer[2] = "ZzzOd.Gui";
		buffer[3] = "Pages";
		buffer[4] = "Devtools";
		buffer[5] = "ZzzScreenManagePage.axaml";
		string text2 = File.ReadAllText(Path.Combine(buffer));
		string[] buffer2 = new string[6];
		buffer2[0] = text;
		buffer2[1] = "src";
		buffer2[2] = "ZzzOd.Gui";
		buffer2[3] = "Pages";
		buffer2[4] = "Devtools";
		buffer2[5] = "ZzzScreenAreaTable.axaml";
		string text3 = File.ReadAllText(Path.Combine(buffer2));
		Assert.Contains("更新合并配置文件", text2, StringComparison.Ordinal);
		Assert.Contains("选择已有", text2, StringComparison.Ordinal);
		Assert.Contains("导入模板区域", text2, StringComparison.Ordinal);
		Assert.Contains("画面信息", text2, StringComparison.Ordinal);
		Assert.Contains("区域表格", text2, StringComparison.Ordinal);
		Assert.Contains("鼠标点击坐标", text2, StringComparison.Ordinal);
		Assert.Contains("fa:CommandBar", text2, StringComparison.Ordinal);
		Assert.Contains("fa:FAComboBox", text2, StringComparison.Ordinal);
		Assert.Contains("fa:NumberBox", text3, StringComparison.Ordinal);
		Assert.DoesNotContain("主按钮", text2 + text3, StringComparison.Ordinal);
		Assert.DoesNotContain("Python", text2 + text3, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("PageModel", text2 + text3, StringComparison.OrdinalIgnoreCase);
	}

	private static string NewRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzz-screen-manage-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static string FindRepositoryRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (File.Exists(Path.Combine(directoryInfo.FullName, "ZzzOneDragon.slnx")))
			{
				return directoryInfo.FullName;
			}
		}
		throw new DirectoryNotFoundException("未找到 zzzod-dotnet 仓库根目录。");
	}
}
