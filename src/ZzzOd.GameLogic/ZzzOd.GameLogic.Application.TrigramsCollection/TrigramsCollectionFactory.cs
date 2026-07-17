using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.TrigramsCollection;

/// <summary>
/// 卦象集录应用 factory。
/// </summary>
public sealed class TrigramsCollectionFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public TrigramsCollectionFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("trigrams_collection", "卦象集录", "one_dragon", NeedNotify: true))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		return new TrigramsCollectionApp(base.Context, (TrigramsCollectionRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		return TrigramsCollectionRunRecord.Load(base.Context.Environment, instanceIndex, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
