using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// 길드장 집무실: 플레이어 발화가 작전 맥락이 필요한지,
/// 난해·메타·세계관 밖 등 **비정상 패턴**인지 판별합니다.
/// </summary>
public enum GuildMasterAtypicalInputKind
{
    None,
    /// <summary>기호·난독 위주, 뜻 파악 곤란.</summary>
    Gibberish,
    /// <summary>AI/프롬프트/게임 엔진 등 메타 언급.</summary>
    MetaOrSystem,
    /// <summary>현실·밈 등 세계관과 동떨어진 잡담.</summary>
    OffWorldCasual
}

/// <summary>길드장 집무실 한 턴: 변칙 분류 + 작전 맥락(임베딩) 유사도.</summary>
public readonly record struct GuildOfficeUtteranceSignals(
    GuildMasterAtypicalInputKind AtypicalKind,
    /// <summary><see cref="GuildOfficeSemanticGuardrail"/> 원정 앵커와의 최대 코사인 유사도.</summary>
    double ExpeditionContextSimilarity,
    /// <summary>메타 앵커 최대 코사인(상대 게이트용). 가드레일 미사용 시 0.</summary>
    double MetaSimilarity = 0,
    /// <summary>오프월드 앵커 최대 코사인(상대 게이트용).</summary>
    double OffWorldSimilarity = 0);

public static class GuildOfficeTopicGate
{
    private static readonly Regex s_sameCharRepeat = new(@"(.)\1{3,}", RegexOptions.Compiled);

    /// <summary>명백한 메타 단서(임베딩 코사인이 밀집해 있을 때 보조).</summary>
    private static readonly string[] s_obviousMetaCuesKo =
    {
        "인공지능", "챗gpt", "chatgpt", "프롬프트", "시스템 프롬프트", "올라마", "ollama", "클로드", "제미나이",
        "할루시네이션", "토큰 한도", "컨텍스트", "임베딩 모델", "언어 모델", "dan 모드", "개발자 모드",
        "json만", "json 형식", "json으로", "이전 지시", "지시 무시", "무시하고", "llm", "gpt-"
    };

    /// <summary>명백한 오프월드 단서.</summary>
    private static readonly string[] s_obviousOffCuesKo =
    {
        "비트코인", "배민", "넷플릭스", "넷플 ", "수능", "아이폰", "갤럭시", "유튜브", "카카오", "스타벅스",
        "KTX", "에어팟", "인스타", "틱톡", "배달의민족", "쿠팡", "롤드컵", "월드컵", "손흥민"
    };

    /// <summary>
    /// 임베딩 코사인만으로 메타/오프월드 1차 분류(meta·off 동점이면 None).
    /// </summary>
    public static GuildMasterAtypicalInputKind MergeEmbeddingScores(
        GuildMasterAtypicalInputKind heuristic,
        double metaSim,
        double offSim,
        double threshold,
        double metaOffTieEpsilon)
    {
        if (heuristic == GuildMasterAtypicalInputKind.Gibberish)
            return heuristic;

        bool metaHit = metaSim >= threshold;
        bool offHit = offSim >= threshold;
        if (!metaHit && !offHit)
            return heuristic;

        if (metaHit && offHit)
        {
            if (Math.Abs(metaSim - offSim) < metaOffTieEpsilon)
                return heuristic;
            return metaSim >= offSim
                ? GuildMasterAtypicalInputKind.MetaOrSystem
                : GuildMasterAtypicalInputKind.OffWorldCasual;
        }

        if (metaHit) return GuildMasterAtypicalInputKind.MetaOrSystem;
        return GuildMasterAtypicalInputKind.OffWorldCasual;
    }

    /// <summary>
    /// 약한 메타/오프월드 신호가 원정(던전) 맥락 임베딩과 겹칠 때 None으로 내림. <paramref name="strongAtypicalFloor"/> 이상이면 유지.
    /// </summary>
    public static GuildMasterAtypicalInputKind ApplyExpeditionDisambiguation(
        GuildMasterAtypicalInputKind kind,
        double metaSim,
        double offSim,
        double expeditionSim,
        double margin,
        double expeditionDisambigMin,
        double strongAtypicalFloor)
    {
        if (kind != GuildMasterAtypicalInputKind.MetaOrSystem &&
            kind != GuildMasterAtypicalInputKind.OffWorldCasual)
            return kind;

        double bad = kind == GuildMasterAtypicalInputKind.MetaOrSystem ? metaSim : offSim;
        // 임베딩이 코사인 1.0에 포화돼도 meta≈exp면 ‘강한 확신’으로 보지 않음
        if (bad >= strongAtypicalFloor && bad > expeditionSim + 0.02)
            return kind;
        if (margin <= 0)
            return kind;
        if (expeditionSim < expeditionDisambigMin)
            return kind;
        if (expeditionSim >= bad - margin)
            return GuildMasterAtypicalInputKind.None;
        return kind;
    }

