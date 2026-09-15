export function canExecuteInspection(roles: string[]) {
  return roles.some((role) => role === 'ADMINISTRADOR' || role === 'TECNICO_EVALUADOR')
}

export function canEditInspection(status: string, canExecute: boolean) {
  return canExecute && status === 'EN_EJECUCION'
}

export function inspectionActionLabel(status: string, canExecute: boolean) {
  return canEditInspection(status, canExecute) ? 'Realizar inspección' : 'Ver ficha'
}

export function canSubmitForReview(status: string, roles: string[]) {
  return status === 'FINALIZADA' && roles.includes('TECNICO_EVALUADOR')
}

export function canFinalizeReview(status: string, roles: string[]) {
  return ['ENVIADA', 'EN_REVISION'].includes(status) && roles.includes('COORDINADOR')
}
