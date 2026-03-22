import React, { useMemo, useCallback } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import {
  EFFECT_KINDS,
  EFFECT_KIND_LABELS,
  formatEffectSnippetDisplay,
  normalizeEffectSnippetRefs,
  defaultParamsFromDef,
  joinEffectSnippetRefs,
} from '../utils/effectSnippetSchema.js';

export function stringifyPretty(obj) {
  return JSON.stringify(obj, null, 2);
}

function parseJson(text) {
  try {
    return { ok: true, data: JSON.parse(text) };
  } catch (e) {
    return { ok: false, error: e.message || String(e) };
  }
}

function Field({ label, hint, children, className = '' }) {
  return (
    <label className={`hub-world-field ${className}`}>
      <span className="hub-world-field-label">{label}</span>
      {hint ? <span className="hub-world-field-hint">{hint}</span> : null}
      {children}
    </label>
  );
}

function TextInput({ value, onChange, placeholder }) {
  return (
    <input
      type="text"
      className="hub-world-input"
      value={value ?? ''}
      onChange={(e) => onChange(e.target.value)}
      placeholder={placeholder}
    />
  );
}

function TextArea({ value, onChange, rows = 3, placeholder }) {
  return (
    <textarea
      className="hub-world-input hub-world-input--multiline"
      rows={rows}
      value={value ?? ''}
      onChange={(e) => onChange(e.target.value)}
      placeholder={placeholder}
    />
  );
}

function NumberInput({ value, onChange }) {
  return (
    <input
      type="number"
      step="any"
      className="hub-world-input"
      value={Number.isFinite(Number(value)) ? value : 0}
      onChange={(e) => onChange(e.target.value === '' ? 0 : Number(e.target.value))}
    />
  );
}

/** 단일 선택 — 타입·등급·위험도 마스터 */
function SelectField({ label, hint, value, options, onChange, placeholder = '선택…' }) {
  return (
    <Field label={label} hint={hint}>
      <select className="hub-world-input hub-world-select" value={value ?? ''} onChange={(e) => onChange(e.target.value)}>
        <option value="">{placeholder}</option>
        {(options || []).map((o) => (
          <option key={o} value={o}>
            {o}
          </option>
        ))}
      </select>
    </Field>
  );
}

/** 스니펫 마스터(effectSnippetDefs) + EffectSnippetRefs 로 Effects 합성 */
function EffectSnippetRefsEditor({ label, hint, row, patch, snippetNameOptions, effectSnippetDefs }) {
  const refs = normalizeEffectSnippetRefs(row);
  const selSet = useMemo(() => new Set(refs.map((r) => r.SnippetName)), [refs]);

  const applyRefs = (nextRefs) => {
    patch({
      ...row,
      EffectSnippetRefs: nextRefs,
      EffectSnippetNames: [],
      Effects: joinEffectSnippetRefs(nextRefs, effectSnippetDefs),
    });
  };

  const toggle = (name) => {
    if (selSet.has(name)) {
      applyRefs(refs.filter((r) => r.SnippetName !== name));
    } else {
      const def = effectSnippetDefs[name];
      const defaults = defaultParamsFromDef(def);
      applyRefs([...refs, { SnippetName: name, Params: defaults }]);
    }
  };

  const setParam = (snippetName, key, value) => {
    const next = refs.map((r) => {
      if (r.SnippetName !== snippetName) return r;
      return { ...r, Params: { ...(r.Params || {}), [key]: value } };
    });
    applyRefs(next);
  };

  if (!snippetNameOptions.length) {
    return (
      <div className="hub-world-ref-block">
        <span className="hub-world-field-label">{label}</span>
        {hint ? <span className="hub-world-field-hint hub-world-field-hint--ref">{hint}</span> : null}
        <p className="hub-world-ref-empty">EffectSnippetDatabase에 스니펫이 없습니다.</p>
      </div>
    );
  }

  return (
    <div className="hub-world-ref-block">
      <span className="hub-world-field-label">{label}</span>
      {hint ? <span className="hub-world-field-hint hub-world-field-hint--ref">{hint}</span> : null}
      {refs.length > 0 && (
        <p className="hub-world-ref-summary">선택됨 · {refs.map((r) => r.SnippetName).join(' · ')}</p>
      )}
      <div className="hub-world-ref-grid" role="group">
        {snippetNameOptions.map((opt) => (
          <label key={opt} className="hub-world-ref-check">
            <input type="checkbox" checked={selSet.has(opt)} onChange={() => toggle(opt)} />
            <span>{opt}</span>
          </label>
        ))}
      </div>
      {refs.map((ref) => {
        const def = effectSnippetDefs[ref.SnippetName];
        const plist = Array.isArray(def?.Parameters) ? def.Parameters : [];
        if (plist.length === 0) return null;
        return (
          <div key={ref.SnippetName} className="hub-world-card-fields hub-world-card-fields--nested" style={{ marginTop: 12 }}>
            <span className="hub-world-field-label">{ref.SnippetName} — 파라미터</span>
            {plist.map((pd) => {
              const k = String(pd?.Key ?? '').trim();
              if (!k) return null;
              const lab = String(pd?.Label ?? k);
              const unit = pd?.Unit ? ` (${pd.Unit})` : '';
              return (
                <Field key={k} label={`${lab}${unit}`}>
                  <NumberInput
                    value={ref.Params?.[k] ?? 0}
                    onChange={(v) => setParam(ref.SnippetName, k, v)}
                  />
                </Field>
              );
            })}
          </div>
        );
      })}
    </div>
  );
}

/** 문자열 배열: 한 줄에 하나 */
function LinesToListEditor({ label, lines, onChange, hint }) {
  const text = (lines || []).join('\n');
  return (
    <Field label={label} hint={hint}>
      <TextArea
        value={text}
        onChange={(v) =>
          onChange(
            v
              .split('\n')
              .map((s) => s.trim())
              .filter(Boolean)
          )
        }
        rows={6}
        placeholder="한 줄에 하나씩"
      />
    </Field>
  );
}

/** 다른 DB에 등록된 이름만 체크해 연결 (던전 전리품/몬스터, 직업 허용 스킬 등) */
function RefPickerColumn({ label, hint, selected, options, onChange, emptyMessage }) {
  const selSet = useMemo(() => new Set(selected || []), [selected]);
  const toggle = (name) => {
    const cur = selected || [];
    if (selSet.has(name)) onChange(cur.filter((x) => x !== name));
    else onChange([...cur, name]);
  };

  const emptyText =
    emptyMessage ||
    'DB에 등록된 이름이 없습니다. 먼저 해당 단계에서 항목을 추가하세요.';

  if (!options.length) {
    return (
      <div className="hub-world-ref-block">
        <span className="hub-world-field-label">{label}</span>
        {hint ? <span className="hub-world-field-hint hub-world-field-hint--ref">{hint}</span> : null}
        <p className="hub-world-ref-empty">{emptyText}</p>
      </div>
    );
  }

  return (
    <div className="hub-world-ref-block">
      <span className="hub-world-field-label">{label}</span>
      {hint ? <span className="hub-world-field-hint hub-world-field-hint--ref">{hint}</span> : null}
      {(selected || []).length > 0 && (
        <p className="hub-world-ref-summary">선택됨 · {(selected || []).join(' · ')}</p>
      )}
      <div className="hub-world-ref-grid" role="group">
        {options.map((opt) => (
          <label key={opt} className="hub-world-ref-check">
            <input type="checkbox" checked={selSet.has(opt)} onChange={() => toggle(opt)} />
            <span>{opt}</span>
          </label>
        ))}
      </div>
    </div>
  );
}

