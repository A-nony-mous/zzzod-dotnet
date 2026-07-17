using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Telemetry;

namespace ZzzOd.GameLogic.Tests.Telemetry;

public sealed class TelemetryTests
{
	private sealed class RecordingTelemetryRecorder : ITelemetryRecorder
	{
		public List<TelemetryEvent> Events { get; } = new List<TelemetryEvent>();

		public void Record(string eventName, IReadOnlyDictionary<string, string> properties)
		{
			Events.Add(new TelemetryEvent(eventName, new Dictionary<string, string>(properties)));
		}
	}

	private sealed class ThrowingTelemetryRecorder : ITelemetryRecorder
	{
		public void Record(string eventName, IReadOnlyDictionary<string, string> properties)
		{
			throw new InvalidOperationException("boom");
		}
	}

	private sealed class FakeTelemetryInfoProvider : ITelemetryInfoProvider
	{
		public TelemetryStaticInfo Info { get; } = new TelemetryStaticInfo("user-1", "app-1", "commit-1", "launcher-1", "Windows", "machine-1");

		public TelemetryStaticInfo GetInfo()
		{
			return Info;
		}
	}

	private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
	{
		private DateTimeOffset _utcNow = utcNow;

		public override DateTimeOffset GetUtcNow()
		{
			return _utcNow;
		}

		public void Advance(TimeSpan delta)
		{
			_utcNow = _utcNow.Add(delta);
		}
	}

	private sealed class RecordingHttpMessageHandler : HttpMessageHandler
	{
		private readonly List<HttpRequestMessage> _requests = new List<HttpRequestMessage>();

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			_requests.Add(request);
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
		}

