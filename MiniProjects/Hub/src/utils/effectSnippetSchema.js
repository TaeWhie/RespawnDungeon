/**
 * EffectSnippetDatabase: 역할·문장 틀(SummaryTemplate + Parameters 정의)
 * 스킬/아이템: EffectSnippetRefs[].Params 에서 수치 지정
 */

export const EFFECT_KINDS = ['Custom', 'None'];

export const EFFECT_KIND_LABELS = {
  Custom: '효과 요약 / 템플릿',
  None: '효과 없음 · 비고',
};

const TEMPLATE_KEY_RE = /\{([a-zA-Z_][a-zA-Z0-9_]*)\}/g;

/** SummaryTemplate에서 {key} 목록 추출 */
export function extractTemplateKeys(template) {
  const s = String(template ?? '');
  const keys = [];
  const seen = new Set();
  let m;
  const re = new RegExp(TEMPLATE_KEY_RE.source, 'g');
  while ((m = re.exec(s)) !== null) {
    const k = m[1];
    if (!seen.has(k)) {
      seen.add(k);
      keys.push(k);
    }
  }
  return keys;
}

/** @param {Record<string, unknown>|null|undefined} params */
export function interpolateTemplate(template, params) {
  const p = params && typeof params === 'object' ? params : {};
  return String(template ?? '').replace(/\{([a-zA-Z_][a-zA-Z0-9_]*)\}/g, (_, key) => {
    if (Object.prototype.hasOwnProperty.call(p, key) && p[key] !== undefined && p[key] !== null) {
      return String(p[key]);
    }
    return `{${key}}`;
  });
}

/**
 * @param {object} e 스니펫 정의
 * @param {Record<string, unknown>} [params] 스킬/아이템에서 넘긴 값
 */
export function formatEffectSnippetDisplay(e, params) {
  if (!e || typeof e !== 'object') return '';
  const kind = String(e.EffectKind ?? '').trim();
  const legacyBody = String(e.Body ?? '').trim();

  if (!kind) {
    return legacyBody;
  }
  if (kind === 'None') {
    const n = String(e.Note ?? '').trim();
    return n || '없음';
  }
  if (kind === 'Custom') {
    const tpl = String(e.SummaryTemplate ?? '').trim();
    if (tpl) return interpolateTemplate(tpl, params);
    return String(e.Summary ?? '').trim();
  }
  return legacyBody || String(e.Summary ?? '').trim();
}

/** 스니펫 정의에서 기본 Params 객체 (숫자 0) */
export function defaultParamsFromDef(def) {
  const p = {};
  const list = Array.isArray(def?.Parameters) ? def.Parameters : [];
  for (const d of list) {
    const k = String(d?.Key ?? '').trim();
    if (k) p[k] = 0;
  }
  return p;
}

/**
 * @param {object} row Skill 또는 Item 행
 * @returns {{ SnippetName: string, Params?: Record<string, number> }[]}
 */
export function normalizeEffectSnippetRefs(row) {
  if (Array.isArray(row.EffectSnippetRefs) && row.EffectSnippetRefs.length > 0) {
    return row.EffectSnippetRefs.map((r) => ({
      SnippetName: String(r?.SnippetName ?? '').trim(),
      Params: r?.Params && typeof r.Params === 'object' ? { ...r.Params } : {},
    }));
  }
  const names = row.EffectSnippetNames || [];
  return names.map((n) => ({
    SnippetName: String(n ?? '').trim(),
    Params: {},
  }));
}

export function joinEffectSnippetRefs(refs, snippetDefByName) {
  return (refs || [])
    .map((r) => {
      const name = String(r?.SnippetName ?? '').trim();
      const def = snippetDefByName?.[name];
      if (!def) return '';
      return formatEffectSnippetDisplay(def, r.Params || {});
    })
    .filter(Boolean)
    .join(' | ');
}

export function validateEffectSnippetRow(row) {
  const n = String(row?.SnippetName ?? '').trim();
  const sc = String(row?.Scope ?? '').trim().toLowerCase();
  const body = String(row?.Body ?? '').trim();
  const kind = String(row?.EffectKind ?? '').trim();

  if (!n && (body || kind || sc)) {
    return 'SnippetName 없이 내용만 있는 항목이 있습니다.';
  }
  if (!n) return null;

  if (sc && !['skill', 'item', 'both'].includes(sc)) {
    return `스니펫 «${n}»: Scope는 skill / item / both 중 하나여야 합니다.`;
  }

  if (!kind) {
    if (!body) return `스니펫 «${n}»: EffectKind를 지정하거나(권장) 레거시 Body를 채우세요.`;
    return null;
  }

  if (kind === 'Custom') {
    const tpl = String(row.SummaryTemplate ?? '').trim();
    const sum = String(row.Summary ?? '').trim();
    if (!tpl && !sum) {
      return `스니펫 «${n}»: Summary 또는 SummaryTemplate 중 하나를 채우세요.`;
    }
    if (tpl) {
      const keysInTpl = extractTemplateKeys(tpl);
      const paramDefs = Array.isArray(row.Parameters) ? row.Parameters : [];
      const declared = new Set(paramDefs.map((p) => String(p?.Key ?? '').trim()).filter(Boolean));
      for (const k of keysInTpl) {
        if (!declared.has(k)) {
          return `스니펫 «${n}»: 템플릿의 {${k}}에 대응하는 Parameters 항목(Key)을 추가하세요.`;
        }
      }
    }
    return null;
  }
  if (kind === 'None') {
    return null;
  }
  return `스니펫 «${n}»: EffectKind는 Custom 또는 None만 사용하세요.`;
}
