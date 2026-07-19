using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using OneDragon.Core.Configuration;
using OneDragon.Core.Events;
using OneDragon.Core.Runtime;
using System.Threading.Tasks;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class OperationTraceBridgeTests
{
	[Fact]
	public async Task PublishPendingMapsOperationAndTimelineTrace()
	{
		string runRoot = CreateRunRoot();
		try
		{
			using ZzzRuntimeManager runtime = new ZzzRuntimeManager(
				runRoot,
				NullLogger<ZzzRuntimeManager>.Instance,
				_ => new ZContext(new OneDragonEnvironment(runRoot), null, 0));
			ZzzBackendEventBus eventBus = new ZzzBackendEventBus();
			using ZzzOperationTraceBridge bridge = new ZzzOperationTraceBridge(runtime, eventBus, NullLogger<ZzzOperationTraceBridge>.Instance);
			ChannelReader<ZzzBackendEvent> reader = eventBus.Subscribe();
			ZContext context = runtime.EnsureContext();
			DateTimeOffset timestamp = DateTimeOffset.UtcNow;
			context.OverlayDebugBus.PublishOperation(new OperationTraceItem(
				"suibian_temple",
				"随便观",
				"处理德丰大押",
				"处理好物铺",
				"完成后返回",
				0,
				"transition",
				"大世界-普通",
				timestamp));
			context.OverlayDebugBus.PublishTimeline(new TimelineItem(
				"operation",
				"随便观",
				"处理德丰大押 | 大世界-普通",
				"information",
				timestamp,
				Metadata: new Dictionary<string, object?>
				{
					["exception_type"] = null,
				}));

			Assert.Equal(1, bridge.PublishPending());
			ZzzBackendEvent backendEvent = await reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3L));
			ZzzOperationTraceDto trace = Assert.IsType<ZzzOperationTraceDto>(backendEvent.Data);
			Assert.Equal("run.operationTrace", backendEvent.Type);
			Assert.Equal("suibian_temple", trace.AppId);
			Assert.Equal(0, trace.InstanceIndex);
			Assert.Equal("随便观", trace.Operation);
			Assert.Equal("处理德丰大押", trace.CurrentNode);
			Assert.Equal("完成后返回", trace.NextNode);
			Assert.Equal("transition", trace.ResultKind);
			Assert.Equal("大世界-普通", trace.Status);
			Assert.Equal(timestamp, trace.Timestamp);
			Assert.Equal(0, bridge.PublishPending());
			eventBus.Unsubscribe(reader);
		}
		finally
		{
			TryDelete(runRoot);
		}
	}

	[Fact]
	public async Task PublishPendingMapsExceptionMetadataAndMessage()
	{
		string runRoot = CreateRunRoot();
		try
		{
			using ZzzRuntimeManager runtime = new ZzzRuntimeManager(
				runRoot,
				NullLogger<ZzzRuntimeManager>.Instance,
				_ => new ZContext(new OneDragonEnvironment(runRoot), null, 0));
			ZzzBackendEventBus eventBus = new ZzzBackendEventBus();
			using ZzzOperationTraceBridge bridge = new ZzzOperationTraceBridge(runtime, eventBus, NullLogger<ZzzOperationTraceBridge>.Instance);
			ChannelReader<ZzzBackendEvent> reader = eventBus.Subscribe();
			ZContext context = runtime.EnsureContext();
			DateTimeOffset timestamp = DateTimeOffset.UtcNow;
			context.OverlayDebugBus.PublishOperation(new OperationTraceItem(
				"coffee",
				"咖啡店",
				"选择咖啡",
				null,
				null,
				2,
				"exception",
				"识别失败",
				timestamp));
			context.OverlayDebugBus.PublishTimeline(new TimelineItem(
				"operation",
				"咖啡店",
				"选择咖啡 | 识别失败",
				"error",
				timestamp,
				Metadata: new Dictionary<string, object?>
				{
					["exception_type"] = "InvalidOperationException",
				}));

			Assert.Equal(1, bridge.PublishPending());
			ZzzOperationTraceDto trace = Assert.IsType<ZzzOperationTraceDto>((await reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3L))).Data);
			Assert.Equal("InvalidOperationException", trace.ExceptionType);
			Assert.Equal("识别失败", trace.ExceptionMessage);
			Assert.Equal(2, trace.RetryCount);
			eventBus.Unsubscribe(reader);
		}
		finally
		{
			TryDelete(runRoot);
		}
	}

	private static string CreateRunRoot()
	{
		string root = Path.Combine(Path.GetTempPath(), "zzzod-operation-trace-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		return root;
	}

	private static void TryDelete(string path)
	{
		try
		{
			Directory.Delete(path, recursive: true);
		}
		catch
		{
		}
	}
}