		public Task<HttpRequestMessage> GetSingleRequestAsync()
		{
			return Task.FromResult(Assert.Single(_requests));
		}
	}

	[Fact]
	public void Initialize_RecordsLaunchEventWithReplaceableRecorder()
	{
		string text = CreateTempRoot();
		try
		{
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			using ZContext context = new ZContext(environment);
			FakeTimeProvider fakeTimeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 7, 5, 1, 0, 0, TimeSpan.Zero));
			RecordingTelemetryRecorder recordingTelemetryRecorder = new RecordingTelemetryRecorder();
			FakeTelemetryInfoProvider fakeTelemetryInfoProvider = new FakeTelemetryInfoProvider();
			TelemetryManager telemetryManager = new TelemetryManager(context, recordingTelemetryRecorder, fakeTelemetryInfoProvider, fakeTimeProvider);
			fakeTimeProvider.Advance(TimeSpan.FromSeconds(12L));
			bool condition = telemetryManager.Initialize();
			Assert.True(condition);
			Assert.True(telemetryManager.IsInitialized);
			TelemetryEvent telemetryEvent = Assert.Single(recordingTelemetryRecorder.Events);
			Assert.Equal("app_launched", telemetryEvent.EventName);
			Assert.Equal(fakeTelemetryInfoProvider.Info.UserId, telemetryEvent.Properties["user_id"]);
			Assert.Equal(fakeTelemetryInfoProvider.Info.AppVersion, telemetryEvent.Properties["app_version"]);
			Assert.Equal(fakeTelemetryInfoProvider.Info.CommitVersion, telemetryEvent.Properties["commit_version"]);
			Assert.Equal(fakeTelemetryInfoProvider.Info.LauncherVersion, telemetryEvent.Properties["launcher_version"]);
			Assert.Equal(fakeTelemetryInfoProvider.Info.Platform, telemetryEvent.Properties["platform"]);
			Assert.Equal(fakeTelemetryInfoProvider.Info.MachineId, telemetryEvent.Properties["machine_id"]);
			Assert.Equal("12", telemetryEvent.Properties["launch_time_seconds"]);
			Assert.Equal("2026-07-05T01:00:00.0000000+00:00", telemetryEvent.Properties["session_start"]);
			Assert.Equal("2026-07-05T01:00:12.0000000+00:00", telemetryEvent.Properties["timestamp"]);
			Assert.NotEmpty(telemetryEvent.Properties["session_id"]);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Shutdown_RecordsSessionDurationAndTurnsOffTelemetry()
	{
		string text = CreateTempRoot();
		try
		{
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			using ZContext context = new ZContext(environment);
			FakeTimeProvider fakeTimeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 7, 5, 2, 0, 0, TimeSpan.Zero));
			RecordingTelemetryRecorder recordingTelemetryRecorder = new RecordingTelemetryRecorder();
			TelemetryManager telemetryManager = new TelemetryManager(context, recordingTelemetryRecorder, new FakeTelemetryInfoProvider(), fakeTimeProvider);
			telemetryManager.Initialize();
			fakeTimeProvider.Advance(TimeSpan.FromSeconds(30L));
			telemetryManager.Shutdown();
			telemetryManager.Shutdown();
			Assert.False(telemetryManager.IsInitialized);
			Assert.Equal(2, recordingTelemetryRecorder.Events.Count);
			TelemetryEvent telemetryEvent = recordingTelemetryRecorder.Events[1];
			Assert.Equal("app_shutdown", telemetryEvent.EventName);
			Assert.Equal("30", telemetryEvent.Properties["session_duration_seconds"]);
			Assert.Equal("True", telemetryEvent.Properties["clean_shutdown"]);
			Assert.Equal("2026-07-05T02:00:30.0000000+00:00", telemetryEvent.Properties["timestamp"]);
			Assert.All(recordingTelemetryRecorder.Events, delegate(TelemetryEvent telemetryEvent2)
			{
				Assert.DoesNotContain("notify", telemetryEvent2.EventName, StringComparison.OrdinalIgnoreCase);
				Assert.DoesNotContain("push", telemetryEvent2.EventName, StringComparison.OrdinalIgnoreCase);
			});
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Initialize_ReturnsFalseWhenRecorderThrows()
	{
		string text = CreateTempRoot();
		try
		{
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			using ZContext context = new ZContext(environment);
			TelemetryManager telemetryManager = new TelemetryManager(context, new ThrowingTelemetryRecorder(), new FakeTelemetryInfoProvider(), new FakeTimeProvider(new DateTimeOffset(2026, 7, 5, 3, 0, 0, TimeSpan.Zero)));
			bool condition = telemetryManager.Initialize();
			Assert.False(condition);
			Assert.False(telemetryManager.IsInitialized);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task AliyunWebTrackingRecorder_SendsFlattenedPayloadViaGetAsync()
	{
		RecordingHttpMessageHandler handler = new RecordingHttpMessageHandler();
		using HttpClient httpClient = new HttpClient(handler);
		AliyunWebTrackingRecorder recorder = new AliyunWebTrackingRecorder("https://example.com/track?APIVersion=0.6.0", httpClient);
		recorder.Record("app_launched", new Dictionary<string, string>
		{
			["session_id"] = "session-1",
			["clean_shutdown"] = "True",
			["extra"] = "{\"a\":1}"
		});
		HttpRequestMessage request = await handler.GetSingleRequestAsync();
		Assert.Equal(HttpMethod.Get, request.Method);
		Assert.NotNull(request.RequestUri);
		string requestUri = request.RequestUri.ToString();
		Assert.Contains("event_name=app_launched", requestUri, StringComparison.Ordinal);
		Assert.Contains("session_id=session-1", requestUri, StringComparison.Ordinal);
		Assert.Contains("clean_shutdown=True", requestUri, StringComparison.Ordinal);
		Assert.Equal("{\"a\":1}", GetQueryValue(request.RequestUri, "extra"));
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static string? GetQueryValue(Uri uri, string key)
	{
		string[] array = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
		foreach (string text in array)
		{
			string[] array2 = text.Split('=', 2);
			if (array2.Length == 2 && string.Equals(Uri.UnescapeDataString(array2[0]), key, StringComparison.Ordinal))
			{
				return Uri.UnescapeDataString(array2[1]);
			}
		}
		return null;
	}
}
