/**
 * Utility helper to format event prices consistently across EventPulse.
 * Format: "LKR 5,000" or "Free" when amount is 0.
 *
 * Future Internationalization Note:
 * In a future release supporting multi-currency internationalization, the Event model
 * should represent monetary values as:
 *   - Amount (decimal)
 *   - CurrencyCode (string, ISO-4217 e.g. "LKR", "USD", "SGD")
 * For current Sprint 1 EventPulse, all ticket prices are in LKR.
 */
export function formatPrice(price, currencyCode = 'LKR') {
  const numPrice = Number(price);
  if (isNaN(numPrice) || numPrice === 0) {
    return 'Free';
  }
  return `${currencyCode} ${numPrice.toLocaleString('en-US', {
    maximumFractionDigits: 2,
  })}`;
}
