import React from 'react';
import { Navigate, useLocation, Outlet } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';

/// <summary>
/// Route guard requiring user to be authenticated.
/// If unauthenticated, redirects to /login with state preserved for post-login return.
/// </summary>
export function RequireAuth({ children }) {
  const { isAuthenticated } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return children ? children : <Outlet />;
}

/// <summary>
/// Route guard requiring user to have one or more specific roles.
/// - If unauthenticated -> redirects to /login.
/// - If authenticated but missing required role(s) -> redirects to /forbidden.
/// </summary>
export function RequireRole({ allowedRoles, children }) {
  const { isAuthenticated, hasAnyRole } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  const rolesArray = Array.isArray(allowedRoles) ? allowedRoles : [allowedRoles];

  if (!hasAnyRole(rolesArray)) {
    return <Navigate to="/forbidden" replace />;
  }

  return children ? children : <Outlet />;
}
