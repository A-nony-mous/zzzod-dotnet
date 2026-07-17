using Microsoft.Extensions.DependencyInjection;

namespace ZzzOd.Api;

/// <summary>
/// ZZZ API 服务注册扩展。
/// </summary>
public static class ZzzApiServiceCollectionExtensions
{
    /// <summary>
    /// 注册 GUI 进程内 API 服务。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddZzzGuiApiServer(this IServiceCollection services)
    {
        services.AddSingleton<ZzzApiHostedService>();
        services.AddSingleton<IZzzApiServerController>(sp => sp.GetRequiredService<ZzzApiHostedService>());
        services.AddHostedService(sp => sp.GetRequiredService<ZzzApiHostedService>());
        return services;
    }
}
