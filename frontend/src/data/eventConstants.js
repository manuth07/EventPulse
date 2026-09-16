/**
 * EventPulse - Shared Event Constants
 * Whitelisted categories and venue types used across event creation, editing, and filtering.
 */

export const EVENT_CATEGORIES = [
  'Music',
  'Sports',
  'Conference',
  'Workshop',
  'Festival',
  'Arts & Theatre',
  'Community',
  'Other',
];

export const VENUE_TYPES = ['Indoor', 'Outdoor'];

const LEGACY_CATEGORY_MAP = {
  'Musical Concert': 'Music',
  'Theatre / Performance': 'Arts & Theatre',
  'Theatre': 'Arts & Theatre',
  'Arts': 'Arts & Theatre',
};

export function normalizeCategory(category) {
  if (!category) return category;
  const trimmed = category.trim();
  return LEGACY_CATEGORY_MAP[trimmed] || trimmed;
}
