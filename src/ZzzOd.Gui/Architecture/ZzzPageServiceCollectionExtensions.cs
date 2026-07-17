using Microsoft.Extensions.DependencyInjection;

namespace ZzzOd.Gui.Architecture;

public static class ZzzPageServiceCollectionExtensions
{
    public static IServiceCollection AddZzzAxamlPage<TView, TViewModel>(this IServiceCollection services)
        where TView : ZzzPageView
        where TViewModel : ZzzPageViewModel
    {
        services.AddTransient<TViewModel>();
        services.AddTransient<TView>();
        return services;
    }
}
