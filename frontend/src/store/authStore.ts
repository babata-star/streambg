import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import api from '../api/client'

interface User {
  id: string
  username: string
  avatarUrl?: string
  isStreamer: boolean
  isAdmin: boolean
}

interface AuthState {
  user: User | null
  accessToken: string | null
  refreshToken: string | null
  isLoading: boolean
  login: (email: string, password: string) => Promise<void>
  register: (username: string, email: string, password: string) => Promise<void>
  logout: () => Promise<void>
  fetchMe: () => Promise<void>
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      user: null,
      accessToken: null,
      refreshToken: null,
      isLoading: false,

      login: async (email, password) => {
        set({ isLoading: true })
        try {
          const { data } = await api.post('/auth/login', { email, password })
          set({ user: data.user, accessToken: data.accessToken, refreshToken: data.refreshToken })
        } finally {
          set({ isLoading: false })
        }
      },

      register: async (username, email, password) => {
        set({ isLoading: true })
        try {
          const { data } = await api.post('/auth/register', { username, email, password })
          set({ user: data.user, accessToken: data.accessToken, refreshToken: data.refreshToken })
        } finally {
          set({ isLoading: false })
        }
      },

      logout: async () => {
        const { refreshToken } = get()

        // Invalidate refresh token server-side (best-effort)
        if (refreshToken) {
          try {
            await api.post('/auth/logout', { refreshToken })
          } catch { /* ignore — clear state regardless */ }
        }

        set({ user: null, accessToken: null, refreshToken: null })
      },

      fetchMe: async () => {
        const { data } = await api.get('/auth/me')
        set({ user: data })
      },
    }),
    { name: 'streambg-auth', partialize: (s) => ({ accessToken: s.accessToken, refreshToken: s.refreshToken, user: s.user }) }
  )
)
