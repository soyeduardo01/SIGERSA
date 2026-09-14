import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { TableSkeleton } from '../../components/feedback/Skeletons'
import { useAuth } from '../../contexts/useAuth'
import { useParameterOptions } from '../../hooks/useParameterOptions'
import {
  downloadEvidence,
  getEvidences,
  getEvaluations,
  type EvidenceSummary,
  type EvidencesPage,
  type EvaluationSummary,
} from '../../lib/api'
import { alerts } from '../../lib/alerts'
import { getOptionalLocation } from '../../lib/geolocation'
import { formatStatusLabel } from '../../lib/formatters'
import { offlineDb } from '../../offline/database'
import { flushSyncQueue, queueEvidence } from '../../offline/syncQueue'

const emptyPage: EvidencesPage = { items: [], page: 1, pageSize: 20, total: 0 }
type EvidencePreviewState = {
  item: EvidenceSummary
  url?: string
  loading: boolean
  error?: string
}

export function EvidenceManagement() {
  const { roles } = useAuth()
  const canUpload = roles.some((role) => role === 'ADMINISTRADOR' || role === 'TECNICO_EVALUADOR')
  const [result, setResult] = useState(emptyPage)
  const [evaluations, setEvaluations] = useState<EvaluationSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [open, setOpen] = useState(false)
  const [preview, setPreview] = useState<EvidencePreviewState | null>(null)
  const previewRequest = useRef(0)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const [page, evaluationPage] = await Promise.all([
        getEvidences({}),
        getEvaluations({ pageSize: 100 }),
      ])
      setResult(page)
      setEvaluations(evaluationPage.items)
      setError('')
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'No se pudieron cargar las evidencias.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    queueMicrotask(() => void load())
  }, [load])

  async function download(item: EvidenceSummary) {
    try {
      const blob = await downloadEvidence(item.id)
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = item.originalName
      anchor.click()
      URL.revokeObjectURL(url)
    } catch (caught) {
      await alerts.error(caught, 'No se pudo descargar la evidencia')
    }
  }

  async function showPreview(item: EvidenceSummary) {
    const requestId = ++previewRequest.current
    setPreview({ item, loading: true })
    try {
      const blob = await downloadEvidence(item.id)
      const url = URL.createObjectURL(blob)
      if (requestId !== previewRequest.current) {
        URL.revokeObjectURL(url)
        return
      }
      setPreview({ item, url, loading: false })
    } catch (caught) {
      if (requestId !== previewRequest.current) return
      setPreview({
        item,
        loading: false,
        error: caught instanceof Error ? caught.message : 'No se pudo cargar la vista previa.',
      })
    }
  }

  function closePreview() {
    previewRequest.current += 1
    setPreview((current) => {
      if (current?.url) URL.revokeObjectURL(current.url)
      return null
    })
  }

  useEffect(
    () => () => {
      previewRequest.current += 1
      if (preview?.url) URL.revokeObjectURL(preview.url)
    },
    [preview?.url],
  )

  async function save(evaluationId: string, evidenceType: string, file: File) {
    try {
      const location = await getOptionalLocation()
      const queueId = await queueEvidence({
        evaluationId,
        evidenceType,
        fileName: file.name,
        mimeType: file.type,
        file,
        ...location,
      })
      if (navigator.onLine) await flushSyncQueue()
      const pending = await offlineDb.syncQueue.get(queueId)
      setOpen(false)
      await load()
      await alerts.success(
        pending ? 'Evidencia guardada en cola' : 'Evidencia sincronizada',
        pending
          ? 'El archivo permanece protegido en el dispositivo y se reintentará desde Notificaciones y sincronización.'
          : 'El archivo fue verificado y registrado en Storage privado.',
      )
    } catch (caught) {
      await alerts.error(caught, 'No se pudo guardar la evidencia')
      throw caught
    }
  }

  return (
    <section aria-labelledby="evidence-title">
      <div className="flex flex-col justify-between gap-4 md:flex-row md:items-end">
        <div>
          <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">
            Soporte documental
          </p>
          <h1 id="evidence-title" className="mt-2 text-2xl font-extrabold">
            Evidencias
          </h1>
          <p className="mt-2 text-sm text-ink-muted">
            Los binarios se conservan en Supabase Storage privado; aquí se muestran metadatos
            autorizados.
          </p>
        </div>
        {canUpload && (
          <button
            type="button"
            onClick={() => setOpen(true)}
            className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white"
          >
            + Adjuntar evidencia
          </button>
        )}
      </div>
      <div className="mt-6 overflow-hidden rounded-card bg-white p-5 shadow-card">
        {error && (
          <div
            role="alert"
            className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800"
          >
            {error}
          </div>
        )}
        <div className="overflow-x-auto">
          <table className="w-full min-w-[850px] text-left text-sm">
            <thead>
              <tr className="border-b text-xs text-ink-muted uppercase">
                <th className="px-3 py-3">Archivo</th>
                <th className="px-3 py-3">Evaluación</th>
                <th className="px-3 py-3">Tipo</th>
                <th className="px-3 py-3">Autor</th>
                <th className="px-3 py-3">Estado</th>
                <th className="px-3 py-3 text-right">Acción</th>
              </tr>
            </thead>
            <tbody>
              {result.items.map((item) => (
                <tr key={item.id} className="border-b border-slate-100">
                  <td className="px-3 py-4 font-bold">
                    {item.originalName}
                    <span className="block text-xs font-normal text-ink-muted">
                      {(item.fileSize / 1024).toFixed(1)} KB · {item.mimeType}
                    </span>
                  </td>
                  <td className="px-3 py-4">
                    {item.evaluationNumber}
                    <span className="block text-xs text-ink-muted">{item.establishmentName}</span>
                  </td>
                  <td className="px-3 py-4">{formatStatusLabel(item.evidenceType)}</td>
                  <td className="px-3 py-4">{item.uploadedByName}</td>
                  <td className="px-3 py-4">{formatStatusLabel(item.synchronizationStatus)}</td>
                  <td className="px-3 py-4 text-right">
                    <div className="flex justify-end gap-2">
                      <button
                        type="button"
                        onClick={() => void showPreview(item)}
                        className="rounded-lg bg-brand-700 px-3 py-2 font-bold text-white"
                      >
                        Ver
                      </button>
                      <button
                        type="button"
                        onClick={() => void download(item)}
                        className="rounded-lg border px-3 py-2 font-bold"
                      >
                        Descargar
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {loading && <TableSkeleton rows={5} columns={6} />}
          {!loading && !error && result.items.length === 0 && (
            <p className="p-10 text-center text-sm text-ink-muted">
              No hay evidencias disponibles.
            </p>
          )}
        </div>
      </div>
      {open && (
        <EvidenceForm evaluations={evaluations} onClose={() => setOpen(false)} onSave={save} />
      )}
      {preview && (
        <EvidencePreview preview={preview} onClose={closePreview} onDownload={download} />
      )}
    </section>
  )
}

function EvidencePreview({
  preview,
  onClose,
  onDownload,
}: {
  preview: EvidencePreviewState
  onClose: () => void
  onDownload: (item: EvidenceSummary) => Promise<void>
}) {
  const { item, url, loading, error } = preview
  const isImage = item.mimeType.startsWith('image/')
  const isPdf = item.mimeType === 'application/pdf'
  const isVideo = item.mimeType.startsWith('video/')

  return (
    <div className="sigersa-modal-overlay fixed inset-0 z-50 grid place-items-center p-4">
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="evidence-preview-title"
        className="sigersa-modal-panel flex max-h-[92vh] w-full max-w-5xl flex-col overflow-hidden"
      >
        <header className="flex items-start justify-between gap-4 border-b px-5 py-4">
          <div className="min-w-0">
            <p className="text-xs font-bold tracking-[0.12em] text-brand-700 uppercase">
              Vista previa de evidencia
            </p>
            <h2 id="evidence-preview-title" className="mt-1 truncate text-xl font-extrabold">
              {item.originalName}
            </h2>
            <p className="mt-1 text-xs text-ink-muted">
              {(item.fileSize / 1024).toFixed(1)} KB · {item.mimeType}
            </p>
          </div>
          <button type="button" onClick={onClose} aria-label="Cerrar vista previa">
            ✕
          </button>
        </header>

        <div className="grid min-h-[22rem] flex-1 place-items-center overflow-auto bg-slate-100 p-4 sm:min-h-[34rem]">
          {loading && <p className="font-semibold text-ink-muted">Cargando archivo…</p>}
          {error && <p className="rounded-xl bg-red-50 p-4 text-sm text-red-800">{error}</p>}
          {!loading && !error && url && isImage && (
            <img
              src={url}
              alt={`Evidencia ${item.originalName}`}
              className="max-h-[65vh] max-w-full rounded-lg object-contain shadow-sm"
            />
          )}
          {!loading && !error && url && isPdf && (
            <iframe
              src={url}
              title={`Vista previa de ${item.originalName}`}
              className="h-[65vh] w-full rounded-lg bg-white"
            />
          )}
          {!loading && !error && url && isVideo && (
            <video src={url} controls className="max-h-[65vh] max-w-full rounded-lg bg-black">
              <track
                kind="captions"
                src="data:text/vtt,WEBVTT"
                srcLang="es"
                label="Sin subtítulos disponibles"
              />
              Su navegador no puede reproducir este archivo.
            </video>
          )}
          {!loading && !error && url && !isImage && !isPdf && !isVideo && (
            <p className="max-w-md rounded-xl bg-white p-6 text-center text-sm text-ink-muted">
              Este formato no admite vista previa en el navegador. Puede descargarlo para abrirlo
              con una aplicación compatible.
            </p>
          )}
        </div>

        <footer className="flex justify-end gap-3 border-t px-5 py-4">
          <button
            type="button"
            onClick={onClose}
            className="rounded-xl border px-4 py-2.5 font-bold"
          >
            Cerrar
          </button>
          <button
            type="button"
            onClick={() => void onDownload(item)}
            className="rounded-xl bg-brand-700 px-5 py-2.5 font-bold text-white"
          >
            Descargar
          </button>
        </footer>
      </section>
    </div>
  )
}

function EvidenceForm({
  evaluations,
  onClose,
  onSave,
}: {
  evaluations: EvaluationSummary[]
  onClose: () => void
  onSave: (evaluationId: string, evidenceType: string, file: File) => Promise<void>
}) {
  const evidenceTypes = useParameterOptions('TIPO_EVIDENCIA')
  const [evaluationId, setEvaluationId] = useState('')
  const [evidenceType, setEvidenceType] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [saving, setSaving] = useState(false)
  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!file) return
    setSaving(true)
    try {
      await onSave(evaluationId, evidenceType, file)
    } finally {
      setSaving(false)
    }
  }
  return (
    <div
      className="sigersa-modal-overlay fixed inset-0 z-50 grid place-items-center p-4"
      role="presentation"
    >
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="evidence-form-title"
        className="sigersa-modal-panel w-full max-w-xl p-6"
      >
        <div className="flex justify-between">
          <h2 id="evidence-form-title" className="text-xl font-extrabold">
            Adjuntar evidencia
          </h2>
          <button type="button" onClick={onClose} aria-label="Cerrar">
            ✕
          </button>
        </div>
        <form onSubmit={(event) => void submit(event)} className="mt-6 grid gap-4">
          <label className="text-sm font-bold">
            Evaluación asignada
            <select
              required
              value={evaluationId}
              onChange={(e) => setEvaluationId(e.target.value)}
              className="mt-1.5 min-h-11 w-full rounded-xl border px-3 font-normal"
            >
              <option value="">Seleccione</option>
              {evaluations.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.number} · {item.establishmentName}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold">
            Tipo
            <select
              required
              value={evidenceType}
              onChange={(e) => setEvidenceType(e.target.value)}
              className="mt-1.5 min-h-11 w-full rounded-xl border px-3 font-normal"
            >
              <option value="">Seleccione</option>
              {evidenceTypes.options.map((value) => (
                <option key={value.parametersId} value={value.stringData ?? ''}>
                  {formatStatusLabel(value.stringData ?? '')}
                </option>
              ))}
            </select>
            {!evidenceTypes.loading && evidenceTypes.options.length === 0 && (
              <span className="mt-1 block text-xs font-normal text-amber-700">
                El catálogo TIPO_EVIDENCIA no tiene valores activos.
              </span>
            )}
          </label>
          <label className="text-sm font-bold">
            Archivo
            <input
              required
              type="file"
              accept="image/jpeg,image/png,application/pdf,video/mp4"
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
              className="mt-1.5 block w-full rounded-xl border p-3 font-normal"
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
              disabled={saving || !file}
              className="rounded-xl bg-brand-700 px-5 py-2.5 font-bold text-white disabled:opacity-50"
            >
              {saving ? 'Guardando…' : 'Guardar evidencia'}
            </button>
          </div>
        </form>
      </section>
    </div>
  )
}
