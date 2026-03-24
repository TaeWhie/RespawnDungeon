import React, { useState, useEffect, useCallback, useRef } from 'react';
import { motion } from 'framer-motion';
import { Cpu, Loader2, AlertCircle, RefreshCw } from 'lucide-react';
import { apiPost } from '../api';

/**
 * Hub 최초 표시 전 Ollama 모델(대화·임베딩) 워밍업 — 첫 로드 시 GPU/VRAM 준비 시간을 여기서 소비합니다.
 */
export default function ModelWarmupGate({ children }) {
  const EARLY_RELEASE_MS = 8000;
  const [phase, setPhase] = useState('loading'); // loading | done | ready | error
  const [detail, setDetail] = useState(null);
  const [phases, setPhases] = useState([]);
  const [totalMs, setTotalMs] = useState(null);
  const ranRef = useRef(false);
  const doneTimerRef = useRef(null);
  const releaseTimerRef = useRef(null);
  const releasedEarlyRef = useRef(false);

  const runWarmup = useCallback(async () => {
    if (doneTimerRef.current) {
      clearTimeout(doneTimerRef.current);
      doneTimerRef.current = null;
    }
    setPhase('loading');
    setDetail(null);
    setPhases([]);
    setTotalMs(null);
    releasedEarlyRef.current = false;
    releaseTimerRef.current = window.setTimeout(() => {
      releasedEarlyRef.current = true;
      setPhase('ready');
    }, EARLY_RELEASE_MS);
    try {
      const data = await apiPost('/api/model/warmup', {
        fastStart: true,
        backgroundEmbed: true,
      });
      if (releaseTimerRef.current) {
        clearTimeout(releaseTimerRef.current);
        releaseTimerRef.current = null;
      }
      if (releasedEarlyRef.current) return;
      const list = data.phases ?? data.Phases ?? [];
      setPhases(Array.isArray(list) ? list : []);
      const ms = data.totalMs ?? data.TotalMs;
      if (ms != null) setTotalMs(ms);
      if (data.ok === false || data.error) {
        setDetail(data.error || data.message || '워밍업 실패');
        setPhase('error');
        return;
      }
      setPhase('done');
      doneTimerRef.current = window.setTimeout(() => {
        setPhase('ready');
        doneTimerRef.current = null;
      }, 1400);
    } catch (e) {
      if (releaseTimerRef.current) {
        clearTimeout(releaseTimerRef.current);
        releaseTimerRef.current = null;
      }
      if (releasedEarlyRef.current) return;
      setDetail(e.message || String(e));
      setPhase('error');
    }
  }, []);

  useEffect(() => {
    if (ranRef.current) return;
    ranRef.current = true;
    void runWarmup();
    return () => {
      if (doneTimerRef.current) clearTimeout(doneTimerRef.current);
      if (releaseTimerRef.current) clearTimeout(releaseTimerRef.current);
    };
  }, [runWarmup]);

  if (phase === 'ready') return children;

  return (
    <motion.div
        className="hub-model-warmup-overlay"
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        transition={{ duration: 0.25 }}
        role="alertdialog"
        aria-busy={phase === 'loading' || phase === 'done'}
        aria-label="모델 서버 준비"
      >
        <div className="hub-model-warmup-card glass-panel">
          <div className="hub-model-warmup-icon">
            {phase === 'loading' ? (
              <Loader2 size={40} className="spin" aria-hidden />
            ) : phase === 'done' ? (
              <Cpu size={40} aria-hidden />
            ) : (
              <AlertCircle size={40} aria-hidden />
            )}
          </div>
          <h1 className="hub-model-warmup-title">
            {phase === 'done' ? '모델 준비 완료' : '모델 서버 준비 중'}
          </h1>
          <p className="hub-model-warmup-lead">
            {phase === 'done'
              ? '잠시 후 길드 허브로 이동합니다.'
              : '대화 모델 우선 워밍업 후 메인으로 진입합니다. 임베딩 모델은 백그라운드에서 이어서 로딩됩니다.'}
          </p>
          {phase === 'done' && totalMs != null && (
            <p className="hub-model-warmup-total">
              전체 소요: <strong>{totalMs}ms</strong>
            </p>
          )}
          {phase === 'loading' && (
            <p className="hub-model-warmup-hint">
              <Cpu size={16} aria-hidden /> 먼저 <code>Ollama.Model</code>을 올리고, <code>EmbeddingModel</code>은 백그라운드로 준비합니다.
            </p>
          )}
          {phases.length > 0 && (
            <ul className="hub-model-warmup-phases">
              {phases.map((p, i) => {
                const id = p.id ?? p.Id ?? '';
                const model = p.model ?? p.Model ?? '';
                const ok = p.ok ?? p.Ok;
                const ms = p.ms ?? p.Ms ?? 0;
                const skipped = p.skipped ?? p.Skipped;
                const det = p.detail ?? p.Detail;
                return (
                  <li key={`${id}-${i}`} className={ok ? 'hub-model-warmup-phase--ok' : 'hub-model-warmup-phase--err'}>
                    <span className="hub-model-warmup-phase-id">{id}</span>
                    <code className="hub-model-warmup-phase-model">{model}</code>
                    {skipped ? (
                      <span className="hub-muted"> 생략</span>
                    ) : (
                      <span className="hub-model-warmup-phase-ms"> {ms}ms</span>
                    )}
                    {det && <span className="hub-model-warmup-phase-detail"> — {det}</span>}
                  </li>
                );
              })}
            </ul>
          )}
          {phase === 'error' && (
            <div className="hub-model-warmup-err">
              <p>{detail}</p>
              <button type="button" className="hud-button primary" onClick={() => void runWarmup()}>
                <RefreshCw size={16} /> 다시 시도
              </button>
            </div>
          )}
        </div>
      </motion.div>
  );
}
