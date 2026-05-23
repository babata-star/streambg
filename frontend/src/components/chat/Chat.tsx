import { useEffect, useRef, useState } from 'react'
import { useChatHub, ChatMsg } from '../../hooks/useChatHub'
import { useAuthStore } from '../../store/authStore'
import styles from './Chat.module.css'

interface Props { streamId: string | undefined }

export function Chat({ streamId }: Props) {
  const { user } = useAuthStore()
  const { messages, viewers, connected, sendMessage, deleteMessage } = useChatHub(streamId)
  const [input, setInput] = useState('')
  const [autoScroll, setAutoScroll] = useState(true)
  const bottomRef = useRef<HTMLDivElement>(null)
  const listRef = useRef<HTMLDivElement>(null)

  // Auto-scroll to bottom
  useEffect(() => {
    if (autoScroll) bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, autoScroll])

  const handleScroll = () => {
    const el = listRef.current
    if (!el) return
    const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 80
    setAutoScroll(atBottom)
  }

  const handleSend = () => {
    const trimmed = input.trim()
    if (!trimmed || !user) return
    sendMessage(trimmed)
    setInput('')
  }

  const handleKey = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSend()
    }
  }

  return (
    <aside className={styles.chat}>
      {/* Header */}
      <div className={styles.header}>
        <span className={styles.headerTitle}>Чат на живо</span>
        <span className={styles.viewerBadge}>
          <svg width="13" height="13" viewBox="0 0 24 24" fill="currentColor">
            <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/>
            <circle cx="12" cy="12" r="3"/>
          </svg>
          {viewers.toLocaleString('bg-BG')}
        </span>
      </div>

      {/* Messages */}
      <div className={styles.messages} ref={listRef} onScroll={handleScroll}>
        {messages.length === 0 && (
          <div className={styles.empty}>
            {connected
              ? 'Бъди първият, който пише в чата!'
              : 'Свързване с чата...'}
          </div>
        )}
        {messages.map((msg) => (
          <MessageRow
            key={msg.id}
            msg={msg}
            isAdmin={user?.isAdmin}
            onDelete={() => deleteMessage(msg.id)}
          />
        ))}
        <div ref={bottomRef} />
      </div>

      {/* Scroll to bottom button */}
      {!autoScroll && (
        <button
          className={styles.scrollBtn}
          onClick={() => { setAutoScroll(true); bottomRef.current?.scrollIntoView({ behavior: 'smooth' }) }}
        >
          ↓ Нови съобщения
        </button>
      )}

      {/* Input area */}
      <div className={styles.inputArea}>
        {user ? (
          <>
            <div className={styles.inputRow}>
              <div className={styles.userAvatar}>
                {user.avatarUrl
                  ? <img src={user.avatarUrl} alt="" />
                  : user.username[0].toUpperCase()}
              </div>
              <textarea
                className={styles.textarea}
                placeholder="Напиши съобщение..."
                value={input}
                onChange={(e) => setInput(e.target.value.slice(0, 500))}
                onKeyDown={handleKey}
                rows={1}
                maxLength={500}
              />
            </div>
            <div className={styles.inputFooter}>
              <span className={styles.charCount}>{input.length}/500</span>
              <button
                className={styles.sendBtn}
                onClick={handleSend}
                disabled={!input.trim()}
              >
                Изпрати
              </button>
            </div>
          </>
        ) : (
          <div className={styles.loginPrompt}>
            <a href="/login">Влез в профила</a> за да пишеш в чата
          </div>
        )}
      </div>
    </aside>
  )
}

function MessageRow({ msg, isAdmin, onDelete }: { msg: ChatMsg; isAdmin?: boolean; onDelete: () => void }) {
  const [hover, setHover] = useState(false)

  return (
    <div
      className={styles.message}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
    >
      <span className={styles.username} style={{ color: msg.color }}>
        {msg.username}
      </span>
      <span className={styles.content}>{msg.content}</span>
      {isAdmin && hover && (
        <button className={styles.deleteBtn} onClick={onDelete} title="Изтрий">✕</button>
      )}
    </div>
  )
}
