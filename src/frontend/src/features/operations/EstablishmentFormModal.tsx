import { useEffect, useId, useState, type DragEvent, type FormEvent, type ReactNode } from 'react'
import { alerts } from '../../lib/alerts'

interface EstablishmentFormModalProps {
  open: boolean
  onClose: () => void
}

type FieldIconName =
  | 'building'
  | 'calendar'
  | 'code'
  | 'list'
  | 'mail'
  | 'note'
  | 'pin'
  | 'phone'
  | 'risk'
  | 'status'
  | 'store'
  | 'user'

const fieldIconPaths: Record<FieldIconName, string> = {
  building: 'M4 21V8l8-4 8 4v13M8 21v-5h8v5M8 10h.01M12 10h.01M16 10h.01',
  calendar: 'M6 2v3m12-3v3M4 9h16M5 5h14a2 2 0 0 1 2 2v13H3V7a2 2 0 0 1 2-2Z',
  code: 'M9 5h6m-7 3h8m-9 13h10a2 2 0 0 0 2-2V5a2 2 0 0 0-2-2H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2Z',
  list: 'M9 6h11M9 12h11M9 18h11M4 6h.01M4 12h.01M4 18h.01',
  mail: 'M3 6h18v12H3V6Zm0 1 9 6 9-6',
  note: 'M6 3h12v18H6V3Zm3 5h6M9 12h6M9 16h4',
  pin: 'M12 21s6-5.2 6-12a6 6 0 1 0-12 0c0 6.8 6 12 6 12Zm0-9a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z',
  phone:
    'M7 3H4a1 1 0 0 0-1 1c0 9.4 7.6 17 17 17a1 1 0 0 0 1-1v-3l-4-1-2 2a14 14 0 0 1-9-9l2-2-1-4Z',
  risk: 'M5 20v-6h3v6H5Zm6 0V9h3v11h-3Zm6 0V4h3v16h-3Z',
  status: 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Zm-4-9 2.5 2.5L16 9',
  store: 'M4 10h16M5 10v10h14V10M3 10l2-6h14l2 6M9 14h6v6',
  user: 'M20 21a8 8 0 0 0-16 0m8-10a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z',
}

function FieldIcon({ name }: { name: FieldIconName }) {
  return (
    <svg
      viewBox="0 0 24 24"
      className="size-[18px]"
      fill="none"
      stroke="currentColor"
      aria-hidden="true"
    >
      <path
        d={fieldIconPaths[name]}
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.8"
      />
    </svg>
  )
}

function Icon({ children }: { children: ReactNode }) {
  return (
    <span className="grid size-11 shrink-0 place-items-center rounded-l-xl bg-brand-50 text-brand-700">
      {children}
    </span>
  )
}

function InputFrame({ children, icon }: { children: ReactNode; icon: ReactNode }) {
  return (
    <span className="flex min-h-11 overflow-hidden rounded-xl border border-slate-200 bg-white transition focus-within:border-brand-500 focus-within:ring-2 focus-within:ring-brand-100">
      <Icon>{icon}</Icon>
      {children}
    </span>
  )
}

function Label({ children, required = false }: { children: ReactNode; required?: boolean }) {
  return (
    <span className="mb-1.5 block text-xs font-bold text-ink-strong">
      {children} {required && <span className="text-red-600">*</span>}
    </span>
  )
}

const fieldClass =
  'min-w-0 flex-1 bg-transparent px-3 text-sm text-ink-strong outline-none placeholder:text-slate-400'

function SelectControl({
  children,
  defaultValue,
  label,
  required = false,
}: {
  children: ReactNode
  defaultValue?: string
  label: string
  required?: boolean
}) {
  return (
    <span className="relative flex min-w-0 flex-1">
      <select
        aria-label={label}
        required={required}
        defaultValue={defaultValue}
        className={`${fieldClass} h-11 appearance-none border-0 pr-10 focus:ring-0`}
      >
        {children}
      </select>
      <svg
        viewBox="0 0 24 24"
        className="pointer-events-none absolute top-1/2 right-3 size-4 -translate-y-1/2 text-slate-400"
        fill="none"
        stroke="currentColor"
        aria-hidden="true"
      >
        <path d="m7 10 5 5 5-5" strokeLinecap="round" strokeLinejoin="round" strokeWidth="1.8" />
      </svg>
    </span>
  )
}

