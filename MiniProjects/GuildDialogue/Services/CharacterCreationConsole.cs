using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>콘솔에서 새 캐릭터를 만들어 <c>CharactersDatabase.json</c>에 저장합니다.</summary>
public static class CharacterCreationConsole
{
    /// <summary>
    /// 대화형으로 캐릭터를 생성하고 DB에 반영합니다.
    /// 서사(Id·이름·배경·말투)는 Ollama만 사용합니다(<see cref="CharacterCreationLlmGenerator"/>).
    /// 나이·경력·접수대 위치는 <see cref="CharacterCreationRng"/>으로 뽑아 프롬프트에 넣습니다.
    /// </summary>
    public static async Task RunCreateCharacterAsync(DialogueConfigLoader loader, CancellationToken ct = default)
    {
        Console.WriteLine();
        Console.WriteLine("========== 캐릭터 생성 ==========");
        Console.WriteLine($"Config: {loader.ConfigDirectory}");
        Console.WriteLine("(서사는 Ollama 필수 — DialogueSettings.json의 BaseUrl·Model 확인)");

        var jobs = loader.LoadJobDatabase();
        if (jobs.Count == 0)
        {
            Console.WriteLine("JobDatabase.json에 직업이 없습니다. Config를 확인하세요.");
            return;
        }

        Console.WriteLine("--- 직업 선택 ---");
        for (var i = 0; i < jobs.Count; i++)
        {
            var j = jobs[i];
            Console.WriteLine($"  {i + 1}. {j.DisplayName} ({j.RoleId})");
        }

        Console.Write("번호: ");
        if (!int.TryParse(Console.ReadLine()?.Trim(), out var jobIdx) || jobIdx < 1 || jobIdx > jobs.Count)
        {
            Console.WriteLine("잘못된 선택입니다.");
            return;
        }

        var job = jobs[jobIdx - 1];
        Console.WriteLine();
        Console.WriteLine($"선택: {job.DisplayName}");
        if (!string.IsNullOrWhiteSpace(job.Description))
            Console.WriteLine(job.Description.Trim());

        var allowed = job.AllowedSkillNames ?? new List<string>();
        var pickedSkills = new List<string>();
        if (allowed.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("--- 스킬 선택 (직업 허용 목록만) ---");
            for (var s = 0; s < allowed.Count; s++)
                Console.WriteLine($"  {s + 1}. {allowed[s]}");
            Console.WriteLine("포함할 번호를 쉼표로 입력 (예: 1,2) 또는 all(전부) 또는 none(없음): ");
            var skillLine = Console.ReadLine()?.Trim() ?? "";
            if (string.Equals(skillLine, "all", StringComparison.OrdinalIgnoreCase))
                pickedSkills.AddRange(allowed);
            else if (string.Equals(skillLine, "none", StringComparison.OrdinalIgnoreCase))
            { }
            else
            {
                foreach (var part in skillLine.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!int.TryParse(part, out var n) || n < 1 || n > allowed.Count)
                    {
                        Console.WriteLine($"잘못된 번호: {part}");
                        return;
                    }

                    var skillName = allowed[n - 1];
                    if (!pickedSkills.Contains(skillName, StringComparer.Ordinal))
                        pickedSkills.Add(skillName);
                }
            }
        }

        var roster = loader.LoadCharacters();
        var bases = loader.LoadBaseDatabase();
        var rng = Random.Shared;

        var age = CharacterCreationRng.RollAge(rng);
        var career = CharacterCreationRng.RollCareerYears(age, rng);
        var currentLocationId = CharacterCreationRng.ResolveReceptionLocationId(bases);
        var currentLocationNote = CharacterCreationRng.RollReceptionLocationNote(rng);
        var receptionFacility = bases.FirstOrDefault(b =>
            string.Equals(b.BaseId?.Trim(), currentLocationId, StringComparison.OrdinalIgnoreCase));

        Console.WriteLine();
        Console.WriteLine("--- Ollama로 서사 생성 중… ---");

        var settings = loader.LoadSettings();
        var ollama = new OllamaClient(settings);
        var profile = await CharacterCreationLlmGenerator.TryGenerateAsync(
            loader,
            ollama,
            job,
            age,
            career,
            pickedSkills,
            currentLocationId,
            receptionFacility,
            roster,
            ct);

