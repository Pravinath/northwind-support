import { useEffect, useMemo, useState } from 'react';
import { Navigate, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { authApi } from '../api/auth';
import { clearAccessToken, getAccessToken, setAccessToken } from '../api/client';
import { AuthContext } from './AuthContext';
import type { AuthContextValue } from './AuthContext';
import type { CurrentAgent } from '../types/api';
import { useAuth } from './AuthContext';

export function AuthProvider() {
  const [agent, setAgent] = useState<CurrentAgent | null>(null);
  const [isLoading, setIsLoading] = useState(() => Boolean(getAccessToken()));
  const navigate = useNavigate();
  const location = useLocation();

  useEffect(() => {
    let cancelled = false;
    if (!getAccessToken()) {
      return () => { cancelled = true; };
    }

    void authApi.me().then((current) => {
      if (!cancelled) setAgent(current);
    }).catch(() => {
      if (!cancelled) setAgent(null);
    }).finally(() => {
      if (!cancelled) setIsLoading(false);
    });

    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    const handleUnauthorized = () => {
      clearAccessToken();
      setAgent(null);
      setIsLoading(false);
      const from = `${location.pathname}${location.search}`;
      if (location.pathname !== '/login') {
        navigate('/login', { replace: true, state: { from } });
      }
    };

    window.addEventListener('northwind-support:unauthorized', handleUnauthorized);
    return () => window.removeEventListener('northwind-support:unauthorized', handleUnauthorized);
  }, [location.pathname, location.search, navigate]);

  const value = useMemo<AuthContextValue>(() => ({
    agent,
    isLoading,
    async login(email, password) {
      clearAccessToken();
      const result = await authApi.login(email, password);
      setAccessToken(result.accessToken);
      setAgent(result.agent);
    },
    logout() {
      clearAccessToken();
      setAgent(null);
      navigate('/login', { replace: true });
    },
  }), [agent, isLoading, navigate]);

  return <AuthContext.Provider value={value}><Outlet /></AuthContext.Provider>;
}

export function RequireAuth() {
  const { agent, isLoading } = useAuth();
  const location = useLocation();
  if (isLoading) return <main className="app__main" aria-busy="true">Checking session…</main>;
  if (!agent) {
    return <Navigate to="/login" replace state={{ from: `${location.pathname}${location.search}` }} />;
  }
  return <Outlet />;
}
