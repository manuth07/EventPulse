import { API_BASE_URL, getApiBaseUrl } from './apiConfig';

export async function fetchPublishedEvents() {
  const response = await fetch(`${getApiBaseUrl()}/api/events`, {
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
  const response = await fetch(`${getApiBaseUrl()}/api/events/${id}`, {
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
  const response = await fetch(`${getApiBaseUrl()}/api/events`, {
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
  const response = await fetch(`${getApiBaseUrl()}/api/events/my-submissions`, {
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
  const response = await fetch(`${getApiBaseUrl()}/api/events/admin/pending`, {
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
  const response = await fetch(`${getApiBaseUrl()}/api/events/admin/pending/${id}`, {
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
  const response = await fetch(`${getApiBaseUrl()}/api/events/${id}/approve`, {
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
  const response = await fetch(`${getApiBaseUrl()}/api/events/${id}/reject`, {
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
  const response = await fetch(`${getApiBaseUrl()}/api/events/my-submissions/${id}`, {
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
  const response = await fetch(`${getApiBaseUrl()}/api/events/${id}/resubmit`, {
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

/**
 * Submits an Event Update Request for an Approved or Published event (EP-34 / US-14).
 * The live event is not directly mutated; proposed changes are queued for admin review.
 * @param {string} id - The Event GUID.
 * @param {FormData} formData - Multipart form containing proposed fields and optional images.
 * @param {string} token - The authenticated organizer's JWT token.
 * @returns {Promise<Object>} The created EventUpdateRequestDto.
 */
export async function submitEventUpdateRequest(id, formData, token) {
  const response = await fetch(`${getApiBaseUrl()}/api/events/${id}/update-request`, {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`,
      // Content-Type is intentionally omitted so the browser sets the multipart boundary
    },
    body: formData,
  });

  if (!response.ok) {
    let errorMsg = `Failed to submit update request (${response.status})`;
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

/**
 * Retrieves the latest Event Update Request for an organizer's event (EP-34 / US-14).
 * Returns null if no update request exists (404).
 * @param {string} id - The Event GUID.
 * @param {string} token - The authenticated organizer's JWT token.
 * @returns {Promise<Object|null>} The EventUpdateRequestDto or null if none exists.
 */
export async function getEventUpdateRequest(id, token) {
  const response = await fetch(`${getApiBaseUrl()}/api/events/${id}/update-request`, {
    method: 'GET',
    headers: {
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
  });

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    let errorMsg = `Failed to load event update request (${response.status})`;
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

/**
 * Retrieves all pending Event Update Requests for admin review (EP-210 / EP-34).
 * @param {string} token - The authenticated admin's JWT token.
 * @returns {Promise<Array>} List of pending update requests.
 */
export async function getPendingEventUpdateRequests(token) {
  const response = await fetch(`${getApiBaseUrl()}/api/events/admin/update-requests/pending`, {
    method: 'GET',
    headers: {
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    let errorMsg = `Failed to load pending update requests (${response.status})`;
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

/**
 * Retrieves comparison details between current event and update request for admin review (EP-210 / EP-34).
 * @param {string} requestId - The update request GUID.
 * @param {string} token - The authenticated admin's JWT token.
 * @returns {Promise<Object>} AdminEventUpdateComparisonDto
 */
export async function getEventUpdateRequestReview(requestId, token) {
  const response = await fetch(`${getApiBaseUrl()}/api/events/admin/update-requests/${requestId}`, {
    method: 'GET',
    headers: {
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    let errorMsg = `Failed to load update request review (${response.status})`;
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

/**
 * Approves an event update request and applies changes to the live event (EP-210 / EP-34).
 * @param {string} requestId - The update request GUID.
 * @param {string} token - The authenticated admin's JWT token.
 * @param {string} [notes] - Optional admin review notes.
 * @returns {Promise<Object>} The updated live event.
 */
export async function approveEventUpdateRequest(requestId, token, notes = '') {
  const response = await fetch(`${getApiBaseUrl()}/api/events/admin/update-requests/${requestId}/approve`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify({ notes }),
  });

  if (!response.ok) {
    let errorMsg = `Failed to approve update request (${response.status})`;
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

/**
 * Rejects an event update request with mandatory review notes (EP-210 / EP-34).
 * @param {string} requestId - The update request GUID.
 * @param {string} token - The authenticated admin's JWT token.
 * @param {string} notes - Admin rejection feedback notes.
 * @returns {Promise<Object>} The rejected update request.
 */
export async function rejectEventUpdateRequest(requestId, token, notes = '') {
  const response = await fetch(`${getApiBaseUrl()}/api/events/admin/update-requests/${requestId}/reject`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify({ notes }),
  });

  if (!response.ok) {
    let errorMsg = `Failed to reject update request (${response.status})`;
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

/**
 * Submits an Event Cancellation Request for an Approved or Published event (EP-35 / US-15).
 * The live event is not directly cancelled; an auditable request is submitted for admin review.
 * @param {string} id - The Event GUID.
 * @param {string} reason - The cancellation reason (5-1000 characters).
 * @param {string} token - The authenticated organizer's JWT token.
 * @returns {Promise<Object>} The created EventCancellationRequestDto.
 */
export async function submitEventCancellationRequest(id, reason, token) {
  const response = await fetch(`${getApiBaseUrl()}/api/events/${id}/cancellation-request`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify({ reason }),
  });

  if (!response.ok) {
    let errorMsg = `Failed to submit cancellation request (${response.status})`;
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

/**
 * Retrieves the latest Event Cancellation Request for an organizer's event (EP-35 / US-15).
 * Returns null if no cancellation request exists (404).
 * @param {string} id - The Event GUID.
 * @param {string} token - The authenticated organizer's JWT token.
 * @returns {Promise<Object|null>} The EventCancellationRequestDto or null if none exists.
 */
export async function getEventCancellationRequest(id, token) {
  const response = await fetch(`${getApiBaseUrl()}/api/events/${id}/cancellation-request`, {
    method: 'GET',
    headers: {
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
  });

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    let errorMsg = `Failed to load event cancellation request (${response.status})`;
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

/**
 * Retrieves all pending event cancellation requests for Administrator review (EP-35 / US-15).
 * @param {string} token - The authenticated admin's JWT token.
 * @returns {Promise<Array>} Array of AdminEventCancellationReviewDto objects.
 */
export async function getPendingEventCancellationRequests(token) {
  const response = await fetch(`${getApiBaseUrl()}/api/events/admin/cancellation-requests/pending`, {
    method: 'GET',
    headers: {
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    let errorMsg = `Failed to load pending cancellation requests (${response.status})`;
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

/**
 * Retrieves a single event cancellation request with event details and ticket sales for Admin review (EP-35 / US-15).
 * @param {string} requestId - The cancellation request GUID.
 * @param {string} token - The authenticated admin's JWT token.
 * @returns {Promise<Object>} AdminEventCancellationReviewDto.
 */
export async function getEventCancellationRequestReview(requestId, token) {
  const response = await fetch(`${getApiBaseUrl()}/api/events/admin/cancellation-requests/${requestId}`, {
    method: 'GET',
    headers: {
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    let errorMsg = `Failed to load cancellation request review (${response.status})`;
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

/**
 * Approves an event cancellation request, transitioning the live event to Cancelled (EP-35 / US-15).
 * @param {string} requestId - The cancellation request GUID.
 * @param {string} token - The authenticated admin's JWT token.
 * @param {string} [notes] - Optional admin review notes.
 * @returns {Promise<Object>} The approved cancellation review DTO.
 */
export async function approveEventCancellationRequest(requestId, token, notes = '') {
  const response = await fetch(`${getApiBaseUrl()}/api/events/admin/cancellation-requests/${requestId}/approve`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify({ notes }),
  });

  if (!response.ok) {
    let errorMsg = `Failed to approve cancellation request (${response.status})`;
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

/**
 * Rejects an event cancellation request with mandatory review feedback (EP-35 / US-15).
 * @param {string} requestId - The cancellation request GUID.
 * @param {string} token - The authenticated admin's JWT token.
 * @param {string} notes - Mandatory admin rejection feedback.
 * @returns {Promise<Object>} The rejected cancellation review DTO.
 */
export async function rejectEventCancellationRequest(requestId, token, notes = '') {
  const response = await fetch(`${getApiBaseUrl()}/api/events/admin/cancellation-requests/${requestId}/reject`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify({ notes }),
  });

  if (!response.ok) {
    let errorMsg = `Failed to reject cancellation request (${response.status})`;
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

