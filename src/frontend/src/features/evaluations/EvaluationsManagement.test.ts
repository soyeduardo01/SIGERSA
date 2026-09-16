import { describe, expect, it } from 'vitest'
import {
  canEditInspection,
  canExecuteInspection,
  canFinalizeReview,
  canSubmitForReview,
  inspectionActionLabel,
} from './evaluationActions'

describe('flujo unificado de evaluaciones e inspecciones', () => {
  it('habilita la inspección durante la ejecución', () => {
    const status = 'EN_EJECUCION'
    expect(canEditInspection(status, true)).toBe(true)
    expect(inspectionActionLabel(status, true)).toBe('Realizar inspección')
  })

  it('habilita solo al técnico y mantiene a administrador y coordinador en consulta', () => {
    expect(canExecuteInspection(['ADMINISTRADOR'])).toBe(false)
    expect(canExecuteInspection(['TECNICO_EVALUADOR'])).toBe(true)
    expect(canExecuteInspection(['COORDINADOR'])).toBe(false)
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

  it('mantiene la ficha en lectura cuando el servidor no reconoce al técnico como asignado', () => {
    expect(canEditInspection('EN_EJECUCION', true, false)).toBe(false)
    expect(inspectionActionLabel('EN_EJECUCION', true, false)).toBe('Ver ficha')
  })

  it('solo permite al técnico enviar una evaluación finalizada a revisión', () => {
    expect(canSubmitForReview('FINALIZADA', ['TECNICO_EVALUADOR'])).toBe(true)
    expect(canSubmitForReview('FINALIZADA', ['ADMINISTRADOR'])).toBe(false)
    expect(canSubmitForReview('FINALIZADA', ['COORDINADOR'])).toBe(false)
  })

  it('solo permite al coordinador finalizar una evaluación enviada', () => {
    expect(canFinalizeReview('ENVIADA', ['COORDINADOR'])).toBe(true)
    expect(canFinalizeReview('ENVIADA', ['ADMINISTRADOR'])).toBe(false)
    expect(canFinalizeReview('ENVIADA', ['TECNICO_EVALUADOR'])).toBe(false)
  })
})
