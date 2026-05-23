import { useState, useCallback } from 'react'
import { useHlsPlayer } from '../../hooks/useHlsPlayer'
import styles from './VideoPlayer.module.css'

interface Props {
  hlsUrl: string | undefined
  isLive: boolean
  title: string
  streamerName: string
  viewers: number
}

export function VideoPlayer({ hlsUrl, isLive, title, streamerName, viewers }: Props) {
  const videoRef = useHlsPlayer(hlsUrl)
  const [muted, setMuted] = useState(false)
  const [volume, setVolume] = useState(0.8)
  const [fullscreen, setFullscreen] = useState(false)
  const [showControls, setShowControls] = useState(false)

  const toggleMute = useCallback(() => {
    setMuted((m) => {
      if (videoRef.current) videoRef.current.muted = !m
      return !m
    })
  }, [videoRef])

  const changeVolume = useCallback((v: number) => {
    setVolume(v)
    if (videoRef.current) {
      videoRef.current.volume = v
      videoRef.current.muted = v === 0
    }
    setMuted(v === 0)
  }, [videoRef])

  const toggleFullscreen = useCallback(() => {
    if (!document.fullscreenElement) {
      videoRef.current?.parentElement?.requestFullscreen()
      setFullscreen(true)
    } else {
      document.exitFullscreen()
      setFullscreen(false)
    }
  }, [videoRef])

  return (
    <div
      className={styles.wrapper}
      onMouseEnter={() => setShowControls(true)}
      onMouseLeave={() => setShowControls(false)}
    >
      {!isLive && (
        <div className={styles.offline}>
          <div className={styles.offlineIcon}>📺</div>
          <p>{streamerName} не стриймва в момента</p>
        </div>
      )}

      <video
        ref={videoRef}
        className={styles.video}
        playsInline
        autoPlay
        muted={muted}
      />

      {/* Live badge + info overlay */}
      {isLive && (
        <div className={styles.topBar}>
          <span className={styles.liveBadge}>
            <span className={styles.liveDot} />
            НА ЖИВО
          </span>
          <span className={styles.viewers}>
            <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor">
              <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/>
              <circle cx="12" cy="12" r="3"/>
            </svg>
            {viewers.toLocaleString('bg-BG')}
          </span>
        </div>
      )}

      {/* Bottom controls */}
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
          <span className={styles.streamTitle}>{title}</span>
        </div>

        <div className={styles.rightCtrls}>
          <button className={styles.ctrlBtn} onClick={toggleFullscreen} title="Fullscreen">
            {fullscreen
              ? <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M8 3v3a2 2 0 0 1-2 2H3m18 0h-3a2 2 0 0 1-2-2V3m0 18v-3a2 2 0 0 1 2-2h3M3 16h3a2 2 0 0 1 2 2v3"/></svg>
              : <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M8 3H5a2 2 0 0 0-2 2v3m18 0V5a2 2 0 0 0-2-2h-3m0 18h3a2 2 0 0 0 2-2v-3M3 16v3a2 2 0 0 0 2 2h3"/></svg>
            }
          </button>
        </div>
      </div>
    </div>
  )
}
