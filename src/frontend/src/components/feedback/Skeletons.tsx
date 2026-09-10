function Bone({ className }: { className: string }) {
  return (
    <span
      aria-hidden="true"
      className={`block animate-pulse rounded-lg bg-slate-200 ${className}`}
    />
  )
}

export function TableSkeleton({ rows = 5, columns = 5 }: { rows?: number; columns?: number }) {
  return (
    <div role="status" aria-label="Cargando tabla" className="space-y-3 p-3">
      {Array.from({ length: rows }, (_, row) => (
        <div
          key={row}
          className="grid gap-3"
          style={{ gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))` }}
        >
          {Array.from({ length: columns }, (_, column) => (
            <Bone key={column} className={`h-10 ${column === 0 ? 'w-full' : 'w-4/5'}`} />
          ))}
        </div>
      ))}
      <span className="sr-only">Cargando contenido…</span>
    </div>
  )
}

export function CardGridSkeleton({ cards = 4 }: { cards?: number }) {
  return (
    <div
      role="status"
      aria-label="Cargando tarjetas"
      className="grid gap-4 md:grid-cols-2 xl:grid-cols-4"
    >
      {Array.from({ length: cards }, (_, index) => (
        <div key={index} className="rounded-card bg-white p-5 shadow-card">
          <Bone className="h-4 w-2/3" />
          <Bone className="mt-4 h-3 w-full" />
          <Bone className="mt-2 h-3 w-4/5" />
          <Bone className="mt-5 h-6 w-24 rounded-full" />
        </div>
      ))}
      <span className="sr-only">Cargando contenido…</span>
    </div>
  )
}

export function FormSkeleton() {
  return (
    <div
      role="status"
      aria-label="Cargando formulario"
      className="grid animate-pulse gap-4 md:grid-cols-2"
    >
      <Bone className="h-16 w-full" />
      <Bone className="h-16 w-full" />
      <Bone className="h-24 w-full md:col-span-2" />
      <span className="sr-only">Cargando formulario…</span>
    </div>
  )
}
