import { useEffect, useState } from 'react'
import { flushSyncQueue, getPendingMutationCount, subscribeToSyncQueue } from '../offline/syncQueue'

export function useSyncStatus() {
  const [isOnline, setIsOnline] = useState(() => navigator.onLine)
  const [pendingCount, setPendingCount] = useState(0)
  const [isSyncing, setIsSyncing] = useState(false)

  useEffect(() => {
    const refreshCount = () => {
      void getPendingMutationCount().then(setPendingCount)
    }

    const handleOnline = () => {
      setIsOnline(true)
      setIsSyncing(true)
      void flushSyncQueue().finally(() => {
        setIsSyncing(false)
        refreshCount()
      })
    }

    const handleOffline = () => setIsOnline(false)

    refreshCount()
    window.addEventListener('online', handleOnline)
    window.addEventListener('offline', handleOffline)
    const unsubscribe = subscribeToSyncQueue(refreshCount)

    return () => {
      window.removeEventListener('online', handleOnline)
      window.removeEventListener('offline', handleOffline)
      unsubscribe()
    }
  }, [])

  return { isOnline, isSyncing, pendingCount }
}
