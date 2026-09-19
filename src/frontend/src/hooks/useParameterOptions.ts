import { useEffect, useState } from 'react'
import { getParameters, type ParameterControl } from '../lib/api'
import { readCachedParameters, replaceCachedParameterCatalog } from '../offline/parameterCache'

export function useParameterOptions(keyWord: string) {
  const [options, setOptions] = useState<ParameterControl[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    const load = async () => {
      setLoading(true)
      let hadCachedData = false
      try {
        const cached = await readCachedParameters(keyWord)
        if (!active) return
        hadCachedData = cached.length > 0
        setOptions(cached)
        setError('')
      } catch {
        // IndexedDB can be unavailable (private mode/storage policy). Online
        // loading must still work in that case.
      }

      if (!navigator.onLine) {
        if (active) setLoading(false)
        return
      }

      try {
        const values = await getParameters(keyWord)
        if (active) {
          setOptions(values)
          setError('')
        }
        await replaceCachedParameterCatalog(keyWord, undefined, values).catch(() => undefined)
      } catch (caught) {
        if (!active) return
        if (!hadCachedData) {
          setError(caught instanceof Error ? caught.message : 'No se pudo cargar el catálogo.')
        }
      } finally {
        if (active) setLoading(false)
      }
    }

    queueMicrotask(() => active && void load())
    const handleOnline = () => void load()
    window.addEventListener('online', handleOnline)

    return () => {
      active = false
      window.removeEventListener('online', handleOnline)
    }
  }, [keyWord])

  return { options, loading, error }
}
