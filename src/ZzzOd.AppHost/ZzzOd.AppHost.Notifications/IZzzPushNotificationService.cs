using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;

namespace ZzzOd.AppHost.Notifications;

/// <summary>
/// 通知设置页使用的生产通知服务。
/// </summary>
public interface IZzzPushNotificationService
{
	/// <summary>
	/// BaselineParity PushService 注册顺序下的渠道定义。
	/// </summary>
	IReadOnlyList<ZzzPushChannelDescriptor> Channels { get; }

	/// <summary>
	/// BaselineParity PushEmailServices 中的邮箱服务预设。
	/// </summary>
	IReadOnlyDictionary<string, ZzzEmailServicePreset> EmailServices { get; }

	/// <summary>
	/// 向当前渠道或全部已配置渠道发送测试消息。
	/// </summary>
	Task<ZzzPushTestResult> SendTestAsync(string? channelId, string title, string content, CancellationToken cancellationToken = default(CancellationToken), Mat? image = null);
}
