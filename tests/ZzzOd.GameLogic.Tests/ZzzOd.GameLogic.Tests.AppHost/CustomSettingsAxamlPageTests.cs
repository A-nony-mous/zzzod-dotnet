using System;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;
using ZzzOd.Gui.Services.LauncherMedia;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// 自定义设置页 AXAML、真实配置和主题媒体行为审计。
/// </summary>
public sealed class CustomSettingsAxamlPageTests
{
	/// <summary>
	/// BaselineParity 支持的 AVI 和 MOV 背景文件应通过真实媒体校验。
	/// </summary>
	[Theory]
	[InlineData(new object[] { ".avi" })]
	[InlineData(new object[] { ".mov" })]
	public void LauncherMediaAcceptsPythonVideoExtensions(string extension)
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-custom-media-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		string path = Path.Combine(text, "background" + extension);
		try
		{
			File.WriteAllBytes(path, "RIFF0000AVI "u8.ToArray());
			ZzzLauncherMediaService.ValidateCustomBackground(path);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}
}
