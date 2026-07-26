using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using YamlDotNet.Serialization;

namespace ZzzOd.AppHost.Notifications;

/// <summary>
/// 读取真实 push.yml 并向第三方渠道发送通知。
/// </summary>
public sealed class ZzzPushNotificationService : IZzzPushNotificationService
{
	private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();

	private static readonly JsonSerializerOptions UnescapedJsonOptions = new JsonSerializerOptions
	{
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	private readonly ZzzRunRoot _runRoot;

	private readonly Func<IWebProxy?, HttpClient> _clientFactory;

	/// <inheritdoc />
	public IReadOnlyList<ZzzPushChannelDescriptor> Channels => ZzzPushChannelCatalog.Channels;

	/// <inheritdoc />
	public IReadOnlyDictionary<string, ZzzEmailServicePreset> EmailServices { get; } = new Dictionary<string, ZzzEmailServicePreset>(StringComparer.Ordinal)
	{
		["126"] = new ZzzEmailServicePreset("smtp.126.com", 465, Secure: true),
		["163"] = new ZzzEmailServicePreset("smtp.163.com", 465, Secure: true),
		["1und1"] = new ZzzEmailServicePreset("smtp.1und1.de", 465, Secure: true),
		["Aliyun"] = new ZzzEmailServicePreset("smtp.aliyun.com", 465, Secure: true),
		["AliyunQiye"] = new ZzzEmailServicePreset("smtp.qiye.aliyun.com", 465, Secure: true),
		["AOL"] = new ZzzEmailServicePreset("smtp.aol.com", 587, Secure: false),
		["Bluewin"] = new ZzzEmailServicePreset("smtpauths.bluewin.ch", 465, Secure: false),
		["DebugMail"] = new ZzzEmailServicePreset("debugmail.io", 25, Secure: false),
		["DynectEmail"] = new ZzzEmailServicePreset("smtp.dynect.net", 25, Secure: false),
		["Ethereal"] = new ZzzEmailServicePreset("smtp.ethereal.email", 587, Secure: false),
		["FastMail"] = new ZzzEmailServicePreset("smtp.fastmail.com", 465, Secure: true),
		["Forward Email"] = new ZzzEmailServicePreset("smtp.forwardemail.net", 465, Secure: true),
		["Feishu Mail"] = new ZzzEmailServicePreset("smtp.feishu.cn", 465, Secure: true),
		["GandiMail"] = new ZzzEmailServicePreset("mail.gandi.net", 587, Secure: false),
		["Gmail"] = new ZzzEmailServicePreset("smtp.gmail.com", 465, Secure: true),
		["Godaddy"] = new ZzzEmailServicePreset("smtpout.secureserver.net", 25, Secure: false),
		["GodaddyAsia"] = new ZzzEmailServicePreset("smtp.asia.secureserver.net", 25, Secure: false),
		["GodaddyEurope"] = new ZzzEmailServicePreset("smtp.europe.secureserver.net", 25, Secure: false),
		["hot.ee"] = new ZzzEmailServicePreset("mail.hot.ee", 587, Secure: false),
		["Hotmail"] = new ZzzEmailServicePreset("smtp-mail.outlook.com", 587, Secure: false),
		["iCloud"] = new ZzzEmailServicePreset("smtp.mail.me.com", 587, Secure: false),
		["Infomaniak"] = new ZzzEmailServicePreset("mail.infomaniak.com", 587, Secure: false),
		["Loopia"] = new ZzzEmailServicePreset("mailcluster.loopia.se", 465, Secure: false),
		["mail.ee"] = new ZzzEmailServicePreset("smtp.mail.ee", 587, Secure: false),
		["Mail.ru"] = new ZzzEmailServicePreset("smtp.mail.ru", 465, Secure: true),
		["Mailcatch.app"] = new ZzzEmailServicePreset("sandbox-smtp.mailcatch.app", 2525, Secure: false),
		["Maildev"] = new ZzzEmailServicePreset("localhost", 1025, Secure: false),
		["Mailgun"] = new ZzzEmailServicePreset("smtp.mailgun.org", 465, Secure: true),
		["Mailjet"] = new ZzzEmailServicePreset("in.mailjet.com", 587, Secure: false),
		["Mailosaur"] = new ZzzEmailServicePreset("mailosaur.io", 25, Secure: false),
		["Mailtrap"] = new ZzzEmailServicePreset("live.smtp.mailtrap.io", 587, Secure: false),
		["Mandrill"] = new ZzzEmailServicePreset("smtp.mandrillapp.com", 587, Secure: false),
		["Naver"] = new ZzzEmailServicePreset("smtp.naver.com", 587, Secure: false),
		["One"] = new ZzzEmailServicePreset("send.one.com", 465, Secure: true),
		["OpenMailBox"] = new ZzzEmailServicePreset("smtp.openmailbox.org", 465, Secure: true),
		["Outlook365"] = new ZzzEmailServicePreset("smtp.office365.com", 587, Secure: false),
		["OhMySMTP"] = new ZzzEmailServicePreset("smtp.ohmysmtp.com", 587, Secure: false),
		["Postmark"] = new ZzzEmailServicePreset("smtp.postmarkapp.com", 2525, Secure: false),
		["Proton"] = new ZzzEmailServicePreset("smtp.protonmail.ch", 587, Secure: false),
		["qiye.aliyun"] = new ZzzEmailServicePreset("smtp.mxhichina.com", 465, Secure: true),
		["QQ"] = new ZzzEmailServicePreset("smtp.qq.com", 465, Secure: true),
		["QQex"] = new ZzzEmailServicePreset("smtp.exmail.qq.com", 465, Secure: true),
		["SendCloud"] = new ZzzEmailServicePreset("smtp.sendcloud.net", 2525, Secure: false),
		["SendGrid"] = new ZzzEmailServicePreset("smtp.sendgrid.net", 587, Secure: false),
		["SendinBlue"] = new ZzzEmailServicePreset("smtp-relay.brevo.com", 587, Secure: false),
		["SendPulse"] = new ZzzEmailServicePreset("smtp-pulse.com", 465, Secure: true),
		["SES"] = new ZzzEmailServicePreset("email-smtp.us-east-1.amazonaws.com", 465, Secure: true),
		["SES-US-EAST-1"] = new ZzzEmailServicePreset("email-smtp.us-east-1.amazonaws.com", 465, Secure: true),
		["SES-US-WEST-2"] = new ZzzEmailServicePreset("email-smtp.us-west-2.amazonaws.com", 465, Secure: true),
		["SES-EU-WEST-1"] = new ZzzEmailServicePreset("email-smtp.eu-west-1.amazonaws.com", 465, Secure: true),
		["SES-AP-SOUTH-1"] = new ZzzEmailServicePreset("email-smtp.ap-south-1.amazonaws.com", 465, Secure: true),
		["SES-AP-NORTHEAST-1"] = new ZzzEmailServicePreset("email-smtp.ap-northeast-1.amazonaws.com", 465, Secure: true),
		["SES-AP-NORTHEAST-2"] = new ZzzEmailServicePreset("email-smtp.ap-northeast-2.amazonaws.com", 465, Secure: true),
		["SES-AP-NORTHEAST-3"] = new ZzzEmailServicePreset("email-smtp.ap-northeast-3.amazonaws.com", 465, Secure: true),
		["SES-AP-SOUTHEAST-1"] = new ZzzEmailServicePreset("email-smtp.ap-southeast-1.amazonaws.com", 465, Secure: true),
		["SES-AP-SOUTHEAST-2"] = new ZzzEmailServicePreset("email-smtp.ap-southeast-2.amazonaws.com", 465, Secure: true),
		["Seznam"] = new ZzzEmailServicePreset("smtp.seznam.cz", 465, Secure: true),
		["Sparkpost"] = new ZzzEmailServicePreset("smtp.sparkpostmail.com", 587, Secure: false),
		["Tipimail"] = new ZzzEmailServicePreset("smtp.tipimail.com", 587, Secure: false),
		["Yahoo"] = new ZzzEmailServicePreset("smtp.mail.yahoo.com", 465, Secure: true),
		["Yandex"] = new ZzzEmailServicePreset("smtp.yandex.ru", 465, Secure: true),
		["Zoho"] = new ZzzEmailServicePreset("smtp.zoho.com", 465, Secure: true)
	};

	/// <summary>
	/// 初始化通知服务。
	/// </summary>
	public ZzzPushNotificationService(ZzzRunRoot runRoot)
		: this(runRoot, CreateClient)
	{
	}

	internal ZzzPushNotificationService(ZzzRunRoot runRoot, Func<IWebProxy?, HttpClient> clientFactory)
	{
		_runRoot = runRoot;
		_clientFactory = clientFactory;
	}

	/// <inheritdoc />
	public async Task<ZzzPushTestResult> SendTestAsync(string? channelId, string title, string content, CancellationToken cancellationToken = default(CancellationToken), Mat? image = null)
	{
		Dictionary<string, string> config = ReadConfig(Path.Combine(_runRoot.Path, "config", "push.yml"));
		if (!bool.TryParse(Get(config, "send_image", "false"), out var sendImage) || !sendImage)
		{
			image = null;
		}
		string proxyMode = Get(config, "proxy", "NONE");
		string personalProxy = ReadConfig(Path.Combine(_runRoot.Path, "config", "env.yml")).GetValueOrDefault("personal_proxy", string.Empty);
		Uri proxyUri;
		IWebProxy proxy = ((string.Equals(proxyMode, "PERSONAL", StringComparison.Ordinal) && Uri.TryCreate(personalProxy, UriKind.Absolute, out proxyUri)) ? new WebProxy(proxyUri) : null);
		IReadOnlyList<ZzzPushChannelDescriptor> targets = ((channelId == null) ? Channels.Where((ZzzPushChannelDescriptor channel2) => IsConfigured(channel2, config)).ToArray() : Channels.Where((ZzzPushChannelDescriptor zzzPushChannelDescriptor) => string.Equals(zzzPushChannelDescriptor.ChannelId, channelId, StringComparison.Ordinal)).ToArray());
		if (targets.Count == 0)
		{
			return new ZzzPushTestResult(Success: false, (channelId == null) ? "没有可用的推送渠道" : ("推送渠道不存在: " + channelId));
		}
		List<string> errors = new List<string>();
		bool anySuccess = false;
		foreach (ZzzPushChannelDescriptor channel in targets)
		{
			string validationError = Validate(channel, config);
			if (validationError != null)
			{
				errors.Add(channel.ChannelId + " " + validationError);
				continue;
			}
			try
			{
				ZzzPushTestResult result = await SendChannelAsync(channel.ChannelId, config, title, content, image, proxy, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				anySuccess |= result.Success;
				if (!result.Success)
				{
					errors.Add(channel.ChannelId + " " + result.Message);
				}
			}
			catch (Exception ex) when (((ex is HttpRequestException || ex is SmtpException || ex is JsonException || ex is InvalidOperationException) ? 1 : 0) != 0)
			{
				errors.Add(channel.ChannelId + " " + ex.Message);
			}
		}
		return anySuccess ? new ZzzPushTestResult(Success: true, (errors.Count == 0) ? "推送成功" : string.Join(Environment.NewLine, errors)) : new ZzzPushTestResult(Success: false, (errors.Count == 0) ? "没有可用的推送渠道" : string.Join(Environment.NewLine, errors));
	}

	private async Task<ZzzPushTestResult> SendChannelAsync(string channelId, IReadOnlyDictionary<string, string> config, string title, string content, Mat? image, IWebProxy? proxy, CancellationToken cancellationToken)
	{
		if (string.Equals(channelId, "SMTP", StringComparison.Ordinal))
		{
			return await SendSmtpAsync(config, title, content, image, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		using HttpClient client = _clientFactory(proxy);
		if (string.Equals(channelId, "QYWX_APP", StringComparison.Ordinal))
		{
			return await SendWorkWeixinAppAsync(client, config, title, content, image, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (string.Equals(channelId, "DISCORD", StringComparison.Ordinal))
		{
			return await SendDiscordAsync(client, config, title, content, image, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (string.Equals(channelId, "QYWX", StringComparison.Ordinal))
		{
			return await SendWorkWeixinBotAsync(client, config, title, content, image, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (string.Equals(channelId, "ONEBOT", StringComparison.Ordinal))
		{
			return await SendOneBotAsync(client, config, title, content, image, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (string.Equals(channelId, "TG", StringComparison.Ordinal))
		{
			return await SendTelegramAsync(client, config, title, content, image, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (string.Equals(channelId, "NTFY", StringComparison.Ordinal))
		{
			return await SendNtfyAsync(client, config, title, content, image, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (string.Equals(channelId, "FS", StringComparison.Ordinal))
		{
			return await SendFeishuAsync(client, config, title, content, image, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		using HttpRequestMessage request = BuildHttpRequest(channelId, config, title, content, image);
		using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return response.IsSuccessStatusCode ? new ZzzPushTestResult(Success: true, "推送成功") : new ZzzPushTestResult(Success: false, $"HTTP {(int)response.StatusCode}: {responseBody}");
	}

	private static HttpRequestMessage BuildHttpRequest(string channelId, IReadOnlyDictionary<string, string> config, string title, string content, Mat? image = null)
	{
		if (1 == 0)
		{
		}
		int result2;
		HttpRequestMessage result = channelId switch
		{
			"WEBHOOK" => BuildWebhook(config, title, content, image), 
			"QYWX" => JsonPost(Get(config, "qywx_origin", "https://qyapi.weixin.qq.com").TrimEnd('/') + "/cgi-bin/webhook/send?key=" + Uri.EscapeDataString(Get(config, "qywx_key")), new
			{
				msgtype = "text",
				text = new
				{
					content = title + "\n" + content
				}
			}), 
			"FS" => JsonPost((string.Equals(Get(config, "fs_channel"), "Lark", StringComparison.Ordinal) ? "https://open.larksuite.com" : "https://open.feishu.cn") + "/open-apis/bot/v2/hook/" + Uri.EscapeDataString(Get(config, "fs_key")), new
			{
				msg_type = "text",
				content = new
				{
					text = title + "\n" + content
				}
			}), 
			"SERVERCHAN" => FormPost("https://sctapi.ftqq.com/" + Uri.EscapeDataString(Get(config, "serverchan_push_key")) + ".send", new Dictionary<string, string>
			{
				["title"] = title,
				["desp"] = content
			}), 
			"PUSH_PLUS" => JsonPost("https://www.pushplus.plus/send", new
			{
				token = Get(config, "push_plus_token"),
				title = title,
				content = content,
				template = Get(config, "push_plus_template", "html"),
				channel = Get(config, "push_plus_channel", "wechat")
			}), 
			"TG" => FormPost(Get(config, "tg_api_host", "https://api.telegram.org").TrimEnd('/') + "/bot" + Get(config, "tg_bot_token") + "/sendMessage", new Dictionary<string, string>
			{
				["chat_id"] = Get(config, "tg_user_id"),
				["text"] = title + "\n" + content
			}), 
			"NTFY" => TextPost(Get(config, "ntfy_url", "https://ntfy.sh").TrimEnd('/') + "/" + Uri.EscapeDataString(Get(config, "ntfy_topic")), content, new Dictionary<string, string>
			{
				["Title"] = title,
				["Priority"] = Get(config, "ntfy_priority", "3")
			}), 
			"GOTIFY" => JsonPost(Get(config, "gotify_url").TrimEnd('/') + "/message?token=" + Uri.EscapeDataString(Get(config, "gotify_token")), new
			{
				title = title,
				message = content,
				priority = (int.TryParse(Get(config, "gotify_priority", "5"), out result2) ? result2 : 5)
			}), 
			"WXPUSHER" => JsonPost("https://wxpusher.zjiecode.com/api/send/message", new
			{
				appToken = Get(config, "wxpusher_app_token"),
				content = title + "\n" + content,
				contentType = 1,
				topicIds = Split(Get(config, "wxpusher_topic_ids")),
				uids = Split(Get(config, "wxpusher_uids"))
			}), 
			"QMSG" => FormPost("https://qmsg.zendee.cn/" + Get(config, "qmsg_type", "send") + "/" + Uri.EscapeDataString(Get(config, "qmsg_key")), new Dictionary<string, string> { ["msg"] = title + "\n" + content }), 
			"DEER" => FormPost(Get(config, "deer_url", "https://api2.pushdeer.com").TrimEnd('/') + "/message/push", new Dictionary<string, string>
			{
				["pushkey"] = Get(config, "deer_key"),
				["text"] = title,
				["desp"] = content,
				["type"] = "markdown"
			}), 
			"IGOT" => FormPost("https://push.hellyw.com/" + Uri.EscapeDataString(Get(config, "igot_push_key")), new Dictionary<string, string>
			{
				["title"] = title,
				["content"] = content
			}), 
			"SYNOLOGY_CHAT" => FormPost(Get(config, "synology_chat_url") + Get(config, "synology_chat_token"), new Dictionary<string, string> { ["payload"] = JsonSerializer.Serialize(new
			{
				text = title + "\n" + content
			}) }), 
			"ONEBOT" => JsonPost(Get(config, "onebot_url"), new
			{
				message = title + "\n" + content,
				user_id = Get(config, "onebot_user"),
				group_id = Get(config, "onebot_group")
			}, Get(config, "onebot_token")), 
			"BARK" => JsonPost(NormalizeBarkUrl(Get(config, "bark_push")), new
			{
				title = title,
				body = content,
				device_key = Get(config, "bark_device_key"),
				group = Get(config, "bark_group"),
				sound = Get(config, "bark_sound"),
				icon = Get(config, "bark_icon"),
				level = Get(config, "bark_level", "active"),
				url = Get(config, "bark_url")
			}), 
			"PUSHME" => FormPost(string.IsNullOrWhiteSpace(Get(config, "pushme_url")) ? "https://push.i-i.me" : Get(config, "pushme_url"), new Dictionary<string, string>
			{
				["push_key"] = Get(config, "pushme_key"),
				["title"] = title,
				["content"] = content
			}), 
			"AIBOTK" => JsonPost("https://api-bot.aibotk.com/openapi/v1/chat/notification", new
			{
				apiKey = Get(config, "aibotk_key"),
				type = Get(config, "aibotk_type", "contact"),
				name = Get(config, "aibotk_name"),
				content = title + "\n" + content
			}), 
			"WE_PLUS_BOT" => JsonPost("https://www.weplusbot.com/api/v1/send", new
			{
				token = Get(config, "we_plus_bot_token"),
				receiver = Get(config, "we_plus_bot_receiver"),
				content = title + "\n" + content,
				version = Get(config, "we_plus_bot_version")
			}), 
			"CHRONOCAT" => JsonPost(Get(config, "chronocat_url").TrimEnd('/') + "/api/message", new
			{
				message = title + "\n" + content,
				targets = Get(config, "chronocat_qq")
			}, Get(config, "chronocat_token")), 
			"DD_BOT" => BuildDingTalk(config, title, content), 
			"FAKE" => throw new InvalidOperationException("不支持"), 
			_ => throw new InvalidOperationException("推送渠道不存在: " + channelId), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private static async Task<ZzzPushTestResult> SendFeishuAsync(HttpClient client, IReadOnlyDictionary<string, string> config, string title, string content, Mat? image, CancellationToken cancellationToken)
	{
		string baseUrl = (string.Equals(Get(config, "fs_channel"), "Lark", StringComparison.Ordinal) ? "https://open.larksuite.com" : "https://open.feishu.cn");
		string imageKey = null;
		string appId = Get(config, "fs_appid");
		string appSecret = Get(config, "fs_appsecret");
		if (image != null && !string.IsNullOrWhiteSpace(appId) && !string.IsNullOrWhiteSpace(appSecret))
		{
			using HttpRequestMessage authRequest = JsonPost(baseUrl + "/open-apis/auth/v3/tenant_access_token/internal", new
			{
				app_id = appId,
				app_secret = appSecret
			});
			using HttpResponseMessage authResponse = await client.SendAsync(authRequest, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			string authBody = await authResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!authResponse.IsSuccessStatusCode || !TryReadJsonString(authBody, "tenant_access_token", out string accessToken))
			{
				return new ZzzPushTestResult(Success: false, "飞书图片上传失败：无法获取 tenant_access_token");
			}
			if (!TryEncodeJpeg(image, out byte[] jpeg))
			{
				return new ZzzPushTestResult(Success: false, "飞书图片上传失败：图片转换失败");
			}
			using MultipartFormDataContent multipart = new MultipartFormDataContent();
			ByteArrayContent imageContent = new ByteArrayContent(jpeg);
			imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
			multipart.Add(imageContent, "image", "image.jpg");
			multipart.Add(new StringContent("message", Encoding.UTF8), "image_type");
			using HttpRequestMessage uploadRequest = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/open-apis/im/v1/images")
			{
				Content = multipart
			};
			uploadRequest.Headers.TryAddWithoutValidation("Authorization", "Bearer " + accessToken);
			using HttpResponseMessage uploadResponse = await client.SendAsync(uploadRequest, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			string uploadBody = await uploadResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!uploadResponse.IsSuccessStatusCode || !TryReadFeishuImageKey(uploadBody, out imageKey))
			{
				return new ZzzPushTestResult(Success: false, "飞书图片上传失败");
			}
		}
		using HttpRequestMessage webhookRequest = JsonPost(body: (imageKey == null) ? ((object)new
		{
			msg_type = "text",
			content = new
			{
				text = title + "\n" + content
			}
		}) : ((object)new
		{
			msg_type = "post",
			content = new
			{
				post = new
				{
					zh_cn = new
					{
						title = title,
						content = new object[1][] { new object[2]
						{
							new
							{
								tag = "text",
								text = content
							},
							new
							{
								tag = "img",
								image_key = imageKey
							}
						} }
					}
				}
			}
		}), url: baseUrl + "/open-apis/bot/v2/hook/" + Uri.EscapeDataString(Get(config, "fs_key")));
		using HttpResponseMessage webhookResponse = await client.SendAsync(webhookRequest, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		string webhookBody = await webhookResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return (webhookResponse.IsSuccessStatusCode && IsFeishuSuccess(webhookBody)) ? new ZzzPushTestResult(Success: true, "推送成功") : new ZzzPushTestResult(Success: false, string.IsNullOrWhiteSpace(webhookBody) ? "飞书推送失败" : webhookBody);
	}

	private static HttpRequestMessage BuildDingTalk(IReadOnlyDictionary<string, string> config, string title, string content)
	{
		long value = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		string text = Get(config, "dd_bot_secret");
		byte[] inArray = HMACSHA256.HashData(Encoding.UTF8.GetBytes(text), Encoding.UTF8.GetBytes($"{value}\n{text}"));
		string value2 = Uri.EscapeDataString(Convert.ToBase64String(inArray));
		string url = $"https://oapi.dingtalk.com/robot/send?access_token={Uri.EscapeDataString(Get(config, "dd_bot_token"))}&timestamp={value}&sign={value2}";
		return JsonPost(url, new
		{
			msgtype = "text",
			text = new
			{
				content = title + "\n" + content
			}
		});
	}

	private static async Task<ZzzPushTestResult> SendWorkWeixinAppAsync(HttpClient client, IReadOnlyDictionary<string, string> config, string title, string content, Mat? image, CancellationToken cancellationToken)
	{
		string tokenUrl = "https://qyapi.weixin.qq.com/cgi-bin/gettoken?corpid=" + Uri.EscapeDataString(Get(config, "qywx_app_corp_id")) + "&corpsecret=" + Uri.EscapeDataString(Get(config, "qywx_app_corp_secret"));
		using HttpResponseMessage tokenResponse = await client.GetAsync(tokenUrl, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		string tokenBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		tokenResponse.EnsureSuccessStatusCode();
		using JsonDocument tokenDocument = JsonDocument.Parse(tokenBody);
		JsonElement error;
		if (!tokenDocument.RootElement.TryGetProperty("access_token", out var tokenElement))
		{
			return new ZzzPushTestResult(Success: false, tokenDocument.RootElement.TryGetProperty("errmsg", out error) ? (error.GetString() ?? "获取 access_token 失败") : "获取 access_token 失败");
		}
		string accessToken = tokenElement.GetString() ?? string.Empty;
		string url = "https://qyapi.weixin.qq.com/cgi-bin/message/send?access_token=" + Uri.EscapeDataString(accessToken);
		object textPayload = new
		{
			touser = Get(config, "qywx_app_to_user", "@all"),
			msgtype = "text",
			agentid = Get(config, "qywx_app_agent_id"),
			text = new
			{
				content = title + "\n" + content
			},
			safe = "0"
		};
		if (image == null || !TryEncodeJpeg(image, out byte[] jpeg, 2097152))
		{
			return await SendWorkWeixinAppPayloadAsync(client, url, textPayload, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		string permanentImageUrl = await UploadWorkWeixinImageAsync(client, "https://qyapi.weixin.qq.com/cgi-bin/media/uploadimg?access_token=" + Uri.EscapeDataString(accessToken), jpeg, (string value) => TryReadJsonString(value, "url", out string value2) ? value2 : null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		string thumbMediaId = await UploadWorkWeixinImageAsync(client, "https://qyapi.weixin.qq.com/cgi-bin/media/upload?access_token=" + Uri.EscapeDataString(accessToken) + "&type=image", jpeg, (string value) => TryReadJsonString(value, "media_id", out string value2) ? value2 : null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (string.IsNullOrWhiteSpace(thumbMediaId))
		{
			return await SendWorkWeixinAppPayloadAsync(client, url, textPayload, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		string htmlContent = content.Replace("\n", "<br/>\n", StringComparison.Ordinal);
		if (!string.IsNullOrWhiteSpace(permanentImageUrl))
		{
			htmlContent = htmlContent + "<br/>\n<img src=\"" + permanentImageUrl + "\">";
		}
		object mpnewsPayload = new
		{
			touser = Get(config, "qywx_app_to_user", "@all"),
			msgtype = "mpnews",
			agentid = Get(config, "qywx_app_agent_id"),
			mpnews = new
			{
				articles = new[]
				{
					new
					{
						title = title,
						thumb_media_id = thumbMediaId,
						author = "OneDragon",
						content_source_url = string.Empty,
						content = htmlContent,
						digest = content
					}
				}
			},
			safe = "0"
		};
		ZzzPushTestResult mpnewsResult = await SendWorkWeixinAppPayloadAsync(client, url, mpnewsPayload, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return (!mpnewsResult.Success) ? (await SendWorkWeixinAppPayloadAsync(client, url, textPayload, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) : mpnewsResult;
	}

	private static async Task<ZzzPushTestResult> SendWorkWeixinAppPayloadAsync(HttpClient client, string url, object payload, CancellationToken cancellationToken)
	{
		using HttpRequestMessage request = JsonPost(url, payload);
		using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return (response.IsSuccessStatusCode && IsApiSuccess(body, "errcode")) ? new ZzzPushTestResult(Success: true, "推送成功") : new ZzzPushTestResult(Success: false, body);
	}

	private static async Task<string?> UploadWorkWeixinImageAsync(HttpClient client, string url, byte[] jpeg, Func<string, string?> extract, CancellationToken cancellationToken)
	{
		using MultipartFormDataContent multipart = new MultipartFormDataContent();
		ByteArrayContent media = new ByteArrayContent(jpeg);
		media.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
		multipart.Add(media, "media", "image.jpg");
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url)
		{
			Content = multipart
		};
		using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return (response.IsSuccessStatusCode && IsApiSuccess(body, "errcode")) ? extract(body) : null;
	}

	private static async Task<ZzzPushTestResult> SendDiscordAsync(HttpClient client, IReadOnlyDictionary<string, string> config, string title, string content, Mat? image, CancellationToken cancellationToken)
	{
		string host = Get(config, "discord_api_host", "https://discord.com/api/v9").TrimEnd('/');
		string authorization = "Bot " + Get(config, "discord_bot_token");
		using HttpRequestMessage channelRequest = JsonPost(host + "/users/@me/channels", new
		{
			recipient_id = Get(config, "discord_user_id")
		}, authorization);
		using HttpResponseMessage channelResponse = await client.SendAsync(channelRequest, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		string channelBody = await channelResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		channelResponse.EnsureSuccessStatusCode();
		using JsonDocument channelDocument = JsonDocument.Parse(channelBody);
		if (!channelDocument.RootElement.TryGetProperty("id", out var idElement))
		{
			return new ZzzPushTestResult(Success: false, "Discord 私信频道创建失败");
		}
		using HttpRequestMessage messageRequest = ((image == null) ? JsonPost(host + "/channels/" + idElement.GetString() + "/messages", new
		{
			content = title + "\n" + content
		}, authorization) : CreateDiscordImageRequest(host + "/channels/" + idElement.GetString() + "/messages", title, content, image, authorization));
		using HttpResponseMessage messageResponse = await client.SendAsync(messageRequest, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		string messageBody = await messageResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return messageResponse.IsSuccessStatusCode ? new ZzzPushTestResult(Success: true, "推送成功") : new ZzzPushTestResult(Success: false, messageBody);
	}

	private static async Task<ZzzPushTestResult> SendWorkWeixinBotAsync(HttpClient client, IReadOnlyDictionary<string, string> config, string title, string content, Mat? image, CancellationToken cancellationToken)
	{
		string origin = Get(config, "qywx_origin", "https://qyapi.weixin.qq.com").TrimEnd('/');
		string url = origin + "/cgi-bin/webhook/send?key=" + Uri.EscapeDataString(Get(config, "qywx_key"));
		using HttpResponseMessage textResponse = await client.SendAsync(JsonPost(url, new
		{
			msgtype = "text",
			text = new
			{
				content = title + "\n" + content
			}
		}), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		string textBody = await textResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		bool textSuccess = textResponse.IsSuccessStatusCode && IsApiSuccess(textBody, "errcode");
		if (image == null)
		{
			return textSuccess ? new ZzzPushTestResult(Success: true, "推送成功") : new ZzzPushTestResult(Success: false, textBody);
		}
		if (!TryEncodeJpeg(image, out byte[] jpeg, 2097152))
		{
			return textSuccess ? new ZzzPushTestResult(Success: true, "部分推送成功：图片转换失败") : new ZzzPushTestResult(Success: false, "图片转换失败");
		}
		string base64 = Convert.ToBase64String(jpeg);
		string md5 = Convert.ToHexString(MD5.HashData(jpeg)).ToLowerInvariant();
		using HttpResponseMessage imageResponse = await client.SendAsync(JsonPost(url, new
		{
			msgtype = "image",
			image = new { base64, md5 }
		}), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		string imageBody = await imageResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		bool imageSuccess = imageResponse.IsSuccessStatusCode && IsApiSuccess(imageBody, "errcode");
		return (textSuccess || imageSuccess) ? new ZzzPushTestResult(Success: true, (textSuccess && imageSuccess) ? "推送成功" : "部分推送成功") : new ZzzPushTestResult(Success: false, string.Join(Environment.NewLine, new string[2] { textBody, imageBody }.Where((string item) => !string.IsNullOrWhiteSpace(item))));
	}

	private static async Task<ZzzPushTestResult> SendOneBotAsync(HttpClient client, IReadOnlyDictionary<string, string> config, string title, string content, Mat? image, CancellationToken cancellationToken)
	{
		string url = Get(config, "onebot_url").TrimEnd('/');
		url = (url.EndsWith("/send_msg", StringComparison.Ordinal) ? url : (url + "/send_msg"));
		List<object> message = new List<object>
		{
			new
			{
				type = "text",
				data = new
				{
					text = title + "\n" + content
				}
			}
		};
		if (image != null && TryEncodeJpeg(image, out byte[] jpeg))
		{
			message.Add(new
			{
				type = "image",
				data = new
				{
					file = "base64://" + Convert.ToBase64String(jpeg)
				}
			});
		}
		string authorization = (string.IsNullOrWhiteSpace(Get(config, "onebot_token")) ? null : ("Bearer " + Get(config, "onebot_token")));
		int attempts = 0;
		int successes = 0;
		List<string> errors = new List<string>();
		(string, string, string)[] array = new(string, string, string)[2]
		{
			("private", "user_id", Get(config, "onebot_user")),
			("group", "group_id", Get(config, "onebot_group"))
		};
		for (int i = 0; i < array.Length; i++)
		{
			var (type, idName, id) = array[i];
			if (string.IsNullOrWhiteSpace(id))
			{
				continue;
			}
			attempts++;
			Dictionary<string, object> payload = new Dictionary<string, object>
			{
				["message"] = message,
				["message_type"] = type,
				[idName] = id
			};
			string status;
			using (HttpRequestMessage request = JsonPost(url, payload, authorization))
			{
				using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (response.IsSuccessStatusCode && TryReadJsonString(body, "status", out status) && string.Equals(status, "ok", StringComparison.Ordinal))
				{
					successes++;
				}
				else
				{
					errors.Add(body);
				}
			}
			status = null;
		}
		return (successes > 0) ? new ZzzPushTestResult(Success: true, (successes == attempts) ? "推送成功" : "部分推送成功") : new ZzzPushTestResult(Success: false, (errors.Count == 0) ? "未配置有效的接收者" : string.Join(Environment.NewLine, errors));
	}

	private static async Task<ZzzPushTestResult> SendTelegramAsync(HttpClient client, IReadOnlyDictionary<string, string> config, string title, string content, Mat? image, CancellationToken cancellationToken)
	{
		string host = Get(config, "tg_api_host", "https://api.telegram.org").TrimEnd('/');
		string token = Get(config, "tg_bot_token");
		string userId = Get(config, "tg_user_id");
		using HttpRequestMessage request = ((image == null) ? new HttpRequestMessage(HttpMethod.Post, host + "/bot" + token + "/sendMessage")
		{
			Content = new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["chat_id"] = userId,
				["text"] = title + "\n" + content
			})
		} : CreateTelegramPhotoRequest(host + "/bot" + token + "/sendPhoto", userId, title, content, image));
		using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return (response.IsSuccessStatusCode && IsBooleanJsonSuccess(body, "ok")) ? new ZzzPushTestResult(Success: true, "推送成功") : new ZzzPushTestResult(Success: false, body);
	}

	private static async Task<ZzzPushTestResult> SendNtfyAsync(HttpClient client, IReadOnlyDictionary<string, string> config, string title, string content, Mat? image, CancellationToken cancellationToken)
	{
		string url = Get(config, "ntfy_url", "https://ntfy.sh").TrimEnd('/') + "/" + Uri.EscapeDataString(Get(config, "ntfy_topic"));
		Dictionary<string, string> headers = new Dictionary<string, string>
		{
			["Title"] = "=?utf-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(title)) + "?=",
			["Priority"] = Get(config, "ntfy_priority", "3")
		};
		if (!string.IsNullOrWhiteSpace(Get(config, "ntfy_token")))
		{
			headers["Authorization"] = "Bearer " + Get(config, "ntfy_token");
		}
		else if (!string.IsNullOrWhiteSpace(Get(config, "ntfy_username")) && !string.IsNullOrWhiteSpace(Get(config, "ntfy_password")))
		{
			headers["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(Get(config, "ntfy_username") + ":" + Get(config, "ntfy_password")));
		}
		if (!string.IsNullOrWhiteSpace(Get(config, "ntfy_actions")))
		{
			headers["Actions"] = "=?utf-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Get(config, "ntfy_actions"))) + "?=";
		}
		List<byte[]> payloads = new List<byte[]>(1) { Encoding.UTF8.GetBytes(content) };
		if (image != null)
		{
			if (!TryEncodeJpeg(image, out byte[] jpeg))
			{
				return new ZzzPushTestResult(Success: false, "图片处理失败");
			}
			payloads.Insert(0, jpeg);
		}
		int successCount = 0;
		foreach (byte[] payload in payloads)
		{
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url)
			{
				Content = new ByteArrayContent(payload)
			};
			foreach (var (key, value) in headers)
			{
				request.Headers.TryAddWithoutValidation(key, value);
			}
			using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (response.IsSuccessStatusCode)
			{
				successCount++;
			}
		}
		return (successCount == payloads.Count) ? new ZzzPushTestResult(Success: true, "Ntfy 推送成功！") : ((successCount > 0) ? new ZzzPushTestResult(Success: true, "部分 Ntfy 推送成功！") : new ZzzPushTestResult(Success: false, "Ntfy 推送失败！"));
	}

	private static bool IsApiSuccess(string body, string codeProperty)
	{
		using JsonDocument jsonDocument = JsonDocument.Parse(body);
		JsonElement value;
		return !jsonDocument.RootElement.TryGetProperty(codeProperty, out value) || (value.ValueKind == JsonValueKind.Number && value.GetInt32() == 0);
	}

	private static bool IsBooleanJsonSuccess(string body, string propertyName)
	{
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(body);
			JsonElement value;
			return jsonDocument.RootElement.TryGetProperty(propertyName, out value) && value.ValueKind == JsonValueKind.True;
		}
		catch (JsonException)
		{
			return false;
		}
	}

	private static bool IsFeishuSuccess(string body)
	{
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(body);
			JsonElement rootElement = jsonDocument.RootElement;
			JsonElement value;
			JsonElement value2;
			return (!rootElement.TryGetProperty("StatusCode", out value)) ? (!rootElement.TryGetProperty("code", out value2) || (value2.ValueKind == JsonValueKind.Number && value2.GetInt32() == 0)) : (value.ValueKind == JsonValueKind.Number && value.GetInt32() == 0);
		}
		catch (JsonException)
		{
			return false;
		}
	}

	private static bool TryReadJsonString(string body, string propertyName, out string? value)
	{
		value = null;
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(body);
			if (!jsonDocument.RootElement.TryGetProperty(propertyName, out var value2) || value2.ValueKind != JsonValueKind.String)
			{
				return false;
			}
			value = value2.GetString();
			return !string.IsNullOrWhiteSpace(value);
		}
		catch (JsonException)
		{
			return false;
		}
	}

	private static bool TryReadFeishuImageKey(string body, out string? imageKey)
	{
		imageKey = null;
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(body);
			JsonElement rootElement = jsonDocument.RootElement;
			if (!IsFeishuSuccess(body) || !rootElement.TryGetProperty("data", out var value) || !value.TryGetProperty("image_key", out var value2) || value2.ValueKind != JsonValueKind.String)
			{
				return false;
			}
			imageKey = value2.GetString();
			return !string.IsNullOrWhiteSpace(imageKey);
		}
		catch (JsonException)
		{
			return false;
		}
	}

	private static bool TryEncodeJpeg(Mat image, out byte[] bytes, int? maxBytes = null)
	{
		if (!Cv2.ImEncode(".jpg", image, out bytes))
		{
			return false;
		}
		if (!maxBytes.HasValue || bytes.Length <= maxBytes.Value)
		{
			return true;
		}
		byte[] array = null;
		int num = 30;
		int num2 = 90;
		while (num <= num2)
		{
			int num3 = (num + num2) / 2;
			InputArray img = image;
			int[] obj = new int[6] { 1, 0, 3, 1, 2, 1 };
			obj[1] = num3;
			if (!Cv2.ImEncode(".jpg", img, out byte[] buf, obj))
			{
				return false;
			}
			if (buf.Length <= maxBytes.Value)
			{
				array = buf;
				num = num3 + 1;
			}
			else
			{
				num2 = num3 - 1;
			}
		}
		bytes = array ?? Array.Empty<byte>();
		return array != null;
	}

	private static HttpRequestMessage CreateTelegramPhotoRequest(string url, string userId, string title, string content, Mat image)
	{
		if (!TryEncodeJpeg(image, out byte[] bytes))
		{
			throw new InvalidOperationException("图片处理失败");
		}
		MultipartFormDataContent multipartFormDataContent = new MultipartFormDataContent();
		ByteArrayContent byteArrayContent = new ByteArrayContent(bytes);
		byteArrayContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
		multipartFormDataContent.Add(byteArrayContent, "photo", "image.jpg");
		multipartFormDataContent.Add(new StringContent(userId, Encoding.UTF8), "chat_id");
		multipartFormDataContent.Add(new StringContent(title + "\n" + content, Encoding.UTF8), "caption");
		return new HttpRequestMessage(HttpMethod.Post, url)
		{
			Content = multipartFormDataContent
		};
	}

	private static HttpRequestMessage CreateDiscordImageRequest(string url, string title, string content, Mat image, string authorization)
	{
		if (!TryEncodeJpeg(image, out byte[] bytes))
		{
			// 图片编码失败时回退为不带图的纯文本请求，而不是让整次推送失败。
			return JsonPost(url, new
			{
				content = title + "\n" + content
			}, authorization);
		}
		MultipartFormDataContent multipartFormDataContent = new MultipartFormDataContent();
		multipartFormDataContent.Add(new StringContent(JsonSerializer.Serialize(new
		{
			embeds = new[]
			{
				new
				{
					title = title,
					description = content,
					thumbnail = new
					{
						url = "attachment://screenshot.jpg"
					}
				}
			}
		}, UnescapedJsonOptions), Encoding.UTF8, "application/json"), "payload_json");
		ByteArrayContent byteArrayContent = new ByteArrayContent(bytes);
		byteArrayContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
		multipartFormDataContent.Add(byteArrayContent, "files[0]", "screenshot.jpg");
		HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, url)
		{
			Content = multipartFormDataContent
		};
		httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authorization);
		return httpRequestMessage;
	}

	private static HttpRequestMessage BuildWebhook(IReadOnlyDictionary<string, string> config, string title, string content, Mat? image)
	{
		string requestUri = ReplaceUrlVariables(Get(config, "webhook_url"), title, content);
		string text = Get(config, "webhook_method", "POST");
		string mediaType = Get(config, "webhook_content_type", "application/json");
		string content2 = ReplaceVariables(Get(config, "webhook_body"), title, content, image);
		HttpRequestMessage httpRequestMessage = new HttpRequestMessage(new HttpMethod(text), requestUri);
		if (!string.Equals(text, "GET", StringComparison.OrdinalIgnoreCase))
		{
			httpRequestMessage.Content = new StringContent(content2, Encoding.UTF8, mediaType);
		}
		string headers = ReplaceVariables(Get(config, "webhook_headers", "{}"), title, content, image);
		foreach (var (name, value) in ParseWebhookHeaders(headers))
		{
			httpRequestMessage.Headers.TryAddWithoutValidation(name, value);
		}
		return httpRequestMessage;
	}

	private static IReadOnlyDictionary<string, string> ParseWebhookHeaders(string headers)
	{
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(headers) ? "{}" : headers);
			return (jsonDocument.RootElement.ValueKind == JsonValueKind.Object) ? jsonDocument.RootElement.EnumerateObject().ToDictionary<JsonProperty, string, string>((JsonProperty item) => item.Name, (JsonProperty item) => item.Value.ToString(), StringComparer.Ordinal) : new Dictionary<string, string>(StringComparer.Ordinal);
		}
		catch (JsonException)
		{
			return (from line in headers.Split('\n', StringSplitOptions.RemoveEmptyEntries)
				select line.Split(':', 2) into parts
				where parts.Length == 2
				select parts).ToDictionary<string[], string, string>((string[] parts) => parts[0].Trim(), (string[] parts) => parts[1].Trim(), StringComparer.Ordinal);
		}
	}

	private static async Task<ZzzPushTestResult> SendSmtpAsync(IReadOnlyDictionary<string, string> config, string title, string content, Mat? image, CancellationToken cancellationToken)
	{
		string[] server = Get(config, "smtp_server").Split(':', 2);
		int configuredPort;
		int port = ((server.Length == 2 && int.TryParse(server[1], out configuredPort)) ? configuredPort : 465);
		string email = Get(config, "smtp_email");
		string displayName = Get(config, "smtp_name", "OneDragon");
		using MailMessage message = new MailMessage(new MailAddress(email, displayName), new MailAddress(email, displayName))
		{
			Subject = title,
			Body = content,
			BodyEncoding = Encoding.UTF8,
			SubjectEncoding = Encoding.UTF8
		};
		if (image != null && TryEncodeJpeg(image, out byte[] jpeg))
		{
			string html = "<p>" + WebUtility.HtmlEncode(content).Replace("\n", "<br>\n", StringComparison.Ordinal) + "</p><br><img src=\"cid:screenshot\">";
			AlternateView view = AlternateView.CreateAlternateViewFromString(html, Encoding.UTF8, "text/html");
			LinkedResource linked = new LinkedResource(new MemoryStream(jpeg), "image/jpeg")
			{
				ContentId = "screenshot"
			};
			view.LinkedResources.Add(linked);
			message.AlternateViews.Add(view);
		}
		bool ssl;
		bool starttls;
		bool.TryParse(Get(config, "smtp_ssl", "true"), out ssl);
		bool.TryParse(Get(config, "smtp_starttls", "false"), out starttls);
		using SmtpClient client = new SmtpClient(server[0], port)
		{
			// System.Net.Mail.SmtpClient 的 EnableSsl 只表达 STARTTLS 语义（连接后升级），
			// 没有真正的隐式 SSL；ssl 或 starttls 任一开启都需要它。
			EnableSsl = ssl || starttls,
			Credentials = new NetworkCredential(email, Get(config, "smtp_password"))
		};
		await client.SendMailAsync(message, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return new ZzzPushTestResult(Success: true, "推送成功");
	}

	private static string? Validate(ZzzPushChannelDescriptor channel, IReadOnlyDictionary<string, string> config)
	{
		foreach (ZzzPushFieldDescriptor item in channel.Fields.Where((ZzzPushFieldDescriptor field) => field.Required))
		{
			if (string.IsNullOrWhiteSpace(Get(config, item.Key, item.DefaultValue)))
			{
				return item.Title + " 不能为空";
			}
		}
		return null;
	}

	private static bool IsConfigured(ZzzPushChannelDescriptor channel, IReadOnlyDictionary<string, string> config)
	{
		return channel.Fields.Count > 0 && channel.Fields.Where((ZzzPushFieldDescriptor field) => field.Required).All((ZzzPushFieldDescriptor field) => !string.IsNullOrWhiteSpace(Get(config, field.Key, field.DefaultValue)));
	}

	private static Dictionary<string, string> ReadConfig(string path)
	{
		if (!File.Exists(path) || string.IsNullOrWhiteSpace(File.ReadAllText(path)))
		{
			return new Dictionary<string, string>(StringComparer.Ordinal);
		}
		Dictionary<string, object> source = Deserializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path)) ?? new Dictionary<string, object>();
		return source.ToDictionary<KeyValuePair<string, object>, string, string>((KeyValuePair<string, object> pair) => pair.Key, (KeyValuePair<string, object> pair) => Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty, StringComparer.Ordinal);
	}

	private static HttpClient CreateClient(IWebProxy? proxy)
	{
		HttpClientHandler handler = new HttpClientHandler
		{
			AutomaticDecompression = DecompressionMethods.All,
			Proxy = proxy,
			UseProxy = (proxy != null)
		};
		return new HttpClient(handler, disposeHandler: true)
		{
			Timeout = TimeSpan.FromSeconds(20L)
		};
	}

	private static HttpRequestMessage JsonPost(string url, object body, string? authorization = null)
	{
		HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, url)
		{
			Content = JsonContent.Create(body)
		};
		if (!string.IsNullOrWhiteSpace(authorization))
		{
			httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authorization);
		}
		return httpRequestMessage;
	}

	private static HttpRequestMessage FormPost(string url, IReadOnlyDictionary<string, string> values)
	{
		return new HttpRequestMessage(HttpMethod.Post, url)
		{
			Content = new FormUrlEncodedContent(values)
		};
	}

	private static HttpRequestMessage TextPost(string url, string content, IReadOnlyDictionary<string, string> headers)
	{
		HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, url)
		{
			Content = new StringContent(content, Encoding.UTF8, "text/plain")
		};
		foreach (var (name, value) in headers)
		{
			httpRequestMessage.Headers.TryAddWithoutValidation(name, value);
		}
		return httpRequestMessage;
	}

	private static string ReplaceVariables(string value, string title, string content, Mat? image = null)
	{
		DateTimeOffset now = DateTimeOffset.Now;
		byte[] bytes;
		return value.Replace("$title", title.Replace("\n", "\\n", StringComparison.Ordinal), StringComparison.Ordinal).Replace("{{title}}", title.Replace("\n", "\\n", StringComparison.Ordinal), StringComparison.Ordinal).Replace("$content", content.Replace("\n", "\\n", StringComparison.Ordinal), StringComparison.Ordinal)
			.Replace("{{content}}", content.Replace("\n", "\\n", StringComparison.Ordinal), StringComparison.Ordinal)
			.Replace("$timestamp", now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), StringComparison.Ordinal)
			.Replace("{{timestamp}}", now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), StringComparison.Ordinal)
			.Replace("$iso_timestamp", now.ToString("O", CultureInfo.InvariantCulture), StringComparison.Ordinal)
			.Replace("{{iso_timestamp}}", now.ToString("O", CultureInfo.InvariantCulture), StringComparison.Ordinal)
			.Replace("$unix_timestamp", now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
			.Replace("{{unix_timestamp}}", now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
			.Replace("$image", (image != null && TryEncodeJpeg(image, out bytes)) ? Convert.ToBase64String(bytes) : string.Empty, StringComparison.Ordinal);
	}

	private static string ReplaceUrlVariables(string value, string title, string content)
	{
		DateTimeOffset now = DateTimeOffset.Now;
		return value.Replace("$title", WebUtility.UrlEncode(title), StringComparison.Ordinal).Replace("{{title}}", WebUtility.UrlEncode(title), StringComparison.Ordinal).Replace("$content", WebUtility.UrlEncode(content), StringComparison.Ordinal)
			.Replace("{{content}}", WebUtility.UrlEncode(content), StringComparison.Ordinal)
			.Replace("$timestamp", WebUtility.UrlEncode(now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)), StringComparison.Ordinal)
			.Replace("{{timestamp}}", WebUtility.UrlEncode(now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)), StringComparison.Ordinal)
			.Replace("$iso_timestamp", WebUtility.UrlEncode(now.ToString("O", CultureInfo.InvariantCulture)), StringComparison.Ordinal)
			.Replace("{{iso_timestamp}}", WebUtility.UrlEncode(now.ToString("O", CultureInfo.InvariantCulture)), StringComparison.Ordinal)
			.Replace("$unix_timestamp", now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
			.Replace("{{unix_timestamp}}", now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
	}

	private static string Get(IReadOnlyDictionary<string, string> values, string key, string defaultValue = "")
	{
		string value;
		return values.TryGetValue(key, out value) ? value : defaultValue;
	}

	private static string[] Split(string value)
	{
		return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	}

	private static string NormalizeBarkUrl(string push)
	{
		return push.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? push : ("https://api.day.app/" + Uri.EscapeDataString(push));
	}
}