function ArrayCardList({ title, items, onItemsChange, emptyTemplate, renderItem, addLabel = '항목 추가' }) {
  const list = Array.isArray(items) ? items : [];
  const updateAt = (i, next) => {
    const copy = [...list];
    copy[i] = next;
    onItemsChange(copy);
  };
  const removeAt = (i) => {
    onItemsChange(list.filter((_, j) => j !== i));
  };
  const add = () => onItemsChange([...list, { ...emptyTemplate }]);

  return (
    <div className="hub-world-array-section">
      <div className="hub-world-array-head">
        <h4 className="hub-world-array-title">{title}</h4>
        <button type="button" className="hud-button hub-world-icon-btn" onClick={add}>
          <Plus size={16} /> {addLabel}
        </button>
      </div>
      <div className="hub-world-array-cards">
        {list.map((row, i) => (
          <div key={i} className="hub-world-card glass-panel">
            <div className="hub-world-card-toolbar">
              <span className="hub-world-card-idx">#{i + 1}</span>
              <button type="button" className="hud-button hub-world-icon-btn danger" onClick={() => removeAt(i)} title="삭제">
                <Trash2 size={16} />
              </button>
            </div>
            {renderItem(row, (next) => updateAt(i, next))}
          </div>
        ))}
      </div>
    </div>
  );
}

/** TypeName / RarityName / LevelName / CategoryName + Description */
function LexiconTwoColEditor({ data, onChange, title, primaryKey, nameLabel, addLabel = '항목 추가' }) {
  if (!Array.isArray(data)) {
    return <p className="hub-world-guided-err">루트가 배열([])이어야 합니다.</p>;
  }
  const emptyTemplate = { [primaryKey]: '', Description: '' };
  return (
    <ArrayCardList
      title={title}
      items={data}
      onItemsChange={onChange}
      emptyTemplate={emptyTemplate}
      addLabel={addLabel}
      renderItem={(row, patch) => (
        <div className="hub-world-card-fields">
          <Field label={nameLabel} hint="마스터에 등록된 값만 스킬·아이템 등에서 선택됩니다.">
            <TextInput value={row[primaryKey] ?? ''} onChange={(v) => patch({ ...row, [primaryKey]: v })} />
          </Field>
          <Field label="Description" hint="저장 시 필수입니다.">
            <TextArea value={row.Description} onChange={(v) => patch({ ...row, Description: v })} rows={2} />
          </Field>
        </div>
      )}
    />
  );
}

function SnippetParameterDefsEditor({ parameters, onChange }) {
  const list = Array.isArray(parameters) ? parameters : [];
  const rowTpl = { Key: '', Label: '', Unit: '' };
  const updateAt = (i, next) => {
    const copy = [...list];
    copy[i] = next;
    onChange(copy);
  };
  const removeAt = (i) => onChange(list.filter((_, j) => j !== i));
  const add = () => onChange([...list, { ...rowTpl }]);

  return (
    <div className="hub-world-array-section">
      <div className="hub-world-array-head">
        <h4 className="hub-world-array-title">Parameters (템플릿 자리)</h4>
        <button type="button" className="hud-button hub-world-icon-btn" onClick={add}>
          <Plus size={16} /> 자리 추가
        </button>
      </div>
      {list.map((p, i) => (
        <div key={i} className="hub-world-field-row" style={{ alignItems: 'flex-end' }}>
          <Field label="Key" hint="템플릿과 동일한 식별자.">
            <TextInput value={p.Key ?? ''} onChange={(v) => updateAt(i, { ...p, Key: v })} />
          </Field>
          <Field label="Label">
            <TextInput value={p.Label ?? ''} onChange={(v) => updateAt(i, { ...p, Label: v })} />
          </Field>
          <Field label="Unit">
            <TextInput value={p.Unit ?? ''} onChange={(v) => updateAt(i, { ...p, Unit: v })} placeholder="HP 등" />
          </Field>
          <button type="button" className="hud-button hub-world-icon-btn danger" onClick={() => removeAt(i)} title="삭제">
            <Trash2 size={16} />
          </button>
        </div>
      ))}
    </div>
  );
}

function EffectSnippetDatabaseEditor({ data, onChange }) {
  if (!Array.isArray(data)) {
    return <p className="hub-world-guided-err">루트가 배열([])이어야 합니다.</p>;
  }

  const emptyTemplate = {
    SnippetName: '',
    Scope: 'both',
    EffectKind: 'Custom',
    Summary: '',
    SummaryTemplate: '',
    Parameters: [],
  };

  const patchKind = (row, nextKind) => {
    const base = {
      SnippetName: row.SnippetName ?? '',
      Scope: row.Scope || 'both',
      EffectKind: nextKind,
    };
    if (nextKind === 'None') return { ...base, Note: '' };
    return { ...base, Summary: '', SummaryTemplate: '', Parameters: [] };
  };

  return (
    <ArrayCardList
      title="효과 스니펫 (Effects)"
      items={data}
      onItemsChange={onChange}
      emptyTemplate={emptyTemplate}
      addLabel="스니펫 추가"
      renderItem={(row, patch) => {
        const kind = row.EffectKind || 'Custom';

        return (
          <div className="hub-world-card-fields">
            <Field
              label="SnippetName"
              hint="한글 ID. 스킬·아이템 EffectSnippetRefs.SnippetName과 같아야 합니다. 수치는 스킬/아이템 Params에서 넣습니다."
            >
              <TextInput value={row.SnippetName} onChange={(v) => patch({ ...row, SnippetName: v })} />
            </Field>
            <Field label="Scope" hint="skill=스킬만, item=아이템만, both=양쪽 선택 가능.">
              <select
                className="hub-world-input hub-world-select"
                value={row.Scope || 'both'}
                onChange={(e) => patch({ ...row, Scope: e.target.value })}
              >
                <option value="skill">skill</option>
                <option value="item">item</option>
                <option value="both">both</option>
              </select>
            </Field>
            <Field label="효과 유형" hint="Custom=요약·템플릿 · None=효과 없음(재료 등).">
              <select
                className="hub-world-input hub-world-select"
                value={kind}
                onChange={(e) => patch(patchKind(row, e.target.value))}
              >
                {EFFECT_KINDS.map((k) => (
                  <option key={k} value={k}>
                    {EFFECT_KIND_LABELS[k] || k}
                  </option>
                ))}
              </select>
            </Field>

            {kind === 'Custom' && (
              <>
                <Field
                  label="SummaryTemplate"
                  hint="예: HP를 {amount}만큼 즉시 회복. 아래 Parameters Key와 {이름}이 일치해야 합니다."
                >
                  <TextArea
                    value={row.SummaryTemplate ?? ''}
                    onChange={(v) => patch({ ...row, SummaryTemplate: v })}
                    rows={2}
                  />
                </Field>
                <SnippetParameterDefsEditor
                  parameters={row.Parameters}
                  onChange={(p) => patch({ ...row, Parameters: p })}
                />
                <Field label="Summary (고정 문장)" hint="템플릿 없이 짧은 설명만 쓸 때. 둘 다 비우면 안 됩니다.">
                  <TextArea value={row.Summary ?? ''} onChange={(v) => patch({ ...row, Summary: v })} rows={3} />
                </Field>
              </>
            )}

            {kind === 'None' && (
              <Field label="Note" hint="비어 있으면 표시용으로 «없음»만 씁니다.">
                <TextArea value={row.Note} onChange={(v) => patch({ ...row, Note: v })} rows={2} />
              </Field>
            )}

            <Field
              label="Effects 합성 미리보기"
              hint="템플릿은 스킬/아이템에서 Params 넣은 뒤 최종 문장이 됩니다. 여기서는 수치 없이 {자리}가 보일 수 있습니다."
            >
              <input
                type="text"
                readOnly
                className="hub-world-input hub-world-input--readonly"
                value={formatEffectSnippetDisplay(row)}
              />
            </Field>
          </div>
        );
      }}
    />
  );
}

