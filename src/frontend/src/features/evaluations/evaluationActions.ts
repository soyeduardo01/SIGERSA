export function canEditInspection(status: string, canExecute: boolean) {
  return canExecute && status === 'EN_EJECUCION'
}

export function inspectionActionLabel(status: string, canExecute: boolean) {
  return canEditInspection(status, canExecute) ? 'Realizar inspección' : 'Ver ficha'
}
