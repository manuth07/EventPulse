/**
 * Security and Route Guard Utilities (EP-373 — Admin Management: Frontend Route Protection)
 * 
 * Provides centralized, role-based authorization decision logic, navigation visibility filters,
 * and API authorization error handlers (HTTP 401 / 403) with infinite-loop prevention.
 */

const TOKEN_KEY = 'ep_access_token';
const USER_KEY = 'ep_user';

/**
 * Checks whether a user is an authenticated Administrator.
 * @param {Object|null} user - The current user object.
 * @param {boolean} isAuthenticated - Whether the user possesses a valid, non-expired token.
 * @returns {boolean} True only if authenticated AND user has the Administrator role.
 */
export function isAdmin(user, isAuthenticated) {
  if (!isAuthenticated || !user || !Array.isArray(user.roles)) {
    return false;
  }
  return user.roles.includes('Administrator');
}

/**
 * Determines whether administrative navigation entries (links, badges, menus)
 * should be displayed in the UI.
 * @param {Object|null} user - The current user object.
 * @param {boolean} isAuthenticated - Authentication state.
 * @returns {boolean} True if and only if authenticated as an Administrator.
 */
export function shouldShowAdminNavigation(user, isAuthenticated) {
  return isAdmin(user, isAuthenticated);
}

/**
 * Checks whether a user is an authenticated Organizer (EP-386).
 * @param {Object|null} user - The current user object.
 * @param {boolean} isAuthenticated - Whether the user possesses a valid, non-expired token.
 * @returns {boolean} True only if authenticated AND user has the Organizer role.
 */
export function isOrganizer(user, isAuthenticated) {
  if (!isAuthenticated || !user || !Array.isArray(user.roles)) {
    return false;
  }
  return user.roles.includes('Organizer');
}

/**
 * Determines whether organizer navigation entries should be displayed in the UI.
 * @param {Object|null} user - The current user object.
 * @param {boolean} isAuthenticated - Authentication state.
 * @returns {boolean} True if and only if authenticated as an Organizer.
 */
export function shouldShowOrganizerNavigation(user, isAuthenticated) {
  return isOrganizer(user, isAuthenticated);
}

/**
 * Determines whether organizer sales analytics navigation entries (links, badges, menus)
 * should be displayed in the UI (EP-386).
 * Ensures private organizer sales analytics are strictly hidden from customers, logged-out users,
 * and administrators without the Organizer role.
 * 
 * @param {Object|null} user - The current user object.
 * @param {boolean} isAuthenticated - Authentication state.
 * @returns {boolean} True if and only if authenticated as an Organizer.
 */
export function shouldShowSalesAnalyticsNavigation(user, isAuthenticated) {
  return isOrganizer(user, isAuthenticated);
}

/**
 * Determines whether organizer booking analytics navigation entries (links, badges, menus)
 * should be displayed in the UI (EP-391).
 * Ensures private organizer booking analytics are strictly hidden from customers, logged-out users,
 * and administrators without the Organizer role.
 * 
 * @param {Object|null} user - The current user object.
 * @param {boolean} isAuthenticated - Authentication state.
 * @returns {boolean} True if and only if authenticated as an Organizer.
 */
export function shouldShowBookingAnalyticsNavigation(user, isAuthenticated) {
  return isOrganizer(user, isAuthenticated);
}

/**
 * Determines whether administrative reports navigation entries (links, badges, menus)
 * should be displayed in the UI (EP-380).
 * Ensures reporting links are strictly hidden from customers, organizers, and logged-out visitors.
 * 
 * @param {Object|null} user - The current user object.
 * @param {boolean} isAuthenticated - Authentication state.
 * @returns {boolean} True if and only if authenticated as an Administrator.
 */
export function shouldShowReportsNavigation(user, isAuthenticated) {
  return isAdmin(user, isAuthenticated);
}

/**
 * Evaluates route access decisions for guarded components.
 * 
 * Rules:
 * - If still verifying authentication (isLoading = true) -> LOADING (do not render content or redirect prematurely)
 * - If unauthenticated (!isAuthenticated) -> REDIRECT_LOGIN (/login)
 * - If authenticated but missing required role -> ACCESS_DENIED (/forbidden)
 * - If authenticated with required role -> ALLOWED
 * 
 * @param {Object} params
 * @param {boolean} params.isAuthenticated - Current authentication flag
 * @param {Object|null} [params.user] - Current user object
 * @param {Array<string>} [params.roles] - User's current roles array
 * @param {string|Array<string>} [params.requiredRole='Administrator'] - Role(s) required for access
 * @param {boolean} [params.isLoading=false] - Whether authentication is actively being verified
 * @returns {{ status: 'LOADING'|'REDIRECT_LOGIN'|'ACCESS_DENIED'|'ALLOWED', redirectTo?: string }}
 */
