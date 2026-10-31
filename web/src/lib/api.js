const BASE_URL = (import.meta.env.VITE_API_URL ?? '/api').replace(/\/$/, '');
const SESSION_KEY = 'qb.session';

// the session lives in localStorage when "keep me signed in" is ticked,
// otherwise in sessionStorage so it ends with the tab
export const session = {
  get() {
    try {
      const raw = localStorage.getItem(SESSION_KEY) ?? sessionStorage.getItem(SESSION_KEY);
      const value = raw ? JSON.parse(raw) : null;

      if (value && new Date(value.expiresAt) <= new Date()) {
        session.clear();
        return null;
      }

      return value;
    } catch {
      return null;
    }
  },
  set(value, remember) {
    session.clear();
    try {
      (remember ? localStorage : sessionStorage).setItem(SESSION_KEY, JSON.stringify(value));
    } catch {
      // storage blocked: the session still lives in memory for this page
    }
  },
  clear() {
    try {
      localStorage.removeItem(SESSION_KEY);
      sessionStorage.removeItem(SESSION_KEY);
    } catch {
      // nothing to clear
    }
  }
};

export class ApiError extends Error {
  constructor(message, status) {
    super(message);
    this.status = status;
  }
}

let unauthorizedHandler = () => {};

export function onUnauthorized(handler) {
  unauthorizedHandler = handler;
}

const FALLBACK_MESSAGES = {
  401: 'Your session has ended. Please sign in again.',
  403: 'You do not have access to this.',
  404: 'Not found.',
  500: 'Something went wrong on the server.'
};

async function request(path, { method = 'GET', body, form, raw = false } = {}) {
  const current = session.get();
  const headers = {};

  if (current?.accessToken) {
    headers.Authorization = `Bearer ${current.accessToken}`;
  }

  let payload;
  if (form) {
    payload = form;
  } else if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
    payload = JSON.stringify(body);
  }

  let response;
  try {
    response = await fetch(`${BASE_URL}${path}`, { method, headers, body: payload });
  } catch {
    throw new ApiError('Cannot reach the server. Is the API running?', 0);
  }

  if (response.status === 401 && current) {
    unauthorizedHandler();
  }

  if (raw && response.ok) {
    return response;
  }

  const json = await response.json().catch(() => null);

  if (!response.ok || json?.success === false) {
    const message = json?.message ?? FALLBACK_MESSAGES[response.status] ?? `Request failed (${response.status}).`;
    throw new ApiError(message, response.status);
  }

  return json;
}

const data = (promise) => promise.then((json) => json.data);

const paged = (path, page, size) =>
  request(`${path}?pageNumber=${page}&pageSize=${size}`).then((json) => ({
    items: json.data ?? [],
    pageNumber: json.pageNumber,
    pageSize: json.pageSize,
    totalCount: json.totalCount,
    totalPages: json.totalPages
  }));

export const api = {
  login: (email, password) => data(request('/Auth/login', { method: 'POST', body: { email, password } })),
  me: () => data(request('/Auth/me')),

  callList: (page, size) => paged('/Prospects/call-list', page, size),
  doNotCall: (page, size) => paged('/Prospects/do-not-call', page, size),
  importProspects: (file) => {
    const form = new FormData();
    form.append('File', file);
    return data(request('/Prospects/import', { method: 'POST', form }));
  },
  deleteProspects: () => data(request('/Prospects', { method: 'DELETE' })),

  guide: (prospectId, useAi) =>
    data(request(`/Guides/prospect/${encodeURIComponent(prospectId)}?useAi=${useAi}`)),
  regenerateGuide: (prospectId, useAi) =>
    data(request(`/Guides/prospect/${encodeURIComponent(prospectId)}/regenerate?useAi=${useAi}`, { method: 'POST' })),
  saveFeedback: (guideId, feedback) => data(request(`/Guides/${guideId}/feedback`, { method: 'PUT', body: feedback })),
  report: () => data(request('/Guides/report')),
  budget: () => data(request('/Guides/budget')),
  startBatch: (limit) => data(request('/Guides/batch', { method: 'POST', body: { limit } })),
  batchStatus: () => data(request('/Guides/batch')),
  exportGuides: async () => {
    const response = await request('/Guides/export', { raw: true });
    return response.blob();
  }
};
