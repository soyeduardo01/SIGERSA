import { useEffect, useState, type FormEvent } from 'react'
import { TableSkeleton } from '../../components/feedback/Skeletons'
import { useParameterOptions } from '../../hooks/useParameterOptions'
import {
  createCompany,
  getCompanies,
  getEstablishmentOptions,
  updateCompany,
  type CompaniesPage,
  type CompanyDraft,
  type CompanySummary,
  type EstablishmentOption,
  type MunicipalityOption,
} from '../../lib/api'
import { alerts } from '../../lib/alerts'
import { formatStatusLabel } from '../../lib/formatters'

const emptyPage: CompaniesPage = { items: [], page: 1, pageSize: 10, total: 0 }

const emptyDraft: CompanyDraft = {
  legalName: '',
  taxId: '',
  tradeName: null,
  economicActivity: null,
  phone: null,
  email: null,
  address: null,
  municipalityId: null,
  contacts: [],
  status: '',
  rowVersion: null,
}

export function CompaniesManagement() {
  const statuses = useParameterOptions('ESTADO_EMPRESA')
  const [result, setResult] = useState<CompaniesPage>(emptyPage)
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [editing, setEditing] = useState<CompanySummary | null>(null)
  const [modalOpen, setModalOpen] = useState(false)
  const [provinces, setProvinces] = useState<EstablishmentOption[]>([])
  const [municipalities, setMunicipalities] = useState<MunicipalityOption[]>([])

  useEffect(() => {
    void getEstablishmentOptions()
      .then((options) => {
        setProvinces(options.provinces)
        setMunicipalities(options.municipalities)
      })
      .catch((caught) => void alerts.error(caught, 'No se pudo cargar la ubicación'))
  }, [])

  useEffect(() => {
    let active = true
    void getCompanies({ search, status, page, pageSize: 10 })
      .then((value) => {
        if (!active) return
        setResult(value)
        setError('')
      })
      .catch((caught) => {
        if (active)
          setError(caught instanceof Error ? caught.message : 'No se pudieron cargar las empresas.')
      })
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [page, search, status])

  async function reload() {
    setLoading(true)
    try {
      setResult(await getCompanies({ search, status, page, pageSize: 10 }))
      setError('')
    } finally {
      setLoading(false)
    }
  }

  function openCreate() {
    setEditing(null)
    setModalOpen(true)
  }

  function openEdit(company: CompanySummary) {
    setEditing(company)
    setModalOpen(true)
  }

  async function save(draft: CompanyDraft) {
    try {
      if (editing) await updateCompany(editing.id, draft)
      else await createCompany(draft)
      setModalOpen(false)
      await reload()
      await alerts.success(
        editing ? 'Empresa actualizada' : 'Empresa registrada',
        'Los datos ya están disponibles para usuarios y establecimientos.',
      )
    } catch (caught) {
      await alerts.error(caught, 'No se pudo guardar la empresa')
      throw caught
    }
  }

  const pages = Math.max(1, Math.ceil(result.total / result.pageSize))

  return (
    <section aria-labelledby="companies-title">
      <div className="flex flex-col justify-between gap-4 md:flex-row md:items-end">
        <div>
          <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">
            Administración empresarial
          </p>
          <h1 id="companies-title" className="mt-2 text-2xl font-extrabold text-ink-strong">
            Empresas
          </h1>
          <p className="mt-2 text-sm text-ink-muted">
            Registre y mantenga las organizaciones disponibles en los demás módulos.
          </p>
        </div>
        <button
          type="button"
          onClick={openCreate}
          className="hover:bg-brand-800 min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white shadow-sm"
        >
          + Nueva empresa
        </button>
      </div>

      <div className="mt-6 rounded-card bg-white p-5 shadow-card">
        <form
          onSubmit={(event) => {
            event.preventDefault()
            setLoading(true)
            setPage(1)
            setSearch(searchInput.trim())
          }}
          className="grid gap-3 md:grid-cols-[1fr_14rem_auto]"
        >
          <label className="text-sm font-bold text-ink-body">
            Buscar
            <input
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Razón social, RNC o nombre comercial"
              className={inputClass}
            />
          </label>
          <label className="text-sm font-bold text-ink-body">
            Estado
            <select
              value={status}
              onChange={(event) => {
                setLoading(true)
                setPage(1)
                setStatus(event.target.value)
              }}
              className={inputClass}
            >
              <option value="">Todos</option>
              {statuses.options.map((option) => (
                <option key={option.parametersId} value={option.stringData ?? ''}>
                  {formatStatusLabel(option.stringData ?? '')}
                </option>
              ))}
            </select>
          </label>
          <button className="text-brand-800 mt-auto min-h-11 rounded-xl border border-brand-700 px-5 font-bold">
            Aplicar
          </button>
        </form>

        {error && (
          <p
            role="alert"
            className="mt-5 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800"
          >
            {error}
          </p>
        )}
        {loading ? (
          <TableSkeleton rows={5} columns={5} />
        ) : (
          <div className="mt-5 overflow-x-auto">
            <table className="w-full min-w-[820px] text-left text-sm">
              <thead>
                <tr className="border-b border-slate-200 text-xs tracking-wide text-ink-muted uppercase">
                  <th className="px-3 py-3">Razón social</th>
                  <th className="px-3 py-3">RNC</th>
                  <th className="px-3 py-3">Contacto</th>
                  <th className="px-3 py-3">Estado</th>
                  <th className="px-3 py-3 text-right">Acciones</th>
                </tr>
              </thead>
              <tbody>
                {result.items.map((company) => (
                  <tr key={company.id} className="border-b border-slate-100">
                    <td className="px-3 py-4 font-bold text-ink-strong">
                      {company.legalName}
                      {company.tradeName && (
                        <span className="mt-1 block text-xs font-normal text-ink-muted">
                          {company.tradeName}
                        </span>
                      )}
                    </td>
                    <td className="px-3 py-4 text-ink-body">{company.taxId}</td>
                    <td className="px-3 py-4 text-ink-body">
                      {company.email ?? company.phone ?? '—'}
                      {(company.municipalityName || company.provinceName) && (
                        <span className="mt-1 block text-xs text-ink-muted">
                          {[company.municipalityName, company.provinceName]
                            .filter(Boolean)
                            .join(', ')}
                        </span>
                      )}
                    </td>
                    <td className="px-3 py-4">
                      <span className="text-brand-800 rounded-full bg-brand-50 px-2.5 py-1 text-xs font-bold">
                        {formatStatusLabel(company.status)}
                      </span>
                    </td>
                    <td className="px-3 py-4 text-right">
                      <div className="flex justify-end gap-2">
                        <a
                          href={`/modulo.html?module=reportes&search=${encodeURIComponent(company.legalName)}`}
                          className="rounded-lg border border-slate-300 px-3 py-2 font-bold"
                        >
                          Evaluaciones
                        </a>
                        <a
                          href={`/modulo.html?module=auditoria&search=${company.id}`}
                          className="rounded-lg border border-slate-300 px-3 py-2 font-bold"
                        >
                          Actividad
                        </a>
                        <button
                          type="button"
                          onClick={() => openEdit(company)}
                          className="rounded-lg border border-slate-300 px-3 py-2 font-bold"
                        >
                          Editar
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {result.items.length === 0 && (
              <div className="p-10 text-center">
                <p className="font-bold text-ink-strong">No hay empresas registradas</p>
                <p className="mt-2 text-sm text-ink-muted">
                  Registre la primera empresa para comenzar.
                </p>
              </div>
            )}
          </div>
        )}

        <div className="mt-5 flex items-center justify-between border-t border-slate-100 pt-4 text-sm">
          <span className="text-ink-muted">{result.total} empresa(s)</span>
          <div className="flex gap-2">
            <button
              type="button"
              disabled={page <= 1}
              onClick={() => setPage((current) => current - 1)}
              className="rounded-lg border border-slate-300 px-3 py-2 font-bold disabled:opacity-40"
            >
              Anterior
            </button>
            <span className="px-2 py-2">
              {page} / {pages}
            </span>
            <button
              type="button"
              disabled={page >= pages}
              onClick={() => setPage((current) => current + 1)}
              className="rounded-lg border border-slate-300 px-3 py-2 font-bold disabled:opacity-40"
            >
              Siguiente
            </button>
          </div>
        </div>
      </div>

      {modalOpen && (
        <CompanyFormModal
          company={editing}
          statuses={statuses.options.map((option) => option.stringData ?? '').filter(Boolean)}
          provinces={provinces}
          municipalities={municipalities}
          onClose={() => setModalOpen(false)}
          onSave={save}
        />
      )}
    </section>
  )
}

function CompanyFormModal({
  company,
  statuses,
  provinces,
  municipalities,
  onClose,
  onSave,
}: {
  company: CompanySummary | null
  statuses: string[]
  provinces: EstablishmentOption[]
  municipalities: MunicipalityOption[]
  onClose: () => void
  onSave: (draft: CompanyDraft) => Promise<void>
}) {
  const [draft, setDraft] = useState<CompanyDraft>(() =>
    company
      ? {
          legalName: company.legalName,
          taxId: company.taxId,
          tradeName: company.tradeName,
          economicActivity: company.economicActivity,
          phone: company.phone,
          email: company.email,
          address: company.address,
          municipalityId: company.municipalityId,
          contacts: company.contacts,
          status: company.status,
          rowVersion: company.rowVersion,
        }
      : { ...emptyDraft, status: statuses[0] ?? '' },
  )
  const [saving, setSaving] = useState(false)
  const [provinceId, setProvinceId] = useState(
    () => municipalities.find((item) => item.id === company?.municipalityId)?.provinceId ?? '',
  )

  async function submit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    try {
      await onSave(draft)
    } finally {
      setSaving(false)
    }
  }

  const setOptional = (field: keyof CompanyDraft, value: string) =>
    setDraft({ ...draft, [field]: value.trimStart() || null })

  const visibleMunicipalities = municipalities.filter(
    (municipality) => !provinceId || municipality.provinceId === provinceId,
  )

  function updateContact(
    type: 'LEGAL' | 'CALIDAD' | 'PRINCIPAL',
    field: 'fullName' | 'identification' | 'phone' | 'email',
    value: string,
  ) {
    const existing = draft.contacts.find((contact) => contact.type === type)
    const next = {
      type,
      fullName: existing?.fullName ?? '',
      identification: existing?.identification ?? null,
      phone: existing?.phone ?? null,
      email: existing?.email ?? null,
      [field]: field === 'fullName' ? value : value.trimStart() || null,
    }
    setDraft({
      ...draft,
      contacts: [
        ...draft.contacts.filter((contact) => contact.type !== type),
        ...(next.fullName.trim() ? [next] : []),
      ],
    })
  }

  return (
    <div className="sigersa-modal-overlay fixed inset-0 z-50 grid place-items-center p-4">
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="company-modal-title"
        className="sigersa-modal-panel w-full max-w-3xl overflow-hidden"
      >
        <div className="flex items-start justify-between border-b border-slate-200 px-6 py-5">
          <div>
            <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">Empresa</p>
            <h2 id="company-modal-title" className="mt-1 text-xl font-extrabold text-ink-strong">
              {company ? 'Editar empresa' : 'Registrar empresa'}
            </h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Cerrar formulario"
            className="rounded-lg px-3 py-2 text-xl text-slate-500 hover:bg-slate-100"
          >
            ×
          </button>
        </div>
        <form onSubmit={submit} className="grid gap-4 p-6 md:grid-cols-2">
          <label className="text-sm font-bold text-ink-body md:col-span-2">
            Razón social
            <input
              required
              maxLength={250}
              value={draft.legalName}
              onChange={(event) => setDraft({ ...draft, legalName: event.target.value })}
              className={inputClass}
            />
          </label>
          <label className="text-sm font-bold text-ink-body">
            RNC
            <input
              required
              maxLength={30}
              value={draft.taxId}
              onChange={(event) => setDraft({ ...draft, taxId: event.target.value })}
              className={inputClass}
            />
          </label>
          <label className="text-sm font-bold text-ink-body">
            Nombre comercial
            <input
              maxLength={250}
              value={draft.tradeName ?? ''}
              onChange={(event) => setOptional('tradeName', event.target.value)}
              className={inputClass}
            />
          </label>
          <label className="text-sm font-bold text-ink-body md:col-span-2">
            Actividad económica
            <input
              maxLength={250}
              value={draft.economicActivity ?? ''}
              onChange={(event) => setOptional('economicActivity', event.target.value)}
              className={inputClass}
            />
          </label>
          <label className="text-sm font-bold text-ink-body md:col-span-2">
            Dirección
            <textarea
              rows={2}
              maxLength={2000}
              value={draft.address ?? ''}
              onChange={(event) => setOptional('address', event.target.value)}
              className={`${inputClass} py-3`}
            />
          </label>
          <label className="text-sm font-bold text-ink-body">
            Provincia
            <select
              value={provinceId}
              onChange={(event) => {
                setProvinceId(event.target.value)
                setDraft({ ...draft, municipalityId: null })
              }}
              className={inputClass}
            >
              <option value="">Seleccione</option>
              {provinces.map((province) => (
                <option key={province.id} value={province.id}>
                  {province.name}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold text-ink-body">
            Municipio
            <select
              value={draft.municipalityId ?? ''}
              onChange={(event) =>
                setDraft({ ...draft, municipalityId: event.target.value || null })
              }
              className={inputClass}
            >
              <option value="">Seleccione</option>
              {visibleMunicipalities.map((municipality) => (
                <option key={municipality.id} value={municipality.id}>
                  {municipality.name}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold text-ink-body">
            Teléfono
            <input
              type="tel"
              maxLength={40}
              value={draft.phone ?? ''}
              onChange={(event) => setOptional('phone', event.target.value)}
              className={inputClass}
            />
          </label>
          <label className="text-sm font-bold text-ink-body">
            Correo electrónico
            <input
              type="email"
              maxLength={320}
              value={draft.email ?? ''}
              onChange={(event) => setOptional('email', event.target.value)}
              className={inputClass}
            />
          </label>
          <label className="text-sm font-bold text-ink-body md:col-span-2">
            Estado
            <select
              required
              value={draft.status}
              onChange={(event) => setDraft({ ...draft, status: event.target.value })}
              className={inputClass}
            >
              <option value="">Seleccione</option>
              {statuses.map((value) => (
                <option key={value} value={value}>
                  {formatStatusLabel(value)}
                </option>
              ))}
            </select>
          </label>
          <fieldset className="grid gap-4 rounded-xl border border-slate-200 p-4 md:col-span-2 md:grid-cols-2">
            <legend className="px-2 text-sm font-extrabold text-ink-strong">
              Representantes y contactos
            </legend>
            {(['LEGAL', 'CALIDAD', 'PRINCIPAL'] as const).map((type) => {
              const contact = draft.contacts.find((item) => item.type === type)
              return (
                <div key={type} className="grid gap-3 rounded-lg bg-slate-50 p-3 md:grid-cols-2">
                  <h3 className="text-sm font-bold text-brand-800 md:col-span-2">
                    {type === 'LEGAL'
                      ? 'Representante legal'
                      : type === 'CALIDAD'
                        ? 'Responsable de calidad'
                        : 'Contacto principal'}
                  </h3>
                  <label className="text-xs font-bold text-ink-body md:col-span-2">
                    Nombre completo
                    <input
                      maxLength={250}
                      value={contact?.fullName ?? ''}
                      onChange={(event) => updateContact(type, 'fullName', event.target.value)}
                      className={inputClass}
                    />
                  </label>
                  <label className="text-xs font-bold text-ink-body md:col-span-2">
                    Cédula o pasaporte
                    <input
                      maxLength={100}
                      value={contact?.identification ?? ''}
                      onChange={(event) =>
                        updateContact(type, 'identification', event.target.value)
                      }
                      className={inputClass}
                    />
                  </label>
                  <label className="text-xs font-bold text-ink-body">
                    Teléfono
                    <input
                      maxLength={40}
                      value={contact?.phone ?? ''}
                      onChange={(event) => updateContact(type, 'phone', event.target.value)}
                      className={inputClass}
                    />
                  </label>
                  <label className="text-xs font-bold text-ink-body">
                    Correo
                    <input
                      type="email"
                      maxLength={320}
                      value={contact?.email ?? ''}
                      onChange={(event) => updateContact(type, 'email', event.target.value)}
                      className={inputClass}
                    />
                  </label>
                </div>
              )
            })}
          </fieldset>
          <div className="flex justify-end gap-3 border-t border-slate-200 pt-5 md:col-span-2">
            <button
              type="button"
              onClick={onClose}
              className="min-h-11 rounded-xl border border-slate-300 px-5 font-bold"
            >
              Cancelar
            </button>
            <button
              disabled={saving}
              className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white disabled:opacity-50"
            >
              {saving ? 'Guardando…' : 'Guardar empresa'}
            </button>
          </div>
        </form>
      </section>
    </div>
  )
}

const inputClass =
  'mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 bg-white px-3 font-normal outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-100'
