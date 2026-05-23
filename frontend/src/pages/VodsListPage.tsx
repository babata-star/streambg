import { useState, useEffect, useCallback } from 'react'
import { Link } from 'react-router-dom'
import api from '../api/client'
import styles from './VodsListPage.module.css'

interface Vod {
  id: number
  username: string
  avatarUrl?: string
  title: string
  category?: string
  thumbnailUrl?: string
  viewCount: number
  createdAt: string
  durationSeconds?: number
}

function formatDuration(s?: number) {
  if (!s) return ''
  const h = Math.floor(s / 3600)
  const m = Math.floor((s % 3600) / 60)
  const sec = s % 60
  if (h > 0) return `${h}:${String(m).padStart(2,'0')}:${String(sec).padStart(2,'0')}`
  return `${m}:${String(sec).padStart(2,'0')}`
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('bg-BG', { day: 'numeric', month: 'short', year: 'numeric' })
}

export function VodsListPage() {
  const [vods, setVods]       = useState<Vod[]>([])
  const [loading, setLoading] = useState(true)
  const [page, setPage]       = useState(1)
  const [hasMore, setHasMore] = useState(true)

  const load = useCallback(async (p: number, replace: boolean) => {
    setLoading(true)
    try {
      const { data } = await api.get('/vod', { params: { page: p, pageSize: 24 } })
      const items: Vod[] = data.items ?? []
      setVods((prev) => replace ? items : [...prev, ...items])
      setHasMore(items.length >= 24)
    } catch { /* offline */ }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { load(1, true) }, [load])

  const loadMore = () => {
    const next = page + 1
    setPage(next)
    load(next, false)
  }

  return (
    <div className={styles.page}>
      <div className={styles.pageHeader}>
        <h1 className={styles.title}>🎬 Видео архив (ВОД)</h1>
        <p className={styles.sub}>Гледай минали стриймове</p>
      </div>

      {loading && vods.length === 0 ? (
        <div className={styles.centered}>
          <div className={styles.spinner} />
        </div>
      ) : vods.length === 0 ? (
        <div className={styles.empty}>
          <span className={styles.emptyIcon}>🎬</span>
          <p>Все още няма записани видеа</p>
        </div>
      ) : (
        <>
          <div className={styles.grid}>
            {vods.map((v) => (
              <Link key={v.id} to={`/vod/${v.id}`} className={styles.card}>
                {/* Thumbnail */}
                <div className={styles.thumb}>
                  {v.thumbnailUrl
                    ? <img src={v.thumbnailUrl} alt={v.title} loading="lazy" />
                    : <div className={styles.thumbPlaceholder}>
                        <span>{v.username[0].toUpperCase()}</span>
                      </div>}
                  {v.durationSeconds && (
                    <span className={styles.duration}>{formatDuration(v.durationSeconds)}</span>
                  )}
                </div>

                {/* Info */}
                <div className={styles.info}>
                  <p className={styles.vodTitle}>{v.title}</p>
                  <div className={styles.meta}>
                    <span className={styles.author}>{v.username}</span>
                    {v.category && <span className={styles.cat}>{v.category}</span>}
                  </div>
                  <div className={styles.stats}>
                    <span>{v.viewCount.toLocaleString('bg-BG')} гледания</span>
                    <span>{formatDate(v.createdAt)}</span>
                  </div>
                </div>
              </Link>
            ))}
          </div>

          {hasMore && (
            <div className={styles.loadMore}>
              <button className={styles.loadMoreBtn} onClick={loadMore} disabled={loading}>
                {loading ? 'Зареждане…' : 'Зареди още'}
              </button>
            </div>
          )}
        </>
      )}
    </div>
  )
}
