import test, { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import {
  isAdmin,
  isOrganizer,
  shouldShowAdminNavigation,
  shouldShowReportsNavigation,
  getRouteAccessDecision,
  handleApiAuthError,
  removeRoleFromUser,
} from './routeSecurity.js';
import { formatReportApiError } from '../services/adminReportsService.js';

describe('Admin Dynamic Reports Security & Frontend Route Protection (EP-380 / US-39)', () => {
  const adminUser = {
    id: 'admin-001',
    email: 'admin@eventpulse.lk',
    roles: ['Administrator'],
  };

  const customerUser = {
    id: 'customer-002',
    email: 'customer@eventpulse.lk',
    roles: ['Customer'],
  };

  const organizerUser = {
    id: 'organizer-003',
    email: 'organizer@eventpulse.lk',
    roles: ['Organizer'],
  };

  const multiRoleAdminUser = {
    id: 'multi-004',
    email: 'multi@eventpulse.lk',
    roles: ['Customer', 'Organizer', 'Administrator'],
  };

  // =========================================================================
  // 1. Direct navigation by role
  // =========================================================================
  it('1. Administrator navigates directly to /admin/reports -> access is ALLOWED', () => {
    const decision = getRouteAccessDecision({
      isAuthenticated: true,
      user: adminUser,
      requiredRole: 'Administrator',
    });

    assert.equal(decision.status, 'ALLOWED');
    assert.equal(decision.redirectTo, undefined);
  });

  it('2. Authenticated Customer attempts direct navigation to /admin/reports -> ACCESS_DENIED and redirect to /forbidden', () => {
    const decision = getRouteAccessDecision({
      isAuthenticated: true,
      user: customerUser,
      requiredRole: 'Administrator',
    });

    assert.equal(decision.status, 'ACCESS_DENIED');
    assert.equal(decision.redirectTo, '/forbidden');
  });

  it('3. Authenticated Organizer attempts direct navigation to /admin/reports -> ACCESS_DENIED and redirect to /forbidden', () => {
    const decision = getRouteAccessDecision({
      isAuthenticated: true,
      user: organizerUser,
      requiredRole: 'Administrator',
    });

    assert.equal(decision.status, 'ACCESS_DENIED');
    assert.equal(decision.redirectTo, '/forbidden');
  });

  it('4. Logged-out visitor attempts direct navigation to /admin/reports -> REDIRECT_LOGIN to /login', () => {
    const decision = getRouteAccessDecision({
      isAuthenticated: false,
      user: null,
      requiredRole: 'Administrator',
    });

    assert.equal(decision.status, 'REDIRECT_LOGIN');
    assert.equal(decision.redirectTo, '/login');
  });

  it('5. Multi-role user possessing Administrator role -> access is ALLOWED', () => {
    const decision = getRouteAccessDecision({
      isAuthenticated: true,
      user: multiRoleAdminUser,
      requiredRole: 'Administrator',
    });

    assert.equal(decision.status, 'ALLOWED');
  });

  // =========================================================================
  // 2. Loading state prevents UI or report data leak
  // =========================================================================
  it('6. Authentication loading state -> returns LOADING and avoids premature redirect or UI leak', () => {
    const decision = getRouteAccessDecision({
      isAuthenticated: true,
      user: adminUser,
      requiredRole: 'Administrator',
      isLoading: true,
    });

    assert.equal(decision.status, 'LOADING');
  });

  // =========================================================================
  // 3. Navigation visibility rules (EP-380 Requirement 4)
  // =========================================================================
  it('7. Reports navigation visibility: visible ONLY for authenticated Administrator', () => {
    // Administrator -> visible
    assert.equal(shouldShowReportsNavigation(adminUser, true), true);
    assert.equal(shouldShowAdminNavigation(adminUser, true), true);
    assert.equal(isAdmin(adminUser, true), true);

    // Multi-role admin -> visible
    assert.equal(shouldShowReportsNavigation(multiRoleAdminUser, true), true);

    // Customer -> hidden
    assert.equal(shouldShowReportsNavigation(customerUser, true), false);
    assert.equal(isAdmin(customerUser, true), false);

    // Organizer -> hidden
    assert.equal(shouldShowReportsNavigation(organizerUser, true), false);
    assert.equal(isAdmin(organizerUser, true), false);

    // Logged-out visitor -> hidden
    assert.equal(shouldShowReportsNavigation(null, false), false);
    assert.equal(shouldShowReportsNavigation(adminUser, false), false);
  });

  // =========================================================================
  // 4. API 401 & 403 Response Handling (EP-380 Requirement 5 & 6)
  // =========================================================================
  it('8. Reporting API returns 401 -> clears auth, redirects to login with return URL, avoids loop', () => {
    let authCleared = false;
    const result = handleApiAuthError({
      status: 401,
      url: '/api/admin/reports/bookings',
      currentPath: '/admin/reports',
      clearAuth: () => {
        authCleared = true;
      },
    });

    assert.equal(result.handled, true);
    assert.equal(result.action, 'REDIRECT_LOGIN');
    assert.equal(result.redirectTo, '/login?from=%2Fadmin%2Freports');
    assert.equal(authCleared, true);

    // Loop prevention when already on /login
    const loopResult = handleApiAuthError({
      status: 401,
      url: '/api/admin/reports/bookings',
      currentPath: '/login',
    });
    assert.equal(loopResult.action, 'NO_OP_ALREADY_ON_LOGIN');
  });

  it('9. Reporting API returns 403 -> revokes stale Administrator role, redirects to /forbidden, avoids loop', () => {
    let staleRole = null;
    const result = handleApiAuthError({
      status: 403,
      url: '/api/admin/reports/revenue',
      currentPath: '/admin/reports',
      onStaleRole: (role) => {
        staleRole = role;
      },
    });

    assert.equal(result.handled, true);
    assert.equal(result.action, 'SHOW_ACCESS_DENIED');
    assert.equal(result.redirectTo, '/forbidden');
    assert.equal(result.staleRoleRemoved, true);
    assert.equal(staleRole, 'Administrator');

    // Loop prevention when already on /forbidden
    const loopResult = handleApiAuthError({
      status: 403,
      url: '/api/admin/reports/revenue',
      currentPath: '/forbidden',
    });
    assert.equal(loopResult.action, 'NO_OP_ALREADY_ON_FORBIDDEN');
  });

  // =========================================================================
  // 5. Active session role revocation
  // =========================================================================
  it('10. Administrator role revoked during active session -> immediately revokes report access and navigation', () => {
    const revokedUser = removeRoleFromUser(adminUser, 'Administrator');
    assert.deepEqual(revokedUser.roles, []);

    // Navigation immediately hidden
    assert.equal(shouldShowReportsNavigation(revokedUser, true), false);

    // Route guard denies access
    const decision = getRouteAccessDecision({
      isAuthenticated: true,
      user: revokedUser,
      requiredRole: 'Administrator',
    });
    assert.equal(decision.status, 'ACCESS_DENIED');
    assert.equal(decision.redirectTo, '/forbidden');
  });

  // =========================================================================
  // 6. Sanitized Reporting API error formatting
  // =========================================================================
  it('11. formatReportApiError formats 401, 403, and validation errors safely without leaking internals', () => {
    assert.equal(
      formatReportApiError({ status: 401 }),
      'Authentication required or your session has expired. Please log in again.'
    );

    assert.equal(
      formatReportApiError({ status: 403 }),
      'Access denied. You must be a platform Administrator to access dynamic reports.'
    );

    assert.equal(
      formatReportApiError({ data: { code: 'INVALID_DATE_RANGE' } }),
      'The start date must be earlier than or equal to the end date.'
    );

    assert.equal(
      formatReportApiError({ data: { code: 'DATE_RANGE_EXCEEDED' } }),
      'The selected date range exceeds the maximum allowed period of 730 days (2 years).'
    );
  });
});
