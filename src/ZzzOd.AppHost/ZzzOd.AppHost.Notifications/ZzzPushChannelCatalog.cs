using System;
using System.Collections.Generic;
using System.Linq;

namespace ZzzOd.AppHost.Notifications;

/// <summary>
/// 与 BaselineParity PushService.init_push_channels() 顺序一致的通知渠道合同。
/// </summary>
public static class ZzzPushChannelCatalog
{
	/// <summary>
	/// 通知渠道。
	/// </summary>
	public static IReadOnlyList<ZzzPushChannelDescriptor> Channels { get; } = new ZzzPushChannelDescriptor[24]
	{
		Channel("SMTP", "SMTP邮件", Text("SERVER", "邮件服务器", "smtp.exmail.qq.com:465", required: true), Combo("SSL", "使用 SSL", "true", "true", "false"), Combo("STARTTLS", "使用 STARTTLS", "false", "true", "false"), Text("EMAIL", "收发件邮箱", "填写自己的邮箱", required: true), Text("PASSWORD", "登录密码", "SMTP 登录密码，也可能为授权码", required: true), Text("NAME", "收发件人名称", "留空则不填写")),
		Channel("WEBHOOK", "通用Webhook", Text("URL", "URL", "请输入 Webhook URL", required: true), Combo("METHOD", "HTTP 方法", "POST", "POST", "GET", "PUT"), Combo("CONTENT_TYPE", "Content-Type", "application/json", "application/json", "application/x-www-form-urlencoded", "application/xml", "text/plain"), KeyValue("HEADERS", "请求头 (Headers)", "{}"), Code("BODY", "请求体 (Payload)", "请输入请求体内容", required: true, "{\"content\":\"$content\"}")),
		Channel("DD_BOT", "钉钉机器人", Text("SECRET", "Secret", "请输入钉钉机器人的Secret密钥", required: true), Text("TOKEN", "Token", "请输入钉钉机器人的Token密钥", required: true)),
		Channel("FS", "飞书/Lark 机器人", Combo("CHANNEL", "机器人渠道", "飞书", "飞书", "Lark"), Text("KEY", "Webhook地址后缀", "请输入机器人的Webhook地址后缀", required: true), Text("BOT_SECRET", "机器人签名校验密钥", "启用签名校验时填写"), Text("APPID", "自建应用 App ID", "非必填，填写后可用于发送图片"), Text("APPSECRET", "自建应用 Secret", "非必填，填写后可用于发送图片")),
		Channel("QYWX", "企业微信机器人", Text("ORIGIN", "企业微信代理地址", "可选", required: false, "https://qyapi.weixin.qq.com"), Text("KEY", "企业微信机器人 Key", "只填 Key", required: true)),
		Channel("QYWX_APP", "企业微信应用", Text("CORP_ID", "企业 ID", "企业微信后台中的企业 ID", required: true), Text("CORP_SECRET", "应用 Secret", "企业微信应用 Secret", required: true), Text("AGENT_ID", "应用 AgentId", "企业微信应用 AgentId", required: true), Text("TO_USER", "接收者 ID", "成员ID使用 '|' 分隔，默认 @all", required: false, "@all")),
		Channel("ONEBOT", "OneBot", Text("URL", "请求地址", "请输入请求地址", required: true), Text("USER", "QQ 号", "请输入目标 QQ 号"), Text("GROUP", "群号", "请输入目标群号"), Text("TOKEN", "Token", "请输入 OneBot 的 Token")),
		Channel("BARK", "Bark", Text("PUSH", "推送地址或 Key", "请输入 Bark 推送地址或 Key", required: true), Text("DEVICE_KEY", "设备码", "请填写设备码"), Combo("ARCHIVE", "推送是否归档", "0", "", "1", "0"), Text("GROUP", "推送分组", "请填写推送分组"), Text("SOUND", "推送铃声", "请填写推送铃声"), Text("ICON", "推送图标", "请填写图标的URL"), Combo("LEVEL", "推送中断级别", "active", "", "critical", "active", "timeSensitive", "passive"), Text("URL", "推送跳转URL", "请填写推送跳转URL")),
		Channel("SERVERCHAN", "Server酱", Text("PUSH_KEY", "PUSH_KEY", "请输入 Server酱 的 PUSH_KEY", required: true)),
		Channel("PUSH_PLUS", "PushPlus", Text("TOKEN", "用户令牌", "请输入用户令牌", required: true), Text("USER", "群组编码", "请输入群组编码"), Combo("TEMPLATE", "发送模板", "html", "", "html", "txt", "json", "markdown", "cloudMonitor", "jenkins", "route"), Combo("CHANNEL", "发送渠道", "wechat", "", "wechat", "webhook", "cp", "mail", "sms"), Text("TO", "好友令牌或用户ID", "微信公众号、企业微信用户ID"), Text("WEBHOOK", "Webhook 编码", "用于公众号渠道扩展配置"), Text("CALLBACKURL", "发送结果回调地址", "用于接收最终通知结果")),
		Channel("DISCORD", "Discord 机器人", Text("API_HOST", "API 地址", "Discord API 地址", required: true, "https://discord.com/api/v9"), Text("BOT_TOKEN", "机器人 Token", "请输入 Discord 机器人的 Token", required: true), Text("USER_ID", "用户 ID", "请输入要接收私信的用户 ID", required: true)),
		Channel("TG", "Telegram", Text("BOT_TOKEN", "BOT_TOKEN", "1234567890:AAAAAA-BBBBBBBBBBBBBBBBBBBBBBBBBBB", required: true), Text("USER_ID", "用户 ID", "1234567890", required: true), Text("API_HOST", "API_HOST", "可选")),
		Channel("NTFY", "ntfy", Text("URL", "URL", "ntfy服务器", required: true, "https://ntfy.sh"), Text("TOPIC", "TOPIC", "ntfy 应用 Topic", required: true), Combo("PRIORITY", "消息优先级", "3", "1", "2", "3", "4", "5"), Text("TOKEN", "TOKEN", "ntfy 应用 token"), Text("USERNAME", "用户名", "ntfy 应用用户名"), Text("PASSWORD", "用户密码", "ntfy 应用密码"), Text("ACTIONS", "用户操作", "ntfy 用户操作，可留空")),
		Channel("FAKE", "下面的方法无人维护，遇到问题请自行解决"),
		Channel("GOTIFY", "GOTIFY", Text("URL", "Gotify 地址", "https://push.example.de:8080", required: true), Text("TOKEN", "App Token", "Gotify 的 App Token", required: true), Combo("PRIORITY", "消息优先级", "5", "", "0", "1", "2", "3", "4", "5")),
		Channel("AIBOTK", "智能微秘书", Text("KEY", "APIKEY", "请输入智能微秘书的 APIKEY", required: true), Combo("TYPE", "目标类型", "contact", "room", "contact"), Text("NAME", "目标名称", "请输入群名或者好友昵称", required: true)),
		Channel("WXPUSHER", "WxPusher", Text("APP_TOKEN", "appToken", "请输入 appToken", required: true), Text("TOPIC_IDS", "TOPIC_IDs", "多个使用英文分号;分隔"), Text("UIDS", "UIDs", "UIDs 和 TOPIC_IDs 至少填写一项")),
		Channel("WE_PLUS_BOT", "微加机器人", Text("TOKEN", "用户令牌", "请输入用户令牌", required: true), Text("RECEIVER", "消息接收者", "请输入消息接收者", required: true), Text("VERSION", "接口版本", "可选")),
		Channel("QMSG", "Qmsg酱", Text("KEY", "KEY", "请输入 Qmsg酱 的 KEY", required: true), Combo("TYPE", "通知类型", "send", "send", "group")),
		Channel("PUSHME", "PushMe", Text("KEY", "KEY", "请输入 PushMe 的 KEY", required: true), Text("URL", "URL", "请输入 PushMe 的 URL")),
		Channel("CHRONOCAT", "Chronocat", Text("URL", "请求地址", "http://127.0.0.1:16530", required: true), Text("TOKEN", "服务密钥", "填写 Chronocat 生成的服务密钥", required: true), Text("QQ", "QQ 配置", "user_id=xxx;group_id=yyy;group_id=zzz", required: true)),
		Channel("DEER", "PushDeer", Text("KEY", "推送 Key", "请输入 PushDeer 的 KEY", required: true), Text("URL", "推送 URL", "请输入 PushDeer 的推送URL")),
		Channel("IGOT", "iGot", Text("PUSH_KEY", "推送 Key", "请输入 iGot 的推送 Key", required: true)),
		Channel("SYNOLOGY_CHAT", "Synology Chat", Text("URL", "URL", "请输入 Synology Chat 的 URL", required: true), Text("TOKEN", "Token", "请输入 Synology Chat 的 Token", required: true))
	};

