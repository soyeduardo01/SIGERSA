export const canonicalRoles = [
  'ADMINISTRADOR',
  'ADMINISTRADOR_EMPRESA',
  'USUARIO_DELEGADO',
  'COORDINADOR',
  'TECNICO_EVALUADOR',
] as const

export type CanonicalRole = (typeof canonicalRoles)[number]

export type AppModule =
  | 'resumen'
  | 'empresas'
  | 'evaluaciones'
  | 'programacion'
  | 'establecimientos'
  | 'parametros'
  | 'solicitudes'
  | 'alertas-denuncias'
  | 'casos'
  | 'fichas-bpm'
  | 'hallazgos'
  | 'evidencias'
  | 'correcciones'
  | 'reportes'
  | 'usuarios'
  | 'auditoria'
  | 'perfil'
  | 'notificaciones'

const allRoles: readonly CanonicalRole[] = canonicalRoles

// SRS V2, sección 10. La ruta concede acceso al módulo; cada API vuelve a
// comprobar rol, ámbito y propiedad del recurso antes de operar.
export const moduleRoles: Record<AppModule, readonly CanonicalRole[]> = {
  resumen: allRoles,
  perfil: allRoles,
  notificaciones: allRoles,
  empresas: ['ADMINISTRADOR'],
  usuarios: ['ADMINISTRADOR', 'ADMINISTRADOR_EMPRESA'],
  parametros: ['ADMINISTRADOR'],
  auditoria: ['ADMINISTRADOR'],
  'fichas-bpm': ['ADMINISTRADOR'],
  reportes: allRoles,
  solicitudes: ['ADMINISTRADOR_EMPRESA', 'USUARIO_DELEGADO', 'COORDINADOR'],
  'alertas-denuncias': ['COORDINADOR'],
  casos: ['COORDINADOR'],
  programacion: ['COORDINADOR', 'TECNICO_EVALUADOR'],
  evaluaciones: ['COORDINADOR', 'TECNICO_EVALUADOR'],
  establecimientos: ['ADMINISTRADOR'],
  hallazgos: ['ADMINISTRADOR_EMPRESA', 'USUARIO_DELEGADO', 'COORDINADOR', 'TECNICO_EVALUADOR'],
  evidencias: ['ADMINISTRADOR_EMPRESA', 'USUARIO_DELEGADO', 'COORDINADOR', 'TECNICO_EVALUADOR'],
  correcciones: ['ADMINISTRADOR_EMPRESA', 'USUARIO_DELEGADO', 'COORDINADOR', 'TECNICO_EVALUADOR'],
}

export const roleNames: Record<CanonicalRole, string> = {
  ADMINISTRADOR: 'Administrador',
  ADMINISTRADOR_EMPRESA: 'Administrador de empresa',
  USUARIO_DELEGADO: 'Usuario delegado',
  COORDINADOR: 'Coordinador',
  TECNICO_EVALUADOR: 'Técnico evaluador',
}

export function normalizeRoles(roles: readonly string[]): CanonicalRole[] {
  const supported = new Set<string>(canonicalRoles)
  return [...new Set(roles.map((role) => role.trim().toUpperCase()))].filter(
    (role): role is CanonicalRole => supported.has(role),
  )
}

export function hasAnyRole(
  roles: readonly CanonicalRole[],
  allowedRoles: readonly CanonicalRole[],
) {
  return roles.some((role) => allowedRoles.includes(role))
}

export function canAccessModule(roles: readonly CanonicalRole[], module: AppModule) {
  return hasAnyRole(roles, moduleRoles[module])
}
