export const correctionMinimumLeadMinutes = 30

export function toUtcIsoFromLocalInput(value: string) {
  const date = new Date(value)
  if (!value || Number.isNaN(date.getTime())) {
    throw new Error('Seleccione una fecha y hora válidas.')
  }
  return date.toISOString()
}

export function toLocalDateTimeInput(value: string | number | Date) {
  const date = new Date(value)
  const offset = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offset).toISOString().slice(0, 16)
}

export function futureLocalDateTime(minutesFromNow: number) {
  return toLocalDateTimeInput(Date.now() + minutesFromNow * 60_000)
}
