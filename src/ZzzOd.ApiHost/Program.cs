using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ZzzOd.Api;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Resources;

ZzzRunRootResolution runRootResolution = ZzzRunRootResolver.Resolve(args);
string runRoot = runRootResolution.RunRoot.Path;
ZzzAssetManifestValidationResult manifestValidation = ZzzAssetManifestStartupGate.Validate(runRoot);
if (!manifestValidation.IsValid)
{
    ZzzAssetManifestStartupGate.WriteIssues(manifestValidation.Issues, Console.Error);
    return 3;
}
ZzzValidatedRunRoot validatedRunRoot = ZzzAssetManifestStartupGate.CreateValidatedRunRoot(manifestValidation);
using ZzzRuntimeLock? runtimeLock = ZzzRuntimeLock.TryAcquire(runRoot);
if (runtimeLock is null)
{
    Console.Error.WriteLine("已有 GUI 或 API-only 宿主持有当前运行根目录。");
    return 2;
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddZzzAppHost(validatedRunRoot, ZzzHostMode.ApiOnly);
builder.Services.AddZzzApiServices();

ZzzApiOptions apiOptions = ZzzApiOptions.LoadOrCreate(runRoot);
builder.Services.AddZzzApiCors(apiOptions);
builder.WebHost.UseUrls($"http://{apiOptions.ListenAddress}:{apiOptions.Port}");

if (args.Any(argument => string.Equals(argument, "--health-once", StringComparison.Ordinal)))
{
    using WebApplication healthApp = builder.Build();
    ZzzBackendResult<ZzzHealthDto> health = healthApp.Services.GetRequiredService<IZzzAppBackend>().GetHealth();
    Console.WriteLine(JsonSerializer.Serialize(health.Value));
    return health.Success ? 0 : 1;
}

WebApplication app = builder.Build();
app.UseZzzApiCors(apiOptions);
app.MapZzzApiEndpoints();

await app.RunAsync().ConfigureAwait(false);
return 0;
