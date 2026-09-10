import { moduleHref } from '../../lib/navigation'

export function AccessDenied() {
  return (
    <section
      className="rounded-card border border-amber-200 bg-white p-8 text-center shadow-card"
      aria-labelledby="access-denied-title"
    >
      <span
        className="mx-auto grid size-14 place-items-center rounded-full bg-amber-100 text-2xl"
        aria-hidden="true"
      >
        🔒
      </span>
      <h1 id="access-denied-title" className="mt-4 text-2xl font-extrabold text-ink-strong">
        Acceso restringido
      </h1>
      <p className="mx-auto mt-2 max-w-lg text-sm leading-6 text-ink-muted">
        Su rol actual no tiene permisos para consultar este módulo. Si considera que necesita
        acceso, contacte al administrador de SIGERSA.
      </p>
      <a
        href={moduleHref('resumen')}
        className="mt-5 inline-flex min-h-11 items-center rounded-xl bg-brand-700 px-5 font-bold text-white"
      >
        Volver al resumen
      </a>
    </section>
  )
}
