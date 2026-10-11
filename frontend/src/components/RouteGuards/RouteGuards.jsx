import React from 'react';
import { Navigate, useLocation, Outlet } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { getRouteAccessDecision } from '../../utils/routeSecurity';

/**
 * Route guard requiring user to be authenticated.
 * If unauthenticated, redirects to /login with state preserved for post-login return.
 */
export function RequireAuth({ children }) {
  const { isAuthenticated, isLoading } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return (
      <div
        data-testid="auth-loading"
        aria-busy="true"
        aria-label="Checking authentication"
        style={{
          minHeight: '100vh',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          backgroundColor: 'var(--ep-canvas)',
        }}
      >
        <div className="spinner-border text-primary" role="status" style={{ width: '2.5rem', height: '2.5rem' }}>
          <span className="visually-hidden">Loading...</span>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return children ? children : <Outlet />;
}

/**
 * Route guard requiring user to have one or more specific roles.
 * - If unauthenticated -> redirects to /login with state preserved.
 * - If authenticated but missing required role(s) -> redirects to /forbidden.
 */
export function RequireRole({ allowedRoles, children }) {
  const { isAuthenticated, currentUser, isLoading } = useAuth();
  const location = useLocation();

  const decision = getRouteAccessDecision({
    isAuthenticated,
    user: currentUser,
    roles: currentUser?.roles,
    requiredRole: allowedRoles,
    isLoading,
  });

  if (decision.status === 'LOADING') {
    return (
      <div
        data-testid="role-loading"
        aria-busy="true"
        aria-label="Verifying role permissions"
        style={{
          minHeight: '100vh',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          backgroundColor: 'var(--ep-canvas)',
        }}
      >
        <div className="spinner-border text-primary" role="status" style={{ width: '2.5rem', height: '2.5rem' }}>
          <span className="visually-hidden">Loading...</span>
        </div>
      </div>
    );
  }

  if (decision.status === 'REDIRECT_LOGIN') {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  if (decision.status === 'ACCESS_DENIED') {
    return <Navigate to="/forbidden" replace />;
  }

  return children ? children : <Outlet />;
}

/**
 * Specialized, reusable Admin route guard (EP-373).
 * 
 * Guarantees:
 * - Logged-out user -> Redirect to /login (preserving return location).
 * - Authenticated Customer -> Access Denied (/forbidden).
 * - Authenticated Organizer -> Access Denied (/forbidden).
 * - Authenticated Administrator -> Renders protected admin content.
 * - While auth is being verified -> Accessible loading state (prevents leaking admin UI or premature redirect).
 * 
 * Usable both as an outlet layout route: `<Route element={<AdminRoute />}>`
 * and as a direct component wrapper: `<AdminRoute><AdminComponent /></AdminRoute>`.
 */
export function AdminRoute({ children }) {
  const { isAuthenticated, currentUser, isLoading } = useAuth();
  const location = useLocation();

  const decision = getRouteAccessDecision({
    isAuthenticated,
    user: currentUser,
    roles: currentUser?.roles,
    requiredRole: 'Administrator',
    isLoading,
  });

  if (decision.status === 'LOADING') {
    return (
      <div
        data-testid="admin-route-loading"
        aria-busy="true"
        aria-label="Verifying administrative access"
        style={{
          minHeight: '100vh',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          backgroundColor: 'var(--ep-canvas)',
        }}
      >
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '12px' }}>
          <div className="spinner-border text-primary" role="status" style={{ width: '2.5rem', height: '2.5rem' }}>
            <span className="visually-hidden">Verifying authorization...</span>
          </div>
          <span style={{ fontSize: '14px', color: 'var(--ep-text-secondary)', fontWeight: 500 }}>
            Verifying administrative access...
          </span>
        </div>
      </div>
    );
  }

  if (decision.status === 'REDIRECT_LOGIN') {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  if (decision.status === 'ACCESS_DENIED') {
    return <Navigate to="/forbidden" replace />;
  }

  return children ? children : <Outlet />;
}
