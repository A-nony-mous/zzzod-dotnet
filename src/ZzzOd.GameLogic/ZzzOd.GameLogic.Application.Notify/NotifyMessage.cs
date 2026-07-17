namespace ZzzOd.GameLogic.Application.Notify;

/// <summary>
/// 通知消息。
/// </summary>
public sealed record NotifyMessage(string Content, bool HasFailure);
