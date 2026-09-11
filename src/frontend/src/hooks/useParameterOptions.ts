import { useEffect, useState } from 'react'
import { getParameters, type ParameterControl } from '../lib/api'

export function useParameterOptions(keyWord: string) {
  const [options, setOptions] = useState<ParameterControl[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    queueMicrotask(() => active && setLoading(true))
    void getParameters(keyWord)
      .then((values) => {
        if (!active) return
        setOptions(
          [...values].sort(
            (left, right) =>
              (left.numericData ?? Number.MAX_SAFE_INTEGER) -
              (right.numericData ?? Number.MAX_SAFE_INTEGER),
          ),
        )
        setError('')
      })
      .catch((caught) => {
        if (!active) return
        setOptions([])
        setError(caught instanceof Error ? caught.message : 'No se pudo cargar el catálogo.')
      })
      .finally(() => active && setLoading(false))

    return () => {
      active = false
    }
  }, [keyWord])

  return { options, loading, error }
}
