import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'
import styles from './AuthPages.module.css'

// ── Login ─────────────────────────────────────────────────────────────────────

export function LoginPage() {
  const [email, setEmail]       = useState('')
  const [password, setPassword] = useState('')
  const [error, setError]       = useState('')
  const { login, isLoading }    = useAuthStore()
  const navigate                = useNavigate()

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    try {
      await login(email, password)
      navigate('/')
    } catch (err: any) {
      setError(err?.response?.data?.error ?? 'Грешен имейл или парола')
    }
  }

  return (
    <div className={styles.page}>
      <div className={styles.card}>
        <div className={styles.logo}>▶ StreamBG</div>
        <h1 className={styles.heading}>Добре дошъл обратно</h1>
        <p className={styles.sub}>Влез в профила си</p>

        {error && <div className={styles.error}>{error}</div>}

        <form onSubmit={handleSubmit} className={styles.form}>
          <label className={styles.label}>
            Имейл
            <input
              type="email" value={email} autoComplete="email"
              onChange={(e) => setEmail(e.target.value)}
              className={styles.input} placeholder="ti@example.com" required
            />
          </label>
          <label className={styles.label}>
            Парола
            <input
              type="password" value={password} autoComplete="current-password"
              onChange={(e) => setPassword(e.target.value)}
              className={styles.input} placeholder="••••••••" required
            />
          </label>
          <button type="submit" className={styles.submitBtn} disabled={isLoading}>
            {isLoading ? <><span className={styles.spinner} /> Влизане...</> : 'Вход'}
          </button>
        </form>

        <p className={styles.switch}>
          Нямаш акаунт? <Link to="/register">Регистрирай се</Link>
        </p>
      </div>
    </div>
  )
}

// ── Password strength ─────────────────────────────────────────────────────────

function passwordStrength(pw: string): { level: 0 | 1 | 2 | 3; label: string } {
  if (pw.length === 0) return { level: 0, label: '' }
  let score = 0
  if (pw.length >= 8)  score++
  if (/[A-Z]/.test(pw)) score++
  if (/[0-9]/.test(pw)) score++
  if (/[^A-Za-z0-9]/.test(pw)) score++
  if (score <= 1) return { level: 1, label: 'Слаба' }
  if (score === 2) return { level: 2, label: 'Средна' }
  return { level: 3, label: 'Силна' }
}

// ── Register ──────────────────────────────────────────────────────────────────

export function RegisterPage() {
  const [username, setUsername]         = useState('')
  const [email, setEmail]               = useState('')
  const [password, setPassword]         = useState('')
  const [confirmPassword, setConfirm]   = useState('')
  const [error, setError]               = useState('')
  const { register, isLoading }         = useAuthStore()
  const navigate                        = useNavigate()
  const strength                        = passwordStrength(password)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')

    if (!/^[a-zA-Z0-9_]{3,32}$/.test(username)) {
      setError('Потребителското име може да съдържа само букви, цифри и _ (3–32 знака)')
      return
    }
    if (password.length < 8) {
      setError('Паролата трябва да е поне 8 символа')
      return
    }
    if (password !== confirmPassword) {
      setError('Паролите не съвпадат')
      return
    }

    try {
      await register(username, email, password)
      navigate('/')
    } catch (err: any) {
      setError(err?.response?.data?.error ?? 'Грешка при регистрацията')
    }
  }

  return (
    <div className={styles.page}>
      <div className={styles.card}>
        <div className={styles.logo}>▶ StreamBG</div>
        <h1 className={styles.heading}>Създай акаунт</h1>
        <p className={styles.sub}>Присъедини се към общността</p>

        {error && <div className={styles.error}>{error}</div>}

        <form onSubmit={handleSubmit} className={styles.form}>
          <label className={styles.label}>
            Потребителско име
            <input
              type="text" value={username} autoComplete="username"
              onChange={(e) => setUsername(e.target.value)}
              className={styles.input} placeholder="cool_streamer"
              required minLength={3} maxLength={32}
            />
            <span className={styles.hint}>Само букви, цифри и _ · 3–32 знака</span>
          </label>

          <label className={styles.label}>
            Имейл
            <input
              type="email" value={email} autoComplete="email"
              onChange={(e) => setEmail(e.target.value)}
              className={styles.input} placeholder="ti@example.com" required
            />
          </label>

          <label className={styles.label}>
            Парола
            <input
              type="password" value={password} autoComplete="new-password"
              onChange={(e) => setPassword(e.target.value)}
              className={styles.input} placeholder="Поне 8 символа" required minLength={8}
            />
            {password.length > 0 && (
              <div className={styles.strengthBar}>
                <div className={`${styles.strengthFill} ${styles[`strength${strength.level}`]}`} />
                <span className={`${styles.strengthLabel} ${styles[`strengthText${strength.level}`]}`}>
                  {strength.label}
                </span>
              </div>
            )}
          </label>

          <label className={styles.label}>
            Потвърди парола
            <input
              type="password" value={confirmPassword} autoComplete="new-password"
              onChange={(e) => setConfirm(e.target.value)}
              className={`${styles.input} ${confirmPassword && confirmPassword !== password ? styles.inputError : ''}`}
              placeholder="••••••••" required
            />
            {confirmPassword && confirmPassword !== password && (
              <span className={styles.fieldError}>Паролите не съвпадат</span>
            )}
          </label>

          <button
            type="submit"
            className={styles.submitBtn}
            disabled={isLoading || (confirmPassword.length > 0 && confirmPassword !== password)}
          >
            {isLoading ? <><span className={styles.spinner} /> Регистрация...</> : 'Регистрирай се'}
          </button>
        </form>

        <p className={styles.switch}>
          Вече имаш акаунт? <Link to="/login">Влез</Link>
        </p>
      </div>
    </div>
  )
}
