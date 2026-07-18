using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Events;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Events;
using OneDragon.Core.Operation;
using OneDragon.Core.Runtime;
using OneDragon.Core.Template;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.Tests.AutoBattle;

public sealed class TargetStateCheckerTests
{
	[Fact]
	public void ContourCountInRange_ReturnsHitOrClearOnMiss()
	{
		TargetStateChecker targetStateChecker = new TargetStateChecker();
		TargetStateCheckFrame frame = new TargetStateCheckFrame
		{
			Contours = new OpenCvSharp.Point[2][]
			{
				new OpenCvSharp.Point[3]
				{
					new OpenCvSharp.Point(0, 0),
					new OpenCvSharp.Point(1, 0),
					new OpenCvSharp.Point(1, 1)
				},
				new OpenCvSharp.Point[3]
				{
					new OpenCvSharp.Point(5, 5),
					new OpenCvSharp.Point(6, 5),
					new OpenCvSharp.Point(6, 6)
				}
			}
		};
		TargetStateDef stateDef = new TargetStateDef
		{
			StateName = "目标-近距离锁定",
			CheckWay = TargetCheckWay.ContourCountInRange,
			CheckParams = new Dictionary<string, object>
			{
				["min_count"] = 2,
				["max_count"] = 2
			},
			ClearOnMiss = true
		};
		TargetStateCheckResult targetStateCheckResult = targetStateChecker.InterpretResult(frame, stateDef);
		TargetStateCheckResult targetStateCheckResult2 = targetStateChecker.InterpretResult(WithContours(Array.Empty<OpenCvSharp.Point[]>()), stateDef);
		Assert.True(targetStateCheckResult.IsHit);
		Assert.False(targetStateCheckResult.IsClear);
		Assert.False(targetStateCheckResult2.IsHit);
		Assert.True(targetStateCheckResult2.IsClear);
	}

	[Fact]
	public void OcrAsNumber_ReturnsFirstIntegerValue()
	{
		TargetStateChecker targetStateChecker = new TargetStateChecker();
		TargetStateCheckFrame frame = new TargetStateCheckFrame
		{
			OcrText = "失衡值 73%"
		};
		TargetStateDef stateDef = new TargetStateDef
		{
			StateName = "强敌-失衡值",
			CheckWay = TargetCheckWay.OcrResultAsNumber
		};
		TargetStateCheckResult targetStateCheckResult = targetStateChecker.InterpretResult(frame, stateDef);
		Assert.True(targetStateCheckResult.IsHit);
		Assert.Equal(73, targetStateCheckResult.Value);
	}

	[Fact]
	public void OcrContains_HandlesModeExcludeAndCaseSensitivity()
	{
		TargetStateChecker targetStateChecker = new TargetStateChecker();
		TargetStateDef stateDef = new TargetStateDef
		{
			StateName = "目标-异常",
			CheckWay = TargetCheckWay.OcrTextContains,
			CheckParams = new Dictionary<string, object>
			{
				["contains"] = new string[2] { "burn", "freeze" },
				["exclude"] = new string[1] { "immune" },
				["mode"] = "any",
				["case_sensitive"] = false
			}
		};
		TargetStateCheckResult targetStateCheckResult = targetStateChecker.InterpretResult(new TargetStateCheckFrame
		{
			OcrText = "BURN damage"
		}, stateDef);
		TargetStateCheckResult targetStateCheckResult2 = targetStateChecker.InterpretResult(new TargetStateCheckFrame
		{
			OcrText = "burn immune"
		}, stateDef);
		Assert.True(targetStateCheckResult.IsHit);
		Assert.False(targetStateCheckResult2.IsHit);
	}

