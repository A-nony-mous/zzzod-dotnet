namespace ZzzOd.AppHost.Notifications;

/// <summary>
/// 邮箱服务预设。
/// </summary>
public sealed record ZzzEmailServicePreset(string Host, int Port, bool Secure);
