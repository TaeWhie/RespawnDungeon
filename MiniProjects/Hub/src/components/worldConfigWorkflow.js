/**
 * 세계관 설정: 작은 DB(스킬·아이템·몬스터…) 먼저 → 세계·던전에서 DB 항목을 **선택**해 연결.
 */

import {
  formatEffectSnippetDisplay,
  validateEffectSnippetRow,
  normalizeEffectSnippetRefs,
} from '../utils/effectSnippetSchema.js';

export function stringifyPretty(obj) {
  return JSON.stringify(obj, null, 2);
}

function isBlank(s) {
  return !String(s ?? '').trim();
}

function parseJsonSafe(text, fallback) {
  try {
    return JSON.parse(text ?? '');
  } catch {
    return fallback;
  }
}

/** 단계: 태그·분류 마스터 → 스킬/아이템/몬스터에서 선택 → 던전은 아이템·몬스터 DB에서 선택 */
export const WORLD_CONFIG_STEPS = [
  {
    id: 'tags',
    title: '1. 스킬 태그 마스터',
    files: ['TagDatabase.json'],
    hint: '스킬 Tags에 넣을 태그 이름을 먼저 등록합니다. 스킬 단계에서는 여기서 고릅니다.',
  },
  {
    id: 'lexicon',
    title: '2. 타입·등급·효과 스니펫',
    files: [
      'SkillTypeDatabase.json',
      'ItemTypeDatabase.json',
      'RarityDatabase.json',
      'MonsterTypeDatabase.json',
      'DangerLevelDatabase.json',
      'TrapCategoryDatabase.json',
      'EffectSnippetDatabase.json',
    ],
    hint: 'SkillType·ItemType·Rarity·몬스터 종류·위험도·함정 분류·효과 문구 스니펫을 정의합니다. 이후 단계에서는 여기서만 고릅니다.',
  },
  {
    id: 'skill',
    title: '3. 스킬 마스터',
    files: ['SkillDatabase.json'],
    hint: 'SkillType·Tags·효과 스니펫은 마스터에서 선택합니다.',
  },
  {
    id: 'job',
    title: '4. 직업·허용 스킬',
    files: ['JobDatabase.json'],
    hint: 'AllowedSkillNames는 스킬 DB에 등록된 SkillName을 목록에서 체크해 고릅니다.',
  },
  {
    id: 'item',
    title: '5. 아이템 DB',
    files: ['ItemDatabase.json'],
    hint: 'ItemType·Rarity·효과 스니펫은 마스터에서 선택합니다.',
  },
  {
    id: 'monsterTraits',
    title: '6. 몬스터 특성 마스터',
    files: ['MonsterTraitDatabase.json'],
    hint: '몬스터 Traits에 쓸 특성 문구를 먼저 등록합니다. 몬스터 DB에서는 여기서 고릅니다.',
  },
  {
    id: 'monster',
    title: '7. 몬스터 DB',
    files: ['MonsterDatabase.json'],
    hint: 'Type·DangerLevel·Traits는 각 마스터에서 선택합니다.',
  },
  {
    id: 'trap',
    title: '8. 함정 타입',
    files: ['TrapTypeDatabase.json'],
    hint: 'Category·DangerLevel은 마스터에서 선택합니다.',
  },
  {
    id: 'base',
    title: '9. 아지트 시설',
    files: ['BaseDatabase.json'],
    hint: '베이스 시설 ID·이름.',
  },
  {
    id: 'worldMeta',
    title: '10. 세계·길드 (개요)',
    files: ['WorldLore.json'],
    guidedFocus: 'meta',
    hint: '세계 이름·요약·지역·조직 등. 던전은 다음 단계에서 DB를 골라 연결합니다.',
  },
  {
    id: 'worldDungeons',
    title: '11. 던전 (DB에서 선택)',
    files: ['WorldLore.json'],
    guidedFocus: 'dungeons',
    hint: '전리품·몬스터는 주관식이 아니라 아이템/몬스터 DB 목록에서 체크해 넣습니다.',
  },
  {
    id: 'event',
    title: '12. 이벤트 타입',
    files: ['EventTypeDatabase.json'],
    hint: '던전/베이스 이벤트 라벨.',
  },
  {
    id: 'dialogue',
    title: '13. 대화·LLM',
    files: ['DialogueSettings.json'],
    hint: 'Ollama·RAG 등.',
  },
  {
    id: 'llmAux',
    title: '14. 길드 집무실 보조',
    files: ['SemanticGuardrailAnchors.json', 'GuildOfficeExploration.json'],
    hint: '가드레일·탐색 발화.',
  },
];

export function stepIndexById(id) {
  return WORLD_CONFIG_STEPS.findIndex((s) => s.id === id);
}

/** 오류 메시지용 단계 인덱스 */
export function stepIndexOf(id) {
  const i = stepIndexById(id);
  return i >= 0 ? i : 0;
}