	/// <summary>
	/// 配置 key 到默认值。
	/// </summary>
	public static IReadOnlyDictionary<string, string> FieldDefaults { get; } = Channels.SelectMany((ZzzPushChannelDescriptor channel) => channel.Fields).ToDictionary<ZzzPushFieldDescriptor, string, string>((ZzzPushFieldDescriptor field) => field.Key, (ZzzPushFieldDescriptor field) => field.DefaultValue, StringComparer.Ordinal);

	private static ZzzPushChannelDescriptor Channel(string id, string name, params ZzzPushFieldDescriptor[] fields)
	{
		return new ZzzPushChannelDescriptor(id, name, fields.Select((ZzzPushFieldDescriptor field) => field with
		{
			Key = (id + "_" + field.Key).ToLowerInvariant()
		}).ToArray());
	}

	private static ZzzPushFieldDescriptor Text(string suffix, string title, string placeholder, bool required = false, string defaultValue = "")
	{
		return Field(suffix, title, ZzzPushFieldType.Text, placeholder, required, defaultValue, Array.Empty<string>());
	}

	private static ZzzPushFieldDescriptor Combo(string suffix, string title, string defaultValue, params string[] options)
	{
		return Field(suffix, title, ZzzPushFieldType.Combo, string.Empty, required: true, defaultValue, options);
	}

	private static ZzzPushFieldDescriptor KeyValue(string suffix, string title, string defaultValue)
	{
		return Field(suffix, title, ZzzPushFieldType.KeyValue, string.Empty, required: false, defaultValue, Array.Empty<string>());
	}

	private static ZzzPushFieldDescriptor Code(string suffix, string title, string placeholder, bool required, string defaultValue)
	{
		return Field(suffix, title, ZzzPushFieldType.CodeEditor, placeholder, required, defaultValue, Array.Empty<string>());
	}

	private static ZzzPushFieldDescriptor Field(string suffix, string title, ZzzPushFieldType type, string placeholder, bool required, string defaultValue, IReadOnlyList<string> options)
	{
		return new ZzzPushFieldDescriptor(suffix, title, type, placeholder, required, defaultValue, options);
	}
}
