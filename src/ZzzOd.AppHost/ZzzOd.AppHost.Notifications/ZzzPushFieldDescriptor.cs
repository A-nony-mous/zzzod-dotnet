using System.Collections.Generic;

namespace ZzzOd.AppHost.Notifications;

/// <summary>
/// 通知渠道配置字段。
/// </summary>
public sealed record ZzzPushFieldDescriptor(string Key, string Title, ZzzPushFieldType FieldType, string Placeholder, bool Required, string DefaultValue, IReadOnlyList<string> Options);
