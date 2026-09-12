import { describe, expect, it } from 'vitest'
import { canonicalRoles, canAccessModule, moduleRoles, type AppModule } from './rbac'

const expectedByRole: Record<(typeof canonicalRoles)[number], AppModule[]> = {
  ADMINISTRADOR: [
    'resumen',
    'perfil',
    'notificaciones',
    'empresas',
    'usuarios',
    'parametros',
    'auditoria',
    'fichas-bpm',
    'reportes',
    'establecimientos',
  ],
  ADMINISTRADOR_EMPRESA: [
    'resumen',
    'perfil',
    'notificaciones',
    'usuarios',
    'reportes',
    'solicitudes',
    'hallazgos',
    'evidencias',
    'correcciones',
  ],
  USUARIO_DELEGADO: [
    'resumen',
    'perfil',
    'notificaciones',
    'reportes',
    'solicitudes',
    'hallazgos',
    'evidencias',
    'correcciones',
  ],
  COORDINADOR: [
    'resumen',
    'perfil',
    'notificaciones',
    'reportes',
    'solicitudes',
    'alertas-denuncias',
    'casos',
    'programacion',
    'evaluaciones',
    'hallazgos',
    'evidencias',
    'correcciones',
  ],
  TECNICO_EVALUADOR: [
    'resumen',
    'perfil',
    'notificaciones',
    'reportes',
    'programacion',
    'evaluaciones',
    'hallazgos',
    'evidencias',
    'correcciones',
  ],
}

describe('matriz RBAC de módulos', () => {
  it.each(canonicalRoles)('concede exactamente los módulos de %s', (role) => {
    const accessible = (Object.keys(moduleRoles) as AppModule[]).filter((module) =>
      canAccessModule([role], module),
    )

    expect(new Set(accessible)).toEqual(new Set(expectedByRole[role]))
  })
})