export function getRouteAccessDecision({
  isAuthenticated,
  user = null,
  roles = null,
  requiredRole = 'Administrator',
  isLoading = false,
}) {
  if (isLoading) {
    return { status: 'LOADING' };
  }

  if (!isAuthenticated) {
    return {
      status: 'REDIRECT_LOGIN',
      redirectTo: '/login',
    };
  }

  const effectiveRoles = Array.isArray(roles)
    ? roles
    : (user && Array.isArray(user.roles) ? user.roles : []);

  const requiredRolesArray = Array.isArray(requiredRole)
    ? requiredRole
    : [requiredRole];

  const hasPermission = requiredRolesArray.some((r) => effectiveRoles.includes(r));

  if (!hasPermission) {
    return {
      status: 'ACCESS_DENIED',
      redirectTo: '/forbidden',
    };
  }

  return { status: 'ALLOWED' };
}

/**
 * Handles API authorization errors (401 Unauthorized and 403 Forbidden).
 * Enforces:
 * - 401: Clears authentication storage and directs to /login, preventing redirect loops if already on login.
 * - 403: Navigates to /forbidden, preventing redirect loops if already on forbidden, and strips stale roles.
 * 
 * @param {Object} params
 * @param {number} params.status - HTTP response status code
 * @param {string} [params.url=''] - Requested API endpoint URL
 * @param {string} [params.currentPath='/'] - Current browser pathname
 * @param {Function} [params.clearAuth] - Optional callback to clear auth state
 * @param {Function} [params.onStaleRole] - Optional callback to notify stale role removal
 * @returns {{ handled: boolean, action?: string, redirectTo?: string, staleRoleRemoved?: boolean }}
 */
export function handleApiAuthError({
  status,
  url = '',
  currentPath = '/',
  clearAuth = null,
  onStaleRole = null,
}) {
  if (status === 401) {
    // Clear storage if available
    try {
      sessionStorage.removeItem(TOKEN_KEY);
      sessionStorage.removeItem(USER_KEY);
      localStorage.removeItem(TOKEN_KEY);
      localStorage.removeItem(USER_KEY);
    } catch {
      // Storage access may be restricted in non-browser test environments
    }

    if (typeof clearAuth === 'function') {
      clearAuth();
    }

    // Prevent endless redirect loops if already on login or registration pages
    if (currentPath === '/login' || currentPath === '/register') {
      return {
        handled: true,
        action: 'NO_OP_ALREADY_ON_LOGIN',
      };
    }

    const returnUrl = encodeURIComponent(currentPath);
    const redirectTo = currentPath && currentPath !== '/'
      ? `/login?from=${returnUrl}`
      : '/login';

    return {
      handled: true,
      action: 'REDIRECT_LOGIN',
      redirectTo,
    };
  }

  if (status === 403) {
    let staleRoleRemoved = false;

    // Detect stale administrator role if hitting admin endpoints
    const isTargetingAdmin = url.includes('/admin') || url.includes('/api/admin') || currentPath.startsWith('/admin');
    if (isTargetingAdmin && typeof onStaleRole === 'function') {
      onStaleRole('Administrator');
      staleRoleRemoved = true;
    }

    // Detect stale organizer role if hitting organizer endpoints (EP-386)
    const isTargetingOrganizer = url.includes('/organizer') || url.includes('/api/organizer') || currentPath.startsWith('/organizer');
    if (isTargetingOrganizer && typeof onStaleRole === 'function') {
      onStaleRole('Organizer');
      staleRoleRemoved = true;
    }

    // Prevent endless redirect loops if already on forbidden page
    if (currentPath === '/forbidden') {
      return {
        handled: true,
        action: 'NO_OP_ALREADY_ON_FORBIDDEN',
        staleRoleRemoved,
      };
    }

    return {
      handled: true,
      action: 'SHOW_ACCESS_DENIED',
      redirectTo: '/forbidden',
      staleRoleRemoved,
    };
  }

  return { handled: false };
}

/**
 * Removes a specific role from a user object without modifying the original object.
 * Used to handle role revocation in active sessions.
 * 
 * @param {Object|null} user - The current user object.
 * @param {string} roleToRemove - Role to revoke (e.g., 'Administrator').
 * @returns {Object|null} Updated user object with the specified role removed.
 */
export function removeRoleFromUser(user, roleToRemove) {
  if (!user || !Array.isArray(user.roles)) {
    return user;
  }
  return {
    ...user,
    roles: user.roles.filter((r) => r !== roleToRemove),
  };
}
