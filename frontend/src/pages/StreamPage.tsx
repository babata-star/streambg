import { useState, useEffect } from 'react'
import { useParams } from 'react-router-dom'
import api from '../api/client'
import { VideoPlayer } from '../components/stream/VideoPlayer'
import { Chat } from '../components/chat/Chat'
import { useChatHub } from '../hooks/useChatHub'
import styles from './StreamPage.module.css'

interface StreamInfo {
  id: number
  userId: string
  username: string
  avatarUrl?: string
  title: string
  description?: string
  category?: string
  hlsUrl: string
  viewerCount: number
  isLive: boolean
  startedAt?: string
}

export function StreamPage() {
  const { username } = useParams<{ username: string }>()
  const [stream, setStream] = useState<StreamInfo | null>(null)
  const [loading, setLoading] = useState(true)
  const { viewers } = useChatHub(stream?.id?.toString())

  useEffect(() => {
    if (!username) return
    api.get(`/streams/${username}`)
      .then(({ data }) => setStream(data))
      .catch(() => setStream(null))
      .finally(() => setLoading(false))
  }, [username])

  if (loading) {
    return (
      <div className={styles.loading}>
        <div className={styles.spinner} />
      </div>
    )
  }

  if (!stream) {
    return (
      <div className={styles.notFound}>
        <div className={styles.notFoundIcon}>🔍</div>
        <h2>Стриймърът не е намерен</h2>
        <p>Провери дали URL адресът е правилен</p>
      </div>
    )
  }

  return (
    <div className={styles.layout}>
      <div className={styles.main}>
        {/* Video player */}
        <VideoPlayer
          hlsUrl={stream.isLive ? stream.hlsUrl : undefined}
          isLive={stream.isLive}
          title={stream.title}
          streamerName={stream.username}
          viewers={viewers}
        />

        {/* Stream info */}
        <div className={styles.info}>
          <div className={styles.streamerRow}>
            <div className={styles.avatar}>
              {stream.avatarUrl
                ? <img src={stream.avatarUrl} alt={stream.username} />
                : stream.username[0].toUpperCase()}
            </div>
            <div className={styles.streamerInfo}>
              <h1 className={styles.title}>{stream.title}</h1>
              <div className={styles.meta}>
                <span className={styles.username}>{stream.username}</span>
                {stream.category && (
                  <span className={styles.category}>{stream.category}</span>
                )}
                {stream.isLive && (
                  <span className={styles.liveIndicator}>
                    <span className={styles.liveDot} />НА ЖИВО
                  </span>
                )}
              </div>
            </div>
            <div className={styles.actions}>
              <button className={styles.followBtn}>
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                  <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
                </svg>
                Последвай
              </button>
            </div>
          </div>

          {stream.description && (
            <p className={styles.description}>{stream.description}</p>
          )}
        </div>
      </div>

      {/* Chat panel */}
      <Chat streamId={stream.id?.toString()} />
    </div>
  )
}