/* ---------- Tag master (스킬 Tags) ---------- */
function TagDatabaseEditor({ data, onChange }) {
  if (!Array.isArray(data)) {
    return <p className="hub-world-guided-err">루트가 배열([])이어야 합니다.</p>;
  }
  return (
    <ArrayCardList
      title="스킬 태그 마스터"
      items={data}
      onItemsChange={onChange}
      emptyTemplate={{ TagName: '', Description: '' }}
      addLabel="태그 추가"
      renderItem={(row, patch) => (
        <div className="hub-world-card-fields">
          <Field
            label="TagName"
            hint="스킬 Tags에 저장되는 식별자. 영문·케밥케이스 등 팀 규칙에 맞춥니다."
          >
            <TextInput value={row.TagName} onChange={(v) => patch({ ...row, TagName: v })} />
          </Field>
          <Field label="Description" hint="태그 의미(필터·프롬프트용). 저장 시 필수입니다.">
            <TextArea value={row.Description} onChange={(v) => patch({ ...row, Description: v })} rows={2} />
          </Field>
        </div>
      )}
    />
  );
}

/* ---------- Monster trait master ---------- */
function MonsterTraitDatabaseEditor({ data, onChange }) {
  if (!Array.isArray(data)) {
    return <p className="hub-world-guided-err">루트가 배열([])이어야 합니다.</p>;
  }
  return (
    <ArrayCardList
      title="몬스터 특성 마스터"
      items={data}
      onItemsChange={onChange}
      emptyTemplate={{ TraitName: '', Description: '' }}
      addLabel="특성 추가"
      renderItem={(row, patch) => (
        <div className="hub-world-card-fields">
          <Field label="TraitName" hint="몬스터 Traits에 저장되는 문구와 동일해야 합니다.">
            <TextInput value={row.TraitName} onChange={(v) => patch({ ...row, TraitName: v })} />
          </Field>
          <Field label="Description" hint="특성 설명. 저장 시 필수입니다.">
            <TextArea value={row.Description} onChange={(v) => patch({ ...row, Description: v })} rows={2} />
          </Field>
        </div>
      )}
    />
  );
}

/* ---------- Skill ---------- */
function SkillDatabaseEditor({
  data,
  onChange,
  tagNameOptions = [],
  skillTypeOptions = [],
  effectSnippetOptionsSkill = [],
  effectSnippetDefs = {},
}) {
  if (!Array.isArray(data)) {
    return <p className="hub-world-guided-err">루트가 배열([])이어야 합니다.</p>;
  }
  return (
    <ArrayCardList
      title="스킬 목록"
      items={data}
      onItemsChange={onChange}
      emptyTemplate={{
        SkillName: '',
        SkillType: '',
        Tags: [],
        Description: '',
        Effects: '',
        EffectSnippetRefs: [],
        EffectSnippetNames: [],
        MpCost: 0,
        CooldownTurns: 0,
      }}
      renderItem={(row, patch) => (
        <div className="hub-world-card-fields">
          <Field
            label="SkillName"
            hint="다른 DB에서 참조할 때 쓰는 고유 이름입니다. 직업 AllowedSkillNames와 정확히 같아야 합니다."
          >
            <TextInput value={row.SkillName} onChange={(v) => patch({ ...row, SkillName: v })} />
          </Field>
          <SelectField
            label="SkillType"
            hint="SkillTypeDatabase에 등록된 값만 선택합니다."
            value={row.SkillType}
            options={skillTypeOptions}
            onChange={(v) => patch({ ...row, SkillType: v })}
          />
          <RefPickerColumn
            label="Tags — 스킬 태그 마스터에서 선택"
            hint="쉼표 입력 대신 태그 DB(TagName)에 등록된 항목만 체크합니다."
            selected={row.Tags || []}
            options={tagNameOptions}
            onChange={(arr) => patch({ ...row, Tags: arr })}
            emptyMessage="태그 마스터에 TagName이 없습니다. 먼저 스킬 태그 단계에서 항목을 추가하세요."
          />
          <Field label="Description" hint="플레이어·LLM에게 보여 줄 스킬 설명(세계관 톤 유지).">
            <TextArea value={row.Description} onChange={(v) => patch({ ...row, Description: v })} rows={2} />
          </Field>
          <EffectSnippetRefsEditor
            label="Effects — 효과 스니펫 마스터에서 선택"
            hint="스니펫에 Parameters가 있으면 아래에서 수치를 입력합니다. « | »로 합쳐져 Effects에 저장됩니다."
            row={row}
            patch={patch}
            snippetNameOptions={effectSnippetOptionsSkill}
            effectSnippetDefs={effectSnippetDefs}
          />
          <Field label="Effects (합쳐진 저장값)" hint="스니펫 선택으로 자동 갱신됩니다. 원문 수정은 JSON 모드에서 하세요.">
            <TextArea value={row.Effects ?? ''} readOnly rows={2} className="hub-world-input--readonly" />
          </Field>
          <div className="hub-world-field-row">
            <Field label="MpCost" hint="사용 시 소비 MP. 0이면 무비용으로 봅니다.">
              <NumberInput value={row.MpCost} onChange={(v) => patch({ ...row, MpCost: v })} />
            </Field>
            <Field label="CooldownTurns" hint="다시 쓰기까지 필요한 턴 수. 0이면 매 턴 사용 가능(다른 제한 없을 때).">
              <NumberInput value={row.CooldownTurns} onChange={(v) => patch({ ...row, CooldownTurns: v })} />
            </Field>
          </div>
        </div>
      )}
    />
  );
}

