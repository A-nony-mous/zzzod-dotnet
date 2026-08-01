using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Events;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Events;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.DebugData;

namespace ZzzOd.GameLogic.Tests.DebugData;

public sealed class DebugDataTests
{
	[Fact]
	public async Task DebugDataPublisher_ShouldPublishStructuredBusinessDebugData()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			TaskCompletionSource<ZzzDebugDataEventPayload> completionSource = new TaskCompletionSource<ZzzDebugDataEventPayload>(TaskCreationOptions.RunContinuationsAsynchronously);
			using (context.EventBus.Subscribe("Zzz.Debug.Data", delegate(ContextEvent<ZzzDebugDataEventPayload> contextEvent)
			{
				completionSource.TrySetResult(contextEvent.Payload);
			}))
			{
				ZzzDebugDataPublisher debugDataPublisher = context.DebugDataPublisher;
				ZzzDebugDataItem[] obj = new ZzzDebugDataItem[5]
				{
					ZzzDebugDataItem.Ocr("lost_void_artifact", "[强攻]割草除根", new Rect(10, 20, 110, 60), 0.91, "matched"),
					null,
					null,
					null,
					null
				};
				Rect region = new Rect(200, 120, 260, 190);
				double? confidence = 0.87;
				IReadOnlyDictionary<string, object> metadata = new Dictionary<string, object>
				{
					["class_id"] = 12,
					["category"] = "event"
				};
				obj[1] = ZzzDebugDataItem.Yolo("hollow_event", "event_battle", region, confidence, "detected", null, metadata);
				obj[2] = ZzzDebugDataItem.Path("hollow_pathfinding", "boss_route", new Point[2]
				{
					new Point(320, 240),
					new Point(420, 240)
				}, "selected");
				obj[3] = ZzzDebugDataItem.ActionDecision("auto_battle", "连携技-准备", "切换角色", "执行", "priority=1");
				obj[4] = ZzzDebugDataItem.Performance("hollow_event", "yolo_ms", 18.5, "ms", new Dictionary<string, object> { ["result_count"] = 3 });
				debugDataPublisher.PublishMany(obj);
				ZzzDebugDataEventPayload payload = await completionSource.Task.WaitAsync(TimeSpan.FromSeconds(2L));
				Assert.Equal(5, payload.Items.Count);
				Assert.Contains((IEnumerable<ZzzDebugDataItem>)payload.Items, (Predicate<ZzzDebugDataItem>)((ZzzDebugDataItem item) => item.Kind == ZzzDebugDataKind.Ocr && item.Label == "[强攻]割草除根" && item.Confidence.GetValueOrDefault() == 0.91));
				Assert.Contains((IEnumerable<ZzzDebugDataItem>)payload.Items, (Predicate<ZzzDebugDataItem>)((ZzzDebugDataItem item) => item.Kind == ZzzDebugDataKind.Yolo && object.Equals(item.Metadata["class_id"], 12)));
				Assert.Contains((IEnumerable<ZzzDebugDataItem>)payload.Items, (Predicate<ZzzDebugDataItem>)((ZzzDebugDataItem item) => item.Kind == ZzzDebugDataKind.Path && item.PathPoints.Count == 2 && item.State == "selected"));
				Assert.Contains((IEnumerable<ZzzDebugDataItem>)payload.Items, (Predicate<ZzzDebugDataItem>)((ZzzDebugDataItem item) => item.Kind == ZzzDebugDataKind.ActionDecision && object.Equals(item.Metadata["trigger"], "连携技-准备")));
				Assert.Contains((IEnumerable<ZzzDebugDataItem>)payload.Items, (Predicate<ZzzDebugDataItem>)((ZzzDebugDataItem item) => item.Kind == ZzzDebugDataKind.Performance && item.ElapsedMilliseconds.GetValueOrDefault() == 18.5));
				OverlayDebugSnapshot overlaySnapshot = context.OverlayDebugBus.Snapshot();
				Assert.Contains((IEnumerable<VisionDrawItem>)overlaySnapshot.VisionItems, (Predicate<VisionDrawItem>)((VisionDrawItem item) => item.Source == "ocr" && item.Label == "[强攻]割草除根"));
				Assert.Contains((IEnumerable<VisionDrawItem>)overlaySnapshot.VisionItems, (Predicate<VisionDrawItem>)((VisionDrawItem item) => item.Source == "yolo" && item.Label == "event_battle"));
				Assert.Contains((IEnumerable<VisionDrawItem>)overlaySnapshot.VisionItems, (Predicate<VisionDrawItem>)((VisionDrawItem item) => item.Source == "path" && item.Metadata != null && item.Metadata.ContainsKey("path_points")));
				Assert.Equal("连携技-准备", Assert.Single(overlaySnapshot.DecisionItems).Trigger);
				Assert.Equal("yolo_ms", Assert.Single(overlaySnapshot.PerformanceItems).Metric);
				Assert.Equal("boss_route", Assert.Single(overlaySnapshot.TimelineItems).Title);
			}
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DebugDataPublisher_ShouldPublishKindSpecificEvents()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(CreateTempRoot()));
		TaskCompletionSource<ZzzDebugDataEventPayload> completionSource = new TaskCompletionSource<ZzzDebugDataEventPayload>(TaskCreationOptions.RunContinuationsAsynchronously);
		using (context.EventBus.Subscribe("Zzz.Debug.Data.Ocr", delegate(ContextEvent<ZzzDebugDataEventPayload> contextEvent)
		{
			completionSource.TrySetResult(contextEvent.Payload);
		}))
		{
			context.DebugDataPublisher.Publish(ZzzDebugDataItem.Ocr("screen_ocr", "战斗开始", new Rect(1, 2, 3, 4), 0.8));
			ZzzDebugDataEventPayload payload = await completionSource.Task.WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.Single(payload.Items);
			Assert.Equal(ZzzDebugDataKind.Ocr, payload.Items[0].Kind);
		}
	}

	[Fact]
	public void DebugDataPublisher_ShouldPublishBusinessStateWithSourceAndTtl()
	{
		using ZContext context = new ZContext(new OneDragonEnvironment(CreateTempRoot()));
		Assert.True(context.DebugDataPublisher.PublishBusinessState(
			"迷失之地-当前区域",
			"战斗-普通",
			"LostVoidRunLevel",
			60d));

		BusinessStateItem item = Assert.Single(context.OverlayDebugBus.Snapshot().BusinessStateItems);
		Assert.Equal("迷失之地-当前区域", item.Key);
		Assert.Equal("战斗-普通", item.Value);
		Assert.Equal("LostVoidRunLevel", item.Source);
		Assert.Equal(60d, item.TtlSeconds);
		Assert.Empty(context.OverlayDebugBus.Snapshot(item.CreatedAt.AddSeconds(61d)).BusinessStateItems);
	}

	[Fact]
	public void DebugDataModels_ShouldNotExposeGuiOrWindowTypes()
	{
		Type[] array = (from type2 in typeof(ZzzDebugDataItem).Assembly.GetTypes()
			where string.Equals(type2.Namespace, "ZzzOd.GameLogic.DebugData", StringComparison.Ordinal)
			select type2).ToArray();
		Assert.NotEmpty(array);
		Type[] array2 = array;
		foreach (Type type in array2)
		{
			Type[] collection = (from property in type.GetProperties()
				select property.PropertyType).Concat(from parameter in type.GetConstructors().SelectMany((ConstructorInfo ctor) => ctor.GetParameters())
				select parameter.ParameterType).ToArray();
			Assert.DoesNotContain((IEnumerable<Type>)collection, (Predicate<Type>)IsGuiOrWindowType);
		}
	}

	private static bool IsGuiOrWindowType(Type type)
	{
		string text = type.FullName ?? string.Empty;
		return text.Contains("System.Windows", StringComparison.Ordinal) || text.Contains("OneDragon.Core.Windows", StringComparison.Ordinal) || text.Contains("OpenCvSharp", StringComparison.Ordinal) || text.Contains("Gui", StringComparison.Ordinal) || text.Contains("Window", StringComparison.Ordinal);
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}
}
