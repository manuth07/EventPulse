import React, { createContext, useContext, useState, useEffect } from 'react';

const TOKEN_KEY = 'ep_access_token';
const USER_KEY = 'ep_user';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [accessToken, setAccessToken] = useState(() => sessionStorage.getItem(TOKEN_KEY) || null);
  const [currentUser, setCurrentUser] = useState(() => {
    try {
      const savedUser = sessionStorage.getItem(USER_KEY);
      return savedUser ? JSON.parse(savedUser) : null;
    } catch {
      return null;
    }
  });

  const loginUser = (authData) => {
    if (!authData || !authData.accessToken) return;
    
    sessionStorage.setItem(TOKEN_KEY, authData.accessToken);
    sessionStorage.setItem(USER_KEY, JSON.stringify(authData.user));

    setAccessToken(authData.accessToken);
    setCurrentUser(authData.user);
  };

  const logoutUser = () => {
    sessionStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem(USER_KEY);
    setAccessToken(null);
    setCurrentUser(null);
  };

  const hasRole = (role) => {
    if (!currentUser || !Array.isArray(currentUser.roles)) return false;
    return currentUser.roles.includes(role);
  };

  const hasAnyRole = (roles) => {
    if (!currentUser || !Array.isArray(currentUser.roles)) return false;
    return roles.some((r) => currentUser.roles.includes(r));
  };

  const value = {
    accessToken,
    currentUser,
    isAuthenticated: Boolean(accessToken),
    login: loginUser,
    logout: logoutUser,
    hasRole,
    hasAnyRole,
  };

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
