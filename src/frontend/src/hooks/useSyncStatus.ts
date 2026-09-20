import { useEffect, useState } from 'react'
import { refreshOfflineMirror } from '../offline/evaluationBootstrap'
import { flushSyncQueue, getPendingMutationCount, subscribeToSyncQueue } from '../offline/syncQueue'

export function useSyncStatus() {
  const [isOnline, setIsOnline] = useState(() => navigator.onLine)
  const [pendingCount, setPendingCount] = useState(0)
  const [isSyncing, setIsSyncing] = useState(false)

  useEffect(() => {
    const refreshCount = () => {
      void getPendingMutationCount().then(setPendingCount)
    }

    const synchronize = async (refreshEvaluations = false) => {
      if (!navigator.onLine) return
      setIsOnline(true)
      const count = await getPendingMutationCount()
      let remaining = count
      setPendingCount(count)
      if (count > 0) {
        setIsSyncing(true)
        await flushSyncQueue({ retryFailed: refreshEvaluations }).finally(async () => {
          setIsSyncing(false)
          remaining = await getPendingMutationCount()
          setPendingCount(remaining)
        })
      }
      if (refreshEvaluations) await refreshOfflineMirror(remaining === 0).catch(() => undefined)
    }

    const handleOnline = () => void synchronize(true)

    const handleOffline = () => setIsOnline(false)

    refreshCount()
    void synchronize(true)
    const retryTimer = window.setInterval(() => void synchronize(), 30_000)
    window.addEventListener('online', handleOnline)
    window.addEventListener('offline', handleOffline)
    const unsubscribe = subscribeToSyncQueue(refreshCount)

    return () => {
      window.removeEventListener('online', handleOnline)
      window.removeEventListener('offline', handleOffline)
      window.clearInterval(retryTimer)
      unsubscribe()
    }
  }, [])

  return { isOnline, isSyncing, pendingCount }
}
