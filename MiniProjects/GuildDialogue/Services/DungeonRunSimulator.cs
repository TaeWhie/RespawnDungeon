using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// Config(Character·Monster·Trap·Item·Base·WorldLore)를 읽어 **인과·시간 순서가 맞는** 던전 타임라인을 생성합니다.
/// 순서: 아지트 준비 → 던전 진입 → (전투 → 함정/회피 → 필요 시 치유/포션)*웨이브 → 정리·루팅·유물 → 결과 → 복귀.
/// </summary>
public static class DungeonRunSimulator
{
    private static readonly string[] TalkClosingA =
    {
        "이번엔 무사히 돌아왔네.", "룬은 길드장님께 넘기자.", "다음엔 한 층 더 올라가 보자.",
        "브람 상태 괜찮아 보여?", "탐색 시간이 좀 길었지."
    };

    private static readonly string[] TalkClosingB =
    {
        "응, 포션만 아꼈으면 더 좋았을 텐데.", "브람은 벌써 씻으러 갔대.", "맞아, 보고부터 하자.",
        "무리한 선택은 없었어.", "돌아오는 길에 안개가 꽤 껴서."
    };

    private sealed class Member
    {
        public int Hp;
        public int MpMax;
        public int Mp;
        public int HpMax;
        public readonly string Id;
        public readonly string Kr;

        public Member(string id, string kr, int hp, int hpMax, int mp, int mpMax)
        {
            Id = id;
            Kr = kr;
            Hp = hp;
            HpMax = hpMax;
            Mp = mp;
            MpMax = mpMax;
        }
    }

    public static DungeonSimulationResult GenerateDefaultRun(int? rngSeed = null)
    {
        var loader = new DialogueConfigLoader();
        return Generate(loader.LoadSimulationInputs(), rngSeed);
    }

