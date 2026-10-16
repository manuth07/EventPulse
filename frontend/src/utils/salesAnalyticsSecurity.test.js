import test, { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import {
  isOrganizer,
  shouldShowOrganizerNavigation,
  shouldShowSalesAnalyticsNavigation,
  getRouteAccessDecision,
  handleApiAuthError,
  removeRoleFromUser,
} from './routeSecurity.js';
import { formatSalesAnalyticsError } from '../services/salesAnalyticsService.js';

describe('Sales Analytics Security & Route Protection (EP-386)', () => {
  const organizerUser = {
    id: 'organizer-456',
    email: 'organizer@eventpulse.lk',
    roles: ['Organizer'],
  };

  const customerUser = {
    id: 'customer-123',
    email: 'customer@eventpulse.lk',
    roles: ['Customer'],
  };

  const adminOnlyUser = {
    id: 'admin-789',
    email: 'admin@eventpulse.lk',
    roles: ['Administrator'],
  };

  const multiRoleOrganizerUser = {
    id: 'super-admin-organizer',
    email: 'super@eventpulse.lk',
    roles: ['Administrator', 'Organizer'],
  };

  // =========================================================================
  // 1. Organizer frontend access
  // =========================================================================
  it('1. Organizer opens sales analytics route -> access is ALLOWED', () => {
    const decision = getRouteAccessDecision({
      isAuthenticated: true,
      user: organizerUser,
      requiredRole: 'Organizer',
    });

    assert.equal(decision.status, 'ALLOWED');
    assert.equal(decision.redirectTo, undefined);
  });

  // =========================================================================
  // 2. Customer frontend access
  // =========================================================================
  it('2. Customer opens sales analytics route manually -> ACCESS_DENIED and redirect to /forbidden', () => {
    const decision = getRouteAccessDecision({
      isAuthenticated: true,
      user: customerUser,
      requiredRole: 'Organizer',
    });

    assert.equal(decision.status, 'ACCESS_DENIED');
    assert.equal(decision.redirectTo, '/forbidden');
  });

  // =========================================================================
  // 3. Logged-out frontend access
  // =========================================================================
  it('3. Logged-out user opens sales analytics route -> REDIRECT_LOGIN to /login', () => {
    const decision = getRouteAccessDecision({
      isAuthenticated: false,
      user: null,
      requiredRole: 'Organizer',
    });

    assert.equal(decision.status, 'REDIRECT_LOGIN');
    assert.equal(decision.redirectTo, '/login');
  });

  // =========================================================================
  // 4. Admin without Organizer role
  // =========================================================================
  it('4. Admin without Organizer role -> ACCESS_DENIED (no automatic access to private organizer sales)', () => {
    const decision = getRouteAccessDecision({
      isAuthenticated: true,
      user: adminOnlyUser,
      requiredRole: 'Organizer',
    });

    assert.equal(decision.status, 'ACCESS_DENIED');
    assert.equal(decision.redirectTo, '/forbidden');
  });

  // =========================================================================
  // 5. Admin WITH Organizer role
  // =========================================================================
  it('5. Admin possessing Organizer role -> ALLOWED', () => {
    const decision = getRouteAccessDecision({
      isAuthenticated: true,
      user: multiRoleOrganizerUser,
      requiredRole: 'Organizer',
    });

    assert.equal(decision.status, 'ALLOWED');
  });

  // =========================================================================
  // 6. Loading state prevents UI leak
  // =========================================================================
  it('6. Authentication loading state -> returns LOADING and avoids premature UI leak', () => {
    const decision = getRouteAccessDecision({
      isAuthenticated: true,
      user: organizerUser,
      requiredRole: 'Organizer',
      isLoading: true,
    });

    assert.equal(decision.status, 'LOADING');
  });

  // =========================================================================
  // 7. Navigation visibility rules
  // =========================================================================
  it('7. Navigation visibility: visible for Organizer, hidden for Customer and Admin', () => {
    // Organizer -> visible
    assert.equal(shouldShowSalesAnalyticsNavigation(organizerUser, true), true);
    assert.equal(isOrganizer(organizerUser, true), true);

    // Customer -> hidden
    assert.equal(shouldShowSalesAnalyticsNavigation(customerUser, true), false);
    assert.equal(isOrganizer(customerUser, true), false);

    // Admin without Organizer -> hidden
    assert.equal(shouldShowSalesAnalyticsNavigation(adminOnlyUser, true), false);
    assert.equal(isOrganizer(adminOnlyUser, true), false);

    // Logged-out visitor -> hidden
    assert.equal(shouldShowSalesAnalyticsNavigation(null, false), false);
    assert.equal(shouldShowSalesAnalyticsNavigation(organizerUser, false), false);
  });

  // =========================================================================
  // 8. Stale organizer role revocation on 403
  // =========================================================================
  it('8. API returns 403 on organizer endpoint -> detects stale Organizer role for revocation', () => {
    let strippedRole = null;
    const result = handleApiAuthError({
      status: 403,
      url: '/api/organizer/analytics/sales',
      currentPath: '/organizer',
      onStaleRole: (role) => {
        strippedRole = role;
      },
    });

    assert.equal(result.handled, true);
    assert.equal(result.action, 'SHOW_ACCESS_DENIED');
    assert.equal(result.redirectTo, '/forbidden');
    assert.equal(result.staleRoleRemoved, true);
    assert.equal(strippedRole, 'Organizer');
  });

  // =========================================================================
  // 9. Active session role revocation
  // =========================================================================
  it('9. removeRoleFromUser removes Organizer role cleanly', () => {
    const updated = removeRoleFromUser(organizerUser, 'Organizer');
    assert.deepEqual(updated.roles, []);
    assert.equal(isOrganizer(updated, true), false);
  });

  // =========================================================================
  // 10. Sanitized API error formatting
  // =========================================================================
  it('10. formatSalesAnalyticsError handles 401, 403, 404 without disclosing sensitive details', () => {
    const err401 = { status: 401 };
    assert.equal(
      formatSalesAnalyticsError(err401),
      'Authentication required or your session has expired. Please log in again.'
    );

    const err403 = { status: 403 };
    assert.equal(
      formatSalesAnalyticsError(err403),
      'Access denied. You do not have permission to view sales analytics.'
    );

    const err404 = { status: 404 };
    assert.equal(
      formatSalesAnalyticsError(err404),
      'The requested event was not found or is inaccessible.'
    );

    const errGeneric = { status: 500, message: 'Database connection failed' };
    assert.equal(
      formatSalesAnalyticsError(errGeneric),
      'An error occurred while retrieving sales analytics. Please try again.'
    );
  });
});
