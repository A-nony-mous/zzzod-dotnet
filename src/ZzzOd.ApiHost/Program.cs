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
using ZzzRuntimeLock? runtimeLock = ZzzRuntimeLock.TryAcquire(runRoot);
if (runtimeLock is null)
{
    Console.Error.WriteLine("已有 GUI 或 API-only 宿主持有当前运行根目录。");
    return 2;
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddZzzAppHost(runRoot, ZzzHostMode.ApiOnly);
builder.Services.AddZzzApiServices();

ZzzApiOptions apiOptions = ZzzApiOptions.LoadOrCreate(runRoot);
builder.Services.AddZzzApiCors(apiOptions);
builder.WebHost.UseUrls($"http://{apiOptions.ListenAddress}:{apiOptions.Port}");

WebApplication app = builder.Build();
app.UseZzzApiCors(apiOptions);
app.MapZzzApiEndpoints();

await app.RunAsync().ConfigureAwait(false);
return 0;