function itemNameSetFromDraft(draft) {
  const items = parseJsonSafe(draft['ItemDatabase.json'], []);
  if (!Array.isArray(items)) return new Set();
  return new Set(items.map((i) => String(i?.ItemName ?? '').trim()).filter(Boolean));
}

function monsterNameSetFromDraft(draft) {
  const monsters = parseJsonSafe(draft['MonsterDatabase.json'], []);
  if (!Array.isArray(monsters)) return new Set();
  return new Set(monsters.map((m) => String(m?.MonsterName ?? '').trim()).filter(Boolean));
}

function tagNameSetFromDraft(draft) {
  const tags = parseJsonSafe(draft['TagDatabase.json'], []);
  if (!Array.isArray(tags)) return new Set();
  return new Set(tags.map((t) => String(t?.TagName ?? '').trim()).filter(Boolean));
}

function traitNameSetFromDraft(draft) {
  const traits = parseJsonSafe(draft['MonsterTraitDatabase.json'], []);
  if (!Array.isArray(traits)) return new Set();
  return new Set(traits.map((t) => String(t?.TraitName ?? '').trim()).filter(Boolean));
}

function tagExistsInMaster(name, set) {
  const n = String(name ?? '').trim();
  if (!n) return true;
  return [...set].some((x) => x.toLowerCase() === n.toLowerCase());
}

function traitExistsInMaster(name, set) {
  const n = String(name ?? '').trim();
  if (!n) return true;
  return [...set].some((x) => x.toLowerCase() === n.toLowerCase());
}

/** 스킬 Tags가 TagDatabase에 있는지 */
export function validateSkillTagsAgainstDb(draft) {
  const tagSet = tagNameSetFromDraft(draft);
  const skills = parseJsonSafe(draft['SkillDatabase.json'], []);
  if (!Array.isArray(skills)) {
    return { stepIndex: stepIndexOf('skill'), fileName: 'SkillDatabase.json', message: 'SkillDatabase 형식이 배열이 아닙니다.' };
  }
  for (const s of skills) {
    const sn = String(s?.SkillName ?? '').trim();
    if (!sn) continue;
    for (const tg of s.Tags || []) {
      const t = String(tg ?? '').trim();
      if (!t) continue;
      if (!tagExistsInMaster(t, tagSet)) {
        return {
          stepIndex: stepIndexOf('skill'),
          fileName: 'SkillDatabase.json',
          message: `스킬 «${sn}»: Tags의 «${t}»이(가) 스킬 태그 마스터(TagDatabase)에 없습니다.`,
        };
      }
    }
  }
  return null;
}

/** 몬스터 Traits가 MonsterTraitDatabase에 있는지 */
export function validateMonsterTraitsAgainstDb(draft) {
  const traitSet = traitNameSetFromDraft(draft);
  const monsters = parseJsonSafe(draft['MonsterDatabase.json'], []);
  if (!Array.isArray(monsters)) {
    return { stepIndex: stepIndexOf('monster'), fileName: 'MonsterDatabase.json', message: 'MonsterDatabase 형식이 배열이 아닙니다.' };
  }
  for (const m of monsters) {
    const mn = String(m?.MonsterName ?? '').trim();
    if (!mn) continue;
    for (const tr of m.Traits || []) {
      const t = String(tr ?? '').trim();
      if (!t) continue;
      if (!traitExistsInMaster(t, traitSet)) {
        return {
          stepIndex: stepIndexOf('monster'),
          fileName: 'MonsterDatabase.json',
          message: `몬스터 «${mn}»: Traits의 «${t}»이(가) 몬스터 특성 마스터에 없습니다.`,
        };
      }
    }
  }
  return null;
}

function skillTypeSetFromDraft(draft) {
  const rows = parseJsonSafe(draft['SkillTypeDatabase.json'], []);
  if (!Array.isArray(rows)) return new Set();
  return new Set(rows.map((r) => String(r?.TypeName ?? '').trim()).filter(Boolean));
}

function itemTypeSetFromDraft(draft) {
  const rows = parseJsonSafe(draft['ItemTypeDatabase.json'], []);
  if (!Array.isArray(rows)) return new Set();
  return new Set(rows.map((r) => String(r?.TypeName ?? '').trim()).filter(Boolean));
}

function raritySetFromDraft(draft) {
  const rows = parseJsonSafe(draft['RarityDatabase.json'], []);
  if (!Array.isArray(rows)) return new Set();
  return new Set(rows.map((r) => String(r?.RarityName ?? '').trim()).filter(Boolean));
}

function monsterTypeSetFromDraft(draft) {
  const rows = parseJsonSafe(draft['MonsterTypeDatabase.json'], []);
  if (!Array.isArray(rows)) return new Set();
  return new Set(rows.map((r) => String(r?.TypeName ?? '').trim()).filter(Boolean));
}

