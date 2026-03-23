import React, { useState, useCallback, useEffect, useRef, useMemo } from 'react';
import { GameProvider, useGame } from './context/GameContext';
import { motion, AnimatePresence } from 'framer-motion';
import {
  MessageCircle,
  Swords,
  UserPlus,
  LayoutGrid,
  ChevronLeft,
  Loader2,
  Send,
  Trash2,
  Check,
  Users,
  Sparkles,
  UserCheck,
  RefreshCw,
  Settings,
  BookOpen,
} from 'lucide-react';
import { apiGet, apiPost, apiPut, apiDelete, apiPostSpectatorStream } from './api';
import CharacterCardsSection from './components/CharacterCardsSection';
import ActionLogSection from './components/ActionLogSection';
import WorldSettingsPanel from './components/WorldSettingsPanel';
import ModelWarmupGate from './components/ModelWarmupGate';
import './index.css';

const HUB_DEBUG_STORAGE_KEY = 'guildDialogueHubDebug';
/** 아지트 스펙테이터 트랜스크립트 캐시 (홈 재진입 시 재사용) */
const AGIT_CACHE_KEY = 'guildDialogueHubAgitTranscript';
/** 원정 완료 후 다음 홈 진입 시 아지트만 새로 생성 */
const AGIT_INVALIDATE_KEY = 'guildDialogueHubAgitInvalidateAfterExpedition';

/** `[화자] 대사` 형태만 파싱. 시스템/구분선 등은 null */
function parseBracketDialogueLine(line) {
  const m = String(line ?? '').trim().match(/^\[([^\]]+)\]\s*([\s\S]*)$/);
  if (!m) return null;
  const speaker = m[1].trim();
  const text = m[2].trim();
  if (speaker === '시스템') return null;
  return { speaker, text };
}

/** 디버그 꺼짐: 대화 말풍선용 턴만 */
function extractDialogueTurns(transcript) {
  const turns = [];
  for (const line of transcript || []) {
    const p = parseBracketDialogueLine(line);
    if (p && p.text) turns.push(p);
  }
  return turns;
}

/** 첫 두 화자를 좌/우에 매핑 (1:1 채팅과 비슷한 말풍선 배치) */
function speakerSideMap(turns) {
  const seen = [];
  for (const t of turns) {
    if (!seen.includes(t.speaker)) seen.push(t.speaker);
  }
  const map = {};
  if (seen[0]) map[seen[0]] = 'left';
  if (seen[1]) map[seen[1]] = 'right';
  return map;
}

/** `[대화 쌍] …` 한 줄 추출 (요약 표시용) */
function extractPairSummaryLine(transcript) {
  for (const line of transcript || []) {
    const s = String(line).trim();
    if (s.startsWith('[대화 쌍]')) return s.replace(/^\[대화 쌍\]\s*/, '');
  }
  return null;
}

