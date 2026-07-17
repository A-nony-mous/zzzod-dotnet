using System;
using System.IO;
using System.Runtime.CompilerServices;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.E2E;

namespace ZzzOd.GameLogic.Tests.E2E;

/// <summary>
/// 测试 E2E profile 写入隔离。
/// </summary>
public sealed class E2EWriteIsolationTests : IDisposable
{
	private readonly string _rootDirectory;

	private readonly string _csharpRoot;

	private readonly string _pythonRoot;

	public E2EWriteIsolationTests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		_csharpRoot = Path.Combine(_rootDirectory, "csharp");
		_pythonRoot = Path.Combine(_rootDirectory, "python");
		Directory.CreateDirectory(_csharpRoot);
		Directory.CreateDirectory(_pythonRoot);
		Directory.CreateDirectory(Path.Combine(_pythonRoot, "assets", "template"));
		Directory.CreateDirectory(Path.Combine(_pythonRoot, "assets", "models"));
	}

	[Fact]
	public void ProfileSave_ShouldWriteOnlyToCSharpConfigDirectory()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment(_csharpRoot, _pythonRoot);
		YamlConfig<E2EAutomationProfile> yamlConfig = new YamlConfig<E2EAutomationProfile>(environment, "e2e_profile");
		yamlConfig.Update(delegate(E2EAutomationProfile profile)
		{
			profile.Enabled = true;
			profile.PythonReferenceRoot = _pythonRoot;
			profile.AssetsRoot = "assets";
		});
		Assert.True(File.Exists(Path.Combine(_csharpRoot, "config", "e2e_profile.yml")));
		Assert.False(File.Exists(Path.Combine(_pythonRoot, "config", "e2e_profile.yml")));
		Assert.Empty(Directory.GetFiles(Path.Combine(_pythonRoot, "assets"), "*", SearchOption.AllDirectories));
	}

	[Fact]
	public void RunRecordSave_ShouldWriteOnlyToCSharpInstanceDirectory()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment(_csharpRoot, _pythonRoot);
		DateTimeOffset now = new DateTimeOffset(2026, 7, 7, 10, 30, 0, TimeSpan.Zero);
		ZApplicationRunRecord zApplicationRunRecord = ZApplicationRunRecord.Load(environment, "coffee", 1, 0, ZApplicationRunRecordPeriod.Daily, () => now);
		zApplicationRunRecord.UpdateStatus(1);
		string[] buffer = new string[5];
		buffer[0] = _csharpRoot;
		buffer[1] = "config";
		buffer[2] = "01";
		buffer[3] = "app_run_record";
		buffer[4] = "coffee.yml";
		Assert.True(File.Exists(Path.Combine(buffer)));
		string[] buffer2 = new string[5];
		buffer2[0] = _pythonRoot;
		buffer2[1] = "config";
		buffer2[2] = "01";
		buffer2[3] = "app_run_record";
		buffer2[4] = "coffee.yml";
		Assert.False(File.Exists(Path.Combine(buffer2)));
		Assert.Empty(Directory.GetFiles(Path.Combine(_pythonRoot, "assets"), "*", SearchOption.AllDirectories));
	}

	public void Dispose()
	{
		if (Directory.Exists(_rootDirectory))
		{
			Directory.Delete(_rootDirectory, recursive: true);
		}
	}
}
