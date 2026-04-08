using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using GuildDialogue.Data;
using GuildDialogue.Services;

namespace GuildDialogue;

/// <summary>웹 허브(Vite)용 최소 HTTP API — 콘솔 메뉴 1~5와 동일 동작을 노출합니다.</summary>
public static class GuildDialogueHubHost
{
    private static DialogueManager? _manager;
    private static HubImageGenerationService? _imageService;
    private static HubImagePromptTranslator? _imagePromptTranslator;
    private static readonly object EmbedWarmupLock = new();
    private static Task<OllamaModelWarmup.WarmupPhase>? _embedWarmupTask;

    /// <summary>다음 스펙테이터(홈) 요청에서 동료 2인 LLM 대화를 돌릴지 — 초기화 직후 첫 방문.</summary>
    private static bool _pendingCompanionDialogueAfterInit;

    /// <summary>던전 원정(시뮬) 성공 직후 다음 홈 대화 — <see cref="CompanionDialoguePendingFile"/>로 콘솔·CLI와 공유.</summary>

    private static readonly object CompanionDialogueFlagsLock = new();

    private static bool PeekCompanionDialogueThisRun()
    {
        bool init;
        lock (CompanionDialogueFlagsLock)
            init = _pendingCompanionDialogueAfterInit;
        if (init)
            return true;
        return CompanionDialoguePendingFile.ReadPostDungeonPending(DialogueConfigLoader.ResolveDefaultConfigDirectory());
    }

    private static void ClearCompanionDialoguePendingIfConsumed(bool runPair, bool dialogueAttempted)
    {
        if (!runPair || !dialogueAttempted)
            return;
        lock (CompanionDialogueFlagsLock)
            _pendingCompanionDialogueAfterInit = false;
        CompanionDialoguePendingFile.ClearPostDungeonPending(DialogueConfigLoader.ResolveDefaultConfigDirectory());
    }

    public static async Task RunAsync(string[] args)
    {
        var port = 5050;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--port", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(args[i + 1], out var p) && p > 0)
                port = p;
        }

