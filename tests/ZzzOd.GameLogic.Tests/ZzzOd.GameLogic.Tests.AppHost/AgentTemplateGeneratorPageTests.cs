using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Runtime;
using OneDragon.Core.Template;
using OpenCvSharp;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Pages.Devtools;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class AgentTemplateGeneratorPageTests
{
	private class AgentTemplateBackendProxy : DispatchProxy
	{
		public string RunRoot { get; set; } = string.Empty;

		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
		{
			string text = targetMethod?.Name;
			if (1 == 0)
			{
			}
			if (text == "GetHealth")
			{
				ZzzBackendResult<ZzzHealthDto> result = ZzzBackendResult<ZzzHealthDto>.Ok(new ZzzHealthDto(ZzzHostMode.Gui, "test", RunRoot, ApiEnabled: false, ContextReady: true, 0));
				if (1 == 0)
				{
				}
				return result;
			}
			throw new NotSupportedException(targetMethod?.Name);
		}
	}

	[Fact]
	public void AgentTemplatePageUsesAxamlFluentControlsWithoutDemoAgent()
	{
		string path = FindDevtoolsDirectory();
		string actualString = File.ReadAllText(Path.Combine(path, "ZzzAgentTemplateGeneratorPage.axaml"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "ZzzAgentTemplateGeneratorPage.axaml.cs"));
		Assert.Contains("fa:SettingsExpanderItem", actualString, StringComparison.Ordinal);
		Assert.Contains("fa:CommandBar", actualString, StringComparison.Ordinal);
		Assert.Contains("输入代理人英文名", actualString, StringComparison.Ordinal);
		Assert.Contains("一键生成", actualString, StringComparison.Ordinal);
		Assert.Contains("选择截图", actualString, StringComparison.Ordinal);
		Assert.Contains("游戏截图", actualString, StringComparison.Ordinal);
		Assert.Contains("保存", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("anby", actualString, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("来源", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("Python", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString2, StringComparison.Ordinal);
	}

	[Fact]
	public void SaveTemplateUsesReferenceGeometryAndProductionTemplateDirectory()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-agent-template-page-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		try
		{
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			using (TemplateInfo templateInfo = new TemplateInfo(environment, "template", "avatar_1_template"))
			{
				templateInfo.TemplateShape = "rectangle";
				templateInfo.AutoMask = true;
				templateInfo.SetPointList(new OneDragon.Core.Abstractions.Geometry.Point[2]
				{
					new OneDragon.Core.Abstractions.Geometry.Point(10, 10),
					new OneDragon.Core.Abstractions.Geometry.Point(30, 30)
				});
				templateInfo.SaveConfig();
			}
			string text2 = Path.Combine(text, "screen.png");
			using (Mat img = new Mat(60, 60, MatType.CV_8UC3, new Scalar(20.0, 40.0, 60.0)))
			{
				Cv2.Rectangle(img, new OpenCvSharp.Point(10, 10), new OpenCvSharp.Point(29, 29), new Scalar(220.0, 120.0, 20.0), -1);
				Cv2.ImWrite(text2, img);
			}
			IZzzAppBackend zzzAppBackend = DispatchProxy.Create<IZzzAppBackend, AgentTemplateBackendProxy>();
			((AgentTemplateBackendProxy)zzzAppBackend).RunRoot = text;
			ZzzAgentTemplateGeneratorState zzzAgentTemplateGeneratorState = new ZzzAgentTemplateGeneratorState(zzzAppBackend);
			Assert.True(zzzAgentTemplateGeneratorState.SetAgentId("anby"));
			zzzAgentTemplateGeneratorState.ChooseScreenshot(0, text2);
			Assert.NotNull(zzzAgentTemplateGeneratorState.Cards[0].PreviewBytes);
			string actual = zzzAgentTemplateGeneratorState.SaveTemplate(0);
			string[] buffer = new string[5];
			buffer[0] = text;
			buffer[1] = "assets";
			buffer[2] = "template";
			buffer[3] = "battle";
			buffer[4] = "avatar_1_anby";
			string path = Path.Combine(buffer);
			Assert.Equal(Path.Combine(path, "raw.png"), actual);
			Assert.True(File.Exists(Path.Combine(path, "config.yml")));
			Assert.True(File.Exists(Path.Combine(path, "raw.png")));
			Assert.True(File.Exists(Path.Combine(path, "mask.png")));
			using Mat mat = Cv2.ImRead(Path.Combine(path, "raw.png"));
			Assert.Equal(20, mat.Width);
			Assert.Equal(20, mat.Height);
			Assert.False(Directory.Exists(Path.Combine(text, ".debug", "devtools-gui", "agent-template")));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	private static string FindDevtoolsDirectory()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string[] buffer = new string[5];
			buffer[0] = directoryInfo.FullName;
			buffer[1] = "src";
			buffer[2] = "ZzzOd.Gui";
			buffer[3] = "Pages";
			buffer[4] = "Devtools";
			string text = Path.Combine(buffer);
			if (Directory.Exists(text))
			{
				return text;
			}
			string[] buffer2 = new string[6];
			buffer2[0] = directoryInfo.FullName;
			buffer2[1] = "zzzod-dotnet";
			buffer2[2] = "src";
			buffer2[3] = "ZzzOd.Gui";
			buffer2[4] = "Pages";
			buffer2[5] = "Devtools";
			text = Path.Combine(buffer2);
			if (Directory.Exists(text))
			{
				return text;
			}
		}
		throw new DirectoryNotFoundException("未找到 Devtools 页面目录。");
	}
}
