import { describe, expect, it } from 'vitest'
import { MAX_EVIDENCE_SIZE_BYTES, validateAndHashEvidence } from './evidenceFile'

describe('validateAndHashEvidence', () => {
  it('acepta un PNG con firma válida y calcula SHA-256', async () => {
    const png = new Blob([new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00])], {
      type: 'image/png',
    })

    await expect(validateAndHashEvidence(png, 'image/png')).resolves.toMatch(/^[a-f0-9]{64}$/)
  })

  it('rechaza archivos cuya firma no coincide con el MIME declarado', async () => {
    const invalidPdf = new Blob([new Uint8Array([0x00, 0x01, 0x02])], {
      type: 'application/pdf',
    })

    await expect(validateAndHashEvidence(invalidPdf, 'application/pdf')).rejects.toThrow(
      'La firma del archivo no coincide',
    )
  })

  it('rechaza evidencias mayores de 5 MB', async () => {
    const oversized = new Blob([new Uint8Array(MAX_EVIDENCE_SIZE_BYTES + 1)], {
      type: 'image/png',
    })

    await expect(validateAndHashEvidence(oversized, 'image/png')).rejects.toThrow('límite de 5 MB')
  })
})