function AgitTranscriptView({ transcript, hubDebug, agitBusy }) {
  const turns = useMemo(() => extractDialogueTurns(transcript || []), [transcript]);
  const pairHint = useMemo(() => extractPairSummaryLine(transcript || []), [transcript]);
  const sides = useMemo(() => speakerSideMap(turns), [turns]);

  if (hubDebug) {
    return (
      <div className="hub-transcript hub-agit-transcript">
        {(transcript || []).map((line, i) => (
          <pre key={i}>{line}</pre>
        ))}
      </div>
    );
  }

  return (
    <div className="hub-agit-transcript hub-agit-bubbles-wrap">
      {pairHint && (
        <p className="hub-agit-pair-hint">
          <strong>대화</strong> · {pairHint}
        </p>
      )}
      {turns.length === 0 && (transcript || []).length > 0 && (
        <p className="hub-muted hub-agit-empty-dlg">대화 한 줄을 찾지 못했습니다. 디버그 ON으로 원문을 확인하세요.</p>
      )}
      <div className="hub-bubble-list">
        {turns.map((t, i) => {
          const side = sides[t.speaker] || (i % 2 === 0 ? 'left' : 'right');
          return (
            <motion.div
              key={`${t.speaker}-${i}-${t.text?.slice(0, 24)}`}
              className={`hub-bubble-row hub-bubble-row--${side}`}
              initial={{ opacity: 0, y: 8 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.2 }}
            >
              <div className={`hub-bubble hub-bubble--${side}`}>
                <span className="hub-bubble-name">{t.speaker}</span>
                <p className="hub-bubble-text">{t.text}</p>
              </div>
            </motion.div>
          );
        })}
        {agitBusy && (
          <div className="hub-agit-thinking-row" aria-live="polite">
            <div className="hub-agit-thinking-chip">
              <Loader2 size={16} className="spin" aria-hidden />
              <span>다음 줄 생성 중…</span>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

/** 메인에서 자동 실행되는 아지트(콘솔 메뉴 1)는 카드에 넣지 않음. 선택 메뉴는 1:1부터 1번. */
const MENU = [
  { id: 'guild', n: 1, title: '길드장 집무실', desc: '1:1 대화 · Ollama', icon: MessageCircle },
  { id: 'party', n: 2, title: '파티 편성', desc: 'PartyDatabase.json', icon: LayoutGrid },
  { id: 'expedition', n: 3, title: '원정 보내기', desc: '던전 시뮬 → ActionLog·캐릭터', icon: Swords },
  { id: 'create', n: 4, title: '캐릭터 생성', desc: '직업·스킬 + Ollama 서사', icon: UserPlus },
  { id: 'guildReset', n: 5, title: '길드 초기화', desc: 'ActionLog·캐릭터·파티 전부 삭제', icon: RefreshCw },
];

function HubApp() {
  const { configDirectory, characters, parties, jobs, logs, loading, error, refreshState } =
    useGame();
  const [mode, setMode] = useState('home');
  const [busy, setBusy] = useState(false);
  const [toast, setToast] = useState(null);
  const [agitTranscript, setAgitTranscript] = useState(() => {
    try {
      const raw = localStorage.getItem(AGIT_CACHE_KEY);
      if (raw) {
        const parsed = JSON.parse(raw);
        if (Array.isArray(parsed)) return parsed;
      }
    } catch {
      /* ignore */
    }
    return [];
  });
  const [agitBusy, setAgitBusy] = useState(false);
  const [agitError, setAgitError] = useState(null);
  const agitRunKey = useRef(0);
  const [worldOverview, setWorldOverview] = useState(null);
  const [hubDebug, setHubDebug] = useState(() => {
    try {
      return localStorage.getItem(HUB_DEBUG_STORAGE_KEY) === '1';
    } catch {
      return false;
    }
  });

  const toggleHubDebug = useCallback(() => {
    setHubDebug((v) => {
      const next = !v;
      try {
        localStorage.setItem(HUB_DEBUG_STORAGE_KEY, next ? '1' : '0');
      } catch {
        /* ignore */
      }
      return next;
    });
  }, []);

  const showToast = useCallback((msg, isErr) => {
    setToast({ msg, isErr });
    setTimeout(() => setToast(null), 5000);
  }, []);

  useEffect(() => {
    if (mode !== 'home') return undefined;
    let cancelled = false;
    (async () => {
      try {
        const d = await apiGet('/api/world/overview');
        if (!cancelled) setWorldOverview(d);
      } catch {
        if (!cancelled) setWorldOverview(null);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [mode]);

  const refreshHubData = useCallback(async () => {
    await refreshState();
    try {
      const d = await apiGet('/api/world/overview');
      setWorldOverview(d);
    } catch {
      setWorldOverview(null);
    }
  }, [refreshState]);

  const clearAgitLocalCache = useCallback(() => {
    try {
      localStorage.removeItem(AGIT_CACHE_KEY);
      localStorage.removeItem(AGIT_INVALIDATE_KEY);
    } catch {
      /* ignore */
    }
    setAgitTranscript([]);
  }, []);

  const selectable = (characters || []).filter(
    (c) => c.id && !String(c.id).toLowerCase().includes('master')
  );

  const worldName = worldOverview?.worldName ?? worldOverview?.WorldName ?? '';
  const worldSummary = worldOverview?.worldSummary ?? worldOverview?.WorldSummary ?? '';
  const guildInfoBlurb = worldOverview?.guildInfo ?? worldOverview?.GuildInfo ?? '';
  const loreTeasers = worldOverview?.teaserLines ?? worldOverview?.TeaserLines ?? [];
  const hasWorldLoreContent =
    Boolean(String(worldName).trim()) ||
    Boolean(String(worldSummary).trim()) ||
    Boolean(String(guildInfoBlurb).trim()) ||
    (Array.isArray(loreTeasers) && loreTeasers.length > 0);

  /**
   * 홈: 캐시가 있고 던전 직후 플래그가 없으면 init/스트림 생략.
   * 새로 생성: (1) 캐시 없음 — 첫 방문 (2) invalidate — 원정 성공 후
   */
  useEffect(() => {
    if (mode !== 'home') return;
    const runId = ++agitRunKey.current;
    let cancelled = false;

    let shouldRegenerate = true;
    try {
      let invalidate = false;
      try {
        invalidate = localStorage.getItem(AGIT_INVALIDATE_KEY) === '1';
      } catch {
        /* ignore */
      }
      let cached = [];
      try {
        const raw = localStorage.getItem(AGIT_CACHE_KEY);
        if (raw) cached = JSON.parse(raw);
        if (!Array.isArray(cached)) cached = [];
      } catch {
        cached = [];
      }
      shouldRegenerate = !cached.length || invalidate;
    } catch {
      shouldRegenerate = true;
    }

    if (!shouldRegenerate) {
      (async () => {
        try {
          let cached = [];
          try {
            const raw = localStorage.getItem(AGIT_CACHE_KEY);
            if (raw) cached = JSON.parse(raw);
            if (!Array.isArray(cached)) cached = [];
          } catch {
            cached = [];
          }
          if (!cancelled && runId === agitRunKey.current) {
            setAgitTranscript(cached);
            setAgitError(null);
            setAgitBusy(false);
            await refreshState();
          }
        } catch (e) {
          if (!cancelled && runId === agitRunKey.current) {
            setAgitError(e.message || String(e));
          }
        }
      })();
      return () => {
        cancelled = true;
      };
    }

    (async () => {
      setAgitBusy(true);
      setAgitError(null);
      setAgitTranscript([]);
      const collected = [];
      try {
        await apiPost('/api/dialogue/init');
        if (cancelled || runId !== agitRunKey.current) return;
        await apiPostSpectatorStream((line) => {
          if (cancelled || runId !== agitRunKey.current) return;
          collected.push(line);
          setAgitTranscript((prev) => [...prev, line]);
        });
        if (cancelled || runId !== agitRunKey.current) return;
        try {
          localStorage.setItem(AGIT_CACHE_KEY, JSON.stringify(collected));
          localStorage.removeItem(AGIT_INVALIDATE_KEY);
        } catch {
          /* ignore */
        }
        refreshState();
      } catch (e) {
        if (!cancelled && runId === agitRunKey.current) {
          setAgitError(e.message || String(e));
          showToast(e.message || '아지트 실행 실패', true);
        }
      } finally {
        if (!cancelled && runId === agitRunKey.current) setAgitBusy(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [mode, refreshState, showToast]);

  return (
    <div className="game-container hub-root">
      <div className="hub-overlay" />
      <header className="hub-header glass-panel">
        <div>
          <h1 className="title-text" style={{ fontSize: '1.25rem' }}>
            GuildDialogue Hub
          </h1>
          <p className="hub-sub">
            Config: <code>{configDirectory || '…'}</code>
          </p>
        </div>
        <div className="hub-header-actions">
          <button
            type="button"
            className={`hud-button ${hubDebug ? 'hub-debug-on' : ''}`}
            onClick={toggleHubDebug}
            title="켜면 시스템 로그·활동 배정 줄까지 전부 표시합니다."
          >
            디버그 {hubDebug ? 'ON' : 'OFF'}
          </button>
          <button
            type="button"
            className="hud-button hub-settings-btn"
            onClick={() => setMode('worldSettings')}
            title="Config JSON을 단계 순서대로 편집합니다 (스킬→직업→…→세계관)."
            aria-label="세계관 설정"
          >
            <Settings size={18} strokeWidth={2} aria-hidden />
          </button>
          <button type="button" className="hud-button" onClick={() => void refreshHubData()} disabled={loading}>
            {loading ? <Loader2 size={16} className="spin" /> : null} 새로고침
          </button>
        </div>
      </header>

      {error && (
        <div className="hub-banner err">
          <strong>상태 로드 실패</strong> — Hub API가 실행 중인지 확인하세요:{' '}
          <code>dotnet run -- --hub-api</code> (기본 http://127.0.0.1:5050). Vite는 /api 를 프록시합니다.
        </div>
      )}
      {toast && (
        <div className={`hub-banner ${toast.isErr ? 'err' : 'ok'}`}>{toast.msg}</div>
      )}

      <main className="hub-main">
        <AnimatePresence mode="wait">
          {mode === 'home' && (
            <motion.div
              key="home"
              initial={{ opacity: 0, y: 12 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -8 }}
              className="hub-home-stack"
            >
              <section className="hub-section glass-panel hub-section--world-lore">
                <div className="hub-section-head">
                  <BookOpen size={22} className="hub-world-lore-icon" aria-hidden />
                  <h3 className="hub-section-title title-text">세계관</h3>
                </div>
                {hasWorldLoreContent ? (
                  <div className="hub-world-lore-content">
                    {String(worldName).trim() ? (
                      <h2 className="hub-world-lore-name title-text">{worldName.trim()}</h2>
                    ) : null}
                    {String(worldSummary).trim() ? (
                      <p className="hub-world-lore-prose">{worldSummary.trim()}</p>
                    ) : null}
                    {String(guildInfoBlurb).trim() ? (
                      <aside className="hub-world-lore-aside">
                        <span className="hub-world-lore-aside-label">길드</span>
                        <p className="hub-world-lore-aside-text">{guildInfoBlurb.trim()}</p>
                      </aside>
                    ) : null}
                    {Array.isArray(loreTeasers) && loreTeasers.length > 0 ? (
                      <ul className="hub-world-lore-bullets">
                        {loreTeasers.map((line, i) => (
                          <li key={i}>{String(line).trim()}</li>
                        ))}
                      </ul>
                    ) : null}
                  </div>
                ) : (
                  <p className="hub-world-lore-fallback">
                    이 허브에서의 모험은 <strong>Config의 WorldLore</strong>를 무대로 합니다. 상단 톱니의{' '}
                    <strong>세계관 설정</strong>에서 세계 이름·요약·길드·던전을 채우면, 이곳에 이야기처럼
                    펼쳐집니다. 아래 메뉴로 집무실 대화·파티·원정·동료 영입을 이어 가세요.
                  </p>
                )}
              </section>

              <section className="hub-section glass-panel hub-section--menu">
                <div className="hub-section-head">
                  <span className="hub-step-badge">1</span>
                  <h3 className="hub-section-title title-text">기능 선택</h3>
                </div>
                <p className="hub-muted hub-section-lead">
                  길드장 집무실·파티·원정·캐릭터 생성·길드 초기화로 이동합니다.
                </p>
                <div className="hub-grid">
                  {MENU.map((m) => (
                    <button
                      key={m.id}
                      type="button"
                      className="hub-card glass-panel"
                      onClick={() => setMode(m.id)}
                      disabled={agitBusy}
                    >
                      <div className="hub-card-n">{m.n}</div>
                      <m.icon size={28} className="hub-card-icon" />
                      <h2>{m.title}</h2>
                      <p>{m.desc}</p>
                    </button>
                  ))}
                </div>
              </section>

              <section className="hub-section hub-agit glass-panel">
                <div className="hub-section-head">
                  <span className="hub-step-badge">2</span>
                  <h3 className="hub-agit-title title-text" style={{ fontSize: '1.05rem' }}>
                    아지트 현황 <span className="hub-muted hub-agit-badge">자동 · 콘솔 메뉴 1</span>
                  </h3>
                </div>
                <p className="hub-muted hub-agit-lead">
                  BaseDatabase 기반 활동을 동료에게 무작위 배정합니다. 조건에 맞으면 선택된 <strong>2명</strong>이
                  총 <strong>2~4발화</strong>로 잠깐 대화합니다 (Ollama).
                </p>
                {agitBusy && (
                  <p className="hub-agit-status">
                    <Loader2 size={18} className="spin" /> 임베딩·활동 실행 중… (시간이 걸릴 수 있습니다)
                  </p>
                )}
                {agitError && !agitBusy && (
                  <div className="hub-banner err hub-agit-err">{agitError}</div>
                )}
                <AgitTranscriptView transcript={agitTranscript} hubDebug={hubDebug} agitBusy={agitBusy} />
              </section>

              <section className="hub-section glass-panel hub-section--roster">
                <div className="hub-section-head">
                  <span className="hub-step-badge">3</span>
                  <h3 className="hub-section-title title-text">캐릭터 카드</h3>
                </div>
                <p className="hub-muted hub-section-lead">
                  CharactersDatabase에 등록된 동료의 스탯·위치·소지품 요약입니다.
                </p>
                <CharacterCardsSection characters={characters} parties={parties} />
              </section>

              {hubDebug && (
                <section className="hub-section glass-panel hub-section--actionlog">
                  <div className="hub-section-head">
                    <span className="hub-step-badge">4</span>
                    <h3 className="hub-section-title title-text">ActionLog 현황</h3>
                  </div>
                  <p className="hub-muted hub-section-lead">
                    디버그 ON일 때만 표시됩니다. 최근 타임라인 꼬리 — 원정 후 새로고침으로 갱신됩니다.
                  </p>
                  <ActionLogSection logs={logs} maxItems={28} />
                </section>
              )}
            </motion.div>
          )}

          {mode === 'guild' && (
            <GuildMasterPanel
              key="gm"
              onBack={() => setMode('home')}
              selectable={selectable}
              parties={parties}
              busy={busy}
              setBusy={setBusy}
              showToast={showToast}
            />
          )}
          {mode === 'party' && (
            <PartyPanel
              key="pty"
              onBack={() => setMode('home')}
              parties={parties}
              roster={selectable}
              refreshState={refreshState}
              showToast={showToast}
            />
          )}
          {mode === 'expedition' && (
            <ExpeditionPanel
              key="ex"
              onBack={() => setMode('home')}
              parties={parties}
              showToast={showToast}
              refreshState={refreshState}
            />
          )}
          {mode === 'create' && (
            <CreateCharacterPanel
              key="cr"
              onBack={() => setMode('home')}
              jobs={jobs}
              parties={parties}
              showToast={showToast}
              refreshState={refreshState}
            />
          )}
          {mode === 'guildReset' && (
            <GuildResetPanel
              key="gr"
              onBack={() => setMode('home')}
              showToast={showToast}
              refreshState={refreshState}
              clearAgitLocalCache={clearAgitLocalCache}
            />
          )}
          {mode === 'worldSettings' && (
            <WorldSettingsPanel
              key="world"
              onBack={() => setMode('home')}
              showToast={showToast}
              refreshState={refreshState}
            />
          )}
        </AnimatePresence>

      </main>
    </div>
  );
}

function gmPartyLabel(parties, partyId) {
  if (!partyId || !parties?.length) return null;
  const p = parties.find(
    (x) => (x.partyId || x.PartyId || '').toLowerCase() === String(partyId).toLowerCase()
  );
  return p ? p.name || p.Name || partyId : partyId;
}

function GuildMasterPanel({ onBack, selectable, parties, busy, setBusy, showToast }) {
  const [ready, setReady] = useState(false);
  const [session, setSession] = useState(false);
  const [bootError, setBootError] = useState(false);
  const [starting, setStarting] = useState(false);
  const [buddyId, setBuddyId] = useState('');
  const [input, setInput] = useState('');
  const [lines, setLines] = useState([]);
  const [savingExit, setSavingExit] = useState(false);

  const hasUserMessages = useMemo(() => lines.some((m) => m.role === 'user'), [lines]);

  const roster = selectable || [];

  const startConversation = async () => {
    if (starting || ready) return;
    setStarting(true);
    setBootError(false);
    setBusy(true);
    try {
      await apiPost('/api/dialogue/init');
      await apiPost('/api/dialogue/guild-master/begin');
      setReady(true);
      setSession(true);
      setLines([]);
      const first = roster[0];
      const id = first ? first.Id || first.id : '';
      if (id) {
        setBuddyId(id);
        await apiPost('/api/dialogue/guild-master/switch-buddy', { buddyId: id });
      }
      showToast('집무실 세션·임베딩 준비됨');
    } catch (e) {
      setBootError(true);
      showToast(e.message, true);
    } finally {
      setStarting(false);
      setBusy(false);
    }
  };

  const selectBuddy = async (id) => {
    if (!ready || savingExit || starting || !id || id === buddyId) return;
    setBuddyId(id);
    setBusy(true);
    try {
      await apiPost('/api/dialogue/guild-master/switch-buddy', { buddyId: id });
      showToast(`대화 상대 변경 · 맥락 초기화 (${id})`);
    } catch (e) {
      showToast(e.message, true);
    } finally {
      setBusy(false);
    }
  };

  const send = async () => {
    if (!input.trim() || !buddyId) return;
    setBusy(true);
    const userLine = input.trim();
    setInput('');
    setLines((prev) => [...prev, { role: 'user', text: userLine }]);
    try {
      const r = await apiPost('/api/dialogue/guild-master/message', {
        buddyId,
        message: userLine,
      });
      if (r.line) setLines((prev) => [...prev, { role: 'buddy', text: r.line }]);
      else showToast(r.error || '응답 없음', true);
    } catch (e) {
      showToast(e.message, true);
    } finally {
      setBusy(false);
    }
  };

  /**
   * 메인: 사용자 발화가 있을 때만 종료 API(저장) 호출. 없으면 저장 없이 나감.
   * 저장 중에는 savingExit · 버튼에 '저장 중' 표시.
   */
  const leaveToMain = async () => {
    if (savingExit) return;
    if (ready && session && hasUserMessages) {
      setSavingExit(true);
      try {
        await apiPost('/api/dialogue/guild-master/end');
        showToast('집무실 종료 · ActionLog·관계 정산 저장됨.');
        onBack();
      } catch (e) {
        showToast(e.message, true);
      } finally {
        setSavingExit(false);
      }
      return;
    }
    onBack();
  };

  return (
    <motion.div
      className="hub-panel glass-panel wide hub-gm-panel"
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.22 }}
    >
      <button
        type="button"
        className="hud-button hub-back"
        onClick={leaveToMain}
        disabled={busy || savingExit || starting}
        aria-busy={savingExit}
      >
        {savingExit ? (
          <>
            <Loader2 size={18} className="spin" aria-hidden /> 저장 중…
          </>
        ) : (
          <>
            <ChevronLeft size={18} /> 메인
          </>
        )}
      </button>

      <div className="hub-gm-header">
        <p className="hub-gm-kicker">길드 마스터 · 1:1 면담</p>
        <h2 className="title-text hub-gm-title">1 · 길드장 집무실</h2>
        <p className="hub-muted hub-gm-lead">
          콘솔 메뉴 2와 동일 파이프라인 (가드레일·원정 턴·MCP 팩트 포함).{' '}
          <strong className="hub-gm-lead-strong">대화 시작</strong>을 눌러야 세션·임베딩이 준비됩니다.{' '}
          <strong className="hub-gm-lead-strong">길드장으로 한 번이라도 말한 뒤</strong> 메인으로 나가면 대화가
          저장·정산됩니다. 대화가 없으면 저장 없이 나갑니다.
        </p>
      </div>

      {!ready && (
        <div className="hub-gm-start-block glass-panel">
          {starting ? (
            <div className="hub-gm-status hub-gm-status--loading">
              <Loader2 size={18} className="spin" aria-hidden />
              <span>세션·임베딩 준비 중…</span>
            </div>
          ) : (
            <>
              <p className="hub-gm-start-lead">아래 버튼으로 대화 파이프라인(임베딩·집무실 세션)을 시작합니다.</p>
              <button
                type="button"
                className="hud-button primary hub-gm-start-btn"
                onClick={startConversation}
                disabled={busy || roster.length === 0}
              >
                <MessageCircle size={18} /> 대화 시작
              </button>
              {roster.length === 0 && (
                <p className="hub-muted hub-gm-start-warn">동료가 없으면 시작할 수 없습니다.</p>
              )}
            </>
          )}
        </div>
      )}

      <div className="hub-gm-status-bar">
        {!ready && !starting && !bootError && (
          <div className="hub-gm-status hub-gm-status--idle">대화 시작 전 · 세션·임베딩 대기</div>
        )}
        {ready && !savingExit && (
          <div className="hub-gm-status hub-gm-status--ok">
            <Sparkles size={16} aria-hidden />
            <span>세션 활성 · 임베딩 반영됨</span>
          </div>
        )}
        {ready && savingExit && (
          <div className="hub-gm-status hub-gm-status--saving">
            <Loader2 size={16} className="spin" aria-hidden />
            <span>대화 저장·관계 정산 중…</span>
          </div>
        )}
        {ready && !hasUserMessages && !savingExit && (
          <div className="hub-gm-status hub-gm-status--hint">아직 저장할 대화 없음 · 메인은 바로 나가기</div>
        )}
        {ready && hasUserMessages && !savingExit && (
          <div className="hub-gm-status hub-gm-status--dirty">대화 기록 있음 · 메인에서 저장됨</div>
        )}
        {bootError && (
          <div className="hub-gm-status hub-gm-status--err">세션 시작에 실패했습니다. 메인으로 돌아가 다시 시도하세요.</div>
        )}
      </div>

      <section className="hub-gm-section" aria-labelledby="gm-buddy-heading">
        <header className="hub-gm-section-head">
          <span className="hub-gm-section-icon" aria-hidden>
            <Users size={20} strokeWidth={2} />
          </span>
          <div>
            <h3 id="gm-buddy-heading" className="hub-gm-section-title">
              대화할 동료
            </h3>
            <p className="hub-gm-section-sub">
              {ready
                ? '카드를 눌러 상대를 바꿉니다 (맥락 초기화)'
                : '대화 시작 후 선택·대화가 가능합니다'}
            </p>
          </div>
        </header>

        {roster.length === 0 ? (
          <p className="hub-muted">동료 캐릭터가 없습니다. CharactersDatabase를 확인하세요.</p>
        ) : (
          <div className="hub-gm-buddy-row" role="listbox" aria-label="동료 선택">
            {roster.map((c) => {
              const id = c.Id || c.id;
              const name = c.Name || c.name || id;
              const role = c.Role || c.role || '';
              const age = c.Age ?? c.age;
              const pid = c.PartyId || c.partyId;
              const partyName = gmPartyLabel(parties, pid);
              const initial = String(name).charAt(0) || '?';
              const isSel = buddyId === id;
              return (
                <button
                  key={id}
                  type="button"
                  role="option"
                  aria-selected={isSel}
                  disabled={!ready || savingExit || starting}
                  className={`hub-gm-buddy-card ${isSel ? 'hub-gm-buddy-card--selected' : ''}`}
                  onClick={() => selectBuddy(id)}
                >
                  <div className="hub-gm-buddy-avatar" aria-hidden>
                    {initial}
                  </div>
                  <div className="hub-gm-buddy-text">
                    <span className="hub-gm-buddy-name">{name}</span>
                    {role ? <span className="hub-gm-buddy-role">{role}</span> : null}
                    {age != null ? <span className="hub-gm-buddy-age">나이 {age}</span> : null}
                    {partyName ? <span className="hub-gm-buddy-party">{partyName}</span> : null}
                    <code className="hub-gm-buddy-id">{id}</code>
                  </div>
                  {isSel && (
                    <span className="hub-gm-buddy-check" aria-hidden>
                      <Check size={14} />
                    </span>
                  )}
                </button>
              );
            })}
          </div>
        )}
      </section>

      <div className="hub-gm-chat-wrap">
        <div className="hub-chat hub-gm-chat">
          {lines.length === 0 && !ready && (
            <p className="hub-gm-chat-placeholder">대화 시작 후 메시지를 보낼 수 있습니다.</p>
          )}
          {lines.length === 0 && ready && (
            <p className="hub-gm-chat-placeholder">길드장으로 첫 마디를 남겨 보세요.</p>
          )}
          {lines.map((m, i) => (
            <div key={i} className={`hub-msg ${m.role}`}>
              {m.text}
            </div>
          ))}
        </div>
      </div>

      <div className="hub-input-row hub-gm-input-row">
        <input
          className="rpg-input"
          placeholder={ready ? '길드장으로 말하기…' : '세션 준비 중…'}
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && send()}
          disabled={!ready || busy || savingExit || starting || !buddyId}
        />
        <button
          type="button"
          className="send-btn hub-gm-send"
          onClick={send}
          disabled={!ready || busy || savingExit || starting || !buddyId}
        >
          <Send size={20} />
        </button>
      </div>

      {savingExit && (
        <div className="hub-gm-saving-overlay" aria-hidden>
          <p className="hub-gm-saving-overlay-text">저장 중…</p>
        </div>
      )}
    </motion.div>
  );
}

function PartyPanel({ onBack, parties, roster, refreshState, showToast }) {
  const [name, setName] = useState('');
  const [memberIds, setMemberIds] = useState([]);

  const partyList = parties || [];
  const rosterList = roster || [];
  const nameOk = String(name).trim().length > 0;

  const toggleMember = (id) => {
    setMemberIds((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    );
  };

  const resolveMemberLabel = (mid) => {
    const char = rosterList.find((c) => (c.Id || c.id) === mid);
    if (char) return char.Name || char.name || mid;
    return mid;
  };

  const create = async () => {
    if (!nameOk) {
      showToast('파티 표시 이름을 입력하세요.', true);
      return;
    }
    try {
      await apiPost('/api/parties', {
        name: name.trim(),
        memberIds,
      });
      showToast('파티가 등록되었습니다.');
      setName('');
      setMemberIds([]);
      refreshState();
    } catch (e) {
      showToast(e.message, true);
    }
  };

  const remove = async (pid) => {
    if (!window.confirm('이 파티를 삭제할까요?')) return;
    try {
      await apiDelete(`/api/parties/${encodeURIComponent(pid)}`);
      showToast('파티를 삭제했습니다.');
      refreshState();
    } catch (e) {
      showToast(e.message, true);
    }
  };

  return (
    <motion.div
      className="hub-panel glass-panel wide hub-party-page"
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.25 }}
    >
      <button type="button" className="hud-button hub-back" onClick={onBack}>
        <ChevronLeft size={18} /> 메인
      </button>

      <header className="hub-party-hero">
        <div className="hub-party-hero-icon" aria-hidden>
          <LayoutGrid size={26} strokeWidth={2} />
        </div>
        <div className="hub-party-hero-text">
          <p className="hub-party-hero-kicker">메뉴 2</p>
          <h2 className="title-text hub-party-hero-title">파티 편성</h2>
          <p className="hub-party-hero-lead">
            동료를 한 팀으로 묶어 원정에 보냅니다. 설정은 <code>PartyDatabase.json</code>에 저장됩니다.
          </p>
        </div>
      </header>

      <div className="hub-party-layout">
        <section className="hub-party-panel" aria-labelledby="party-list-heading">
          <div className="hub-party-panel-head">
            <Users size={18} className="hub-party-panel-head-icon" aria-hidden />
            <h3 id="party-list-heading" className="hub-party-panel-title">
              등록된 파티
            </h3>
            <span className="hub-party-count-pill">{partyList.length}</span>
          </div>

          {partyList.length === 0 ? (
            <div className="hub-party-empty">
              <p className="hub-party-empty-title">아직 파티가 없습니다</p>
              <p className="hub-muted hub-party-empty-desc">
                오른쪽에서 이름을 정하고 동료를 고른 뒤 등록하면 여기에 표시됩니다.
              </p>
            </div>
          ) : (
            <ul className="hub-party-card-list">
              {partyList.map((p) => {
                const pid = p.partyId || p.PartyId;
                const pname = p.name || p.Name || '(이름 없음)';
                const mids = p.memberIds || p.MemberIds || [];
                return (
                  <li key={pid} className="hub-party-summary-card">
                    <div className="hub-party-summary-top">
                      <div className="hub-party-summary-title-block">
                        <h4 className="hub-party-summary-name">{pname}</h4>
                        <code className="hub-party-summary-id">{pid}</code>
                      </div>
                      <button
                        type="button"
                        className="hub-party-delete"
                        onClick={() => remove(pid)}
                        title="파티 삭제"
                        aria-label={`${pname} 파티 삭제`}
                      >
                        <Trash2 size={17} strokeWidth={2} />
                      </button>
                    </div>
                    <div className="hub-party-summary-members">
                      <span className="hub-party-summary-label">멤버</span>
                      <div className="hub-party-member-chips">
                        {mids.length === 0 ? (
                          <span className="hub-party-member-chip hub-party-member-chip--empty">없음</span>
                        ) : (
                          mids.map((mid) => (
                            <span key={mid} className="hub-party-member-chip">
                              {resolveMemberLabel(mid)}
                            </span>
                          ))
                        )}
                      </div>
                    </div>
                  </li>
                );
              })}
            </ul>
          )}
        </section>

        <section className="hub-party-panel hub-party-panel--compose" aria-labelledby="party-new-heading">
          <div className="hub-party-panel-head">
            <Sparkles size={18} className="hub-party-panel-head-icon hub-party-panel-head-icon--accent" aria-hidden />
            <h3 id="party-new-heading" className="hub-party-panel-title">
              새 파티 만들기
            </h3>
          </div>

          <label className="hub-party-field">
            <span className="hub-party-field-label">표시 이름</span>
            <input
              className="rpg-input hub-party-field-input"
              placeholder="예: 제1 정찰대"
              value={name}
              onChange={(e) => setName(e.target.value)}
              autoComplete="off"
            />
          </label>
          <p className="hub-party-mini-hint">PartyId는 저장 시 서버에서 자동으로 붙습니다.</p>

          <div className="hub-party-compose-toolbar">
            <span className="hub-party-field-label">동료 선택</span>
            <span className="hub-party-selected-pill" aria-live="polite">
              {memberIds.length}명 선택
            </span>
          </div>
          <p className="hub-muted hub-party-roster-note">길드장(master) 제외한 등록 동료만 표시됩니다.</p>

          {rosterList.length === 0 ? (
            <div className="hub-party-roster-empty">
              <p>등록된 동료가 없습니다.</p>
              <p className="hub-muted">캐릭터 생성에서 동료를 만든 뒤 다시 오세요.</p>
            </div>
          ) : (
            <div className="hub-party-pick-grid hub-party-pick-grid--party" role="group" aria-label="파티에 넣을 동료">
              {rosterList.map((c) => {
                const id = c.Id || c.id;
                const displayName = c.Name || c.name || id;
                const role = c.Role || c.role;
                const selected = memberIds.includes(id);
                const initial = String(displayName).charAt(0) || '?';
                return (
                  <button
                    key={id}
                    type="button"
                    className={`hub-party-pick-card glass-panel ${selected ? 'hub-party-pick-card--selected' : ''}`}
                    onClick={() => toggleMember(id)}
                    aria-pressed={selected}
                  >
                    <div className="hub-party-pick-avatar" aria-hidden>
                      {initial}
                    </div>
                    <div className="hub-party-pick-body">
                      <span className="hub-party-pick-name">{displayName}</span>
                      <code className="hub-party-pick-id">{id}</code>
                      {role ? <span className="hub-party-pick-role">{role}</span> : null}
                    </div>
                    {selected && (
                      <span className="hub-party-pick-check" aria-hidden>
                        <Check size={16} strokeWidth={2.5} />
                      </span>
                    )}
                  </button>
                );
              })}
            </div>
          )}

          <div className="hub-party-compose-actions">
            <button
              type="button"
              className="hud-button primary hub-party-submit"
              onClick={create}
              disabled={!nameOk}
              title={!nameOk ? '표시 이름을 입력하세요' : ''}
            >
              파티 등록
            </button>
          </div>
        </section>
      </div>
    </motion.div>
  );
}

function ExpeditionPanel({ onBack, parties, showToast, refreshState }) {
  const [partyId, setPartyId] = useState('');
  const [expeditionBusy, setExpeditionBusy] = useState(false);
  const [simLines, setSimLines] = useState([]);
  const [expDungeons, setExpDungeons] = useState([]);
  const [dungeonName, setDungeonName] = useState('');
  const [floorOrdinal, setFloorOrdinal] = useState(1);
  const [expOptionsLoading, setExpOptionsLoading] = useState(false);
  const replayTimersRef = useRef([]);
  const mountedRef = useRef(true);

  const loadExpeditionOptions = useCallback(async () => {
    setExpOptionsLoading(true);
    try {
      const data = await apiGet('/api/expedition/options');
      const dungs = data.dungeons ?? data.Dungeons ?? [];
      setExpDungeons(dungs);
      if (dungs.length) {
        setDungeonName((prev) => {
          if (prev && dungs.some((d) => (d.name || d.Name) === prev)) return prev;
          return dungs[0].name || dungs[0].Name || '';
        });
      }
    } catch {
      setExpDungeons([]);
    } finally {
      setExpOptionsLoading(false);
    }
  }, []);

  useEffect(() => {
    loadExpeditionOptions();
  }, [loadExpeditionOptions]);

  useEffect(() => {
    if (!dungeonName || !expDungeons.length) return;
    const d = expDungeons.find((x) => (x.name || x.Name) === dungeonName);
    if (!d) return;
    const floors = d.floors || d.Floors || [];
    if (!floors.length) return;
    setFloorOrdinal((fo) => {
      const ordinals = floors.map((f) => f.ordinal ?? f.Ordinal).filter((n) => n != null);
      if (ordinals.includes(fo)) return fo;
      return ordinals[0] ?? 1;
    });
  }, [dungeonName, expDungeons]);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      replayTimersRef.current.forEach((id) => clearTimeout(id));
      replayTimersRef.current = [];
    };
  }, []);

  const clearReplayTimers = () => {
    replayTimersRef.current.forEach((id) => clearTimeout(id));
    replayTimersRef.current = [];
  };

  const run = async () => {
    if (!partyId) {
      showToast('파티를 선택하세요', true);
      return;
    }
    clearReplayTimers();
    setSimLines([]);
    setExpeditionBusy(true);
    try {
      const r = await apiPost('/api/expedition', {
        partyId,
        seed: null,
        syncChars: true,
        replaceActionLog: false,
        ...(dungeonName && expDungeons.length > 0 ? { dungeonName, floorOrdinal } : {}),
      });
      const lines = Array.isArray(r.simLogLines)
        ? r.simLogLines
        : Array.isArray(r.SimLogLines)
          ? r.SimLogLines
          : [];
      const pname = r.partyName ?? r.PartyName ?? '';
      const thisRun = r.thisRunEntries ?? r.ThisRunEntries ?? 0;
      const newTotal = r.newTotalEntries ?? r.NewTotalEntries ?? 0;

      const finish = () => {
        if (!mountedRef.current) return;
        showToast(`원정 완료: ${pname} · 이번 ${thisRun}건 · 전체 ${newTotal}건`);
        try {
          localStorage.setItem(AGIT_INVALIDATE_KEY, '1');
        } catch {
          /* ignore */
        }
        refreshState();
        loadExpeditionOptions();
        setExpeditionBusy(false);
      };

      if (lines.length === 0) {
        finish();
        return;
      }

      let i = 0;
      const scheduleNext = () => {
        if (!mountedRef.current) return;
        if (i >= lines.length) {
          finish();
          return;
        }
        const line = lines[i];
        i += 1;
        setSimLines((prev) => [...prev, line]);
        if (i >= lines.length) {
          finish();
          return;
        }
        const delay = 500 + Math.random() * 500;
        const tid = setTimeout(scheduleNext, delay);
        replayTimersRef.current.push(tid);
      };
      scheduleNext();
    } catch (e) {
      if (mountedRef.current) {
        showToast(e.message, true);
        setExpeditionBusy(false);
      }
    }
  };

  const list = parties || [];

  return (
    <motion.div className="hub-panel glass-panel wide" initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
      <button type="button" className="hud-button hub-back" onClick={onBack}>
        <ChevronLeft size={18} /> 메인
      </button>
      <h2 className="title-text">3 · 원정 보내기</h2>
      <p className="hub-muted">
        던전·층을 고른 뒤 원정합니다. 층은 ActionLog에서 해당 던전을 클리어한 기록이 있을 때만 한 층씩
        해금됩니다. 시드는 매번 무작위.
      </p>

      {expOptionsLoading ? (
        <p className="hub-muted hub-expedition-options-hint">
          <Loader2 size={14} className="spin" aria-hidden /> 던전 목록 불러오는 중…
        </p>
      ) : expDungeons.length > 0 ? (
        <section className="hub-expedition-targets glass-panel" aria-labelledby="exp-dungeon-heading">
          <h3 id="exp-dungeon-heading" className="hub-expedition-targets-title">
            던전·층
          </h3>
          <div className="hub-expedition-targets-grid">
            <label className="hub-expedition-field">
              <span className="hub-expedition-label">던전</span>
              <select
                className="hub-expedition-select"
                value={dungeonName}
                onChange={(e) => {
                  const v = e.target.value;
                  setDungeonName(v);
                  const d = expDungeons.find((x) => (x.name || x.Name) === v);
                  const floors = d?.floors || d?.Floors || [];
                  setFloorOrdinal(floors[0]?.ordinal ?? floors[0]?.Ordinal ?? 1);
                }}
              >
                {expDungeons.map((d) => {
                  const n = d.name || d.Name;
                  const diff = d.difficulty || d.Difficulty || '';
                  return (
                    <option key={n} value={n}>
                      {diff ? `${n} (${diff})` : n}
                    </option>
                  );
                })}
              </select>
            </label>
            <label className="hub-expedition-field">
              <span className="hub-expedition-label">층</span>
              <select
                className="hub-expedition-select"
                value={String(floorOrdinal)}
                onChange={(e) => setFloorOrdinal(Number(e.target.value))}
                disabled={!dungeonName}
              >
                {(expDungeons.find((x) => (x.name || x.Name) === dungeonName)?.floors ||
                  expDungeons.find((x) => (x.name || x.Name) === dungeonName)?.Floors ||
                  []
                ).map((f) => {
                  const ord = f.ordinal ?? f.Ordinal;
                  const lab = f.label ?? f.Label ?? String(ord);
                  return (
                    <option key={ord} value={String(ord)}>
                      {lab} ({ord}단계)
                    </option>
                  );
                })}
              </select>
            </label>
          </div>
          {dungeonName ? (
            <p className="hub-muted hub-expedition-progress-hint">
              {(() => {
                const d = expDungeons.find((x) => (x.name || x.Name) === dungeonName);
                if (!d) return null;
                const cleared = d.maxClearedOrdinal ?? d.MaxClearedOrdinal ?? 0;
                const maxSel = d.maxSelectableOrdinal ?? d.MaxSelectableOrdinal ?? 1;
                const cap = d.floorCap ?? d.FloorCap ?? '—';
                return (
                  <>
                    이 던전 클리어 기록: {cleared}층까지 · 이번에 선택 가능: {maxSel}층까지 (상한 {cap}층)
                  </>
                );
              })()}
            </p>
          ) : null}
        </section>
      ) : (
        <p className="hub-muted hub-expedition-options-hint">
          WorldLore.json에 던전이 없습니다. 던전·층 지정 없이 무작위로 진행합니다.
        </p>
      )}

      <p className="hub-muted">파티 선택</p>
      <div className="hub-party-pick-grid" role="group" aria-label="원정 파티">
        {list.length === 0 ? (
          <p className="hub-muted">등록된 파티가 없습니다. 파티 편성에서 먼저 만드세요.</p>
        ) : (
          list.map((p) => {
            const pid = p.partyId || p.PartyId;
            const pname = p.name || p.Name || pid;
            const members = (p.memberIds || p.MemberIds || []).join(', ');
            const selected = partyId === pid;
            const initial = String(pname).charAt(0) || 'P';
            return (
              <button
                key={pid}
                type="button"
                className={`hub-party-pick-card glass-panel ${selected ? 'hub-party-pick-card--selected' : ''}`}
                onClick={() => setPartyId(pid)}
                aria-pressed={selected}
              >
                <div className="hub-party-pick-avatar" aria-hidden>
                  {initial}
                </div>
                <div className="hub-party-pick-body">
                  <span className="hub-party-pick-name">{pname}</span>
                  <code className="hub-party-pick-id">{pid}</code>
                  {members ? <span className="hub-party-pick-role">멤버: {members}</span> : null}
                </div>
                {selected && (
                  <span className="hub-party-pick-check" aria-hidden>
                    <Check size={16} strokeWidth={2.5} />
                  </span>
                )}
              </button>
            );
          })
        )}
      </div>

      {simLines.length > 0 && (
        <div className="hub-exp-sim-log glass-panel" aria-live="polite">
          <div className="hub-exp-sim-log-head">이번 원정 시뮬 로그</div>
          <div className="hub-exp-sim-log-body">
            {simLines.map((line, idx) => (
              <div key={`${idx}-${line.slice(0, 48)}`} className="hub-exp-sim-line">
                {line}
              </div>
            ))}
            {expeditionBusy && (
              <div className="hub-exp-sim-pending">
                <Loader2 size={14} className="spin" aria-hidden />
                <span>다음 줄…</span>
              </div>
            )}
          </div>
        </div>
      )}

      <button type="button" className="hud-button primary" onClick={run} disabled={expeditionBusy || list.length === 0}>
        {expeditionBusy ? <Loader2 className="spin" /> : null} 원정 실행
      </button>
    </motion.div>
  );
}

const GUILD_RESET_CONFIRM_PHRASE = '길드 초기화';

function GuildResetPanel({ onBack, showToast, refreshState, clearAgitLocalCache }) {
  const [confirmText, setConfirmText] = useState('');
  const [resetting, setResetting] = useState(false);
  const canRun = confirmText.trim() === GUILD_RESET_CONFIRM_PHRASE;

  const runReset = async () => {
    if (!canRun || resetting) return;
    setResetting(true);
    try {
      await apiPost('/api/guild/reset');
      clearAgitLocalCache();
      await refreshState();
      showToast('길드 데이터가 초기화되었습니다. ActionLog·캐릭터·파티가 비었습니다.');
      setConfirmText('');
      onBack();
    } catch (e) {
      showToast(e.message, true);
    } finally {
      setResetting(false);
    }
  };

  return (
    <motion.div
      className="hub-panel glass-panel wide hub-guild-reset-panel"
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.25 }}
    >
      <button type="button" className="hud-button hub-back" onClick={onBack} disabled={resetting}>
        <ChevronLeft size={18} /> 메인
      </button>
      <div className="hub-guild-reset-header">
        <p className="hub-recruit-kicker">위험 구역</p>
        <h2 className="title-text hub-recruit-title">5 · 길드 초기화</h2>
        <p className="hub-muted hub-recruit-lead">
          ActionLog.json, CharactersDatabase(또는 Characters.json), PartyDatabase.json을 모두 비웁니다. 서버의 대화
          세션도 종료됩니다. 직업·몬스터 등 마스터 DB는 그대로입니다.
        </p>
      </div>
      <div className="hub-banner err hub-guild-reset-warn" role="alert">
        <strong>되돌릴 수 없습니다.</strong> 백업이 필요하면 Config 폴더의 JSON을 먼저 복사해 두세요.
      </div>
      <label className="hub-guild-reset-label">
        확인을 위해 아래 입력란에 <strong>{GUILD_RESET_CONFIRM_PHRASE}</strong>를 정확히 입력하세요.
        <input
          type="text"
          className="hub-input hub-guild-reset-input"
          value={confirmText}
          onChange={(e) => setConfirmText(e.target.value)}
          placeholder={GUILD_RESET_CONFIRM_PHRASE}
          disabled={resetting}
          autoComplete="off"
          spellCheck="false"
        />
      </label>
      <button
        type="button"
        className="hud-button hub-guild-reset-btn"
        onClick={runReset}
        disabled={!canRun || resetting}
      >
        {resetting ? <Loader2 size={18} className="spin" aria-hidden /> : <Trash2 size={18} aria-hidden />}
        {resetting ? '초기화 중…' : '길드 데이터 삭제'}
      </button>
    </motion.div>
  );
}

const RECRUIT_PREVIEW_COUNT = 3;

function charIdKey(c) {
  return String(c?.id ?? c?.Id ?? '');
}

function CreateCharacterPanel({ onBack, jobs, parties, showToast, refreshState }) {
  const [candidates, setCandidates] = useState([]);
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [finding, setFinding] = useState(false);
  const [findingStep, setFindingStep] = useState(0);
  const [committing, setCommitting] = useState(false);

  const jlist = jobs || [];

  const pickRandomJobIndex = () => {
    if (!jlist.length) return 1;
    return 1 + Math.floor(Math.random() * jlist.length);
  };

  const findCompanion = async () => {
    setFinding(true);
    setFindingStep(0);
    setCandidates([]);
    setSelectedIndex(0);
    try {
      const batch = [];
      const excludeIds = [];
      const excludeNames = [];
      for (let i = 0; i < RECRUIT_PREVIEW_COUNT; i++) {
        setFindingStep(i + 1);
        const r = await apiPost('/api/character/preview', {
          jobIndex: pickRandomJobIndex(),
          skillMode: 'all',
          skillIndices: null,
          excludeIds: [...excludeIds],
          excludeNames: [...excludeNames],
        });
        const c = r.character ?? r.Character;
        if (c) {
          batch.push(c);
          const id = charIdKey(c);
          const nm = String(c.name ?? c.Name ?? '').trim();
          if (id) excludeIds.push(id);
          if (nm) excludeNames.push(nm);
        }
      }
      setCandidates(batch);
      if (batch.length > 0) setSelectedIndex(0);
    } catch (e) {
      showToast(e.message, true);
    } finally {
      setFinding(false);
      setFindingStep(0);
    }
  };

  const safeSelectedIndex =
    candidates.length > 0 ? Math.min(Math.max(0, selectedIndex), candidates.length - 1) : 0;
  const selected = candidates.length > 0 ? candidates[safeSelectedIndex] : null;

  const commitRecruit = async () => {
    if (!selected) return;
    setCommitting(true);
    try {
      const r = await apiPost('/api/character/commit', { character: selected });
      const name = r.character?.name ?? r.character?.Name ?? '';
      const id = r.character?.id ?? r.character?.Id ?? '';
      showToast(`영입 완료: ${name} (${id})`);
      setCandidates([]);
      setSelectedIndex(0);
      refreshState();
    } catch (e) {
      showToast(e.message, true);
    } finally {
      setCommitting(false);
    }
  };

  const discardAndFindAnother = () => {
    setCandidates([]);
    setSelectedIndex(0);
  };

  const skills = selected ? selected.skills ?? selected.Skills ?? [] : [];
  const speech = selected ? selected.speechStyle ?? selected.SpeechStyle ?? '' : '';
  const career = selected != null ? selected.career ?? selected.Career : null;

  const showRoster = candidates.length > 0;

  return (
    <motion.div
      className="hub-panel glass-panel wide hub-recruit-panel"
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.25 }}
    >
      <button type="button" className="hud-button hub-back" onClick={onBack}>
        <ChevronLeft size={18} /> 메인
      </button>
      <div className="hub-recruit-header">
        <p className="hub-recruit-kicker">길드 접수 · 신규 동료</p>
        <h2 className="title-text hub-recruit-title">4 · 캐릭터 접수</h2>
        <p className="hub-muted hub-recruit-lead">
          Ollama로 서사를 생성합니다. 직업·스킬은 DB 규칙에 따라 무작위 직업과 허용 스킬 전부가 부여됩니다.
        </p>
      </div>

      <div className="hub-recruit-intro">
        <blockquote className="hub-recruit-wait">
          접수대에 동료가 기다리고 있습니다…
        </blockquote>

        {!showRoster && (
          <button
            type="button"
            className="hud-button primary hub-recruit-cta"
            onClick={findCompanion}
            disabled={finding || jlist.length === 0}
          >
            {finding ? <Loader2 className="spin" /> : <UserPlus size={18} />} 동료 찾기
          </button>
        )}

        {finding && findingStep > 0 && (
          <div className="hub-recruit-progress" aria-live="polite">
            <span className="hub-recruit-progress-pill">
              후보 {findingStep}/{RECRUIT_PREVIEW_COUNT}
            </span>
            <span className="hub-recruit-progress-text">Ollama 생성 중…</span>
          </div>
        )}
      </div>

      {jlist.length === 0 && <p className="hub-muted">JobDatabase.json에 직업이 없습니다.</p>}

      {showRoster && (
        <>
          <section className="hub-recruit-block" aria-labelledby="recruit-candidates-heading">
            <header className="hub-recruit-section-head">
              <span className="hub-recruit-section-icon" aria-hidden>
                <Users size={20} strokeWidth={2} />
              </span>
              <div className="hub-recruit-section-head-text">
                <h3 id="recruit-candidates-heading" className="hub-recruit-section-title">
                  동료 후보
                </h3>
                <p className="hub-recruit-section-sub">카드를 선택하면 아래에 상세 정보가 펼쳐집니다</p>
              </div>
            </header>
            <div className="hub-recruit-candidate-row" role="listbox" aria-label="동료 후보">
              {candidates.map((c, index) => {
                const id = charIdKey(c);
                const name = c.name ?? c.Name ?? id;
                const role = c.role ?? c.Role ?? '';
                const age = c.age ?? c.Age;
                const initial = String(name).charAt(0) || '?';
                const isSel = safeSelectedIndex === index;
                return (
                  <button
                    key={`recruit-slot-${index}`}
                    type="button"
                    role="option"
                    aria-selected={isSel}
                    className={`hub-recruit-candidate-card ${isSel ? 'hub-recruit-candidate-card--selected' : ''}`}
                    onClick={() => setSelectedIndex(index)}
                  >
                    <div className="hub-recruit-candidate-avatar" aria-hidden>
                      {initial}
                    </div>
                    <div className="hub-recruit-candidate-text">
                      <span className="hub-recruit-candidate-name">{name}</span>
                      {role ? <span className="hub-recruit-candidate-role">{role}</span> : null}
                      {age != null ? <span className="hub-recruit-candidate-age">나이 {age}</span> : null}
                      <code className="hub-recruit-candidate-id">{id}</code>
                    </div>
                    {isSel && <span className="hub-recruit-candidate-check" aria-hidden><Check size={14} /></span>}
                  </button>
                );
              })}
            </div>
          </section>

          {selected && (
            <motion.section
              className="hub-recruit-block hub-recruit-block--detail"
              aria-labelledby="recruit-detail-heading"
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.22 }}
            >
              <header className="hub-recruit-section-head">
                <span className="hub-recruit-section-icon hub-recruit-section-icon--accent" aria-hidden>
                  <Sparkles size={20} strokeWidth={2} />
                </span>
                <div className="hub-recruit-section-head-text">
                  <h3 id="recruit-detail-heading" className="hub-recruit-section-title">
                    상세 프로필
                  </h3>
                  <p className="hub-recruit-section-sub">영입 전 최종 확인</p>
                </div>
              </header>

              <div className="hub-recruit-detail-stack">
                <div className="hub-recruit-profile-shell glass-panel">
                  <CharacterCardsSection
                    characters={[selected]}
                    parties={parties || []}
                    backgroundMaxChars={null}
                  />
                </div>

                <div className="hub-recruit-kv-panel">
                  {career != null && career !== '' && (
                    <div className="hub-recruit-kv-row">
                      <span className="hub-recruit-kv-label">경력</span>
                      <span className="hub-recruit-kv-value">{String(career)}년</span>
                    </div>
                  )}
                  {speech ? (
                    <div className="hub-recruit-kv-row hub-recruit-kv-row--block">
                      <span className="hub-recruit-kv-label">말투</span>
                      <p className="hub-recruit-kv-value hub-recruit-kv-prose">{speech}</p>
                    </div>
                  ) : null}
                  <div className="hub-recruit-kv-row hub-recruit-kv-row--block">
                    <span className="hub-recruit-kv-label">스킬</span>
                    <p className="hub-recruit-kv-value">
                      {skills.length ? skills.join(' · ') : '—'}
                    </p>
                  </div>
                </div>
              </div>

              <div className="hub-recruit-actions">
                <button
                  type="button"
                  className="hud-button primary hub-recruit-btn-primary"
                  onClick={commitRecruit}
                  disabled={committing}
                >
                  {committing ? <Loader2 className="spin" /> : <UserCheck size={18} />} 영입하기
                </button>
                <button
                  type="button"
                  className="hud-button hub-recruit-btn-secondary"
                  onClick={discardAndFindAnother}
                  disabled={committing}
                >
                  <RefreshCw size={17} /> 다른 동료 찾기
                </button>
              </div>
            </motion.section>
          )}
        </>
      )}
    </motion.div>
  );
}

function App() {
  return (
    <GameProvider>
      <ModelWarmupGate>
        <HubApp />
      </ModelWarmupGate>
    </GameProvider>
  );
}

export default App;