/* ---------- Job ---------- */
function JobDatabaseEditor({ data, onChange, skillNameOptions = [] }) {
  if (!Array.isArray(data)) {
    return <p className="hub-world-guided-err">루트가 배열([])이어야 합니다.</p>;
  }
  return (
    <ArrayCardList
      title="직업(Role) 목록"
      items={data}
      onItemsChange={onChange}
      emptyTemplate={{
        RoleId: '',
        DisplayName: '',
        Description: '',
        AllowedSkillNames: [],
      }}
      renderItem={(row, patch) => (
        <div className="hub-world-card-fields">
          <Field label="RoleId" hint="내부 식별자(영문·숫자 권장). 파티·저장 데이터와 맞출 때 씁니다.">
            <TextInput value={row.RoleId} onChange={(v) => patch({ ...row, RoleId: v })} />
          </Field>
          <Field label="DisplayName" hint="UI·대화에 나올 직업 표시 이름입니다.">
            <TextInput value={row.DisplayName} onChange={(v) => patch({ ...row, DisplayName: v })} />
          </Field>
          <Field label="Description" hint="이 직업의 역할·분위기·제약을 한 줄 요약합니다.">
            <TextArea value={row.Description} onChange={(v) => patch({ ...row, Description: v })} rows={2} />
          </Field>
          <RefPickerColumn
            label="AllowedSkillNames — 스킬 DB(SkillName)에서 선택"
            hint="이 직업이 배울 수 있는 스킬만 체크합니다. 목록은 스킬 단계에 등록된 SkillName입니다."
            selected={row.AllowedSkillNames || []}
            options={skillNameOptions}
            onChange={(arr) => patch({ ...row, AllowedSkillNames: arr })}
            emptyMessage="스킬 DB에 SkillName이 없습니다. 먼저 스킬 단계에서 항목을 추가하세요."
          />
        </div>
      )}
    />
  );
}

/* ---------- Item ---------- */
function ItemDatabaseEditor({
  data,
  onChange,
  itemTypeOptions = [],
  rarityOptions = [],
  effectSnippetOptionsItem = [],
  effectSnippetDefs = {},
}) {
  if (!Array.isArray(data)) {
    return <p className="hub-world-guided-err">루트가 배열([])이어야 합니다.</p>;
  }
  return (
    <ArrayCardList
      title="아이템 목록"
      items={data}
      onItemsChange={onChange}
      emptyTemplate={{
        ItemName: '',
        ItemType: '',
        Description: '',
        Effects: '',
        EffectSnippetRefs: [],
        EffectSnippetNames: [],
        Value: 0,
        Rarity: '',
      }}
      renderItem={(row, patch) => (
        <div className="hub-world-card-fields">
          <Field
            label="ItemName"
            hint="던전 KnownRewards 등에서 참조하는 고유 이름입니다. 다른 아이템과 겹치지 않게 합니다."
          >
            <TextInput value={row.ItemName} onChange={(v) => patch({ ...row, ItemName: v })} />
          </Field>
          <SelectField
            label="ItemType"
            hint="ItemTypeDatabase 마스터에서 선택합니다."
            value={row.ItemType}
            options={itemTypeOptions}
            onChange={(v) => patch({ ...row, ItemType: v })}
          />
          <Field label="Description" hint="아이템 설명. 세계관·퀘스트 대화에 쓰입니다.">
            <TextArea value={row.Description} onChange={(v) => patch({ ...row, Description: v })} rows={2} />
          </Field>
          <EffectSnippetRefsEditor
            label="Effects — 효과 스니펫 마스터에서 선택"
            hint="item·both 스니펫만 목록에 나옵니다. Parameters가 있으면 수치를 입력합니다."
            row={row}
            patch={patch}
            snippetNameOptions={effectSnippetOptionsItem}
            effectSnippetDefs={effectSnippetDefs}
          />
          <Field label="Effects (합쳐진 저장값)" hint="스니펫 선택으로 자동 갱신됩니다.">
            <TextArea value={row.Effects ?? ''} readOnly rows={2} className="hub-world-input--readonly" />
          </Field>
          <div className="hub-world-field-row">
            <Field label="Value" hint="가격·가치 감각용 숫자(골드 등과 연동할 때 참고).">
              <NumberInput value={row.Value} onChange={(v) => patch({ ...row, Value: v })} />
            </Field>
            <SelectField
              label="Rarity"
              hint="RarityDatabase 마스터에서 선택합니다."
              value={row.Rarity}
              options={rarityOptions}
              onChange={(v) => patch({ ...row, Rarity: v })}
            />
          </div>
        </div>
      )}
    />
  );
}

/* ---------- Monster ---------- */
function MonsterDatabaseEditor({
  data,
  onChange,
  traitNameOptions = [],
  monsterTypeOptions = [],
  dangerLevelOptions = [],
}) {
  if (!Array.isArray(data)) {
    return <p className="hub-world-guided-err">루트가 배열([])이어야 합니다.</p>;
  }
  return (
    <ArrayCardList
      title="몬스터 목록"
      items={data}
      onItemsChange={onChange}
      emptyTemplate={{
        MonsterId: '',
        MonsterName: '',
        Type: '',
        Description: '',
        Weakness: '',
        DangerLevel: '',
        Traits: [],
      }}
      renderItem={(row, patch) => (
        <div className="hub-world-card-fields">
          <div className="hub-world-field-row">
            <Field label="MonsterId" hint="내부 식별자. 스폰·스크립트에서 쓸 수 있는 짧은 ID.">
              <TextInput value={row.MonsterId} onChange={(v) => patch({ ...row, MonsterId: v })} />
            </Field>
            <Field
              label="MonsterName"
              hint="던전 TypicalMonsters 등에서 참조하는 표시 이름입니다. DB 내에서 고유하게 둡니다."
            >
              <TextInput value={row.MonsterName} onChange={(v) => patch({ ...row, MonsterName: v })} />
            </Field>
          </div>
          <SelectField
            label="Type"
            hint="MonsterTypeDatabase 마스터에서 선택합니다."
            value={row.Type}
            options={monsterTypeOptions}
            onChange={(v) => patch({ ...row, Type: v })}
          />
          <Field label="Description" hint="외형·행동·위협도를 묘사합니다. LLM이 장면을 그릴 때 참고합니다.">
            <TextArea value={row.Description} onChange={(v) => patch({ ...row, Description: v })} rows={2} />
          </Field>
          <div className="hub-world-field-row">
            <Field label="Weakness" hint="속성·무기 유형 등 약점 힌트(대화·전술 참고).">
              <TextInput value={row.Weakness} onChange={(v) => patch({ ...row, Weakness: v })} />
            </Field>
            <SelectField
              label="DangerLevel"
              hint="DangerLevelDatabase 마스터(Low, Mid, …)에서 선택합니다."
              value={row.DangerLevel}
              options={dangerLevelOptions}
              onChange={(v) => patch({ ...row, DangerLevel: v })}
            />
          </div>
          <RefPickerColumn
            label="Traits — 몬스터 특성 마스터에서 선택"
            hint="한 줄씩 입력 대신 특성 DB(TraitName)에 등록된 문구만 체크합니다."
            selected={row.Traits || []}
            options={traitNameOptions}
            onChange={(arr) => patch({ ...row, Traits: arr })}
            emptyMessage="몬스터 특성 마스터에 TraitName이 없습니다. 먼저 특성 마스터 단계에서 항목을 추가하세요."
          />
        </div>
      )}
    />
  );
}

