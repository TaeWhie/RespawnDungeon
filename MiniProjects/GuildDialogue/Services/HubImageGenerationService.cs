using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GuildDialogue.Services;

public sealed class HubImageGenerationService
{
    private readonly string _cacheDir;
    private readonly string _manifestPath;
    private readonly string _providerEndpoint;
    private readonly string _providerToken;
    private readonly int _maxRetries;
    private readonly int _maxConcurrency;
    private readonly TimeSpan _cacheTtl;
    private readonly ConcurrentDictionary<string, HubImageJob> _jobs = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _queueSignal = new(0);
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _workerTasks = new();
    private readonly object _manifestLock = new();
    private Dictionary<string, HubImageCacheEntry> _cache = new(StringComparer.Ordinal);

    public HubImageGenerationService(string configDirectory)
    {
        _cacheDir = Path.Combine(configDirectory, "HubImageCache");
        Directory.CreateDirectory(_cacheDir);
        _manifestPath = Path.Combine(_cacheDir, "manifest.json");
        _providerEndpoint = Environment.GetEnvironmentVariable("HUB_IMAGE_GEN_ENDPOINT")?.Trim() ?? "";
        _providerToken = Environment.GetEnvironmentVariable("HUB_IMAGE_GEN_TOKEN")?.Trim() ?? "";
        _maxRetries = Math.Clamp(ParseIntEnv("HUB_IMAGE_GEN_RETRY", 2), 0, 5);
        _maxConcurrency = Math.Clamp(ParseIntEnv("HUB_IMAGE_GEN_MAX_CONCURRENCY", 1), 1, 4);
        _cacheTtl = TimeSpan.FromHours(Math.Clamp(ParseIntEnv("HUB_IMAGE_CACHE_TTL_HOURS", 24 * 14), 1, 24 * 365));
        LoadManifest();
        CleanupExpiredCache();
        for (var i = 0; i < _maxConcurrency; i++)
            _workerTasks.Add(Task.Run(ProcessQueueLoopAsync));
    }

    public HubImageResolveResult ResolveOrQueue(HubImageResolveRequest req)
    {
        var normalized = Normalize(req);
        var cacheKey = ComputeCacheKey(normalized);
        if (_cache.TryGetValue(cacheKey, out var cached) && File.Exists(cached.FilePath))
        {
            if (DateTime.UtcNow - cached.UpdatedAtUtc > _cacheTtl)
            {
                TryDeleteCachedFile(cacheKey, cached);
            }
            else
            {
            return HubImageResolveResult.Ready(cacheKey, BuildPublicImageUrl(cacheKey), cached.ThemeId);
            }
        }

        var existing = _jobs.Values.FirstOrDefault(j => j.CacheKey == cacheKey && (j.Status is HubImageJobStatus.Queued or HubImageJobStatus.Processing));
        if (existing != null)
            return HubImageResolveResult.FromJob(existing);

        var job = new HubImageJob
        {
            JobId = Guid.NewGuid().ToString("N"),
            CacheKey = cacheKey,
            Prompt = normalized.Prompt,
            ThemeId = normalized.ThemeId,
            Width = normalized.Width,
            Height = normalized.Height,
            Scope = normalized.Scope,
            EntityKey = normalized.EntityKey,
            Status = HubImageJobStatus.Queued,
            CreatedAtUtc = DateTime.UtcNow
        };
        _jobs[job.JobId] = job;
        _queueSignal.Release();
        return HubImageResolveResult.FromJob(job);
    }