    /// <summary>임베딩이 None이지만 코사인이 근처이고 문자열 단서가 명백할 때 승격.</summary>
    public static GuildMasterAtypicalInputKind ApplyObviousKeywordTieBreak(
        GuildMasterAtypicalInputKind kind,
        string? utterance,
        double metaSim,
        double offSim,
        double threshold,
        bool enabled,
        double nearThresholdDelta = 0.12)
    {
        if (!enabled || kind != GuildMasterAtypicalInputKind.None ||
            string.IsNullOrWhiteSpace(utterance))
            return kind;

        double near = threshold - nearThresholdDelta;
        if (metaSim < near && offSim < near)
            return kind;

        bool m = HasObviousMetaCue(utterance);
        bool o = HasObviousOffWorldCue(utterance);
        if (m && !o) return GuildMasterAtypicalInputKind.MetaOrSystem;
        if (o && !m) return GuildMasterAtypicalInputKind.OffWorldCasual;
        if (m && o)
            return metaSim >= offSim ? GuildMasterAtypicalInputKind.MetaOrSystem : GuildMasterAtypicalInputKind.OffWorldCasual;
        return kind;
    }

    /// <summary>휴리스틱 + 임베딩 + 원정 억제 + 키워드 타이브레이크.</summary>
    public static GuildMasterAtypicalInputKind ClassifyWithEmbeddingSignals(
        GuildMasterAtypicalInputKind heuristic,
        string userInput,
        double metaSim,
        double offSim,
        double expeditionSim,
        RetrievalSettings retrieval)
    {
        double th = retrieval.GuildOfficeSemanticGuardrailThreshold;
        double tieEps = retrieval.GuildOfficeSemanticMetaOffEmbeddingTieEpsilon;

        var k = MergeEmbeddingScores(heuristic, metaSim, offSim, th, tieEps);
        k = ApplyExpeditionDisambiguation(
            k,
            metaSim,
            offSim,
            expeditionSim,
            retrieval.GuildOfficeSemanticDisambiguationMargin,
            retrieval.GuildOfficeSemanticExpeditionDisambigMin,
            retrieval.GuildOfficeSemanticStrongAtypicalFloor);
        k = ApplyObviousKeywordTieBreak(
            k,
            userInput,
            metaSim,
            offSim,
            th,
            retrieval.UseGuildOfficeObviousKeywordTieBreak);
        return k;
    }

    private static bool HasObviousMetaCue(string utterance)
    {
        var t = utterance.Trim();
        var low = t.ToLowerInvariant();
        foreach (var cue in s_obviousMetaCuesKo)
        {
            if (cue.Length <= 2 && cue.All(c => c < 128))
            {
                if (low.Contains(cue, StringComparison.Ordinal))
                    return true;
                continue;
            }

            if (t.Contains(cue, StringComparison.Ordinal))
                return true;
        }

        if (low.Contains("gpt", StringComparison.Ordinal) ||
            low.Contains("json", StringComparison.Ordinal) ||
            low.Contains("ollama", StringComparison.Ordinal))
            return true;
        return false;
    }