/* ---------- Trap ---------- */
function TrapDatabaseEditor({ data, onChange, trapCategoryOptions = [], dangerLevelOptions = [] }) {
  if (!Array.isArray(data)) {
    return <p className="hub-world-guided-err">루트가 배열([])이어야 합니다.</p>;
  }
  return (
    <ArrayCardList
      title="함정 타입"
      items={data}
      onItemsChange={onChange}
      emptyTemplate={{
        TrapId: '',
        TrapName: '',
        Category: '',
        TriggerCondition: '',
        Effect: '',
        CounterMeasure: '',
        DangerLevel: '',
      }}
      renderItem={(row, patch) => (
        <div className="hub-world-card-fields">
          <div className="hub-world-field-row">
            <Field label="TrapId" hint="이벤트·데이터에서 구분할 짧은 코드.">
              <TextInput value={row.TrapId} onChange={(v) => patch({ ...row, TrapId: v })} />
            </Field>
            <Field label="TrapName" hint="플레이어·문서에 보일 함정 이름.">
              <TextInput value={row.TrapName} onChange={(v) => patch({ ...row, TrapName: v })} />
            </Field>
          </div>
          <SelectField
            label="Category"
            hint="TrapCategoryDatabase 마스터에서 선택합니다."
            value={row.Category}
            options={trapCategoryOptions}
            onChange={(v) => patch({ ...row, Category: v })}
          />
          <Field label="TriggerCondition" hint="발동 조건(밟음, 조사, 타이머 등)을 자연어로 적습니다.">
            <TextArea value={row.TriggerCondition} onChange={(v) => patch({ ...row, TriggerCondition: v })} rows={2} />
          </Field>
          <Field label="Effect" hint="발동 시 피해·상태·이동 등 결과를 적습니다.">
            <TextArea value={row.Effect} onChange={(v) => patch({ ...row, Effect: v })} rows={2} />
          </Field>
          <Field label="CounterMeasure" hint="회피·해제 방법(도구, 스킬, 관찰 포인트).">
            <TextArea value={row.CounterMeasure} onChange={(v) => patch({ ...row, CounterMeasure: v })} rows={2} />
          </Field>
          <SelectField
            label="DangerLevel"
            hint="DangerLevelDatabase 마스터에서 선택합니다."
            value={row.DangerLevel}
            options={dangerLevelOptions}
            onChange={(v) => patch({ ...row, DangerLevel: v })}
          />
        </div>
      )}
    />
  );
}

/* ---------- Base ---------- */
function BaseDatabaseEditor({ data, onChange }) {
  if (!Array.isArray(data)) {
    return <p className="hub-world-guided-err">루트가 배열([])이어야 합니다.</p>;
  }
  return (
    <ArrayCardList
      title="아지트 시설"
      items={data}
      onItemsChange={onChange}
      emptyTemplate={{
        BaseId: '',
        Name: '',
        Type: '',
        Description: '',
        AvailableServices: '',
        RiskOrLimit: '',
      }}
      renderItem={(row, patch) => (
        <div className="hub-world-card-fields">
          <div className="hub-world-field-row">
            <Field label="BaseId" hint="거점·시설을 구분하는 내부 ID.">
              <TextInput value={row.BaseId} onChange={(v) => patch({ ...row, BaseId: v })} />
            </Field>
            <Field label="Name" hint="길드 거점·상점 등 표시 이름.">
              <TextInput value={row.Name} onChange={(v) => patch({ ...row, Name: v })} />
            </Field>
          </div>
          <Field label="Type" hint="예: 캠프, 상점, 대장간. 분류용.">
            <TextInput value={row.Type} onChange={(v) => patch({ ...row, Type: v })} />
          </Field>
          <Field label="Description" hint="장소 분위기·위치·역할을 설명합니다.">
            <TextArea value={row.Description} onChange={(v) => patch({ ...row, Description: v })} rows={2} />
          </Field>
          <Field label="AvailableServices" hint="이용 가능한 서비스(수리, 휴식, 정보 등)를 적습니다.">
            <TextArea value={row.AvailableServices} onChange={(v) => patch({ ...row, AvailableServices: v })} rows={2} />
          </Field>
          <Field label="RiskOrLimit" hint="이용 제한, 비용, 명성 요구, 위험 등 부가 조건.">
            <TextArea value={row.RiskOrLimit} onChange={(v) => patch({ ...row, RiskOrLimit: v })} rows={2} />
          </Field>
        </div>
      )}
    />
  );
}

