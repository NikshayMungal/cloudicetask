// All communication with the backend lives here, so components never build URLs or parse errors themselves.
// API_BASE_URL comes from /config.js (written at container start). Empty = same origin, which nginx
// (or the Vite dev server) proxies to the backend. In production set it to the backend's https:// URL.
const BASE = (window.__CONFIG__?.API_BASE_URL ?? '').replace(/\/$/, '');
const ROOT = `${BASE}/api/shipments`;

export class ApiError extends Error {
  constructor(message, status, fieldErrors = {}) {
    super(message);
    this.status = status;
    this.fieldErrors = fieldErrors; // { fieldName: "message" } for 400 validation responses
  }
}

// ASP.NET returns validation keys like "CustomerName"; the form uses "customerName".
const camel = (key) => key.charAt(0).toLowerCase() + key.slice(1);

async function request(path, options = {}) {
  let response;
  try {
    response = await fetch(`${ROOT}${path}`, {
      headers: { 'Content-Type': 'application/json' },
      ...options,
    });
  } catch {
    throw new ApiError('Cannot reach the server. Check your connection and try again.', 0);
  }

  if (response.status === 204) return null;

  const body = await response.json().catch(() => null);

  if (!response.ok) {
    const fieldErrors = {};
    if (body?.errors) {
      for (const [key, messages] of Object.entries(body.errors)) {
        fieldErrors[camel(key)] = Array.isArray(messages) ? messages[0] : String(messages);
      }
    }
    throw new ApiError(body?.detail || body?.title || `Request failed (${response.status})`, response.status, fieldErrors);
  }
  return body;
}

export const shipmentsApi = {
  list: ({ search = '', status = '', unassigned = false } = {}) => {
    const params = new URLSearchParams();
    if (search.trim()) params.set('search', search.trim());
    if (status) params.set('status', status);
    if (unassigned) params.set('unassigned', 'true');
    const qs = params.toString();
    return request(qs ? `?${qs}` : '');
  },
  summary: () => request('/summary'),
  create: (data) => request('', { method: 'POST', body: JSON.stringify(data) }),
  update: (id, data) => request(`/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  setStatus: (id, status) => request(`/${id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) }),
  remove: (id) => request(`/${id}`, { method: 'DELETE' }),
};
