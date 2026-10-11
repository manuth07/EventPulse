import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import {
  CANCELLATION_REASON_CODES,
  getCancellationReasonMessage,
  getRefundOutcomeInfo,
  formatCancellationTime,
} from './cancellationHelper.js';

describe('Cancellation Helper (EP-366 / EP-55)', () => {
  describe('getCancellationReasonMessage', () => {
    it('returns exact message for ALREADY_CANCELLED', () => {
      const msg = getCancellationReasonMessage(CANCELLATION_REASON_CODES.ALREADY_CANCELLED);
      assert.strictEqual(msg, 'This booking has already been cancelled.');
    });

    it('returns exact message for CANCELLATION_WINDOW_CLOSED', () => {
      const msg = getCancellationReasonMessage(CANCELLATION_REASON_CODES.CANCELLATION_WINDOW_CLOSED);
      assert.strictEqual(msg, 'The cancellation window has closed for this event. Cancellations are no longer permitted.');
    });

    it('returns exact message for EVENT_ALREADY_STARTED', () => {
      const msg = getCancellationReasonMessage(CANCELLATION_REASON_CODES.EVENT_ALREADY_STARTED);
      assert.strictEqual(msg, 'This event has already started or completed and can no longer be cancelled.');
    });

    it('returns exact message for INVALID_BOOKING_STATUS', () => {
      const msg = getCancellationReasonMessage(CANCELLATION_REASON_CODES.INVALID_BOOKING_STATUS);
      assert.strictEqual(msg, 'This booking cannot be cancelled because its current status does not permit cancellation.');
    });

    it('returns exact message for PAYMENT_NOT_ELIGIBLE', () => {
      const msg = getCancellationReasonMessage(CANCELLATION_REASON_CODES.PAYMENT_NOT_ELIGIBLE);
      assert.strictEqual(msg, 'Payment status is not eligible for cancellation or refund.');
    });

    it('returns exact message for UNAUTHORIZED', () => {
      const msg = getCancellationReasonMessage(CANCELLATION_REASON_CODES.UNAUTHORIZED);
      assert.strictEqual(msg, 'You are not authorized to cancel this booking.');
    });

    it('returns exact message for BOOKING_NOT_FOUND', () => {
      const msg = getCancellationReasonMessage(CANCELLATION_REASON_CODES.BOOKING_NOT_FOUND);
      assert.strictEqual(msg, 'Booking not found or has been removed.');
    });

    it('falls back to custom message when reason code is unknown', () => {
      const msg = getCancellationReasonMessage('CUSTOM_CODE', 'Custom error occurred.');
      assert.strictEqual(msg, 'Custom error occurred.');
    });

    it('falls back to generic error when code and fallback are omitted', () => {
      const msg = getCancellationReasonMessage(null);
      assert.strictEqual(msg, 'An error occurred while processing your cancellation. Please try again.');
    });
  });

  describe('getRefundOutcomeInfo', () => {
    it('returns Refund Pending for Confirmed booking with positive totalAmount', () => {
      const booking = { status: 'Confirmed', totalAmount: 1500 };
      const outcome = getRefundOutcomeInfo(booking);

      assert.strictEqual(outcome.statusLabel, 'Refund Pending');
      assert.strictEqual(outcome.isRefundPending, true);
      assert.strictEqual(outcome.isRefundApplicable, true);
      assert.strictEqual(outcome.amount, 1500);
      assert.match(outcome.explanation, /initiated to your original payment method/);
      assert.strictEqual(outcome.badge.label, 'Refund Pending');
    });

    it('uses cancellationResult previousStatus if provided for Confirmed booking', () => {
      const booking = { status: 'Cancelled', totalAmount: 2500 };
      const cancellationResult = { previousStatus: 'Confirmed' };
      const outcome = getRefundOutcomeInfo(booking, cancellationResult);

      assert.strictEqual(outcome.statusLabel, 'Refund Pending');
      assert.strictEqual(outcome.isRefundPending, true);
      assert.strictEqual(outcome.amount, 2500);
    });

    it('returns No Refund Required for PendingPayment booking', () => {
      const booking = { status: 'PendingPayment', totalAmount: 1500 };
      const outcome = getRefundOutcomeInfo(booking);

      assert.strictEqual(outcome.statusLabel, 'No Refund Required');
      assert.strictEqual(outcome.isRefundPending, false);
      assert.strictEqual(outcome.isRefundApplicable, false);
      assert.strictEqual(outcome.amount, 0);
      assert.match(outcome.explanation, /No payment was captured/);
    });

    it('returns No Refund Required for zero-amount booking even if Confirmed', () => {
      const booking = { status: 'Confirmed', totalAmount: 0 };
      const outcome = getRefundOutcomeInfo(booking);

      assert.strictEqual(outcome.statusLabel, 'No Refund Required');
      assert.strictEqual(outcome.isRefundPending, false);
    });

    it('never claims refund is completed prematurely', () => {
      const booking = { status: 'Confirmed', totalAmount: 3000 };
      const outcome = getRefundOutcomeInfo(booking);
      assert.notStrictEqual(outcome.statusLabel, 'Refund Completed');
      assert.strictEqual(outcome.isRefundPending, true);
    });
  });

  describe('formatCancellationTime', () => {
    it('formats ISO string to localized human string', () => {
      const result = formatCancellationTime('2026-10-11T12:30:00Z');
      assert.notStrictEqual(result, 'Just now');
      assert.match(result, /2026/);
    });

    it('handles null, undefined, or empty gracefully', () => {
      assert.strictEqual(formatCancellationTime(null), 'Just now');
      assert.strictEqual(formatCancellationTime(undefined), 'Just now');
      assert.strictEqual(formatCancellationTime('invalid-date'), 'Just now');
    });
  });
});