export function EstablishmentFormModal({ open, onClose }: EstablishmentFormModalProps) {
  const titleId = useId()
  const [tab, setTab] = useState<'general' | 'documents'>('general')
  const [observations, setObservations] = useState('')
  const [active, setActive] = useState(true)
  const [fileCount, setFileCount] = useState(0)
  const [dragging, setDragging] = useState(false)

  useEffect(() => {
    if (!open) return
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    document.addEventListener('keydown', closeOnEscape)
    return () => {
      document.body.style.overflow = previousOverflow
      document.removeEventListener('keydown', closeOnEscape)
    }
  }, [onClose, open])

  if (!open) return null

  function acceptDrop(event: DragEvent<HTMLLabelElement>) {
    event.preventDefault()
    setDragging(false)
    setFileCount(event.dataTransfer.files.length)
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await alerts.success('Registro preparado', 'El formulario base validó los datos correctamente.')
    onClose()
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/55 p-3 backdrop-blur-sm sm:p-6">
      <button
        type="button"
        className="absolute inset-0 cursor-default"
        aria-label="Cerrar formulario al pulsar fuera"
        onClick={onClose}
      />
      <div
        className="relative z-10 flex max-h-[94dvh] w-full max-w-5xl flex-col overflow-hidden rounded-3xl bg-white shadow-2xl shadow-slate-950/30"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
      >
        <header className="flex shrink-0 items-start justify-between border-b border-slate-100 px-5 py-4 sm:px-7">
          <div className="flex gap-4">
            <span className="grid size-12 shrink-0 place-items-center rounded-2xl bg-brand-50 text-brand-700">
              <svg viewBox="0 0 24 24" className="size-6" fill="none" stroke="currentColor">
                <path
                  strokeWidth="1.8"
                  d="M4 21V8l8-4 8 4v13M8 21v-5h8v5M8 10h.01M12 10h.01M16 10h.01"
                />
              </svg>
            </span>
            <div>
              <h2 id={titleId} className="text-xl font-extrabold tracking-tight text-ink-strong">
                Registrar establecimiento
              </h2>
              <p className="mt-1 text-sm text-ink-muted">
                Complete la información para incorporarlo al sistema.
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="grid size-10 place-items-center rounded-xl text-xl text-slate-400 transition hover:bg-slate-100 hover:text-slate-700"
            aria-label="Cerrar modal"
          >
            ×
          </button>
        </header>

        <div className="flex shrink-0 gap-1 border-b border-slate-100 px-5 sm:px-7" role="tablist">
          {[
            ['general', 'Datos generales'],
            ['documents', 'Documentación y estado'],
          ].map(([value, label]) => {
            const selected = tab === value
            return (
              <button
                key={value}
                type="button"
                role="tab"
                aria-selected={selected}
                onClick={() => setTab(value as typeof tab)}
                className={`border-b-2 px-4 py-3 text-xs font-bold transition ${selected ? 'border-brand-600 text-brand-700' : 'border-transparent text-slate-400 hover:text-slate-700'}`}
              >
                {label}
              </button>
            )
          })}
        </div>

        <form
          id="establishment-form"
          onSubmit={(event) => void submit(event)}
          className="min-h-0 flex-1 overflow-y-auto"
        >
          {tab === 'general' ? (
            <section className="grid gap-4 p-5 sm:grid-cols-2 sm:p-7 lg:grid-cols-6">
              <label className="block lg:col-span-3">
                <Label required>Código del establecimiento</Label>
                <InputFrame icon={<FieldIcon name="code" />}>
                  <input className={fieldClass} placeholder="EST-5621" />
                </InputFrame>
                <small className="mt-1 block text-[0.65rem] text-ink-muted">
                  Se generará automáticamente si se deja en blanco.
                </small>
              </label>
              <label className="block lg:col-span-3">
                <Label required>Nombre comercial</Label>
                <InputFrame icon={<FieldIcon name="store" />}>
                  <input
                    required
                    className={fieldClass}
                    placeholder="Ej.: Restaurant El Buen Sabor"
                  />
                </InputFrame>
              </label>

              <div className="block lg:col-span-2">
                <Label required>Tipo de establecimiento</Label>
                <InputFrame icon={<FieldIcon name="list" />}>
                  <SelectControl required defaultValue="" label="Tipo de establecimiento">
                    <option value="" disabled>
                      Seleccione un tipo
                    </option>
                    <option>Restaurante</option>
                    <option>Supermercado</option>
                    <option>Industria alimentaria</option>
                  </SelectControl>
                </InputFrame>
              </div>
              <div className="block lg:col-span-2">
                <Label required>Provincia</Label>
                <InputFrame icon={<FieldIcon name="pin" />}>
                  <SelectControl required defaultValue="" label="Provincia">
                    <option value="" disabled>
                      Seleccione una provincia
                    </option>
                    <option>Distrito Nacional</option>
                    <option>Santo Domingo</option>
                    <option>Santiago</option>
                  </SelectControl>
                </InputFrame>
              </div>
              <div className="block lg:col-span-2">
                <Label required>Municipio</Label>
                <InputFrame icon={<FieldIcon name="building" />}>
                  <SelectControl required defaultValue="" label="Municipio">
                    <option value="" disabled>
                      Seleccione un municipio
                    </option>
                    <option>Santo Domingo Este</option>
                    <option>Santo Domingo Norte</option>
                  </SelectControl>
                </InputFrame>
              </div>

              <label className="block lg:col-span-6">
                <Label required>Dirección</Label>
                <InputFrame icon={<FieldIcon name="pin" />}>
                  <input
                    required
                    className={fieldClass}
                    placeholder="Calle, avenida, número, referencia..."
                  />
                </InputFrame>
              </label>

              <label className="block lg:col-span-2">
                <Label required>Responsable</Label>
                <InputFrame icon={<FieldIcon name="user" />}>
                  <input required className={fieldClass} placeholder="Nombre del responsable" />
                </InputFrame>
              </label>
              <label className="block lg:col-span-2">
                <Label required>Teléfono</Label>
                <InputFrame icon={<FieldIcon name="phone" />}>
                  <input
                    required
                    type="tel"
                    className={fieldClass}
                    placeholder="Ej. 809 555 0100"
                  />
                </InputFrame>
              </label>
              <label className="block lg:col-span-2">
                <Label required>Correo electrónico</Label>
                <InputFrame icon={<FieldIcon name="mail" />}>
                  <input
                    required
                    type="email"
                    className={fieldClass}
                    placeholder="nombre@empresa.com"
                  />
                </InputFrame>
              </label>

              <div className="block lg:col-span-2">
                <Label required>Nivel de riesgo</Label>
                <InputFrame icon={<FieldIcon name="risk" />}>
                  <SelectControl required defaultValue="" label="Nivel de riesgo">
                    <option value="" disabled>
                      Seleccione un nivel
                    </option>
                    <option>Bajo</option>
                    <option>Medio</option>
                    <option>Alto</option>
                  </SelectControl>
                </InputFrame>
              </div>
              <div className="block lg:col-span-2">
                <Label required>Estado</Label>
                <InputFrame icon={<FieldIcon name="status" />}>
                  <SelectControl required label="Estado">
                    <option>Activo</option>
                    <option>En revisión</option>
                    <option>Suspendido</option>
                  </SelectControl>
                </InputFrame>
              </div>
              <label className="block lg:col-span-2">
                <Label required>Fecha de registro</Label>
                <InputFrame icon={<FieldIcon name="calendar" />}>
                  <input required type="date" className={fieldClass} />
                </InputFrame>
              </label>

              <label className="block lg:col-span-6">
                <Label>Observaciones</Label>
                <span className="relative flex overflow-hidden rounded-xl border border-slate-200 bg-white focus-within:border-brand-500 focus-within:ring-2 focus-within:ring-brand-100">
                  <Icon>
                    <FieldIcon name="note" />
                  </Icon>
                  <textarea
                    value={observations}
                    onChange={(event) => setObservations(event.target.value)}
                    maxLength={500}
                    rows={3}
                    className="min-w-0 flex-1 resize-none bg-transparent p-3 pr-16 text-sm outline-none"
                    placeholder="Información adicional sobre el establecimiento..."
                  />
                  <small className="absolute right-3 bottom-2 text-[0.65rem] text-ink-muted">
                    {observations.length}/500
                  </small>
                </span>
              </label>
            </section>
          ) : (
            <section className="grid gap-6 p-5 sm:p-7 lg:grid-cols-5">
              <div className="lg:col-span-3">
                <Label>Documentos del establecimiento</Label>
                <label
                  className={`mt-2 flex min-h-44 cursor-pointer flex-col items-center justify-center rounded-2xl border-2 border-dashed p-6 text-center transition ${dragging ? 'border-brand-500 bg-brand-50' : 'hover:border-brand-400 border-slate-200 bg-slate-50 hover:bg-brand-50/60'}`}
                  onDragEnter={(event) => {
                    event.preventDefault()
                    setDragging(true)
                  }}
                  onDragOver={(event) => event.preventDefault()}
                  onDragLeave={() => setDragging(false)}
                  onDrop={acceptDrop}
                >
                  <input
                    type="file"
                    multiple
                    accept=".pdf,.jpg,.jpeg,.png,.doc,.docx"
                    className="hidden"
                    onChange={(event) => setFileCount(event.target.files?.length ?? 0)}
                  />
                  <svg
                    viewBox="0 0 24 24"
                    className="size-10 text-brand-600"
                    fill="none"
                    stroke="currentColor"
                  >
                    <path
                      strokeWidth="1.6"
                      d="M7 18a5 5 0 0 1 1-9.9A6 6 0 0 1 19.7 10 4 4 0 0 1 19 18H7Zm5-8v7m-3-4 3-3 3 3"
                    />
                  </svg>
                  <strong className="mt-3 text-sm text-ink-strong">
                    Arrastre y suelte archivos aquí
                  </strong>
                  <span className="mt-1 text-xs text-brand-700">o haga clic para seleccionar</span>
                  <small className="mt-3 text-[0.65rem] text-ink-muted">
                    {fileCount > 0
                      ? `${fileCount} archivo(s) seleccionado(s)`
                      : 'PDF, JPG, PNG, DOC, DOCX · Máx. 10 MB'}
                  </small>
                </label>
              </div>
              <div className="lg:col-span-2 lg:border-l lg:border-slate-100 lg:pl-6">
                <div className="flex items-center justify-between gap-4">
                  <div>
                    <p className="text-xs font-bold text-ink-strong">Activo en el sistema</p>
                    <p className="mt-1 text-xs leading-5 text-ink-muted">
                      Disponible para evaluaciones e inspecciones.
                    </p>
                  </div>
                  <button
                    type="button"
                    role="switch"
                    aria-checked={active}
                    onClick={() => setActive((value) => !value)}
                    className={`relative h-7 w-12 shrink-0 rounded-full transition ${active ? 'bg-brand-600' : 'bg-slate-300'}`}
                  >
                    <span
                      className={`absolute top-1 size-5 rounded-full bg-white shadow transition-all ${active ? 'right-1' : 'left-1'}`}
                    />
                  </button>
                </div>
                <div className="mt-6 flex gap-3 rounded-2xl bg-gradient-to-br from-emerald-50 to-green-100/60 p-4 text-brand-900">
                  <span className="text-2xl">❧</span>
                  <p className="text-xs leading-5">
                    Un registro preciso ayuda a construir un entorno más seguro para una mejor
                    salud.
                  </p>
                </div>
              </div>
            </section>
          )}
        </form>

        <footer className="flex shrink-0 flex-col-reverse gap-2 border-t border-slate-100 px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-7">
          <button
            type="button"
            className="rounded-xl border border-slate-200 px-4 py-2.5 text-xs font-bold text-ink-body shadow-sm hover:bg-slate-50"
          >
            Guardar borrador
          </button>
          <div className="flex gap-2">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 rounded-xl px-4 py-2.5 text-xs font-bold text-ink-body hover:bg-slate-100 sm:flex-none"
            >
              Cancelar
            </button>
            <button
              form="establishment-form"
              type="submit"
              className="hover:bg-brand-800 flex-1 rounded-xl bg-brand-700 px-5 py-2.5 text-xs font-bold text-white shadow-lg shadow-emerald-900/15 sm:flex-none"
            >
              Guardar registro
            </button>
          </div>
        </footer>
      </div>
    </div>
  )
}
