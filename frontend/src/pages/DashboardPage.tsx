import { useState, useEffect } from 'react'
import api from '../api/client'
import { useAuthStore } from '../store/authStore'
import styles from './DashboardPage.module.css'

const CATEGORIES = ['Игри', 'Музика', 'Изкуство', 'Спорт', 'Образование', 'Технологии', 'Готвене', 'Пътешествия', 'Забавление', 'Друго']

const rtmpServer = `rtmp://${window.location.hostname}:1935/live`

export function DashboardPage() {
  const { user }   = useAuthStore()
  const [streamKey, setStreamKey] = useState('')
  const [showKey, setShowKey] = useState(false)
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [category, setCategory] = useState('')
  const [relayYT, setRelayYT] = useState(false)
  const [relayFB, setRelayFB] = useState(false)
  const [relayTT, setRelayTT] = useState(false)
  const [ytKey, setYtKey] = useState('')
  const [fbKey, setFbKey] = useState('')
  const [ttKey, setTtKey] = useState('')
  const [saved, setSaved] = useState(false)
  const [copiedKey, setCopiedKey] = useState(false)
  const [isLive, setIsLive] = useState(false)

  useEffect(() => {
    // Load stream key
    api.get('/streams/my/key').then(({ data }) => setStreamKey(data.streamKey)).catch(() => {})

    // Pre-load current stream settings so fields aren't blank
    if (user?.username) {
      api.get(`/streams/${user.username}`).then(({ data }) => {
        if (data) {
          setTitle(data.title ?? '')
          setDescription(data.description ?? '')
          setCategory(data.category ?? '')
          setIsLive(data.isLive ?? false)
        }
      }).catch(() => {})
    }
  }, [user?.username])

  const saveSettings = async () => {
    await api.put('/streams/settings', { title, description, category })
    await api.put('/streams/my/social', {
      youTubeStreamKey: ytKey, facebookStreamKey: fbKey, tikTokStreamKey: ttKey,
      relayToYouTube: relayYT, relayToFacebook: relayFB, relayToTikTok: relayTT,
    })
    setSaved(true)
    setTimeout(() => setSaved(false), 2500)
  }

  const regenerateKey = async () => {
    if (!confirm('Сигурен ли си? Стария ключ ще спре да работи.')) return
    const { data } = await api.post('/streams/my/regenerate-key')
    setStreamKey(data.streamKey)
  }

  const copyKey = () => {
    navigator.clipboard.writeText(streamKey)
    setCopiedKey(true)
    setTimeout(() => setCopiedKey(false), 2000)
  }

  return (
    <div className={styles.page}>
      <div className={styles.pageHeader}>
        <h1 className={styles.pageTitle}>🎬 Стриймър табло</h1>
        {isLive && (
          <span className={styles.liveBadge}>
            <span className={styles.liveDotRed} /> НА ЖИВО
          </span>
        )}
      </div>

      <div className={styles.grid}>
        {/* Stream key card */}
        <div className={styles.card}>
          <h2 className={styles.cardTitle}>Stream Key</h2>
          <p className={styles.cardSub}>Използвай в OBS или друг стрийминг софтуер</p>

          <div className={styles.keyBox}>
            <code className={styles.keyValue}>
              {showKey ? streamKey : '•'.repeat(Math.min(streamKey.length, 32))}
            </code>
            <div className={styles.keyActions}>
              <button className={styles.iconBtn} onClick={() => setShowKey((v) => !v)} title={showKey ? 'Скрий' : 'Покажи'}>
                {showKey
                  ? <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/></svg>
                  : <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
                }
              </button>
              <button className={styles.iconBtn} onClick={copyKey} title="Копирай">
                {copiedKey
                  ? <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="var(--success)" strokeWidth="2"><polyline points="20 6 9 17 4 12"/></svg>
                  : <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>
                }
              </button>
            </div>
          </div>

          <div className={styles.serverInfo}>
            <p className={styles.serverRow}>
              <span className={styles.serverLabel}>RTMP сървър:</span>
              <code className={styles.serverVal}>{rtmpServer}</code>
              <button className={styles.iconBtn} title="Копирай сървъра" onClick={() => navigator.clipboard.writeText(rtmpServer)}>
                <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/>
                </svg>
              </button>
            </p>
          </div>

          <button className={styles.dangerBtn} onClick={regenerateKey}>
            🔄 Генерирай нов ключ
          </button>
        </div>

        {/* Settings card */}
        <div className={styles.card}>
          <h2 className={styles.cardTitle}>Настройки на стрийма</h2>

          <div className={styles.field}>
            <label className={styles.fieldLabel}>Заглавие</label>
            <input
              className={styles.input} value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="Играя FIFA с приятели..."
              maxLength={140}
            />
          </div>

          <div className={styles.field}>
            <label className={styles.fieldLabel}>Описание</label>
            <textarea
              className={`${styles.input} ${styles.textarea}`} value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Кратко описание на стрийма..."
              rows={3} maxLength={500}
            />
          </div>

          <div className={styles.field}>
            <label className={styles.fieldLabel}>Категория</label>
            <select className={styles.select} value={category} onChange={(e) => setCategory(e.target.value)}>
              <option value="">-- Избери категория --</option>
              {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
          </div>
        </div>

        {/* Social relay card */}
        <div className={`${styles.card} ${styles.fullWidth}`}>
          <h2 className={styles.cardTitle}>📡 Паралелно споделяне в социалните мрежи</h2>
          <p className={styles.cardSub}>Стриймът ти ще се появи едновременно на избраните платформи</p>

          <div className={styles.socialGrid}>
            <SocialInput
              platform="YouTube Live" emoji="📺" color="#FF0000"
              enabled={relayYT} onToggle={setRelayYT}
              keyValue={ytKey} onKeyChange={setYtKey}
              placeholder="xxxx-xxxx-xxxx-xxxx"
              helpText="Stream → Go live → Stream settings → Stream key"
            />
            <SocialInput
              platform="Facebook Live" emoji="👥" color="#1877F2"
              enabled={relayFB} onToggle={setRelayFB}
              keyValue={fbKey} onKeyChange={setFbKey}
              placeholder="FB-xxxxxxxxxxxx"
              helpText="Creator Studio → Go Live → Use Stream Key"
            />
            <SocialInput
              platform="TikTok Live" emoji="🎵" color="#ff0050"
              enabled={relayTT} onToggle={setRelayTT}
              keyValue={ttKey} onKeyChange={setTtKey}
              placeholder="tt_xxxxxxxxxxxx"
              helpText="TikTok Studio → LIVE → Go LIVE → Stream Key"
            />
          </div>
        </div>
      </div>

      {/* Save button */}
      <div className={styles.saveRow}>
        <button className={`${styles.saveBtn} ${saved ? styles.saved : ''}`} onClick={saveSettings}>
          {saved ? '✓ Запазено!' : 'Запази настройките'}
        </button>
      </div>
    </div>
  )
}

function SocialInput({ platform, emoji, color, enabled, onToggle, keyValue, onKeyChange, placeholder, helpText }: any) {
  return (
    <div className={`${styles.socialCard} ${enabled ? styles.socialEnabled : ''}`} style={{ '--sc': color } as any}>
      <div className={styles.socialHeader}>
        <span className={styles.socialEmoji}>{emoji}</span>
        <span className={styles.socialName}>{platform}</span>
        <label className={styles.toggle}>
          <input type="checkbox" checked={enabled} onChange={(e) => onToggle(e.target.checked)} />
          <span className={styles.toggleTrack}>
            <span className={styles.toggleThumb} />
          </span>
        </label>
      </div>
      {enabled && (
        <div className={styles.socialKeyArea}>
          <input
            className={styles.input}
            value={keyValue} onChange={(e) => onKeyChange(e.target.value)}
            placeholder={placeholder}
          />
          <p className={styles.helpText}>💡 {helpText}</p>
        </div>
      )}
    </div>
  )
}
