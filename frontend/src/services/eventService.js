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

export async function getPendingEvents(token) {
  const response = await fetch(`${API_BASE_URL}/api/events/admin/pending`, {
    method: 'GET',
    headers: {
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    let errorMsg = `Failed to load pending events (${response.status})`;
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

export async function getPendingEventById(id, token) {
  const response = await fetch(`${API_BASE_URL}/api/events/admin/pending/${id}`, {
    method: 'GET',
    headers: {
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
  });

  if (response.status === 404) {
    const error = new Error('Pending event submission not found.');
    error.status = 404;
    throw error;
  }

  if (!response.ok) {
    let errorMsg = `Failed to load pending event details (${response.status})`;
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

export async function approveEvent(id, token) {
  const response = await fetch(`${API_BASE_URL}/api/events/${id}/approve`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify({}),
  });

  if (!response.ok) {
    let errorMsg = `Failed to approve event (${response.status})`;
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

export async function rejectEvent(id, token, notes = '') {
  const response = await fetch(`${API_BASE_URL}/api/events/${id}/reject`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify({ notes }),
  });

  if (!response.ok) {
    let errorMsg = `Failed to reject event (${response.status})`;
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

export async function getMySubmission(id, token) {
  const response = await fetch(`${API_BASE_URL}/api/events/my-submissions/${id}`, {
    method: 'GET',
    headers: {
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
  });

  if (response.status === 404) {
    const error = new Error('Event submission not found or you do not have permission to view it.');
    error.status = 404;
    throw error;
  }

  if (!response.ok) {
    let errorMsg = `Failed to load event submission (${response.status})`;
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

export async function resubmitEvent(id, formData, token) {
  const response = await fetch(`${API_BASE_URL}/api/events/${id}/resubmit`, {
    method: 'PUT',
    headers: {
      'Authorization': `Bearer ${token}`,
      // Content-Type is intentionally omitted so the browser sets the multipart boundary
    },
    body: formData,
  });

  if (!response.ok) {
    let errorMsg = `Failed to resubmit event (${response.status})`;
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
