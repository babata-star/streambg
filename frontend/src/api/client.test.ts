import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import axios from 'axios'
import MockAdapter from 'axios-mock-adapter'
import api from './client'

// ── Helpers ──────────────────────────────────────────────────────────────────

const AUTH_KEY = 'streambg-auth'

function setLocalAuth(overrides: Partial<Record<string, unknown>> = {}) {
  const data = {
    state: {
      accessToken: 'test-access-token',
      refreshToken: 'test-refresh-token',
      user: { id: '1', username: 'test' },
      ...overrides,
    },
  }
  localStorage.setItem(AUTH_KEY, JSON.stringify(data))
}

function getLocalAuth(): Record<string, unknown> | null {
  try {
    const raw = localStorage.getItem(AUTH_KEY)
    if (raw) return JSON.parse(raw).state
  } catch { /* ignore */ }
  return null
}

// ── Setup ────────────────────────────────────────────────────────────────────

let mock: MockAdapter

beforeEach(() => {
  localStorage.clear()
  mock = new MockAdapter(api)
})

afterEach(() => {
  mock.restore()
  vi.restoreAllMocks()
  localStorage.clear()
})

// ── Tests ────────────────────────────────────────────────────────────────────

describe('request interceptor', () => {
  it('attaches Bearer token when user is logged in', async () => {
    setLocalAuth()
    mock.onGet('/test').reply(200, { ok: true })

    const res = await api.get('/test')
    expect(res.config.headers.Authorization).toBe('Bearer test-access-token')
  })

  it('does not attach token when no auth state exists', async () => {
    mock.onGet('/test').reply(200, { ok: true })

    const res = await api.get('/test')
    expect(res.config.headers.Authorization).toBeUndefined()
  })

  it('does not crash when localStorage is corrupted', async () => {
    localStorage.setItem(AUTH_KEY, 'invalid-json{{{')
    mock.onGet('/test').reply(200, { ok: true })

    const res = await api.get('/test')
    // Request succeeds without auth header
    expect(res.status).toBe(200)
    expect(res.config.headers.Authorization).toBeUndefined()
  })
})

