using System;
using System.Collections.Generic;
using System.Globalization;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Telemetry;

/// <summary>
/// 业务遥测入口。
/// </summary>
public sealed class TelemetryManager
{
	private readonly ZContext _context;

	private readonly ITelemetryRecorder _recorder;

	private readonly ITelemetryInfoProvider _infoProvider;

	private readonly TimeProvider _timeProvider;

	private readonly Guid _sessionId = Guid.NewGuid();

	private readonly DateTimeOffset _sessionStart;

	private TelemetryStaticInfo? _telemetryInfo;

	public Guid SessionId => _sessionId;

	public bool IsInitialized { get; private set; }

	public TelemetryManager(ZContext context, ITelemetryRecorder? recorder = null, ITelemetryInfoProvider? infoProvider = null, TimeProvider? timeProvider = null)
	{
		_context = context;
		_recorder = recorder ?? new AliyunWebTrackingRecorder("https://zzz-od-1.cn-hangzhou.log.aliyuncs.com/logstores/zzz-od-1/track?APIVersion=0.6.0");
		_infoProvider = infoProvider ?? new DefaultTelemetryInfoProvider(context);
		_timeProvider = timeProvider ?? TimeProvider.System;
		_sessionStart = _timeProvider.GetUtcNow();
	}

	public bool Initialize()
	{
		if (IsInitialized)
		{
			return true;
		}
		try
		{
			_telemetryInfo = _infoProvider.GetInfo();
			DateTimeOffset utcNow = _timeProvider.GetUtcNow();
			_recorder.Record("app_launched", CreatePayload(utcNow, new Dictionary<string, string>
			{
				["launch_time_seconds"] = FormatDuration(utcNow - _sessionStart),
				["platform"] = _telemetryInfo.Platform,
				["machine_id"] = _telemetryInfo.MachineId,
				["session_start"] = FormatTimestamp(_sessionStart)
			}));
			IsInitialized = true;
			return true;
		}
		catch (Exception exception)
		{
			_context.Logger.Debug(exception, "Telemetry init failed");
			_telemetryInfo = null;
			IsInitialized = false;
			return false;
		}
	}

	public void Shutdown()
	{
		if (!IsInitialized || (object)_telemetryInfo == null)
		{
			IsInitialized = false;
			return;
		}
		try
		{
			DateTimeOffset utcNow = _timeProvider.GetUtcNow();
			_recorder.Record("app_shutdown", CreatePayload(utcNow, new Dictionary<string, string>
			{
				["session_duration_seconds"] = FormatDuration(utcNow - _sessionStart),
				["clean_shutdown"] = bool.TrueString
			}));
		}
		catch (Exception exception)
		{
			_context.Logger.Debug(exception, "Telemetry shutdown failed");
		}
		finally
		{
			IsInitialized = false;
		}
	}

	private IReadOnlyDictionary<string, string> CreatePayload(DateTimeOffset timestamp, IReadOnlyDictionary<string, string> extraProperties)
	{
		TelemetryStaticInfo telemetryStaticInfo = _telemetryInfo ?? _infoProvider.GetInfo();
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["session_id"] = _sessionId.ToString(),
			["app_version"] = telemetryStaticInfo.AppVersion,
			["commit_version"] = telemetryStaticInfo.CommitVersion,
			["launcher_version"] = telemetryStaticInfo.LauncherVersion,
			["user_id"] = telemetryStaticInfo.UserId,
			["timestamp"] = FormatTimestamp(timestamp)
		};
		foreach (KeyValuePair<string, string> extraProperty in extraProperties)
		{
			extraProperty.Deconstruct(out var key, out var value);
			string key2 = key;
			string value2 = value;
			dictionary[key2] = value2;
		}
		return dictionary;
	}

	private static string FormatDuration(TimeSpan duration)
	{
		return duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
	}

	private static string FormatTimestamp(DateTimeOffset timestamp)
	{
		return timestamp.ToString("O", CultureInfo.InvariantCulture);
	}
}
