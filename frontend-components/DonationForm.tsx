import { useState } from 'react'
import api from '../api/client'
import styles from './DonationForm.module.css'

const QUICK_AMOUNTS = [2, 5, 10, 20, 50]
const EMOJIS = ['💜', '🔥', '⭐', '👑', '💎', '🎉', '❤️', '🚀']

interface Props {
  creatorUsername: string
  streamId?: number
  onDonated?: () => void
}

export function DonationForm({ creatorUsername, streamId, onDonated }: Props) {
  const [amount, setAmount] = useState<number | ''>('')
  const [message, setMessage] = useState('')
  const [emoji, setEmoji] = useState('💜')
  const [isAnonymous, setIsAnonymous] = useState(false)
  const [loading, setLoading] = useState(false)
  const [sent, setSent] = useState(false)
  const [error, setError] = useState('')

  const handleSend = async () => {
    if (!amount || Number(amount) < 1) {
      setError('Минималното дарение е 1 лв.')
      return
    }
    setError('')
    setLoading(true)
    try {
      await api.post('/donations', {
        recipientUsername: creatorUsername,
        amount: Number(amount),
        message: message.trim() || undefined,
        emojiAnimation: emoji,
        isAnonymous,
        streamId
      })
      setSent(true)
      setTimeout(() => { setSent(false); setAmount(''); setMessage('') }, 3000)
      onDonated?.()
    } catch (err: any) {
      setError(err?.response?.data?.error ?? 'Грешка при изпращане')
    } finally {
      setLoading(false)
    }
  }

  if (sent) {
    return (
      <div className={styles.success}>
        <span className={styles.successEmoji}>{emoji}</span>
        <p>Дарението е изпратено!</p>
        <p className={styles.successSub}>Благодарим за подкрепата!</p>
      </div>
    )
  }

  return (
    <div className={styles.form}>
      <h4 className={styles.heading}>💜 Изпрати дарение</h4>

      {/* Quick amount buttons */}
      <div className={styles.quickRow}>
        {QUICK_AMOUNTS.map(a => (
          <button
            key={a}
            className={`${styles.quickBtn} ${amount === a ? styles.quickActive : ''}`}
            onClick={() => setAmount(a)}
          >
            {a} лв.
          </button>
        ))}
      </div>

      {/* Custom amount */}
      <div className={styles.amountRow}>
        <div className={styles.amountInput}>
          <span className={styles.currency}>лв.</span>
          <input
            type="number"
            min={1} max={9999}
            placeholder="Сума"
            value={amount}
            onChange={e => setAmount(e.target.value === '' ? '' : Number(e.target.value))}
            className={styles.input}
          />
        </div>
      </div>

      {/* Emoji picker */}
      <div className={styles.emojiRow}>
        {EMOJIS.map(e => (
          <button
            key={e}
            className={`${styles.emojiBtn} ${emoji === e ? styles.emojiActive : ''}`}
            onClick={() => setEmoji(e)}
          >
            {e}
          </button>
        ))}
      </div>

      {/* Message */}
      <textarea
        className={styles.textarea}
        placeholder="Добави съобщение (незадължително)..."
        value={message}
        onChange={e => setMessage(e.target.value.slice(0, 300))}
        maxLength={300}
        rows={2}
      />
      <span className={styles.charCount}>{message.length}/300</span>

      {/* Anonymous toggle */}
      <label className={styles.anonRow}>
        <input
          type="checkbox"
          checked={isAnonymous}
          onChange={e => setIsAnonymous(e.target.checked)}
        />
        <span>Изпрати анонимно</span>
      </label>

      {error && <p className={styles.error}>{error}</p>}

      <button
        className={styles.sendBtn}
        onClick={handleSend}
        disabled={loading || !amount}
      >
        {loading ? 'Изпраща се...' : `${emoji} Дари ${amount ? `${amount} лв.` : ''}`}
      </button>
    </div>
  )
}
