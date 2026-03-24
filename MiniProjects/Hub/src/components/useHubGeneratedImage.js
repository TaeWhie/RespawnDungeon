import { useEffect, useMemo, useState } from 'react';
import { apiResolveHubImage, apiTranslateHubImagePrompt } from '../api';

export default function useHubGeneratedImage({ scope, entityKey, prompt, width = 768, height = 768, themeId = 'guildhub-2d-v1' }) {
  const [imageUrl, setImageUrl] = useState(null);
  const [status, setStatus] = useState('idle');
  const [error, setError] = useState(null);
  const stablePrompt = useMemo(() => (prompt || '').trim(), [prompt]);

  useEffect(() => {
    if (!stablePrompt) {
      setImageUrl(null);
      setStatus('idle');
      setError(null);
      return undefined;
    }
    let cancelled = false;
    const start = async () => {
      try {
        setError(null);
        const t = await apiTranslateHubImagePrompt(stablePrompt);
        if (cancelled) return;
        const translatedPrompt = String(t?.translatedPrompt || '').trim() || stablePrompt;
        const r = await apiResolveHubImage({ scope, entityKey, prompt: translatedPrompt, width, height, themeId });
        if (cancelled) return;
        if (r.status === 'ready' && r.imageUrl) {
          setImageUrl(r.imageUrl);
          setStatus('ready');
          return;
        }
        setStatus('error');
        setError(r.error || '이미지 생성 실패');
      } catch (e) {
        if (cancelled) return;
        setStatus('error');
        setError(e.message || String(e));
      }
    };
    start();
    return () => {
      cancelled = true;
    };
  }, [scope, entityKey, stablePrompt, width, height, themeId]);

  return { imageUrl, status, error, loading: status !== 'ready' && status !== 'error' };
}
