import type { AppModule } from './rbac'

export const loginHref = '/'
export const recoveryHref = '/recuperar-clave.html'

export function moduleHref(module: AppModule) {
  return `/modulo.html?module=${encodeURIComponent(module)}`
}

export function readModuleFromLocation(): AppModule | null {
  const requested = new URLSearchParams(window.location.search).get('module')
  return requested && isAppModule(requested) ? requested : null
}

export function isAppModule(value: string): value is AppModule {
  return [
    'resumen',
    'empresas',
    'evaluaciones',
    'inspecciones',
    'programacion',
    'establecimientos',
    'parametros',
    'solicitudes',
    'alertas-denuncias',
    'casos',
    'fichas-bpm',
    'hallazgos',
    'evidencias',
    'correcciones',
    'reportes',
    'usuarios',
    'auditoria',
    'perfil',
    'notificaciones',
  ].includes(value)
}
