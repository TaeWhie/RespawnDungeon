using System;
using System.IO;
using System.Text.Json;

namespace GuildDialogue.Services;

/// <summary>
/// 허브 프로세스와 콘솔/CLI 원정이 서로 다른 프로세스일 때도
/// 「다음 홈에서 동료 2인 대화」 플래그를 공유하기 위한 Config 폴더 내 작은 JSON 파일.
/// </summary>
public static class CompanionDialoguePendingFile
{
    private const string FileName = "CompanionDialogueHubPending.json";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string GetPath(string configDirectory) =>
        Path.Combine(configDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), FileName);

    public static void SetPostDungeonPending(string configDirectory)
    {
        if (string.IsNullOrWhiteSpace(configDirectory)) return;
        try
        {
            Directory.CreateDirectory(configDirectory);
            var path = GetPath(configDirectory);
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(new PendingDto { PendingPostDungeon = true }, JsonOpts));
        }
        catch
        {
            /* 허브 동료 대화 힌트 실패는 원정 자체를 막지 않음 */
        }
    }

    public static bool ReadPostDungeonPending(string configDirectory)
    {
        if (string.IsNullOrWhiteSpace(configDirectory)) return false;
        try
        {
            var path = GetPath(configDirectory);
            if (!File.Exists(path)) return false;
            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<PendingDto>(json, JsonOpts);
            return dto?.PendingPostDungeon == true;
        }
        catch
        {
            return false;
        }
    }

    public static void ClearPostDungeonPending(string configDirectory)
    {
        if (string.IsNullOrWhiteSpace(configDirectory)) return;
        try
        {
            var path = GetPath(configDirectory);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            /* best effort */
        }
    }

    private sealed class PendingDto
    {
        public bool PendingPostDungeon { get; set; }
    }
}
