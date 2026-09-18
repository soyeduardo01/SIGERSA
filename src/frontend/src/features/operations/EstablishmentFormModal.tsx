import {
  useEffect,
  useId,
  useMemo,
  useState,
  type Dispatch,
  type FormEvent,
  type ReactNode,
  type SetStateAction,
} from 'react'
import type {
  EstablishmentContactDraft,
  EstablishmentDetails,
  EstablishmentDraft,
  EstablishmentOptions,
} from '../../lib/api'
import { formatCedula, formatPhone, formatStatusLabel } from '../../lib/formatters'

interface EstablishmentFormModalProps {
  open: boolean
  options: EstablishmentOptions
  initial?: EstablishmentDetails | null
  saving?: boolean
  onClose: () => void
  onSave: (draft: EstablishmentDraft) => Promise<void>
}

type Tab = 'general' | 'production' | 'controls' | 'contacts'

const tabs: Array<[Tab, string]> = [
  ['general', 'Datos generales'],
  ['production', 'Producción y mercado'],
  ['controls', 'Controles y estado'],
  ['contacts', 'Contactos'],
]

const inputClass =
  'mt-1.5 min-h-11 w-full rounded-xl border border-slate-200 bg-white px-3 text-sm text-ink-strong outline-none transition focus:border-brand-500 focus:ring-2 focus:ring-brand-100 disabled:cursor-not-allowed disabled:bg-slate-100'

const emptyContact = (type: 'PRINCIPAL' | 'LEGAL'): EstablishmentContactDraft => ({
  type,
  fullName: '',
  identification: null,
  phone: null,
  email: null,
})

function newDraft(options: EstablishmentOptions): EstablishmentDraft {
  return {
    companyId: '',
    municipalityId: null,
    dpsDasId: null,
    commercializationId: null,
    code: '',
    name: '',
    street: null,
    addressNumber: null,
    phone: null,
    email: null,
    operationsStartDate: null,
    sanitaryPermitNumber: null,
    sanitaryPermitExpiresAt: null,
    productsDescription: null,
    annualProduction: null,
    femaleEmployees: null,
    maleEmployees: null,
    microbiologicalRejectionsLastFiveYears: 0,
    haccpImplemented: null,
    haccpPercentage: null,
    microbiologicalSamplingPlan: null,
    samplingApplicationCode: null,
    isInabieSupplier: null,
    inabieDistributionCode: null,
    status: options.statuses[0]?.stringData ?? '',
    marketIds: [],
    contacts: [],
    products: [],
    rowVersion: null,
  }
}

