using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Notifications;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class PushNotificationServiceTests
{
	private sealed class RecordingHandler : HttpMessageHandler
	{
		public int CallCount { get; private set; }

		public Uri? Uri { get; private set; }

		public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		public string Body { get; private set; } = string.Empty;

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			CallCount++;
			Uri = request.RequestUri;
			foreach (var (key, values) in request.Headers)
			{
				Headers[key] = string.Join(",", values);
			}
			string body = ((request.Content != null) ? (await request.Content.ReadAsStringAsync(cancellationToken)) : string.Empty);
			Body = body;
			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("{}", Encoding.UTF8, "application/json")
			};
		}
	}

	private sealed class SequenceHandler(params string[] responseBodies) : HttpMessageHandler
	{
		private readonly Queue<string> _responseBodies = new Queue<string>(responseBodies);

		public List<RecordedRequest> Requests { get; } = new List<RecordedRequest>();

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			string text = ((request.Content != null) ? (await request.Content.ReadAsStringAsync(cancellationToken)) : string.Empty);
			Requests.Add(new RecordedRequest(Body: text, Uri: request.RequestUri, ContentType: request.Content?.Headers.ContentType?.MediaType ?? string.Empty));
			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(_responseBodies.Dequeue(), Encoding.UTF8, "application/json")
			};
		}
	}

	private sealed record RecordedRequest(Uri Uri, string ContentType, string Body);

	[Fact]
	public async Task WebhookTestUsesRealYamlProxyVariablesHeadersAndBody()
	{
		string runRoot = CreateRunRoot();
		try
		{
			File.WriteAllText(Path.Combine(runRoot, "config", "push.yml"), "proxy: PERSONAL\nwebhook_url: https://example.test/hook/$title\nwebhook_method: POST\nwebhook_content_type: application/json\nwebhook_headers: '{\"X-Content\":\"$content\"}'\nwebhook_body: '{\"title\":\"$title\",\"content\":\"$content\"}'");
			File.WriteAllText(Path.Combine(runRoot, "config", "env.yml"), "personal_proxy: http://127.0.0.1:8080\n");
			RecordingHandler handler = new RecordingHandler();
			IWebProxy observedProxy = null;
			ZzzPushNotificationService service = new ZzzPushNotificationService(new ZzzRunRoot(runRoot), delegate(IWebProxy? proxy)
			{
				observedProxy = proxy;
				return new HttpClient(handler, disposeHandler: false);
			});
			ZzzPushTestResult result = await service.SendTestAsync("WEBHOOK", "测试标题", "测试正文");
			Assert.True(result.Success, result.Message);
			Assert.NotNull(observedProxy);
			Assert.Equal("https://example.test/hook/%E6%B5%8B%E8%AF%95%E6%A0%87%E9%A2%98", handler.Uri?.AbsoluteUri);
			Assert.Equal("测试正文", handler.Headers["X-Content"]);
			Assert.Contains("测试标题", handler.Body, StringComparison.Ordinal);
			Assert.Contains("测试正文", handler.Body, StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	[Fact]
	public async Task AllChannelsSkipsUnconfiguredChannelsAndUsesConfiguredWebhook()
	{
		string runRoot = CreateRunRoot();
		try
		{
			File.WriteAllText(Path.Combine(runRoot, "config", "push.yml"), "webhook_url: https://example.test/hook\nwebhook_method: POST\nwebhook_content_type: application/json\nwebhook_headers: '{}'\nwebhook_body: '{\"content\":\"$content\"}'");
			RecordingHandler handler = new RecordingHandler();
			ZzzPushNotificationService service = new ZzzPushNotificationService(new ZzzRunRoot(runRoot), (IWebProxy? _) => new HttpClient(handler, disposeHandler: false));
			ZzzPushTestResult result = await service.SendTestAsync(null, "测试推送通知", "这是一条测试消息");
			Assert.True(result.Success, result.Message);
			Assert.Equal(1, handler.CallCount);
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	[Fact]
	public async Task RuntimeManager_InjectsConfiguredProductionPushServiceIntoGameContext()
	{
		string runRoot = CreateRunRoot();
		try
		{
			File.WriteAllText(Path.Combine(runRoot, "config", "push.yml"), "webhook_url: https://example.test/notify\nwebhook_method: POST\nwebhook_content_type: application/json\nwebhook_headers: '{}'\nwebhook_body: '{\"title\":\"$title\",\"content\":\"$content\"}'");
			RecordingHandler handler = new RecordingHandler();
			using ZzzRuntimeManager runtime = new ZzzRuntimeManager(pushNotificationService: new ZzzPushNotificationService(new ZzzRunRoot(runRoot), (IWebProxy? _) => new HttpClient(handler, disposeHandler: false)), runRoot: runRoot, logger: NullLogger<ZzzRuntimeManager>.Instance, contextFactory: (int _) => new ZContext(new OneDragonEnvironment(runRoot)));
			ZContext context = runtime.EnsureContext();
			OperationResult result = await context.PushNotificationService.PushAsync(context, "运行标题", "运行内容", null, CancellationToken.None);
			Assert.True(result.IsSuccess, result.Status);
			Assert.Equal(1, handler.CallCount);
			Assert.Contains("运行标题", handler.Body, StringComparison.Ordinal);
			Assert.Contains("运行内容", handler.Body, StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	[Fact]
	public async Task FeishuImagePushUploadsJpegThenPostsPythonCompatibleRichMessage()
	{
		string runRoot = CreateRunRoot();
		try
		{
			File.WriteAllText(Path.Combine(runRoot, "config", "push.yml"), "send_image: true\nfs_channel: Lark\nfs_key: hook-key\nfs_appid: app-id\nfs_appsecret: app-secret");
			SequenceHandler handler = new SequenceHandler("{\"tenant_access_token\":\"access-token\"}", "{\"code\":0,\"data\":{\"image_key\":\"image-key\"}}", "{\"code\":0}");
			ZzzPushNotificationService service = new ZzzPushNotificationService(new ZzzRunRoot(runRoot), (IWebProxy? _) => new HttpClient(handler, disposeHandler: false));
			using Mat image = new Mat(2, 2, MatType.CV_8UC3, new Scalar(12.0, 34.0, 56.0));
			ZzzPushTestResult result = await service.SendTestAsync("FS", "标题", "正文", default(CancellationToken), image);
			Assert.True(result.Success, result.Message);
			Assert.Equal(3, handler.Requests.Count);
			Assert.Equal("/open-apis/auth/v3/tenant_access_token/internal", handler.Requests[0].Uri.AbsolutePath);
			Assert.Equal("/open-apis/im/v1/images", handler.Requests[1].Uri.AbsolutePath);
			Assert.StartsWith("multipart/form-data", handler.Requests[1].ContentType, StringComparison.OrdinalIgnoreCase);
			Assert.Equal("/open-apis/bot/v2/hook/hook-key", handler.Requests[2].Uri.AbsolutePath);
			Assert.Contains("\"msg_type\":\"post\"", handler.Requests[2].Body, StringComparison.Ordinal);
			Assert.Contains("\"image_key\":\"image-key\"", handler.Requests[2].Body, StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	[Fact]
	public async Task ExistingHttpChannels_EncodeImageUsingTheirPythonProtocols()
	{
		string runRoot = CreateRunRoot();
		try
		{
			using Mat image = new Mat(2, 2, MatType.CV_8UC3, new Scalar(5.0, 15.0, 25.0));
			SequenceHandler webhookHandler = new SequenceHandler("{}");
			await SendImageAsync(runRoot, webhookHandler, "webhook_url: https://example.test/webhook\nwebhook_method: POST\nwebhook_content_type: application/json\nwebhook_headers: '{}'\nwebhook_body: '{\"image\":\"$image\"}'", "WEBHOOK", image);
			Assert.Contains("/9j/", webhookHandler.Requests.Single().Body, StringComparison.Ordinal);
			SequenceHandler ntfyHandler = new SequenceHandler("{}", "{}");
			await SendImageAsync(runRoot, ntfyHandler, "ntfy_url: https://example.test\nntfy_topic: topic\n", "NTFY", image);
			Assert.Equal(2, ntfyHandler.Requests.Count);
			Assert.NotEmpty(ntfyHandler.Requests[0].Body);
			Assert.Equal("正文", ntfyHandler.Requests[1].Body);
			SequenceHandler oneBotHandler = new SequenceHandler("{\"status\":\"ok\"}");
			await SendImageAsync(runRoot, oneBotHandler, "onebot_url: https://example.test\nonebot_user: '1'\n", "ONEBOT", image);
			Assert.Contains("base64://", oneBotHandler.Requests.Single().Body, StringComparison.Ordinal);
			SequenceHandler qywxHandler = new SequenceHandler("{\"errcode\":0}", "{\"errcode\":0}");
			await SendImageAsync(runRoot, qywxHandler, "qywx_origin: https://example.test\nqywx_key: key\n", "QYWX", image);
			Assert.Equal(2, qywxHandler.Requests.Count);
			Assert.Contains("\"msgtype\":\"image\"", qywxHandler.Requests[1].Body, StringComparison.Ordinal);
			Assert.Contains("\"md5\":", qywxHandler.Requests[1].Body, StringComparison.Ordinal);
			SequenceHandler telegramHandler = new SequenceHandler("{\"ok\":true}");
			await SendImageAsync(runRoot, telegramHandler, "tg_api_host: https://example.test\ntg_bot_token: token\ntg_user_id: '1'\n", "TG", image);
			Assert.Equal("/bottoken/sendPhoto", telegramHandler.Requests.Single().Uri.AbsolutePath);
			Assert.StartsWith("multipart/form-data", telegramHandler.Requests.Single().ContentType, StringComparison.OrdinalIgnoreCase);
			SequenceHandler discordHandler = new SequenceHandler("{\"id\":\"channel\"}", "{}");
			await SendImageAsync(runRoot, discordHandler, "discord_api_host: https://example.test\ndiscord_bot_token: token\ndiscord_user_id: '1'\n", "DISCORD", image);
			Assert.Equal(2, discordHandler.Requests.Count);
			Assert.StartsWith("multipart/form-data", discordHandler.Requests[1].ContentType, StringComparison.OrdinalIgnoreCase);
			SequenceHandler qywxAppHandler = new SequenceHandler("{\"errcode\":0,\"access_token\":\"token\"}", "{\"errcode\":0,\"url\":\"https://image.test/p.jpg\"}", "{\"errcode\":0,\"media_id\":\"media\"}", "{\"errcode\":0}");
			await SendImageAsync(runRoot, qywxAppHandler, "qywx_app_corp_id: corp\nqywx_app_corp_secret: secret\nqywx_app_agent_id: '1'\n", "QYWX_APP", image);
			Assert.Equal(4, qywxAppHandler.Requests.Count);
			Assert.Contains("\"msgtype\":\"mpnews\"", qywxAppHandler.Requests[3].Body, StringComparison.Ordinal);
			Assert.Contains("\"thumb_media_id\":\"media\"", qywxAppHandler.Requests[3].Body, StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	[Fact]
	public async Task SmtpImagePush_UsesPythonCompatibleCidAttachment()
	{
		string runRoot = CreateRunRoot();
		using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		int port = ((IPEndPoint)listener.LocalEndpoint).Port;
		Task<string> smtpCapture = CaptureSmtpMessageAsync(listener);
		try
		{
			File.WriteAllText(Path.Combine(runRoot, "config", "push.yml"), $"send_image: true\nsmtp_server: 127.0.0.1:{port}\nsmtp_ssl: false\nsmtp_email: test@example.test\nsmtp_password: password");
			ZzzPushNotificationService service = new ZzzPushNotificationService(new ZzzRunRoot(runRoot));
			using Mat image = new Mat(2, 2, MatType.CV_8UC3, new Scalar(1.0, 2.0, 3.0));
			ZzzPushTestResult result = await service.SendTestAsync("SMTP", "标题", "正文", default(CancellationToken), image);
			string message = await smtpCapture.WaitAsync(TimeSpan.FromSeconds(5L));
			Assert.True(result.Success, result.Message);
			Assert.Contains("Content-ID: <screenshot>", message, StringComparison.OrdinalIgnoreCase);
			Assert.Contains("Content-Type: image/jpeg", message, StringComparison.OrdinalIgnoreCase);
		}
		finally
		{
			listener.Stop();
			Directory.Delete(runRoot, recursive: true);
		}
	}

	private static async Task SendImageAsync(string runRoot, SequenceHandler handler, string channelConfig, string channelId, Mat image)
	{
		File.WriteAllText(Path.Combine(runRoot, "config", "push.yml"), "send_image: true\n" + channelConfig);
		ZzzPushNotificationService service = new ZzzPushNotificationService(new ZzzRunRoot(runRoot), (IWebProxy? _) => new HttpClient(handler, disposeHandler: false));
		ZzzPushTestResult result = await service.SendTestAsync(channelId, "标题", "正文", default(CancellationToken), image);
		Assert.True(result.Success, result.Message);
	}

	private static async Task<string> CaptureSmtpMessageAsync(TcpListener listener)
	{
		using TcpClient client = await listener.AcceptTcpClientAsync();
		using NetworkStream stream = client.GetStream();
		using StreamReader reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, 1024, leaveOpen: true);
		using StreamWriter writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true)
		{
			AutoFlush = true,
			NewLine = "\r\n"
		};
		await writer.WriteLineAsync("220 localhost");
		StringBuilder message = new StringBuilder();
		bool readingData = false;
		while (true)
		{
			string line = await reader.ReadLineAsync();
			if (line == null)
			{
				return message.ToString();
			}
			if (readingData)
			{
				if (line == ".")
				{
					readingData = false;
					await writer.WriteLineAsync("250 queued");
				}
				else
				{
					message.AppendLine(line);
				}
			}
			else if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase))
			{
				await writer.WriteLineAsync("250-localhost");
				await writer.WriteLineAsync("250 AUTH LOGIN");
			}
			else if (line.StartsWith("AUTH LOGIN", StringComparison.OrdinalIgnoreCase))
			{
				await writer.WriteLineAsync("334 VXNlcm5hbWU6");
			}
			else if (line.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase) || line.StartsWith("RCPT TO", StringComparison.OrdinalIgnoreCase))
			{
				await writer.WriteLineAsync("250 ok");
			}
			else if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
			{
				readingData = true;
				await writer.WriteLineAsync("354 end with .");
			}
			else
			{
				if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
				{
					break;
				}
				await writer.WriteLineAsync("235 ok");
			}
		}
		await writer.WriteLineAsync("221 bye");
		return message.ToString();
	}

	private static string CreateRunRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-push-service-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(text, "config"));
		return text;
	}
}
