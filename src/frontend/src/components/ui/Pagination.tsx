interface PaginationProps {
  page: number
  pageSize: number
  total: number
  disabled?: boolean
  label?: string
  onChange: (page: number) => void
}

export function Pagination({
  page,
  pageSize,
  total,
  disabled = false,
  label = 'resultados',
  onChange,
}: PaginationProps) {
  const pages = Math.max(1, Math.ceil(total / Math.max(1, pageSize)))
  if (total <= pageSize && page === 1) return null
  return (
    <nav
      aria-label={`Paginación de ${label}`}
      className="mt-5 flex flex-col gap-3 border-t border-slate-100 pt-4 text-sm sm:flex-row sm:items-center sm:justify-between"
    >
      <span className="text-ink-muted">
        {total} registros · Página {page} de {pages}
      </span>
      <div className="flex gap-2">
        <button
          type="button"
          disabled={disabled || page <= 1}
          onClick={() => onChange(page - 1)}
          className="min-h-10 rounded-lg border border-slate-200 px-3 font-bold disabled:cursor-not-allowed disabled:opacity-40"
        >
          Anterior
        </button>
        <button
          type="button"
          disabled={disabled || page >= pages}
          onClick={() => onChange(page + 1)}
          className="min-h-10 rounded-lg border border-slate-200 px-3 font-bold disabled:cursor-not-allowed disabled:opacity-40"
        >
          Siguiente
        </button>
      </div>
    </nav>
  )
}

export function useClientPagination<T>(items: T[], pageSize = 10) {
  const [page, setPage] = useState(1)
  const pages = Math.max(1, Math.ceil(items.length / pageSize))
  useEffect(() => setPage((current) => Math.min(current, pages)), [pages])
  const startIndex = (page - 1) * pageSize
  const visibleItems = useMemo(
    () => items.slice(startIndex, startIndex + pageSize),
    [items, pageSize, startIndex],
  )
  return { page, setPage, pageSize, startIndex, visibleItems }
}
import { useEffect, useMemo, useState } from 'react'
