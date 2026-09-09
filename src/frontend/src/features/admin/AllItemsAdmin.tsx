import { useEffect, useState, type FormEvent } from 'react'
import {
  createAllItem,
  deleteAllItem,
  getAllItems,
  updateAllItem,
  type AllItem,
  type AllItemDraft,
} from '../../lib/api'

const emptyDraft: AllItemDraft = { itemsId: '', description: '', sectionType: 'I', parents: null }

export function AllItemsAdmin() {
  const [items, setItems] = useState<AllItem[]>([])
  const [draft, setDraft] = useState<AllItemDraft>(emptyDraft)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [message, setMessage] = useState('')

  useEffect(() => {
    void reload()
  }, [])

  async function reload() {
    try {
      setItems(await getAllItems())
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No fue posible cargar los ítems.')
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
      setMessage(editingId === null ? 'Nodo agregado.' : 'Nodo actualizado.')
      await reload()
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No fue posible guardar el nodo.')
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
    if (!window.confirm(`¿Remover "${item.description}" de la plantilla?`)) return
    try {
      await deleteAllItem(item.items)
      setMessage('Nodo removido.')
      await reload()
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No fue posible remover el nodo.')
    }
  }

  return (
    <section aria-labelledby="admin-title">
      <h1 id="admin-title" className="text-2xl font-extrabold text-ink-strong">
        Administrar ficha base
      </h1>
      <p className="mt-2 text-sm text-ink-muted">
        Agregue, edite o remueva textos y relaciones jerárquicas de AllItems.
      </p>
      <form
        onSubmit={submit}
        className="mt-6 grid gap-4 rounded-card bg-white p-5 shadow-card lg:grid-cols-2"
      >
        <label className="text-sm font-bold text-ink-body">
          Código del nodo
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
            value={draft.sectionType}
            onChange={(event) => setDraft({ ...draft, sectionType: event.target.value })}
            className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
          >
            <option value="C">Categoría</option>
            <option value="S">Sección</option>
            <option value="SS">Subsección</option>
            <option value="A">Agrupación</option>
            <option value="I">Pregunta</option>
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
          Código del padre (vacío para raíz)
          <input
            maxLength={255}
            value={draft.parents ?? ''}
            onChange={(event) => setDraft({ ...draft, parents: event.target.value })}
            className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
          />
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
              onClick={() => {
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
            {items.map((item) => (
              <tr key={item.items}>
                <td className="p-3 font-semibold">{item.items}</td>
                <td className="p-3">{item.itemsId}</td>
                <td className="max-w-xl p-3">{item.description}</td>
                <td className="p-3">{item.sectionType}</td>
                <td className="p-3">{item.parents ?? '—'}</td>
                <td className="p-3">
                  <div className="flex gap-2">
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
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}