        var builder = WebApplication.CreateBuilder();
        builder.Services.Configure<JsonOptions>(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            o.SerializerOptions.PropertyNameCaseInsensitive = true;
            o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });
        builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
            p.WithOrigins(
                    "http://localhost:5173",
                    "http://127.0.0.1:5173",
                    "http://localhost:3000",
                    "http://127.0.0.1:3000")
                .AllowAnyHeader()
                .AllowAnyMethod()));

        var app = builder.Build();
        app.UseCors();

        var loader = new DialogueConfigLoader();
        _imageService ??= new HubImageGenerationService(loader.ConfigDirectory);
        _imagePromptTranslator ??= new HubImagePromptTranslator(loader.LoadSettings());

        app.MapGet("/api/health", () => Results.Json(new { ok = true, service = "GuildDialogueHub" }));

        /// <summary>Ollama에 대화·임베딩 모델을 미리 올립니다(첫 로드 시 GPU 로딩 시간).</summary>
        app.MapPost("/api/model/warmup", async (HttpRequest req, CancellationToken ct) =>
        {
            try
            {
                WarmupRequestDto? dto = null;
                if ((req.ContentLength ?? 0) > 0)
                {
                    dto = await JsonSerializer.DeserializeAsync<WarmupRequestDto>(
                            req.Body,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                            ct)
                        .ConfigureAwait(false);
                }
                var settings = loader.LoadSettings();
                var fastStart = dto?.FastStart ?? false;
                var backgroundEmbed = dto?.BackgroundEmbed ?? fastStart;
                OllamaModelWarmup.WarmupResult r;
                if (!fastStart)
                {
                    r = await OllamaModelWarmup.RunAsync(settings, ct).ConfigureAwait(false);
                }
                else
                {
                    var phases = new List<OllamaModelWarmup.WarmupPhase>();
                    var chat = await OllamaModelWarmup.WarmupChatAsync(settings, ct).ConfigureAwait(false);
                    phases.Add(chat);
                    if (!chat.Ok)
                    {
                        r = new OllamaModelWarmup.WarmupResult(false, chat.Detail, phases, chat.Ms);
                    }
                    else if (backgroundEmbed)
                    {
                        EnsureBackgroundEmbedWarmup(settings);
                        phases.Add(new OllamaModelWarmup.WarmupPhase(
                            "embed",
                            OllamaModelWarmup.ResolveEmbedModel(settings),
                            true,
                            0,
                            "백그라운드 로딩 중 — 메인 화면 먼저 진입",
                            true));
                        r = new OllamaModelWarmup.WarmupResult(true, null, phases, chat.Ms);
                    }
                    else
                    {
                        var embed = await OllamaModelWarmup.WarmupEmbedAsync(settings, ct).ConfigureAwait(false);
                        phases.Add(embed);
                        var ok = embed.Ok || embed.Skipped;
                        r = new OllamaModelWarmup.WarmupResult(
                            ok,
                            ok ? null : embed.Detail,
                            phases,
                            chat.Ms + embed.Ms);
                    }
                }
                return Results.Json(new
                {
                    ok = r.Ok,
                    error = r.Error,
                    totalMs = r.TotalMs,
                    phases = r.Phases.Select(p => new
                    {
                        id = p.Id,
                        model = p.Model,
                        ok = p.Ok,
                        ms = p.Ms,
                        detail = p.Detail,
                        skipped = p.Skipped
                    })
                });
            }
            catch (Exception ex)
            {
                return Results.Json(new { ok = false, error = ex.Message, totalMs = 0, phases = Array.Empty<object>() }, statusCode: 500);
            }
        });

        /// <summary>홈 화면 세계관 소개용 — WorldLore.json 요약 필드.</summary>
        app.MapGet("/api/world/overview", () =>
        {
            try
            {
                var wl = new DialogueConfigLoader();
                var lore = wl.LoadWorldLore();
                if (lore == null)
                {
                    return Results.Json(new
                    {
                        worldName = "",
                        worldSummary = "",
                        guildInfo = "",
                        teaserLines = System.Array.Empty<string>()
                    });
                }

                var teasers = (lore.Lore ?? new List<string>())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Take(3)
                    .ToList();

                return Results.Json(new
                {
                    worldName = lore.WorldName ?? "",
                    worldSummary = lore.WorldSummary ?? "",
                    guildInfo = lore.GuildInfo ?? "",
                    dungeonSystem = lore.DungeonSystem ?? "",
                    baseCamp = lore.BaseCamp ?? "",
                    currencyAndLoot = lore.CurrencyAndLoot ?? "",
                    locations = lore.Locations ?? new List<LocationData>(),
                    dungeons = lore.Dungeons ?? new List<DungeonData>(),
                    teaserLines = teasers
                });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        app.MapPost("/api/images/resolve", async (HubImageResolveDto dto, CancellationToken ct) =>
        {
            try
            {
                if (_imageService == null)
                    return Results.Json(new { error = "image service not initialized" }, statusCode: 500);
                var r = await _imageService.ResolveOrGenerateNowAsync(new HubImageGenerationService.HubImageResolveRequest(
                    dto.Scope ?? "generic",
                    dto.EntityKey ?? "default",
                    dto.Prompt ?? "",
                    dto.ThemeId ?? "guildhub-2d-v1",
                    dto.Width ?? 768,
                    dto.Height ?? 768), ct).ConfigureAwait(false);
                return Results.Json(new
                {
                    status = r.Status,
                    cacheKey = r.CacheKey,
                    imageUrl = r.ImageUrl,
                    error = r.Error,
                    themeId = r.ThemeId
                });
            }
            catch (Exception ex)
            {
                return Results.Json(new { status = "error", error = ex.Message }, statusCode: 500);
            }
        });

        app.MapPost("/api/images/translate", async (HubImageTranslateDto dto, CancellationToken ct) =>
        {
            try
            {
                if (_imagePromptTranslator == null)
                    return Results.Json(new { error = "image prompt translator not initialized" }, statusCode: 500);
                var translated = await _imagePromptTranslator.TranslateToEnglishAsync(dto.Prompt ?? "", ct).ConfigureAwait(false);
                return Results.Json(new { translatedPrompt = translated });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        app.MapGet("/api/images/file/{cacheKey}", (string cacheKey) =>
        {
            if (_imageService == null)
                return Results.Json(new { error = "image service not initialized" }, statusCode: 500);
            if (!_imageService.TryGetFilePath(cacheKey, out var path))
                return Results.NotFound();
            return Results.File(path, "image/png");
        });

        app.MapGet("/api/state", () =>
        {
            try
            {
                var chars = loader.LoadCharactersWithJobSkillFilter();
                var parties = loader.LoadPartyDatabase();
                var jobs = loader.LoadJobDatabase();
                var logs = loader.LoadTimelineData()?.ActionLog?.OrderBy(e => e.Order).ToList() ?? new();
                return Results.Json(new HubStateDto(
                    loader.ConfigDirectory,
                    chars,
                    parties,
                    jobs,
                    logs.Count > 20 ? logs.Skip(logs.Count - 20).ToList() : logs));
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        /// <summary>ActionLog·캐릭터·파티 DB 비우기 + 동료 대화 대기 플래그 제거. 대화 매니저 메모리도 무효화.</summary>
        app.MapPost("/api/guild/reset", () =>
        {
            try
            {
                var resetLoader = new DialogueConfigLoader();
                var configDir = resetLoader.ConfigDirectory;
                if (string.IsNullOrWhiteSpace(configDir))
                    return Results.Json(new { error = "config directory not resolved" }, statusCode: 500);

                resetLoader.SaveActionLog(new TestDataRoot());
                resetLoader.SaveCharactersDatabase(new List<Character>());
                resetLoader.SavePartyDatabase(new List<PartyData>());
                CompanionDialoguePendingFile.ClearPostDungeonPending(configDir);
                lock (CompanionDialogueFlagsLock)
                {
                    _pendingCompanionDialogueAfterInit = false;
                    _manager = null;
                }

                return Results.Json(new { ok = true });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        app.MapGet("/api/config/world", () =>
        {
            try
            {
                var wl = new DialogueConfigLoader();
                var files = new Dictionary<string, string>();
                foreach (var name in DialogueConfigLoader.WorldConfigEditableFileNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    files[name] = wl.ReadWorldConfigText(name);
                var issues = WorldConfigConsistencyChecker.Validate(wl, null);
                return Results.Json(new { files, issues });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        app.MapPost("/api/config/world/validate", (WorldConfigValidateDto? dto) =>
        {
            try
            {
                var wl = new DialogueConfigLoader();
                var issues = WorldConfigConsistencyChecker.Validate(wl, dto?.Overrides);
                return Results.Json(new { issues });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        app.MapPut("/api/config/world/file", (WorldConfigFilePutDto dto) =>
        {
            try
            {
                var wl = new DialogueConfigLoader();
                wl.WriteWorldConfigText(dto.FileName, dto.Content);
                return Results.Json(new { ok = true });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 400);
            }
        });

        app.MapGet("/api/config/world/presets", () =>
        {
            try
            {
                var wl = new DialogueConfigLoader();
                var presets = wl.LoadWorldPresetsManifest();
                return Results.Json(new { presets });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        app.MapGet("/api/config/world/preset/{presetId}", (string presetId) =>
        {
            try
            {
                var wl = new DialogueConfigLoader();
                var manifest = wl.LoadWorldPresetsManifest();
                var inManifest = manifest.Any(p => string.Equals(p.Id, presetId, StringComparison.OrdinalIgnoreCase));
                var presetDir = Path.Combine(wl.ConfigDirectory, "Presets", presetId.Trim());
                if (manifest.Count > 0 && !inManifest)
                    return Results.Json(new { error = $"알 수 없는 프리셋: {presetId}" }, statusCode: 404);
                if (manifest.Count == 0 && !Directory.Exists(presetDir))
                    return Results.Json(new { error = $"알 수 없는 프리셋: {presetId}" }, statusCode: 404);

                var files = wl.LoadWorldPresetFiles(presetId);
                var issues = WorldConfigConsistencyChecker.Validate(wl, files);
                var label = manifest.FirstOrDefault(p => string.Equals(p.Id, presetId, StringComparison.OrdinalIgnoreCase))?.Label;
                return Results.Json(new { files, issues, presetId, label });
            }
            catch (ArgumentException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 400);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        app.MapPost("/api/dialogue/init", async (CancellationToken ct) =>
        {
            try
            {
                _manager = new DialogueManager();
                await _manager.InitializeAsync(ct).ConfigureAwait(false);
                lock (CompanionDialogueFlagsLock)
                    _pendingCompanionDialogueAfterInit = true;
                return Results.Json(new DialogueInitDto(true, null, _manager.Characters.Count));
            }
            catch (Exception ex)
            {
                _manager = null;
                return Results.Json(new DialogueInitDto(false, ex.Message, 0), statusCode: 500);
            }
        });

        app.MapPost("/api/dialogue/guild-master/begin", () =>
        {
            if (_manager == null)
                return Results.Json(new { error = "먼저 /api/dialogue/init 을 호출하세요." }, statusCode: 400);
            _manager.BeginGuildMasterSession();
            return Results.Json(new { ok = true });
        });

        app.MapPost("/api/dialogue/guild-master/switch-buddy", (SwitchBuddyDto dto) =>
        {
            if (_manager == null)
                return Results.Json(new { error = "dialogue not initialized" }, statusCode: 400);
            _manager.ResetGuildOfficeBuddyContext();
            return Results.Json(new { ok = true, buddyId = dto.BuddyId });
        });

        app.MapPost("/api/dialogue/guild-master/message", async (GuildMasterMessageDto dto, CancellationToken ct) =>
        {
            if (_manager == null)
                return Results.Json(new { error = "dialogue not initialized" }, statusCode: 400);
            var r = await _manager.GuildMasterUserTurnAsync(dto.BuddyId, dto.Message, ct).ConfigureAwait(false);
            return Results.Json(new GuildMasterMessageResponse(
                string.IsNullOrEmpty(r.ErrorMessage),
                r.BuddyLine,
                r.ErrorMessage,
                r.AtypicalKind.ToString(),
                r.DeepExpedition,
                r.MetaSimilarity,
                r.OffWorldSimilarity,
                r.ExpeditionSimilarity));
        });

        app.MapPost("/api/dialogue/guild-master/end", async (CancellationToken ct) =>
        {
            if (_manager == null)
                return Results.Json(new { error = "dialogue not initialized" }, statusCode: 400);
            await _manager.EndGuildMasterSessionAsync(ct).ConfigureAwait(false);
            return Results.Json(new { ok = true });
        });

        app.MapPost("/api/dialogue/spectator/run", async (CancellationToken ct) =>
        {
            if (_manager == null)
                return Results.Json(new { error = "dialogue not initialized" }, statusCode: 400);
            var runPair = PeekCompanionDialogueThisRun();
            var (transcript, attempted) = await BaseHubScreen.RunSpectatorForApiAsync(_manager, ct, runPair)
                .ConfigureAwait(false);
            ClearCompanionDialoguePendingIfConsumed(runPair, attempted);
            return Results.Json(new { ok = true, transcript, companionDialogueGenerated = attempted });
        });

        /// <summary>NDJSON 스트림: <c>{"line":"…"}</c> 반복 후 <c>{"done":true}</c> 또는 <c>{"error":"…"}</c></summary>
        app.MapPost("/api/dialogue/spectator/stream", async (HttpContext http, CancellationToken ct) =>
        {
            if (_manager == null)
            {
                http.Response.StatusCode = 400;
                http.Response.ContentType = "application/json; charset=utf-8";
                await http.Response.WriteAsync("{\"error\":\"dialogue not initialized\"}", ct).ConfigureAwait(false);
                return;
            }

            var jsonOpts = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            http.Response.ContentType = "application/x-ndjson; charset=utf-8";
            http.Response.Headers.CacheControl = "no-store";
            await http.Response.StartAsync(ct).ConfigureAwait(false);

            async Task WriteNdjsonAsync(object payload)
            {
                var chunk = JsonSerializer.Serialize(payload, jsonOpts) + "\n";
                await http.Response.WriteAsync(chunk, ct).ConfigureAwait(false);
                await http.Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }

            try
            {
                var runPair = PeekCompanionDialogueThisRun();
                var attempted = await BaseHubScreen.RunSpectatorStreamForApiAsync(
                    _manager,
                    async line => await WriteNdjsonAsync(new { line }).ConfigureAwait(false),
                    ct,
                    runPair).ConfigureAwait(false);
                ClearCompanionDialoguePendingIfConsumed(runPair, attempted);
                await WriteNdjsonAsync(new { done = true, companionDialogueGenerated = attempted }).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await WriteNdjsonAsync(new { error = "cancelled" }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await WriteNdjsonAsync(new { error = ex.Message }).ConfigureAwait(false);
            }
        });

        app.MapGet("/api/parties", () =>
        {
            try
            {
                var parties = loader.LoadPartyDatabase();
                var chars = loader.LoadCharactersWithJobSkillFilter()
                    .Where(c => !c.Id.Equals("master", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                return Results.Json(new { parties, roster = chars });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        app.MapPost("/api/parties", (CreatePartyDto dto) =>
        {
            try
            {
                var partyId = string.IsNullOrWhiteSpace(dto.PartyId) ? PartyManagementConsole.SuggestPartyId() : dto.PartyId.Trim();
                if (!PartyManagementConsole.IsValidPartyId(partyId))
                    return Results.Json(new { error = "PartyId는 영문·숫자·_·- 만 사용하세요." }, statusCode: 400);
                if (string.IsNullOrWhiteSpace(dto.Name))
                    return Results.Json(new { error = "파티 이름은 필수입니다." }, statusCode: 400);

                var parties = loader.LoadPartyDatabase();
                if (parties.Any(p => p.PartyId.Equals(partyId, StringComparison.OrdinalIgnoreCase)))
                    return Results.Json(new { error = "이미 같은 PartyId가 있습니다." }, statusCode: 400);

                var chars = loader.LoadCharactersWithJobSkillFilter();
                var memberIds = (dto.MemberIds ?? Array.Empty<string>())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (memberIds.Count == 0)
                    return Results.Json(new { error = "멤버가 없습니다." }, statusCode: 400);

                var party = new PartyData
                {
                    PartyId = partyId,
                    Name = dto.Name.Trim(),
                    Callsign = string.IsNullOrWhiteSpace(dto.Callsign) ? null : dto.Callsign.Trim(),
                    Description = dto.Description?.Trim() ?? "",
                    MemberIds = memberIds,
                    Notes = "Hub에서 편성됨."
                };

                parties.Add(party);
                PartyManagementConsole.ApplyPartyMembership(parties, chars, party, memberIds);
                loader.SavePartyDatabase(parties);
                loader.SaveCharactersDatabase(chars);
                return Results.Json(new { ok = true, party });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        app.MapPut("/api/parties/{partyId}", (string partyId, UpdatePartyDto dto) =>
        {
            try
            {
                var parties = loader.LoadPartyDatabase();
                var party = parties.FirstOrDefault(p =>
                    p.PartyId.Equals(partyId, StringComparison.OrdinalIgnoreCase));
                if (party == null)
                    return Results.Json(new { error = "파티 없음" }, statusCode: 404);

                if (!string.IsNullOrWhiteSpace(dto.Name)) party.Name = dto.Name.Trim();
                if (dto.Callsign != null)
                    party.Callsign = string.IsNullOrWhiteSpace(dto.Callsign) ? null : dto.Callsign.Trim();
                if (dto.Description != null) party.Description = dto.Description;

                var chars = loader.LoadCharactersWithJobSkillFilter();
                if (dto.MemberIds != null)
                {
                    var memberIds = dto.MemberIds.Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    if (memberIds.Count == 0)
                        return Results.Json(new { error = "멤버가 비어 있으면 저장할 수 없습니다." }, statusCode: 400);
                    PartyManagementConsole.ApplyPartyMembership(parties, chars, party, memberIds);
                }

                loader.SavePartyDatabase(parties);
                loader.SaveCharactersDatabase(chars);
                return Results.Json(new { ok = true, party });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        app.MapDelete("/api/parties/{partyId}", (string partyId) =>
        {
            try
            {
                var parties = loader.LoadPartyDatabase();
                var party = parties.FirstOrDefault(p =>
                    p.PartyId.Equals(partyId, StringComparison.OrdinalIgnoreCase));
                if (party == null)
                    return Results.Json(new { error = "파티 없음" }, statusCode: 404);

                var chars = loader.LoadCharactersWithJobSkillFilter();
                foreach (var c in chars.Where(c =>
                             !string.IsNullOrWhiteSpace(c.PartyId) &&
                             c.PartyId.Equals(party.PartyId, StringComparison.OrdinalIgnoreCase)))
                    c.PartyId = null;

                parties.RemoveAll(p => p.PartyId.Equals(party.PartyId, StringComparison.OrdinalIgnoreCase));
                loader.SavePartyDatabase(parties);
                loader.SaveCharactersDatabase(chars);
                return Results.Json(new { ok = true });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        app.MapGet("/api/expedition/options", () =>
        {
            try
            {
                var lore = loader.LoadWorldLore();
                var log = loader.LoadTimelineData()?.ActionLog ?? new List<ActionLogEntry>();
                var list = new List<object>();
                if (lore?.Dungeons != null)
                {
                    foreach (var d in lore.Dungeons)
                    {
                        if (string.IsNullOrWhiteSpace(d.Name)) continue;
                        var maxSel = ExpeditionDungeonProgress.GetMaxSelectableOrdinal(log, d);
                        var maxCleared = ExpeditionDungeonProgress.GetMaxClearedOrdinal(log, d);
                        var cap = ExpeditionDungeonProgress.MaxFloorCap(d);
                        var floors = new List<object>();
                        for (var o = 1; o <= maxSel; o++)
                        {
                            floors.Add(new
                            {
                                ordinal = o,
                                label = ExpeditionDungeonProgress.FloorOrdinalToLabel(d, o)
                            });
                        }

                        list.Add(new
                        {
                            name = d.Name,
                            difficulty = d.Difficulty ?? "",
                            maxClearedOrdinal = maxCleared,
                            maxSelectableOrdinal = maxSel,
                            floorCap = cap,
                            isAbyssStyle = ExpeditionDungeonProgress.IsAbyssStyle(d),
                            floors
                        });
                    }
                }

                return Results.Json(new { dungeons = list });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });

        app.MapPost("/api/expedition", (ExpeditionRequestDto dto) =>
        {
            try
            {
                var r = PartyManagementConsole.RunExpeditionForParty(
                    loader,
                    dto.PartyId,
                    dto.Seed,
                    dto.SyncChars,
                    dto.ReplaceActionLog,
                    dto.DungeonName,
                    dto.FloorOrdinal);
                if (!r.Ok)
                    return Results.Json(new { ok = false, error = r.Error }, statusCode: 400);
                /* RunExpeditionForParty 내부에서 CompanionDialoguePendingFile 기록 */
                return Results.Json(new
                {
                    ok = true,
                    r.ActionLogPath,
                    r.PartyName,
                    r.ThisRunEntries,
                    r.PrevTotalEntries,
                    r.NewTotalEntries,
                    r.LogNote,
                    r.SimLogLines
                });
            }
            catch (Exception ex)
            {
                return Results.Json(new { ok = false, error = ex.Message }, statusCode: 500);
            }
        });

        app.MapPost("/api/character/create", async (CreateCharacterDto dto, CancellationToken ct) =>
        {
            try
            {
                var mode = string.IsNullOrWhiteSpace(dto.SkillMode) ? "all" : dto.SkillMode.Trim();
                var r = await CharacterCreationConsole.CreateCharacterWebAsync(
                    loader,
                    dto.JobIndex,
                    mode,
                    dto.SkillIndices,
                    ct).ConfigureAwait(false);
                if (!r.Ok)
                    return Results.Json(new { ok = false, error = r.Error }, statusCode: 400);
                return Results.Json(new { ok = true, character = r.Character });
            }
            catch (Exception ex)
            {
                return Results.Json(new { ok = false, error = ex.Message }, statusCode: 500);
            }
        });

        app.MapPost("/api/character/preview", async (CreateCharacterDto dto, CancellationToken ct) =>
        {
            try
            {
                var mode = string.IsNullOrWhiteSpace(dto.SkillMode) ? "all" : dto.SkillMode.Trim();
                var r = await CharacterCreationConsole.PreviewCharacterWebAsync(
                    loader,
                    dto.JobIndex,
                    mode,
                    dto.SkillIndices,
                    dto.ExcludeIds,
                    dto.ExcludeNames,
                    ct).ConfigureAwait(false);
                if (!r.Ok)
                    return Results.Json(new { ok = false, error = r.Error }, statusCode: 400);
                return Results.Json(new { ok = true, character = r.Character });
            }
            catch (Exception ex)
            {
                return Results.Json(new { ok = false, error = ex.Message }, statusCode: 500);
            }
        });

        app.MapPost("/api/character/commit", (CommitCharacterDto dto) =>
        {
            try
            {
                var r = CharacterCreationConsole.CommitCharacterWebAsync(loader, dto.Character);
                if (!r.Ok)
                    return Results.Json(new { ok = false, error = r.Error }, statusCode: 400);
                return Results.Json(new { ok = true, character = r.Character });
            }
            catch (Exception ex)
            {
                return Results.Json(new { ok = false, error = ex.Message }, statusCode: 500);
            }
        });

        Console.WriteLine($"[Hub API] http://127.0.0.1:{port}  (CORS: Vite 5173)");
        await app.RunAsync($"http://127.0.0.1:{port}").ConfigureAwait(false);
    }

    private static void EnsureBackgroundEmbedWarmup(DialogueSettings settings)
    {
        lock (EmbedWarmupLock)
        {
            if (_embedWarmupTask is { IsCompleted: false })
                return;
            _embedWarmupTask = Task.Run(async () =>
                await OllamaModelWarmup.WarmupEmbedAsync(settings, CancellationToken.None).ConfigureAwait(false));
        }
    }

    private sealed record HubStateDto(
        string ConfigDirectory,
        List<Character> Characters,
        List<PartyData> Parties,
        List<JobRoleData> Jobs,
        List<ActionLogEntry> Logs);

    private sealed record DialogueInitDto(bool Ok, string? Error, int CharacterCount);

    private sealed record GuildMasterMessageDto(string BuddyId, string Message);

    private sealed record SwitchBuddyDto(string BuddyId);

    private sealed record GuildMasterMessageResponse(
        bool Ok,
        string? Line,
        string? Error,
        string AtypicalKind,
        bool DeepExpedition,
        double MetaSimilarity,
        double OffWorldSimilarity,
        double ExpeditionSimilarity);

    private sealed record CreatePartyDto(string? PartyId, string Name, string? Callsign, string? Description, string[]? MemberIds);

    private sealed record UpdatePartyDto(string? Name, string? Callsign, string? Description, string[]? MemberIds);

    private sealed class ExpeditionRequestDto
    {
        public string PartyId { get; set; } = "";
        public int? Seed { get; set; }
        public bool SyncChars { get; set; }
        public bool ReplaceActionLog { get; set; }
        public string? DungeonName { get; set; }
        public int? FloorOrdinal { get; set; }
    }

    private sealed record CreateCharacterDto(
        int JobIndex,
        string SkillMode,
        int[]? SkillIndices,
        string[]? ExcludeIds,
        string[]? ExcludeNames);

    private sealed record CommitCharacterDto(Character? Character);

    private sealed record WarmupRequestDto(bool FastStart = false, bool BackgroundEmbed = false);
    private sealed record HubImageResolveDto(
        string? Scope,
        string? EntityKey,
        string? Prompt,
        string? ThemeId,
        int? Width,
        int? Height);

    private sealed record HubImageTranslateDto(string? Prompt);
}

/// <summary>Hub 세계관 검증 — 편집 중인 JSON을 디스크 내용과 합쳐 검사합니다.</summary>
public sealed class WorldConfigValidateDto
{
    public Dictionary<string, string>? Overrides { get; set; }
}

public sealed class WorldConfigFilePutDto
{
    public string FileName { get; set; } = "";
    public string Content { get; set; } = "";
}