describe('response interceptor — 401 auto-refresh', () => {
  beforeEach(() => {
    setLocalAuth()
  })

  it('calls refresh endpoint and retries the original request on 401', async () => {
    let callCount = 0
    mock.onGet('/me').reply(() => {
      callCount++
      return callCount === 1 ? [401] : [200, { id: '1', username: 'test' }]
    })

    const refreshSpy = vi.spyOn(axios, 'post').mockResolvedValue({
      data: { accessToken: 'new-access-token', refreshToken: 'new-refresh-token' },
    })

    const res = await api.get('/me')

    // Original request eventually succeeded
    expect(res.status).toBe(200)
    expect(res.data).toEqual({ id: '1', username: 'test' })
    expect(callCount).toBe(2)

    // Refresh endpoint was called with the stored refresh token
    expect(refreshSpy).toHaveBeenCalledOnce()
    expect(refreshSpy).toHaveBeenCalledWith('/api/auth/refresh', {
      refreshToken: 'test-refresh-token',
    })

    // Tokens were updated in localStorage
    const auth = getLocalAuth()
    expect(auth?.accessToken).toBe('new-access-token')
    expect(auth?.refreshToken).toBe('new-refresh-token')
  })

  it('clears auth state when refresh fails', async () => {
    mock.onGet('/protected').reply(401)

    const refreshSpy = vi.spyOn(axios, 'post').mockRejectedValue(
      new Error('Refresh token expired')
    )

    await expect(api.get('/protected')).rejects.toThrow('Refresh token expired')

    expect(refreshSpy).toHaveBeenCalledOnce()

    // Auth state should be cleared
    const auth = getLocalAuth()
    expect(auth?.accessToken).toBeNull()
    expect(auth?.refreshToken).toBeNull()
    expect(auth?.user).toBeNull()
  })

  it('only triggers one refresh call for concurrent 401s', async () => {
    // First 2 calls return 401, retries return 200
    let callCount = 0
    mock.onGet().reply(() => {
      callCount++
      return callCount <= 2 ? [401] : [200, {}]
    })

    const refreshSpy = vi.spyOn(axios, 'post').mockResolvedValue({
      data: { accessToken: 'new-token', refreshToken: 'new-refresh' },
    })

    await Promise.allSettled([
      api.get('/ep1'),
      api.get('/ep2'),
    ])

    // Only ONE refresh call despite two 401s
    expect(refreshSpy).toHaveBeenCalledTimes(1)
  })

  it('passes the new token to queued requests after refresh', async () => {
    let callCount = 0
    mock.onGet().reply((config) => {
      callCount++
      if (callCount <= 2) return [401]
      // On retry, check that the new token was attached
      return config.headers?.Authorization === 'Bearer new-token'
        ? [200, { authorized: true }]
        : [200, { authorized: false }]
    })

    vi.spyOn(axios, 'post').mockResolvedValue({
      data: { accessToken: 'new-token', refreshToken: 'new-refresh' },
    })

    const settled = await Promise.allSettled([
      api.get('/ep1'),
      api.get('/ep2'),
    ])

    // Both queued requests should eventually succeed with new token
    for (const result of settled) {
      expect(result.status).toBe('fulfilled')
      if (result.status === 'fulfilled') {
        expect(result.value.data).toEqual({ authorized: true })
      }
    }
  })

  it('excludes /auth/login from refresh interceptor', async () => {
    mock.onPost('/auth/login').reply(401)

    const refreshSpy = vi.spyOn(axios, 'post')

    await expect(api.post('/auth/login', { email: 'x', password: 'y' }))
      .rejects.toThrow()

    // Refresh should NOT have been called
    expect(refreshSpy).not.toHaveBeenCalled()
  })

  it('excludes /auth/register from refresh interceptor', async () => {
    mock.onPost('/auth/register').reply(401)

    const refreshSpy = vi.spyOn(axios, 'post')

    await expect(api.post('/auth/register', { username: 'x', email: 'y', password: 'z' }))
      .rejects.toThrow()

    expect(refreshSpy).not.toHaveBeenCalled()
  })

  it('excludes /auth/refresh from refresh interceptor (no infinite loop)', async () => {
    // Simulate the refresh endpoint itself returning 401
    mock.onPost('/auth/refresh').reply(401)

    const refreshSpy = vi.spyOn(axios, 'post')

    await expect(api.post('/auth/refresh', { refreshToken: 'x' }))
      .rejects.toThrow()

    // Should NOT have called axios.post again
    expect(refreshSpy).not.toHaveBeenCalled()
  })

  it('passes through non-401 errors without refreshing', async () => {
    mock.onGet('/data').reply(403, { error: 'Forbidden' })

    const refreshSpy = vi.spyOn(axios, 'post')

    await expect(api.get('/data')).rejects.toThrow()

    expect(refreshSpy).not.toHaveBeenCalled()
  })

  it('passes through network errors without refreshing', async () => {
    mock.onGet('/data').networkError()

    const refreshSpy = vi.spyOn(axios, 'post')

    await expect(api.get('/data')).rejects.toThrow()

    expect(refreshSpy).not.toHaveBeenCalled()
  })

  it('rejects on 401 when no refresh token is stored', async () => {
    setLocalAuth({ refreshToken: null })
    mock.onGet('/protected').reply(401)

    const refreshSpy = vi.spyOn(axios, 'post')

    await expect(api.get('/protected')).rejects.toThrow()

    // No attempt to refresh
    expect(refreshSpy).not.toHaveBeenCalled()
  })

  it('does not retry if the original retried request also fails', async () => {
    // Both first request AND retry return 401
    mock.onGet('/doomed').reply(401)

    vi.spyOn(axios, 'post').mockResolvedValue({
      data: { accessToken: 'still-bad', refreshToken: 'new-refresh' },
    })

    await expect(api.get('/doomed')).rejects.toThrow()

    // Refresh was called once, but no infinite loop
    expect(axios.post).toHaveBeenCalledTimes(1)
  })
})

describe('edge cases', () => {
  it('handles refresh response without refreshToken gracefully', async () => {
    setLocalAuth()
    mock.onGet('/me').replyOnce(401).onGet('/me').reply(200, { ok: true })

    vi.spyOn(axios, 'post').mockResolvedValue({
      data: { accessToken: 'new-token' }, // No refreshToken in response
    })

    const res = await api.get('/me')
    expect(res.status).toBe(200)

    // accessToken was updated, refreshToken unchanged
    const auth = getLocalAuth()
    expect(auth?.accessToken).toBe('new-token')
    expect(auth?.refreshToken).toBe('test-refresh-token')
  })

  it('updates authorization header on the retried request', async () => {
    setLocalAuth()
    let firstCall = true
    mock.onGet('/check').reply((config) => {
      if (firstCall) {
        firstCall = false
        return [401]
      }
      // The retry should have the new Bearer token
      const auth = config.headers?.Authorization ?? ''
      return auth === 'Bearer new-token'
        ? [200, { updated: true }]
        : [200, { updated: false }]
    })

    vi.spyOn(axios, 'post').mockResolvedValue({
      data: { accessToken: 'new-token', refreshToken: 'new-refresh' },
    })

    const res = await api.get('/check')
    expect(res.data).toEqual({ updated: true })
  })
})
