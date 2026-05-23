import { useState, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../api/client'
import styles from './ClipsPage.module.css'

interface Clip {
  id: number
  title: string
  streamerUsername: string
  thumbnailUrl?: string
  viewCount: number
  durationSeconds: number
}

const SORTS = [
  { label: 'Нови', value: 'newest' },
  { label: 'Популярни', value: 'popular' },
]

function formatDuration(s: number) {
  const m = Math.floor(s / 60)
  const sec = s % 60
  return `${m}:${String(sec).padStart(2, '0')}`
}

export function ClipsPage() {
  const navigate = useNavigate()
  const [clips, setClips] = useState<Clip[]>([])
  const [sort, setSort] = useState('newest')
  const [page, setPage] = useState(1)
  const [hasMore, setHasMore] = useState(true)
  const [loading, setLoading] = useState(true)
  const [loadingMore, setLoadingMore] = useState(false)
  const sentinelRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const firstPage = page === 1
    if (firstPage) setLoading(true)
    else setLoadingMore(true)

    api.get('/clips', { params: { sort, page, pageSize: 24 } })
      .then(({ data }) => {
        const items: Clip[] = data.items ?? data ?? []
        setClips((prev) => firstPage ? items : [...prev, ...items])
        setHasMore(items.length === 24)
      })
      .catch(() => { if (firstPage) setClips([]) })
      .finally(() => { setLoading(false); setLoadingMore(false) })
  }, [sort, page])

  const changeSort = (s: string) => {
    if (s === sort) return
    setSort(s)
    setPage(1)
    setHasMore(true)
  }

  useEffect(() => {
    const el = sentinelRef.current
    if (!el || !hasMore || loading || loadingMore) return
    const ob = new IntersectionObserver(([entry]) => {
      if (entry.isIntersecting) setPage((p) => p + 1)
    }, { threshold: 0.1 })
    ob.observe(el)
    return () => ob.disconnect()
  }, [hasMore, loading, loadingMore])

  return (
    <div className={styles.page}>
      <div className={styles.hero}>
        <h1 className={styles.heroTitle}>
          <span className={styles.heroAccent}>Клипове</span>
        </h1>
        <p className={styles.heroSub}>Най-добрите моменти от българските стриймове</p>
      </div>

      <div className={styles.sortBar}>
        {SORTS.map((s) => (
          <button
            key={s.value}
            className={`${styles.sortBtn} ${sort === s.value ? styles.active : ''}`}
            onClick={() => changeSort(s.value)}
          >
            {s.label}
          </button>
        ))}
      </div>

      {loading ? (
        <div className={styles.grid}>
          {[...Array(12)].map((_, i) => <div key={i} className={styles.skeleton} />)}
        </div>
      ) : clips.length === 0 ? (
        <div className={styles.empty}>
          <div className={styles.emptyIcon}>🎬</div>
          <h3>Няма клипове</h3>
          <p>Клиповете ще се появят тук скоро</p>
        </div>
      ) : (
        <>
          <div className={styles.grid}>
            {clips.map((clip) => (
              <ClipCard
                key={clip.id}
                clip={clip}
                onClick={() => navigate(`/clips/${clip.id}`)}
              />
            ))}
          </div>
          {loadingMore && (
            <div className={styles.loadMore}>
              <div className={styles.spinner} />
            </div>
          )}
          <div ref={sentinelRef} className={styles.sentinel} />
        </>
      )}
    </div>
  )
}

function ClipCard({ clip, onClick }: { clip: Clip; onClick: () => void }) {
  return (
    <div className={styles.card} onClick={onClick}>
      <div className={styles.thumb}>
        {clip.thumbnailUrl
          ? <img src={clip.thumbnailUrl} alt={clip.title} />
          : <div className={styles.thumbPlaceholder}>
              <svg width="32" height="32" viewBox="0 0 24 24" fill="var(--accent)">
                <polygon points="5 3 19 12 5 21 5 3"/>
              </svg>
            </div>
        }
        <span className={styles.duration}>{formatDuration(clip.durationSeconds)}</span>
      </div>
      <div className={styles.cardInfo}>
        <p className={`${styles.cardTitle} truncate`}>{clip.title}</p>
        <p className={styles.streamer}>{clip.streamerUsername}</p>
        <span className={styles.views}>
          <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor">
            <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/>
            <circle cx="12" cy="12" r="3"/>
          </svg>
          {clip.viewCount.toLocaleString('bg-BG')}
        </span>
      </div>
    </div>
  )
}