    /// <param name="rngSeed">null이면 실행마다 다른 시드.</param>
    public static DungeonSimulationResult Generate(DungeonSimulationInputs data, int? rngSeed = null)
    {
        var seed = rngSeed ?? MixSeed();
        var rnd = new Random(seed);

        var sourceChars = FilterCharactersForSimulation(data.Characters, data.SimulationPartyId);
        var workingChars = sourceChars.Count > 0
            ? CloneCharacterRoster(sourceChars)
            : new List<Character>();
        var charById = workingChars
            .Where(c => !string.IsNullOrWhiteSpace(c.Id))
            .ToDictionary(c => c.Id, c => c, StringComparer.OrdinalIgnoreCase);

        var roster = BuildRosterFromCharacters(workingChars);
        var partyDisplay = roster.Select(m => m.Kr).ToList();
        if (partyDisplay.Count == 0)
        {
            roster = DefaultRoster();
            partyDisplay = roster.Select(m => m.Kr).ToList();
        }

        var invContext = charById.Count > 0 ? (IReadOnlyDictionary<string, Character>?)charById : null;

        var bases = data.Bases;
        var mainHall = ResolveBaseId(bases, "main_hall", "main_hall", "메인", "홀");
        var questBoard = ResolveBaseId(bases, "quest_board", "quest_board", "게시", "의뢰");
        var training = ResolveBaseId(bases, "training_ground", "training", "훈련");
        var cafeteria = ResolveBaseId(bases, "cafeteria", "cafeteria", "식당");
        var reception = ResolveBaseId(bases, "reception", "reception", "접수");

        var dungeonPick = PickDungeon(data.Lore, rnd);
        var dungeonName = dungeonPick?.Name ?? "미지의 유적 (기록 미상)";
        var floor = FloorForDifficulty(dungeonPick?.Difficulty, rnd);

        var monsterPool = BuildMonsterPool(data.Monsters, dungeonPick);
        var traps = data.Traps.Count > 0 ? data.Traps.ToList() : new List<TrapTypeData>();
        var items = data.Items;

        var log = new List<ActionLogEntry>();
        var order = 0;
        void Add(ActionLogEntry e)
        {
            e.Order = ++order;
            log.Add(e);
        }

        void AddBase(string eventType, string baseId)
        {
            Add(new ActionLogEntry
            {
                Type = "Base",
                PartyMembers = new List<string>(partyDisplay),
                EventType = eventType,
                TimeOffsetSeconds = 0,
                Location = baseId
            });
        }

        // --- 1) 아지트: 항상 준비 후 출정 (논리 순서 고정) ---
        AddBase("income", mainHall);
        AddBase("partyForm", mainHall);
        if (rnd.NextDouble() < 0.7)
            AddBase("questAccept", questBoard);
        if (rnd.NextDouble() < 0.3)
            AddBase("training", training);
        if (rnd.NextDouble() < 0.15)
            AddBase("meal", cafeteria);
        if (rnd.NextDouble() < 0.12)
            AddBase("adventurerRegister", reception);

        double t = 0;
        void AddDn(string eventType, string location, Action<ActionLogEntry>? fill = null)
        {
            var e = new ActionLogEntry
            {
                Type = "Dungeon",
                PartyMembers = new List<string>(partyDisplay),
                DungeonName = dungeonName,
                FloorOrZone = floor,
                EventType = eventType,
                TimeOffsetSeconds = Math.Round(t, 1),
                Location = location
            };
            fill?.Invoke(e);
            Add(e);
        }

        Member Pick(Func<Member, bool>? pred = null)
        {
            var list = pred == null ? roster : roster.Where(pred).ToList();
            if (list.Count == 0) list = roster;
            return list[rnd.Next(list.Count)];
        }

        Member? Healer() => roster.FirstOrDefault(m => m.Id.Equals("rina", StringComparison.OrdinalIgnoreCase))
                           ?? roster.FirstOrDefault();

        // --- 2) 던전 진입 (첫 이벤트는 반드시 income, t=0) ---
        AddDn("income", "입구", null);
        t += RndRange(rnd, 6, 14);

        var waveCount = rnd.Next(2, 5);
        string Zone(int w) => w == 0 ? "입구 연결 통로" : $"제{w} 교차 구역";

        var outcome = "clear";
        var aborted = false;

        for (var w = 0; w < waveCount && !aborted; w++)
        {
            var loc = Zone(w);

            // 2a) 전투 (이 구역의 주 이벤트)
            var actor = Pick();
            var pack = RollEnemyPack(rnd, monsterPool, dungeonPick);
            var turns = rnd.Next(2, 7);
            var block = rnd.Next(0, 3);
            var before = actor.Hp;
            var loss = rnd.Next(4, 16) + pack.Sum(p => p.Count * 2);
            actor.Hp = Math.Max(1, actor.Hp - loss);
            var mpCost = rnd.Next(4, 12);
            var mpBefore = actor.Mp;
            actor.Mp = Math.Max(0, actor.Mp - mpCost);
            AddDn("combat", loc, e =>
            {
                e.ActorId = actor.Id;
                e.Enemies = pack;
                e.Turns = turns;
                e.BlockCount = block;
                e.HpBefore = before;
                e.HpAfter = actor.Hp;
                e.MpBefore = mpBefore;
                e.MpAfter = actor.Mp;
            });
            t += RndRange(rnd, 10, 24);

            // 2b) 함정 **또는** 회피 (동일 구역에서 한 가지만 — 전투 직후)
            if (traps.Count > 0 && rnd.NextDouble() < 0.65)
            {
                var trap = traps[rnd.Next(traps.Count)];
                if (rnd.NextDouble() < 0.38)
                {
                    AddDn("trapAvoided", $"{trap.TrapName} 인근", e =>
                    {
                        e.AvoidedCount = rnd.Next(1, 3);
                        e.ActorId = Pick().Id;
                    });
                }
                else
                {
                    var victim = Pick();
                    var beforeT = victim.Hp;
                    var dmg = TrapDamage(trap.DangerLevel, rnd);
                    victim.Hp = Math.Max(1, victim.Hp - dmg);
                    AddDn("trap", trap.TrapName, e =>
                    {
                        e.TargetId = victim.Id;
                        e.TrapId = string.IsNullOrEmpty(trap.TrapId) ? null : trap.TrapId;
                        e.Damage = dmg;
                        e.HpBefore = beforeT;
                        e.HpAfter = victim.Hp;
                    });
                }
                t += RndRange(rnd, 3, 12);
            }

            // 2c) 회복: 부상자가 있을 때만, 치유 스킬 → 필요 시 포션 (DB에 있는 이름만)
            ApplyRecovery(rnd, roster, Healer, Pick, items, invContext, AddDn, ref t, loc);

            if (ShouldAbortRun(rnd, roster))
            {
                outcome = rnd.NextDouble() < 0.55 ? "retreat" : "fail";
                aborted = true;
                break;
            }

            // 2d) 간헐적 스킬 사용 (MP 있을 때, 전투 후 정리)
            if (rnd.NextDouble() < 0.28)
            {
                var casters = roster.Where(m => m.Mp >= 10).ToList();
                if (casters.Count > 0)
                {
                    var c = casters[rnd.Next(casters.Count)];
                    var mb = c.Mp;
                    var cost = rnd.Next(6, 15);
                    c.Mp = Math.Max(0, c.Mp - cost);
                    AddDn("skilluse", loc, e =>
                    {
                        e.ActorId = c.Id;
                        e.MpBefore = mb;
                        e.MpAfter = c.Mp;
                    });
                    t += RndRange(rnd, 2, 8);
                }
            }
        }

        // --- 3) 원정 후반: 전투가 끝난 뒤에만 루팅·유물 (논리 순서) ---
        if (!aborted)
        {
            var lootRoom = "전리 정리 지점";
            var healTarget = roster.OrderBy(m => (double)m.Hp / m.HpMax).First();
            if (healTarget.Hp < healTarget.HpMax * 0.45 && rnd.NextDouble() < 0.55)
                ApplyRecovery(rnd, roster, Healer, Pick, items, invContext, AddDn, ref t, lootRoom);

            if (rnd.NextDouble() < 0.42 && roster.FirstOrDefault(m => m.Id.Equals("rina", StringComparison.OrdinalIgnoreCase)) is { } sup)
            {
                var tar = Pick(m => m.Id != sup.Id);
                AddDn("debuffClear", lootRoom, e =>
                {
                    e.ActorId = sup.Id;
                    e.TargetId = tar.Id;
                    e.ClearCount = 1;
                });
                t += RndRange(rnd, 2, 6);
            }

            var lootLines = BuildLootEntries(rnd, dungeonPick, items);
            AddDn("loot", lootRoom, e => e.LootItems = lootLines);
            if (charById.Count > 0)
                DistributeLootToParty(charById, roster, lootLines, items, rnd);
            t += RndRange(rnd, 5, 14);

            var artifactName = PickArtifactName(rnd, dungeonPick, items);
            if (!string.IsNullOrEmpty(artifactName))
            {
                AddDn("artifact", "발굴물 보관함", e =>
                {
                    e.ItemName = artifactName;
                    e.ItemCount = 1;
                });
                if (charById.Count > 0)
                {
                    var recv = roster[rnd.Next(roster.Count)];
                    if (charById.TryGetValue(recv.Id, out var ach))
                    {
                        var ait = items.FirstOrDefault(i => i.ItemName.Equals(artifactName, StringComparison.OrdinalIgnoreCase));
                        AddInventoryCount(ach, artifactName, ait?.ItemType ?? "Etc", 1);
                    }
                }
                t += RndRange(rnd, 6, 14);
            }

            if (rnd.NextDouble() < 0.5 && TryPickTwoDistinct(roster, rnd, out var a, out var b))
            {
                var potion = invContext != null && charById.TryGetValue(a.Id, out var fromProbe)
                    ? PickPotionFromInventory(fromProbe, items, rnd, preferMana: rnd.NextDouble() < 0.4)
                    : PickPotionName(rnd, items, preferMana: rnd.NextDouble() < 0.4);
                if (!string.IsNullOrEmpty(potion))
                {
                    var canXfer = invContext == null ||
                        (charById.TryGetValue(a.Id, out var fromC) && charById.TryGetValue(b.Id, out var toC) &&
                         TryRemoveInventoryOne(fromC, potion));
                    if (canXfer)
                    {
                        AddDn("itemTransfer", lootRoom, e =>
                        {
                            e.FromId = a.Id;
                            e.ToId = b.Id;
                            e.ItemName = potion;
                            e.ItemCount = 1;
                        });
                        if (invContext != null && charById.TryGetValue(a.Id, out var fc) &&
                            charById.TryGetValue(b.Id, out var tc))
                        {
                            var pit = items.FirstOrDefault(i => i.ItemName.Equals(potion, StringComparison.OrdinalIgnoreCase));
                            AddInventoryCount(tc, potion, pit?.ItemType ?? "Etc", 1);
                        }
                        t += RndRange(rnd, 2, 6);
                    }
                }
            }

            if (rnd.NextDouble() < 0.65 && Healer() is { } checker)
            {
                AddDn("partyCheck", lootRoom, e =>
                {
                    e.ActorId = checker.Id;
                    e.CheckType = rnd.NextDouble() < 0.5 ? "HpMp" : "PotionCount";
                });
                t += RndRange(rnd, 2, 8);
            }

            if (rnd.NextDouble() < 0.45)
            {
                AddDn("inventorySort", lootRoom, e => e.ActorId = Pick().Id);
                t += RndRange(rnd, 2, 6);
            }

            // 보스: 풀 중 난이도 높은 개체 1체 (클리어 직전)
            if (rnd.NextDouble() < 0.38 && monsterPool.Count > 0)
            {
                var boss = monsterPool.OrderByDescending(m => DangerRank(m.DangerLevel)).First();
                var front = Pick();
                var hb = front.Hp;
                var loss = rnd.Next(12, 28);
                front.Hp = Math.Max(1, front.Hp - loss);
                AddDn("combat", "심층 제단", e =>
                {
                    e.ActorId = front.Id;
                    e.Enemies = new List<EnemyEntry> { new() { Name = boss.MonsterName, Count = 1 } };
                    e.Turns = rnd.Next(4, 9);
                    e.BlockCount = rnd.Next(0, 2);
                    e.HpBefore = hb;
                    e.HpAfter = front.Hp;
                });
                t += RndRange(rnd, 14, 30);
            }

            var avgHp = roster.Average(m => (double)m.Hp / m.HpMax);
            if (avgHp < 0.28 && rnd.NextDouble() < 0.25)
                outcome = "retreat";
            else if (avgHp < 0.15)
                outcome = rnd.NextDouble() < 0.45 ? "fail" : "retreat";
        }

        Add(new ActionLogEntry
        {
            Type = "Dungeon",
            PartyMembers = new List<string>(partyDisplay),
            DungeonName = dungeonName,
            FloorOrZone = floor,
            EventType = "outcome",
            TimeOffsetSeconds = Math.Round(t, 1),
            Outcome = outcome
        });

        // --- 4) 복귀 ---
        AddBase("income", mainHall);
        if (outcome == "clear")
        {
            AddBase("meal", cafeteria);
            if (rnd.NextDouble() < 0.5)
            {
                Add(new ActionLogEntry
                {
                    Type = "Base",
                    PartyMembers = TalkPairKr(rnd, roster),
                    EventType = "talk",
                    TimeOffsetSeconds = 0,
                    Location = rnd.NextDouble() < 0.5 ? cafeteria : mainHall,
                    Dialogue = new List<string>
                    {
                        TalkClosingA[rnd.Next(TalkClosingA.Length)],
                        TalkClosingB[rnd.Next(TalkClosingB.Length)]
                    }
                });
            }
        }
        else if (outcome == "retreat")
        {
            AddBase("meal", cafeteria);
            Add(new ActionLogEntry
            {
                Type = "Base",
                PartyMembers = new List<string>(partyDisplay),
                EventType = "talk",
                TimeOffsetSeconds = 0,
                Location = mainHall,
                Dialogue = new List<string>
                {
                    "이번엔 선을 넘지 않는 편이 좋겠어.",
                    "부상자부터 치료하자. 길드장 보고는 내가 할게."
                }
            });
        }
        else
        {
            Add(new ActionLogEntry
            {
                Type = "Base",
                PartyMembers = new List<string>(partyDisplay),
                EventType = "talk",
                TimeOffsetSeconds = 0,
                Location = mainHall,
                Dialogue = new List<string>
                {
                    "전멸 직전이었어. 다음엔 보급을 늘리자.",
                    "…일단 살아 돌아왔다. 그것부터 말하자."
                }
            });
        }

        foreach (var m in roster)
        {
            if (!charById.TryGetValue(m.Id, out var c)) continue;
            c.Stats ??= new CharacterStats();
            c.Stats.MaxHP = m.HpMax;
            c.Stats.MaxMP = m.MpMax;
            c.Stats.CurrentHP = Math.Clamp(m.Hp, 0, m.HpMax);
            c.Stats.CurrentMP = Math.Clamp(m.Mp, 0, m.MpMax);
            // 복귀 후 아지트 메인홀(로그 마지막 Base와 동일 키)
            c.CurrentLocationId = mainHall;
            c.CurrentLocationNote = null;
        }

        foreach (var c in workingChars)
        {
            if (string.IsNullOrWhiteSpace(c.Id)) continue;
            c.RecentMemorableEvent = ExpeditionRecentMemorableSummarizer.SummarizeLineForCharacter(c.Id, c.Name, log);
        }

        return new DungeonSimulationResult
        {
            Timeline = new TestDataRoot { ActionLog = log },
            CharactersAfterRun = MergeRunResultsIntoFullRoster(data.Characters, workingChars)
        };
    }