export function EstablishmentFormModal(props: EstablishmentFormModalProps) {
  const { open, options, initial, saving = false, onClose, onSave } = props
  const titleId = useId()
  const [tab, setTab] = useState<Tab>('general')
  const [draft, setDraft] = useState<EstablishmentDraft>(() => newDraft(options))
  const [provinceId, setProvinceId] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [subcategoryId, setSubcategoryId] = useState('')
  const [productDescription, setProductDescription] = useState('')
  const [monthlyVolume, setMonthlyVolume] = useState('')
  const [unit, setUnit] = useState('')
  const [owner, setOwner] = useState(() => emptyContact('PRINCIPAL'))
  const [representative, setRepresentative] = useState(() => emptyContact('LEGAL'))

  useEffect(() => {
    if (!open) return
    const source = initial?.data ?? newDraft(options)
    const maskedSource = {
      ...source,
      phone: source.phone ? formatPhone(source.phone) : null,
    }
    const product = source.products[0]
    queueMicrotask(() => {
      setDraft(maskedSource)
      setProvinceId(initial?.provinceId ?? '')
      setCategoryId(
        product
          ? (options.subcategories.find((value) => value.id === product.subcategoryId)
              ?.categoryId ?? '')
          : '',
      )
      setSubcategoryId(product?.subcategoryId ?? '')
      setProductDescription(product?.description ?? '')
      setMonthlyVolume(product?.monthlyVolume?.toString() ?? '')
      setUnit(product?.unit ?? '')
      setOwner(
        maskContact(
          source.contacts.find((contact) => contact.type === 'PRINCIPAL') ??
            emptyContact('PRINCIPAL'),
        ),
      )
      setRepresentative(
        maskContact(
          source.contacts.find((contact) => contact.type === 'LEGAL') ?? emptyContact('LEGAL'),
        ),
      )
      setTab('general')
    })
  }, [initial, open, options])

  useEffect(() => {
    if (!open) return
    const closeOnEscape = (event: KeyboardEvent) => event.key === 'Escape' && onClose()
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    document.addEventListener('keydown', closeOnEscape)
    return () => {
      document.body.style.overflow = previousOverflow
      document.removeEventListener('keydown', closeOnEscape)
    }
  }, [onClose, open])

  const municipalities = useMemo(
    () => options.municipalities.filter((value) => value.provinceId === provinceId),
    [options.municipalities, provinceId],
  )
  const subcategories = useMemo(
    () => options.subcategories.filter((value) => value.categoryId === categoryId),
    [categoryId, options.subcategories],
  )

  if (!open) return null

  function update<K extends keyof EstablishmentDraft>(key: K, value: EstablishmentDraft[K]) {
    setDraft((current) => ({ ...current, [key]: value }))
  }

  function updateContact(
    setter: Dispatch<SetStateAction<EstablishmentContactDraft>>,
    key: keyof EstablishmentContactDraft,
    value: string,
  ) {
    setter((current) => ({ ...current, [key]: value || null }))
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    const contacts = [owner, representative].filter((contact) => contact.fullName.trim())
    const products =
      subcategoryId && productDescription.trim()
        ? [
            {
              subcategoryId,
              description: productDescription.trim(),
              monthlyVolume: numberValue(monthlyVolume),
              unit: unit.trim() || null,
            },
            ...sourceProductsAfterFirst(draft.products),
          ]
        : draft.products
    await onSave({ ...draft, contacts, products })
  }

  const missingCatalogs = [
    options.companies.length === 0 && 'empresas',
    options.provinces.length === 0 && 'provincias',
    options.statuses.length === 0 && 'estados de establecimiento',
  ].filter(Boolean) as string[]

  return (
    <div className="sigersa-modal-overlay fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-6">
      <button
        type="button"
        className="absolute inset-0 cursor-default"
        aria-label="Cerrar formulario al pulsar fuera"
        onClick={onClose}
      />
      <div
        className="sigersa-modal-panel relative z-10 flex w-full max-w-5xl flex-col overflow-hidden"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
      >
        <header className="flex shrink-0 items-start justify-between border-b border-slate-100 px-5 py-4 sm:px-7">
          <div className="flex gap-4">
            <span className="grid size-12 shrink-0 place-items-center rounded-2xl bg-brand-50 text-brand-700">
              <svg
                viewBox="0 0 24 24"
                className="size-6"
                fill="none"
                stroke="currentColor"
                aria-hidden="true"
              >
                <path
                  strokeWidth="1.8"
                  d="M4 21V8l8-4 8 4v13M8 21v-5h8v5M8 10h.01M12 10h.01M16 10h.01"
                />
              </svg>
            </span>
            <div>
              <h2 id={titleId} className="text-xl font-extrabold tracking-tight text-ink-strong">
                {initial ? 'Editar establecimiento' : 'Registrar establecimiento'}
              </h2>
              <p className="mt-1 text-sm text-ink-muted">
                Datos definidos por la ficha oficial de inspección BPM.
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

        <div
          className="flex shrink-0 gap-1 overflow-x-auto border-b border-slate-100 px-5 sm:px-7"
          role="tablist"
        >
          {tabs.map(([value, label]) => (
            <button
              key={value}
              type="button"
              role="tab"
              aria-selected={tab === value}
              onClick={() => setTab(value)}
              className={`border-b-2 px-4 py-3 text-xs font-bold whitespace-nowrap transition ${tab === value ? 'border-brand-600 text-brand-700' : 'border-transparent text-slate-400 hover:text-slate-700'}`}
            >
              {label}
            </button>
          ))}
        </div>

        <form
          id="establishment-form"
          onSubmit={(event) => void submit(event)}
          className="min-h-0 flex-1 overflow-y-auto p-5 sm:p-7"
        >
          <p className="mb-4 text-xs text-ink-muted">
            Los campos marcados con <span className="font-bold text-red-600">*</span> son
            obligatorios.
          </p>
          {missingCatalogs.length > 0 && <CatalogNotice names={missingCatalogs} />}
          <FormSections
            isEditing={Boolean(initial)}
            tab={tab}
            draft={draft}
            options={options}
            provinceId={provinceId}
            categoryId={categoryId}
            subcategoryId={subcategoryId}
            municipalities={municipalities}
            subcategories={subcategories}
            productDescription={productDescription}
            monthlyVolume={monthlyVolume}
            unit={unit}
            owner={owner}
            representative={representative}
            update={update}
            setProvinceId={setProvinceId}
            setCategoryId={setCategoryId}
            setSubcategoryId={setSubcategoryId}
            setProductDescription={setProductDescription}
            setMonthlyVolume={setMonthlyVolume}
            setUnit={setUnit}
            updateOwner={(key, value) => updateContact(setOwner, key, value)}
            updateRepresentative={(key, value) => updateContact(setRepresentative, key, value)}
          />
        </form>

        <footer className="flex shrink-0 justify-end gap-2 border-t border-slate-100 px-5 py-4 sm:px-7">
          <button
            type="button"
            onClick={onClose}
            className="rounded-xl px-4 py-2.5 text-xs font-bold text-ink-body hover:bg-slate-100"
          >
            Cancelar
          </button>
          <button
            form="establishment-form"
            type="submit"
            disabled={saving || missingCatalogs.length > 0}
            className="hover:bg-brand-800 rounded-xl bg-brand-700 px-5 py-2.5 text-xs font-bold text-white shadow-lg shadow-emerald-900/15 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {saving ? 'Guardando…' : initial ? 'Guardar cambios' : 'Registrar establecimiento'}
          </button>
        </footer>
      </div>
    </div>
  )
}

