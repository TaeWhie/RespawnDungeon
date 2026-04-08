import React from 'react';
import { ScrollText } from 'lucide-react';
import useHubGeneratedImage from './useHubGeneratedImage';
import { HUB_IMAGE_THEME_ID, buildDungeonLogPrompt } from './hubImageTheme';

function field(e, ...keys) {
  for (const k of keys) {
    if (e[k] != null && e[k] !== '') return e[k];
  }
  return '';
}

export default function ActionLogSection({ logs, maxItems = 24 }) {
  const items = Array.isArray(logs) ? logs : [];
  const tail = items.slice(-maxItems);

  if (tail.length === 0) {
    return (
      <div className="hub-actionlog-empty">
        <ScrollText size={20} className="hub-muted" />
        <p className="hub-muted">ActionLog 항목이 없습니다. 원정을 보내거나 시뮬을 실행하면 여기에 쌓입니다.</p>
      </div>
    );
  }

  return (
    <div className="hub-actionlog-table-wrap">
      <table className="hub-actionlog-table">
        <thead>
          <tr>
            <th>Scene</th>
            <th>Order</th>
            <th>Type</th>
            <th>Event</th>
            <th>Location</th>
            <th>요약</th>
          </tr>
        </thead>
        <tbody>
          {tail.map((e, i) => {
            const order = field(e, 'order', 'Order');
            const type = field(e, 'type', 'Type');
            const ev = field(e, 'eventType', 'EventType');
            const loc = field(e, 'location', 'Location');
            const dRaw = e.dialogue ?? e.Dialogue;
            const dialogue =
              Array.isArray(dRaw) ? dRaw.filter(Boolean).join(' · ') : dRaw || '';
            const dungeon = field(e, 'dungeonName', 'DungeonName');
            const summary =
              dialogue ||
              [dungeon, field(e, 'outcome', 'Outcome')].filter(Boolean).join(' · ') ||
              JSON.stringify(e).slice(0, 140);

            return (
              <tr key={`${order}-${i}`}>
                <td>
                  <DungeonLogImage
                    order={order}
                    eventType={ev}
                    dungeonName={dungeon}
                    summary={summary}
                  />
                </td>
                <td>
                  <code>{order}</code>
                </td>
                <td>{type || '—'}</td>
                <td>{ev || '—'}</td>
                <td className="hub-actionlog-loc">{loc || '—'}</td>
                <td className="hub-actionlog-sum">{summary}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
      <p className="hub-actionlog-foot hub-muted">
        최근 <strong>{tail.length}</strong>건 표시 (전체 {items.length}건 중 꼬리)
      </p>
    </div>
  );
}

function DungeonLogImage({ order, eventType, dungeonName, summary }) {
  const key = `${order || 'na'}-${eventType || 'event'}`;
  const { imageUrl, loading } = useHubGeneratedImage({
    scope: 'dungeon-log',
    entityKey: key,
    prompt: buildDungeonLogPrompt({ eventType, dungeonName, summary }),
    width: 640,
    height: 512,
    themeId: HUB_IMAGE_THEME_ID,
  });
  if (imageUrl) return <img src={imageUrl} alt="Dungeon scene" className="hub-log-scene-img" loading="lazy" />;
  return <div className="hub-log-scene-placeholder">{loading ? <div className="hub-image-spinner" aria-hidden /> : '—'}</div>;
}