    /// <summary>
    /// 파티만 원정한 경우에도 CharactersDatabase 전체를 유지하고, 참가자만 갱신된 스냅샷으로 덮어씀.
    /// </summary>
    private static List<Character> MergeRunResultsIntoFullRoster(
        IReadOnlyList<Character> originalFull,
        List<Character> mutatedParticipated)
    {
        if (originalFull == null || originalFull.Count == 0)
            return CloneCharacterRoster(mutatedParticipated);

        var opts = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        };

        var full = CloneCharacterRoster(originalFull);
        var byUpdated = mutatedParticipated
            .Where(c => !string.IsNullOrWhiteSpace(c.Id))
            .ToDictionary(c => c.Id, c => c, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < full.Count; i++)
        {
            var id = full[i].Id;
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!byUpdated.TryGetValue(id, out var updated)) continue;
            var json = JsonSerializer.Serialize(updated, opts);
            var copy = JsonSerializer.Deserialize<Character>(json, opts);
            if (copy != null)
                full[i] = copy;
        }

        return full;
    }

    private static void ApplyRecovery(
        Random rnd,
        List<Member> roster,
        Func<Member?> healer,
        Func<Func<Member, bool>?, Member> pick,
        IReadOnlyList<ItemData> items,
        IReadOnlyDictionary<string, Character>? charById,
        Action<string, string, Action<ActionLogEntry>?> addDn,
        ref double t,
        string loc)
    {
        var wounded = roster.Where(m => m.Hp < m.HpMax * 0.88).ToList();
        if (wounded.Count == 0) return;

        var h = healer();
        if (h != null && h.Mp >= 12 && rnd.NextDouble() < 0.72)
        {
            var tar = wounded[rnd.Next(wounded.Count)];
            var before = tar.Hp;
            var amt = rnd.Next(14, 32);
            tar.Hp = Math.Min(tar.HpMax, tar.Hp + amt);
            h.Mp = Math.Max(0, h.Mp - rnd.Next(10, 20));
            addDn("heal", loc, e =>
            {
                e.ActorId = h.Id;
                e.TargetId = tar.Id;
                e.HpBefore = before;
                e.HpAfter = tar.Hp;
            });
            t += RndRange(rnd, 3, 10);
            wounded = roster.Where(m => m.Hp < m.HpMax * 0.75).ToList();
        }

        if (wounded.Count == 0) return;
        if (rnd.NextDouble() > 0.55) return;

        var user = pick(m => wounded.Contains(m));
        string? pot;
        if (charById != null && charById.TryGetValue(user.Id, out var uch))
        {
            pot = PickPotionFromInventory(uch, items, rnd, preferMana: false);
            if (string.IsNullOrEmpty(pot) || !TryRemoveInventoryOne(uch, pot)) return;
        }
        else
        {
            pot = PickPotionName(rnd, items, preferMana: false);
            if (string.IsNullOrEmpty(pot)) return;
        }

        var hpB = user.Hp;
        user.Hp = Math.Min(user.HpMax, user.Hp + 35);
        addDn("consumePotion", loc, e =>
        {
            e.ActorId = user.Id;
            e.ItemName = pot;
            e.ItemCount = 1;
            e.HpBefore = hpB;
            e.HpAfter = user.Hp;
        });
        t += RndRange(rnd, 2, 6);
    }

    private static bool TryPickTwoDistinct(List<Member> roster, Random rnd, out Member a, out Member b)
    {
        a = roster[rnd.Next(roster.Count)];
        var aId = a.Id;
        var others = roster.Where(m => m.Id != aId).ToList();
        if (others.Count == 0)
        {
            b = a;
            return false;
        }
        b = others[rnd.Next(others.Count)];
        return true;
    }

    private static List<LootEntry> BuildLootEntries(Random rnd, DungeonData? dung, IReadOnlyList<ItemData> items)
    {
        var list = new List<LootEntry>();
        var known = dung?.KnownRewards?.Where(r => items.Any(i => i.ItemName.Equals(r, StringComparison.OrdinalIgnoreCase))).ToList()
                    ?? new List<string>();
        if (known.Count > 0)
        {
            var name = known[rnd.Next(known.Count)];
            list.Add(new LootEntry { ItemName = name, Count = rnd.Next(1, 3) });
        }
        var lowVal = items.Where(i => i.Value > 0 && i.Value < 500 && i.ItemType.Equals("Etc", StringComparison.OrdinalIgnoreCase)).ToList();
        if (lowVal.Count > 0 && rnd.NextDouble() < 0.65)
            list.Add(new LootEntry { ItemName = lowVal[rnd.Next(lowVal.Count)].ItemName, Count = rnd.Next(1, 4) });
        if (list.Count == 0 && items.Count > 0)
            list.Add(new LootEntry { ItemName = items[rnd.Next(items.Count)].ItemName, Count = rnd.Next(1, 3) });
        return list;
    }

    private static string? PickArtifactName(Random rnd, DungeonData? dung, IReadOnlyList<ItemData> items)
    {
        if (rnd.NextDouble() > 0.45) return null;
        var candidates = dung?.KnownRewards?
            .Where(r => r.Contains("룬", StringComparison.OrdinalIgnoreCase) || r.Contains("유물", StringComparison.OrdinalIgnoreCase))
            .Where(r => items.Any(i => i.ItemName.Equals(r, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (candidates is { Count: > 0 })
            return candidates[rnd.Next(candidates.Count)];
        var rare = items.Where(i => i.Rarity.Contains("희귀", StringComparison.OrdinalIgnoreCase) || i.Value >= 500).ToList();
        return rare.Count > 0 ? rare[rnd.Next(rare.Count)].ItemName : null;
    }

    private static string? PickPotionName(Random rnd, IReadOnlyList<ItemData> items, bool preferMana)
    {
        var potions = items.Where(i =>
            i.ItemName.Contains("포션", StringComparison.OrdinalIgnoreCase) ||
            i.Effects.Contains("HP", StringComparison.OrdinalIgnoreCase) ||
            i.Effects.Contains("MP", StringComparison.OrdinalIgnoreCase)).ToList();
        if (potions.Count == 0) return null;
        if (preferMana)
        {
            var m = potions.FirstOrDefault(p => p.ItemName.Contains("마나", StringComparison.OrdinalIgnoreCase));
            if (m != null) return m.ItemName;
        }
        else
        {
            var h = potions.FirstOrDefault(p => p.ItemName.Contains("회복", StringComparison.OrdinalIgnoreCase));
            if (h != null) return h.ItemName;
        }
        return potions[rnd.Next(potions.Count)].ItemName;
    }

    private static List<EnemyEntry> RollEnemyPack(Random rnd, List<MonsterData> pool, DungeonData? dung)
    {
        if (pool.Count == 0)
            return new List<EnemyEntry> { new() { Name = "스켈레톤", Count = 1 } };

        var nTypes = rnd.Next(1, Math.Min(3, pool.Count + 1));
        if (nTypes < 1) nTypes = 1;
        var pack = new List<EnemyEntry>();
        var bag = pool.OrderBy(_ => rnd.Next()).Take(nTypes).ToList();
        foreach (var m in bag)
        {
            var maxC = DangerRank(m.DangerLevel) >= 3 ? 2 : 4;
            var c = rnd.Next(1, maxC);
            pack.Add(new EnemyEntry { Name = m.MonsterName, Count = c });
        }
        return pack;
    }

    /// <summary>
    /// SimulationPartyId가 있으면 해당 파티 멤버만(0명이면 master 제외 전원으로 폴백).
    /// </summary>
    private static List<Character> FilterCharactersForSimulation(IReadOnlyList<Character> characters, string? simulationPartyId)
    {
        if (characters == null || characters.Count == 0) return new List<Character>();

        var noMaster = characters
            .Where(c => !c.Id.Equals("master", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (string.IsNullOrWhiteSpace(simulationPartyId))
            return noMaster;

        var party = noMaster
            .Where(c =>
                !string.IsNullOrWhiteSpace(c.PartyId) &&
                c.PartyId.Equals(simulationPartyId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return party.Count > 0 ? party : noMaster;
    }

    private static List<Member> BuildRosterFromCharacters(IReadOnlyList<Character> characters)
    {
        var list = new List<Member>();
        foreach (var c in characters)
        {
            if (string.IsNullOrWhiteSpace(c.Id)) continue;
            if (c.Id.Equals("master", StringComparison.OrdinalIgnoreCase)) continue;
            var hpMax = c.Stats?.MaxHP > 0 ? c.Stats.MaxHP : 100;
            var mpMax = c.Stats?.MaxMP > 0 ? c.Stats.MaxMP : 50;
            var hp = c.Stats?.CurrentHP > 0 ? c.Stats.CurrentHP : hpMax;
            var mp = c.Stats?.CurrentMP > 0 ? c.Stats.CurrentMP : Math.Min(mpMax, 45);
            list.Add(new Member(c.Id, c.Name, hp, hpMax, mp, mpMax));
        }
        return list;
    }

    private static List<Member> DefaultRoster() => new()
    {
        new Member("kyle", "카일", 100, 100, 45, 50),
        new Member("rina", "리나", 95, 100, 70, 80),
        new Member("bram", "브람", 100, 100, 40, 52)
    };

    private static List<MonsterData> BuildMonsterPool(IReadOnlyList<MonsterData> all, DungeonData? dung)
    {
        if (all.Count == 0) return new List<MonsterData>();
        var byName = all.ToDictionary(m => m.MonsterName, m => m, StringComparer.OrdinalIgnoreCase);
        var pool = new List<MonsterData>();
        if (dung?.TypicalMonsters != null)
        {
            foreach (var n in dung.TypicalMonsters)
            {
                if (byName.TryGetValue(n, out var m)) pool.Add(m);
            }
        }
        if (pool.Count == 0)
        {
            var target = DangerRank(dung?.Difficulty);
            pool = all.Where(m => Math.Abs(DangerRank(m.DangerLevel) - target) <= 1).ToList();
            if (pool.Count == 0) pool = all.ToList();
        }
        return pool.Distinct().ToList();
    }

    private static DungeonData? PickDungeon(WorldLore? lore, Random rnd)
    {
        var list = lore?.Dungeons;
        if (list == null || list.Count == 0) return null;
        return list[rnd.Next(list.Count)];
    }

    private static string FloorForDifficulty(string? diff, Random rnd)
    {
        var d = diff?.Trim().ToLowerInvariant() ?? "low";
        if (d.Contains("심") || d.Contains("abyss")) return rnd.NextDouble() < 0.5 ? "B2" : "B1";
        if (d.Contains("high") || d.Contains("상")) return rnd.Next(3, 6).ToString();
        if (d.Contains("mid") || d.Contains("중")) return rnd.Next(2, 4).ToString();
        return rnd.Next(1, 3).ToString();
    }

    private static int DangerRank(string? level)
    {
        var s = level?.Trim().ToLowerInvariant() ?? "";
        if (s.Contains("abyss") || s.Contains("심연")) return 5;
        if (s.Contains("high") || s.Contains("상")) return 4;
        if (s.Contains("mid") || s.Contains("중")) return 3;
        if (s.Contains("low") || s.Contains("하") || s.Contains("초")) return 2;
        return 2;
    }

    private static int TrapDamage(string? trapDanger, Random rnd)
    {
        var r = DangerRank(trapDanger);
        return r switch
        {
            >= 4 => rnd.Next(16, 32),
            3 => rnd.Next(12, 24),
            _ => rnd.Next(6, 18)
        };
    }

    private static bool ShouldAbortRun(Random rnd, List<Member> roster)
    {
        var minHpFrac = roster.Min(m => (double)m.Hp / m.HpMax);
        if (minHpFrac < 0.1) return rnd.NextDouble() < 0.88;
        if (minHpFrac < 0.18) return rnd.NextDouble() < 0.32;
        return false;
    }

    private static string ResolveBaseId(IReadOnlyList<BaseFacilityData> bases, string fallback, params string[] hints)
    {
        foreach (var h in hints)
        {
            var exact = bases.FirstOrDefault(b => b.BaseId.Equals(h, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact.BaseId;
        }
        foreach (var h in hints)
        {
            var fuzzy = bases.FirstOrDefault(b =>
                b.BaseId.Contains(h, StringComparison.OrdinalIgnoreCase) ||
                b.Name.Contains(h, StringComparison.OrdinalIgnoreCase));
            if (fuzzy != null) return fuzzy.BaseId;
        }
        return fallback;
    }

    private static List<string> TalkPairKr(Random rnd, List<Member> roster)
    {
        var a = roster[rnd.Next(roster.Count)];
        var others = roster.Where(m => m.Id != a.Id).ToList();
        var b = others[rnd.Next(others.Count)];
        return new List<string> { a.Kr, b.Kr };
    }

    private static List<Character> CloneCharacterRoster(IReadOnlyList<Character> src)
    {
        var opts = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        };
        var json = JsonSerializer.Serialize(src.ToList(), opts);
        return JsonSerializer.Deserialize<List<Character>>(json, opts) ?? new List<Character>();
    }

    private static void DistributeLootToParty(
        Dictionary<string, Character> charById,
        List<Member> roster,
        List<LootEntry> loot,
        IReadOnlyList<ItemData> items,
        Random rnd)
    {
        foreach (var entry in loot)
        {
            var recipient = roster[rnd.Next(roster.Count)];
            if (!charById.TryGetValue(recipient.Id, out var ch)) continue;
            var it = items.FirstOrDefault(i => i.ItemName.Equals(entry.ItemName, StringComparison.OrdinalIgnoreCase));
            AddInventoryCount(ch, entry.ItemName, it?.ItemType ?? "Etc", entry.Count);
        }
    }

    private static void AddInventoryCount(Character c, string itemName, string itemType, int count)
    {
        if (count <= 0 || string.IsNullOrWhiteSpace(itemName)) return;
        var ent = c.Inventory.FirstOrDefault(i => i.ItemName.Equals(itemName, StringComparison.OrdinalIgnoreCase));
        if (ent != null)
            ent.Count += count;
        else
            c.Inventory.Add(new InventoryEntry { ItemName = itemName, ItemType = itemType, Count = count });
    }

    private static bool TryRemoveInventoryOne(Character c, string itemName)
    {
        var ent = c.Inventory.FirstOrDefault(i =>
            i.ItemName.Equals(itemName, StringComparison.OrdinalIgnoreCase) && i.Count > 0);
        if (ent == null) return false;
        ent.Count--;
        if (ent.Count <= 0)
            c.Inventory.Remove(ent);
        return true;
    }

    private static string? PickPotionFromInventory(Character ch, IReadOnlyList<ItemData> db, Random rnd, bool preferMana)
    {
        bool LooksPotion(InventoryEntry i) =>
            i.Count > 0 && (i.ItemName.Contains("포션", StringComparison.OrdinalIgnoreCase) ||
                            db.Any(d => d.ItemName.Equals(i.ItemName, StringComparison.OrdinalIgnoreCase) &&
                                        (d.Effects.Contains("HP", StringComparison.OrdinalIgnoreCase) ||
                                         d.Effects.Contains("MP", StringComparison.OrdinalIgnoreCase))));

        var inv = ch.Inventory.Where(LooksPotion).ToList();
        if (inv.Count == 0) return null;
        if (preferMana)
        {
            var m = inv.FirstOrDefault(i => i.ItemName.Contains("마나", StringComparison.OrdinalIgnoreCase));
            if (m != null) return m.ItemName;
        }
        else
        {
            var h = inv.FirstOrDefault(i => i.ItemName.Contains("회복", StringComparison.OrdinalIgnoreCase));
            if (h != null) return h.ItemName;
        }

        return inv[rnd.Next(inv.Count)].ItemName;
    }

    private static int MixSeed() =>
        unchecked(Environment.TickCount
                 ^ (int)(DateTime.UtcNow.Ticks & 0xFFFFFFFF)
                 ^ Guid.NewGuid().GetHashCode());

    private static int RndRange(Random r, int a, int b) => r.Next(Math.Min(a, b), Math.Max(a, b) + 1);

    public static string SerializeToJson(TestDataRoot root)
    {
        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        return JsonSerializer.Serialize(root, opts);
    }
}
