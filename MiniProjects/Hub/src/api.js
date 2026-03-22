/** Vite dev: proxy /api → GuildDialogue --hub-api (기본 5050) */
async function parseJson(res) {
  const text = await res.text();
  try {
    return text ? JSON.parse(text) : {};
  } catch {
    return { _raw: text };
  }
}

export async function apiGet(path) {
  const res = await fetch(path);
  const data = await parseJson(res);
  if (!res.ok) throw new Error(data.error || data.message || res.statusText);
  return data;
}

export async function apiPost(path, body) {
  const res = await fetch(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const data = await parseJson(res);
  if (!res.ok) throw new Error(data.error || data.message || res.statusText);
  return data;
}

export async function apiPut(path, body) {
  const res = await fetch(path, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  const data = await parseJson(res);
  if (!res.ok) throw new Error(data.error || data.message || res.statusText);
  return data;
}

export async function apiDelete(path) {
  const res = await fetch(path, { method: 'DELETE' });
  const data = await parseJson(res);
  if (!res.ok) throw new Error(data.error || data.message || res.statusText);
  return data;
}

/**
 * 아지트 관전 NDJSON 스트림 — 서버가 한 줄 만들 때마다 `onLine` 호출, 끝나면 resolve.
 * `application/x-ndjson`: `{"line":"…"}` 반복 후 `{"done":true}` 또는 `{"error":"…"}`
 */
export async function apiPostSpectatorStream(onLine) {
  const res = await fetch('/api/dialogue/spectator/stream', { method: 'POST' });
  if (res.status === 404) {
    const data = await apiPost('/api/dialogue/spectator/run');
    for (const line of data.transcript || []) onLine(line);
    return;
  }
  if (!res.ok) {
    const t = await res.text();
    let msg = t;
    try {
      const j = JSON.parse(t);
      msg = j.error || j.message || t;
    } catch {
      /* ignore */
    }
    throw new Error(msg || res.statusText);
  }
  const reader = res.body.getReader();
  const decoder = new TextDecoder();
  let buf = '';

  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    buf += decoder.decode(value, { stream: true });
    for (;;) {
      const nl = buf.indexOf('\n');
      if (nl < 0) break;
      const row = buf.slice(0, nl).trim();
      buf = buf.slice(nl + 1);
      if (!row) continue;
      let obj;
      try {
        obj = JSON.parse(row);
      } catch {
        continue;
      }
      if (obj.error) throw new Error(obj.error);
      if (obj.done) return;
      if (obj.line !== undefined && obj.line !== null) onLine(obj.line);
    }
  }
}
