import { useEffect, useRef, useState, useCallback } from 'react'
import * as signalR from '@microsoft/signalr'
import { useAuthStore } from '../store/authStore'

export interface PlatformStats {
  totalUsers: number
  activeStreams: number
  totalViewers: number
  streamsToday: number
  newUsersToday: number
}

export function useAdminHub() {
  const { accessToken } = useAuthStore()
  const [stats, setStats] = useState<PlatformStats | null>(null)
  const [connected, setConnected] = useState(false)
  const hubRef = useRef<signalR.HubConnection | null>(null)

  const requestStats = useCallback(() => {
    hubRef.current?.invoke('GetLiveStats').catch(() => {})
  }, [])

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/admin', {
        accessTokenFactory: () => accessToken ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    connection.on('StatsUpdated', (data: PlatformStats) => {
      setStats(data)
    })

    connection
      .start()
      .then(() => {
        setConnected(true)
        // Request initial stats on connect
        connection.invoke('GetLiveStats').catch(() => {})
      })
      .catch(console.error)

    // Reconnect handlers
    connection.onreconnected(() => {
      setConnected(true)
      // Re-request stats after reconnect
      connection.invoke('GetLiveStats').catch(() => {})
    })

    connection.onclose(() => setConnected(false))
    connection.onreconnecting(() => setConnected(false))

    hubRef.current = connection

    return () => {
      connection.stop()
      setConnected(false)
    }
  }, [accessToken])

  return { stats, connected, requestStats }
}
