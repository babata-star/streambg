import { useState, useRef, useEffect, KeyboardEvent } from 'react'
import { Link, NavLink, useNavigate } from 'react-router-dom'
import { useAuthStore } from '../../store/authStore'
import styles from './Navbar.module.css'

export function Navbar() {
  const { user, logout }          = useAuthStore()
  const navigate                  = useNavigate()
  const [searchQ, setSearchQ]     = useState('')
  const [showMenu, setShowMenu]   = useState(false)
  const menuRef                   = useRef<HTMLDivElement>(null)

  // Close dropdown when clicking outside
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setShowMenu(false)
      }
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  const handleSearch = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter' && searchQ.trim()) {
      navigate(`/?q=${encodeURIComponent(searchQ.trim())}`)
      setSearchQ('')
    }
  }

  const handleLogout = async () => {
    setShowMenu(false)
    await logout()
    navigate('/')
  }

  return (
    <nav className={styles.nav}>
      {/* Logo */}
      <Link to="/" className={styles.logo}>
        <span className={styles.logoIcon}>▶</span>
        <span className={styles.logoText}>StreamBG</span>
      </Link>

      {/* Nav links */}
      <div className={styles.navLinks}>
        <NavLink to="/clips" className={({ isActive }) => `${styles.navLink} ${isActive ? styles.navLinkActive : ''}`}>
          Клипове
        </NavLink>
        <NavLink to="/vods" className={({ isActive }) => `${styles.navLink} ${isActive ? styles.navLinkActive : ''}`}>
          ВОД
        </NavLink>
      </div>

      {/* Search */}
      <div className={styles.search}>
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/>
        </svg>
        <input
          placeholder="Търси стриймове... (Enter)"
          value={searchQ}
          onChange={(e) => setSearchQ(e.target.value)}
          onKeyDown={handleSearch}
        />
      </div>

      {/* Actions */}
      <div className={styles.actions}>
        {user ? (
          <>
            {user.isStreamer && (
              <Link to="/dashboard" className={styles.streamBtn}>
                <span className={styles.liveDot} />
                Стриймвай
              </Link>
            )}

            {/* Avatar + dropdown */}
            <div className={styles.avatarWrap} ref={menuRef}>
              <button
                className={styles.avatar}
                onClick={() => setShowMenu((v) => !v)}
                title={user.username}
              >
                {user.avatarUrl
                  ? <img src={user.avatarUrl} alt={user.username} />
                  : <span>{user.username[0].toUpperCase()}</span>}
              </button>

              {showMenu && (
                <div className={styles.dropdown}>
                  {/* User info */}
                  <div className={styles.dropUser}>
                    <div className={styles.dropAvatar}>
                      {user.avatarUrl
                        ? <img src={user.avatarUrl} alt={user.username} />
                        : <span>{user.username[0].toUpperCase()}</span>}
                    </div>
                    <div>
                      <p className={styles.dropUsername}>{user.username}</p>
                      <p className={styles.dropRole}>
                        {user.isAdmin ? '🛡 Администратор' : user.isStreamer ? '📡 Стриймър' : '👤 Зрител'}
                      </p>
                    </div>
                  </div>

                  <div className={styles.dropDivider} />

                  {user.isStreamer && (
                    <Link to="/dashboard" className={styles.dropItem} onClick={() => setShowMenu(false)}>
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                        <rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/>
                        <rect x="14" y="14" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/>
                      </svg>
                      Табло на стриймъра
                    </Link>
                  )}

                  {user.isAdmin && (
                    <Link to="/admin" className={styles.dropItem} onClick={() => setShowMenu(false)}>
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                        <path d="M12 2L2 7l10 5 10-5-10-5z"/><path d="M2 17l10 5 10-5"/><path d="M2 12l10 5 10-5"/>
                      </svg>
                      Администрация
                    </Link>
                  )}

                  <div className={styles.dropDivider} />

                  <button className={`${styles.dropItem} ${styles.dropLogout}`} onClick={handleLogout}>
                    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/>
                      <polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/>
                    </svg>
                    Изход
                  </button>
                </div>
              )}
            </div>
          </>
        ) : (
          <>
            <Link to="/login" className={styles.btnGhost}>Вход</Link>
            <Link to="/register" className={styles.btnAccent}>Регистрация</Link>
          </>
        )}
      </div>
    </nav>
  )
}
