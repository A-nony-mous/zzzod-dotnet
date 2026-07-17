using System.Collections.Generic;

namespace ZzzOd.AppHost.Notifications;

/// <summary>
/// 通知渠道。
/// </summary>
public sealed record ZzzPushChannelDescriptor(string ChannelId, string ChannelName, IReadOnlyList<ZzzPushFieldDescriptor> Fields);
