using System.Text.Json;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

public class DialogueConfigLoader
{
    private readonly string _configPath;

    public DialogueConfigLoader(string? configPath = null)
    {
        _configPath = configPath ?? Path.Combine(AppContext.BaseDirectory, "Config");
    }

    public DialogueSettings LoadSettings()
    {
        var path = Path.Combine(_configPath, "DialogueSettings.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"설정 파일을 찾을 수 없습니다: {path}");
        var json = File.ReadAllText(path);
        var settings = JsonSerializer.Deserialize<DialogueSettings>(json);
        return settings ?? throw new InvalidOperationException("DialogueSettings.json 파싱 실패.");
    }

    public List<Character> LoadCharacters()
    {
        var path = Path.Combine(_configPath, "Characters.json");
        if (!File.Exists(path))
            return new List<Character>();
        var json = File.ReadAllText(path);
        var list = JsonSerializer.Deserialize<List<Character>>(json);
        return list ?? new List<Character>();
    }

    public TestDataRoot? LoadTestData()
    {
        var path = Path.Combine(_configPath, "TestData.json");
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<TestDataRoot>(json);
    }
}