	[Fact]
	public void OcrSimilarity_UsesConfiguredThreshold()
	{
		TargetStateChecker targetStateChecker = new TargetStateChecker();
		TargetStateDef stateDef = new TargetStateDef
		{
			StateName = "目标-异常-灼烧",
			CheckWay = TargetCheckWay.OcrTextSimilarity,
			CheckParams = new Dictionary<string, object>
			{
				["expected_texts"] = new string[1] { "灼烧" },
				["threshold"] = 0.5
			}
		};
		TargetStateCheckResult targetStateCheckResult = targetStateChecker.InterpretResult(new TargetStateCheckFrame
		{
			OcrText = "灼烧"
		}, stateDef);
		TargetStateCheckResult targetStateCheckResult2 = targetStateChecker.InterpretResult(new TargetStateCheckFrame
		{
			OcrText = "冻结"
		}, stateDef);
		Assert.True(targetStateCheckResult.IsHit);
		Assert.False(targetStateCheckResult2.IsHit);
	}

	[Fact]
	public void MapContourLengthToPercent_MapsBoundingWidthAgainstMaskWidth()
	{
		TargetStateChecker targetStateChecker = new TargetStateChecker();
		using Mat maskImage = new Mat(5, 100, MatType.CV_8UC1, Scalar.White);
		TargetStateCheckFrame frame = new TargetStateCheckFrame
		{
			MaskImage = maskImage,
			Contours = new OpenCvSharp.Point[][] { new OpenCvSharp.Point[4]
			{
				new OpenCvSharp.Point(10, 1),
				new OpenCvSharp.Point(34, 1),
				new OpenCvSharp.Point(34, 4),
				new OpenCvSharp.Point(10, 4)
			} }
		};
		TargetStateDef stateDef = new TargetStateDef
		{
			StateName = "强敌-失衡值",
			CheckWay = TargetCheckWay.MapContourLengthToPercent,
			ClearOnMiss = true
		};
		TargetStateCheckResult targetStateCheckResult = targetStateChecker.InterpretResult(frame, stateDef);
		TargetStateCheckResult targetStateCheckResult2 = targetStateChecker.InterpretResult(new TargetStateCheckFrame
		{
			MaskImage = maskImage
		}, stateDef);
		Assert.True(targetStateCheckResult.IsHit);
		Assert.Equal(25, targetStateCheckResult.Value);
		Assert.True(targetStateCheckResult2.IsClear);
	}

	[Fact]
	public void RunTask_InterpretsAllStateDefinitions()
	{
		TargetStateChecker targetStateChecker = new TargetStateChecker();
		DetectionTask task = new DetectionTask
		{
			TaskId = "test",
			PipelineName = "test",
			StateDefinitions = new TargetStateDef[2]
			{
				new TargetStateDef
				{
					StateName = "目标-近距离锁定",
					CheckWay = TargetCheckWay.ContourCountInRange,
					CheckParams = new Dictionary<string, object> { ["min_count"] = 1 }
				},
				new TargetStateDef
				{
					StateName = "强敌-失衡值",
					CheckWay = TargetCheckWay.OcrResultAsNumber
				}
			}
		};
		TargetStateCheckFrame frame = new TargetStateCheckFrame
		{
			OcrText = "88",
			Contours = new OpenCvSharp.Point[][] { new OpenCvSharp.Point[3]
			{
				new OpenCvSharp.Point(0, 0),
				new OpenCvSharp.Point(1, 0),
				new OpenCvSharp.Point(1, 1)
			} }
		};
		IReadOnlyList<TargetStateCheckResult> readOnlyList = targetStateChecker.RunTask(frame, task);
		Assert.Equal(2, readOnlyList.Count);
		Assert.True(readOnlyList[0].IsHit);
		Assert.Equal(88, readOnlyList[1].Value);
	}

