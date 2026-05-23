import { useEffect, useState, useRef } from 'react'
import styles from './DonationAlert.module.css'

export interface DonationEvent {
  id: number
  donorName: string
  amount: number
  currency: string
  message?: string
  emoji: string
  animationMs: number
}

interface Props {
  donation: DonationEvent | null
}

export function DonationAlert({ donation }: Props) {
  const [visible, setVisible] = useState(false)
  const [current, setCurrent] = useState<DonationEvent | null>(null)
  const timerRef = useRef<ReturnType<typeof setTimeout>>()

  useEffect(() => {
    if (!donation) return

    // Postavi u red ako stigla nova dok stara se prikazuje
    setCurrent(donation)
    setVisible(true)

    clearTimeout(timerRef.current)
    timerRef.current = setTimeout(() => {
      setVisible(false)
    }, donation.animationMs)

    return () => clearTimeout(timerRef.current)
  }, [donation])

  if (!current) return null

  return (
    <div className={`${styles.alert} ${visible ? styles.visible : styles.hidden}`}>
      <div className={styles.emojiFloat} aria-hidden="true">
        {[...Array(6)].map((_, i) => (
          <span key={i} className={styles.particle}
            style={{ '--delay': `${i * 0.15}s`, '--x': `${(i % 3 - 1) * 40}px` } as any}>
            {current.emoji}
          </span>
        ))}
      </div>

      <div className={styles.card}>
        <div className={styles.topRow}>
          <span className={styles.mainEmoji}>{current.emoji}</span>
          <div className={styles.info}>
            <span className={styles.donorName}>{current.donorName}</span>
            <span className={styles.donated}>дари</span>
          </div>
          <div className={styles.amount}>
            {current.amount.toLocaleString('bg-BG', { minimumFractionDigits: 2 })}
            <span className={styles.currency}> лв.</span>
          </div>
        </div>

        {current.message && (
          <p className={styles.message}>"{current.message}"</p>
        )}
      </div>
    </div>
  )
}
