import React, { useMemo } from 'react';
import { MapPin, Heart, Zap, Users, Sparkles, Package } from 'lucide-react';

function pickChar(c, key) {
  if (key === 'id') return c.id ?? c.Id ?? '';
  if (key === 'name') return c.name ?? c.Name ?? '';
  if (key === 'role') return c.role ?? c.Role ?? '';
  if (key === 'age') return c.age ?? c.Age;
  if (key === 'partyId') return c.partyId ?? c.PartyId ?? '';
  if (key === 'loc') return c.currentLocationId ?? c.CurrentLocationId ?? '';
  if (key === 'locNote') return c.currentLocationNote ?? c.CurrentLocationNote ?? '';
  if (key === 'mood') return c.mood ?? c.Mood ?? '';
  if (key === 'bg') return c.background ?? c.Background ?? '';
  if (key === 'recent') return c.recentMemorableEvent ?? c.RecentMemorableEvent ?? '';
  return undefined;
}

function partyLabelForCharacter(parties, partyId) {
  if (!partyId || !parties?.length) return null;
  const p = parties.find(
    (x) => (x.partyId || x.PartyId || '').toLowerCase() === String(partyId).toLowerCase()
  );
  if (!p) return partyId;
  return p.name || p.Name || partyId;
}

function topPersonalityTraits(personality) {
  if (!personality || typeof personality !== 'object') return [];
  const entries = Object.entries(personality).filter(
    ([k]) => !['constructor'].includes(k)
  );
  entries.sort((a, b) => (Number(b[1]) || 0) - (Number(a[1]) || 0));
  return entries.slice(0, 3);
}

export default function CharacterCardsSection({ characters, parties, backgroundMaxChars = 220 }) {
  const list = useMemo(() => {
    return (characters || []).filter((c) => {
      const id = pickChar(c, 'id');
      return id && !String(id).toLowerCase().includes('master');
    });
  }, [characters]);

  if (list.length === 0) {
    return (
      <div className="hub-char-empty glass-panel">
        <p className="hub-muted">표시할 동료 캐릭터가 없습니다. CharactersDatabase.json을 확인하세요.</p>
      </div>
    );
  }

  return (
    <div className="hub-char-grid">
      {list.map((c) => {
        const id = pickChar(c, 'id');
        const name = pickChar(c, 'name') || id;
        const stats = c.stats ?? c.Stats ?? {};
        const hp = stats.currentHP ?? stats.CurrentHP ?? 0;
        const maxHp = stats.maxHP ?? stats.MaxHP ?? 1;
        const mp = stats.currentMP ?? stats.CurrentMP ?? 0;
        const maxMp = stats.maxMP ?? stats.MaxMP ?? 1;
        const hpPct = Math.min(100, Math.round((hp / Math.max(1, maxHp)) * 100));
        const mpPct = Math.min(100, Math.round((mp / Math.max(1, maxMp)) * 100));
        const partyId = pickChar(c, 'partyId');
        const partyName = partyLabelForCharacter(parties, partyId);
        const loc = pickChar(c, 'loc');
        const locNote = pickChar(c, 'locNote');
        const inv = c.inventory ?? c.Inventory ?? [];
        const traits = topPersonalityTraits(c.personality ?? c.Personality);
        const initial = String(name).charAt(0) || '?';

        return (
          <article key={id} className="hub-char-card glass-panel">
            <div className="hub-char-card-head">
              <div className="hub-char-avatar" aria-hidden>
                {initial}
              </div>
              <div className="hub-char-titles">
                <h4 className="hub-char-name">{name}</h4>
                <span className="hub-char-id">
                  <code>{id}</code>
                  {pickChar(c, 'role') && (
                    <span className="hub-char-role">{pickChar(c, 'role')}</span>
                  )}
                </span>
              </div>
            </div>

            <dl className="hub-char-meta">
              {pickChar(c, 'age') != null && (
                <div className="hub-char-meta-row">
                  <dt>나이</dt>
                  <dd>{pickChar(c, 'age')}</dd>
                </div>
              )}
              {partyName && (
                <div className="hub-char-meta-row">
                  <dt>
                    <Users size={14} /> 파티
                  </dt>
                  <dd>{partyName}</dd>
                </div>
              )}
              <div className="hub-char-meta-row">
                <dt>
                  <MapPin size={14} /> 위치
                </dt>
                <dd>
                  {loc || '—'}
                  {locNote ? ` (${locNote})` : ''}
                </dd>
              </div>
              {pickChar(c, 'mood') && (
                <div className="hub-char-meta-row">
                  <dt>
                    <Sparkles size={14} /> 기분
                  </dt>
                  <dd>{pickChar(c, 'mood')}</dd>
                </div>
              )}
            </dl>

            <div className="hub-char-bars">
              <div className="hub-char-bar-row">
                <Heart size={14} className="hub-icon-hp" />
                <div className="hub-stat-bar-bg">
                  <div className="hub-stat-bar-fill hub-stat-bar-fill--hp" style={{ width: `${hpPct}%` }} />
                </div>
                <span className="hub-stat-num">
                  {hp}/{maxHp}
                </span>
              </div>
              <div className="hub-char-bar-row">
                <Zap size={14} className="hub-icon-mp" />
                <div className="hub-stat-bar-bg">
                  <div className="hub-stat-bar-fill hub-stat-bar-fill--mp" style={{ width: `${mpPct}%` }} />
                </div>
                <span className="hub-stat-num">
                  {mp}/{maxMp}
                </span>
              </div>
            </div>

            {traits.length > 0 && (
              <ul className="hub-char-traits">
                {traits.map(([k, v]) => (
                  <li key={k}>
                    <span className="hub-char-trait-key">{k}</span>
                    <span className="hub-char-trait-val">{v}</span>
                  </li>
                ))}
              </ul>
            )}

            {pickChar(c, 'recent') && (
              <p className="hub-char-recent">
                <strong>최근 기억</strong> — {pickChar(c, 'recent')}
              </p>
            )}

            {pickChar(c, 'bg') && (
              <p className="hub-char-bg">
                {backgroundMaxChars == null ||
                String(pickChar(c, 'bg')).length <= backgroundMaxChars
                  ? pickChar(c, 'bg')
                  : `${String(pickChar(c, 'bg')).slice(0, backgroundMaxChars)}…`}
              </p>
            )}

            <div className="hub-char-inv">
              <div className="hub-char-inv-head">
                <Package size={14} />
                <span>소지품 ({inv.length})</span>
              </div>
              <ul className="hub-char-inv-list">
                {inv.slice(0, 6).map((it, i) => {
                  const iname = it.itemName ?? it.ItemName ?? '?';
                  const cnt = it.count ?? it.Count ?? 1;
                  return (
                    <li key={`${iname}-${i}`}>
                      {iname}
                      {cnt > 1 ? ` ×${cnt}` : ''}
                    </li>
                  );
                })}
                {inv.length > 6 && <li className="hub-muted">외 {inv.length - 6}종…</li>}
              </ul>
            </div>
          </article>
        );
      })}
    </div>
  );
}
