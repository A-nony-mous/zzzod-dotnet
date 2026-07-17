using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NAudio.Wave;
using OneDragon.Core.Windows.Audio;
using Xunit;

namespace ZzzOd.GameLogic.Tests.TestData;

public sealed class TestDataAssetTests
{
	[Fact]
	public void PythonAssetSamples_AreCopiedToTestOutput()
	{
		string path = Path.Combine(AppContext.BaseDirectory, "TestData", "PythonAssets");
		string text = Path.Combine(path, "yaml", "agent_anby.yml");
		string text2 = Path.Combine(path, "images", "agent_state_raw.png");
		string text3 = Path.Combine(path, "audio", "dodge_audio_template_1.wav");
		Assert.True(File.Exists(text), text);
		Assert.True(File.Exists(text2), text2);
		Assert.True(File.Exists(text3), text3);
		string actualString = File.ReadAllText(text);
		byte[] source = File.ReadAllBytes(text2);
		byte[] array = File.ReadAllBytes(text3);
		Assert.Contains("agent_name: \"安比\"", actualString);
		Assert.Equal(new byte[4] { 137, 80, 78, 71 }, source.Take(4).ToArray());
		Assert.Equal(new byte[4] { 82, 73, 70, 70 }, array.Take(4).ToArray());
		Assert.True(array.Length > 1000);
	}

	[Fact]
	public void PythonDodgeAudioTemplate_ParsesAndCorrelatesWithItsFilteredSignal()
	{
		string[] buffer = new string[5];
		buffer[0] = AppContext.BaseDirectory;
		buffer[1] = "TestData";
		buffer[2] = "PythonAssets";
		buffer[3] = "audio";
		buffer[4] = "dodge_audio_template_1.wav";
		string fileName = Path.Combine(buffer);
		using AudioFileReader audioFileReader = new AudioFileReader(fileName);
		float[] array = new float[audioFileReader.Length / 4];
		int newSize = audioFileReader.Read(array, 0, array.Length);
		Array.Resize(ref array, newSize);
		float[] array2 = AudioFilterUtils.HighPassFilter(array, audioFileReader.WaveFormat.SampleRate);
		double maxCorr = AudioMathUtils.GetMaxCorr(array2, array2);
		Assert.Equal(48000, audioFileReader.WaveFormat.SampleRate);
		Assert.NotEmpty(array2);
		Assert.Contains((IEnumerable<float>)array2, (Predicate<float>)((float sample) => Math.Abs(sample) > float.Epsilon));
		Assert.InRange(maxCorr, 0.99, 1.01);
	}
}