/* ---------- World lore ---------- */
/** @param {'meta' | 'dungeons' | 'all'} focus — 단계별로 큰 개념만 또는 던전만 표시 */
function WorldLoreEditor({
  data,
  onChange,
  focus = 'all',
  itemNameOptions = [],
  monsterNameOptions = [],
}) {
  if (!data || typeof data !== 'object' || Array.isArray(data)) {
    return <p className="hub-world-guided-err">루트가 객체({})이어야 합니다.</p>;
  }
  const d = { ...data };
  const patch = (part) => onChange({ ...d, ...part });

  const showMeta = focus === 'meta' || focus === 'all';
  const showDungeons = focus === 'dungeons' || focus === 'all';

  return (
    <div className="hub-world-lore-root">
      {showMeta && (
      <section className="hub-world-section glass-panel">
        <h4 className="hub-world-section-title">세계 개요</h4>
        <Field label="WorldName" hint="세계관·UI에 쓰는 공식 이름입니다.">
          <TextInput value={d.WorldName} onChange={(v) => patch({ WorldName: v })} />
        </Field>
        <Field label="WorldSummary" hint="한눈에 보는 세계 설정. LLM·플레이어용 개요(2~5문장 권장).">
          <TextArea value={d.WorldSummary} onChange={(v) => patch({ WorldSummary: v })} rows={3} />
        </Field>
        <Field label="GuildInfo" hint="길드의 역할, 위치, 평판 등. 대화·퀘스트 맥락에 씁니다.">
          <TextArea value={d.GuildInfo} onChange={(v) => patch({ GuildInfo: v })} rows={2} />
        </Field>
        <Field label="DungeonSystem" hint="던전이 어떻게 생기고 운영되는지(규칙·위험·보상 감각).">
          <TextArea value={d.DungeonSystem} onChange={(v) => patch({ DungeonSystem: v })} rows={2} />
        </Field>
        <Field label="BaseCamp" hint="거점 이름 또는 한 줄 요약(아지트·캠프와 연결).">
          <TextInput value={d.BaseCamp} onChange={(v) => patch({ BaseCamp: v })} />
        </Field>
        <Field label="CurrencyAndLoot" hint="화폐 단위, 전리품 문화, 암시장 등 경제 감각.">
          <TextArea value={d.CurrencyAndLoot} onChange={(v) => patch({ CurrencyAndLoot: v })} rows={2} />
        </Field>
      </section>
      )}

      {showMeta && (
      <ArrayCardList
        title="Locations"
        items={d.Locations || []}
        onItemsChange={(arr) => patch({ Locations: arr })}
        emptyTemplate={{ Name: '', Description: '', Type: '' }}
        addLabel="장소 추가"
        renderItem={(row, p) => (
          <div className="hub-world-card-fields">
            <Field label="Name" hint="지명·도시·유적 등 장소 이름.">
              <TextInput value={row.Name} onChange={(v) => p({ ...row, Name: v })} />
            </Field>
            <Field label="Type" hint="예: 도시, 마을, 던전 입구. 분류용.">
              <TextInput value={row.Type} onChange={(v) => p({ ...row, Type: v })} />
            </Field>
            <Field label="Description" hint="분위기·거리·주민 감각 등 짧은 묘사.">
              <TextArea value={row.Description} onChange={(v) => p({ ...row, Description: v })} rows={2} />
            </Field>
          </div>
        )}
      />
      )}

      {showDungeons && (
      <ArrayCardList
        title="Dungeons (KnownRewards → ItemDB, TypicalMonsters → MonsterDB)"
        items={d.Dungeons || []}
        onItemsChange={(arr) => patch({ Dungeons: arr })}
        emptyTemplate={{
          Name: '',
          Description: '',
          Difficulty: 'Low',
          TypicalMonsters: [],
          KnownRewards: [],
        }}
        addLabel="던전 추가"
        renderItem={(row, p) => (
          <div className="hub-world-card-fields">
            <Field label="Name" hint="던전·구역 이름. 세계관에서 구별되는 호칭으로 둡니다.">
              <TextInput value={row.Name} onChange={(v) => p({ ...row, Name: v })} />
            </Field>
            <Field
              label="Difficulty"
              hint="난이도 태그(예: Low, Mid, Abyss). 이벤트·경고 문구와 맞춥니다."
            >
              <TextInput value={row.Difficulty} onChange={(v) => p({ ...row, Difficulty: v })} placeholder="Low / Mid / Abyss" />
            </Field>
            <Field label="Description" hint="환경·위험·전설 등. 이 던전만의 분위기를 적습니다.">
              <TextArea value={row.Description} onChange={(v) => p({ ...row, Description: v })} rows={2} />
            </Field>
            <RefPickerColumn
              label="TypicalMonsters — 몬스터 DB에서 선택"
              hint="여기서 자주 등장하는 적. MonsterDB의 MonsterName과 동일한 항목만 선택합니다."
              selected={row.TypicalMonsters || []}
              options={monsterNameOptions}
              onChange={(arr) => p({ ...row, TypicalMonsters: arr })}
              emptyMessage="몬스터 DB에 MonsterName이 없습니다. 먼저 몬스터 단계에서 항목을 추가하세요."
            />
            <RefPickerColumn
              label="KnownRewards — 아이템 DB에서 선택"
              hint="전설·기록상 알려진 전리품. ItemDB의 ItemName과 동일한 항목만 선택합니다."
              selected={row.KnownRewards || []}
              options={itemNameOptions}
              onChange={(arr) => p({ ...row, KnownRewards: arr })}
              emptyMessage="아이템 DB에 ItemName이 없습니다. 먼저 아이템 단계에서 항목을 추가하세요."
            />
          </div>
        )}
      />
      )}

      {showMeta && (
      <>
      <ArrayCardList
        title="Organizations"
        items={d.Organizations || []}
        onItemsChange={(arr) => patch({ Organizations: arr })}
        emptyTemplate={{ Name: '', Description: '', Type: '' }}
        addLabel="조직 추가"
        renderItem={(row, p) => (
          <div className="hub-world-card-fields">
            <Field label="Name" hint="국가, 길드 연합, 교단 등 조직 이름.">
              <TextInput value={row.Name} onChange={(v) => p({ ...row, Name: v })} />
            </Field>
            <Field label="Type" hint="예: 왕국, 범죄조직, 종교. 분류용.">
              <TextInput value={row.Type} onChange={(v) => p({ ...row, Type: v })} />
            </Field>
            <Field label="Description" hint="목적, 세력 범위, 플레이어와의 관계.">
              <TextArea value={row.Description} onChange={(v) => p({ ...row, Description: v })} rows={2} />
            </Field>
          </div>
        )}
      />

      <LinesToListEditor
        label="Lore (한 줄에 하나)"
        hint="짧은 설정 조각·역사·금기를 한 줄씩. LLM이 인용하기 좋은 단위로 씁니다."
        lines={d.Lore || []}
        onChange={(arr) => patch({ Lore: arr })}
      />
      </>
      )}
    </div>
  );
}

/* ---------- Event types ---------- */
function EventTypeDatabaseEditor({ data, onChange }) {
  if (!data || typeof data !== 'object' || Array.isArray(data)) {
    return <p className="hub-world-guided-err">루트가 객체({})이어야 합니다.</p>;
  }
  const d = { DungeonEventTypes: [], BaseEventTypes: [], ...data };

  const renderEntryList = (key, title) => (
    <ArrayCardList
      title={title}
      items={d[key] || []}
      onItemsChange={(arr) => onChange({ ...d, [key]: arr })}
      emptyTemplate={{ Code: '', LabelKo: '', Meaning: '' }}
      addLabel="항목 추가"
      renderItem={(row, p) => (
        <div className="hub-world-card-fields">
          <Field label="Code" hint="이벤트 타입을 식별하는 짧은 코드(영문·스네이크 케이스 권장).">
            <TextInput value={row.Code} onChange={(v) => p({ ...row, Code: v })} />
          </Field>
          <Field label="LabelKo" hint="UI·로그에 보일 한글 라벨.">
            <TextInput value={row.LabelKo} onChange={(v) => p({ ...row, LabelKo: v })} />
          </Field>
          <Field label="Meaning" hint="언제 쓰이는지, 게임적으로 무엇을 의미하는지 설명합니다.">
            <TextArea value={row.Meaning} onChange={(v) => p({ ...row, Meaning: v })} rows={2} />
          </Field>
        </div>
      )}
    />
  );

  return (
    <div className="hub-world-lore-root">
      {renderEntryList('DungeonEventTypes', 'DungeonEventTypes')}
      {renderEntryList('BaseEventTypes', 'BaseEventTypes')}
    </div>
  );
}

