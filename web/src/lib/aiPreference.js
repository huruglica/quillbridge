import { useState } from 'react';

const AI_KEY = 'qb.useAi';

function read() {
  try {
    return localStorage.getItem(AI_KEY) !== 'false';
  } catch {
    return true;
  }
}

// chosen on the call list before opening a guide; the guide page generates with it
export function useAiPreference() {
  const [useAi, setUseAi] = useState(read);

  const set = (value) => {
    setUseAi(value);
    try {
      localStorage.setItem(AI_KEY, String(value));
    } catch {
      // the choice just won't survive a reload
    }
  };

  return [useAi, set];
}
