using System;
using System.IO;
using System.Runtime.CompilerServices;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Template;
using OpenCvSharp;
using Xunit;
using ZzzOd.Gui.Pages.Devtools;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// 验证模板管理页使用真实 BaselineParity 兼容模板目录、图片和 AXAML 控件。
/// </summary>
public sealed class TemplateHelperParityTests : IDisposable
{
	private readonly string _runRoot = Path.Combine(Path.GetTempPath(), "zzzod-template-helper-tests", Guid.NewGuid().ToString("N"));

	/// <summary>
	/// 配置、原图和掩码应写入真实 assets/template 目录。
	/// </summary>
	[Fact]
	public void ServiceReadsAndWritesPythonCompatibleTemplateAssets()
	{
		Directory.CreateDirectory(_runRoot);
		ZzzTemplateHelperService zzzTemplateHelperService = new ZzzTemplateHelperService(_runRoot);
		using Mat mat = new Mat(180, 240, MatType.CV_8UC3, new Scalar(15.0, 90.0, 180.0));
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(30, 40, 90, 80), new Scalar(200.0, 100.0, 40.0), -1);
		Cv2.ImEncode(".png", mat, out byte[] buf);
		using TemplateInfo templateInfo = zzzTemplateHelperService.Create("battle", "button_confirm");
		templateInfo.TemplateName = "确认按钮";
		templateInfo.TemplateShape = "rectangle";
		templateInfo.AutoMask = true;
		zzzTemplateHelperService.SetScreenImage(templateInfo, buf);
		templateInfo.SetPointList(new OneDragon.Core.Abstractions.Geometry.Point[2]
		{
			new OneDragon.Core.Abstractions.Geometry.Point(30, 40),
			new OneDragon.Core.Abstractions.Geometry.Point(120, 120)
		});
		zzzTemplateHelperService.SaveConfig(templateInfo);
		zzzTemplateHelperService.SaveRaw(templateInfo);
		zzzTemplateHelperService.SaveMask(templateInfo);
		string[] buffer = new string[6];
		buffer[0] = _runRoot;
		buffer[1] = "assets";
		buffer[2] = "template";
		buffer[3] = "battle";
		buffer[4] = "button_confirm";
		buffer[5] = "config.yml";
		Assert.True(File.Exists(Path.Combine(buffer)));
		string[] buffer2 = new string[6];
		buffer2[0] = _runRoot;
		buffer2[1] = "assets";
		buffer2[2] = "template";
		buffer2[3] = "battle";
		buffer2[4] = "button_confirm";
		buffer2[5] = "raw.png";
		Assert.True(File.Exists(Path.Combine(buffer2)));
		string[] buffer3 = new string[6];
		buffer3[0] = _runRoot;
		buffer3[1] = "assets";
		buffer3[2] = "template";
		buffer3[3] = "battle";
		buffer3[4] = "button_confirm";
		buffer3[5] = "mask.png";
		Assert.True(File.Exists(Path.Combine(buffer3)));
		string actualString = File.ReadAllText(templateInfo.ConfigPath);
		Assert.Contains("template_name: 确认按钮", actualString, StringComparison.Ordinal);
		Assert.Contains("template_shape: rectangle", actualString, StringComparison.Ordinal);
		Assert.Contains("30, 40", actualString, StringComparison.Ordinal);
		using Mat mat2 = Cv2.ImRead(templateInfo.RawPath);
		using Mat mat3 = Cv2.ImRead(templateInfo.MaskPath, ImreadModes.Grayscale);
		Assert.Equal(90, mat2.Width);
		Assert.Equal(80, mat2.Height);
		Assert.Equal(mat2.Size(), mat3.Size());
		Assert.True(Cv2.CountNonZero(mat3) > 0);
		TemplateOption templateOption = Assert.Single(zzzTemplateHelperService.GetTemplates());
		Assert.Equal("确认按钮", templateOption.Label);
		Assert.Equal("battle", templateOption.SubDir);
		Assert.Equal("button_confirm", templateOption.TemplateId);
	}

	/// <summary>
	/// 复制和删除应操作真实模板目录。
	/// </summary>
	[Fact]
	public void CopyAndDeleteOperateOnRealTemplateDirectories()
	{
		Directory.CreateDirectory(_runRoot);
		ZzzTemplateHelperService zzzTemplateHelperService = new ZzzTemplateHelperService(_runRoot);
		using TemplateInfo templateInfo = zzzTemplateHelperService.Create("battle", "source");
		templateInfo.TemplateName = "源模板";
		templateInfo.SaveConfig();
		using (Mat img = new Mat(12, 16, MatType.CV_8UC3, Scalar.White))
		{
			Cv2.ImWrite(templateInfo.RawPath, img);
		}
		using TemplateInfo templateInfo2 = zzzTemplateHelperService.Copy(templateInfo);
		Assert.Equal("source_copy", templateInfo2.TemplateId);
		Assert.True(File.Exists(templateInfo2.ConfigPath));
		Assert.True(File.Exists(templateInfo2.RawPath));
		Assert.True(zzzTemplateHelperService.Delete(templateInfo2));
		Assert.False(Directory.Exists(templateInfo2.DirectoryPath));
	}

	/// <summary>
	/// 模板身份不得越过 assets/template 边界。
	/// </summary>
	[Theory]
	[InlineData(new object[] { "../battle", "id" })]
	[InlineData(new object[] { "battle", "../id" })]
	[InlineData(new object[] { "", "id" })]
	[InlineData(new object[] { "battle", "" })]
	public void ServiceRejectsTemplatePathsOutsideAssetsTemplate(string subDir, string templateId)
	{
		ZzzTemplateHelperService service = new ZzzTemplateHelperService(_runRoot);
		Assert.Throws<InvalidOperationException>(delegate
		{
			service.ValidateIdentity(subDir, templateId);
		});
	}

	/// <summary>
	/// 页面应使用 AXAML Fluent 控件并移除示例与来源说明。
	/// </summary>
	[Fact]
	public void AxamlUsesFluentControlsAndContainsNoExampleOrSourceUi()
	{
		string path = FindGuiRoot();
		string actualString = File.ReadAllText(Path.Combine(path, "Pages", "Devtools", "ZzzTemplateHelperPage.axaml"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "Pages", "Devtools", "ZzzTemplateHelperPage.cs"));
		Assert.Contains("<fa:CommandBar", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:FAComboBox", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:SettingsExpanderItem", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:TeachingTip", actualString, StringComparison.Ordinal);
		Assert.Contains("模板原图", actualString, StringComparison.Ordinal);
		Assert.Contains("模板掩码", actualString, StringComparison.Ordinal);
		Assert.Contains("模板抠图", actualString, StringComparison.Ordinal);
		Assert.Contains("反向抠图", actualString, StringComparison.Ordinal);
		Assert.Contains("Ctrl+Z 撤回，Ctrl+Shift+Z 恢复", actualString, StringComparison.Ordinal);
		Assert.Contains("assets", actualString2, StringComparison.Ordinal);
		Assert.Contains("template.SaveConfig()", actualString2, StringComparison.Ordinal);
		Assert.Contains("template.SaveRaw()", actualString2, StringComparison.Ordinal);
		Assert.Contains("template.SaveMask()", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("PageModel 摘要", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("Python 来源", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("battle/avatar", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("avatar_1_anby", actualString, StringComparison.Ordinal);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (Directory.Exists(_runRoot))
		{
			Directory.Delete(_runRoot, recursive: true);
		}
	}

	private static string FindGuiRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string text = Path.Combine(directoryInfo.FullName, "src", "ZzzOd.Gui");
			if (Directory.Exists(text))
			{
				return text;
			}
		}
		throw new DirectoryNotFoundException("找不到 ZzzOd.Gui 源码目录。");
	}
}
