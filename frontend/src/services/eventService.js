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

export async function submitEvent(formData, token) {
  const response = await fetch(`${API_BASE_URL}/api/events`, {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`
      // Content-Type is intentionally omitted so the browser sets the multipart boundary
    },
    body: formData,
  });

  if (!response.ok) {
    let errorMsg = `Failed to submit event (${response.status})`;
    try {
      const data = await response.json();
      if (data.message) {
        errorMsg = data.message;
      }
      if (data.errors && data.errors.length > 0) {
        errorMsg += ': ' + data.errors.join(', ');
      }
    } catch (e) {
      // response might not be JSON
    }
    const error = new Error(errorMsg);
    error.status = response.status;
    throw error;
  }

  return response.json();
}

export async function getMySubmissions(token) {
  const response = await fetch(`${API_BASE_URL}/api/events/my-submissions`, {
    method: 'GET',
    headers: {
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    let errorMsg = `Failed to load event submissions (${response.status})`;
    try {
      const data = await response.json();
      if (data.message) {
        errorMsg = data.message;
      }
    } catch (e) {
      // response might not be JSON
    }
    const error = new Error(errorMsg);
    error.status = response.status;
    throw error;
  }

  return response.json();
}