    private static bool HasObviousOffWorldCue(string utterance)
    {
        var t = utterance.Trim();
        var low = t.ToLowerInvariant();
        foreach (var cue in s_obviousOffCuesKo)
        {
            if (cue.All(c => c < 128))
            {
                if (low.Contains(cue, StringComparison.OrdinalIgnoreCase))
                    return true;
                continue;
            }

            if (t.Contains(cue, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    static int AtypicalPriority(GuildMasterAtypicalInputKind k) =>
        k switch
        {
            GuildMasterAtypicalInputKind.MetaOrSystem => 3,
            GuildMasterAtypicalInputKind.Gibberish => 2,
            GuildMasterAtypicalInputKind.OffWorldCasual => 1,
            _ => 0
        };

    /// <summary>LLM 라우터 힌트는 더 높은 우선순위 변칙만 반영.</summary>
    public static GuildMasterAtypicalInputKind MergeWithLlmHint(
        GuildMasterAtypicalInputKind current,
        GuildMasterAtypicalInputKind? llmKind)
    {
        if (!llmKind.HasValue || llmKind.Value == GuildMasterAtypicalInputKind.None)
            return current;
        return AtypicalPriority(llmKind.Value) > AtypicalPriority(current) ? llmKind.Value : current;
    }

    /// <summary>변칙 분류 + 원정 맥락 유사도(임베딩 1회).</summary>
    public static async Task<GuildOfficeUtteranceSignals> ResolveGuildOfficeSignalsAsync(
        string userInput,
        GuildOfficeSemanticGuardrail? guardrail,
        OllamaEmbeddingClient? embedClient,
        OllamaClient ollama,
        DialogueSettings settings,
        CancellationToken ct)
    {
        var h = ClassifyAtypicalPlayerInput(userInput);
        var retrieval = settings.Retrieval ?? new RetrievalSettings();
        double expeditionSim = 0;
        double metaSim = 0;
        double offSim = 0;

        if (retrieval.UseGuildOfficeSemanticGuardrail &&
            guardrail != null &&
            embedClient != null &&
            guardrail.IsReady)
        {
            try
            {
                var (meta, off, ex) = await guardrail.ScoreUtteranceFullAsync(userInput, embedClient, ct)
                    .ConfigureAwait(false);
                metaSim = meta;
                offSim = off;
                expeditionSim = ex;
                h = ClassifyWithEmbeddingSignals(h, userInput, meta, off, ex, retrieval);
            }
            catch
            {
                /* 임베딩 실패 시 ExpeditionSimilarity=0 → DialogueManager에서 최소 폴백 */
            }
        }

        if (retrieval.UseGuildOfficeLlmIntentRouter)
        {
            var llm = await GuildOfficeLlmIntentRouter.TryClassifyAsync(ollama, userInput, ct).ConfigureAwait(false);
            h = MergeWithLlmHint(h, llm);
        }

        return new GuildOfficeUtteranceSignals(h, expeditionSim, metaSim, offSim);
    }

    /// <summary>가드레일 꺼짐 시: 던전·원정 최소 단어만(기존 폴백).</summary>
    public static bool HasMinimalDungeonExpeditionKeywords(string? utterance)
    {
        if (string.IsNullOrWhiteSpace(utterance)) return false;
        var t = utterance.Trim();
        string[] keys = { "던전", "원정", "작전", "로그", "탐험", "탐사", "심층", "보스" };
        foreach (var k in keys)
        {
            if (t.Contains(k, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>
    /// 길드장이 작전·로그 블록을 열라는 뜻이 분명한 키워드(임베딩 포화일 때 보조).
    /// </summary>
    public static bool HasExplicitExpeditionKeywordCue(string? utterance)
    {
        if (string.IsNullOrWhiteSpace(utterance)) return false;
        var t = utterance.Trim();
        string[] keys =
        {
            "actionlog", "액션로그", "원정 로그", "던전 로그", "작전 로그",
            "던전", "원정", "작전", "탐험", "원정대", "전리품", "임무", "퀘스트",
            "보스", "전투", "루팅", "함정", "함정방", "레이드", "엘리트",
            "파티 와이프", "와이프 ", "와이프가", "와이프를", "와이프는",
            "타임어택", "리스폰", "스폰", "페이즈",
            "클리어", "딜 1", "딜1", "딜 순위", "딜머터", "딜 미터",
            "탐사", "최근 원정", "지난 원정", "이번 원정", "던전은", "던전이", "던전을", "던전에"
        };

        var low = t.ToLowerInvariant();
        foreach (var k in keys)
        {
            if (k.All(c => c < 128))
            {
                if (low.Contains(k, StringComparison.OrdinalIgnoreCase)) return true;
            }
            else if (t.Contains(k, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    /// <summary>
    /// Episodic·ActionLog·RAG를 시스템에 넣을지(두꺼운 원정 턴). 키워드 우선, 임베딩은 상대(원정-메타/오프) 게이트.
    /// </summary>
    public static bool ComputeDeepExpeditionForGuildOffice(
        string? userUtterance,
        GuildOfficeUtteranceSignals signals,
        RetrievalSettings r,
        bool embeddingGuardrailReady)
    {
        if (HasExplicitExpeditionKeywordCue(userUtterance))
            return true;
        if (!embeddingGuardrailReady)
            return HasMinimalDungeonExpeditionKeywords(userUtterance);

        double exp = signals.ExpeditionContextSimilarity;
        if (exp < r.GuildOfficeExpeditionContextThreshold)
            return false;
        if (!r.GuildOfficeExpeditionUseRelativeEmbeddingGate)
            return true;

        double maxBad = Math.Max(signals.MetaSimilarity, signals.OffWorldSimilarity);
        return exp >= maxBad + r.GuildOfficeExpeditionDeepContextLead;
    }

    /// <summary>
    /// 집무실 1:1에서 길드장이 **방을 나가려는 명령**인지(한국어·영문). 매칭되면 LLM 없이 루프 종료에 씁니다.
    /// </summary>
    public static bool IsGuildOfficeExitRoomCommand(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var t = raw.Trim();
        var low = t.ToLowerInvariant();
        var compact = string.Concat(low.Where(ch => !char.IsWhiteSpace(ch)));

        if (low is "exit" or "end" or "quit" or "x") return true;
        if (t.Equals("종료", StringComparison.Ordinal)) return true;

        string[] compactPhrases =
        {
            "나가", "나가그냥", "그냥나가", "나가기", "나갈게", "나가자", "나가줘", "나가요",
            "집무실나가", "집무실나갈게", "대화끝", "그만", "그만해", "그만하자",
            "꺼지", "꺼져", "꺼지라", "꺼지라고", "꺼져라", "저리가", "저리가라"
        };
        if (compactPhrases.Contains(compact)) return true;

        if (t.StartsWith("대화 끝", StringComparison.OrdinalIgnoreCase)) return true;
        if (t.StartsWith("집무실", StringComparison.OrdinalIgnoreCase) &&
            t.Contains("나가", StringComparison.OrdinalIgnoreCase) &&
            t.Length <= 24)
            return true;

        return false;
    }

    /// <summary>
    /// 길드장이 **짜증·소통 거부·대화 중단**을 드러낸 턴. 설교·추가 제안을 끊기 위한 프롬프트 플래그.
    /// </summary>
    public static bool LooksLikeGuildMasterFrustrationOrStop(string? utterance)
    {
        if (string.IsNullOrWhiteSpace(utterance)) return false;
        if (IsGuildOfficeExitRoomCommand(utterance)) return false;

        var t = utterance.Trim();
        var compact = string.Concat(t.ToLowerInvariant().Where(ch => !char.IsWhiteSpace(ch)));

        string[] cues =
        {
            "말이안통", "말안통", "안통하", "에휴", "짜증", "그만말", "말을말자", "말말자",
            "입닫", "시끄", "대화그만", "답답", "듣기싫", "짜증나", "화났", "열받",
            "말안통하", "말좀그만", "말을말", "말하지마", "말하지말", "시시끄",
            "시발", "씨발", "좆", "병신", "닥치", "닥쳐", "뒤질", "죽을래", "지랄"
        };

        foreach (var k in cues)
        {
            if (compact.Contains(k, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// **로컬만** 판별: 자모 나열·난독 등(메타/오프월드는 <see cref="GuildOfficeSemanticGuardrail"/>·LLM 라우터).
    /// </summary>
    public static GuildMasterAtypicalInputKind ClassifyAtypicalPlayerInput(string? guildMasterUtterance)
    {
        if (string.IsNullOrWhiteSpace(guildMasterUtterance)) return GuildMasterAtypicalInputKind.None;
        var t = guildMasterUtterance.Trim();

        if (LooksLikeJamoMash(t)) return GuildMasterAtypicalInputKind.Gibberish;
        if (LooksGibberish(t)) return GuildMasterAtypicalInputKind.Gibberish;

        return GuildMasterAtypicalInputKind.None;
    }

    private static bool LooksGibberish(string t)
    {
        if (t.Length >= 40 && LetterishRatio(t) < 0.15) return true;
        if (s_sameCharRepeat.IsMatch(t)) return true;

        var compact = t.Replace(" ", "", StringComparison.Ordinal).Replace("\t", "", StringComparison.Ordinal);
        if (compact.Length >= 2 && LetterishCount(compact) == 0) return true;

        if (compact.Length >= 6 && LetterishRatio(compact) < 0.25) return true;

        return false;
    }

    private static int LetterishCount(string s) =>
        s.Count(c => char.IsLetter(c) || IsHangulSyllable(c));

    private static double LetterishRatio(string s)
    {
        if (s.Length == 0) return 0;
        return LetterishCount(s) / (double)s.Length;
    }

    private static bool IsHangulSyllable(char c) => c is >= '\uAC00' and <= '\uD7A3';

    /// <summary>ㅁㄴㅇㄹ 등 자음·모음만 반복되는 패턴(의미 음절 거의 없음).</summary>
    public static bool LooksLikeJamoMash(string? t)
    {
        if (string.IsNullOrWhiteSpace(t)) return false;
        int jamo = 0, syll = 0;
        foreach (var c in t.Trim())
        {
            if (c is >= '\u3131' and <= '\u318E') jamo++;
            else if (IsHangulSyllable(c)) syll++;
        }

        int total = jamo + syll;
        return total >= 4 && syll <= 1 && jamo >= 3;
    }
}
