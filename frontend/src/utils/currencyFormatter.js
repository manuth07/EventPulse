/**
 * Utility helper to format event prices consistently across EventPulse.
 * Format: "LKR 15,000", "LKR 1,499", or "LKR 0" when amount is 0.
 *
 * Current EventPulse Sri Lankan ticketing scope:
 *   - Currency: LKR
 *   - Amounts: Whole rupee values (zero fractional digits)
 *   - No currency conversion or fractional cent formatting
 */
export function formatPrice(price, currencyCode = 'LKR') {
  const numPrice = Number(price);
  if (isNaN(numPrice)) {
    return `${currencyCode} 0`;
  }
  return `${currencyCode} ${numPrice.toLocaleString('en-US', {
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  })}`;
}
