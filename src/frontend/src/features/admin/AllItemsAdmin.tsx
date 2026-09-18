import { useEffect, useState, type FormEvent } from 'react'
import { TableSkeleton } from '../../components/feedback/Skeletons'
import { Pagination, useClientPagination } from '../../components/ui/Pagination'
import {
  createAllItem,
  deleteAllItem,
  getAllItems,
  reorderAllItems,
  updateAllItem,
  type AllItem,
  type AllItemDraft,
} from '../../lib/api'
import { alerts } from '../../lib/alerts'
import { useParameterOptions } from '../../hooks/useParameterOptions'

const emptyDraft: AllItemDraft = { itemsId: '', description: '', sectionType: '', parents: null }

export function AllItemsAdmin({ readOnly = false }: { readOnly?: boolean }) {
  const sectionTypes = useParameterOptions('TIPO_SECCION_ALLITEMS')
  const sectionTypeLabels = Object.fromEntries(
    sectionTypes.options.map((option) => [
      option.cCode ?? '',
      option.stringData ?? option.cCode ?? '',
    ]),
  )
  const [items, setItems] = useState<AllItem[]>([])
  const [draft, setDraft] = useState<AllItemDraft>(emptyDraft)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<AllItem | null>(null)
  const [reparentToItems, setReparentToItems] = useState('')
  const [message, setMessage] = useState('')
  const [proposalOpen, setProposalOpen] = useState(false)
  const [proposal, setProposal] = useState('')
  const pagination = useClientPagination(items)

  useEffect(() => {
    void reload()
  }, [])

  async function reload() {
    try {
      setItems(await getAllItems())
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No fue posible cargar los ítems.')
      await alerts.error(error, 'No se pudo cargar la ficha')
    }
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    try {
      const normalized = { ...draft, parents: draft.parents?.trim() || null }
      if (editingId === null) await createAllItem(normalized)
      else await updateAllItem(editingId, normalized)
      setDraft(emptyDraft)
      setEditingId(null)
      const wasCreating = editingId === null
      setMessage(wasCreating ? 'Nodo agregado.' : 'Nodo actualizado.')
      await reload()
      await alerts.success(
        wasCreating ? 'Ítem agregado' : 'Ítem actualizado',
        'La estructura de la ficha se guardó correctamente.',
      )
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No fue posible guardar el nodo.')
      await alerts.error(error, 'No se pudo guardar el ítem')
    }
  }

  function edit(item: AllItem) {
    setEditingId(item.items)
    setDraft({
      itemsId: item.itemsId,
      description: item.description,
      sectionType: item.sectionType,
      parents: item.parents,
    })
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  async function remove(item: AllItem) {
    const hasChildren = items.some((candidate) => candidate.parents === item.itemsId)
    if (hasChildren) {
      setDeleteTarget(item)
      setReparentToItems('')
      return
    }
    await executeRemove(item)
  }

  async function executeRemove(
    item: AllItem,
    childStrategy?: 'SUBTREE' | 'REPARENT',
    newParent?: number,
  ) {
    const confirmed = await alerts.confirm({
      title: childStrategy === 'SUBTREE' ? '¿Eliminar el subárbol?' : '¿Remover este ítem?',
      text:
        childStrategy === 'SUBTREE'
          ? `Se eliminará «${item.description}» junto con todos sus descendientes. Esta acción no se puede deshacer.`
          : `Se removerá «${item.description}» de la ficha. Esta acción no se puede deshacer.`,
      confirmText: childStrategy === 'SUBTREE' ? 'Eliminar subárbol' : 'Remover',
    })
    if (!confirmed) return
    try {
      await deleteAllItem(item.items, childStrategy, newParent)
      setDeleteTarget(null)
      setMessage('Nodo removido.')
      await reload()
      await alerts.success('Ítem removido', 'La ficha fue actualizada correctamente.')
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No fue posible remover el nodo.')
      await alerts.error(error, 'No se pudo remover el ítem')
    }
  }

  async function move(index: number, direction: -1 | 1) {
    const target = index + direction
    if (target < 0 || target >= items.length) return
    const reordered = [...items]
    ;[reordered[index], reordered[target]] = [reordered[target], reordered[index]]
    try {
      setItems(await reorderAllItems(reordered.map((item) => item.items)))
      setMessage('Orden de la ficha actualizado.')
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No fue posible reordenar la ficha.')
      await alerts.error(error, 'No se pudo cambiar el orden')
    }
  }

  if (readOnly) {
    return (
      <section aria-labelledby="catalog-title">
        <div className="flex flex-col justify-between gap-4 md:flex-row md:items-end">
          <div>
            <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">
              Diseño BPM
            </p>
            <h1 id="catalog-title" className="mt-2 text-2xl font-extrabold text-ink-strong">
              Fichas BPM
            </h1>
            <p className="mt-2 text-sm text-ink-muted">
              Consulte la estructura jerárquica vigente y documente propuestas para revisión
              administrativa.
            </p>
          </div>
          <button
            type="button"
            onClick={() => setProposalOpen(true)}
            className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white"
          >
            + Proponer ajuste
          </button>
        </div>
        {message && (
          <p role="status" className="mt-4 rounded-xl bg-brand-50 p-3 text-sm text-brand-900">
            {message}
          </p>
        )}
        <div className="mt-6 overflow-x-auto rounded-card bg-white shadow-card">
          <table className="min-w-full text-left text-sm">
            <caption className="sr-only">Estructura de la ficha BPM</caption>
            <thead className="bg-surface-muted text-ink-muted">
              <tr>
                <th className="p-3">Orden</th>
                <th className="p-3">Código</th>
                <th className="p-3">Descripción</th>
                <th className="p-3">Tipo</th>
                <th className="p-3">Padre</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {pagination.visibleItems.map((item) => (
                <tr key={item.items}>
                  <td className="p-3 font-semibold">{item.items}</td>
                  <td className="p-3">{item.itemsId}</td>
                  <td className="max-w-xl p-3">{item.description}</td>
                  <td className="p-3">{sectionTypeLabels[item.sectionType] ?? item.sectionType}</td>
                  <td className="p-3">{item.parents ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {items.length === 0 && !message && <TableSkeleton rows={6} columns={5} />}
        </div>
        <Pagination
          page={pagination.page}
          pageSize={pagination.pageSize}
          total={items.length}
          label="ítems BPM"
          onChange={pagination.setPage}
        />
        {proposalOpen && (
          <div
            className="sigersa-modal-overlay fixed inset-0 z-50 flex items-center justify-center p-4"
            role="presentation"
          >
            <form
              onSubmit={(event) => {
                event.preventDefault()
                setProposalOpen(false)
                setProposal('')
                void alerts.success(
                  'Propuesta registrada',
                  'El ajuste quedó preparado para la revisión administrativa.',
                )
              }}
              className="sigersa-modal-panel w-full max-w-xl p-6"
              role="dialog"
              aria-modal="true"
              aria-labelledby="proposal-title"
            >
              <h2 id="proposal-title" className="text-xl font-extrabold text-ink-strong">
                Proponer ajuste de ficha
              </h2>
              <p className="mt-2 text-sm text-ink-muted">
                Indique el nodo afectado, el cambio sugerido y su justificación.
              </p>
              <label className="mt-5 block text-sm font-bold text-ink-body">
                Detalle de la propuesta
                <textarea
                  required
                  rows={6}
                  value={proposal}
                  onChange={(event) => setProposal(event.target.value)}
                  className="mt-1.5 w-full rounded-xl border border-slate-300 p-3 font-normal"
                />
              </label>
              <div className="mt-5 flex justify-end gap-3">
                <button
                  type="button"
                  onClick={() => setProposalOpen(false)}
                  className="min-h-11 rounded-xl border border-slate-300 px-5 font-bold"
                >
                  Cancelar
                </button>
                <button className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white">
                  Enviar propuesta
                </button>
              </div>
            </form>
          </div>
        )}
      </section>
    )
  }

  return (
    <section aria-labelledby="admin-title">
      <h1 id="admin-title" className="text-2xl font-extrabold text-ink-strong">
        Administrar ficha base
      </h1>
      <p className="mt-2 text-sm text-ink-muted">
        Construya la ficha de arriba hacia abajo: cree primero el capítulo y después sus secciones,
        subsecciones, agrupaciones y preguntas.
      </p>
      <div className="mt-5 grid gap-3 md:grid-cols-3" aria-label="Guía para editar la ficha">
        <div className="rounded-xl border border-brand-200 bg-brand-50 p-4">
          <strong className="text-sm text-brand-900">1. Defina el ítem</strong>
          <p className="mt-1 text-xs leading-5 text-ink-body">
            Asigne un código único, el tipo y un texto claro.
          </p>
        </div>
        <div className="rounded-xl border border-brand-200 bg-brand-50 p-4">
          <strong className="text-sm text-brand-900">2. Ubíquelo en la jerarquía</strong>
          <p className="mt-1 text-xs leading-5 text-ink-body">
            Seleccione el ítem padre. Déjelo vacío sólo si será un capítulo raíz.
          </p>
        </div>
        <div className="rounded-xl border border-brand-200 bg-brand-50 p-4">
          <strong className="text-sm text-brand-900">3. Revise el orden</strong>
          <p className="mt-1 text-xs leading-5 text-ink-body">
            Use las flechas de la tabla para ajustar la secuencia final.
          </p>
        </div>
      </div>
      <div className="mt-5 flex flex-wrap gap-2" aria-label="Accesos rápidos para agregar ítems">
        {sectionTypes.options.map((option) => (
          <button
            key={option.parametersId}
            type="button"
            onClick={() => {
              setEditingId(null)
              setDraft({ ...emptyDraft, sectionType: option.cCode ?? '' })
              window.scrollTo({ top: 260, behavior: 'smooth' })
            }}
            className="border-brand-300 text-brand-800 min-h-10 rounded-xl border bg-white px-4 text-sm font-bold"
          >
            + Agregar {(option.stringData ?? option.cCode ?? '').toLowerCase()}
          </button>
        ))}
      </div>
      <form
        onSubmit={submit}
        className="mt-6 grid gap-4 rounded-card bg-white p-5 shadow-card lg:grid-cols-2"
      >
        <label className="text-sm font-bold text-ink-body">
          Código del nodo
          <span
            className="ml-1 cursor-help text-brand-700"
            title="Identificador único usado para relacionar padres e hijos."
            aria-label="Ayuda sobre el código del nodo"
          >
            ⓘ
          </span>
          <input
            required
            maxLength={50}
            value={draft.itemsId}
            onChange={(event) => setDraft({ ...draft, itemsId: event.target.value })}
            className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
          />
        </label>
        <label className="text-sm font-bold text-ink-body">
          Tipo de sección
          <select
            required
            value={draft.sectionType}
            onChange={(event) => setDraft({ ...draft, sectionType: event.target.value })}
            className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
          >
            <option value="">Seleccione</option>
            {sectionTypes.options.map((option) => (
              <option key={option.parametersId} value={option.cCode ?? ''}>
                {option.stringData ?? option.cCode}
              </option>
            ))}
          </select>
        </label>
        <label className="text-sm font-bold text-ink-body lg:col-span-2">
          Descripción
          <textarea
            required
            value={draft.description}
            onChange={(event) => setDraft({ ...draft, description: event.target.value })}
            rows={3}
            className="mt-1.5 w-full rounded-xl border border-slate-300 p-3 font-normal"
          />
        </label>
        <label className="text-sm font-bold text-ink-body">
          Ítem padre (vacío para capítulo raíz)
          <span
            className="ml-1 cursor-help text-brand-700"
            title="El padre determina dónde aparecerá este ítem dentro de la ficha."
            aria-label="Ayuda sobre el ítem padre"
          >
            ⓘ
          </span>
          <select
            value={draft.parents ?? ''}
            onChange={(event) => setDraft({ ...draft, parents: event.target.value || null })}
            className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
          >
            <option value="">Sin padre (raíz)</option>
            {items
              .filter((item) => item.items !== editingId)
              .map((item) => (
                <option key={item.items} value={item.itemsId}>
                  {item.itemsId} — {item.description}
                </option>
              ))}
          </select>
        </label>
        <div className="flex items-end gap-2">
          <button
            type="submit"
            className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white"
          >
            {editingId === null ? 'Agregar nodo' : 'Guardar cambios'}
          </button>
          {editingId !== null && (
            <button
              type="button"
              onClick={async () => {
                if (
                  !(await alerts.confirm({
                    title: '¿Cancelar la edición?',
                    text: 'Los cambios no guardados se perderán.',
                    confirmText: 'Descartar cambios',
                  }))
                )
                  return
                setEditingId(null)
                setDraft(emptyDraft)
              }}
              className="min-h-11 rounded-xl border border-slate-300 px-4 font-bold"
            >
              Cancelar
            </button>
          )}
        </div>
      </form>
      {message && (
        <p className="mt-4 rounded-xl bg-brand-100 p-3 text-sm text-brand-900" role="status">
          {message}
        </p>
      )}
      {deleteTarget && (
        <div className="sigersa-modal-overlay fixed inset-0 z-50 grid place-items-center p-4">
          <section
            role="dialog"
            aria-modal="true"
            aria-labelledby="delete-node-title"
            className="sigersa-modal-panel w-full max-w-xl p-6"
          >
            <h2 id="delete-node-title" className="font-extrabold text-ink-strong">
              El nodo tiene hijos
            </h2>
            <p className="mt-2 text-sm text-ink-body">
              Elija si desea eliminar todo el subárbol de «{deleteTarget.description}» o conservar
              sus hijos asignándolos a otro padre.
            </p>
            <div className="mt-5 flex flex-wrap items-end gap-3">
              <button
                type="button"
                onClick={() => void executeRemove(deleteTarget, 'SUBTREE')}
                className="min-h-11 rounded-xl bg-red-700 px-4 font-bold text-white"
              >
                Eliminar subárbol
              </button>
              <label className="text-sm font-bold text-ink-body">
                Nuevo padre de los hijos
                <select
                  value={reparentToItems}
                  onChange={(event) => setReparentToItems(event.target.value)}
                  className="mt-1 block min-h-11 rounded-xl border border-slate-300 bg-white px-3 font-normal"
                >
                  <option value="">Padre actual del nodo</option>
                  {items
                    .filter((item) => item.items !== deleteTarget.items)
                    .map((item) => (
                      <option key={item.items} value={item.items}>
                        {item.items}. {item.itemsId} — {item.description}
                      </option>
                    ))}
                </select>
              </label>
              <button
                type="button"
                onClick={() =>
                  void executeRemove(
                    deleteTarget,
                    'REPARENT',
                    reparentToItems ? Number(reparentToItems) : undefined,
                  )
                }
                className="min-h-11 rounded-xl bg-brand-700 px-4 font-bold text-white"
              >
                Reubicar hijos y remover
              </button>
              <button
                type="button"
                onClick={() => setDeleteTarget(null)}
                className="min-h-11 rounded-xl border border-slate-300 bg-white px-4 font-bold"
              >
                Cancelar
              </button>
            </div>
          </section>
        </div>
      )}
      <div className="mt-6 overflow-x-auto rounded-card bg-white shadow-card">
        <table className="min-w-full text-left text-sm">
          <caption className="sr-only">Nodos de la ficha AllItems</caption>
          <thead className="bg-surface-muted text-ink-muted">
            <tr>
              <th className="p-3">Orden</th>
              <th className="p-3">Código</th>
              <th className="p-3">Descripción</th>
              <th className="p-3">Tipo</th>
              <th className="p-3">Padre</th>
              <th className="p-3">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {pagination.visibleItems.map((item, index) => {
              const absoluteIndex = pagination.startIndex + index
              return (
                <tr key={item.items}>
                  <td className="p-3 font-semibold">{item.items}</td>
                  <td className="p-3">{item.itemsId}</td>
                  <td className="max-w-xl p-3">{item.description}</td>
                  <td className="p-3">{sectionTypeLabels[item.sectionType] ?? item.sectionType}</td>
                  <td className="p-3">{item.parents ?? '—'}</td>
                  <td className="p-3">
                    <div className="flex gap-2">
                      <button
                        type="button"
                        disabled={absoluteIndex === 0}
                        aria-label={`Mover ${item.itemsId} hacia arriba`}
                        onClick={() => void move(absoluteIndex, -1)}
                        className="min-h-10 rounded-lg border border-slate-300 px-3 font-bold disabled:opacity-40"
                      >
                        ↑
                      </button>
                      <button
                        type="button"
                        disabled={absoluteIndex === items.length - 1}
                        aria-label={`Mover ${item.itemsId} hacia abajo`}
                        onClick={() => void move(absoluteIndex, 1)}
                        className="min-h-10 rounded-lg border border-slate-300 px-3 font-bold disabled:opacity-40"
                      >
                        ↓
                      </button>
                      <button
                        type="button"
                        onClick={() => edit(item)}
                        className="min-h-10 rounded-lg border border-brand-700 px-3 font-bold text-brand-700"
                      >
                        Editar
                      </button>
                      <button
                        type="button"
                        onClick={() => void remove(item)}
                        className="min-h-10 rounded-lg border border-red-300 px-3 font-bold text-red-700"
                      >
                        Remover
                      </button>
                    </div>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
      <Pagination
        page={pagination.page}
        pageSize={pagination.pageSize}
        total={items.length}
        label="ítems BPM"
        onChange={pagination.setPage}
      />
    </section>
  )
}