interface FormSectionsProps {
  isEditing: boolean
  tab: Tab
  draft: EstablishmentDraft
  options: EstablishmentOptions
  provinceId: string
  categoryId: string
  subcategoryId: string
  municipalities: EstablishmentOptions['municipalities']
  subcategories: EstablishmentOptions['subcategories']
  productDescription: string
  monthlyVolume: string
  unit: string
  owner: EstablishmentContactDraft
  representative: EstablishmentContactDraft
  update: <K extends keyof EstablishmentDraft>(key: K, value: EstablishmentDraft[K]) => void
  setProvinceId: (value: string) => void
  setCategoryId: (value: string) => void
  setSubcategoryId: (value: string) => void
  setProductDescription: (value: string) => void
  setMonthlyVolume: (value: string) => void
  setUnit: (value: string) => void
  updateOwner: (key: keyof EstablishmentContactDraft, value: string) => void
  updateRepresentative: (key: keyof EstablishmentContactDraft, value: string) => void
}

function FormSections(props: FormSectionsProps) {
  const { tab, draft, options, update, isEditing } = props
  if (tab === 'general') {
    return (
      <section className="grid gap-4 md:grid-cols-2 lg:grid-cols-6">
        <Field label="Empresa" required className="lg:col-span-3">
          <select
            required
            disabled={isEditing}
            value={draft.companyId}
            onChange={(event) => {
              const companyId = event.target.value
              update('companyId', companyId)
              if (!draft.name.trim())
                update(
                  'name',
                  options.companies.find((value) => value.id === companyId)?.name ?? '',
                )
            }}
            className={inputClass}
          >
            <option value="">Seleccione</option>
            {options.companies.map((value) => (
              <option key={value.id} value={value.id}>
                {value.name} · {value.code}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Nombre o razón social" required className="lg:col-span-3">
          <input
            required
            maxLength={250}
            value={draft.name}
            onChange={(event) => update('name', event.target.value)}
            className={inputClass}
          />
        </Field>
        <Field label="Calle" className="lg:col-span-4">
          <input
            maxLength={250}
            value={draft.street ?? ''}
            onChange={(event) => update('street', event.target.value || null)}
            className={inputClass}
          />
        </Field>
        <Field label="Número" className="lg:col-span-2">
          <input
            maxLength={50}
            value={draft.addressNumber ?? ''}
            onChange={(event) => update('addressNumber', event.target.value || null)}
            className={inputClass}
          />
        </Field>
        <Field label="Provincia" required className="lg:col-span-2">
          <select
            required
            value={props.provinceId}
            onChange={(event) => {
              props.setProvinceId(event.target.value)
              update('municipalityId', null)
            }}
            className={inputClass}
          >
            <option value="">Seleccione</option>
            {options.provinces.map((value) => (
              <option key={value.id} value={value.id}>
                {value.name}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Municipio" required className="lg:col-span-2">
          <select
            required
            disabled={!props.provinceId}
            value={draft.municipalityId ?? ''}
            onChange={(event) => update('municipalityId', event.target.value || null)}
            className={inputClass}
          >
            <option value="">Seleccione</option>
            {props.municipalities.map((value) => (
              <option key={value.id} value={value.id}>
                {value.name}
              </option>
            ))}
          </select>
        </Field>
        <Field label="DPS/DAS" className="lg:col-span-2">
          <select
            value={draft.dpsDasId ?? ''}
            onChange={(event) => update('dpsDasId', event.target.value || null)}
            className={inputClass}
          >
            <option value="">Seleccione</option>
            {options.dpsDas.map((value) => (
              <option key={value.id} value={value.id}>
                {value.name}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Teléfono" className="lg:col-span-3">
          <input
            type="tel"
            inputMode="numeric"
            pattern="(809|829|849)-[0-9]{3}-[0-9]{4}"
            title="Use 10 dígitos y un prefijo 809, 829 o 849."
            maxLength={12}
            placeholder="809-555-1234"
            value={draft.phone ?? ''}
            onChange={(event) => update('phone', formatPhone(event.target.value) || null)}
            className={inputClass}
          />
        </Field>
        <Field label="Correo electrónico" className="lg:col-span-3">
          <input
            type="email"
            maxLength={320}
            value={draft.email ?? ''}
            onChange={(event) => update('email', event.target.value || null)}
            className={inputClass}
          />
        </Field>
      </section>
    )
  }

  if (tab === 'production') {
    return (
      <section className="grid gap-4 md:grid-cols-2 lg:grid-cols-6">
        <Field label="Fecha de inicio de operaciones" className="lg:col-span-2">
          <input
            type="date"
            value={dateValue(draft.operationsStartDate)}
            onChange={(event) => update('operationsStartDate', event.target.value || null)}
            className={inputClass}
          />
        </Field>
        <Field label="No. del Permiso Sanitario" className="lg:col-span-2">
          <input
            maxLength={100}
            value={draft.sanitaryPermitNumber ?? ''}
            onChange={(event) => update('sanitaryPermitNumber', event.target.value || null)}
            className={inputClass}
          />
        </Field>
        <Field label="Vencimiento del permiso" className="lg:col-span-2">
          <input
            type="date"
            value={dateValue(draft.sanitaryPermitExpiresAt)}
            onChange={(event) => update('sanitaryPermitExpiresAt', event.target.value || null)}
            className={inputClass}
          />
        </Field>
        <Field label="Productos elaborados" className="lg:col-span-4">
          <textarea
            rows={3}
            value={draft.productsDescription ?? ''}
            onChange={(event) => update('productsDescription', event.target.value || null)}
            className={inputClass}
          />
        </Field>
        <Field label="Producción anual" required className="lg:col-span-2">
          <input
            required
            type="number"
            min="0"
            step="0.0001"
            value={draft.annualProduction ?? ''}
            onChange={(event) => update('annualProduction', numberValue(event.target.value))}
            className={inputClass}
          />
        </Field>
        <Field label="Comercialización" className="lg:col-span-3">
          <select
            value={draft.commercializationId ?? ''}
            onChange={(event) => update('commercializationId', event.target.value || null)}
            className={inputClass}
          >
            <option value="">Seleccione</option>
            {options.commercializations.map((value) => (
              <option key={value.id} value={value.id}>
                {value.name}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Mercado objetivo" className="lg:col-span-3">
          <select
            value={draft.marketIds[0] ?? ''}
            onChange={(event) =>
              update('marketIds', event.target.value ? [event.target.value] : [])
            }
            className={inputClass}
          >
            <option value="">Seleccione</option>
            {options.markets.map((value) => (
              <option key={value.id} value={value.id}>
                {value.name}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Categoría de alimento" required className="lg:col-span-3">
          <select
            required
            value={props.categoryId}
            onChange={(event) => {
              props.setCategoryId(event.target.value)
              props.setSubcategoryId('')
            }}
            className={inputClass}
          >
            <option value="">Seleccione</option>
            {options.categories.map((value) => (
              <option key={value.id} value={value.id}>
                {value.name}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Subcategoría" required className="lg:col-span-3">
          <select
            required
            disabled={!props.categoryId}
            value={props.subcategoryId}
            onChange={(event) => props.setSubcategoryId(event.target.value)}
            className={inputClass}
          >
            <option value="">Seleccione</option>
            {props.subcategories.map((value) => (
              <option key={value.id} value={value.id}>
                {value.name}
                {value.riskLevel ? ` · Riesgo ${value.riskLevel}` : ' · Riesgo pendiente'}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Descripción del producto" required className="lg:col-span-3">
          <input
            required
            maxLength={300}
            value={props.productDescription}
            onChange={(event) => props.setProductDescription(event.target.value)}
            className={inputClass}
          />
        </Field>
        <Field label="Volumen mensual" className="lg:col-span-2">
          <input
            type="number"
            min="0"
            step="0.0001"
            value={props.monthlyVolume}
            onChange={(event) => props.setMonthlyVolume(event.target.value)}
            className={inputClass}
          />
        </Field>
        <Field label="Unidad" className="lg:col-span-1">
          <input
            maxLength={30}
            value={props.unit}
            onChange={(event) => props.setUnit(event.target.value)}
            className={inputClass}
          />
        </Field>
      </section>
    )
  }

  if (tab === 'controls') {
    return (
      <section className="grid gap-5 md:grid-cols-2">
        <RiskBooleanCard
          label="¿Tienen implementado el sistema HACCP?"
          value={draft.haccpImplemented}
          onChange={(value) => {
            update('haccpImplemented', value)
            update('haccpPercentage', null)
          }}
        >
          <select
            required
            disabled={draft.haccpImplemented !== true}
            value={draft.haccpPercentage ?? ''}
            onChange={(event) => update('haccpPercentage', numberValue(event.target.value))}
            className={inputClass}
          >
            <option value="">Nivel de implementación</option>
            {options.haccpLevels.map((value) => (
              <option key={value.parametersId} value={value.numericData ?? ''}>
                {value.stringData}
              </option>
            ))}
          </select>
        </RiskBooleanCard>
        <RiskBooleanCard
          label="¿Tienen un plan de muestreo microbiológico?"
          value={draft.microbiologicalSamplingPlan}
          onChange={(value) => {
            update('microbiologicalSamplingPlan', value)
            update('samplingApplicationCode', null)
          }}
        >
          <select
            required
            disabled={draft.microbiologicalSamplingPlan !== true}
            value={draft.samplingApplicationCode ?? ''}
            onChange={(event) => update('samplingApplicationCode', event.target.value || null)}
            className={inputClass}
          >
            <option value="">¿Dónde lo aplican?</option>
            {options.samplingApplications.map((value) => (
              <option key={value.parametersId} value={value.cCode ?? ''}>
                {value.stringData}
              </option>
            ))}
          </select>
        </RiskBooleanCard>
        <RiskBooleanCard
          label="¿Son suplidores del INABIE?"
          value={draft.isInabieSupplier}
          onChange={(value) => {
            update('isInabieSupplier', value)
            update('inabieDistributionCode', null)
          }}
        >
          <select
            required
            disabled={draft.isInabieSupplier !== true}
            value={draft.inabieDistributionCode ?? ''}
            onChange={(event) => update('inabieDistributionCode', event.target.value || null)}
            className={inputClass}
          >
            <option value="">¿Cómo lo distribuyen?</option>
            {options.inabieDistributions.map((value) => (
              <option key={value.parametersId} value={value.cCode ?? ''}>
                {value.stringData}
              </option>
            ))}
          </select>
        </RiskBooleanCard>
        <div className="grid gap-4 rounded-2xl border border-slate-100 bg-slate-50 p-4 sm:grid-cols-2">
          <Field label="Empleados mujeres">
            <input
              type="number"
              min="0"
              value={draft.femaleEmployees ?? ''}
              onChange={(event) => update('femaleEmployees', numberValue(event.target.value))}
              className={inputClass}
            />
          </Field>
          <Field label="Empleados hombres">
            <input
              type="number"
              min="0"
              value={draft.maleEmployees ?? ''}
              onChange={(event) => update('maleEmployees', numberValue(event.target.value))}
              className={inputClass}
            />
          </Field>
        </div>
        <Field
          label="Rechazos microbiológicos en los últimos 5 años"
          required
          className="md:col-span-2"
        >
          <input
            required
            type="number"
            min="0"
            step="1"
            value={draft.microbiologicalRejectionsLastFiveYears}
            onChange={(event) =>
              update('microbiologicalRejectionsLastFiveYears', Number(event.target.value))
            }
            className={inputClass}
          />
        </Field>
        <Field label="Estado" required className="md:col-span-2">
          <select
            required
            value={draft.status}
            onChange={(event) => update('status', event.target.value)}
            className={inputClass}
          >
            <option value="">Seleccione</option>
            {options.statuses.map((value) => (
              <option key={value.parametersId} value={value.stringData ?? ''}>
                {formatStatusLabel(value.stringData ?? '')}
              </option>
            ))}
          </select>
        </Field>
      </section>
    )
  }

  return (
    <section className="grid gap-5 lg:grid-cols-2">
      <ContactFields title="Propietario" value={props.owner} onChange={props.updateOwner} />
      <ContactFields
        title="Representante legal"
        value={props.representative}
        onChange={props.updateRepresentative}
      />
    </section>
  )
}

function Field({
  label,
  required = false,
  className = '',
  children,
}: {
  label: string
  required?: boolean
  className?: string
  children: ReactNode
}) {
  return (
    <label className={`block text-xs font-bold text-ink-strong ${className}`}>
      {label}
      {required && <span className="text-red-600"> *</span>}
      {children}
    </label>
  )
}

function RiskBooleanCard({
  label,
  value,
  onChange,
  children,
}: {
  label: string
  value: boolean | null
  onChange: (value: boolean) => void
  children: ReactNode
}) {
  return (
    <div className="rounded-2xl border border-slate-100 bg-slate-50 p-4">
      <label className="text-sm font-bold text-ink-strong">
        {label} <span className="text-red-600">*</span>
        <select
          required
          value={value === null ? '' : String(value)}
          onChange={(event) => onChange(event.target.value === 'true')}
          className={inputClass}
        >
          <option value="">Seleccione</option>
          <option value="true">Sí</option>
          <option value="false">No</option>
        </select>
      </label>
      {value === true && children}
    </div>
  )
}

function ContactFields({
  title,
  value,
  onChange,
}: {
  title: string
  value: EstablishmentContactDraft
  onChange: (key: keyof EstablishmentContactDraft, value: string) => void
}) {
  return (
    <fieldset className="grid gap-4 rounded-2xl border border-slate-100 p-5">
      <legend className="text-brand-800 px-2 text-sm font-extrabold">{title}</legend>
      <Field label="Nombre completo">
        <input
          maxLength={250}
          value={value.fullName}
          onChange={(event) => onChange('fullName', event.target.value)}
          className={inputClass}
        />
      </Field>
      <Field label="Cédula de identidad">
        <input
          inputMode="numeric"
          maxLength={13}
          placeholder="001-0000000-1"
          value={value.identification ?? ''}
          onChange={(event) => onChange('identification', formatCedula(event.target.value))}
          className={inputClass}
        />
      </Field>
      <Field label="Teléfono celular">
        <input
          type="tel"
          inputMode="numeric"
          pattern="(809|829|849)-[0-9]{3}-[0-9]{4}"
          title="Use 10 dígitos y un prefijo 809, 829 o 849."
          maxLength={12}
          placeholder="809-555-1234"
          value={value.phone ?? ''}
          onChange={(event) => onChange('phone', formatPhone(event.target.value))}
          className={inputClass}
        />
      </Field>
      <Field label="Correo electrónico">
        <input
          type="email"
          maxLength={320}
          value={value.email ?? ''}
          onChange={(event) => onChange('email', event.target.value)}
          className={inputClass}
        />
      </Field>
    </fieldset>
  )
}

function maskContact(contact: EstablishmentContactDraft): EstablishmentContactDraft {
  return {
    ...contact,
    identification: contact.identification ? formatCedula(contact.identification) : null,
    phone: contact.phone ? formatPhone(contact.phone) : null,
  }
}

function CatalogNotice({ names }: { names: string[] }) {
  return (
    <div
      role="alert"
      className="mb-5 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900"
    >
      No se puede completar el registro porque faltan datos en los catálogos de {names.join(', ')}.
    </div>
  )
}

function numberValue(value: string) {
  return value === '' ? null : Number(value)
}

function sourceProductsAfterFirst(products: EstablishmentDraft['products']) {
  return products.length > 1 ? products.slice(1) : []
}

function dateValue(value: string | null) {
  return value ? value.slice(0, 10) : ''
}
