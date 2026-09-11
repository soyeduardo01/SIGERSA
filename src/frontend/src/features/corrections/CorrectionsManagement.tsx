import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { TableSkeleton } from '../../components/feedback/Skeletons'
import { useAuth } from '../../contexts/useAuth'
import { useParameterOptions } from '../../hooks/useParameterOptions'
import {
  createCorrection,
  getCorrectionOptions,
  getCorrections,
  transitionCorrection,
  type Correction,
  type CorrectionDraft,
  type CorrectionOptions,
  type CorrectionsPage,
} from '../../lib/api'
import { alerts } from '../../lib/alerts'
import { formatStatusLabel } from '../../lib/formatters'
import { queueCorrection } from '../../offline/syncQueue'

const emptyPage: CorrectionsPage = { items: [], page: 1, pageSize: 50, total: 0 }

export function CorrectionsManagement() {
  const correctionStates = useParameterOptions('ESTADO_CORRECCION')
  const { roles } = useAuth()
  const reviewer = roles.some((role) => role === 'ADMINISTRADOR' || role === 'COORDINADOR')
  const canSubmit = roles.some((role) =>
    ['ADMINISTRADOR', 'ADMINISTRADOR_EMPRESA', 'USUARIO_DELEGADO', 'TECNICO_EVALUADOR'].includes(
      role,
    ),
  )
  const [result, setResult] = useState(emptyPage)
  const [options, setOptions] = useState<CorrectionOptions | null>(null)
  const [status, setStatus] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [open, setOpen] = useState(false)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const [page, choices] = await Promise.all([
        getCorrections({ status }),
        getCorrectionOptions(),
      ])
      setResult(page)
      setOptions(choices)
      setError('')
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'No se pudieron cargar las correcciones.')
    } finally {
      setLoading(false)
    }
  }, [status])
  useEffect(() => {
    queueMicrotask(() => void load())
  }, [load])

  async function save(draft: CorrectionDraft) {
    try {
      if (navigator.onLine) await createCorrection(draft)
      else await queueCorrection(draft)
      setOpen(false)
      if (navigator.onLine) await load()
      await alerts.success(
        navigator.onLine ? 'Corrección solicitada' : 'Corrección guardada sin conexión',
      )
    } catch (caught) {
      await alerts.error(caught, 'No se pudo solicitar la corrección')
      throw caught
    }
  }

  async function transition(item: Correction, action: 'submit' | 'accept' | 'reject') {
    const accepted = await alerts.confirm({
      title:
        action === 'submit'
          ? 'Enviar corrección'
          : action === 'accept'
            ? 'Aceptar corrección'
            : 'Rechazar corrección',
      text: 'La transición quedará registrada y no sobrescribirá cambios concurrentes.',
      confirmText: 'Continuar',
    })
    if (!accepted) return
    try {
      await transitionCorrection(item.id, action, item.rowVersion)
      await load()
      await alerts.success('Estado actualizado')
    } catch (caught) {
      await alerts.error(caught, 'No se pudo actualizar la corrección')
    }
  }

  return (
    <section aria-labelledby="corrections-title">
      <div className="flex flex-col justify-between gap-4 md:flex-row md:items-end">
        <div>
          <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">
            Seguimiento
          </p>
          <h1 id="corrections-title" className="mt-2 text-2xl font-extrabold">
            Correcciones
          </h1>
          <p className="mt-2 text-sm text-ink-muted">
            Solicite, atienda y revise correcciones dentro del ámbito autorizado.
          </p>
        </div>
        {options?.canCreate && (
          <button
            type="button"
            onClick={() => setOpen(true)}
            className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white"
          >
            + Solicitar corrección
          </button>
        )}
      </div>
      <div className="mt-6 rounded-card bg-white p-5 shadow-card">
        <label className="block max-w-xs text-sm font-bold">
          Estado
          <select
            value={status}
            onChange={(e) => setStatus(e.target.value)}
            className="mt-1.5 min-h-11 w-full rounded-xl border px-3 font-normal"
          >
            <option value="">Todos</option>
            {correctionStates.options.map((value) => (
              <option key={value.parametersId} value={value.stringData ?? ''}>
                {formatStatusLabel(value.stringData ?? '')}
              </option>
            ))}
          </select>
          {!correctionStates.loading && correctionStates.options.length === 0 && (
            <span className="mt-1 block text-xs font-normal text-amber-700">
              El catálogo ESTADO_CORRECCION no tiene valores activos.
            </span>
          )}
        </label>
        {error && (
          <div
            role="alert"
            className="mt-5 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800"
          >
            {error}
          </div>
        )}
        <div className="mt-5 overflow-x-auto">
          <table className="w-full min-w-[950px] text-left text-sm">
            <thead>
              <tr className="border-b text-xs text-ink-muted uppercase">
                <th className="px-3 py-3">Evaluación</th>
                <th className="px-3 py-3">Revisión</th>
                <th className="px-3 py-3">Responsable</th>
                <th className="px-3 py-3">Solicitud</th>
                <th className="px-3 py-3">Límite</th>
                <th className="px-3 py-3">Estado</th>
                <th className="px-3 py-3 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {result.items.map((item) => (
                <tr key={item.id} className="border-b border-slate-100">
                  <td className="px-3 py-4 font-bold">
                    {item.evaluationNumber}
                    <span className="block text-xs font-normal text-ink-muted">
                      {item.establishmentName}
                    </span>
                  </td>
                  <td className="px-3 py-4">#{item.revisionNumber}</td>
                  <td className="px-3 py-4">
                    {formatStatusLabel(item.responsibleType)}
                    <span className="block text-xs text-ink-muted">
                      {item.assignedToName ?? 'Empresa evaluada'}
                    </span>
                  </td>
                  <td className="max-w-xs px-3 py-4">{item.coordinatorObservation}</td>
                  <td className="px-3 py-4">{new Date(item.dueAt).toLocaleDateString()}</td>
                  <td className="px-3 py-4">{formatStatusLabel(item.status)}</td>
                  <td className="px-3 py-4 text-right">
                    <div className="flex justify-end gap-2">
                      {canSubmit && ['PENDIENTE', 'EN_PROCESO'].includes(item.status) && (
                        <button
                          type="button"
                          onClick={() => void transition(item, 'submit')}
                          className="rounded-lg border px-3 py-2 font-bold"
                        >
                          Enviar
                        </button>
                      )}
                      {reviewer && item.status === 'ENVIADA' && (
                        <>
                          <button
                            type="button"
                            onClick={() => void transition(item, 'accept')}
                            className="rounded-lg border border-green-300 px-3 py-2 font-bold text-green-700"
                          >
                            Aceptar
                          </button>
                          <button
                            type="button"
                            onClick={() => void transition(item, 'reject')}
                            className="rounded-lg border border-red-300 px-3 py-2 font-bold text-red-700"
                          >
                            Rechazar
                          </button>
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {loading && <TableSkeleton rows={5} columns={7} />}
          {!loading && !error && result.items.length === 0 && (
            <p className="p-10 text-center text-sm text-ink-muted">
              No hay correcciones disponibles.
            </p>
          )}
        </div>
      </div>
      {open && options && (
        <CorrectionForm options={options} onClose={() => setOpen(false)} onSave={save} />
      )}
    </section>
  )
}

function CorrectionForm({
  options,
  onClose,
  onSave,
}: {
  options: CorrectionOptions
  onClose: () => void
  onSave: (draft: CorrectionDraft) => Promise<void>
}) {
  const responsibleTypes = useParameterOptions('TIPO_RESPONSABLE_CORRECCION')
  const [evaluationId, setEvaluationId] = useState('')
  const [responsibleType, setResponsibleType] = useState<'' | 'TECNICO' | 'EMPRESA'>('')
  const [assignedToId, setAssignedToId] = useState('')
  const [observation, setObservation] = useState('')
  const [dueAt, setDueAt] = useState('')
  const [saving, setSaving] = useState(false)
  async function submit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    try {
      await onSave({
        evaluationId,
        responsibleType: responsibleType as 'TECNICO' | 'EMPRESA',
        assignedToId: responsibleType === 'TECNICO' ? assignedToId : null,
        coordinatorObservation: observation,
        dueAt: new Date(dueAt).toISOString(),
        idempotencyKey: crypto.randomUUID(),
      })
    } finally {
      setSaving(false)
    }
  }
  const field = 'mt-1.5 min-h-11 w-full rounded-xl border px-3 font-normal'
  return (
    <div
      className="sigersa-modal-overlay fixed inset-0 z-50 grid place-items-center p-4"
      role="presentation"
    >
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="correction-form-title"
        className="sigersa-modal-panel w-full max-w-xl p-6"
      >
        <div className="flex justify-between">
          <h2 id="correction-form-title" className="text-xl font-extrabold">
            Solicitar corrección
          </h2>
          <button type="button" onClick={onClose} aria-label="Cerrar">
            ✕
          </button>
        </div>
        <form onSubmit={(event) => void submit(event)} className="mt-6 grid gap-4">
          <label className="text-sm font-bold">
            Evaluación
            <select
              required
              value={evaluationId}
              onChange={(e) => setEvaluationId(e.target.value)}
              className={field}
            >
              <option value="">Seleccione</option>
              {options.evaluations.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.name}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold">
            Responsable
            <select
              value={responsibleType}
              onChange={(e) => {
                setResponsibleType(e.target.value as 'TECNICO' | 'EMPRESA')
                setAssignedToId('')
              }}
              className={field}
            >
              <option value="">Seleccione</option>
              {responsibleTypes.options.map((value) => (
                <option key={value.parametersId} value={value.stringData ?? ''}>
                  {formatStatusLabel(value.stringData ?? '')}
                </option>
              ))}
            </select>
            {!responsibleTypes.loading && responsibleTypes.options.length === 0 && (
              <span className="mt-1 block text-xs font-normal text-amber-700">
                El catálogo TIPO_RESPONSABLE_CORRECCION no tiene valores activos.
              </span>
            )}
          </label>
          {responsibleType === 'TECNICO' && (
            <label className="text-sm font-bold">
              Técnico
              <select
                required
                value={assignedToId}
                onChange={(e) => setAssignedToId(e.target.value)}
                className={field}
              >
                <option value="">Seleccione</option>
                {options.technicians.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name}
                  </option>
                ))}
              </select>
            </label>
          )}
          <label className="text-sm font-bold">
            Fecha límite
            <input
              required
              type="datetime-local"
              value={dueAt}
              onChange={(e) => setDueAt(e.target.value)}
              className={field}
            />
          </label>
          <label className="text-sm font-bold">
            Instrucciones
            <textarea
              required
              rows={4}
              value={observation}
              onChange={(e) => setObservation(e.target.value)}
              className={`${field} py-3`}
            />
          </label>
          <div className="flex justify-end gap-3 border-t pt-5">
            <button
              type="button"
              onClick={onClose}
              className="rounded-xl border px-4 py-2.5 font-bold"
            >
              Volver
            </button>
            <button
              disabled={saving || (responsibleType === 'TECNICO' && !assignedToId)}
              className="rounded-xl bg-brand-700 px-5 py-2.5 font-bold text-white disabled:opacity-50"
            >
              {saving ? 'Guardando…' : 'Solicitar'}
            </button>
          </div>
        </form>
      </section>
    </div>
  )
}
