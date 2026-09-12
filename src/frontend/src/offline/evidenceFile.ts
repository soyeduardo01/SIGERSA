export const MAX_EVIDENCE_SIZE_BYTES = 5 * 1024 * 1024

const allowedMimeTypes = new Set(['application/pdf', 'image/jpeg', 'image/png', 'video/mp4'])

export async function validateAndHashEvidence(file: Blob, mimeType: string) {
  if (file.size === 0) {
    throw new Error('La evidencia está vacía.')
  }

  if (file.size > MAX_EVIDENCE_SIZE_BYTES) {
    throw new Error('La evidencia excede el límite de 5 MB.')
  }

  if (!allowedMimeTypes.has(mimeType)) {
    throw new Error('El tipo de archivo no está permitido para evidencias.')
  }

  const signature = new Uint8Array(await file.slice(0, 12).arrayBuffer())
  if (!hasExpectedSignature(mimeType, signature)) {
    throw new Error('La firma del archivo no coincide con su tipo MIME.')
  }

  const digest = await crypto.subtle.digest('SHA-256', await file.arrayBuffer())
  return Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, '0')).join('')
}

function hasExpectedSignature(mimeType: string, bytes: Uint8Array) {
  switch (mimeType) {
    case 'application/pdf':
      return startsWith(bytes, [0x25, 0x50, 0x44, 0x46])
    case 'image/jpeg':
      return startsWith(bytes, [0xff, 0xd8, 0xff])
    case 'image/png':
      return startsWith(bytes, [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])
    case 'video/mp4':
      return (
        bytes.length >= 8 &&
        bytes[4] === 0x66 &&
        bytes[5] === 0x74 &&
        bytes[6] === 0x79 &&
        bytes[7] === 0x70
      )
    default:
      return false
  }
}

function startsWith(actual: Uint8Array, expected: number[]) {
  return expected.every((byte, index) => actual[index] === byte)
}