function dangerLevelSetFromDraft(draft) {
  const rows = parseJsonSafe(draft['DangerLevelDatabase.json'], []);
  if (!Array.isArray(rows)) return new Set();
  return new Set(rows.map((r) => String(r?.LevelName ?? '').trim()).filter(Boolean));
}

function trapCategorySetFromDraft(draft) {
  const rows = parseJsonSafe(draft['TrapCategoryDatabase.json'], []);
  if (!Array.isArray(rows)) return new Set();
  return new Set(rows.map((r) => String(r?.CategoryName ?? '').trim()).filter(Boolean));
}

function parseEffectSnippets(draft) {
  const arr = parseJsonSafe(draft['EffectSnippetDatabase.json'], []);
  const effectByName = {};
  if (!Array.isArray(arr)) return { effectByName };
  for (const e of arr) {
    const n = String(e?.SnippetName ?? '').trim();
    if (!n) continue;
    effectByName[n] = {
      body: formatEffectSnippetDisplay(e),
      scope: String(e?.Scope ?? 'both').toLowerCase(),
    };
  }
  return { effectByName };
}

function inNameSet(value, set) {
  const v = String(value ?? '').trim();
  if (set.size === 0) return true;
  if (!v) return false;
  return [...set].some((x) => x.toLowerCase() === v.toLowerCase());
}

/** SkillType·EffectSnippetNames가 마스터와 맞는지 */
export function validateSkillLexiconAgainstDb(draft) {
  const st = skillTypeSetFromDraft(draft);
  const { effectByName } = parseEffectSnippets(draft);
  const skills = parseJsonSafe(draft['SkillDatabase.json'], []);
  if (!Array.isArray(skills)) {
    return { stepIndex: stepIndexOf('skill'), fileName: 'SkillDatabase.json', message: 'SkillDatabase 형식이 배열이 아닙니다.' };
  }
  for (const s of skills) {
    const sn = String(s?.SkillName ?? '').trim();
    if (!sn) continue;
    if (!inNameSet(s.SkillType, st)) {
      return {
        stepIndex: stepIndexOf('skill'),
        fileName: 'SkillDatabase.json',
        message: `스킬 «${sn}»: SkillType «${s.SkillType}»이(가) SkillTypeDatabase에 없습니다.`,
      };
    }
    for (const r of normalizeEffectSnippetRefs(s)) {
      const name = String(r.SnippetName ?? '').trim();
      if (!name) continue;
      const ent = effectByName[name];
      if (!ent) {
        return {
          stepIndex: stepIndexOf('skill'),
          fileName: 'SkillDatabase.json',
          message: `스킬 «${sn}»: 스니펫 «${name}»이(가) EffectSnippetDatabase에 없습니다.`,
        };
      }
      if (ent.scope === 'item') {
        return {
          stepIndex: stepIndexOf('skill'),
          fileName: 'SkillDatabase.json',
          message: `스킬 «${sn}»: item 전용 스니펫 «${name}»은(는) 스킬에 쓸 수 없습니다.`,
        };
      }
    }
  }
  return null;
}

/** ItemType·Rarity·EffectSnippetNames */
export function validateItemLexiconAgainstDb(draft) {
  const it = itemTypeSetFromDraft(draft);
  const ra = raritySetFromDraft(draft);
  const { effectByName } = parseEffectSnippets(draft);
  const items = parseJsonSafe(draft['ItemDatabase.json'], []);
  if (!Array.isArray(items)) {
    return { stepIndex: stepIndexOf('item'), fileName: 'ItemDatabase.json', message: 'ItemDatabase 형식이 배열이 아닙니다.' };
  }
  for (const row of items) {
    const nm = String(row?.ItemName ?? '').trim();
    if (!nm) continue;
    if (!inNameSet(row.ItemType, it)) {
      return {
        stepIndex: stepIndexOf('item'),
        fileName: 'ItemDatabase.json',
        message: `아이템 «${nm}»: ItemType «${row.ItemType}»이(가) ItemTypeDatabase에 없습니다.`,
      };
    }
    if (!inNameSet(row.Rarity, ra)) {
      return {
        stepIndex: stepIndexOf('item'),
        fileName: 'ItemDatabase.json',
        message: `아이템 «${nm}»: Rarity «${row.Rarity}»가 RarityDatabase에 없습니다.`,
      };
    }
    for (const r of normalizeEffectSnippetRefs(row)) {
      const name = String(r.SnippetName ?? '').trim();
      if (!name) continue;
      const ent = effectByName[name];
      if (!ent) {
        return {
          stepIndex: stepIndexOf('item'),
          fileName: 'ItemDatabase.json',
          message: `아이템 «${nm}»: 스니펫 «${name}»이(가) EffectSnippetDatabase에 없습니다.`,
        };
      }
      if (ent.scope === 'skill') {
        return {
          stepIndex: stepIndexOf('item'),
          fileName: 'ItemDatabase.json',
          message: `아이템 «${nm}»: skill 전용 스니펫 «${name}»은(는) 아이템에 쓸 수 없습니다.`,
        };
      }
    }
  }
  return null;
}

