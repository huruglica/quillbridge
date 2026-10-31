import { useSearchParams } from 'react-router';

// page and page size live in the URL so back/forward and reloads keep your place
export function usePaging(defaultSize = 20) {
  const [params, setParams] = useSearchParams();
  const page = Math.max(1, Number(params.get('page')) || 1);
  const size = Number(params.get('size')) || defaultSize;

  const set = (next) =>
    setParams((current) => {
      const merged = new URLSearchParams(current);
      Object.entries(next).forEach(([key, value]) => merged.set(key, String(value)));
      return merged;
    });

  return { page, size, setPage: (p) => set({ page: p }), setSize: (s) => set({ size: s, page: 1 }) };
}