    public async Task<HubImageResolveResult> ResolveOrGenerateNowAsync(HubImageResolveRequest req, CancellationToken ct)
    {
        var normalized = Normalize(req);
        var cacheKey = ComputeCacheKey(normalized);
        if (_cache.TryGetValue(cacheKey, out var cached) && File.Exists(cached.FilePath))
            return HubImageResolveResult.Ready(cacheKey, BuildPublicImageUrl(cacheKey), cached.ThemeId);

        var bytes = await GenerateWithRetryAsync(new HubImageJob
        {
            JobId = Guid.NewGuid().ToString("N"),
            CacheKey = cacheKey,
            Prompt = normalized.Prompt,
            ThemeId = normalized.ThemeId,
            Width = normalized.Width,
            Height = normalized.Height,
            Scope = normalized.Scope,
            EntityKey = normalized.EntityKey,
            Status = HubImageJobStatus.Processing,
            CreatedAtUtc = DateTime.UtcNow
        }, ct).ConfigureAwait(false);

        var path = Path.Combine(_cacheDir, $"{cacheKey}.png");
        await File.WriteAllBytesAsync(path, bytes, ct).ConfigureAwait(false);
        lock (_manifestLock)
        {
            _cache[cacheKey] = new HubImageCacheEntry(path, normalized.ThemeId, DateTime.UtcNow);
            SaveManifest();
        }
        return HubImageResolveResult.Ready(cacheKey, BuildPublicImageUrl(cacheKey), normalized.ThemeId);
    }

    public HubImageJobSnapshot? GetJob(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
            return null;
        return new HubImageJobSnapshot(
            job.JobId,
            job.Status.ToString().ToLowerInvariant(),
            job.Error,
            job.CacheKey,
            job.Status == HubImageJobStatus.Done ? BuildPublicImageUrl(job.CacheKey) : null,
            job.ThemeId);
    }

    public bool TryGetFilePath(string cacheKey, out string filePath)
    {
        filePath = "";
        if (!_cache.TryGetValue(cacheKey, out var entry))
            return false;
        if (!File.Exists(entry.FilePath))
            return false;
        filePath = entry.FilePath;
        return true;
    }

