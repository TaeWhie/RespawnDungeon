using System;
using System.Collections.Generic;
using System.IO;
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
        return JsonSerializer.Deserialize<DialogueSettings>(File.ReadAllText(path))!;
    }

    public List<Character> LoadCharacters()
    {
        var path = Path.Combine(_configPath, "Characters.json");
        if (!File.Exists(path)) return new List<Character>();
        return JsonSerializer.Deserialize<List<Character>>(File.ReadAllText(path)) ?? new();
    }

    public void SaveCharacters(List<Character> characters)
    {
        var path = Path.Combine(_configPath, "Characters.json");
        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var json = JsonSerializer.Serialize(characters, options);
        File.WriteAllText(path, json);
    }

    public TestDataRoot? LoadTestData()
    {
        var path = Path.Combine(_configPath, "TestData.json");
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<TestDataRoot>(File.ReadAllText(path));
    }

    // Load LogGlossary for Lorebook conditional retrieval
    public LogGlossaryRoot? LoadLogGlossary()
    {
        var path = Path.Combine(_configPath, "LogGlossary.json");
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<LogGlossaryRoot>(File.ReadAllText(path));
    }

    public WorldLore? LoadWorldLore()
    {
        var path = Path.Combine(_configPath, "WorldLore.json");
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<WorldLore>(File.ReadAllText(path));
    }

    public List<ItemData> LoadItemDatabase()
    {
        var path = Path.Combine(_configPath, "ItemDatabase.json");
        if (!File.Exists(path)) return new List<ItemData>();
        return JsonSerializer.Deserialize<List<ItemData>>(File.ReadAllText(path)) ?? new();
    }
}
