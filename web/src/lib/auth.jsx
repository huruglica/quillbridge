import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { api, onUnauthorized, session } from './api';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const queryClient = useQueryClient();
  const [current, setCurrent] = useState(() => session.get());

  const logout = useCallback(() => {
    session.clear();
    setCurrent(null);
    queryClient.clear();
  }, [queryClient]);

  const login = useCallback(async (email, password, remember) => {
    const result = await api.login(email, password);
    session.set(result, remember);
    return result;
  }, []);

  // the login page lets the mates celebrate before the app takes over
  const enter = useCallback(() => setCurrent(session.get()), []);

  useEffect(() => {
    onUnauthorized(logout);
  }, [logout]);

  // sign out when the token expires while the tab is open
  useEffect(() => {
    if (!current) return;

    const ms = new Date(current.expiresAt).getTime() - Date.now();
    const timer = setTimeout(logout, Math.max(ms, 0));

    return () => clearTimeout(timer);
  }, [current, logout]);

  const value = useMemo(
    () => ({ user: current?.user ?? null, isAuthenticated: !!current, login, enter, logout }),
    [current, login, enter, logout]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  return useContext(AuthContext);
}
