import { useEffect, useState, type FormEvent } from 'react'
import { alerts } from '../../lib/alerts'
import { createPublicComplaint, getPublicComplaintOptions } from '../../lib/api'
import { loginHref } from '../../lib/navigation'

export function LandingPage() {
  const [options, setOptions] = useState<Array<{ id: string; name: string }>>([])
  const [establishmentId, setEstablishmentId] = useState('')
  const [complaintType, setComplaintType] = useState('Condición sanitaria')
  const [description, setDescription] = useState('')
  const [isConfidential, setIsConfidential] = useState(true)
  const [sending, setSending] = useState(false)

  useEffect(() => {
    void getPublicComplaintOptions().then(setOptions).catch(() => setOptions([]))
  }, [])

  async function submit(event: FormEvent) {
    event.preventDefault()
    setSending(true)
    try {
      await createPublicComplaint({ establishmentId, complaintType, description, isConfidential })
      setEstablishmentId('')
      setDescription('')
      await alerts.success(
        'Denuncia recibida',
        'Fue asignada directamente a un coordinador de DIGEMAPS para su revisión.',
      )
    } catch (error) {
      await alerts.error(error, 'No se pudo enviar la denuncia')
    } finally {
      setSending(false)
    }
  }

  return (
    <main className="min-h-dvh bg-surface-canvas text-ink-body">
      <header className="bg-brand-900 text-white">
        <nav className="mx-auto flex max-w-6xl items-center justify-between px-5 py-5">
          <img src="/assets/logo-white.png" alt="SIGERSA" className="h-10 w-auto" />
          <a href={loginHref} className="rounded-xl border border-white/40 px-4 py-2 text-sm font-bold">
            Acceso institucional
          </a>
        </nav>
        <section className="mx-auto grid max-w-6xl gap-10 px-5 py-16 lg:grid-cols-[1.15fr_.85fr] lg:items-center">
          <div>
            <p className="text-xs font-bold tracking-[0.18em] text-lime-300 uppercase">DIGEMAPS · República Dominicana</p>
            <h1 className="mt-5 max-w-3xl text-4xl font-black tracking-tight sm:text-6xl">
              Seguridad alimentaria con trazabilidad y respuesta oportuna.
            </h1>
            <p className="mt-6 max-w-2xl text-lg leading-8 text-emerald-50/85">
              SIGERSA integra inspecciones, evaluación de riesgos y seguimiento sanitario para proteger a la ciudadanía.
            </p>
          </div>
          <div className="rounded-3xl border border-white/15 bg-white/10 p-6 backdrop-blur">
            <p className="text-sm font-bold text-lime-300">Canal ciudadano</p>
            <h2 className="mt-2 text-2xl font-extrabold">¿Observaste un riesgo sanitario?</h2>
            <p className="mt-3 text-sm leading-6 text-emerald-50/80">Envía una denuncia sin crear una cuenta. La recibirá un coordinador.</p>
            <a href="#denuncia" className="mt-6 inline-flex rounded-xl bg-lime-300 px-5 py-3 font-extrabold text-brand-950">Reportar ahora</a>
          </div>
        </section>
      </header>

      <section className="mx-auto grid max-w-6xl gap-6 px-5 py-14 md:grid-cols-3">
        {[
          ['Evaluación basada en riesgo', 'Priorización objetiva de inspecciones y controles sanitarios.'],
          ['Seguimiento verificable', 'Trazabilidad de hallazgos, evidencias, correcciones e informes.'],
          ['Servicio disponible', 'Capacidades PWA y trabajo sin conexión para operaciones de campo.'],
        ].map(([title, text]) => (
          <article key={title} className="rounded-2xl bg-white p-6 shadow-card">
            <h2 className="font-extrabold text-ink-strong">{title}</h2>
            <p className="mt-3 text-sm leading-6 text-ink-muted">{text}</p>
          </article>
        ))}
      </section>

      <section id="denuncia" className="mx-auto max-w-3xl px-5 pb-20">
        <form onSubmit={(event) => void submit(event)} className="rounded-3xl bg-white p-6 shadow-card sm:p-9">
          <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">Formulario público</p>
          <h2 className="mt-2 text-3xl font-black text-ink-strong">Enviar denuncia sanitaria</h2>
          <div className="mt-7 grid gap-5">
            <label className="text-sm font-bold">
              Establecimiento
              <select required value={establishmentId} onChange={(e) => setEstablishmentId(e.target.value)} className="mt-2 min-h-12 w-full rounded-xl border border-slate-300 px-3 font-normal">
                <option value="">Seleccione</option>
                {options.map((option) => <option key={option.id} value={option.id}>{option.name}</option>)}
              </select>
            </label>
            <label className="text-sm font-bold">
              Tipo de denuncia
              <input required maxLength={100} value={complaintType} onChange={(e) => setComplaintType(e.target.value)} className="mt-2 min-h-12 w-full rounded-xl border border-slate-300 px-3 font-normal" />
            </label>
            <label className="text-sm font-bold">
              Descripción
              <textarea required minLength={20} maxLength={4000} rows={6} value={description} onChange={(e) => setDescription(e.target.value)} className="mt-2 w-full rounded-xl border border-slate-300 p-3 font-normal" />
            </label>
            <label className="flex items-start gap-3 text-sm">
              <input type="checkbox" checked={isConfidential} onChange={(e) => setIsConfidential(e.target.checked)} className="mt-1 size-4" />
              Mantener la denuncia con tratamiento confidencial.
            </label>
            <button disabled={sending} className="min-h-12 rounded-xl bg-brand-700 px-5 font-extrabold text-white disabled:opacity-50">{sending ? 'Enviando…' : 'Enviar denuncia'}</button>
          </div>
        </form>
      </section>
    </main>
  )
}
