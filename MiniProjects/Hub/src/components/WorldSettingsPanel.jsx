import React, { useState, useCallback, useEffect, useMemo } from 'react';
import { motion } from 'framer-motion';
import {
  ChevronLeft,
  Loader2,
  AlertTriangle,
  Info,
  Save,
  ListChecks,
  Braces,
  LayoutList,
  Layers,
  RefreshCw,
} from 'lucide-react';
import { apiGet, apiPost, apiPut } from '../api';
import { GuidedFileEditor } from './worldConfigGuidedEditors';
import {
  WORLD_CONFIG_STEPS,
  validateBeforeSaveStep,
  findFirstGlobalCompletenessProblem,
} from './worldConfigWorkflow';

const WORLD_PRESET_STORAGE_KEY = 'hub.worldConfig.presetId';

export default function WorldSettingsPanel({ onBack, showToast, refreshState }) {
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(null);
  const [draft, setDraft] = useState({});
  const [savedSnapshot, setSavedSnapshot] = useState({});
  const [stepIndex, setStepIndex] = useState(0);
  const [activeFile, setActiveFile] = useState(null);
  const [issues, setIssues] = useState([]);
  const [validating, setValidating] = useState(false);
  const [savingStep, setSavingStep] = useState(false);
  const [savingBatchRebuild, setSavingBatchRebuild] = useState(false);
  /** 파일별: true = JSON 원문 textarea, false = 차근차근 폼 */
  const [rawJsonByFile, setRawJsonByFile] = useState({});
  const [presets, setPresets] = useState([]);
  const [appliedPresetId, setAppliedPresetId] = useState(() => {
    try {
      return typeof localStorage !== 'undefined' ? localStorage.getItem(WORLD_PRESET_STORAGE_KEY) || '' : '';
    } catch {
      return '';
    }
  });
  const [loadingPreset, setLoadingPreset] = useState(false);

  const step = WORLD_CONFIG_STEPS[stepIndex] ?? WORLD_CONFIG_STEPS[0];

  useEffect(() => {
    const first = step.files[0];
    setActiveFile(first);
  }, [stepIndex, step.files]);

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      const data = await apiGet('/api/config/world');
      const files = data.files ?? {};
      setDraft(files);
      setSavedSnapshot({ ...files });
      setIssues(data.issues ?? []);
    } catch (e) {
      setLoadError(e.message || String(e));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const data = await apiGet('/api/config/world/presets');
        if (!cancelled) setPresets(data.presets ?? []);
      } catch {
        if (!cancelled) setPresets([]);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const applyWorldPreset = useCallback(
    async (presetId) => {
      if (!presetId) {
        setAppliedPresetId('');
        try {
          localStorage.removeItem(WORLD_PRESET_STORAGE_KEY);
        } catch {
          /* ignore */
        }
        return;
      }
      const ok = window.confirm(
        '선택한 프리셋으로 편집 중인 세계관 JSON이 모두 교체됩니다. 아직 디스크에 저장되지 않습니다. 계속할까요?'
      );
      if (!ok) return;

      setLoadingPreset(true);
      try {
        const data = await apiGet(`/api/config/world/preset/${encodeURIComponent(presetId)}`);
        if (data.error) throw new Error(data.error);
        const files = data.files ?? {};
        setDraft(files);
        setIssues(data.issues ?? []);
        setAppliedPresetId(presetId);
        try {
          localStorage.setItem(WORLD_PRESET_STORAGE_KEY, presetId);
        } catch {
          /* ignore */
        }
        const label = data.label ?? presetId;
        showToast(`프리셋 적용: ${label}`);
        try {
          if (typeof refreshState === 'function') await refreshState();
        } catch {
          /* ignore */
        }
      } catch (e) {
        showToast(e.message || String(e), true);
      } finally {
        setLoadingPreset(false);
      }
    },
    [showToast, refreshState]
  );

  const dirtyFiles = useMemo(() => {
    const keys = new Set([...Object.keys(draft), ...Object.keys(savedSnapshot)]);
    const dirty = [];
    keys.forEach((k) => {
      if ((draft[k] ?? '') !== (savedSnapshot[k] ?? '')) dirty.push(k);
    });
    return dirty;
  }, [draft, savedSnapshot]);

  const setFileText = (fileName, text) => {
    setDraft((prev) => ({ ...prev, [fileName]: text }));
  };

  const pickerLists = useMemo(() => {
    try {
      const tagDb = JSON.parse(draft['TagDatabase.json'] || '[]');
      const tagNameOptions = Array.isArray(tagDb)
        ? [...new Set(tagDb.map((t) => String(t?.TagName ?? '').trim()).filter(Boolean))].sort((a, b) =>
            a.localeCompare(b, 'ko')
          )
        : [];
      const traitDb = JSON.parse(draft['MonsterTraitDatabase.json'] || '[]');
      const traitNameOptions = Array.isArray(traitDb)
        ? [...new Set(traitDb.map((t) => String(t?.TraitName ?? '').trim()).filter(Boolean))].sort((a, b) =>
            a.localeCompare(b, 'ko')
          )
        : [];
      const skills = JSON.parse(draft['SkillDatabase.json'] || '[]');
      const skillNameOptions = Array.isArray(skills)
        ? [...new Set(skills.map((s) => String(s?.SkillName ?? '').trim()).filter(Boolean))].sort((a, b) =>
            a.localeCompare(b, 'ko')
          )
        : [];
      const items = JSON.parse(draft['ItemDatabase.json'] || '[]');
      const itemNameOptions = Array.isArray(items)
        ? [...new Set(items.map((i) => String(i?.ItemName ?? '').trim()).filter(Boolean))].sort((a, b) =>
            a.localeCompare(b, 'ko')
          )
        : [];
      const monsters = JSON.parse(draft['MonsterDatabase.json'] || '[]');
      const monsterNameOptions = Array.isArray(monsters)
        ? [...new Set(monsters.map((m) => String(m?.MonsterName ?? '').trim()).filter(Boolean))].sort((a, b) =>
            a.localeCompare(b, 'ko')
          )
        : [];
      const skillTypes = JSON.parse(draft['SkillTypeDatabase.json'] || '[]');
      const skillTypeOptions = Array.isArray(skillTypes)
        ? [...new Set(skillTypes.map((r) => String(r?.TypeName ?? '').trim()).filter(Boolean))].sort((a, b) =>
            a.localeCompare(b, 'ko')
          )
        : [];
      const itemTypes = JSON.parse(draft['ItemTypeDatabase.json'] || '[]');
      const itemTypeOptions = Array.isArray(itemTypes)
        ? [...new Set(itemTypes.map((r) => String(r?.TypeName ?? '').trim()).filter(Boolean))].sort((a, b) =>
            a.localeCompare(b, 'ko')
          )
        : [];
      const rarities = JSON.parse(draft['RarityDatabase.json'] || '[]');
      const rarityOptions = Array.isArray(rarities)
        ? [...new Set(rarities.map((r) => String(r?.RarityName ?? '').trim()).filter(Boolean))].sort((a, b) =>
            a.localeCompare(b, 'ko')
          )
        : [];
      const monsterTypes = JSON.parse(draft['MonsterTypeDatabase.json'] || '[]');
      const monsterTypeOptions = Array.isArray(monsterTypes)
        ? [...new Set(monsterTypes.map((r) => String(r?.TypeName ?? '').trim()).filter(Boolean))].sort((a, b) =>
            a.localeCompare(b, 'ko')
          )
        : [];
      const dangerLevels = JSON.parse(draft['DangerLevelDatabase.json'] || '[]');
      const dangerLevelOptions = Array.isArray(dangerLevels)
        ? [...new Set(dangerLevels.map((r) => String(r?.LevelName ?? '').trim()).filter(Boolean))].sort((a, b) =>
            a.localeCompare(b, 'ko')
          )
        : [];
      const trapCats = JSON.parse(draft['TrapCategoryDatabase.json'] || '[]');
      const trapCategoryOptions = Array.isArray(trapCats)
        ? [...new Set(trapCats.map((r) => String(r?.CategoryName ?? '').trim()).filter(Boolean))].sort((a, b) =>
            a.localeCompare(b, 'ko')
          )
        : [];
      const effectRows = JSON.parse(draft['EffectSnippetDatabase.json'] || '[]');
      const effectSnippetDefs = {};
      const effectSnippetOptionsSkill = [];
      const effectSnippetOptionsItem = [];
      if (Array.isArray(effectRows)) {
        for (const e of effectRows) {
          const n = String(e?.SnippetName ?? '').trim();
          if (!n) continue;
          effectSnippetDefs[n] = e;
          const sc = String(e?.Scope ?? 'both').toLowerCase();
          if (sc === 'skill' || sc === 'both') effectSnippetOptionsSkill.push(n);
          if (sc === 'item' || sc === 'both') effectSnippetOptionsItem.push(n);
        }
        effectSnippetOptionsSkill.sort((a, b) => a.localeCompare(b, 'ko'));
        effectSnippetOptionsItem.sort((a, b) => a.localeCompare(b, 'ko'));
      }
      return {
        tagNameOptions,
        traitNameOptions,
        skillNameOptions,
        itemNameOptions,
        monsterNameOptions,
        skillTypeOptions,
        itemTypeOptions,
        rarityOptions,
        monsterTypeOptions,
        dangerLevelOptions,
        trapCategoryOptions,
        effectSnippetDefs,
        effectSnippetOptionsSkill,
        effectSnippetOptionsItem,
      };
    } catch {
      return {
        tagNameOptions: [],
        traitNameOptions: [],
        skillNameOptions: [],
        itemNameOptions: [],
        monsterNameOptions: [],
        skillTypeOptions: [],
        itemTypeOptions: [],
        rarityOptions: [],
        monsterTypeOptions: [],
        dangerLevelOptions: [],
        trapCategoryOptions: [],
        effectSnippetDefs: {},
        effectSnippetOptionsSkill: [],
        effectSnippetOptionsItem: [],
      };
    }
  }, [draft]);

  const runValidate = async () => {
    setValidating(true);
    try {
      const global = findFirstGlobalCompletenessProblem(draft);
      if (global) {
        setStepIndex(global.stepIndex);
        if (global.fileName) setActiveFile(global.fileName);
        showToast(`[채우기 감사] ${global.message}`, true);
      }
      const data = await apiPost('/api/config/world/validate', { overrides: draft });
      setIssues(data.issues ?? []);
      const err = (data.issues ?? []).filter((i) => i.severity === 'error');
      if (!global && err.length === 0) showToast('검사 통과: 오류 없음 (경고는 목록 확인)');
      else if (!global && err.length > 0) showToast(`서버 검사: 오류 ${err.length}건`, true);
    } catch (e) {
      showToast(e.message, true);
    } finally {
      setValidating(false);
    }
  };

  const saveCurrentStepOnly = async () => {
    const workDraft = { ...draft };

    const localProblem = validateBeforeSaveStep(stepIndex, workDraft);
    if (localProblem) {
      setStepIndex(localProblem.stepIndex);
      if (localProblem.fileName) setActiveFile(localProblem.fileName);
      showToast(localProblem.message, true);
      return;
    }

    const toSave = step.files.filter((f) => (workDraft[f] ?? '') !== (savedSnapshot[f] ?? ''));
    if (toSave.length === 0) {
      showToast('이 단계에서 저장할 변경이 없습니다.');
      return;
    }

    setSavingStep(true);
    try {
      for (const fileName of toSave) {
        const text = workDraft[fileName];
        await apiPut('/api/config/world/file', { fileName, content: text });
        setSavedSnapshot((prev) => ({ ...prev, [fileName]: text }));
      }
      setValidating(true);
      try {
        const data = await apiPost('/api/config/world/validate', { overrides: workDraft });
        setIssues(data.issues ?? []);
        const err = (data.issues ?? []).filter((i) => i.severity === 'error');
        if (err.length === 0) showToast(`저장 완료 (${toSave.join(', ')})`);
        else showToast(`저장됨 · 서버 검사 오류 ${err.length}건`, true);
      } finally {
        setValidating(false);
      }
      try {
        if (typeof refreshState === 'function') await refreshState();
      } catch {
        /* ignore */
      }
    } catch (e) {
      showToast(e.message || String(e), true);
    } finally {
      setSavingStep(false);
    }
  };

  const saveAllDirtyAndRebuild = async () => {
    const workDraft = { ...draft };

    const global = findFirstGlobalCompletenessProblem(workDraft);
    if (global) {
      setStepIndex(global.stepIndex);
      if (global.fileName) setActiveFile(global.fileName);
      showToast(`[채우기 감사] ${global.message}`, true);
      return;
    }

    for (let si = 0; si < WORLD_CONFIG_STEPS.length; si++) {
      const localProblem = validateBeforeSaveStep(si, workDraft);
      if (localProblem) {
        setStepIndex(localProblem.stepIndex);
        if (localProblem.fileName) setActiveFile(localProblem.fileName);
        showToast(localProblem.message, true);
        return;
      }
    }

    const toSave = [...dirtyFiles].sort((a, b) => a.localeCompare(b, 'en'));
    if (toSave.length === 0) {
      showToast('디스크에 반영할 변경이 없습니다. (이미 모두 저장됨)');
      return;
    }

    const ok = window.confirm(
      `변경된 ${toSave.length}개 파일을 모두 저장한 뒤,\n` +
        '• 길드 데이터(캐릭터·파티·행동 로그)를 초기화하고\n' +
        '• 참조 지식(RAG) 임베딩을 다시 구축합니다.\n' +
        '집무실 등 대화 세션도 리셋됩니다. 계속할까요?'
    );
    if (!ok) return;

    setSavingBatchRebuild(true);
    try {
      for (const fileName of toSave) {
        const text = workDraft[fileName];
        await apiPut('/api/config/world/file', { fileName, content: text });
        setSavedSnapshot((prev) => ({ ...prev, [fileName]: text }));
      }
      setValidating(true);
      try {
        const data = await apiPost('/api/config/world/validate', { overrides: workDraft });
        setIssues(data.issues ?? []);
        const err = (data.issues ?? []).filter((i) => i.severity === 'error');
        if (err.length > 0)
          showToast(`저장됨 · 서버 검사 오류 ${err.length}건 — 재구축은 계속합니다`, true);
      } finally {
        setValidating(false);
      }

      await apiPost('/api/guild/reset');
      await apiPost('/api/dialogue/init');

      try {
        if (typeof refreshState === 'function') await refreshState();
      } catch {
        /* ignore */
      }
      showToast(`일괄 저장·길드 초기화·RAG 재구축 완료 (${toSave.join(', ')})`);
    } catch (e) {
      showToast(e.message || String(e), true);
    } finally {
      setSavingBatchRebuild(false);
    }
  };

  const issuesForStep = useMemo(() => {
    return (issues || []).filter((i) => {
      if (i.stepId === step.id) return true;
      if (i.stepId === 'worldLore' && step.files.includes('WorldLore.json')) return true;
      if (i.stepId) return false;
      const msg = i.message || '';
      return step.files.some((f) => msg.includes(f));
    });
  }, [issues, step.id, step.files]);

  if (loading) {
    return (
      <motion.div
        className="hub-panel glass-panel wide hub-world-panel"
        initial={{ opacity: 0, y: 8 }}
        animate={{ opacity: 1, y: 0 }}
      >
        <div className="hub-world-loading">
          <Loader2 className="spin" size={28} />
          <p>세계관 Config 로드 중…</p>
        </div>
      </motion.div>
    );
  }

  if (loadError) {
    return (
      <motion.div
        className="hub-panel glass-panel wide hub-world-panel"
        initial={{ opacity: 0, y: 8 }}
        animate={{ opacity: 1, y: 0 }}
      >
        <button type="button" className="hud-button hub-back" onClick={onBack}>
          <ChevronLeft size={18} /> 메인
        </button>
        <div className="hub-banner err">로드 실패: {loadError}</div>
      </motion.div>
    );
  }

  const currentText = activeFile ? draft[activeFile] ?? '' : '';
  const showRawJson = activeFile ? !!rawJsonByFile[activeFile] : false;

  const setRawModeForActive = (raw) => {
    if (!activeFile) return;
    setRawJsonByFile((prev) => ({ ...prev, [activeFile]: raw }));
  };

  return (
    <motion.div
      className="hub-panel glass-panel wide hub-world-panel"
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.25 }}
    >
      <div className="hub-world-toolbar">
        <div className="hub-world-toolbar-left">
          <button type="button" className="hud-button hub-back" onClick={onBack}>
            <ChevronLeft size={18} /> 메인
          </button>
          <div className="hub-world-toolbar-presets" aria-label="테스트 데이터 프리셋">
            <span className="hub-world-preset-label">
              <Layers size={16} aria-hidden />
              프리셋
            </span>
            <select
              className="hub-world-preset-select"
              value={appliedPresetId}
              onChange={(e) => void applyWorldPreset(e.target.value)}
              disabled={loading || loadingPreset || savingStep || savingBatchRebuild}
              title="테스트 데이터 1~3 중 하나를 불러오면 편집 중인 세계관 파일이 한꺼번에 바뀝니다. 디스크에 반영하려면 이 단계 저장 또는 일괄 저장 및 재구축을 사용하세요."
            >
              <option value="">— 선택 —</option>
              {presets.map((p) => {
                const id = p.id ?? p.Id ?? '';
                const label = p.label ?? p.Label ?? id;
                const desc = p.description ?? p.Description ?? '';
                return (
                  <option key={id} value={id}>
                    {label}
                    {desc ? ` (${desc})` : ''}
                  </option>
                );
              })}
            </select>
            {loadingPreset ? <Loader2 size={16} className="spin hub-world-preset-spinner" aria-hidden /> : null}
          </div>
        </div>
        <div className="hub-world-toolbar-actions">
          <button
            type="button"
            className="hud-button"
            onClick={runValidate}
            disabled={validating}
            title="현재 편집 내용(저장 여부 무관)으로 전체 교차 검사"
          >
            {validating ? <Loader2 size={16} className="spin" /> : <ListChecks size={16} />}
            전체 검사
          </button>
        </div>
      </div>

      <div className="hub-world-head">
        <h2 className="title-text">세계관 설정</h2>
        <p className="hub-muted hub-world-lead">
          <strong>스킬·아이템·몬스터 등 DB를 먼저</strong> 채운 뒤, 세계·던전 단계에서 전리품·몬스터는{' '}
          <strong>목록에서 선택</strong>해 연결합니다. <strong>이 단계 저장</strong>은 현재 단계의 변경만 디스크에 씁니다.{' '}
          <strong>일괄 저장 및 재구축</strong>은 아직 저장되지 않은 모든 파일을 한 번에 저장한 뒤 길드 초기화·RAG 재구축을
          수행합니다. 빈 칸이 있으면 해당 단계로 안내합니다.
        </p>
        {dirtyFiles.length > 0 && (
          <p className="hub-world-dirty">
            저장 안 됨: <code>{dirtyFiles.join(', ')}</code>
          </p>
        )}
      </div>

      <div className="hub-world-body">
        <nav className="hub-world-steps glass-panel" aria-label="설정 단계">
          {WORLD_CONFIG_STEPS.map((s, i) => (
            <button
              key={s.id}
              type="button"
              className={`hub-world-step ${i === stepIndex ? 'hub-world-step--active' : ''}`}
              onClick={() => setStepIndex(i)}
            >
              <span className="hub-world-step-title">{s.title}</span>
            </button>
          ))}
        </nav>

        <div className="hub-world-editor">
          <p className="hub-muted hub-world-hint">{step.hint}</p>

          <div className="hub-world-edit-mode-bar glass-panel" role="toolbar" aria-label="편집 방식">
            <span className="hub-world-edit-mode-label">편집 방식</span>
            <div className="hub-world-edit-mode-toggle">
              <button
                type="button"
                className={!showRawJson ? 'hub-world-mode--active' : ''}
                onClick={() => setRawModeForActive(false)}
                disabled={!activeFile}
              >
                <LayoutList size={16} aria-hidden />
                차근차근
              </button>
              <button
                type="button"
                className={showRawJson ? 'hub-world-mode--active' : ''}
                onClick={() => setRawModeForActive(true)}
                disabled={!activeFile}
              >
                <Braces size={16} aria-hidden />
                JSON 원문
              </button>
            </div>
            <p className="hub-muted hub-world-edit-mode-hint">
              기본은 항목별 폼입니다. 전체 구조를 한 번에 붙여넣을 때는 <strong>JSON 원문</strong>을 쓰세요.
            </p>
          </div>

          {step.files.length > 1 && (
            <div className="hub-world-file-tabs" role="tablist">
              {step.files.map((f) => (
                <button
                  key={f}
                  type="button"
                  role="tab"
                  className={activeFile === f ? 'hub-world-tab--on' : ''}
                  onClick={() => setActiveFile(f)}
                >
                  {f}
                </button>
              ))}
            </div>
          )}

          <div className="hub-world-editor-surface">
            {showRawJson ? (
              <label className="hub-world-editor-label">
                {step.files.length === 1 ? step.files[0] : activeFile}
                <textarea
                  className="hub-world-textarea"
                  value={currentText}
                  onChange={(e) => activeFile && setFileText(activeFile, e.target.value)}
                  spellCheck={false}
                  disabled={!activeFile}
                />
              </label>
            ) : (
              <div className="hub-world-guided-wrap">
                {activeFile && (
                  <GuidedFileEditor
                    fileName={activeFile}
                    text={currentText}
                    onChange={(nextJson) => setFileText(activeFile, nextJson)}
                    guidedFocus={step.guidedFocus}
                    pickerLists={pickerLists}
                  />
                )}
              </div>
            )}
          </div>

          <div className="hub-world-editor-actions hub-world-editor-actions--split">
            <button
              type="button"
              className="hud-button"
              onClick={saveCurrentStepOnly}
              disabled={savingStep || savingBatchRebuild || validating}
              title="이 단계에서 변경된 JSON만 디스크에 씁니다. 길드·RAG는 그대로입니다."
            >
              {savingStep ? <Loader2 size={16} className="spin" /> : <Save size={16} />}
              이 단계 저장
            </button>
            <button
              type="button"
              className="hud-button primary"
              onClick={saveAllDirtyAndRebuild}
              disabled={savingStep || savingBatchRebuild || validating}
              title="저장 안 된 모든 파일을 저장한 뒤 길드 데이터를 비우고 RAG 임베딩을 다시 구축합니다."
            >
              {savingBatchRebuild ? <Loader2 size={16} className="spin" /> : <RefreshCw size={16} />}
              일괄 저장 및 재구축
            </button>
          </div>

          {issuesForStep.length > 0 && (
            <div className="hub-world-issues glass-panel">
              <h3 className="hub-world-issues-title">이 단계 관련 검사 결과</h3>
              <ul className="hub-world-issues-list">
                {issuesForStep.map((iss, idx) => (
                  <li
                    key={`${iss.code}-${idx}`}
                    className={iss.severity === 'error' ? 'hub-world-issue--err' : 'hub-world-issue--warn'}
                  >
                    {iss.severity === 'error' ? (
                      <AlertTriangle size={16} aria-hidden />
                    ) : (
                      <Info size={16} aria-hidden />
                    )}
                    <span>
                      <code>{iss.code}</code> {iss.message}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      </div>
    </motion.div>
  );
}
