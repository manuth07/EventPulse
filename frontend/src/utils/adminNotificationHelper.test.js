import test from 'node:test';
import assert from 'node:assert/strict';
import {
  formatNotificationDate,
  getNotificationBadgeConfig,
} from './adminNotificationHelper.js';

test('Admin Notification Helpers (EP-151 / EP-32)', async (t) => {
  await t.test('formatNotificationDate formats valid ISO string nicely', () => {
    const formatted = formatNotificationDate('2026-10-08T10:00:00Z');
    assert.match(formatted, /Oct/);
    assert.match(formatted, /2026/);
  });

  await t.test('formatNotificationDate handles missing or null date gracefully', () => {
    assert.equal(formatNotificationDate(null), 'Recently');
    assert.equal(formatNotificationDate(undefined), 'Recently');
    assert.equal(formatNotificationDate('invalid-date-string'), 'Recently');
  });

  await t.test('getNotificationBadgeConfig returns pending config for pending status', () => {
    const config = getNotificationBadgeConfig('Pending');
    assert.equal(config.label, 'Pending Review');
    assert.equal(config.isPending, true);
    assert.equal(config.color, '#FF5B00');
  });

  await t.test('getNotificationBadgeConfig returns approved config for approved status', () => {
    const config = getNotificationBadgeConfig('Approved');
    assert.equal(config.label, 'Approved');
    assert.equal(config.isPending, false);
    assert.equal(config.color, '#2E7D32');
  });

  await t.test('getNotificationBadgeConfig returns published config for published status', () => {
    const config = getNotificationBadgeConfig('Published');
    assert.equal(config.label, 'Published');
    assert.equal(config.isPending, false);
    assert.equal(config.color, '#2E7D32');
  });

  await t.test('getNotificationBadgeConfig returns rejected config for rejected status', () => {
    const config = getNotificationBadgeConfig('Rejected');
    assert.equal(config.label, 'Rejected');
    assert.equal(config.isPending, false);
    assert.equal(config.color, '#FF3B30');
  });

  await t.test('getNotificationBadgeConfig returns fallback config for unknown status', () => {
    const config = getNotificationBadgeConfig(null);
    assert.equal(config.label, 'Unknown');
    assert.equal(config.isPending, false);
  });
});
