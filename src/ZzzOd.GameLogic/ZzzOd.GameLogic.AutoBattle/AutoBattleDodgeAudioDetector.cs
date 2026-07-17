using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using OneDragon.Core.Windows.Audio;

namespace ZzzOd.GameLogic.AutoBattle;

public sealed class AutoBattleDodgeAudioDetector : IAutoBattleDodgeAudioDetector
{
	private readonly AudioRecorder _audioRecorder;

	private readonly object _checkAudioLock = new object();

	private float[]? _audioTemplate;

	private double _lastCheckAudioTime;

	private readonly float _checkAudioInterval = 0.02f;

	public AutoBattleDodgeAudioDetector(AudioRecorder? audioRecorder = null)
	{
		_audioRecorder = audioRecorder ?? new AudioRecorder();
	}

	public bool CheckAudio(double screenshotTime)
	{
		if (!Monitor.TryEnter(_checkAudioLock))
		{
			return false;
		}
		try
		{
			if (screenshotTime - _lastCheckAudioTime < (double)_checkAudioInterval)
			{
				return false;
			}
			if (_audioTemplate == null)
			{
				return false;
			}
			_lastCheckAudioTime = screenshotTime;
			float[] latestAudioCopy = _audioRecorder.GetLatestAudioCopy();
			if (latestAudioCopy.Length == 0)
			{
				return false;
			}
			float[] y = AudioFilterUtils.HighPassFilter(latestAudioCopy);
			double maxCorr = AudioMathUtils.GetMaxCorr(_audioTemplate, y);
			if (maxCorr <= _audioRecorder.TriggerThreshold)
			{
				return false;
			}
			_audioRecorder.ClearAudio();
			return true;
		}
		finally
		{
			Monitor.Exit(_checkAudioLock);
		}
	}

	public void ResetBattle()
	{
		lock (_checkAudioLock)
		{
			_lastCheckAudioTime = 0.0;
		}
	}

	public void Start()
	{
		Task.Run((Action)InitAudioTemplate);
		_audioRecorder.StartRunningAsync();
	}

	public void Stop()
	{
		_audioRecorder.StopRunning();
	}

	private void InitAudioTemplate()
	{
		if (_audioTemplate != null)
		{
			return;
		}
		string text = Path.Combine("assets", "template", "dodge_audio", "template_1.wav");
		if (!File.Exists(text))
		{
			return;
		}
		using AudioFileReader audioFileReader = new AudioFileReader(text);
		float[] array = new float[audioFileReader.Length / 4];
		int newSize = audioFileReader.Read(array, 0, array.Length);
		Array.Resize(ref array, newSize);
		float[] samples = AudioRecorder.ConvertInterleavedToMonoSamples(array, audioFileReader.WaveFormat.Channels);
		float[] input = AudioRecorder.ResampleToTargetRate(samples, audioFileReader.WaveFormat.SampleRate, 32000);
		_audioTemplate = AudioFilterUtils.HighPassFilter(input);
	}
}
