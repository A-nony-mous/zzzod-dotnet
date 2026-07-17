using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.EmailApp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class EmailAppTests
{
	private sealed class RecordingEmailFlow : IEmailAppFlow
	{
		public int RunCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
		{
			RunCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "邮件领取完成"));
		}
	}

	private sealed class ClickFailController : ControllerBase, IDisposable
	{
		private readonly Mat _screenshot = new Mat(new Size(100, 60), MatType.CV_8UC3, Scalar.Black);

		public int ClickCount { get; private set; }

		public override bool IsGameWindowReady => true;

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			ClickCount++;
			return false;
		}

		public override void Scroll(int down, OneDragon.Core.Abstractions.Geometry.Point? position = null)
		{
		}

		public override void DragTo(OneDragon.Core.Abstractions.Geometry.Point end, OneDragon.Core.Abstractions.Geometry.Point? start = null, TimeSpan? duration = null)
		{
		}

		public override void InputText(string text)
		{
		}

		public override void MouseMove(OneDragon.Core.Abstractions.Geometry.Point position)
		{
		}

		public void Dispose()
		{
			_screenshot.Dispose();
		}

		protected override Mat? GetScreenshot(bool independent = false)
		{
			return _screenshot.Clone();
		}
	}

	private sealed class FakeOcrMatcher(IReadOnlyList<OcrMatchResult> results) : IOcrMatcher
	{
		public void UpdateUseGpu(bool useGpu)
		{
		}

		public bool IsUseGpu()
		{
			return false;
		}

		public bool InitModel(string? proxyUrl = null, string? ghProxyUrl = null, bool skipIfExisted = true, Action<double, string>? progressCallback = null)
		{
			return true;
		}

		public string RunOcrSingleLine(Mat image, double? threshold = null, bool strictOneLine = true)
		{
			return string.Concat(from result in results
				orderby result.Y, result.X
				select result.Text);
		}

		public IReadOnlyDictionary<string, MatchResultList> RunOcr(Mat image, double? threshold = null, double mergeLineDistance = -1.0)
		{
			Dictionary<string, MatchResultList> dictionary = new Dictionary<string, MatchResultList>(StringComparer.Ordinal);
			foreach (OcrMatchResult item in Ocr(image, threshold.GetValueOrDefault(), mergeLineDistance))
			{
				if (!dictionary.TryGetValue(item.Text, out var value))
				{
					value = new MatchResultList(onlyBest: false);
					dictionary[item.Text] = value;
				}
				value.Append(item, autoMerge: false);
			}
			return dictionary;
		}

		public IReadOnlyList<OcrMatchResult> Ocr(Mat image, double threshold = 0.0, double mergeLineDistance = -1.0)
		{
			return results.Select((OcrMatchResult result) => new OcrMatchResult(result.Confidence, result.X, result.Y, result.Width, result.Height, result.Text)).ToArray();
		}
	}

	[Fact]
	public void EmailOperation_AnnotatesClaimSuccessLikePython()
	{
		MethodInfo method = typeof(EmailOperation).GetMethod("ClickGetAll");
		Assert.Contains(method.GetCustomAttributes<OperationNodeNotifyAttribute>(), (OperationNodeNotifyAttribute annotation) => annotation.Timing == OperationNodeNotifyTiming.CurrentSuccess);
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesEmailApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			EmailAppFactory emailAppFactory = zContext.ApplicationFactoryRegistry.CreateEmailFactory();
			IApplication application = emailAppFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = emailAppFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = emailAppFactory.GetRunRecord(0);
			Assert.Equal("email", emailAppFactory.AppId);
			Assert.Equal("邮件", emailAppFactory.AppName);
			Assert.Equal("one_dragon", emailAppFactory.GroupId);
			Assert.True(emailAppFactory.NeedNotify);
			Assert.IsType<EmailApp>(application);
			Assert.IsType<ZApplicationConfig>(config);
			EmailRunRecord emailRunRecord = Assert.IsType<EmailRunRecord>(runRecord);
			Assert.Equal("email", emailRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersEmailAsDefaultNotifyApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterEmailApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("email"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("email"));
			Assert.Contains("email", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task EmailApp_RunsInjectedClaimFlowAndUpdatesRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingEmailFlow flow = new RecordingEmailFlow();
			EmailRunRecord runRecord = new EmailRunRecord();
			EmailApp app = new EmailApp(context, runRecord, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("邮件领取完成", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(1, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void EmailRunRecord_UsesEmailAppIdAndGameRefreshOffset()
	{
		DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
		EmailRunRecord emailRunRecord = new EmailRunRecord(4, () => now);
		emailRunRecord.UpdateStatus(1);
		Assert.Equal("email", emailRunRecord.AppId);
		Assert.Equal("20260706", emailRunRecord.Dt);
		Assert.True(emailRunRecord.IsDone);
	}

	[Fact]
	public void EmailOperation_ClickConfirmRetriesWhenClickReturnsFalse()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 20, 10, 40, 20, "确认") });
			using ClickFailController clickFailController = new ClickFailController();
			zContext.AttachController(clickFailController);
			EmailOperation emailOperation = new EmailOperation(zContext);
			CaptureOnce(emailOperation);
			OperationRoundResult operationRoundResult = emailOperation.ClickConfirm();
			Assert.False(operationRoundResult.IsSuccess);
			Assert.Equal("点击失败 确认", operationRoundResult.Status);
			Assert.Equal(TimeSpan.FromSeconds(1L), operationRoundResult.Delay);
			Assert.Equal(1, clickFailController.ClickCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static void CaptureOnce(EmailOperation operation)
	{
		MethodInfo methodInfo = typeof(EmailOperation).BaseType?.GetMethod("Screenshot", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(methodInfo);
		methodInfo.Invoke(operation, new object[1] { false });
	}
}
