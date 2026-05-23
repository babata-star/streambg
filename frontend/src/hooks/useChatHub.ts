import { useEffect, useRef, useState, useCallback } from 'react'
import * as signalR from '@microsoft/signalr'
import { useAuthStore } from '../store/authStore'

export interface ChatMsg {
  id: number
  userId: string
  username: string
  avatarUrl?: string
  content: string
  color: string
  sentAt: string
}

export function useChatHub(streamId: string | undefined) {
  const { accessToken } = useAuthStore()
  const [messages, setMessages] = useState<ChatMsg[]>([])
  const [viewers, setViewers] = useState(0)
  const [connected, setConnected] = useState(false)
  const hubRef = useRef<signalR.HubConnection | null>(null)

  useEffect(() => {
    if (!streamId) return

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/chat', {
        accessTokenFactory: () => accessToken ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    connection.on('NewMessage', (msg: ChatMsg) => {
      setMessages((prev) => [...prev.slice(-199), msg])
    })

    connection.on('MessageDeleted', (id: number) => {
      setMessages((prev) => prev.filter((m) => m.id !== id))
    })

    connection.on('ViewerCountUpdated', (count: number) => {
      setViewers(count)
    })

    connection
      .start()
      .then(() => {
        setConnected(true)
        connection.invoke('JoinStream', streamId)
      })
      .catch(console.error)

    hubRef.current = connection

    return () => {
      connection.invoke('LeaveStream', streamId).catch(() => {})
      connection.stop()
      setConnected(false)
    }
  }, [streamId, accessToken])

  const sendMessage = useCallback(
    (content: string) => {
      if (!streamId || !hubRef.current || !connected) return
      hubRef.current.invoke('SendMessage', streamId, content).catch(console.error)
    },
    [streamId, connected]
  )

  const deleteMessage = useCallback(
    (messageId: number) => {
      if (!streamId || !hubRef.current) return
      hubRef.current.invoke('DeleteMessage', streamId, messageId).catch(console.error)
    },
    [streamId]
  )

  return { messages, viewers, connected, sendMessage, deleteMessage }
}