	[Fact]
	public async Task RunTask_WithMatRunsBossStunPipelineFromTemplateCrop()
	{
		string repoRoot = FindRepoRoot();
		using ZContext ctx = new ZContext(new OneDragonEnvironment(repoRoot));
		TargetStateChecker checker = new TargetStateChecker(ctx);
		TaskCompletionSource<PerformanceMetricSample> received = new TaskCompletionSource<PerformanceMetricSample>(TaskCreationOptions.RunContinuationsAsynchronously);
		using (ctx.EventBus.Subscribe(PerformanceMetricEventIds.Sample, delegate(ContextEvent<PerformanceMetricEventPayload> envelope)
		{
			if (envelope.Payload.Sample.Metric == "cv_pipeline_ms")
			{
				received.TrySetResult(envelope.Payload.Sample);
			}
		}))
		{
			using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, new Scalar(0.0, 0.0, 0.0));
			TemplateInfo template = ctx.TemplateLoader.GetTemplate("target_state", "boss_stun_line");
			OneDragon.Core.Abstractions.Geometry.Rect rect = template.GetTemplateRectByPoint().Value;
			Vec3b color = RgbFromHsv(26, byte.MaxValue, byte.MaxValue);
			for (int x = rect.X1; x < rect.X1 + 120; x++)
			{
				screen.Set(rect.Y1, x, color);
			}
			DetectionTask task = new DetectionTask
			{
				TaskId = "boss_stun_by_length",
				PipelineName = "boss_stun_line",
				StateDefinitions = new TargetStateDef[] { new TargetStateDef
				{
					StateName = "强敌-失衡值",
					CheckWay = TargetCheckWay.MapContourLengthToPercent,
					ClearOnMiss = true
				} }
			};
			IReadOnlyList<TargetStateCheckResult> results = checker.RunTask(screen, task);
			Assert.Single(results);
			Assert.True(results[0].IsHit);
			Assert.InRange(results[0].Value.Value, 1, 100);
			PerformanceMetricSample sample = await received.Task.WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.Equal("cv_pipeline_ms", sample.Metric);
			Assert.Equal("boss_stun_line", sample.Metadata["pipeline"]);
			Assert.True(sample.Value >= 0.0);
			Assert.True(SpinWait.SpinUntil(() => ctx.OverlayDebugBus.Snapshot().PerformanceItems.Any(item => item.Metric == "cv_pipeline_ms"), TimeSpan.FromSeconds(2L)));
		}
	}

	[Fact]
	public void AutoBattleTargetContext_DefaultCheckerWritesStateFromFrame()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment("test_project", "test_user_id");
		ZContext zContext = new ZContext(environment);
		AutoBattleTargetContext autoBattleTargetContext = new AutoBattleTargetContext(zContext);
		autoBattleTargetContext.ApplyConfigIntervals(0.5, 0.0);
		TargetStateCheckFrame screen = new TargetStateCheckFrame
		{
			Contours = new OpenCvSharp.Point[2][]
			{
				new OpenCvSharp.Point[3]
				{
					new OpenCvSharp.Point(0, 0),
					new OpenCvSharp.Point(1, 0),
					new OpenCvSharp.Point(1, 1)
				},
				new OpenCvSharp.Point[3]
				{
					new OpenCvSharp.Point(5, 5),
					new OpenCvSharp.Point(6, 5),
					new OpenCvSharp.Point(6, 6)
				}
			}
		};
		IReadOnlyList<StateRecord> readOnlyList = autoBattleTargetContext.RunAllChecks(screen, 0.5);
		Assert.Single(readOnlyList);
		Assert.Equal("目标-近距离锁定", readOnlyList[0].StateName);
		Assert.Equal(0.5, zContext.AutoBattleContext.StateRecordService.GetStateRecorder("目标-近距离锁定").LastRecordTime);
	}

	private static TargetStateCheckFrame WithContours(IReadOnlyList<OpenCvSharp.Point[]> contours)
	{
		return new TargetStateCheckFrame
		{
			Contours = contours
		};
	}

	private static Vec3b RgbFromHsv(byte hue, byte saturation, byte value)
	{
		using Mat mat = new Mat(1, 1, MatType.CV_8UC3);
		mat.Set(0, 0, new Vec3b(hue, saturation, value));
		using Mat mat2 = new Mat();
		Cv2.CvtColor(mat, mat2, ColorConversionCodes.HSV2RGB);
		return mat2.At<Vec3b>(0, 0);
	}

	private static string FindRepoRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string fullName = directoryInfo.FullName;
			if (Directory.Exists(Path.Combine(fullName, "assets", "template", "target_state")) && Directory.Exists(Path.Combine(fullName, "zzzod-dotnet", "src", "ZzzOd.GameLogic")))
			{
				return fullName;
			}
		}
		throw new DirectoryNotFoundException("未找到 zzz-od-dotnet 仓库根目录。");
	}
}