        if (profile == null)
        {
            Console.WriteLine();
            Console.WriteLine("[캐릭터 생성] Ollama 서사 생성에 실패했습니다. 저장하지 않습니다. Ollama 실행·모델·네트워크를 확인한 뒤 다시 시도하세요.");
            return;
        }

        var id = profile.Id;
        var name = profile.Name;
        var background = profile.Background;
        var speech = profile.SpeechStyle;

        const string mood = "중립";

        Console.WriteLine();
        Console.WriteLine("--- 생성 결과 ---");
        Console.WriteLine($"  서사 출처: Ollama LLM");
        Console.WriteLine($"  Id: {id}");
        Console.WriteLine($"  이름: {name}");
        Console.WriteLine($"  나이: {age} (16~50 무작위)");
        Console.WriteLine($"  경력: {career}년 (0~20, 나이 상한·낮은 값 가중)");
        Console.WriteLine($"  Mood: {mood} (생성 시 고정)");
        Console.WriteLine($"  위치: {currentLocationId} (BaseDatabase의 접수·등록 시설)");
        if (!string.IsNullOrWhiteSpace(currentLocationNote))
            Console.WriteLine($"  위치 메모: {currentLocationNote}");
        Console.WriteLine($"  배경: {background}");
        Console.WriteLine($"  말투: {speech}");

        var character = new Character
        {
            Id = id,
            Name = name,
            Age = age,
            Role = job.RoleId?.Trim() ?? "",
            PartyId = null,
            CurrentLocationId = currentLocationId,
            CurrentLocationNote = currentLocationNote,
            Career = career,
            Background = background,
            SpeechStyle = speech,
            Mood = mood,
            RecentMemorableEvent = null,
            Personality = CharacterCreationRng.RollPersonality(rng),
            Stats = new CharacterStats(),
            Inventory = new List<InventoryEntry>(),
            Equipment = new EquipmentSlots(),
            Relationships = new List<Relationship>(),
            Skills = pickedSkills
        };

        JobSkillRules.EnforceForCharacter(character, jobs);

