import { useState, useEffect } from 'react'
import { Link } from 'react-router-dom'
import api from '../api/client'
import styles from './HomePage.module.css'

interface StreamCard {
  id: number
  username: string
  avatarUrl?: string
  title: string
  category?: string
  viewerCount: number
  isLive: boolean
  thumbnailUrl?: string
}

const CATEGORIES = ['Всички', 'Игри', 'Музика', 'Изкуство', 'Спорт', 'Образование', 'Технологии', 'Готвене']

export function HomePage() {
  const [streams, setStreams] = useState<StreamCard[]>([])
  const [category, setCategory] = useState('Всички')
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    setLoading(true)
    api.get('/streams', {
      params: category !== 'Всички' ? { category } : undefined
    })
      .then(({ data }) => setStreams(data.items ?? []))
      .catch(() => setStreams([]))
      .finally(() => setLoading(false))
  }, [category])

  return (
    <div className={styles.page}>
      {/* Hero */}
      <div className={styles.hero}>
        <h1 className={styles.heroTitle}>
          На <span className={styles.heroAccent}>живо</span> сега
        </h1>
        <p className={styles.heroSub}>Гледай български стриймъри в реално време</p>
      </div>

      {/* Category filter */}
      <div className={styles.categories}>
        {CATEGORIES.map((cat) => (
          <button
            key={cat}
            className={`${styles.catBtn} ${category === cat ? styles.active : ''}`}
            onClick={() => setCategory(cat)}
          >
            {cat}
          </button>
        ))}
      </div>

      {/* Grid */}
      {loading ? (
        <div className={styles.loadingGrid}>
          {[...Array(8)].map((_, i) => (
            <div key={i} className={styles.skeleton} />
          ))}
        </div>
      ) : streams.length === 0 ? (
        <div className={styles.empty}>
          <div className={styles.emptyIcon}>📺</div>
          <h3>Няма активни стриймове</h3>
          <p>Провери по-късно или стани първият стриймър!</p>
        </div>
      ) : (
        <div className={styles.grid}>
          {streams.map((s) => (
            <StreamCard key={s.id} stream={s} />
          ))}
        </div>
      )}
    </div>
  )
}

function StreamCard({ stream }: { stream: StreamCard }) {
  return (
    <Link to={`/stream/${stream.username}`} className={styles.card}>
      <div className={styles.thumb}>
        {stream.thumbnailUrl
          ? <img src={stream.thumbnailUrl} alt={stream.title} />
          : <div className={styles.thumbPlaceholder}>
              <svg width="40" height="40" viewBox="0 0 24 24" fill="var(--accent)">
                <polygon points="5 3 19 12 5 21 5 3"/>
              </svg>
            </div>
        }
        <span className={styles.livePill}>
          <span className={styles.liveDot} />НА ЖИВО
        </span>
        <span className={styles.viewers}>
          <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor">
            <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/>
            <circle cx="12" cy="12" r="3"/>
          </svg>
          {stream.viewerCount.toLocaleString('bg-BG')}
        </span>
      </div>

      <div className={styles.info}>
        <div className={styles.avatar}>
          {stream.avatarUrl
            ? <img src={stream.avatarUrl} alt={stream.username} />
            : <span>{stream.username[0].toUpperCase()}</span>}
        </div>
        <div className={styles.meta}>
          <p className={`${styles.title} truncate`}>{stream.title}</p>
          <p className={styles.username}>{stream.username}</p>
          {stream.category && <span className={styles.category}>{stream.category}</span>}
        </div>
      </div>
    </Link>
  )
}
