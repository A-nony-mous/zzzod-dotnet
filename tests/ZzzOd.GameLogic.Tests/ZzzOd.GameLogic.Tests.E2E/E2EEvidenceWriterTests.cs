using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using NAudio.Wave;
using OneDragon.Core.Runtime;
using OneDragon.Core.Windows.Audio;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.E2E;

namespace ZzzOd.GameLogic.Tests.E2E;

/// <summary>
/// 测试 E2E evidence writer。
/// </summary>
public sealed class E2EEvidenceWriterTests : IDisposable
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		Converters = { (JsonConverter)new JsonStringEnumConverter() }
	};

	private readonly string _rootDirectory;

	public E2EEvidenceWriterTests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_rootDirectory);
	}

	[Fact]
	public void Write_ShouldRecordSuccessfulRunAndScreenshotSummary()
	{
		CreateRequiredResourceTree(1);
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		E2EAutomationProfile profile = CreateProfile(1);
		E2EResourceValidationResult resourceValidation = new E2EResourceValidator().Validate(environment, profile);
		E2EEvidenceRecord e2EEvidenceRecord = E2EEvidenceRecord.Create("dotnet test --filter Category=E2E", environment, profile, resourceValidation, Path.Combine(_rootDirectory, "logs", "e2e.log"), new DateTimeOffset(2026, 7, 7, 1, 0, 0, TimeSpan.Zero));
		e2EEvidenceRecord.Screenshots.Add(new E2EScreenshotEvidence
		{
			Label = "first-frame",
			Path = Path.Combine(_rootDirectory, "screenshots", "first.png"),
			Summary = "1920x1080 wgc first frame",
			CapturedAtUtc = new DateTimeOffset(2026, 7, 7, 1, 0, 3, TimeSpan.Zero)
		});
		e2EEvidenceRecord.Audio = new E2EAudioEvidence
		{
			SourceSampleRate = 48000,
			SourceChannelCount = 2,
			TargetSampleRate = 32000,
			ResamplingMode = "linear",
			BufferDurationSeconds = 0.5
		};
		using Mat firstFrame = new Mat(new Size(1920, 1080), MatType.CV_8UC3, Scalar.All(0.0));
		e2EEvidenceRecord.CaptureReadiness = E2ECaptureReadinessEvidence.Succeeded(4660, "wgc", firstFrame, new DateTimeOffset(2026, 7, 7, 1, 0, 2, TimeSpan.Zero));
		e2EEvidenceRecord.Finish(E2EEvidenceStatus.Succeeded, new DateTimeOffset(2026, 7, 7, 1, 1, 0, TimeSpan.Zero));
		E2EEvidenceRecord e2EEvidenceRecord2 = WriteAndRead(e2EEvidenceRecord, "success.json");
		Assert.Equal(E2EEvidenceStatus.Succeeded, e2EEvidenceRecord2.Status);
		Assert.Equal("dotnet test --filter Category=E2E", e2EEvidenceRecord2.Command);
		Assert.True(e2EEvidenceRecord2.Profile.Enabled);
		Assert.Equal("wgc", e2EEvidenceRecord2.Profile.ScreenshotMethod);
		Assert.Equal("keyboard", e2EEvidenceRecord2.Profile.InputMode);
		int num = 2;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		span[0] = "coffee";
		span[1] = "lost_void";
		Assert.Equal<List<string>>(list, e2EEvidenceRecord2.ApplicationIds);
		Assert.All(e2EEvidenceRecord2.Resources, delegate(E2EEvidenceResourceSnapshot resource)
		{
			Assert.Equal(E2EResourceStatus.Present, resource.Status);
		});
		Assert.Equal("first-frame", e2EEvidenceRecord2.Screenshots[0].Label);
		Assert.Equal("1920x1080 wgc first frame", e2EEvidenceRecord2.Screenshots[0].Summary);
		Assert.NotNull(e2EEvidenceRecord2.Audio);
		Assert.Equal(48000, e2EEvidenceRecord2.Audio.SourceSampleRate);
		Assert.Equal(2, e2EEvidenceRecord2.Audio.SourceChannelCount);
		Assert.Equal(32000, e2EEvidenceRecord2.Audio.TargetSampleRate);
		Assert.Equal("linear", e2EEvidenceRecord2.Audio.ResamplingMode);
		Assert.Equal(0.5, e2EEvidenceRecord2.Audio.BufferDurationSeconds);
		Assert.NotNull(e2EEvidenceRecord2.CaptureReadiness);
		Assert.Equal(4660L, e2EEvidenceRecord2.CaptureReadiness.WindowHandle);
		Assert.Equal("wgc", e2EEvidenceRecord2.CaptureReadiness.ScreenshotMethod);
		Assert.Equal(1920, e2EEvidenceRecord2.CaptureReadiness.FirstFrameWidth);
		Assert.Equal(1080, e2EEvidenceRecord2.CaptureReadiness.FirstFrameHeight);
		Assert.Null(e2EEvidenceRecord2.CaptureReadiness.FailureReason);
	}

	[Fact]
	public void Write_ShouldRecordFailedRunReason()
	{
		CreateRequiredResourceTree(1);
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		E2EAutomationProfile profile = CreateProfile(1);
		E2EResourceValidationResult resourceValidation = new E2EResourceValidator().Validate(environment, profile);
		E2EEvidenceRecord e2EEvidenceRecord = E2EEvidenceRecord.Create("dotnet test --filter Category=E2E", environment, profile, resourceValidation, null, new DateTimeOffset(2026, 7, 7, 1, 0, 0, TimeSpan.Zero));
		e2EEvidenceRecord.Finish(E2EEvidenceStatus.Failed, new DateTimeOffset(2026, 7, 7, 1, 1, 0, TimeSpan.Zero), "战斗结算画面未出现");
		E2EEvidenceRecord e2EEvidenceRecord2 = WriteAndRead(e2EEvidenceRecord, "failed.json");
		Assert.Equal(E2EEvidenceStatus.Failed, e2EEvidenceRecord2.Status);
		Assert.Equal("战斗结算画面未出现", e2EEvidenceRecord2.FailureReason);
		Assert.Null(e2EEvidenceRecord2.LogPath);
	}

	[Fact]
	public void Write_ShouldRecordBlockedRunReasonAndMissingResources()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		E2EAutomationProfile profile = CreateProfile(3);
		E2EResourceValidationResult e2EResourceValidationResult = new E2EResourceValidator().Validate(environment, profile);
		E2EEvidenceRecord e2EEvidenceRecord = E2EEvidenceRecord.Create("dotnet test --filter Category=E2E", environment, profile, e2EResourceValidationResult, null, new DateTimeOffset(2026, 7, 7, 1, 0, 0, TimeSpan.Zero));
		e2EEvidenceRecord.Finish(E2EEvidenceStatus.Blocked, new DateTimeOffset(2026, 7, 7, 1, 1, 0, TimeSpan.Zero), e2EResourceValidationResult.FailureSummary);
		E2EEvidenceRecord e2EEvidenceRecord2 = WriteAndRead(e2EEvidenceRecord, "blocked.json");
		Assert.Equal(E2EEvidenceStatus.Blocked, e2EEvidenceRecord2.Status);
		Assert.Contains("assets/models", e2EEvidenceRecord2.FailureReason);
		Assert.Contains((IEnumerable<E2EEvidenceResourceSnapshot>)e2EEvidenceRecord2.Resources, (Predicate<E2EEvidenceResourceSnapshot>)((E2EEvidenceResourceSnapshot resource) => resource.Id == "config.instance" && resource.Status == E2EResourceStatus.Missing && resource.PythonSourcePath == "D:\\python-ref\\config\\03"));
	}

	[Fact]
	public void CaptureReadinessEvidence_ShouldRecordFailureReason()
	{
		E2ECaptureReadinessEvidence e2ECaptureReadinessEvidence = E2ECaptureReadinessEvidence.Failed(0, "print_window", "游戏窗口未就绪");
		Assert.Equal(0L, e2ECaptureReadinessEvidence.WindowHandle);
		Assert.Equal("print_window", e2ECaptureReadinessEvidence.ScreenshotMethod);
		Assert.Null(e2ECaptureReadinessEvidence.FirstFrameWidth);
		Assert.Null(e2ECaptureReadinessEvidence.FirstFrameHeight);
		Assert.Equal("游戏窗口未就绪", e2ECaptureReadinessEvidence.FailureReason);
	}

	[Fact]
	public void AudioEvidence_FromRecorderShouldRecordCaptureFormatAndResampling()
	{
		using AudioRecorder audioRecorder = new AudioRecorder();
		WaveFormat waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
		float[] array = new float[6] { 0.1f, 0.3f, 0.2f, 0.4f, 0.3f, 0.5f };
		byte[] array2 = new byte[array.Length * 4];
		Buffer.BlockCopy(array, 0, array2, 0, array2.Length);
		audioRecorder.ProcessCapturedBuffer(array2, array2.Length, waveFormat);
		E2EAudioEvidence e2EAudioEvidence = E2EAudioEvidence.From(audioRecorder);
		Assert.Equal(48000, e2EAudioEvidence.SourceSampleRate);
		Assert.Equal(2, e2EAudioEvidence.SourceChannelCount);
		Assert.Equal(32000, e2EAudioEvidence.TargetSampleRate);
		Assert.Equal("linear", e2EAudioEvidence.ResamplingMode);
		Assert.Equal(0.5, e2EAudioEvidence.BufferDurationSeconds);
	}

	private E2EEvidenceRecord WriteAndRead(E2EEvidenceRecord record, string fileName)
	{
		string path = new E2EEvidenceWriter(Path.Combine(_rootDirectory, "evidence")).Write(record, fileName);
		string json = File.ReadAllText(path);
		return JsonSerializer.Deserialize<E2EEvidenceRecord>(json, JsonOptions) ?? throw new InvalidOperationException("evidence JSON 反序列化失败。");
	}

	private static E2EAutomationProfile CreateProfile(int instanceIndex)
	{
		E2EAutomationProfile obj = new E2EAutomationProfile
		{
			Enabled = true,
			PythonReferenceRoot = "D:\\python-ref",
			InstanceIndex = instanceIndex,
			ScreenshotMethod = "wgc",
			InputMode = "keyboard",
			OcrProfile = "v6-small"
		};
		int num = 2;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		span[0] = "coffee";
		span[1] = "lost_void";
		obj.ApplicationIds = list;
		return obj;
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