/* ---------- Guild office exploration ---------- */
function GuildOfficeExplorationEditor({ data, onChange }) {
  if (!data || typeof data !== 'object' || Array.isArray(data)) {
    return <p className="hub-world-guided-err">루트가 객체({})이어야 합니다.</p>;
  }
  const d = { Description: '', Cases: [], ...data };
  return (
    <div className="hub-world-lore-root">
      <Field label="Description" hint="탐험 시나리오 전체를 한 줄로 요약합니다.">
        <TextArea value={d.Description} onChange={(v) => onChange({ ...d, Description: v })} rows={2} />
      </Field>
      <ArrayCardList
        title="Cases"
        items={d.Cases || []}
        onItemsChange={(arr) => onChange({ ...d, Cases: arr })}
        emptyTemplate={{ Label: '', Utterance: '' }}
        addLabel="발화 추가"
        renderItem={(row, p) => (
          <div className="hub-world-card-fields">
            <Field label="Label" hint="상황 태그(예: 입장, 전투 전, 휴식). 검색·선택용.">
              <TextInput value={row.Label} onChange={(v) => p({ ...row, Label: v })} />
            </Field>
            <Field label="Utterance" hint="동료·내레이션 예시 대사. 톤 참고용.">
              <TextArea value={row.Utterance} onChange={(v) => p({ ...row, Utterance: v })} rows={3} />
            </Field>
          </div>
        )}
      />
    </div>
  );
}

/* ---------- Semantic guardrail (동적: 문자열 배열 = 줄 단위) ---------- */
function SemanticGuardrailEditor({ data, onChange }) {
  if (!data || typeof data !== 'object' || Array.isArray(data)) {
    return <p className="hub-world-guided-err">루트가 객체({})이어야 합니다.</p>;
  }
  const keys = Object.keys(data).sort();
  return (
    <div className="hub-world-lore-root">
      {keys.map((key) => {
        const val = data[key];
        if (typeof val === 'string') {
          return (
            <Field key={key} label={key}>
              <TextArea value={val} onChange={(v) => onChange({ ...data, [key]: v })} rows={3} />
            </Field>
          );
        }
        if (Array.isArray(val) && val.every((x) => typeof x === 'string')) {
          return (
            <LinesToListEditor
              key={key}
              label={`${key} (한 줄에 하나)`}
              lines={val}
              onChange={(arr) => onChange({ ...data, [key]: arr })}
            />
          );
        }
        return (
          <div key={key} className="hub-world-guided-skip glass-panel">
            <strong>{key}</strong>
            <p className="hub-muted">이 필드는 구조가 복잡합니다. JSON 원문 편집으로 수정하세요.</p>
          </div>
        );
      })}
    </div>
  );
}

/* ---------- Dialogue settings (섹션별) ---------- */
function TraitMapEditor({ map, onChange }) {
  const rows = Object.entries(map || {});
  const renameKey = (oldK, newK) => {
    if (oldK === newK) return;
    const next = { ...(map || {}) };
    const v = next[oldK];
    delete next[oldK];
    next[newK] = v;
    onChange(next);
  };
  const updateVal = (k, v) => onChange({ ...(map || {}), [k]: v });
  const remove = (k) => {
    const next = { ...(map || {}) };
    delete next[k];
    onChange(next);
  };
  const add = () => onChange({ ...(map || {}), '': '' });

  return (
    <div className="hub-world-array-section">
      <div className="hub-world-array-head">
        <h4 className="hub-world-array-title">TraitToToneKeywords</h4>
        <button type="button" className="hud-button hub-world-icon-btn" onClick={add}>
          <Plus size={16} /> 키 추가
        </button>
      </div>
      <p className="hub-world-field-hint hub-world-trait-blurb">
        캐릭터 특성 키(예: 냉정)를 왼쪽에 두고, 오른쪽에 그 톤을 유도할 키워드·문장을 적습니다. 프롬프트 보조용입니다.
      </p>
      <div className="hub-world-trait-grid">
        {rows.map(([k, v], idx) => (
          <div key={`${idx}-${k}`} className="hub-world-trait-row glass-panel">
            <TextInput value={k} onChange={(nk) => renameKey(k, nk)} placeholder="Trait" />
            <TextArea value={v} onChange={(nv) => updateVal(k, nv)} rows={2} placeholder="톤 키워드" />
            <button type="button" className="hud-button hub-world-icon-btn danger" onClick={() => remove(k)}>
              <Trash2 size={16} />
            </button>
          </div>
        ))}
      </div>
    </div>
  );
}

function OllamaSection({ ollama, onChange }) {
  const o = { ...ollama };
  return (
    <section className="hub-world-section glass-panel">
      <h4 className="hub-world-section-title">Ollama</h4>
      <div className="hub-world-card-fields">
        <Field label="BaseUrl" hint="로컬 Ollama API 주소(예: http://127.0.0.1:11434).">
          <TextInput value={o.BaseUrl} onChange={(v) => onChange({ ...o, BaseUrl: v })} />
        </Field>
        <Field label="Model" hint="채팅·생성에 쓸 모델 이름(ollama list와 동일).">
          <TextInput value={o.Model} onChange={(v) => onChange({ ...o, Model: v })} />
        </Field>
        <Field label="EmbeddingModel" hint="RAG 임베딩용 모델. 비우면 기본값을 쓸 수 있습니다.">
          <TextInput value={o.EmbeddingModel ?? ''} onChange={(v) => onChange({ ...o, EmbeddingModel: v })} />
        </Field>
        <div className="hub-world-field-row">
          <Field label="TimeoutSeconds" hint="요청 타임아웃(초). 긴 생성 시 늘립니다.">
            <NumberInput value={o.TimeoutSeconds} onChange={(v) => onChange({ ...o, TimeoutSeconds: v })} />
          </Field>
          <Field label="EmbeddingMaxConcurrency" hint="임베딩 동시 요청 수 상한. 과부하 시 줄입니다.">
            <NumberInput value={o.EmbeddingMaxConcurrency} onChange={(v) => onChange({ ...o, EmbeddingMaxConcurrency: v })} />
          </Field>
        </div>
        <div className="hub-world-field-row">
          <Field label="Temperature" hint="높을수록 다양·창의, 낮을수록 일관(0~2 근처).">
            <NumberInput value={o.Temperature} onChange={(v) => onChange({ ...o, Temperature: v })} />
          </Field>
          <Field label="TopP" hint="핵심 샘플링(nucleus). Temperature와 함께 출력 다양도를 조절합니다.">
            <NumberInput value={o.TopP} onChange={(v) => onChange({ ...o, TopP: v })} />
          </Field>
        </div>
        <div className="hub-world-field-row">
          <Field label="NumPredict" hint="한 번에 생성할 최대 토큰 수(모델·설정에 따라 다름).">
            <NumberInput value={o.NumPredict} onChange={(v) => onChange({ ...o, NumPredict: v })} />
          </Field>
          <Field label="AnalysisStrategyMaxTokens" hint="전략/분석 단계 전용 토큰 상한.">
            <NumberInput value={o.AnalysisStrategyMaxTokens} onChange={(v) => onChange({ ...o, AnalysisStrategyMaxTokens: v })} />
          </Field>
        </div>
      </div>
    </section>
  );
}