/** Monster Type·DangerLevel */
export function validateMonsterClassificationAgainstDb(draft) {
  const mt = monsterTypeSetFromDraft(draft);
  const dl = dangerLevelSetFromDraft(draft);
  const monsters = parseJsonSafe(draft['MonsterDatabase.json'], []);
  if (!Array.isArray(monsters)) {
    return { stepIndex: stepIndexOf('monster'), fileName: 'MonsterDatabase.json', message: 'MonsterDatabase 형식이 배열이 아닙니다.' };
  }
  for (const m of monsters) {
    const mn = String(m?.MonsterName ?? '').trim();
    if (!mn) continue;
    if (!inNameSet(m.Type, mt)) {
      return {
        stepIndex: stepIndexOf('monster'),
        fileName: 'MonsterDatabase.json',
        message: `몬스터 «${mn}»: Type «${m.Type}»이(가) MonsterTypeDatabase에 없습니다.`,
      };
    }
    if (!inNameSet(m.DangerLevel, dl)) {
      return {
        stepIndex: stepIndexOf('monster'),
        fileName: 'MonsterDatabase.json',
        message: `몬스터 «${mn}»: DangerLevel «${m.DangerLevel}»이(가) DangerLevelDatabase에 없습니다.`,
      };
    }
  }
  return null;
}

/** Trap Category·DangerLevel */
export function validateTrapClassificationAgainstDb(draft) {
  const tc = trapCategorySetFromDraft(draft);
  const dl = dangerLevelSetFromDraft(draft);
  const traps = parseJsonSafe(draft['TrapTypeDatabase.json'], []);
  if (!Array.isArray(traps)) {
    return { stepIndex: stepIndexOf('trap'), fileName: 'TrapTypeDatabase.json', message: 'TrapTypeDatabase 형식이 배열이 아닙니다.' };
  }
  for (const t of traps) {
    const tn = String(t?.TrapName ?? '').trim();
    if (!tn) continue;
    if (!inNameSet(t.Category, tc)) {
      return {
        stepIndex: stepIndexOf('trap'),
        fileName: 'TrapTypeDatabase.json',
        message: `함정 «${tn}»: Category «${t.Category}»이(가) TrapCategoryDatabase에 없습니다.`,
      };
    }
    if (!inNameSet(t.DangerLevel, dl)) {
      return {
        stepIndex: stepIndexOf('trap'),
        fileName: 'TrapTypeDatabase.json',
        message: `함정 «${tn}»: DangerLevel «${t.DangerLevel}»이(가) DangerLevelDatabase에 없습니다.`,
      };
    }
  }
  return null;
}

/** 던전에 적힌 이름이 각 DB에 존재하는지 (레거시·수동 편집 대비) */
export function validateDungeonRefsAgainstDb(draft) {
  const itemSet = itemNameSetFromDraft(draft);
  const monSet = monsterNameSetFromDraft(draft);
  const wl = parseJsonSafe(draft['WorldLore.json'], {});
  for (const d of wl.Dungeons || []) {
    for (const r of d.KnownRewards || []) {
      const name = String(r ?? '').trim();
      if (name && !itemSet.has(name)) {
        return {
          stepIndex: stepIndexOf('worldDungeons'),
          fileName: 'WorldLore.json',
          message: `전리품 «${name}»이(가) 아이템 DB에 없습니다. 5단계(아이템 DB)에서 추가하거나 던전 선택을 수정하세요.`,
        };
      }
    }
    for (const m of d.TypicalMonsters || []) {
      const name = String(m ?? '').trim();
      if (name && !monSet.has(name)) {
        return {
          stepIndex: stepIndexOf('worldDungeons'),
          fileName: 'WorldLore.json',
          message: `몬스터 «${name}»이(가) 몬스터 DB에 없습니다. 7단계(몬스터 DB)에서 추가하거나 던전 선택을 수정하세요.`,
        };
      }
    }
  }
  return null;
}

