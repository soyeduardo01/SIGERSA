export function canExecuteInspection(roles: string[]) {
  return roles.includes('TECNICO_EVALUADOR')
}

export function canEditInspection(status: string, canExecute: boolean, serverCanEdit = true) {
  return canExecute && serverCanEdit && status === 'EN_EJECUCION'
}

export function inspectionActionLabel(status: string, canExecute: boolean, serverCanEdit = true) {
  return canEditInspection(status, canExecute, serverCanEdit) ? 'Realizar inspección' : 'Ver ficha'
}

export function canSubmitForReview(status: string, roles: string[]) {
  return status === 'FINALIZADA' && roles.includes('TECNICO_EVALUADOR')
}

export function canFinalizeReview(status: string, roles: string[]) {
  return ['ENVIADA', 'EN_REVISION'].includes(status) && roles.includes('COORDINADOR')
}

export function canCloseEvaluation(status: string, roles: string[]) {
  return ['APROBADA', 'NO_APROBADA'].includes(status) && roles.includes('COORDINADOR')
}
