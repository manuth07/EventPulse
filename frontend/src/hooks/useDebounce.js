import { useState, useEffect } from 'react';

/**
 * Custom hook to debounce a fast-changing value (e.g., search input).
 * @param {*} value The value to debounce.
 * @param {number} delay Debounce delay in milliseconds (defaults to 300ms).
 * @returns {*} The debounced value.
 */
export function useDebounce(value, delay = 300) {
  const [debouncedValue, setDebouncedValue] = useState(value);

  useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedValue(value);
    }, delay);

    return () => {
      clearTimeout(handler);
    };
  }, [value, delay]);

  return debouncedValue;
}
