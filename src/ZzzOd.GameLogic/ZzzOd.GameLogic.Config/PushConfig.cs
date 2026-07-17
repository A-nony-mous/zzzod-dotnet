using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Config;

/// <summary>
/// 通知推送配置。
/// </summary>
public sealed class PushConfig
{
	[YamlMember(Alias = "send_image", ApplyNamingConventions = false)]
	public bool SendImage { get; set; } = true;

	[YamlMember(Alias = "proxy", ApplyNamingConventions = false)]
	public string Proxy { get; set; } = "NONE";

	[YamlMember(Alias = "smtp_server", ApplyNamingConventions = false)]
	public string SmtpServer { get; set; } = string.Empty;

	[YamlMember(Alias = "smtp_ssl", ApplyNamingConventions = false)]
	public string SmtpSsl { get; set; } = "true";

	[YamlMember(Alias = "smtp_starttls", ApplyNamingConventions = false)]
	public string SmtpStarttls { get; set; } = "false";

	[YamlMember(Alias = "smtp_email", ApplyNamingConventions = false)]
	public string SmtpEmail { get; set; } = string.Empty;

	[YamlMember(Alias = "smtp_password", ApplyNamingConventions = false)]
	public string SmtpPassword { get; set; } = string.Empty;

	[YamlMember(Alias = "smtp_name", ApplyNamingConventions = false)]
	public string SmtpName { get; set; } = string.Empty;

	[YamlMember(Alias = "webhook_url", ApplyNamingConventions = false)]
	public string WebhookUrl { get; set; } = string.Empty;

	[YamlMember(Alias = "webhook_method", ApplyNamingConventions = false)]
	public string WebhookMethod { get; set; } = "POST";

	[YamlMember(Alias = "webhook_content_type", ApplyNamingConventions = false)]
	public string WebhookContentType { get; set; } = "application/json";

	[YamlMember(Alias = "webhook_headers", ApplyNamingConventions = false)]
	public string WebhookHeaders { get; set; } = "{}";

	[YamlMember(Alias = "webhook_body", ApplyNamingConventions = false)]
	public string WebhookBody { get; set; } = "{\"content\":\"$content\"}";

	[YamlMember(Alias = "serverchan_sendkey", ApplyNamingConventions = false)]
	public string ServerChanSendKey { get; set; } = string.Empty;

	[YamlMember(Alias = "qywx_key", ApplyNamingConventions = false)]
	public string QywxKey { get; set; } = string.Empty;
}
