import { describe, expect, it, vi } from 'vitest'
import { futureLocalDateTime, toUtcIsoFromLocalInput } from './dateTime'

describe('dateTime', () => {
  it('convierte el datetime-local al instante UTC sin comparar textos ni zonas', () => {
    const value = toUtcIsoFromLocalInput('2026-09-15T10:30')

    expect(new Date(value).getTime()).toBe(new Date(2026, 8, 15, 10, 30).getTime())
    expect(value).toMatch(/Z$/)
  })

  it('genera mínimos futuros en hora local', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 8, 15, 10, 0))

    expect(futureLocalDateTime(30)).toBe('2026-09-15T10:30')
    vi.useRealTimers()
  })
})
