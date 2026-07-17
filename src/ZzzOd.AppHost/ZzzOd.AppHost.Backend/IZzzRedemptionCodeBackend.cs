using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 兑换码全局 sample/user 配置服务。
/// </summary>
public interface IZzzRedemptionCodeBackend
{
	/// <summary>读取 BaselineParity 顺序的合并兑换码。</summary>
	ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>> GetRedemptionCodes();

	/// <summary>添加用户兑换码。</summary>
	ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>> AddRedemptionCode(string code, int endDate);

	/// <summary>更新用户兑换码。</summary>
	ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>> UpdateRedemptionCode(string oldCode, string newCode, int endDate);

	/// <summary>删除用户兑换码。</summary>
	ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>> DeleteRedemptionCode(string code);
}
