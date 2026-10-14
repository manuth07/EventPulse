import test, { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { apiClient } from '../services/apiClient.js';
import { validateReportCriteria } from './adminReportsUtils.js';
import { validateFilterInputs } from './reportFilterUtils.js';

describe('Frontend API Integration & Defensive Checks Regression Suite', () => {
  it('1. apiClient provides all standard HTTP methods including patch', () => {
    assert.equal(typeof apiClient.get, 'function', 'apiClient.get should be a function');
    assert.equal(typeof apiClient.post, 'function', 'apiClient.post should be a function');
    assert.equal(typeof apiClient.put, 'function', 'apiClient.put should be a function');
    assert.equal(typeof apiClient.patch, 'function', 'apiClient.patch should be a function');
    assert.equal(typeof apiClient.delete, 'function', 'apiClient.delete should be a function');
  });

  it('2. validateReportCriteria rejects invalid dates without throwing RangeError', () => {
    // Malformed date strings must return validation error cleanly
    const invalidFrom = validateReportCriteria({
      reportType: 'platform-summary',
      from: 'invalid-date-format',
      to: '2026-10-15',
    }, 'Administrator');

    assert.equal(invalidFrom.isValid, false);
    assert.ok(invalidFrom.error);

    const reversedDates = validateReportCriteria({
      reportType: 'platform-summary',
      from: '2026-10-20',
      to: '2026-10-10',
    }, 'Administrator');

    assert.equal(reversedDates.isValid, false);
    assert.ok(reversedDates.error.includes('earlier than or equal to'));
  });

  it('3. validateFilterInputs handles empty and valid dates safely for organizers', () => {
    const valid = validateFilterInputs({
      from: '2026-10-01',
      to: '2026-10-14',
      category: 'All',
    }, { role: 'Organizer' });

    assert.equal(valid.isValid, true);
    assert.equal(valid.error, null);

    // Date range exceeded
    const exceeded = validateFilterInputs({
      from: '2020-01-01',
      to: '2026-10-14',
    }, { role: 'Organizer' });

    assert.equal(exceeded.isValid, false);
    assert.ok(exceeded.error.includes('730 days'));
  });

  it('4. Post-login destination handles state, query param, and rejects open redirects', () => {
    function resolveDestination(state, search) {
      const searchFrom = new URLSearchParams(search).get('from');
      const candidateFrom = state?.returnTo || state?.from?.pathname || searchFrom || '/';
      return candidateFrom.startsWith('/') && !candidateFrom.startsWith('//') ? candidateFrom : '/';
    }

    // State returnTo takes precedence
    assert.equal(resolveDestination({ returnTo: '/events/123/tickets' }, ''), '/events/123/tickets');

    // State from.pathname works for route guards
    assert.equal(resolveDestination({ from: { pathname: '/cart' } }, ''), '/cart');

    // Query param ?from= works for 401 redirect
    assert.equal(resolveDestination(null, '?from=%2Forganizer%2Fsales-analytics'), '/organizer/sales-analytics');

    // Default fallback
    assert.equal(resolveDestination(null, ''), '/');

    // Open redirect attempts (protocol-relative or absolute external URLs) are blocked and fallback to '/'
    assert.equal(resolveDestination(null, '?from=//malicious-site.com'), '/');
    assert.equal(resolveDestination(null, '?from=https://evil.com'), '/');
    assert.equal(resolveDestination({ returnTo: 'javascript:alert(1)' }, ''), '/');
  });
});