/** 단계 저장 시: 이 단계 범위만 검사 */
export function validateBeforeSaveStep(stepIndex, draft) {
  const step = WORLD_CONFIG_STEPS[stepIndex];
  if (!step) return null;

  if (step.id === 'worldMeta') {
    const wl = parseJsonSafe(draft['WorldLore.json'], {});
    if (isBlank(wl.WorldName)) {
      return { stepIndex, fileName: 'WorldLore.json', message: 'WorldName을 채우세요.' };
    }
    if (isBlank(wl.WorldSummary)) {
      return { stepIndex, fileName: 'WorldLore.json', message: 'WorldSummary를 채우세요.' };
    }
    return null;
  }

  if (step.id === 'worldDungeons') {
    const wl = parseJsonSafe(draft['WorldLore.json'], {});
    const dungeons = wl.Dungeons || [];
    for (let i = 0; i < dungeons.length; i++) {
      const d = dungeons[i];
      if (isBlank(d.Name)) {
        return { stepIndex, fileName: 'WorldLore.json', message: `던전 #${i + 1}: Name이 비어 있습니다.` };
      }
      if (isBlank(d.Description)) {
        return { stepIndex, fileName: 'WorldLore.json', message: `던전 «${d.Name}»: Description을 채우세요.` };
      }
    }
    const ref = validateDungeonRefsAgainstDb(draft);
    if (ref) return { ...ref, stepIndex };
    return null;
  }

  if (step.id === 'tags') {
    const tags = parseJsonSafe(draft['TagDatabase.json'], []);
    if (!Array.isArray(tags)) return { stepIndex, fileName: 'TagDatabase.json', message: '형식 오류' };
    for (const t of tags) {
      const n = String(t?.TagName ?? '').trim();
      const desc = String(t?.Description ?? '').trim();
      if (!n && desc) {
        return { stepIndex, fileName: 'TagDatabase.json', message: 'TagName 없이 설명만 있는 항목이 있습니다.' };
      }
      if (n && isBlank(t.Description)) {
        return { stepIndex, fileName: 'TagDatabase.json', message: `태그 «${n}»: Description을 채우세요.` };
      }
    }
    return null;
  }

  if (step.id === 'lexicon') {
    const checkTypeRows = (text, fileName, keyName, label) => {
      const arr = parseJsonSafe(text, []);
      if (!Array.isArray(arr)) return { fileName, message: `${fileName} 형식 오류` };
      for (const row of arr) {
        const n = String(row?.[keyName] ?? '').trim();
        const desc = String(row?.Description ?? '').trim();
        if (!n && desc) return { fileName, message: `${fileName}: ${label} 없이 설명만 있는 항목이 있습니다.` };
        if (n && isBlank(row.Description)) return { fileName, message: `${fileName}: «${n}» Description을 채우세요.` };
      }
      return null;
    };
    const checkEffectRows = (text) => {
      const arr = parseJsonSafe(text, []);
      if (!Array.isArray(arr)) return { fileName: 'EffectSnippetDatabase.json', message: '형식 오류' };
      for (const row of arr) {
        const err = validateEffectSnippetRow(row);
        if (err) return { fileName: 'EffectSnippetDatabase.json', message: err };
      }
      return null;
    };
    const files = [
      [draft['SkillTypeDatabase.json'], 'SkillTypeDatabase.json', 'TypeName'],
      [draft['ItemTypeDatabase.json'], 'ItemTypeDatabase.json', 'TypeName'],
      [draft['RarityDatabase.json'], 'RarityDatabase.json', 'RarityName'],
      [draft['MonsterTypeDatabase.json'], 'MonsterTypeDatabase.json', 'TypeName'],
      [draft['DangerLevelDatabase.json'], 'DangerLevelDatabase.json', 'LevelName'],
      [draft['TrapCategoryDatabase.json'], 'TrapCategoryDatabase.json', 'CategoryName'],
    ];
    for (const [text, fn, key] of files) {
      const err = checkTypeRows(text, fn, key, key);
      if (err) return { stepIndex, fileName: err.fileName, message: err.message };
    }
    const effErr = checkEffectRows(draft['EffectSnippetDatabase.json']);
    if (effErr) return { stepIndex, fileName: effErr.fileName, message: effErr.message };
    return null;
  }

  if (step.id === 'monsterTraits') {
    const traits = parseJsonSafe(draft['MonsterTraitDatabase.json'], []);
    if (!Array.isArray(traits)) return { stepIndex, fileName: 'MonsterTraitDatabase.json', message: '형식 오류' };
    for (const t of traits) {
      const n = String(t?.TraitName ?? '').trim();
      const desc = String(t?.Description ?? '').trim();
      if (!n && desc) {
        return { stepIndex, fileName: 'MonsterTraitDatabase.json', message: 'TraitName 없이 설명만 있는 항목이 있습니다.' };
      }
      if (n && isBlank(t.Description)) {
        return { stepIndex, fileName: 'MonsterTraitDatabase.json', message: `특성 «${n}»: Description을 채우세요.` };
      }
    }
    return null;
  }

  if (step.id === 'item') {
    const items = parseJsonSafe(draft['ItemDatabase.json'], []);
    if (!Array.isArray(items)) {
      return { stepIndex, fileName: 'ItemDatabase.json', message: 'ItemDatabase 형식이 배열이 아닙니다.' };
    }
    for (const it of items) {
      const n = String(it?.ItemName ?? '').trim();
      if (!n) continue;
      if (isBlank(it.ItemType) || isBlank(it.Description) || isBlank(it.Rarity)) {
        return {
          stepIndex,
          fileName: 'ItemDatabase.json',
          message: `아이템 «${n}»: ItemType·Description·Rarity를 채우세요.`,
        };
      }
    }
    const itemLex = validateItemLexiconAgainstDb(draft);
    if (itemLex) return { ...itemLex, stepIndex };
    return null;
  }

  if (step.id === 'monster') {
    const monsters = parseJsonSafe(draft['MonsterDatabase.json'], []);
    if (!Array.isArray(monsters)) {
      return { stepIndex, fileName: 'MonsterDatabase.json', message: 'MonsterDatabase 형식이 배열이 아닙니다.' };
    }
    for (const m of monsters) {
      const n = String(m?.MonsterName ?? '').trim();
      if (!n) continue;
      if (isBlank(m.MonsterId) || isBlank(m.Description)) {
        return {
          stepIndex,
          fileName: 'MonsterDatabase.json',
          message: `몬스터 «${n}»: MonsterId·Description을 채우세요.`,
        };
      }
    }
    const traitErr = validateMonsterTraitsAgainstDb(draft);
    if (traitErr) return { ...traitErr, stepIndex };
    const clsErr = validateMonsterClassificationAgainstDb(draft);
    if (clsErr) return { ...clsErr, stepIndex };
    return null;
  }

  if (step.id === 'trap') {
    const traps = parseJsonSafe(draft['TrapTypeDatabase.json'], []);
    if (!Array.isArray(traps)) return { stepIndex, fileName: 'TrapTypeDatabase.json', message: '형식 오류' };
    for (const t of traps) {
      if (!t || isBlank(t.TrapName)) continue;
      if (isBlank(t.Effect)) {
        return {
          stepIndex,
          fileName: 'TrapTypeDatabase.json',
          message: `함정 «${t.TrapName}»: Effect를 채우세요.`,
        };
      }
    }
    const trapCls = validateTrapClassificationAgainstDb(draft);
    if (trapCls) return { ...trapCls, stepIndex };
    return null;
  }

  if (step.id === 'base') {
    const bases = parseJsonSafe(draft['BaseDatabase.json'], []);
    if (!Array.isArray(bases)) return { stepIndex, fileName: 'BaseDatabase.json', message: '형식 오류' };
    for (const b of bases) {
      if (!b || isBlank(b.BaseId)) continue;
      if (isBlank(b.Name) || isBlank(b.Description)) {
        return {
          stepIndex,
          fileName: 'BaseDatabase.json',
          message: `시설 «${b.BaseId}»: Name·Description을 채우세요.`,
        };
      }
    }
    return null;
  }

  if (step.id === 'skill') {
    const skills = parseJsonSafe(draft['SkillDatabase.json'], []);
    if (!Array.isArray(skills)) return { stepIndex, fileName: 'SkillDatabase.json', message: '형식 오류' };
    for (const s of skills) {
      const n = String(s?.SkillName ?? '').trim();
      if (!n) continue;
      if (isBlank(s.Description)) {
        return { stepIndex, fileName: 'SkillDatabase.json', message: `스킬 «${n}»: Description을 채우세요.` };
      }
    }
    const tagErr = validateSkillTagsAgainstDb(draft);
    if (tagErr) return { ...tagErr, stepIndex };
    const skLex = validateSkillLexiconAgainstDb(draft);
    if (skLex) return { ...skLex, stepIndex };
    return null;
  }

  if (step.id === 'job') {
    const skills = parseJsonSafe(draft['SkillDatabase.json'], []);
    const skillNames = new Set(
      (Array.isArray(skills) ? skills : []).map((s) => String(s?.SkillName ?? '').trim()).filter(Boolean)
    );
    const jobs = parseJsonSafe(draft['JobDatabase.json'], []);
    if (!Array.isArray(jobs)) return { stepIndex, fileName: 'JobDatabase.json', message: '형식 오류' };
    for (const j of jobs) {
      const rid = String(j?.RoleId ?? '').trim();
      if (!rid) continue;
      for (const sn of j.AllowedSkillNames || []) {
        const name = String(sn ?? '').trim();
        if (!name) continue;
        const ok = [...skillNames].some((x) => x.toLowerCase() === name.toLowerCase());
        if (!ok) {
          return {
            stepIndex,
            fileName: 'JobDatabase.json',
            message: `직업 «${rid}»: «${name}»이(가) 스킬 DB에 없습니다.`,
          };
        }
      }
    }
    return null;
  }

  if (step.id === 'event') {
    const ev = parseJsonSafe(draft['EventTypeDatabase.json'], {});
    if (!ev || typeof ev !== 'object') return { stepIndex, fileName: 'EventTypeDatabase.json', message: '형식 오류' };
    for (const key of ['DungeonEventTypes', 'BaseEventTypes']) {
      const arr = ev[key];
      if (!Array.isArray(arr)) continue;
      for (const e of arr) {
        if (!e || (isBlank(e.Code) && isBlank(e.LabelKo))) continue;
        if (isBlank(e.Code) || isBlank(e.LabelKo)) {
          return {
            stepIndex,
            fileName: 'EventTypeDatabase.json',
            message: '이벤트 항목에 Code·LabelKo가 필요합니다.',
          };
        }
      }
    }
    return null;
  }

  if (step.id === 'dialogue') {
    const dlg = parseJsonSafe(draft['DialogueSettings.json'], {});
    if (dlg?.Ollama && (isBlank(dlg.Ollama.BaseUrl) || isBlank(dlg.Ollama.Model))) {
      return {
        stepIndex,
        fileName: 'DialogueSettings.json',
        message: 'Ollama BaseUrl·Model을 채우세요.',
      };
    }
    return null;
  }

  if (step.id === 'llmAux') {
    return null;
  }

  return null;
}

