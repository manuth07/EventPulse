/**
 * Pure validation function for Edit Event form submissions (EP-34).
 * Returns an object containing only current field validation errors:
 * { [fieldName]: string }
 */
export function validateEditEventForm({
  title = '',
  description = '',
  venue = '',
  eventDate = '',
  category = '',
  venueType = '',
} = {}) {
  const errors = {};

  const trimmedTitle = (title || '').trim();
  if (!trimmedTitle) {
    errors.title = 'Event title is required.';
  } else if (trimmedTitle.length < 3 || trimmedTitle.length > 200) {
    errors.title = 'Event title must be between 3 and 200 characters.';
  }

  const trimmedDesc = (description || '').trim();
  if (!trimmedDesc) {
    errors.description = 'Description is required.';
  } else if (trimmedDesc.length < 10 || trimmedDesc.length > 2000) {
    errors.description = 'Description must be between 10 and 2000 characters.';
  }

  const trimmedVenue = (venue || '').trim();
  if (!trimmedVenue) {
    errors.venue = 'Venue location is required.';
  } else if (trimmedVenue.length < 3 || trimmedVenue.length > 200) {
    errors.venue = 'Venue location must be between 3 and 200 characters.';
  }

  if (!eventDate) {
    errors.eventDate = 'Event date and time is required.';
  } else {
    const parsedDate = new Date(eventDate);
    if (isNaN(parsedDate.getTime()) || parsedDate.getTime() <= Date.now()) {
      errors.eventDate = 'Event date must be in the future.';
    }
  }

  if (!category || !String(category).trim()) {
    errors.category = 'Please select a valid event category.';
  }

  if (!venueType || !String(venueType).trim()) {
    errors.venueType = 'Please select a valid venue type.';
  }

  return errors;
}
