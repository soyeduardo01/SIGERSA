import { describe, expect, it } from 'vitest'
import { canEditInspection, inspectionActionLabel } from './evaluationActions'

describe('flujo unificado de evaluaciones e inspecciones', () => {
  it('habilita la inspección durante la ejecución', () => {
    const status = 'EN_EJECUCION'
    expect(canEditInspection(status, true)).toBe(true)
    expect(inspectionActionLabel(status, true)).toBe('Realizar inspección')
  })

  it.each([
    'ASIGNADA',
    'EN_CORRECCION',
    'FINALIZADA',
    'ENVIADA',
    'EN_REVISION',
    'APROBADA',
    'CERRADA',
  ])('deja la ficha en lectura para el estado %s', (status) => {
    expect(canEditInspection(status, true)).toBe(false)
    expect(inspectionActionLabel(status, true)).toBe('Ver ficha')
  })

  it('mantiene la ficha en lectura sin permiso de ejecución', () => {
    expect(canEditInspection('EN_EJECUCION', false)).toBe(false)
    expect(inspectionActionLabel('EN_EJECUCION', false)).toBe('Ver ficha')
  })
})