        roster.Add(character);
        loader.SaveCharactersDatabase(roster);
        Console.WriteLine();
        Console.WriteLine($"저장 완료: {character.Name} ({character.Id}), Role={character.Role}, 스킬: {string.Join(", ", character.Skills)}");
    }

    /// <summary>웹 허브: 직업·스킬 선택 후 Ollama로 서사 생성 후 DB 저장(콘솔 메뉴 5와 동일).</summary>
    public static Task<CharacterCreationWebResult> CreateCharacterWebAsync(
        DialogueConfigLoader loader,
        int jobIndex1Based,
        string skillSelectionMode,
        int[]? skillIndices1Based,
        CancellationToken ct = default) =>
        CreateCharacterCoreAsync(loader, jobIndex1Based, skillSelectionMode, skillIndices1Based, persist: true, ct);

    /// <summary>웹 허브: 미리보기만 — CharactersDatabase에 쓰지 않음.</summary>
    public static Task<CharacterCreationWebResult> PreviewCharacterWebAsync(
        DialogueConfigLoader loader,
        int jobIndex1Based,
        string skillSelectionMode,
        int[]? skillIndices1Based,
        CancellationToken ct = default) =>
        CreateCharacterCoreAsync(loader, jobIndex1Based, skillSelectionMode, skillIndices1Based, persist: false, ct);

    /// <summary>미리보기에서 받은 캐릭터를 DB에 반영.</summary>
    public static CharacterCreationWebResult CommitCharacterWebAsync(DialogueConfigLoader loader, Character? incoming)
    {
        if (incoming == null || string.IsNullOrWhiteSpace(incoming.Id))
            return new CharacterCreationWebResult(false, "캐릭터 데이터가 없습니다.", null);

        var jobs = loader.LoadJobDatabase();
        var roster = loader.LoadCharacters();
        if (roster.Any(c => c.Id.Equals(incoming.Id.Trim(), StringComparison.OrdinalIgnoreCase)))
            return new CharacterCreationWebResult(false, "이미 같은 Id의 동료가 등록되어 있습니다. 다른 동료를 찾아 보세요.", null);

        incoming.Id = incoming.Id.Trim();
        if (string.IsNullOrWhiteSpace(incoming.Name))
            return new CharacterCreationWebResult(false, "이름이 비어 있습니다.", null);

        incoming.Skills ??= new List<string>();
        incoming.Inventory ??= new List<InventoryEntry>();
        incoming.Relationships ??= new List<Relationship>();
        incoming.Personality ??= CharacterCreationRng.RollPersonality(Random.Shared);
        incoming.Stats ??= new CharacterStats();
        incoming.Equipment ??= new EquipmentSlots();

        JobSkillRules.EnforceForCharacter(incoming, jobs);
        roster.Add(incoming);
        loader.SaveCharactersDatabase(roster);
        return new CharacterCreationWebResult(true, null, incoming);
    }

    private static async Task<CharacterCreationWebResult> CreateCharacterCoreAsync(
        DialogueConfigLoader loader,
        int jobIndex1Based,
        string skillSelectionMode,
        int[]? skillIndices1Based,
        bool persist,
        CancellationToken ct)
    {
        var jobs = loader.LoadJobDatabase();
        if (jobs.Count == 0)
            return new CharacterCreationWebResult(false, "JobDatabase.json에 직업이 없습니다.", null);

        if (jobIndex1Based < 1 || jobIndex1Based > jobs.Count)
            return new CharacterCreationWebResult(false, "직업 번호가 범위를 벗어났습니다.", null);

        var job = jobs[jobIndex1Based - 1];
        var allowed = job.AllowedSkillNames ?? new List<string>();
        var pickedSkills = new List<string>();
        var mode = (skillSelectionMode ?? "all").Trim().ToLowerInvariant();

        if (allowed.Count > 0)
        {
            if (mode == "all")
                pickedSkills.AddRange(allowed);
            else if (mode == "none")
            { }
            else if (mode == "pick")
            {
                if (skillIndices1Based == null || skillIndices1Based.Length == 0)
                    return new CharacterCreationWebResult(false, "pick 모드에서는 스킬 번호가 필요합니다.", null);
                foreach (var n in skillIndices1Based)
                {
                    if (n < 1 || n > allowed.Count)
                        return new CharacterCreationWebResult(false, $"잘못된 스킬 번호: {n}", null);
                    var skillName = allowed[n - 1];
                    if (!pickedSkills.Contains(skillName, StringComparer.Ordinal))
                        pickedSkills.Add(skillName);
                }
            }
            else
                return new CharacterCreationWebResult(false, "skillSelectionMode는 all, none, pick 중 하나여야 합니다.", null);
        }

        var roster = loader.LoadCharacters();
        var bases = loader.LoadBaseDatabase();
        var rng = Random.Shared;

        var age = CharacterCreationRng.RollAge(rng);
        var career = CharacterCreationRng.RollCareerYears(age, rng);
        var currentLocationId = CharacterCreationRng.ResolveReceptionLocationId(bases);
        var currentLocationNote = CharacterCreationRng.RollReceptionLocationNote(rng);
        var receptionFacility = bases.FirstOrDefault(b =>
            string.Equals(b.BaseId?.Trim(), currentLocationId, StringComparison.OrdinalIgnoreCase));

        var settings = loader.LoadSettings();
        var ollama = new OllamaClient(settings);
        var profile = await CharacterCreationLlmGenerator.TryGenerateAsync(
            loader,
            ollama,
            job,
            age,
            career,
            pickedSkills,
            currentLocationId,
            receptionFacility,
            roster,
            ct).ConfigureAwait(false);

        if (profile == null)
            return new CharacterCreationWebResult(false, "Ollama 서사 생성 실패. 모델·네트워크를 확인하세요.", null);

        const string mood = "중립";
        var character = new Character
        {
            Id = profile.Id,
            Name = profile.Name,
            Age = age,
            Role = job.RoleId?.Trim() ?? "",
            PartyId = null,
            CurrentLocationId = currentLocationId,
            CurrentLocationNote = currentLocationNote,
            Career = career,
            Background = profile.Background,
            SpeechStyle = profile.SpeechStyle,
            Mood = mood,
            RecentMemorableEvent = null,
            Personality = CharacterCreationRng.RollPersonality(rng),
            Stats = new CharacterStats(),
            Inventory = new List<InventoryEntry>(),
            Equipment = new EquipmentSlots(),
            Relationships = new List<Relationship>(),
            Skills = pickedSkills
        };

        JobSkillRules.EnforceForCharacter(character, jobs);

        if (persist)
        {
            roster.Add(character);
            loader.SaveCharactersDatabase(roster);
        }

        return new CharacterCreationWebResult(true, null, character);
    }
}

public sealed record CharacterCreationWebResult(bool Ok, string? Error, Character? Character);
