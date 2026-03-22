using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>Config/Presets/manifest.json 한 줄.</summary>
public sealed class WorldPresetInfo
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
}

public class DialogueConfigLoader
{
    private readonly string _configPath;

    /// <summary>Config 폴더 절대경로 (ActionLog·Party 저장 시 사용).</summary>
    public string ConfigDirectory => _configPath;

    public DialogueConfigLoader(string? configPath = null)
    {
        _configPath = configPath ?? ResolveDefaultConfigDirectory();
    }

    /// <summary>
    /// <see cref="AppContext.BaseDirectory"/>가 bin/Debug/netX 아래일 때, 상위로 올라가며
    /// .csproj가 있는 프로젝트 루트의 <c>Config</c>를 사용합니다. (저장이 소스 Config와 같아지도록)
    /// 배포본처럼 csproj가 없으면 <c>BaseDirectory/Config</c>로 폴백합니다.
    /// </summary>
    public static string ResolveDefaultConfigDirectory()
    {
        try
        {
            var markerNames = new[] { "DialogueSettings.json", "CharactersDatabase.json", "PartyDatabase.json" };
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory);
                 dir != null;
                 dir = dir.Parent)
            {
                if (dir.GetFiles("*.csproj").Length == 0)
                    continue;

                var candidate = Path.Combine(dir.FullName, "Config");
                if (!Directory.Exists(candidate))
                    continue;

                foreach (var m in markerNames)
                {
                    if (File.Exists(Path.Combine(candidate, m)))
                        return Path.GetFullPath(candidate);
                }
            }
        }
        catch
        {
            // fall through
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Config"));
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public DialogueSettings LoadSettings()
    {
        var path = Path.Combine(_configPath, "DialogueSettings.json");
        return JsonSerializer.Deserialize<DialogueSettings>(File.ReadAllText(path), JsonOptions())!;
    }

    /// <summary>CharactersDatabase.json 우선, 없으면 Characters.json.</summary>
    public List<Character> LoadCharacters()
    {
        var dbPath = Path.Combine(_configPath, "CharactersDatabase.json");
        if (File.Exists(dbPath))
            return JsonSerializer.Deserialize<List<Character>>(File.ReadAllText(dbPath), JsonOptions()) ?? new();

        var path = Path.Combine(_configPath, "Characters.json");
        if (!File.Exists(path)) return new List<Character>();
        return JsonSerializer.Deserialize<List<Character>>(File.ReadAllText(path), JsonOptions()) ?? new();
    }

    public void SaveCharacters(List<Character> characters)
    {
        var path = Path.Combine(_configPath, "Characters.json");
        var options = WriteJsonOptions();
        var json = JsonSerializer.Serialize(characters, options);
        File.WriteAllText(path, json);
    }

    private static JsonSerializerOptions WriteJsonOptions() => new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>LoadCharacters()와 동일 우선순위: CharactersDatabase.json 있으면 그쪽에 저장.</summary>
    public void SaveCharactersDatabase(List<Character> characters)
    {
        var dbPath = Path.Combine(_configPath, "CharactersDatabase.json");
        var fallback = Path.Combine(_configPath, "Characters.json");
        var path = File.Exists(dbPath) ? dbPath : fallback;
        var json = JsonSerializer.Serialize(characters, WriteJsonOptions());
        File.WriteAllText(path, json);
    }

    public void SaveActionLog(TestDataRoot root)
    {
        var path = Path.Combine(_configPath, "ActionLog.json");
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(root, WriteJsonOptions()));
    }

    /// <param name="replaceExisting">false면 기존 ActionLog.json 뒤에 원정 항목을 이어 붙이고 Order를 재번호화.</param>
    public void SaveActionLogAfterSimulation(TestDataRoot newRun, bool replaceExisting)
    {
        if (replaceExisting || newRun?.ActionLog == null)
        {
            SaveActionLog(newRun ?? new TestDataRoot());
            return;
        }

        var existing = LoadTimelineData();
        var merged = ActionLogPersistence.AppendToExisting(existing, newRun);
        SaveActionLog(merged);
    }

    /// <summary>ActionLog.json 우선(TestDataRoot 형식), 없으면 TestData.json.</summary>
    public TestDataRoot? LoadTimelineData()
    {
        var actionPath = Path.Combine(_configPath, "ActionLog.json");
        if (File.Exists(actionPath))
            return JsonSerializer.Deserialize<TestDataRoot>(File.ReadAllText(actionPath), JsonOptions());

        return LoadTestData();
    }

    public TestDataRoot? LoadTestData()
    {
        var path = Path.Combine(_configPath, "TestData.json");
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<TestDataRoot>(File.ReadAllText(path), JsonOptions());
    }

    public LogGlossaryRoot? LoadLogGlossary()
    {
        var path = Path.Combine(_configPath, "LogGlossary.json");
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<LogGlossaryRoot>(File.ReadAllText(path), JsonOptions());
    }

    public WorldLore? LoadWorldLore()
    {
        var path = Path.Combine(_configPath, "WorldLore.json");
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<WorldLore>(File.ReadAllText(path), JsonOptions());
    }

    public List<ItemData> LoadItemDatabase()
    {
        var path = Path.Combine(_configPath, "ItemDatabase.json");
        if (!File.Exists(path)) return new List<ItemData>();
        return JsonSerializer.Deserialize<List<ItemData>>(File.ReadAllText(path), JsonOptions()) ?? new();
    }

    public List<BaseFacilityData> LoadBaseDatabase()
    {
        var path = Path.Combine(_configPath, "BaseDatabase.json");
        if (!File.Exists(path)) return new List<BaseFacilityData>();
        return JsonSerializer.Deserialize<List<BaseFacilityData>>(File.ReadAllText(path), JsonOptions()) ?? new();
    }

    public List<MonsterData> LoadMonsterDatabase()
    {
        var path = Path.Combine(_configPath, "MonsterDatabase.json");
        if (!File.Exists(path)) return new List<MonsterData>();
        return JsonSerializer.Deserialize<List<MonsterData>>(File.ReadAllText(path), JsonOptions()) ?? new();
    }

    public List<SkillData> LoadSkillDatabase()
    {
        var path = Path.Combine(_configPath, "SkillDatabase.json");
        if (!File.Exists(path)) return new List<SkillData>();
        return JsonSerializer.Deserialize<List<SkillData>>(File.ReadAllText(path), JsonOptions()) ?? new();
    }

    /// <summary>Config/JobDatabase.json — 직업(Role)별 허용 스킬. 없으면 빈 목록.</summary>
    public List<JobRoleData> LoadJobDatabase()
    {
        var path = Path.Combine(_configPath, "JobDatabase.json");
        if (!File.Exists(path)) return new List<JobRoleData>();
        return JsonSerializer.Deserialize<List<JobRoleData>>(File.ReadAllText(path), JsonOptions()) ?? new();
    }

    /// <summary><see cref="LoadCharacters"/> 후 <see cref="JobSkillRules"/>로 스킬을 직업에 맞게 제한합니다.</summary>
    public List<Character> LoadCharactersWithJobSkillFilter()
    {
        var chars = LoadCharacters();
        var jobs = LoadJobDatabase();
        JobSkillRules.EnforceOnRoster(chars, jobs);
        return chars;
    }

    public List<TrapTypeData> LoadTrapTypeDatabase()
    {
        var path = Path.Combine(_configPath, "TrapTypeDatabase.json");
        if (!File.Exists(path)) return new List<TrapTypeData>();
        return JsonSerializer.Deserialize<List<TrapTypeData>>(File.ReadAllText(path), JsonOptions()) ?? new();
    }

    public EventTypeDatabaseRoot? LoadEventTypeDatabase()
    {
        var path = Path.Combine(_configPath, "EventTypeDatabase.json");
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<EventTypeDatabaseRoot>(File.ReadAllText(path), JsonOptions());
    }

    /// <summary>
    /// <see cref="SemanticGuardrailAnchorsRoot"/> — 길드 집무실 시멘틱 가드레일 앵커.
    /// 파일 없음·역직렬화 실패 시 null (<see cref="GuildOfficeSemanticGuardrail"/>이 내장 기본값으로 보강).
    /// </summary>
    public SemanticGuardrailAnchorsRoot? LoadSemanticGuardrailAnchors()
    {
        var path = Path.Combine(_configPath, "SemanticGuardrailAnchors.json");
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<SemanticGuardrailAnchorsRoot>(File.ReadAllText(path), JsonOptions());
        }
        catch
        {
            return null;
        }
    }

    /// <summary>던전 런 시뮬레이터용 입력 묶음.</summary>
    public DungeonSimulationInputs LoadSimulationInputs(string? simulationPartyId = null) => new()
    {
        Characters = LoadCharactersWithJobSkillFilter(),
        Monsters = LoadMonsterDatabase(),
        Traps = LoadTrapTypeDatabase(),
        Items = LoadItemDatabase(),
        Bases = LoadBaseDatabase(),
        Lore = LoadWorldLore(),
        SimulationPartyId = simulationPartyId
    };

    public List<PartyData> LoadPartyDatabase()
    {
        var path = Path.Combine(_configPath, "PartyDatabase.json");
        if (!File.Exists(path)) return new List<PartyData>();
        return JsonSerializer.Deserialize<List<PartyData>>(File.ReadAllText(path), JsonOptions()) ?? new();
    }

    public void SavePartyDatabase(List<PartyData> parties)
    {
        var path = Path.Combine(_configPath, "PartyDatabase.json");
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(parties, WriteJsonOptions()));
    }

    /// <summary>허브 세계관 편집기에서 다루는 Config JSON (경로 조작 방지 — 파일명만 허용).</summary>
    public static readonly HashSet<string> WorldConfigEditableFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "BaseDatabase.json",
        "DangerLevelDatabase.json",
        "DialogueSettings.json",
        "EffectSnippetDatabase.json",
        "EventTypeDatabase.json",
        "GuildOfficeExploration.json",
        "ItemDatabase.json",
        "ItemTypeDatabase.json",
        "JobDatabase.json",
        "MonsterDatabase.json",
        "MonsterTraitDatabase.json",
        "MonsterTypeDatabase.json",
        "RarityDatabase.json",
        "SemanticGuardrailAnchors.json",
        "SkillDatabase.json",
        "SkillTypeDatabase.json",
        "TagDatabase.json",
        "TrapCategoryDatabase.json",
        "TrapTypeDatabase.json",
        "WorldLore.json",
    };

    /// <summary>세계관 편집용 텍스트 로드. 없으면 타입에 맞는 최소 JSON.</summary>
    public string ReadWorldConfigText(string fileName)
    {
        var name = RequireWorldConfigFileName(fileName);
        var path = Path.Combine(_configPath, name);
        if (!File.Exists(path))
            return DefaultWorldConfigJson(name);
        return File.ReadAllText(path);
    }

    /// <summary>JSON 문법 검사 후 들여쓰기 저장.</summary>
    public void WriteWorldConfigText(string fileName, string json)
    {
        var name = RequireWorldConfigFileName(fileName);
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON 내용이 비어 있습니다.", nameof(json));

        using var doc = JsonDocument.Parse(
            json,
            new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

        var path = Path.Combine(_configPath, name);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var pretty = JsonSerializer.Serialize(doc.RootElement, WriteJsonOptions());
        File.WriteAllText(path, pretty);
    }

    private static readonly JsonSerializerOptions PresetManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>Config/Presets/manifest.json</summary>
    public IReadOnlyList<WorldPresetInfo> LoadWorldPresetsManifest()
    {
        var path = Path.Combine(_configPath, "Presets", "manifest.json");
        if (!File.Exists(path))
            return Array.Empty<WorldPresetInfo>();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<WorldPresetInfo>>(json, PresetManifestJsonOptions) ?? new List<WorldPresetInfo>();
    }

    /// <summary>프리셋 폴더의 JSON을 읽고, 없으면 기본 Config로 폴백.</summary>
    public string ReadPresetWorldConfigText(string presetId, string fileName)
    {
        var pid = RequirePresetId(presetId);
        var name = RequireWorldConfigFileName(fileName);
        var path = Path.Combine(_configPath, "Presets", pid, name);
        if (File.Exists(path))
            return File.ReadAllText(path);
        return ReadWorldConfigText(name);
    }

    /// <summary>편집 가능한 세계관 파일 전부를 프리셋에서 로드.</summary>
    public Dictionary<string, string> LoadWorldPresetFiles(string presetId)
    {
        _ = RequirePresetId(presetId);
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in WorldConfigEditableFileNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            files[name] = ReadPresetWorldConfigText(presetId, name);
        return files;
    }

    private static string RequirePresetId(string presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId))
            throw new ArgumentException("프리셋 ID가 비어 있습니다.", nameof(presetId));
        var id = presetId.Trim();
        if (id.Length > 64 || !Regex.IsMatch(id, @"^[a-z0-9_-]+$", RegexOptions.IgnoreCase))
            throw new ArgumentException("허용되지 않는 프리셋 ID입니다.", nameof(presetId));
        return id;
    }

    private static string RequireWorldConfigFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("파일명이 비어 있습니다.", nameof(fileName));
        var n = Path.GetFileName(fileName.Trim());
        if (!string.Equals(n, fileName.Trim(), StringComparison.Ordinal) ||
            n.Contains(Path.DirectorySeparatorChar) ||
            n.Contains(Path.AltDirectorySeparatorChar))
            throw new ArgumentException("허용되지 않는 경로입니다.", nameof(fileName));
        if (!WorldConfigEditableFileNames.Contains(n))
            throw new ArgumentException($"편집할 수 없는 파일입니다: {n}", nameof(fileName));
        return n;
    }

    private static string DefaultWorldConfigJson(string fileName)
    {
        return fileName.Equals("WorldLore.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("EventTypeDatabase.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("DialogueSettings.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("SemanticGuardrailAnchors.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("GuildOfficeExploration.json", StringComparison.OrdinalIgnoreCase)
            ? "{}"
            : "[]";
    }
}
