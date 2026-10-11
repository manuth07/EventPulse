import test, { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import {
  isAdmin,
  shouldShowAdminNavigation,
  getRouteAccessDecision,
  handleApiAuthError,
  removeRoleFromUser,
} from './routeSecurity.js';

describe('Route Security & Admin Frontend Protection (EP-373)', () => {
  const adminUser = {
    id: 'admin-123',
    email: 'admin@eventpulse.lk',
    roles: ['Administrator'],
  };

  const customerUser = {
    id: 'customer-123',
    email: 'customer@eventpulse.lk',
    roles: ['Customer'],
  };

  const organizerUser = {
    id: 'organizer-123',
    email: 'organizer@eventpulse.lk',
    roles: ['Organizer'],
  };

  const multiRoleAdminUser = {
    id: 'super-123',
    email: 'super@eventpulse.lk',
    roles: ['Customer', 'Organizer', 'Administrator'],
  };

  // =========================================================================
  // 1. Admin opens admin route
  // =========================================================================
  it('1. Admin opens admin route -> access is ALLOWED', () => {
    const decision = getRouteAccessDecision({
      isAuthenticated: true,
      user: adminUser,
      requiredRole: 'Administrator',
    });

    assert.equal(decision.status, 'ALLOWED');
    assert.equal(decision.redirectTo, undefined);

    // Multi-role admin also allowed
    const multiRoleDecision = getRouteAccessDecision({
      isAuthenticated: true,
      roles: multiRoleAdminUser.roles,
      requiredRole: 'Administrator',
    });
    assert.equal(multiRoleDecision.status, 'ALLOWED');
  });

  // =========================================================================
  // 2. Customer opens admin URL manually
  // =========================================================================
  it('2. Customer opens admin URL manually -> ACCESS_DENIED and redirect to /forbidden', () => {
    const decision = getRouteAccessDecision({
      isAuthenticated: true,
      user: customerUser,
      requiredRole: 'Administrator',
    });

    assert.equal(decision.status, 'ACCESS_DENIED');
    assert.equal(decision.redirectTo, '/forbidden');
  });

  // =========================================================================
  // 3. Organizer opens admin URL manually
  // =========================================================================
  it('3. Organizer opens admin URL manually -> ACCESS_DENIED and redirect to /forbidden', () => {
    const decision = getRouteAccessDecision({
      isAuthenticated: true,
      user: organizerUser,
      requiredRole: 'Administrator',
    });

    assert.equal(decision.status, 'ACCESS_DENIED');
    assert.equal(decision.redirectTo, '/forbidden');
  });

  // =========================================================================
  // 4. Logged-out user opens admin URL
  // =========================================================================
  it('4. Logged-out user opens admin URL -> REDIRECT_LOGIN to /login', () => {
    const decision = getRouteAccessDecision({
      isAuthenticated: false,
      user: null,
      requiredRole: 'Administrator',
    });

    assert.equal(decision.status, 'REDIRECT_LOGIN');
    assert.equal(decision.redirectTo, '/login');

    // Even if stale admin user object is lingering in memory, unauthenticated must redirect to /login
    const staleObjDecision = getRouteAccessDecision({
      isAuthenticated: false,
      user: adminUser,
      requiredRole: 'Administrator',
    });
    assert.equal(staleObjDecision.status, 'REDIRECT_LOGIN');
    assert.equal(staleObjDecision.redirectTo, '/login');
  });

  // =========================================================================
  // 5. Admin menu visible for Admin
  // =========================================================================
  it('5. Admin menu visible for Admin -> shouldShowAdminNavigation returns true', () => {
    assert.equal(isAdmin(adminUser, true), true);
    assert.equal(shouldShowAdminNavigation(adminUser, true), true);
    assert.equal(shouldShowAdminNavigation(multiRoleAdminUser, true), true);
  });

  // =========================================================================
  // 6. Admin menu hidden for Customer
  // =========================================================================
  it('6. Admin menu hidden for Customer -> shouldShowAdminNavigation returns false', () => {
    assert.equal(isAdmin(customerUser, true), false);
    assert.equal(shouldShowAdminNavigation(customerUser, true), false);
  });

  // =========================================================================
  // 7. Admin menu hidden for Organizer
  // =========================================================================
  it('7. Admin menu hidden for Organizer -> shouldShowAdminNavigation returns false', () => {
    assert.equal(isAdmin(organizerUser, true), false);
    assert.equal(shouldShowAdminNavigation(organizerUser, true), false);
  });

  it('7b. Admin menu hidden for unauthenticated visitors even with admin data', () => {
    assert.equal(isAdmin(adminUser, false), false);
    assert.equal(shouldShowAdminNavigation(adminUser, false), false);
    assert.equal(shouldShowAdminNavigation(null, false), false);
  });

  // =========================================================================
  // 8. API returns 401
  // =========================================================================
  it('8. API returns 401 -> clears auth and redirects to /login without endless loop', () => {
    let authCleared = false;
    const clearAuth = () => {
      authCleared = true;
    };

    // Ordinary route when 401 occurs
    const result = handleApiAuthError({
      status: 401,
      url: '/api/admin/users',
      currentPath: '/admin',
      clearAuth,
    });

    assert.equal(result.handled, true);
    assert.equal(result.action, 'REDIRECT_LOGIN');
    assert.equal(result.redirectTo, '/login?from=%2Fadmin');
    assert.equal(authCleared, true);

    // When already on /login -> NO_OP to avoid infinite redirect loops
    authCleared = false;
    const loginResult = handleApiAuthError({
      status: 401,
      url: '/api/auth/login',
      currentPath: '/login',
      clearAuth,
    });

    assert.equal(loginResult.handled, true);
    assert.equal(loginResult.action, 'NO_OP_ALREADY_ON_LOGIN');
    assert.equal(authCleared, true);

    // When on /register -> NO_OP to avoid infinite redirect loops
    const registerResult = handleApiAuthError({
      status: 401,
      url: '/api/auth/register',
      currentPath: '/register',
    });
    assert.equal(registerResult.action, 'NO_OP_ALREADY_ON_LOGIN');
  });

  // =========================================================================
  // 9. API returns 403
  // =========================================================================
  it('9. API returns 403 -> shows Access Denied and detects stale roles without endless loop', () => {
    let staleRoleNotified = null;
    const onStaleRole = (role) => {
      staleRoleNotified = role;
    };

    // User receives 403 accessing admin endpoint
    const result = handleApiAuthError({
      status: 403,
      url: '/api/admin/events/pending',
      currentPath: '/admin/events/pending',
      onStaleRole,
    });

    assert.equal(result.handled, true);
    assert.equal(result.action, 'SHOW_ACCESS_DENIED');
    assert.equal(result.redirectTo, '/forbidden');
    assert.equal(result.staleRoleRemoved, true);
    assert.equal(staleRoleNotified, 'Administrator');

    // When already on /forbidden -> NO_OP to avoid infinite redirect loops
    const forbiddenResult = handleApiAuthError({
      status: 403,
      url: '/api/admin/overview',
      currentPath: '/forbidden',
    });

    assert.equal(forbiddenResult.handled, true);
    assert.equal(forbiddenResult.action, 'NO_OP_ALREADY_ON_FORBIDDEN');
  });

  // =========================================================================
  // 10. Authentication loading state
  // =========================================================================
  it('10. Authentication loading state -> returns LOADING and avoids premature redirect or content leak', () => {
    // When loading, even if authenticated is not yet established
    const loadingUnauth = getRouteAccessDecision({
      isAuthenticated: false,
      user: null,
      requiredRole: 'Administrator',
      isLoading: true,
    });

    assert.equal(loadingUnauth.status, 'LOADING');
    assert.equal(loadingUnauth.redirectTo, undefined);

    // When loading with admin user
    const loadingAdmin = getRouteAccessDecision({
      isAuthenticated: true,
      user: adminUser,
      requiredRole: 'Administrator',
      isLoading: true,
    });

    assert.equal(loadingAdmin.status, 'LOADING');
    assert.equal(loadingAdmin.redirectTo, undefined);
  });

  // =========================================================================
  // 11. Admin role removed during active session
  // =========================================================================
  it('11. Admin role removed during active session -> immediately revoked from navigation and route guard', () => {
    // Session starts as active Administrator
    assert.equal(shouldShowAdminNavigation(adminUser, true), true);
    assert.equal(getRouteAccessDecision({ isAuthenticated: true, user: adminUser }).status, 'ALLOWED');

    // Backend revokes Administrator role (e.g. EP-371 role demotion to Customer)
    const updatedUser = removeRoleFromUser(adminUser, 'Administrator');
    assert.deepEqual(updatedUser.roles, []);

    // Active session immediately loses Admin navigation visibility
    assert.equal(shouldShowAdminNavigation(updatedUser, true), false);

    // Subsequent route attempt immediately denied with /forbidden redirect
    const postRevocationDecision = getRouteAccessDecision({
      isAuthenticated: true,
      user: updatedUser,
      requiredRole: 'Administrator',
    });

    assert.equal(postRevocationDecision.status, 'ACCESS_DENIED');
    assert.equal(postRevocationDecision.redirectTo, '/forbidden');
  });
});
