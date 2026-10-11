import React, { createContext, useContext, useState, useEffect } from 'react';

const TOKEN_KEY = 'ep_access_token';
const USER_KEY = 'ep_user';

const AuthContext = createContext(null);

function isTokenExpired(token) {
  if (!token) return true;
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    if (!payload.exp) return false; // no exp claim — treat as non-expiring
    return Date.now() >= payload.exp * 1000;
  } catch {
    return true; // malformed token — treat as invalid
  }
}

export function AuthProvider({ children, initialLoading = false }) {
  const [accessToken, setAccessToken] = useState(() => sessionStorage.getItem(TOKEN_KEY) || null);
  const [currentUser, setCurrentUser] = useState(() => {
    try {
      const savedUser = sessionStorage.getItem(USER_KEY);
      return savedUser ? JSON.parse(savedUser) : null;
    } catch {
      return null;
    }
  });
  const [isLoading, setIsLoading] = useState(initialLoading);

  const isAuthenticated = Boolean(accessToken) && !isTokenExpired(accessToken);

  const loginUser = (authData) => {
    if (!authData || !authData.accessToken) return;
    
    sessionStorage.setItem(TOKEN_KEY, authData.accessToken);
    sessionStorage.setItem(USER_KEY, JSON.stringify(authData.user));

    setAccessToken(authData.accessToken);
    setCurrentUser(authData.user);
    setIsLoading(false);
  };

  const logoutUser = () => {
    sessionStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem(USER_KEY);
    try {
      localStorage.removeItem(TOKEN_KEY);
      localStorage.removeItem(USER_KEY);
    } catch {
      // Storage access may be restricted
    }
    setAccessToken(null);
    setCurrentUser(null);
    setIsLoading(false);
  };

  const updateCurrentUser = (partialUser) => {
    setCurrentUser((prev) => {
      const updated = { ...(prev || {}), ...(partialUser || {}) };
      sessionStorage.setItem(USER_KEY, JSON.stringify(updated));
      return updated;
    });
  };

  const removeRole = (roleToRemove) => {
    setCurrentUser((prev) => {
      if (!prev || !Array.isArray(prev.roles)) return prev;
      const updatedRoles = prev.roles.filter((r) => r !== roleToRemove);
      const updated = { ...prev, roles: updatedRoles };
      sessionStorage.setItem(USER_KEY, JSON.stringify(updated));
      return updated;
    });
  };

  const hasRole = (role) => {
    if (!isAuthenticated || !currentUser || !Array.isArray(currentUser.roles)) return false;
    return currentUser.roles.includes(role);
  };

  const hasAnyRole = (roles) => {
    if (!isAuthenticated || !currentUser || !Array.isArray(currentUser.roles)) return false;
    return roles.some((r) => currentUser.roles.includes(r));
  };

  // Global authorization listeners to handle stale sessions & 401/403 API responses
  useEffect(() => {
    const handleUnauthorized = () => {
      logoutUser();
    };

    const handleForbidden = (event) => {
      const url = event?.detail?.url || '';
      // If administrative endpoint returned 403, strip stale Administrator role immediately
      if (url.includes('/admin') || url.includes('/api/admin')) {
        removeRole('Administrator');
      }
    };

    if (typeof window !== 'undefined') {
      window.addEventListener('ep:auth-unauthorized', handleUnauthorized);
      window.addEventListener('ep:auth-forbidden', handleForbidden);
    }

    return () => {
      if (typeof window !== 'undefined') {
        window.removeEventListener('ep:auth-unauthorized', handleUnauthorized);
        window.removeEventListener('ep:auth-forbidden', handleForbidden);
      }
    };
  }, []);

  const value = {
    accessToken,
    currentUser,
    isAuthenticated,
    isLoading,
    setIsLoading,
    login: loginUser,
    logout: logoutUser,
    updateCurrentUser,
    removeRole,
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
