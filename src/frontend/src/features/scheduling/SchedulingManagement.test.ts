import { describe, expect, it } from 'vitest'
import { scheduleTiming } from './SchedulingManagement'

const now = Date.parse('2026-09-16T12:00:00Z')

describe('agenda de programación', () => {
  it('advierte cuando el intervalo asignado ya venció', () => {
    expect(
      scheduleTiming(
        {
          startsAt: '2026-09-16T09:00:00Z',
          endsAt: '2026-09-16T10:00:00Z',
          status: 'PROGRAMADA',
        },
        now,
      ).label,
    ).toBe('Plazo vencido')
  })

  it('marca una inspección que debe atenderse dentro del intervalo actual', () => {
    expect(
      scheduleTiming(
        {
          startsAt: '2026-09-16T11:00:00Z',
          endsAt: '2026-09-16T13:00:00Z',
          status: 'REPROGRAMADA',
        },
        now,
      ).label,
    ).toBe('Atender ahora')
  })

  it('no muestra alertas de plazo para una programación completada', () => {
    expect(
      scheduleTiming(
        {
          startsAt: '2026-09-15T11:00:00Z',
          endsAt: '2026-09-15T13:00:00Z',
          status: 'COMPLETADA',
        },
        now,
      ).label,
    ).toBe('')
  })
})
