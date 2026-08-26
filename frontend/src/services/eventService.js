const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:7000';

export async function fetchPublishedEvents() {
  const response = await fetch(`${API_BASE_URL}/api/events`, {
    method: 'GET',
    headers: {
      'Accept': 'application/json',
    },
  });

  if (!response.ok) {
    throw new Error(`Failed to load events. Server responded with status ${response.status}`);
  }

  const data = await response.json();
  return data;
}

export async function fetchEventById(id) {
  const response = await fetch(`${API_BASE_URL}/api/events/${id}`, {
    method: 'GET',
    headers: {
      'Accept': 'application/json',
    },
  });

  if (response.status === 404) {
    const error = new Error('Event not found or no longer available.');
    error.status = 404;
    throw error;
  }

  if (!response.ok) {
    const error = new Error(`Failed to load event details. Server responded with status ${response.status}`);
    error.status = response.status;
    throw error;
  }

  const data = await response.json();
  return data;
}
