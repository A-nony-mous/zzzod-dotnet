using System;
using System.Collections.Generic;
using System.IO;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.E2E;

namespace ZzzOd.GameLogic.Tests.E2E;

/// <summary>
/// 测试 E2E 资源定位与校验。
/// </summary>
public sealed class E2EResourceValidatorTests : IDisposable
{
	private readonly string _rootDirectory;

	public E2EResourceValidatorTests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_rootDirectory);
	}

	[Fact]
	public void Validate_ShouldPassWhenRequiredAssetsAndConfigsExist()
	{
		CreateRequiredResourceTree(1);
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		E2EAutomationProfile profile = new E2EAutomationProfile
		{
			InstanceIndex = 1,
			PythonReferenceRoot = "D:\\python-ref"
		};
		E2EResourceValidationResult e2EResourceValidationResult = new E2EResourceValidator().Validate(environment, profile);
		Assert.True(e2EResourceValidationResult.IsValid);
		Assert.Empty(e2EResourceValidationResult.MissingItems);
		Assert.All(e2EResourceValidationResult.Items, delegate(E2EResourceValidationItem item)
		{
			Assert.Equal(E2EResourceStatus.Present, item.Status);
		});
	}

	[Fact]
	public void Validate_ShouldReportLocalAndPythonCopyPathsForMissingResources()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		E2EAutomationProfile profile = new E2EAutomationProfile
		{
			InstanceIndex = 3,
			PythonReferenceRoot = "D:\\python-ref"
		};
		E2EResourceValidationResult result = new E2EResourceValidator().Validate(environment, profile);
		Assert.False(result.IsValid);
		Assert.Contains((IEnumerable<E2EResourceValidationItem>)result.MissingItems, (Predicate<E2EResourceValidationItem>)((E2EResourceValidationItem item) => item.Id == "assets.models" && item.LocalPath == Path.Combine(_rootDirectory, "assets", "models") && item.PythonSourcePath == "D:\\python-ref\\assets\\models"));
		Assert.Contains((IEnumerable<E2EResourceValidationItem>)result.MissingItems, (Predicate<E2EResourceValidationItem>)((E2EResourceValidationItem item) => item.Id == "config.instance" && item.LocalPath == Path.Combine(_rootDirectory, "config", "03") && item.PythonSourcePath == "D:\\python-ref\\config\\03"));
		Assert.Contains("D:\\python-ref\\assets\\models", result.FailureSummary);
		Assert.Throws<InvalidOperationException>(delegate
		{
			result.EnsureValid();
		});
	}

	private void CreateRequiredResourceTree(int instanceIndex)
	{
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "models"));
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "template"));
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "game_data", "screen_info"));
		CreateYaml(Path.Combine(_rootDirectory, "config", "auto_battle", "全配队通用.sample.yml"));
		CreateYaml(Path.Combine(_rootDirectory, "config", "dodge", "闪避.sample.yml"));
		CreateYaml(Path.Combine(_rootDirectory, "config", "lost_void_challenge", "默认.sample.yml"));
		CreateYaml(Path.Combine(_rootDirectory, "config", "hollow_zero_challenge", "默认.sample.yml"));
		CreateYaml(Path.Combine(_rootDirectory, "config", instanceIndex.ToString("00"), "game.yml"));
	}

	private static void CreateYaml(string filePath)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(filePath));
		File.WriteAllText(filePath, "enabled: true");
	}

	public void Dispose()
	{
		if (Directory.Exists(_rootDirectory))
		{
			Directory.Delete(_rootDirectory, recursive: true);
		}
	}
}
