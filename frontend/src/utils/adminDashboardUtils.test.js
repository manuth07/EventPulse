import test from 'node:test';
import assert from 'node:assert/strict';
import {
  formatAdminDate,
  getUserStatusBadgeConfig,
  getUserRoleBadgeConfig,
  canAdminDeactivateUser,
  canAdminDemoteUser,
  formatPlatformMetric,
  getAdminErrorMessage,
} from './adminDashboardUtils.js';

test('Admin Dashboard & User Management Helpers (EP-372 / EP-57)', async (t) => {
  await t.test('formatAdminDate: formats valid UTC date with time by default', () => {
    const formatted = formatAdminDate('2026-10-11T14:30:00Z');
    assert.match(formatted, /Oct 11, 2026/);
    assert.match(formatted, /:/);
  });

  await t.test('formatAdminDate: formats valid UTC date without time when requested', () => {
    const formatted = formatAdminDate('2026-10-11T14:30:00Z', false);
    assert.equal(formatted, 'Oct 11, 2026');
  });

  await t.test('formatAdminDate: handles null, undefined, empty, and invalid dates gracefully', () => {
    assert.equal(formatAdminDate(null), '—');
    assert.equal(formatAdminDate(undefined), '—');
    assert.equal(formatAdminDate(''), '—');
    assert.equal(formatAdminDate('not-a-valid-date'), '—');
  });

  await t.test('getUserStatusBadgeConfig: returns correct styles for active user', () => {
    const badge = getUserStatusBadgeConfig(true);
    assert.equal(badge.label, 'Active');
    assert.equal(badge.bg, '#E8F5E9');
    assert.equal(badge.color, '#2E7D32');
    assert.equal(badge.border, '#C8E6C9');
  });

  await t.test('getUserStatusBadgeConfig: returns correct styles for inactive user', () => {
    const badge = getUserStatusBadgeConfig(false);
    assert.equal(badge.label, 'Inactive');
    assert.equal(badge.bg, '#FFF0EF');
    assert.equal(badge.color, '#D32F2F');
    assert.equal(badge.border, '#FFCDD2');
  });

  await t.test('getUserRoleBadgeConfig: returns approved styles for Administrator', () => {
    const badge = getUserRoleBadgeConfig('Administrator');
    assert.equal(badge.label, 'Administrator');
    assert.equal(badge.color, '#512DA8');
  });

  await t.test('getUserRoleBadgeConfig: returns approved styles for Organizer', () => {
    const badge = getUserRoleBadgeConfig('Organizer');
    assert.equal(badge.label, 'Organizer');
    assert.equal(badge.color, '#1565C0');
  });

  await t.test('getUserRoleBadgeConfig: returns approved styles for Customer', () => {
    const badge = getUserRoleBadgeConfig('Customer');
    assert.equal(badge.label, 'Customer');
    assert.equal(badge.color, '#616161');
  });

  await t.test('getUserRoleBadgeConfig: handles case-insensitivity and defaults safely', () => {
    assert.equal(getUserRoleBadgeConfig('administrator').label, 'Administrator');
    assert.equal(getUserRoleBadgeConfig('organizer').label, 'Organizer');
    assert.equal(getUserRoleBadgeConfig('customer').label, 'Customer');
    assert.equal(getUserRoleBadgeConfig('SuperUser').label, 'SuperUser');
    assert.equal(getUserRoleBadgeConfig(null).label, 'Customer');
  });

  await t.test('canAdminDeactivateUser: prevents self-deactivation when admin IDs match', () => {
    const target = { id: 'admin-uuid-1', isActive: true };
    const check = canAdminDeactivateUser(target, 'admin-uuid-1');
    assert.equal(check.allowed, false);
    assert.equal(check.message, 'You cannot deactivate your own administrative account.');
  });

  await t.test('canAdminDeactivateUser: prevents self-deactivation case-insensitively', () => {
    const target = { id: 'ADMIN-UUID-1', isActive: true };
    const check = canAdminDeactivateUser(target, 'admin-uuid-1');
    assert.equal(check.allowed, false);
  });

  await t.test('canAdminDeactivateUser: blocks deactivating an already inactive account', () => {
    const target = { id: 'other-user', isActive: false };
    const check = canAdminDeactivateUser(target, 'admin-uuid-1');
    assert.equal(check.allowed, false);
    assert.equal(check.message, 'This user account is already inactive.');
  });

  await t.test('canAdminDeactivateUser: allows deactivating another active user', () => {
    const target = { id: 'other-user', isActive: true };
    const check = canAdminDeactivateUser(target, 'admin-uuid-1');
    assert.equal(check.allowed, true);
    assert.equal(check.message, null);
  });

  await t.test('canAdminDemoteUser: blocks removing Administrator role from oneself', () => {
    const target = { id: 'admin-uuid-1', role: 'Administrator' };
    const checkCust = canAdminDemoteUser(target, 'Customer', 'admin-uuid-1');
    assert.equal(checkCust.allowed, false);
    assert.equal(checkCust.message, 'You cannot remove your own Administrator role.');

    const checkOrg = canAdminDemoteUser(target, 'Organizer', 'admin-uuid-1');
    assert.equal(checkOrg.allowed, false);
  });

  await t.test('canAdminDemoteUser: allows self to remain Administrator', () => {
    const target = { id: 'admin-uuid-1', role: 'Administrator' };
    const check = canAdminDemoteUser(target, 'Administrator', 'admin-uuid-1');
    assert.equal(check.allowed, true);
    assert.equal(check.message, null);
  });

  await t.test('canAdminDemoteUser: allows modifying roles for other users', () => {
    const target = { id: 'other-user', role: 'Administrator' };
    const check = canAdminDemoteUser(target, 'Customer', 'admin-uuid-1');
    assert.equal(check.allowed, true);
    assert.equal(check.message, null);
  });

  await t.test('formatPlatformMetric: formats plain number into available metric', () => {
    const res = formatPlatformMetric(1250);
    assert.equal(res.display, '1,250');
    assert.equal(res.isAvailable, true);
    assert.equal(res.status, 'Available');
    assert.equal(res.subtext, null);
  });

  await t.test('formatPlatformMetric: formats dependency object when Available', () => {
    const res = formatPlatformMetric({ status: 'Available', totalCount: 42, message: null });
    assert.equal(res.display, '42');
    assert.equal(res.isAvailable, true);
    assert.equal(res.status, 'Available');
  });

  await t.test('formatPlatformMetric: formats dependency object when Unavailable without fabricating value', () => {
    const res = formatPlatformMetric({
      status: 'Unavailable',
      totalCount: null,
      message: 'Booking service temporarily offline',
    });
    assert.equal(res.display, 'N/A');
    assert.equal(res.isAvailable, false);
    assert.equal(res.status, 'Unavailable');
    assert.equal(res.subtext, 'Booking service temporarily offline');
  });

  await t.test('formatPlatformMetric: handles null or undefined metric gracefully', () => {
    const resNull = formatPlatformMetric(null);
    assert.equal(resNull.display, 'N/A');
    assert.equal(resNull.isAvailable, false);
    assert.equal(resNull.status, 'Unavailable');

    const resUndef = formatPlatformMetric(undefined);
    assert.equal(resUndef.display, 'N/A');
    assert.equal(resUndef.isAvailable, false);
  });

  await t.test('getAdminErrorMessage: maps LAST_ADMIN_PROTECTION business error code', () => {
    const err = new Error('LAST_ADMIN_PROTECTION');
    assert.equal(
      getAdminErrorMessage(err),
      'Cannot perform this action: the platform must have at least one active Administrator.'
    );
  });

  await t.test('getAdminErrorMessage: maps SELF_DEACTIVATION_PROHIBITED code', () => {
    const err = new Error('SELF_DEACTIVATION_PROHIBITED');
    assert.equal(
      getAdminErrorMessage(err),
      'You cannot deactivate your own administrator account.'
    );
  });

  await t.test('getAdminErrorMessage: maps SELF_DEMOTION_PROHIBITED code', () => {
    const err = new Error('SELF_DEMOTION_PROHIBITED');
    assert.equal(
      getAdminErrorMessage(err),
      'You cannot demote or remove your own Administrator privileges.'
    );
  });

  await t.test('getAdminErrorMessage: maps CANNOT_DEACTIVATE_ADMIN code', () => {
    const err = new Error('CANNOT_DEACTIVATE_ADMIN');
    assert.equal(
      getAdminErrorMessage(err),
      'Active administrators cannot be deactivated directly. Change their role first.'
    );
  });

  await t.test('getAdminErrorMessage: maps USER_NOT_FOUND code', () => {
    const err = new Error('USER_NOT_FOUND');
    assert.equal(
      getAdminErrorMessage(err),
      'The requested user account was not found in the directory.'
    );
  });

  await t.test('getAdminErrorMessage: extracts message from response object', () => {
    const err = { response: { data: { message: 'Database concurrency conflict detected.' } } };
    assert.equal(getAdminErrorMessage(err), 'Database concurrency conflict detected.');
  });

  await t.test('getAdminErrorMessage: extracts title from ProblemDetails response', () => {
    const err = { response: { data: { title: 'Forbidden administrative action' } } };
    assert.equal(getAdminErrorMessage(err), 'Forbidden administrative action');
  });

  await t.test('getAdminErrorMessage: falls back cleanly for unexpected errors', () => {
    assert.equal(getAdminErrorMessage(new Error('Unknown failure')), 'Unknown failure');
    assert.equal(
      getAdminErrorMessage(null),
      'An unexpected error occurred while processing the admin request.'
    );
  });
});
