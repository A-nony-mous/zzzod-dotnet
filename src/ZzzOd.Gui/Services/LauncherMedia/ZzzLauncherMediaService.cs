using System.Globalization;
using System.Text.Json;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;

namespace ZzzOd.Gui.Services.LauncherMedia;

public sealed class ZzzLauncherMediaService
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    private readonly string _runRoot;
    private readonly IZzzAppBackend? _backend;

    public ZzzLauncherMediaService(ZzzRunRoot runRoot, IZzzAppBackend? backend = null)
    {
        _runRoot = runRoot.Path;
        _backend = backend;
    }

    public async Task<IReadOnlyList<ZzzLauncherMediaItem>> GetDashboardMediaAsync(CancellationToken cancellationToken = default)
    {
        ZzzHomeMediaSelection selection = ReadSelection();
        await TryRefreshSelectedMediaAsync(selection, cancellationToken).ConfigureAwait(false);

        if (selection.CustomBanner && File.Exists(CustomBannerPath))
        {
            return [new ZzzLauncherMediaItem(ZzzLauncherMediaKind.CustomBackground, CustomBannerPath, "banner")];
        }

        (string Path, ZzzLauncherMediaKind Kind) configured = selection.BackgroundType switch
        {
            "version_poster" => (Path.Combine(UiDirectory, "version_poster.webp"), ZzzLauncherMediaKind.VersionPoster),
            "static_background" => (Path.Combine(UiDirectory, "static_background.webp"), ZzzLauncherMediaKind.StaticBackground),
            "dynamic_background" => (Path.Combine(UiDirectory, "dynamic_background.webm"), ZzzLauncherMediaKind.DynamicBackground),
            _ => (Path.Combine(UiDirectory, "index.png"), ZzzLauncherMediaKind.DefaultBackground),
        };
        if (File.Exists(configured.Path))
        {
            return [new ZzzLauncherMediaItem(configured.Kind, configured.Path, Path.GetFileName(configured.Path))];
        }

        string defaultPath = Path.Combine(UiDirectory, "index.png");
        return File.Exists(defaultPath)
            ? [new ZzzLauncherMediaItem(ZzzLauncherMediaKind.DefaultBackground, defaultPath, "index.png")]
            : [];
    }

    public ZzzLauncherMediaReadiness GetCachedMediaReadiness() => new(
        HasCustomBackground: File.Exists(CustomBannerPath),
        HasVersionPoster: File.Exists(Path.Combine(UiDirectory, "version_poster.webp")),
        HasStaticBackground: File.Exists(Path.Combine(UiDirectory, "static_background.webp")),
        HasDynamicBackground: File.Exists(Path.Combine(UiDirectory, "dynamic_background.webm")),
        HasDefaultImage: File.Exists(Path.Combine(UiDirectory, "index.png")));

    public async Task<string> SaveCustomBackgroundAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        ValidateCustomBackground(sourcePath);
        Directory.CreateDirectory(CustomBannerDirectory);
        foreach (string oldFile in Directory.EnumerateFiles(CustomBannerDirectory, "banner*"))
        {
            File.Delete(oldFile);
        }

        await using FileStream source = File.OpenRead(sourcePath);
        await using FileStream target = File.Create(CustomBannerPath);
        await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        return CustomBannerPath;
    }

    public static void ValidateCustomBackground(string path)
    {
        if (!File.Exists(path))
        {
			throw new FileNotFoundException("背景文件不存在。", path);
        }

        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (!SupportedImageExtensions.Contains(extension) && !SupportedVideoExtensions.Contains(extension))
        {
			throw new InvalidOperationException("仅支持 bmp、jpg、png、webp、avi、mov、mp4、mkv、webm。");
        }
    }

    public string UiDirectory => Path.Combine(_runRoot, "assets", "ui");

    private string CustomBannerDirectory => Path.Combine(_runRoot, "custom", "assets", "ui");

    private string CustomBannerPath => Path.Combine(CustomBannerDirectory, "banner");

    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".jpg", ".jpeg", ".png", ".webp",
    };

    private static readonly HashSet<string> SupportedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".avi", ".mov", ".mp4", ".mkv", ".webm",
    };

    private ZzzHomeMediaSelection ReadSelection()
    {
        if (_backend?.GetConfigScope("custom") is not { Success: true, Value: not null } result)
        {
            return new ZzzHomeMediaSelection(false, "static_background", string.Empty, string.Empty, string.Empty);
        }

        IReadOnlyDictionary<string, object?> values = result.Value.Values;
        return new ZzzHomeMediaSelection(
            ReadBool(values, "custom_banner"),
            ReadString(values, "background_type", "static_background"),
            ReadString(values, "last_version_poster_fetch_time"),
            ReadString(values, "last_static_background_fetch_time"),
            ReadString(values, "last_dynamic_background_fetch_time"));
    }

    private async Task TryRefreshSelectedMediaAsync(ZzzHomeMediaSelection selection, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(UiDirectory);
        if (selection.BackgroundType == "version_poster")
        {
            string path = Path.Combine(UiDirectory, "version_poster.webp");
            if (ShouldRefresh(path, selection.LastVersionPosterFetchTime)
                && await TryDownloadVersionPosterAsync(path, cancellationToken).ConfigureAwait(false))
            {
                SaveFetchTime("last_version_poster_fetch_time");
            }

            return;
        }

        if (selection.BackgroundType == "static_background")
        {
            string path = Path.Combine(UiDirectory, "static_background.webp");
            if (ShouldRefresh(path, selection.LastStaticBackgroundFetchTime)
                && await TryDownloadBackgroundAsync(path, dynamic: false, cancellationToken).ConfigureAwait(false))
            {
                SaveFetchTime("last_static_background_fetch_time");
            }

            return;
        }

        if (selection.BackgroundType == "dynamic_background")
        {
            string path = Path.Combine(UiDirectory, "dynamic_background.webm");
            if (ShouldRefresh(path, selection.LastDynamicBackgroundFetchTime)
                && await TryDownloadBackgroundAsync(path, dynamic: true, cancellationToken).ConfigureAwait(false))
            {
                SaveFetchTime("last_dynamic_background_fetch_time");
            }
        }
    }

    private static bool ShouldRefresh(string path, string lastFetchTime)
    {
        if (!File.Exists(path))
        {
            return true;
        }

        return !DateTime.TryParseExact(
                   lastFetchTime,
                   "yyyy-MM-dd HH:mm:ss",
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out DateTime lastFetch)
               || DateTime.Now - lastFetch >= TimeSpan.FromDays(1);
    }

    private void SaveFetchTime(string key)
    {
        _backend?.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            "custom",
            new Dictionary<string, object?>
            {
                [key] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            }));
    }

    private static async Task<bool> TryDownloadVersionPosterAsync(string target, CancellationToken cancellationToken)
    {
        try
        {
            using JsonDocument document = await GetJsonAsync(
                "https://hyp-api.mihoyo.com/hyp/hyp-connect/api/getGames?launcher_id=jGHBHlcOq1&language=zh-cn",
                cancellationToken).ConfigureAwait(false);
            foreach (JsonElement game in document.RootElement.GetProperty("data").GetProperty("games").EnumerateArray())
            {
                if (game.TryGetProperty("biz", out JsonElement biz)
                    && biz.GetString() == "nap_cn"
                    && TryGetString(game, ["display", "background", "url"], out string? url))
                {
                    await DownloadFileAsync(url!, target, cancellationToken).ConfigureAwait(false);
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    private static async Task<bool> TryDownloadBackgroundAsync(string target, bool dynamic, CancellationToken cancellationToken)
    {
        try
        {
            using JsonDocument document = await GetJsonAsync(
                "https://hyp-api.mihoyo.com/hyp/hyp-connect/api/getAllGameBasicInfo?launcher_id=jGHBHlcOq1&language=zh-cn",
                cancellationToken).ConfigureAwait(false);
            foreach (JsonElement item in document.RootElement.GetProperty("data").GetProperty("game_info_list").EnumerateArray())
            {
                if (!TryGetString(item, ["game", "biz"], out string? biz) || biz != "nap_cn"
                    || !item.TryGetProperty("backgrounds", out JsonElement backgrounds))
                {
                    continue;
                }

                foreach (JsonElement background in backgrounds.EnumerateArray())
                {
                    string? url = null;
                    if (dynamic
                        && background.TryGetProperty("type", out JsonElement type)
                        && type.GetString() == "BACKGROUND_TYPE_VIDEO")
                    {
                        TryGetString(background, ["video", "url"], out url);
                    }
                    else if (!dynamic)
                    {
                        TryGetString(background, ["background", "url"], out url);
                    }

                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        await DownloadFileAsync(url, target, cancellationToken).ConfigureAwait(false);
                        return true;
                    }
                }
            }
        }
        catch
        {
        }

        return false;
    }

    private static async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        await using Stream stream = await SharedHttpClient.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static async Task DownloadFileAsync(string url, string target, CancellationToken cancellationToken)
    {
        string temp = target + ".tmp";
        try
        {
            await using Stream stream = await SharedHttpClient.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
            await using FileStream file = File.Create(temp);
            await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            file.Close();
            File.Move(temp, target, true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    private static bool TryGetString(JsonElement root, IReadOnlyList<string> path, out string? value)
    {
        JsonElement current = root;
        foreach (string name in path)
        {
            if (!current.TryGetProperty(name, out current))
            {
                value = null;
                return false;
            }
        }

        value = current.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out object? value) && Convert.ToBoolean(value, CultureInfo.InvariantCulture);

    private static string ReadString(IReadOnlyDictionary<string, object?> values, string key, string defaultValue = "") =>
        values.TryGetValue(key, out object? value) ? value?.ToString()?.Trim() ?? defaultValue : defaultValue;
}

public sealed record ZzzLauncherMediaItem(ZzzLauncherMediaKind Kind, string? LocalPath, string Title)
{
    public bool IsImage => LocalPath is not null && !IsVideo;

    public bool IsVideo => LocalPath is not null && ZzzLauncherMediaKindExtensions.IsVideo(LocalPath);
}

public sealed record ZzzLauncherMediaReadiness(
    bool HasCustomBackground,
    bool HasVersionPoster,
    bool HasStaticBackground,
    bool HasDynamicBackground,
    bool HasDefaultImage)
{
    public bool HasAnyMedia => HasCustomBackground || HasVersionPoster || HasStaticBackground || HasDynamicBackground || HasDefaultImage;
}

public enum ZzzLauncherMediaKind
{
    CustomBackground,

    VersionPoster,

    StaticBackground,

    DynamicBackground,

    DefaultBackground,
}

internal sealed record ZzzHomeMediaSelection(
    bool CustomBanner,
    string BackgroundType,
    string LastVersionPosterFetchTime,
    string LastStaticBackgroundFetchTime,
    string LastDynamicBackgroundFetchTime);

internal static class ZzzLauncherMediaKindExtensions
{
    internal static bool IsVideo(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is ".avi" or ".mov" or ".mp4" or ".mkv" or ".webm")
        {
            return true;
        }

        if (!File.Exists(path))
        {
            return false;
        }

        Span<byte> header = stackalloc byte[12];
        using FileStream stream = File.OpenRead(path);
        int read = stream.Read(header);
        return read >= 4 && header[..4].SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 })
               || read >= 8 && header[4..8].SequenceEqual("ftyp"u8)
               || read >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("AVI "u8);
    }
}