function PrimitiveObjectSection({ title, obj, onChange }) {
  if (!obj || typeof obj !== 'object') return null;
  const entries = Object.entries(obj);
  return (
    <section className="hub-world-section glass-panel">
      <h4 className="hub-world-section-title">{title}</h4>
      <div className="hub-world-primitive-grid">
        {entries.map(([k, v]) => (
          <Field key={k} label={k}>
            {typeof v === 'boolean' ? (
              <label className="hub-world-check">
                <input
                  type="checkbox"
                  checked={!!v}
                  onChange={(e) => onChange({ ...obj, [k]: e.target.checked })}
                />
                <span>{v ? 'true' : 'false'}</span>
              </label>
            ) : typeof v === 'number' ? (
              <NumberInput value={v} onChange={(n) => onChange({ ...obj, [k]: n })} />
            ) : (
              <TextInput value={v == null ? '' : String(v)} onChange={(s) => onChange({ ...obj, [k]: s })} />
            )}
          </Field>
        ))}
      </div>
    </section>
  );
}

function DialogueSettingsEditor({ data, onChange }) {
  if (!data || typeof data !== 'object' || Array.isArray(data)) {
    return <p className="hub-world-guided-err">루트가 객체({})이어야 합니다.</p>;
  }
  const d = { ...data };
  return (
    <div className="hub-world-lore-root">
      <TraitMapEditor map={d.TraitToToneKeywords} onChange={(m) => onChange({ ...d, TraitToToneKeywords: m })} />
      <OllamaSection ollama={d.Ollama || {}} onChange={(o) => onChange({ ...d, Ollama: o })} />
      <PrimitiveObjectSection title="Retrieval" obj={d.Retrieval} onChange={(o) => onChange({ ...d, Retrieval: o })} />
      <PrimitiveObjectSection title="QualitativeAnalysis" obj={d.QualitativeAnalysis} onChange={(o) => onChange({ ...d, QualitativeAnalysis: o })} />
    </div>
  );
}

/* ---------- Router ---------- */
export function GuidedFileEditor({ fileName, text, onChange, guidedFocus, pickerLists }) {
  const parsed = useMemo(() => parseJson(text), [text]);

  const commit = useCallback(
    (nextData) => {
      onChange(stringifyPretty(nextData));
    },
    [onChange]
  );

  if (!parsed.ok) {
    return (
      <div className="hub-world-guided-err glass-panel">
        <p>
          <strong>JSON 파싱 실패</strong> — 아래 오류를 고치려면 <strong>JSON 원문 편집</strong>을 여세요.
        </p>
        <code className="hub-world-parse-err">{parsed.error}</code>
      </div>
    );
  }

  const data = parsed.data;

  switch (fileName) {
    case 'TagDatabase.json':
      return <TagDatabaseEditor data={data} onChange={commit} />;
    case 'MonsterTraitDatabase.json':
      return <MonsterTraitDatabaseEditor data={data} onChange={commit} />;
    case 'SkillTypeDatabase.json':
      return (
        <LexiconTwoColEditor
          data={data}
          onChange={commit}
          title="스킬 타입 (SkillType)"
          primaryKey="TypeName"
          nameLabel="TypeName"
        />
      );
    case 'ItemTypeDatabase.json':
      return (
        <LexiconTwoColEditor
          data={data}
          onChange={commit}
          title="아이템 타입 (ItemType)"
          primaryKey="TypeName"
          nameLabel="TypeName"
        />
      );
    case 'RarityDatabase.json':
      return (
        <LexiconTwoColEditor
          data={data}
          onChange={commit}
          title="희귀도 (Rarity)"
          primaryKey="RarityName"
          nameLabel="RarityName"
        />
      );
    case 'MonsterTypeDatabase.json':
      return (
        <LexiconTwoColEditor
          data={data}
          onChange={commit}
          title="몬스터 종류 (Type)"
          primaryKey="TypeName"
          nameLabel="TypeName"
        />
      );
    case 'DangerLevelDatabase.json':
      return (
        <LexiconTwoColEditor
          data={data}
          onChange={commit}
          title="위험도 (DangerLevel)"
          primaryKey="LevelName"
          nameLabel="LevelName"
        />
      );
    case 'TrapCategoryDatabase.json':
      return (
        <LexiconTwoColEditor
          data={data}
          onChange={commit}
          title="함정 분류 (Category)"
          primaryKey="CategoryName"
          nameLabel="CategoryName"
        />
      );
    case 'EffectSnippetDatabase.json':
      return <EffectSnippetDatabaseEditor data={data} onChange={commit} />;
    case 'SkillDatabase.json':
      return (
        <SkillDatabaseEditor
          data={data}
          onChange={commit}
          tagNameOptions={pickerLists?.tagNameOptions ?? []}
          skillTypeOptions={pickerLists?.skillTypeOptions ?? []}
          effectSnippetOptionsSkill={pickerLists?.effectSnippetOptionsSkill ?? []}
          effectSnippetDefs={pickerLists?.effectSnippetDefs ?? {}}
        />
      );
    case 'JobDatabase.json':
      return (
        <JobDatabaseEditor
          data={data}
          onChange={commit}
          skillNameOptions={pickerLists?.skillNameOptions ?? []}
        />
      );
    case 'ItemDatabase.json':
      return (
        <ItemDatabaseEditor
          data={data}
          onChange={commit}
          itemTypeOptions={pickerLists?.itemTypeOptions ?? []}
          rarityOptions={pickerLists?.rarityOptions ?? []}
          effectSnippetOptionsItem={pickerLists?.effectSnippetOptionsItem ?? []}
          effectSnippetDefs={pickerLists?.effectSnippetDefs ?? {}}
        />
      );
    case 'MonsterDatabase.json':
      return (
        <MonsterDatabaseEditor
          data={data}
          onChange={commit}
          traitNameOptions={pickerLists?.traitNameOptions ?? []}
          monsterTypeOptions={pickerLists?.monsterTypeOptions ?? []}
          dangerLevelOptions={pickerLists?.dangerLevelOptions ?? []}
        />
      );
    case 'TrapTypeDatabase.json':
      return (
        <TrapDatabaseEditor
          data={data}
          onChange={commit}
          trapCategoryOptions={pickerLists?.trapCategoryOptions ?? []}
          dangerLevelOptions={pickerLists?.dangerLevelOptions ?? []}
        />
      );
    case 'BaseDatabase.json':
      return <BaseDatabaseEditor data={data} onChange={commit} />;
    case 'WorldLore.json':
      return (
        <WorldLoreEditor
          data={data}
          onChange={commit}
          focus={guidedFocus === 'meta' || guidedFocus === 'dungeons' ? guidedFocus : 'all'}
          itemNameOptions={pickerLists?.itemNameOptions ?? []}
          monsterNameOptions={pickerLists?.monsterNameOptions ?? []}
        />
      );
    case 'EventTypeDatabase.json':
      return <EventTypeDatabaseEditor data={data} onChange={commit} />;
    case 'DialogueSettings.json':
      return <DialogueSettingsEditor data={data} onChange={commit} />;
    case 'SemanticGuardrailAnchors.json':
      return <SemanticGuardrailEditor data={data} onChange={commit} />;
    case 'GuildOfficeExploration.json':
      return <GuildOfficeExplorationEditor data={data} onChange={commit} />;
    default:
      return (
        <p className="hub-muted">이 파일은 차근차근 편집기가 없습니다. JSON 원문으로 편집하세요.</p>
      );
  }
}
