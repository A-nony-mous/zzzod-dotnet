namespace ZzzOd.Api;

/// <summary>
/// ZZZ API 服务控制器。
/// </summary>
public interface IZzzApiServerController
{
    /// <summary>
    /// 获取 API 服务状态。
    /// </summary>
    /// <returns>API 服务状态。</returns>
    ZzzApiServerStatusDto GetStatus();

    /// <summary>
    /// 启动 API 服务。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>API 服务状态。</returns>
    Task<ZzzApiServerStatusDto> StartServerAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止 API 服务。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>API 服务状态。</returns>
    Task<ZzzApiServerStatusDto> StopServerAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// ZZZ API 服务状态。
/// </summary>
/// <param name="Running">是否运行中。</param>
/// <param name="Enabled">配置是否启用。</param>
/// <param name="Url">监听地址。</param>
/// <param name="LastError">最后错误。</param>
public sealed record ZzzApiServerStatusDto(bool Running, bool Enabled, string Url, string? LastError);
