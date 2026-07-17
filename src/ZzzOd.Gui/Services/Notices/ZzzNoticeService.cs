using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ZzzOd.AppHost;

namespace ZzzOd.Gui.Services.Notices;

public sealed class ZzzNoticeService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(3);
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(3),
    };

    private readonly string _cacheDirectory;
    private readonly string _noticeCachePath;

    public ZzzNoticeService(ZzzRunRoot runRoot)
    {
        _cacheDirectory = Path.Combine(runRoot.Path, "notice_cache");
        _noticeCachePath = Path.Combine(_cacheDirectory, "notice_cache.json");
    }

    public async Task<ZzzNoticeLoadResult> LoadAsync(string noticeUrl, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(noticeUrl, UriKind.Absolute, out Uri? uri)
            || uri.Scheme is not ("http" or "https"))
        {
            return ZzzNoticeLoadResult.Failed("公告地址无效。", timedOut: false);
        }

        try
        {
            using HttpResponseMessage response = await SharedHttpClient.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            ZzzNoticeContent content = ParseContent(json);
            try
            {
                await SaveNoticeCacheAsync(json, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
            }

            return ZzzNoticeLoadResult.Loaded(content, fromCache: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException or IOException)
        {
            bool timedOut = exception is TaskCanceledException;
            ZzzNoticeContent? cached = await TryLoadNoticeCacheAsync(cancellationToken).ConfigureAwait(false);
            return cached is null
                ? ZzzNoticeLoadResult.Failed(exception.Message, timedOut)
                : ZzzNoticeLoadResult.Loaded(cached, fromCache: true, timedOut);
        }
    }

    public async Task<string?> GetBannerImagePathAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri? uri)
            || uri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        Directory.CreateDirectory(_cacheDirectory);
        string cachePath = Path.Combine(_cacheDirectory, BuildBannerCacheName(uri));
        if (IsCacheValid(cachePath))
        {
            return cachePath;
        }

        string temporaryPath = cachePath + ".tmp";
        try
        {
            using HttpResponseMessage response = await SharedHttpClient.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (FileStream target = File.Create(temporaryPath))
            {
                await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, cachePath, true);
            return cachePath;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
        {
            return null;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private async Task<ZzzNoticeContent?> TryLoadNoticeCacheAsync(CancellationToken cancellationToken)
    {
        if (!IsCacheValid(_noticeCachePath))
        {
            return null;
        }

        try
        {
            string json = await File.ReadAllTextAsync(_noticeCachePath, cancellationToken).ConfigureAwait(false);
            return ParseContent(json);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException)
        {
            return null;
        }
    }

    private async Task SaveNoticeCacheAsync(string json, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_cacheDirectory);
        string temporaryPath = _noticeCachePath + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, json, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, _noticeCachePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static ZzzNoticeContent ParseContent(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (!TryGetProperty(document.RootElement, ["data", "content"], out JsonElement content)
            || !content.TryGetProperty("banners", out JsonElement bannersElement)
            || bannersElement.ValueKind != JsonValueKind.Array
            || !content.TryGetProperty("posts", out JsonElement postsElement)
            || postsElement.ValueKind != JsonValueKind.Array)
        {
			throw new InvalidDataException("公告数据格式无效。");
        }

        List<ZzzNoticeBanner> banners = [];
        foreach (JsonElement banner in bannersElement.EnumerateArray())
        {
            if (TryGetProperty(banner, ["image", "url"], out JsonElement imageUrl)
                && TryGetProperty(banner, ["image", "link"], out JsonElement link)
                && imageUrl.GetString() is { Length: > 0 } image
                && link.GetString() is { Length: > 0 } target)
            {
                banners.Add(new ZzzNoticeBanner(image, target));
            }
        }

        List<ZzzNoticePost> announcements = [];
        List<ZzzNoticePost> softwareResearch = [];
        List<ZzzNoticePost> gameGuides = [];
        foreach (JsonElement post in postsElement.EnumerateArray())
        {
            string type = ReadRequiredString(post, "type");
            ZzzNoticePost item = new(
                ReadRequiredString(post, "title"),
                ReadRequiredString(post, "link"),
                ReadRequiredString(post, "date"));
            switch (type)
            {
                case "POST_TYPE_ANNOUNCE":
                    announcements.Add(item);
                    break;
                case "POST_TYPE_RESEARCHS":
                    softwareResearch.Add(item);
                    break;
                case "POST_TYPE_GUIDES":
                    gameGuides.Add(item);
                    break;
            }
        }

        return new ZzzNoticeContent(
            banners,
            announcements.Take(3).ToArray(),
            softwareResearch.Take(3).ToArray(),
            gameGuides.Take(3).ToArray());
    }

    private static bool TryGetProperty(JsonElement root, IReadOnlyList<string> path, out JsonElement value)
    {
        value = root;
        foreach (string name in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(name, out value))
            {
                return false;
            }
        }

        return true;
    }

    private static string ReadRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value)
            || value.GetString() is not { Length: > 0 } text)
        {
            throw new InvalidDataException($"公告缺少 {propertyName}。");
        }

        return text;
    }

    private static bool IsCacheValid(string path) =>
        File.Exists(path) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < CacheDuration;

    private static string BuildBannerCacheName(Uri uri)
    {
        string hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri))).ToLowerInvariant();
        string extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif"
            ? hash + extension
            : hash + ".png";
    }
}

public sealed record ZzzNoticeBanner(string ImageUrl, string Link);

public sealed record ZzzNoticePost(string Title, string Link, string Date);

public sealed record ZzzNoticeContent(
    IReadOnlyList<ZzzNoticeBanner> Banners,
    IReadOnlyList<ZzzNoticePost> Announcements,
    IReadOnlyList<ZzzNoticePost> SoftwareResearch,
    IReadOnlyList<ZzzNoticePost> GameGuides);

public sealed record ZzzNoticeLoadResult(bool Success, ZzzNoticeContent? Content, string? Error, bool FromCache, bool TimedOut)
{
    public static ZzzNoticeLoadResult Loaded(ZzzNoticeContent content, bool fromCache, bool timedOut = false) =>
        new(true, content, null, fromCache, timedOut);

    public static ZzzNoticeLoadResult Failed(string error, bool timedOut = false) =>
        new(false, null, error, false, timedOut);
}