/**
 * 전체 감사 — 작은 DB → 세계관 순으로 첫 문제 탐지
 */
export function findFirstGlobalCompletenessProblem(draft) {
  if (parseJsonSafe(draft['WorldLore.json'], null) === null && (draft['WorldLore.json'] || '').trim()) {
    return {
      stepIndex: stepIndexOf('worldMeta'),
      fileName: 'WorldLore.json',
      message: 'WorldLore.json 형식이 올바르지 않습니다.',
    };
  }

  const tagDbArr = parseJsonSafe(draft['TagDatabase.json'], []);
  if (!Array.isArray(tagDbArr)) {
    return { stepIndex: stepIndexOf('tags'), fileName: 'TagDatabase.json', message: 'TagDatabase가 배열이 아닙니다.' };
  }

  const lexiconFileNames = [
    'SkillTypeDatabase.json',
    'ItemTypeDatabase.json',
    'RarityDatabase.json',
    'MonsterTypeDatabase.json',
    'DangerLevelDatabase.json',
    'TrapCategoryDatabase.json',
    'EffectSnippetDatabase.json',
  ];
  for (const fn of lexiconFileNames) {
    const v = parseJsonSafe(draft[fn], null);
    if (!Array.isArray(v)) {
      return { stepIndex: stepIndexOf('lexicon'), fileName: fn, message: `${fn}가 배열이 아닙니다.` };
    }
  }

  const skills = parseJsonSafe(draft['SkillDatabase.json'], []);
  const skillNames = new Set();
  if (!Array.isArray(skills)) {
    return { stepIndex: stepIndexOf('skill'), fileName: 'SkillDatabase.json', message: 'SkillDatabase가 배열이 아닙니다.' };
  }
  for (const s of skills) {
    const n = String(s?.SkillName ?? '').trim();
    if (!n) continue;
    skillNames.add(n);
    if (isBlank(s.Description)) {
      return {
        stepIndex: stepIndexOf('skill'),
        fileName: 'SkillDatabase.json',
        message: `스킬 «${n}»: Description을 채우세요.`,
      };
    }
  }
  {
    const tagErr = validateSkillTagsAgainstDb(draft);
    if (tagErr) return tagErr;
    const skLex = validateSkillLexiconAgainstDb(draft);
    if (skLex) return skLex;
  }

  const jobs = parseJsonSafe(draft['JobDatabase.json'], []);
  if (Array.isArray(jobs)) {
    for (const j of jobs) {
      const rid = String(j?.RoleId ?? '').trim();
      if (!rid) continue;
      for (const sn of j.AllowedSkillNames || []) {
        const name = String(sn ?? '').trim();
        if (!name) continue;
        const ok = [...skillNames].some((x) => x.toLowerCase() === name.toLowerCase());
        if (!ok) {
          return {
            stepIndex: stepIndexOf('job'),
            fileName: 'JobDatabase.json',
            message: `직업 «${rid}»: «${name}»이(가) 스킬 DB에 없습니다.`,
          };
        }
      }
    }
  }

  const items = parseJsonSafe(draft['ItemDatabase.json'], []);
  if (!Array.isArray(items)) {
    return { stepIndex: stepIndexOf('item'), fileName: 'ItemDatabase.json', message: 'ItemDatabase가 배열이 아닙니다.' };
  }
  for (const it of items) {
    const n = String(it?.ItemName ?? '').trim();
    if (!n) continue;
    if (isBlank(it.ItemType) || isBlank(it.Description) || isBlank(it.Rarity)) {
      return {
        stepIndex: stepIndexOf('item'),
        fileName: 'ItemDatabase.json',
        message: `아이템 «${n}»: ItemType·Description·Rarity를 채우세요.`,
      };
    }
  }
  {
    const itemLex = validateItemLexiconAgainstDb(draft);
    if (itemLex) return itemLex;
  }

  const traitDbArr = parseJsonSafe(draft['MonsterTraitDatabase.json'], []);
  if (!Array.isArray(traitDbArr)) {
    return {
      stepIndex: stepIndexOf('monsterTraits'),
      fileName: 'MonsterTraitDatabase.json',
      message: 'MonsterTraitDatabase가 배열이 아닙니다.',
    };
  }

  const monsters = parseJsonSafe(draft['MonsterDatabase.json'], []);
  if (!Array.isArray(monsters)) {
    return { stepIndex: stepIndexOf('monster'), fileName: 'MonsterDatabase.json', message: 'MonsterDatabase가 배열이 아닙니다.' };
  }
  for (const m of monsters) {
    const n = String(m?.MonsterName ?? '').trim();
    if (!n) continue;
    if (isBlank(m.MonsterId) || isBlank(m.Description)) {
      return {
        stepIndex: stepIndexOf('monster'),
        fileName: 'MonsterDatabase.json',
        message: `몬스터 «${n}»: MonsterId·Description을 채우세요.`,
      };
    }
  }
  {
    const traitErr = validateMonsterTraitsAgainstDb(draft);
    if (traitErr) return traitErr;
    const mCls = validateMonsterClassificationAgainstDb(draft);
    if (mCls) return mCls;
  }

  const traps = parseJsonSafe(draft['TrapTypeDatabase.json'], []);
  if (Array.isArray(traps)) {
    for (const t of traps) {
      if (!t || isBlank(t.TrapName)) continue;
      if (isBlank(t.Effect)) {
        return {
          stepIndex: stepIndexOf('trap'),
          fileName: 'TrapTypeDatabase.json',
          message: `함정 «${t.TrapName}»: Effect를 채우세요.`,
        };
      }
    }
  }
  {
    const trapCls = validateTrapClassificationAgainstDb(draft);
    if (trapCls) return trapCls;
  }

  const bases = parseJsonSafe(draft['BaseDatabase.json'], []);
  if (Array.isArray(bases)) {
    for (const b of bases) {
      if (!b || isBlank(b.BaseId)) continue;
      if (isBlank(b.Name) || isBlank(b.Description)) {
        return {
          stepIndex: stepIndexOf('base'),
          fileName: 'BaseDatabase.json',
          message: `시설 «${b.BaseId}»: Name·Description을 채우세요.`,
        };
      }
    }
  }

  const wl = parseJsonSafe(draft['WorldLore.json'], {});
  if (wl && typeof wl === 'object') {
    if (isBlank(wl.WorldName)) {
      return { stepIndex: stepIndexOf('worldMeta'), fileName: 'WorldLore.json', message: 'WorldName을 채우세요.' };
    }
    if (isBlank(wl.WorldSummary)) {
      return { stepIndex: stepIndexOf('worldMeta'), fileName: 'WorldLore.json', message: 'WorldSummary를 채우세요.' };
    }
    const dungeons = wl.Dungeons || [];
    for (let i = 0; i < dungeons.length; i++) {
      const d = dungeons[i];
      if (isBlank(d.Name)) {
        return {
          stepIndex: stepIndexOf('worldDungeons'),
          fileName: 'WorldLore.json',
          message: `던전 #${i + 1}: Name이 비어 있습니다.`,
        };
      }
      if (isBlank(d.Description)) {
        return {
          stepIndex: stepIndexOf('worldDungeons'),
          fileName: 'WorldLore.json',
          message: `던전 «${d.Name}»: Description을 채우세요.`,
        };
      }
    }
    const refErr = validateDungeonRefsAgainstDb(draft);
    if (refErr) return refErr;
  }

  const ev = parseJsonSafe(draft['EventTypeDatabase.json'], {});
  if (ev && typeof ev === 'object') {
    for (const key of ['DungeonEventTypes', 'BaseEventTypes']) {
      const arr = ev[key];
      if (!Array.isArray(arr)) continue;
      for (const e of arr) {
        if (!e || (isBlank(e.Code) && isBlank(e.LabelKo))) continue;
        if (isBlank(e.Code) || isBlank(e.LabelKo)) {
          return {
            stepIndex: stepIndexOf('event'),
            fileName: 'EventTypeDatabase.json',
            message: '이벤트 타입마다 Code·LabelKo가 필요합니다.',
          };
        }
      }
    }
  }

  const dlg = parseJsonSafe(draft['DialogueSettings.json'], {});
  if (dlg?.Ollama) {
    if (isBlank(dlg.Ollama.BaseUrl) || isBlank(dlg.Ollama.Model)) {
      return {
        stepIndex: stepIndexOf('dialogue'),
        fileName: 'DialogueSettings.json',
        message: 'Ollama BaseUrl·Model을 채우세요.',
      };
    }
  }

  return null;
}
