import { createContext, useContext, useSyncExternalStore, type ReactNode } from 'react';
import { haySesion, login as apiLogin, logout as apiLogout, suscribirseATokens } from '../api/client';

type AuthContextValue = {
  autenticado: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const autenticado = useSyncExternalStore(suscribirseATokens, haySesion);

  const value: AuthContextValue = {
    autenticado,
    login: apiLogin,
    logout: apiLogout,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth debe usarse dentro de <AuthProvider>.');
  return ctx;
}
