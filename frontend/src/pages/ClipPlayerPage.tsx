import { useState, useEffect, useRef, useCallback } from 'react'
import { useParams } from 'react-router-dom'
import api from '../api/client'
import styles from './ClipPlayerPage.module.css'

interface ClipInfo {
  id: number
  title: string
  streamerUsername: string
  creatorUsername: string
  viewCount: number
  videoUrl: string
}

export function ClipPlayerPage() {
  const { id } = useParams<{ id: string }>()
  const [clip, setClip] = useState<ClipInfo | null>(null)
  const [loading, setLoading] = useState(true)
  const [muted, setMuted] = useState(false)
  const [volume, setVolume] = useState(0.8)
  const [fullscreen, setFullscreen] = useState(false)
  const [showControls, setShowControls] = useState(false)
  const videoRef = useRef<HTMLVideoElement>(null)

  useEffect(() => {
    if (!id) return
    api.get(`/clips/${id}`)
      .then(({ data }) => {
        setClip(data)
        api.post(`/clips/${id}/view`).catch(() => {})
      })
      .catch(() => setClip(null))
      .finally(() => setLoading(false))
  }, [id])

  const toggleMute = useCallback(() => {
    setMuted((m) => { if (videoRef.current) videoRef.current.muted = !m; return !m })
  }, [])

  const changeVolume = useCallback((v: number) => {
    setVolume(v)
    if (videoRef.current) { videoRef.current.volume = v; videoRef.current.muted = v === 0 }
    setMuted(v === 0)
  }, [])

  const toggleFullscreen = useCallback(() => {
    if (!document.fullscreenElement) {
      videoRef.current?.parentElement?.requestFullscreen()
      setFullscreen(true)
    } else {
      document.exitFullscreen()
      setFullscreen(false)
    }
  }, [])

  if (loading) {
    return (
      <div className={styles.centered}>
        <div className={styles.spinner} />
      </div>
    )
  }

  if (!clip) {
    return (
      <div className={styles.centered}>
        <div className={styles.emptyIcon}>🎬</div>
        <h2>Клипът не е намерен</h2>
        <p>Провери дали URL адресът е правилен</p>
      </div>
    )
  }

  return (
    <div className={styles.page}>
      <div
        className={styles.playerWrapper}
        onMouseEnter={() => setShowControls(true)}
        onMouseLeave={() => setShowControls(false)}
      >
        <video
          ref={videoRef}
          className={styles.video}
          src={clip.videoUrl}
          playsInline
          autoPlay
          muted={muted}
        />

        <div className={`${styles.controls} ${showControls ? styles.visible : ''}`}>
          <div className={styles.volumeArea}>
            <button className={styles.ctrlBtn} onClick={toggleMute}>
              {muted || volume === 0
                ? <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><path d="M11 5L6 9H2v6h4l5 4V5zM23 9l-6 6M17 9l6 6"/></svg>
                : <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><polygon points="11 5 6 9 2 9 2 15 6 15 11 19 11 5"/><path d="M19.07 4.93a10 10 0 0 1 0 14.14M15.54 8.46a5 5 0 0 1 0 7.07"/></svg>
              }
            </button>
            <input
              type="range" min={0} max={1} step={0.05}
              value={muted ? 0 : volume}
              onChange={(e) => changeVolume(parseFloat(e.target.value))}
              className={styles.volumeSlider}
            />
          </div>
          <div className={styles.titleArea}>
            <span className={styles.playerTitle}>{clip.title}</span>
          </div>
          <div className={styles.rightCtrls}>
            <button className={styles.ctrlBtn} onClick={toggleFullscreen}>
              {fullscreen
                ? <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M8 3v3a2 2 0 0 1-2 2H3m18 0h-3a2 2 0 0 1-2-2V3m0 18v-3a2 2 0 0 1 2-2h3M3 16h3a2 2 0 0 1 2 2v3"/></svg>
                : <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M8 3H5a2 2 0 0 0-2 2v3m18 0V5a2 2 0 0 0-2-2h-3m0 18h3a2 2 0 0 0 2-2v-3M3 16v3a2 2 0 0 0 2 2h3"/></svg>
              }
            </button>
          </div>
        </div>
      </div>

      <div className={styles.info}>
        <h1 className={styles.title}>{clip.title}</h1>
        <div className={styles.meta}>
          <span className={styles.metaItem}>{clip.streamerUsername}</span>
          <span className={styles.dot}>·</span>
          <span className={styles.metaItem}>Клип от {clip.creatorUsername}</span>
          <span className={styles.dot}>·</span>
          <span className={styles.metaItem}>
            <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor">
              <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/>
              <circle cx="12" cy="12" r="3"/>
            </svg>
            {clip.viewCount.toLocaleString('bg-BG')} гледания
          </span>
        </div>
      </div>
    </div>
  )
}
