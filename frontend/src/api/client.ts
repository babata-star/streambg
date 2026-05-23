import axios from 'axios'

const api = axios.create({ baseURL: '/api' })

// ── Token helpers (read/write zustand persist store) ─────────────────────────

function getAuthState(): Record<string, unknown> | null {
  try {
    const raw = localStorage.getItem('streambg-auth')
    if (raw) {
      const parsed = JSON.parse(raw)
      return parsed?.state ?? null
    }
  } catch { /* ignore */ }
  return null
}

function setAuthFields(fields: Record<string, unknown>): void {
  try {
    const raw = localStorage.getItem('streambg-auth')
    if (raw) {
      const parsed = JSON.parse(raw)
      parsed.state = { ...parsed.state, ...fields }
      localStorage.setItem('streambg-auth', JSON.stringify(parsed))
    }
  } catch { /* ignore */ }
}

// ── Request interceptor: attach JWT ───────────────────────────────────────────

api.interceptors.request.use((config) => {
  const auth = getAuthState()
  if (auth?.accessToken) {
    config.headers.Authorization = `Bearer ${auth.accessToken}`
  }
  return config
})

// ── Response interceptor: auto-refresh on 401 ─────────────────────────────────

let isRefreshing = false
let failedQueue: Array<{
  resolve: (token: string) => void
  reject: (err: unknown) => void
}> = []

function processQueue(err: unknown, token: string | null = null): void {
  for (const { resolve, reject } of failedQueue) {
    err ? reject(err) : resolve(token!)
  }
  failedQueue = []
}

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config

    // Only handle 401 from actual API responses (not network errors)
    if (
      !error.response ||
      error.response.status !== 401 ||
      originalRequest._retry === true ||
      originalRequest.url?.endsWith('/auth/refresh') ||
      originalRequest.url?.endsWith('/auth/login') ||
      originalRequest.url?.endsWith('/auth/register')
    ) {
      return Promise.reject(error)
    }

    const auth = getAuthState()
    if (!auth?.refreshToken) {
      return Promise.reject(error)
    }

    originalRequest._retry = true

    if (isRefreshing) {
      // Another refresh is in progress — queue this request
      return new Promise<string>((resolve, reject) => {
        failedQueue.push({ resolve, reject })
      }).then((token) => {
        originalRequest.headers.Authorization = `Bearer ${token}`
        return api(originalRequest)
      })
    }

    isRefreshing = true

    try {
      const { data } = await axios.post('/api/auth/refresh', {
        refreshToken: auth.refreshToken,
      })

      const newToken: string = data.accessToken
      const newRefreshToken: string | undefined = data.refreshToken

      // Update stored tokens
      const update: Record<string, unknown> = { accessToken: newToken }
      if (newRefreshToken) update.refreshToken = newRefreshToken
      setAuthFields(update)

      processQueue(null, newToken)
      originalRequest.headers.Authorization = `Bearer ${newToken}`
      return api(originalRequest)
    } catch (refreshError) {
      processQueue(refreshError, null)
      // Refresh failed — clear auth state (user must log in again)
      setAuthFields({ user: null, accessToken: null, refreshToken: null })
      return Promise.reject(refreshError)
    } finally {
      isRefreshing = false
    }
  }
)

export default api
