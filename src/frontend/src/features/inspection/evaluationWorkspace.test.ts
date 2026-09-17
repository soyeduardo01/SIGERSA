import { describe, expect, it } from 'vitest'
import type { EvaluationFormItem, EvaluationInspectionPolicy } from '../../lib/api'
import {
  groupItemsByChapter,
  evidenceStatusBySlot,
  isValidEvaluationId,
  itemsForInspectionPolicy,
  qualificationForPercentage,
} from './evaluationWorkspace'

const item = (
  id: string,
  order: number,
  level: number,
  isEvaluable: boolean,
): EvaluationFormItem => ({
  id,
  parentId: null,
  sourceItem: order,
  code: id,
  title: id,
  isEvaluable,
  level,
  order,
})

describe('evaluation workspace helpers', () => {
  it('separa la ficha por el primer nivel que contiene varios capítulos', () => {
    const chapters = groupItemsByChapter([
      item('raíz', 1, 0, false),
      item('capítulo-1', 2, 1, false),
      item('pregunta-1', 3, 2, true),
      item('capítulo-2', 4, 1, false),
      item('pregunta-2', 5, 2, true),
    ])

    expect(chapters).toHaveLength(2)
    expect(chapters[0].items.map((value) => value.id)).toEqual(['raíz', 'capítulo-1', 'pregunta-1'])
    expect(chapters[1].items.map((value) => value.id)).toEqual(['capítulo-2', 'pregunta-2'])
  })

  it('aplica los cuatro criterios de calificación', () => {
    expect(qualificationForPercentage(60)).toBe('CONSIDERAR_CIERRE')
    expect(qualificationForPercentage(70)).toBe('URGE_CORREGIR')
    expect(qualificationForPercentage(80)).toBe('NECESARIO_CORREGIR')
    expect(qualificationForPercentage(81)).toBe('ALGUNAS_CORRECCIONES')
  })

  it('acepta el UUID estándar usado al abrir una evaluación desde la tabla', () => {
    expect(isValidEvaluationId('11111111-1111-4111-8111-111111111111')).toBe(true)
    expect(isValidEvaluationId('11111111-1111-4111-1111111111111')).toBe(false)
  })

  it('muestra únicamente los ítems aplicables y sus encabezados en un flujo focalizado', () => {
    const root = { ...item('raíz', 1, 0, false), parentId: null }
    const chapter = { ...item('capítulo', 2, 1, false), parentId: root.id }
    const excluded = { ...item('1.1.2', 3, 2, true), parentId: chapter.id }
    const required = { ...item('1.1.3', 4, 2, true), parentId: chapter.id }
    const policy = {
      mode: 'DESDE_1_1_3',
      requiredSourceItems: [required.sourceItem],
    } as EvaluationInspectionPolicy

    expect(itemsForInspectionPolicy([root, chapter, excluded, required], policy).map((value) => value.id)).toEqual([
      'raíz',
      'capítulo',
      '1.1.3',
    ])
  })

  it('restaura hasta tres indicadores de evidencia por ítem después de recargar', () => {
    const evidences = [
      { id: '1', sourceItem: 42, originalName: 'uno.png', uploadedAt: '2026-09-17T10:00:00Z' },
      { id: '2', sourceItem: 42, originalName: 'dos.pdf', uploadedAt: '2026-09-17T10:01:00Z' },
      { id: '3', sourceItem: 7, originalName: 'otro.png', uploadedAt: '2026-09-17T10:02:00Z' },
    ]

    expect(evidenceStatusBySlot(evidences)).toEqual({
      '42-0': 'Archivo guardado',
      '42-1': 'Archivo guardado',
      '7-0': 'Archivo guardado',
    })
  })

  it('tolera workspaces antiguos que no incluyen la colección de evidencias', () => {
    expect(evidenceStatusBySlot(undefined)).toEqual({})
    expect(evidenceStatusBySlot(null)).toEqual({})
  })
})
