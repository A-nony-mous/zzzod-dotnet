using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.E2E;

namespace ZzzOd.GameLogic.Tests.E2E;

/// <summary>
/// 测试实机 E2E 自动化配置模型。
/// </summary>
public sealed class E2EAutomationProfileTests : IDisposable
{
	private readonly string _rootDirectory;

	public E2EAutomationProfileTests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_rootDirectory);
	}

	[Fact]
	public void Defaults_ShouldBeGuardedAndPointToWorkspaceAssetsAndConfig()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		E2EAutomationProfile e2EAutomationProfile = E2EAutomationProfile.Load(environment);
		Assert.False(e2EAutomationProfile.Enabled);
		Assert.Equal("C:\\Users\\Anonymous\\IdeaProjects\\ZenlessZoneZero-OneDragon", e2EAutomationProfile.PythonReferenceRoot);
		Assert.Equal(0, e2EAutomationProfile.InstanceIndex);
		Assert.Equal("auto", e2EAutomationProfile.ScreenshotMethod);
		Assert.Equal("keyboard", e2EAutomationProfile.InputMode);
		Assert.Equal("evidence\\e2e", e2EAutomationProfile.EvidenceOutputDirectory);
		Assert.Empty(e2EAutomationProfile.ApplicationIds);
		Assert.Equal(Path.Combine(_rootDirectory, "assets"), e2EAutomationProfile.ResolveAssetsRoot(environment));
		Assert.Equal(Path.Combine(_rootDirectory, "config"), e2EAutomationProfile.ResolveConfigRoot(environment));
		Assert.Equal(Path.Combine(_rootDirectory, "config", "00"), e2EAutomationProfile.ResolveInstanceConfigRoot(environment));
		Assert.Equal(Path.Combine(_rootDirectory, "evidence", "e2e"), e2EAutomationProfile.ResolveEvidenceOutputDirectory(environment));
	}

	[Fact]
	public void Load_ShouldDeserializeExplicitProfileFields()
	{
		string text = Path.Combine(_rootDirectory, "config");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "e2e_profile.yml"), "enabled: true\npython_reference_root: 'D:\\python-ref'\nassets_root: copied-assets\nconfig_root: copied-config\ninstance_index: 3\nscreenshot_method: wgc\ninput_mode: xbox\nocr_profile: v6-small\nmodel_profile: yolo-local\nevidence_output_directory: evidence/current-run\napplication_ids:\n  - shiyu_defense\n  - lost_void\n  - ''\n  - shiyu_defense");
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		E2EAutomationProfile e2EAutomationProfile = E2EAutomationProfile.Load(environment);
		Assert.True(e2EAutomationProfile.Enabled);
		Assert.Equal("D:\\python-ref", e2EAutomationProfile.PythonReferenceRoot);
		Assert.Equal("copied-assets", e2EAutomationProfile.AssetsRoot);
		Assert.Equal("copied-config", e2EAutomationProfile.ConfigRoot);
		Assert.Equal(3, e2EAutomationProfile.InstanceIndex);
		Assert.Equal("wgc", e2EAutomationProfile.ScreenshotMethod);
		Assert.Equal("xbox", e2EAutomationProfile.InputMode);
		Assert.Equal("v6-small", e2EAutomationProfile.OcrProfile);
		Assert.Equal("yolo-local", e2EAutomationProfile.ModelProfile);
		Assert.Equal("evidence/current-run", e2EAutomationProfile.EvidenceOutputDirectory);
		int num = 2;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		span[0] = "shiyu_defense";
		span[1] = "lost_void";
		Assert.Equal<List<string>>(list, e2EAutomationProfile.ApplicationIds);
		Assert.Equal(Path.Combine(_rootDirectory, "copied-assets"), e2EAutomationProfile.ResolveAssetsRoot(environment));
		Assert.Equal(Path.Combine(_rootDirectory, "copied-config", "03"), e2EAutomationProfile.ResolveInstanceConfigRoot(environment));
		Assert.Equal(Path.Combine(_rootDirectory, "evidence", "current-run"), e2EAutomationProfile.ResolveEvidenceOutputDirectory(environment));
	}

	public void Dispose()
	{
		if (Directory.Exists(_rootDirectory))
		{
			Directory.Delete(_rootDirectory, recursive: true);
		}
	}
}
