using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application;

/// <summary>
/// ZZZ 应用 factory 基类。
/// </summary>
public abstract class ZApplicationFactory : IApplicationFactory
{
	/// <summary>
	/// ZZZ 上下文。
	/// </summary>
	protected ZContext Context { get; }

	/// <summary>
	/// 元数据。
	/// </summary>
	public ZApplicationFactoryMetadata Metadata { get; }

	/// <inheritdoc />
	public string AppId => Metadata.AppId;

	/// <inheritdoc />
	public string AppName => Metadata.AppName;

	/// <summary>
	/// 默认应用组。
	/// </summary>
	public string GroupId => Metadata.GroupId;

	/// <inheritdoc />
	public bool NeedNotify => Metadata.NeedNotify;

	/// <summary>
	/// 初始化 factory。
	/// </summary>
	protected ZApplicationFactory(ZContext context, ZApplicationFactoryMetadata metadata)
	{
		Context = context;
		Metadata = metadata;
	}

	/// <inheritdoc />
	public abstract IApplication CreateApplication(int instanceIndex, string groupId);

	/// <inheritdoc />
	public virtual IApplicationConfig GetConfig(int instanceIndex, string groupId)
	{
		return ZApplicationConfig.Load<ZApplicationConfig>(Context.Environment, AppId, instanceIndex, groupId);
	}

	/// <inheritdoc />
	public virtual IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		return ZApplicationRunRecord.Load(Context.Environment, AppId, instanceIndex);
	}
}
