using System;
using OneDragon.Core.Windows.Audio;

namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// E2E 音频采集 evidence。
/// </summary>
public sealed class E2EAudioEvidence
{
	public int SourceSampleRate { get; set; }

	public int SourceChannelCount { get; set; }

	public int TargetSampleRate { get; set; }

	public string ResamplingMode { get; set; } = string.Empty;

	public double BufferDurationSeconds { get; set; }

	/// <summary>
	/// 从音频 recorder 创建 evidence。
	/// </summary>
	/// <param name="recorder">音频 recorder。</param>
	/// <returns>音频 evidence。</returns>
	public static E2EAudioEvidence From(AudioRecorder recorder)
	{
		ArgumentNullException.ThrowIfNull(recorder, "recorder");
		return new E2EAudioEvidence
		{
			SourceSampleRate = recorder.SourceSampleRate,
			SourceChannelCount = recorder.SourceChannelCount,
			TargetSampleRate = 32000,
			ResamplingMode = recorder.ResamplingMode,
			BufferDurationSeconds = 0.5
		};
	}
}