    private async Task ProcessQueueLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            await _queueSignal.WaitAsync(_cts.Token).ConfigureAwait(false);
            var next = _jobs.Values
                .Where(x => x.Status == HubImageJobStatus.Queued)
                .OrderBy(x => x.CreatedAtUtc)
                .FirstOrDefault();
            if (next == null)
                continue;
            next.Status = HubImageJobStatus.Processing;
            try
            {
                if (string.IsNullOrWhiteSpace(_providerEndpoint))
                    throw new InvalidOperationException("HUB_IMAGE_GEN_ENDPOINT가 설정되지 않았습니다.");

                var bytes = await GenerateWithRetryAsync(next, _cts.Token).ConfigureAwait(false);
                var path = Path.Combine(_cacheDir, $"{next.CacheKey}.png");
                await File.WriteAllBytesAsync(path, bytes, _cts.Token).ConfigureAwait(false);
                lock (_manifestLock)
                {
                    _cache[next.CacheKey] = new HubImageCacheEntry(path, next.ThemeId, DateTime.UtcNow);
                    SaveManifest();
                }
                next.Status = HubImageJobStatus.Done;
            }
            catch (Exception ex)
            {
                next.Status = HubImageJobStatus.Failed;
                next.Error = ex.Message;
            }
        }
    }

    private async Task<byte[]> GenerateViaProviderAsync(HubImageJob job, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        using var req = new HttpRequestMessage(HttpMethod.Post, _providerEndpoint);
        if (!string.IsNullOrWhiteSpace(_providerToken))
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_providerToken}");
        req.Content = JsonContent.Create(new
        {
            inputs = job.Prompt,
            parameters = new
            {
                width = job.Width,
                height = job.Height,
                num_inference_steps = 4,
                guidance_scale = 0.0
            },
            options = new
            {
                wait_for_model = true,
                use_cache = true
            }
        });
        using var hfRes = await http.SendAsync(req, ct).ConfigureAwait(false);
        if (!hfRes.IsSuccessStatusCode)
        {
            var err = await hfRes.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException($"HF inference error {(int)hfRes.StatusCode}: {err}");
        }
        var mediaType = hfRes.Content.Headers.ContentType?.MediaType ?? "";
        if (mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return await hfRes.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        var maybeJson = await hfRes.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        throw new InvalidOperationException($"HF inference did not return image bytes: {maybeJson}");
    }

    private async Task<byte[]> GenerateWithRetryAsync(HubImageJob job, CancellationToken ct)
    {
        Exception? last = null;
        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                return await GenerateViaProviderAsync(job, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                last = ex;
                if (attempt >= _maxRetries)
                    break;
                var delayMs = 600 * (attempt + 1);
                await Task.Delay(delayMs, ct).ConfigureAwait(false);
            }
        }
        throw new InvalidOperationException($"이미지 생성 재시도 실패: {last?.Message}", last);
    }

    private string ComputeCacheKey(HubImageResolveRequest req)
    {
        var raw = $"{req.Scope}|{req.EntityKey}|{req.ThemeId}|{req.Width}x{req.Height}|{req.Prompt}";
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static HubImageResolveRequest Normalize(HubImageResolveRequest req)
    {
        return new HubImageResolveRequest(
            string.IsNullOrWhiteSpace(req.Scope) ? "generic" : req.Scope.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(req.EntityKey) ? "default" : req.EntityKey.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(req.Prompt) ? "fantasy illustration" : req.Prompt.Trim(),
            string.IsNullOrWhiteSpace(req.ThemeId) ? "guildhub-2d-v1" : req.ThemeId.Trim().ToLowerInvariant(),
            req.Width <= 0 ? 768 : req.Width,
            req.Height <= 0 ? 768 : req.Height);
    }

    private string BuildPublicImageUrl(string cacheKey) => $"/api/images/file/{cacheKey}";

    private void LoadManifest()
    {
        try
        {
            if (!File.Exists(_manifestPath))
                return;
            var text = File.ReadAllText(_manifestPath);
            var data = JsonSerializer.Deserialize<Dictionary<string, HubImageCacheEntry>>(text);
            if (data != null)
                _cache = data;
        }
        catch
        {
            _cache = new Dictionary<string, HubImageCacheEntry>(StringComparer.Ordinal);
        }
    }

    private void SaveManifest()
    {
        var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_manifestPath, json);
    }

    private void CleanupExpiredCache()
    {
        lock (_manifestLock)
        {
            var now = DateTime.UtcNow;
            var expired = _cache
                .Where(kv => now - kv.Value.UpdatedAtUtc > _cacheTtl || !File.Exists(kv.Value.FilePath))
                .Select(kv => kv.Key)
                .ToList();
            foreach (var key in expired)
            {
                if (_cache.TryGetValue(key, out var entry))
                    TryDeleteCachedFile(key, entry);
            }
            if (expired.Count > 0)
                SaveManifest();
        }
    }

    private void TryDeleteCachedFile(string cacheKey, HubImageCacheEntry entry)
    {
        try
        {
            if (File.Exists(entry.FilePath))
                File.Delete(entry.FilePath);
        }
        catch
        {
            // ignore best-effort cleanup
        }
        _cache.Remove(cacheKey);
    }

    private static int ParseIntEnv(string name, int defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return int.TryParse(raw, out var v) ? v : defaultValue;
    }

    public sealed record HubImageResolveRequest(
        string Scope,
        string EntityKey,
        string Prompt,
        string ThemeId,
        int Width,
        int Height);

    public sealed record HubImageResolveResult(
        string Status,
        string CacheKey,
        string? ImageUrl,
        string? JobId,
        string? Error,
        string ThemeId)
    {
        public static HubImageResolveResult Ready(string cacheKey, string imageUrl, string themeId) =>
            new("ready", cacheKey, imageUrl, null, null, themeId);

        internal static HubImageResolveResult FromJob(HubImageJob job) =>
            new(
                job.Status == HubImageJobStatus.Queued ? "queued" :
                job.Status == HubImageJobStatus.Processing ? "processing" :
                job.Status == HubImageJobStatus.Failed ? "error" : "ready",
                job.CacheKey,
                null,
                job.JobId,
                job.Error,
                job.ThemeId);
    }

    public sealed record HubImageJobSnapshot(
        string JobId,
        string Status,
        string? Error,
        string CacheKey,
        string? ImageUrl,
        string ThemeId);

    internal sealed class HubImageJob
    {
        public string JobId { get; set; } = "";
        public string CacheKey { get; set; } = "";
        public string Scope { get; set; } = "";
        public string EntityKey { get; set; } = "";
        public string Prompt { get; set; } = "";
        public string ThemeId { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public HubImageJobStatus Status { get; set; }
        public string? Error { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    internal enum HubImageJobStatus
    {
        Queued,
        Processing,
        Done,
        Failed
    }

    public sealed record HubImageCacheEntry(string FilePath, string ThemeId, DateTime UpdatedAtUtc);

}
