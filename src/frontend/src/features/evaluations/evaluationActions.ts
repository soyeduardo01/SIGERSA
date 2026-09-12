export function canEditInspection(status: string, canExecute: boolean) {
  return canExecute && (status === 'EN_EJECUCION' || status === 'EN_CORRECCION')
}

export function inspectionActionLabel(status: string, canExecute: boolean) {
  return canEditInspection(status, canExecute) ? 'Realizar inspección' : 'Ver ficha'
}
