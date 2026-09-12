export function formatStatusLabel(value: string) {
  return value.replaceAll('_', ' ').trim()
}

export function digitsOnly(value: string, maximumLength: number) {
  return value.replace(/\D/g, '').slice(0, maximumLength)
}

export function formatPhone(value: string) {
  const digits = digitsOnly(value, 10)
  return [digits.slice(0, 3), digits.slice(3, 6), digits.slice(6, 10)].filter(Boolean).join('-')
}

export function formatProfilePhone(value: string) {
  const digits = digitsOnly(value, 10)
  if (digits.length === 0) return ''
  if (digits.length <= 3) return `(${digits}`
  if (digits.length <= 6) return `(${digits.slice(0, 3)}) ${digits.slice(3)}`
  return `(${digits.slice(0, 3)}) ${digits.slice(3, 6)}-${digits.slice(6)}`
}

export function formatCedula(value: string) {
  const digits = digitsOnly(value, 11)
  if (digits.length <= 3) return digits
  if (digits.length <= 10) return `${digits.slice(0, 3)}-${digits.slice(3)}`
  return `${digits.slice(0, 3)}-${digits.slice(3, 10)}-${digits.slice(10)}`
}

export function formatIdentification(value: string, type: string) {
  return type === 'CEDULA' ? formatCedula(value) : value
}
