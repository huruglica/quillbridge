import { useState } from 'react';

const THEME_KEY = 'qb.theme';

function systemTheme() {
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

function apply(theme) {
  document.documentElement.dataset.theme = theme;
  try {
    localStorage.setItem(THEME_KEY, theme);
  } catch {
    // the choice just won't survive a reload
  }
}

export function useTheme() {
  const [theme, setTheme] = useState(() => document.documentElement.dataset.theme || systemTheme());

  const toggle = () => {
    const next = theme === 'dark' ? 'light' : 'dark';
    setTheme(next);

    // cross-fade the whole page where the browser supports view transitions
    if (document.startViewTransition) {
      document.startViewTransition(() => apply(next));
    } else {
      apply(next);
    }
  };

  return [theme, toggle];
}
