"use client"

import { useState, useEffect, createContext, useContext, ReactNode } from 'react';
import { getUserFromToken, clearStoredToken, logout as apiLogout } from './api';
import { AccountDetail } from './types';
import { useRouter } from 'next/navigation';

interface AuthContextType {
  user: AccountDetail | null; 
  login: (userData: AccountDetail) => void;
  logout: () => void;
  isLoading: boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AccountDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const router = useRouter();

  useEffect(() => {
    const initAuth = () => {
      const userData = getUserFromToken();
      setUser(userData);
      setIsLoading(false);
    };

    initAuth();
  }, []);

  const login = (userData: AccountDetail) => {
    setUser(userData);
  };

  const logout = async () => {
    try {
      await apiLogout();
    } catch (error) {
      console.error("Logout failed", error);
    } finally {
      setUser(null);
      clearStoredToken();
      router.push('/login');
    }
  };

  const value = {
    user,
    login,
    logout,
    isLoading,
  };

  return (
    <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
  );
}

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};