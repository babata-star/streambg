// ── Добави в useChatHub.ts — обработка на DonationReceived ──────────────────

// Към интерфейса:
export interface DonationEvent {
  id: number
  donorName: string
  amount: number
  currency: string
  message?: string
  emoji: string
  animationMs: number
}

// Към useChatHub hook — добавяне на donation state:
/*
  const [donation, setDonation] = useState<DonationEvent | null>(null)

  connection.on('DonationReceived', (event: DonationEvent) => {
    setDonation({ ...event, id: Date.now() })   // force re-trigger
  })

  return { messages, viewers, connected, sendMessage, deleteMessage, donation }
*/

// ── Добави в StreamPage.tsx ─────────────────────────────────────────────────
/*
import { DonationAlert } from '../components/stream/DonationAlert'

// В JSX на StreamPage след VideoPlayer:
<DonationAlert donation={donation} />
*/

// ── Пълен пример за интеграция в StreamPage ─────────────────────────────────
import { useState, useEffect } from 'react'
import { useParams } from 'react-router-dom'
import api from '../api/client'
import { VideoPlayer } from '../components/stream/VideoPlayer'
import { Chat } from '../components/chat/Chat'
import { DonationAlert, DonationEvent } from '../components/stream/DonationAlert'
import { DonationForm } from '../components/stream/DonationForm'
import { SubscriptionPlans } from '../components/stream/SubscriptionPlans'
import { useChatHub } from '../hooks/useChatHub'
import styles from './StreamPage.module.css'

export function StreamPageV2() {
  const { username } = useParams<{ username: string }>()
  const [stream, setStream] = useState<any>(null)
  const [sidePanel, setSidePanel] = useState<'chat' | 'donate' | 'subscribe'>('chat')
  const [activeDonation, setActiveDonation] = useState<DonationEvent | null>(null)

  const { viewers, messages, connected, sendMessage, deleteMessage }
    = useChatHub(stream?.id?.toString())

  // Hook в SignalR — при DonationReceived покажи alert
  useEffect(() => {
    // Добавено в useChatHub:
    // connection.on('DonationReceived', (d) => setActiveDonation(d))
  }, [])

  useEffect(() => {
    if (!username) return
    api.get(`/streams/${username}`).then(r => setStream(r.data)).catch(() => {})
  }, [username])

  return (
    <div className={styles.layout}>
      <div className={styles.main}>
        <VideoPlayer
          hlsUrl={stream?.hlsUrl}
          isLive={stream?.isLive}
          title={stream?.title ?? ''}
          streamerName={stream?.username ?? ''}
          viewers={viewers}
        />

        {/* Donation alert overlay */}
        <DonationAlert donation={activeDonation} />

        {/* Stream info */}
        <div className={styles.info}>
          <div className={styles.streamerRow}>
            <div className={styles.avatar}>
              {stream?.username?.[0]?.toUpperCase()}
            </div>
            <div className={styles.streamerInfo}>
              <h1 className={styles.title}>{stream?.title}</h1>
              <span className={styles.username}>{stream?.username}</span>
            </div>
          </div>
        </div>
      </div>

      {/* Right sidebar */}
      <aside className={styles.sidebar}>
        {/* Tab switcher */}
        <div className={styles.sidebarTabs}>
          {(['chat', 'donate', 'subscribe'] as const).map(tab => (
            <button
              key={tab}
              className={`${styles.sideTab} ${sidePanel === tab ? styles.sideTabActive : ''}`}
              onClick={() => setSidePanel(tab)}
            >
              {{ chat: '💬', donate: '💜', subscribe: '⭐' }[tab]}
            </button>
          ))}
        </div>

        {sidePanel === 'chat' && (
          <Chat streamId={stream?.id?.toString()} />
        )}

        {sidePanel === 'donate' && stream && (
          <DonationForm
            creatorUsername={stream.username}
            streamId={stream.id}
            onDonated={() => setSidePanel('chat')}
          />
        )}

        {sidePanel === 'subscribe' && stream && (
          <SubscriptionPlans
            creatorUsername={stream.username}
            onSubscribed={() => setSidePanel('chat')}
          />
        )}
      </aside>
    </div>
  )
}
