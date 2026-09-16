import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { validateEditEventForm } from './editEventValidation.js';

describe('Edit Event Form Validation (EP-34)', () => {
  const futureDate = new Date(Date.now() + 86400000 * 10).toISOString();
  const pastDate = new Date(Date.now() - 86400000).toISOString();

  const validForm = {
    title: 'Valid Event Title',
    description: 'A valid detailed description that exceeds ten characters.',
    venue: 'BMICH, Colombo',
    eventDate: futureDate,
    category: 'Music',
    venueType: 'Indoor',
  };

  it('validates a completely valid form without errors', () => {
    const errors = validateEditEventForm(validForm);
    assert.deepEqual(errors, {});
  });

  it('detects a past event date as invalid', () => {
    const formWithPastDate = { ...validForm, eventDate: pastDate };
    const errors = validateEditEventForm(formWithPastDate);

    assert.equal(errors.eventDate, 'Event date must be in the future.');
    assert.equal(errors.description, undefined);
    assert.equal(errors.title, undefined);
  });

  it('replaces past date error with description error when date is fixed and description is cleared', () => {
    // Step 1: Initial validation with past date
    const step1Errors = validateEditEventForm({ ...validForm, eventDate: pastDate });
    assert.equal(step1Errors.eventDate, 'Event date must be in the future.');

    // Step 2: Correct date to future, clear description
    const step2Errors = validateEditEventForm({
      ...validForm,
      eventDate: futureDate,
      description: '',
    });

    // Stale date error MUST be gone, description error MUST appear
    assert.equal(step2Errors.eventDate, undefined);
    assert.equal(step2Errors.description, 'Description is required.');
  });

  it('treats whitespace-only description as required error', () => {
    const formWithWhitespaceDesc = { ...validForm, description: '          ' };
    const errors = validateEditEventForm(formWithWhitespaceDesc);

    assert.equal(errors.description, 'Description is required.');
  });

  it('reports multiple invalid fields simultaneously', () => {
    const multiInvalidForm = {
      title: '',
      description: '',
      venue: 'A', // too short
      eventDate: pastDate,
      category: '',
      venueType: '',
    };

    const errors = validateEditEventForm(multiInvalidForm);

    assert.equal(errors.title, 'Event title is required.');
    assert.equal(errors.description, 'Description is required.');
    assert.equal(errors.venue, 'Venue location must be between 3 and 200 characters.');
    assert.equal(errors.eventDate, 'Event date must be in the future.');
    assert.equal(errors.category, 'Please select a valid event category.');
    assert.equal(errors.venueType, 'Please select a valid venue type.');
  });

  it('clears only corrected field error when other invalid fields remain', () => {
    const multiInvalidForm = {
      title: 'Valid Title',
      description: '',
      eventDate: pastDate,
      venue: validForm.venue,
      category: validForm.category,
      venueType: validForm.venueType,
    };

    const errorsPass1 = validateEditEventForm(multiInvalidForm);
    assert.equal(errorsPass1.description, 'Description is required.');
    assert.equal(errorsPass1.eventDate, 'Event date must be in the future.');

    // Fix only date
    const errorsPass2 = validateEditEventForm({
      ...multiInvalidForm,
      eventDate: futureDate,
    });

    assert.equal(errorsPass2.eventDate, undefined);
    assert.equal(errorsPass2.description, 'Description is required.');
  });
});
