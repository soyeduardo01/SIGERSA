import { describe, expect, it } from 'vitest'
import { formatCedula, formatPhone, formatStatusLabel } from './formatters'

describe('formateadores de presentación', () => {
  it('presenta estados sin modificar su valor original', () => {
    const status = 'EN_ANALISIS'
    expect(formatStatusLabel(status)).toBe('EN ANALISIS')
    expect(status).toBe('EN_ANALISIS')
  })

  it('aplica las máscaras dominicanas y limita los dígitos', () => {
    expect(formatPhone('(809) 555 1234 extra')).toBe('809-555-1234')
    expect(formatCedula('0010000000199')).toBe('001-0000000-1')
  })
})
