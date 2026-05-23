import { useState, useEffect, useCallback, useRef } from 'react'
import api from '../api/client'
import { useAdminHub } from '../hooks/useAdminHub'
import styles from './AdminPage.module.css'

interface UserRow {
  id: string
  username: string
  email: string
  isStreamer: boolean
  isAdmin: boolean
  isBanned: boolean
  createdAt: string
}

interface LiveStream {
  id: number
  username: string
  title: string
  viewerCount: number
  category?: string
  startedAt?: string
}

// Simple debounce hook
function useDebounce<T>(value: T, delay = 400): T {
  const [debouncedValue, setDebounced] = useState(value)
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delay)
    return () => clearTimeout(timer)
  }, [value, delay])
  return debouncedValue
}

export function AdminPage() {
  const [tab, setTab]       = useState<'overview' | 'users' | 'streams'>('overview')
  const [users, setUsers]   = useState<UserRow[]>([])
  const [streams, setStreams] = useState<LiveStream[]>([])
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(false)
  const [toast, setToast]   = useState<{ msg: string; ok: boolean } | null>(null)
  const toastTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  const debouncedSearch = useDebounce(search, 400)

  // Real-time stats via SignalR AdminHub
  const { stats, connected: signalRConnected, requestStats } = useAdminHub()

  const showToast = useCallback((msg: string, ok = true) => {
    setToast({ msg, ok })
    if (toastTimer.current) clearTimeout(toastTimer.current)
    toastTimer.current = setTimeout(() => setToast(null), 3000)
  }, [])

  // Reload when tab or debounced search changes
  useEffect(() => {
    if (tab === 'users') {
      setLoading(true)
      api.get('/admin/users', { params: { search: debouncedSearch } })
        .then(({ data }) => setUsers(data.items ?? []))
        .catch(() => showToast('Грешка при зареждане на потребители', false))
        .finally(() => setLoading(false))
    }
    if (tab === 'streams') {
      setLoading(true)
      api.get('/admin/streams/live')
        .then(({ data }) => setStreams(data ?? []))
        .catch(() => showToast('Грешка при зареждане на стриймове', false))
        .finally(() => setLoading(false))
    }
  }, [tab, debouncedSearch, showToast])

  // ── Actions ─────────────────────────────────────────────────────────────────

  const banUser = async (user: UserRow) => {
    const action   = user.isBanned ? 'разблокиран' : 'блокиран'
    const endpoint = user.isBanned ? 'unban' : 'ban'
    if (!user.isBanned && !confirm(`Блокирай потребител "${user.username}"?\nТова ще му забрани вход в платформата.`)) return
    try {
      await api.post(`/admin/users/${user.id}/${endpoint}`,
        endpoint === 'ban' ? { reason: 'Admin action' } : {})
      setUsers((u) => u.map((x) => x.id === user.id ? { ...x, isBanned: !user.isBanned } : x))
      showToast(`"${user.username}" е ${action} успешно`)
    } catch {
      showToast('Грешка — действието не бе изпълнено', false)
    }
  }

  const makeStreamer = async (u: UserRow) => {
    if (!confirm(`Даде ли стриймър права на "${u.username}"?`)) return
    try {
      await api.post(`/admin/users/${u.id}/make-streamer`)
      setUsers((list) => list.map((x) => x.id === u.id ? { ...x, isStreamer: true } : x))
      showToast(`"${u.username}" вече може да стриймва`)
    } catch {
      showToast('Грешка — действието не бе изпълнено', false)
    }
  }

  const terminateStream = async (s: LiveStream) => {
    if (!confirm(`Прекрати стрийма на "${s.username}"?\nОтИТ-ата връзка ще бъде прекъсната.`)) return
    try {
      await api.post(`/admin/streams/${s.id}/terminate`)
      setStreams((list) => list.filter((x) => x.id !== s.id))
      showToast(`Стриймът на "${s.username}" е прекратен`)
    } catch {
      showToast('Грешка — стриймът не бе прекратен', false)
    }
  }

  // ── Render ───────────────────────────────────────────────────────────────────

  return (
    <div className={styles.page}>
      {/* Toast notification */}
      {toast && (
        <div className={`${styles.toast} ${toast.ok ? styles.toastOk : styles.toastErr}`}>
          {toast.ok ? '✓' : '✕'} {toast.msg}
        </div>
      )}

      <div className={styles.header}>
        <h1 className={styles.title}>
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M12 2L2 7l10 5 10-5-10-5z"/><path d="M2 17l10 5 10-5"/><path d="M2 12l10 5 10-5"/>
          </svg>
          Административен панел
        </h1>
        <span className={`${styles.badge} ${signalRConnected ? styles.badgeLive : styles.badgeOffline}`}>
          {signalRConnected ? '● Real-time' : '○ Offline'}
        </span>
        <button className={styles.refreshBtn} onClick={requestStats} title="Обнови статистиките">
          🔄
        </button>
      </div>

      {/* Stats row */}
      {stats && (
        <div className={styles.statsRow}>
          <StatCard label="Потребители"       value={stats.totalUsers}    icon="👥" color="accent"  />
          <StatCard label="Живи стриймове"    value={stats.activeStreams}  icon="📡" color="live"    />
          <StatCard label="Зрители сега"      value={stats.totalViewers}  icon="👁" color="success" />
          <StatCard label="Стриймове днес"    value={stats.streamsToday}  icon="🎬" color="warning" />
          <StatCard label="Нови потр. днес"   value={stats.newUsersToday} icon="✨" color="info"    />
        </div>
      )}

      {/* Tabs */}
      <div className={styles.tabs}>
        {(['overview', 'users', 'streams'] as const).map((t) => (
          <button key={t} className={`${styles.tab} ${tab === t ? styles.activeTab : ''}`} onClick={() => setTab(t)}>
            {{ overview: '📊 Преглед', users: '👥 Потребители', streams: '📡 Стриймове' }[t]}
          </button>
        ))}
      </div>

      {/* ── Overview ─────────────────────────────────────────────────────────── */}
      {tab === 'overview' && (
        <div className={styles.overviewGrid}>
          <div className={styles.overviewCard}>
            <h3>Бърз достъп</h3>
            <div className={styles.quickLinks}>
              <button onClick={() => setTab('users')}   className={styles.quickBtn}>👥 Управление на потребители</button>
              <button onClick={() => setTab('streams')} className={styles.quickBtn}>📡 Активни стриймове</button>
            </div>
          </div>
          <div className={styles.overviewCard}>
            <h3>Полезни команди</h3>
            <div className={styles.cmdList}>
              <code className={styles.cmd}>GET /api/admin/stats</code>
              <code className={styles.cmd}>GET /api/admin/users?search=</code>
              <code className={styles.cmd}>POST /api/admin/cdn/invalidate</code>
            </div>
          </div>
        </div>
      )}

      {/* ── Users ─────────────────────────────────────────────────────────────── */}
      {tab === 'users' && (
        <div className={styles.tableSection}>
          <div className={styles.tableHeader}>
            <h2 className={styles.sectionTitle}>Потребители</h2>
            <div className={styles.searchWrap}>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/>
              </svg>
              <input
                className={styles.searchInput}
                placeholder="Търси по username или email..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
              {loading && <span className={styles.searchSpinner} />}
            </div>
          </div>

          <div className={styles.tableWrap}>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>Потребител</th><th>Email</th><th>Роля</th><th>Статус</th><th>Действия</th>
                </tr>
              </thead>
              <tbody>
                {users.map((u) => (
                  <tr key={u.id}>
                    <td>
                      <div className={styles.userCell}>
                        <div className={styles.userAvatar}>{u.username[0].toUpperCase()}</div>
                        <div>
                          <span className={styles.userName}>{u.username}</span>
                          <span className={styles.userId}>{u.id.slice(0, 8)}…</span>
                        </div>
                      </div>
                    </td>
                    <td className={styles.emailCell}>{u.email}</td>
                    <td>
                      <span className={`${styles.roleBadge} ${u.isAdmin ? styles.admin : u.isStreamer ? styles.streamer : styles.viewer}`}>
                        {u.isAdmin ? 'Admin' : u.isStreamer ? 'Стриймър' : 'Зрител'}
                      </span>
                    </td>
                    <td>
                      <span className={`${styles.statusBadge} ${u.isBanned ? styles.banned : styles.active}`}>
                        {u.isBanned ? '🚫 Блокиран' : '✓ Активен'}
                      </span>
                    </td>
                    <td>
                      <div className={styles.actions}>
                        {!u.isStreamer && !u.isAdmin && (
                          <button className={styles.actionBtn} onClick={() => makeStreamer(u)}>
                            📡 Стриймър
                          </button>
                        )}
                        {!u.isAdmin && (
                          <button
                            className={`${styles.actionBtn} ${u.isBanned ? styles.unbanBtn : styles.banBtn}`}
                            onClick={() => banUser(u)}
                          >
                            {u.isBanned ? 'Разблокирай' : 'Блокирай'}
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
                {!loading && users.length === 0 && (
                  <tr><td colSpan={5} className={styles.emptyRow}>
                    {search ? `Няма резултати за „${search}"` : 'Няма потребители'}
                  </td></tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* ── Streams ───────────────────────────────────────────────────────────── */}
      {tab === 'streams' && (
        <div className={styles.tableSection}>
          <div className={styles.tableHeader}>
            <h2 className={styles.sectionTitle}>Активни стриймове</h2>
            <button className={styles.refreshBtn} onClick={() => {
              setLoading(true)
              api.get('/admin/streams/live')
                .then(({ data }) => setStreams(data ?? []))
                .finally(() => setLoading(false))
            }}>
              🔄 Обнови
            </button>
          </div>
          <div className={styles.tableWrap}>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>Стриймър</th><th>Заглавие</th><th>Зрители</th><th>Категория</th><th>Действия</th>
                </tr>
              </thead>
              <tbody>
                {streams.map((s) => (
                  <tr key={s.id}>
                    <td>
                      <div className={styles.userCell}>
                        <div className={styles.liveDot} />
                        <span>{s.username}</span>
                      </div>
                    </td>
                    <td className={styles.truncateCell}>{s.title}</td>
                    <td><span className={styles.viewerCount}>{s.viewerCount.toLocaleString('bg-BG')}</span></td>
                    <td>{s.category ?? '—'}</td>
                    <td>
                      <button
                        className={`${styles.actionBtn} ${styles.banBtn}`}
                        onClick={() => terminateStream(s)}
                      >
                        ✕ Прекрати
                      </button>
                    </td>
                  </tr>
                ))}
                {!loading && streams.length === 0 && (
                  <tr><td colSpan={5} className={styles.emptyRow}>Няма активни стриймове</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  )
}

function StatCard({ label, value, icon, color }: { label: string; value: number; icon: string; color: string }) {
  return (
    <div className={`${styles.statCard} ${styles[`stat_${color}`]}`}>
      <span className={styles.statIcon}>{icon}</span>
      <div>
        <p className={styles.statValue}>{value.toLocaleString('bg-BG')}</p>
        <p className={styles.statLabel}>{label}</p>
      </div>
    </div>
  )
}
