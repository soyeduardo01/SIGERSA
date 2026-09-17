import { useEffect, useState, type FormEvent } from 'react'
import { InstallPwaButton } from '../../components/pwa/InstallPwaButton'
import { alerts } from '../../lib/alerts'
import { createPublicComplaint, getPublicComplaintOptions } from '../../lib/api'
import { loginHref } from '../../lib/navigation'
import { publicComplaintTypes } from './complaintTypes'

const operations = [
  ['01', 'Solicitudes', 'Gestiona solicitudes de inspección con trazabilidad completa.'],
  ['02', 'Alertas y denuncias', 'Centraliza señales sanitarias y reportes ciudadanos.'],
  ['03', 'Evaluaciones BPM', 'Digitaliza inspecciones y calcula el nivel de cumplimiento.'],
  ['04', 'Programación', 'Coordina visitas, equipos técnicos y prioridades operativas.'],
  ['05', 'Hallazgos', 'Registra no conformidades y su nivel de criticidad.'],
  ['06', 'Evidencias', 'Conserva fotografías y documentos vinculados al expediente.'],
  ['07', 'Correcciones', 'Da seguimiento a medidas correctivas y compromisos.'],
  ['08', 'Informes oficiales', 'Genera documentos verificables para cada evaluación.'],
  ['09', 'Gestión empresarial', 'Conecta establecimientos, responsables y solicitudes.'],
  ['10', 'Indicadores', 'Convierte la operación sanitaria en decisiones oportunas.'],
]

const Arrow = () => <span aria-hidden="true">→</span>

export function LandingPage() {
  const [options, setOptions] = useState<Array<{ id: string; name: string }>>([])
  const [establishmentId, setEstablishmentId] = useState('')
  const [complaintType, setComplaintType] = useState('')
  const [description, setDescription] = useState('')
  const [isConfidential, setIsConfidential] = useState(true)
  const [sending, setSending] = useState(false)
  const [dialogOpen, setDialogOpen] = useState(false)
  const [step, setStep] = useState(1)

  useEffect(() => {
    void getPublicComplaintOptions()
      .then(setOptions)
      .catch(() => setOptions([]))
  }, [])

  useEffect(() => {
    if (!dialogOpen) return
    const close = (event: KeyboardEvent) => event.key === 'Escape' && setDialogOpen(false)
    document.body.style.overflow = 'hidden'
    window.addEventListener('keydown', close)
    return () => {
      document.body.style.overflow = ''
      window.removeEventListener('keydown', close)
    }
  }, [dialogOpen])

  function openComplaint() {
    setStep(1)
    setDialogOpen(true)
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (step < 3) {
      setStep((current) => current + 1)
      return
    }
    setSending(true)
    try {
      await createPublicComplaint({ establishmentId, complaintType, description, isConfidential })
      setEstablishmentId('')
      setComplaintType('')
      setDescription('')
      setDialogOpen(false)
      await alerts.success(
        'Denuncia recibida',
        'Fue asignada a un coordinador de DIGEMAPS para su revisión.',
      )
    } catch (error) {
      await alerts.error(error, 'No se pudo enviar la denuncia')
    } finally {
      setSending(false)
    }
  }

  return (
    <main className="landing min-h-dvh overflow-hidden bg-[#f7faf7] text-[#18342b]">
      <section className="landing-hero relative text-white">
        <div className="landing-orb landing-orb-one" />
        <div className="landing-orb landing-orb-two" />
        <nav
          aria-label="Navegación principal"
          className="relative z-20 mx-auto flex max-w-7xl items-center justify-between px-5 py-5 lg:px-8"
        >
          <a href="#inicio" aria-label="SIGERSA, inicio">
            <img src="/assets/logo-white.png" alt="SIGERSA" className="h-10 w-auto sm:h-12" />
          </a>
          <div className="hidden items-center gap-8 text-sm font-semibold text-emerald-50/80 lg:flex">
            <a href="#soluciones" className="landing-nav-link">
              Soluciones
            </a>
            <a href="#impacto" className="landing-nav-link">
              Nuestro impacto
            </a>
            <a href="#plataforma" className="landing-nav-link">
              La plataforma
            </a>
            <a href="#aliados" className="landing-nav-link">
              Aliados
            </a>
          </div>
          <div className="flex items-center gap-2">
            <InstallPwaButton className="hidden rounded-full border border-white/20 px-4 py-2.5 text-sm font-bold text-white transition hover:bg-white/10 sm:inline-flex" />
            <a
              href={loginHref}
              className="rounded-full bg-[#b8ee45] px-4 py-2.5 text-sm font-black text-[#083e30] shadow-lg transition hover:-translate-y-0.5 hover:bg-white sm:px-5"
            >
              Acceso institucional
            </a>
          </div>
        </nav>

        <div
          id="inicio"
          className="relative z-10 mx-auto grid max-w-[90rem] gap-12 px-5 pt-16 pb-32 lg:grid-cols-[.92fr_1.08fr] lg:items-center lg:px-8 lg:pt-24 lg:pb-44"
        >
          <div className="landing-reveal">
            <p className="inline-flex items-center gap-2 rounded-full border border-emerald-300/25 bg-white/8 px-4 py-2 text-xs font-bold tracking-[.16em] text-emerald-100 uppercase backdrop-blur">
              <span className="size-2 animate-pulse rounded-full bg-[#b8ee45]" /> República
              Dominicana avanza
            </p>
            <h1 className="mt-7 max-w-3xl text-[clamp(3rem,7vw,6.8rem)] leading-[.9] font-black tracking-[-.055em]">
              Salud segura.
              <br />
              <span className="text-[#b8ee45]">Gestión inteligente.</span>
            </h1>
            <p className="mt-7 max-w-xl text-base leading-7 text-emerald-50/75 sm:text-lg">
              Un sistema integral que conecta inspecciones, riesgos y respuesta sanitaria para
              cuidar lo que llega a la mesa de cada dominicano.
            </p>
            <div className="mt-9 flex flex-wrap gap-3">
              <button type="button" onClick={openComplaint} className="landing-primary-button">
                Reportar una denuncia <Arrow />
              </button>
              <a href="#soluciones" className="landing-secondary-button">
                Conocer SIGERSA <span aria-hidden="true">↓</span>
              </a>
            </div>
            <div className="mt-10 flex flex-wrap items-center gap-x-8 gap-y-3 text-xs font-semibold text-emerald-100/70">
              <span>✓ Canal confidencial</span>
              <span>✓ Seguimiento institucional</span>
              <span>✓ Disponible 24/7</span>
            </div>
          </div>
          <div
            className="landing-hero-visual landing-reveal-delay"
            aria-label="Vistas de la plataforma SIGERSA"
          >
            <span className="landing-device-aura" aria-hidden="true" />
            <div className="landing-browser-card">
              <div className="flex items-center gap-1.5 border-b border-slate-200 bg-white px-4 py-3">
                <i />
                <i />
                <i />
              </div>
              <img src="/assets/dashboard-screen.png" alt="Dashboard operativo de SIGERSA" />
            </div>
            <div className="landing-phone-card">
              <img src="/assets/mobile-login-device.png" alt="SIGERSA en teléfono móvil" />
            </div>
            <div className="landing-float-note">
              <span className="landing-check">✓</span>
              <span>
                <b>Operación sincronizada</b>
                <small>Información disponible en tiempo real</small>
              </span>
            </div>
          </div>
        </div>
      </section>

      <section className="relative z-20 mx-auto -mt-16 grid max-w-6xl gap-3 px-5 md:grid-cols-3 lg:px-8">
        {[
          ['◎', 'Prevención basada en riesgo', 'Prioriza donde más importa.'],
          ['⌁', 'Trazabilidad de principio a fin', 'Cada decisión queda respaldada.'],
          ['◉', 'Respuesta ciudadana directa', 'Un canal sencillo y confiable.'],
        ].map(([icon, title, text], index) => (
          <article
            key={title}
            className="landing-pillar"
            style={{ animationDelay: `${index * 120}ms` }}
          >
            <span className="landing-pillar-icon">{icon}</span>
            <div>
              <h2>{title}</h2>
              <p>{text}</p>
            </div>
          </article>
        ))}
      </section>

      <section id="soluciones" className="mx-auto max-w-7xl px-5 py-24 lg:px-8 lg:py-32">
        <div className="max-w-3xl">
          <p className="landing-eyebrow">Una operación integral</p>
          <h2 className="landing-heading">
            Todo el ciclo sanitario,
            <br />
            <span>en un mismo lugar.</span>
          </h2>
          <p className="landing-lead">
            SIGERSA reúne las herramientas que los equipos necesitan para actuar con criterio,
            velocidad y evidencia.
          </p>
        </div>
        <div className="mt-14 grid gap-px overflow-hidden rounded-[2rem] border border-emerald-950/10 bg-emerald-950/10 md:grid-cols-2 lg:grid-cols-5">
          {operations.map(([number, title, text]) => (
            <article key={number} className="landing-operation-card">
              <span>{number}</span>
              <h3>{title}</h3>
              <p>{text}</p>
              <i aria-hidden="true">↗</i>
            </article>
          ))}
        </div>
      </section>

      <section id="impacto" className="landing-impact px-5 py-24 text-white lg:px-8 lg:py-32">
        <div className="mx-auto max-w-7xl">
          <div className="grid gap-10 lg:grid-cols-[.8fr_1.2fr] lg:items-end">
            <div>
              <p className="landing-eyebrow !text-[#b8ee45]">Diseñado para generar impacto</p>
              <h2 className="text-4xl font-black tracking-tight sm:text-6xl">
                Mejores datos.
                <br />
                Mejores decisiones.
              </h2>
            </div>
            <p className="max-w-xl text-lg leading-8 text-emerald-50/65 lg:justify-self-end">
              De la primera señal al informe oficial, la información fluye sin perder contexto,
              responsables ni evidencia.
            </p>
          </div>
          <div className="mt-16 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {[
              ['360°', 'Visibilidad del proceso'],
              ['10', 'Módulos conectados'],
              ['24/7', 'Canal ciudadano'],
              ['1', 'Expediente trazable'],
            ].map(([value, label]) => (
              <article key={label} className="landing-stat">
                <b>{value}</b>
                <span>{label}</span>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section id="plataforma" className="relative mx-auto max-w-7xl px-5 py-24 lg:px-8 lg:py-36">
        <div className="mx-auto max-w-3xl text-center">
          <p className="landing-eyebrow">En oficina y en campo</p>
          <h2 className="landing-heading">
            Una experiencia preparada
            <br />
            <span>para cada pantalla.</span>
          </h2>
          <p className="landing-lead mx-auto">
            Responsiva, instalable y diseñada para acompañar el trabajo sanitario donde ocurra.
          </p>
        </div>
        <div className="landing-device-stage mt-16">
          <div className="landing-laptop">
            <div className="landing-laptop-screen">
              <img src="/assets/dashboard-screen.png" alt="Dashboard SIGERSA en computadora" />
            </div>
            <div className="landing-laptop-base" />
          </div>
          <div className="landing-tablet">
            <img
              src="/assets/inspection-screen.png"
              alt="Evaluación de inspección SIGERSA en tableta"
            />
          </div>
          <img
            className="landing-mobile-device"
            src="/assets/mobile-login-device.png"
            alt="Acceso SIGERSA desde un móvil"
          />
          <span className="landing-device-badge badge-one">Gestión BPM</span>
          <span className="landing-device-badge badge-two">Gestión de Alertas LAPCH</span>
        </div>
      </section>

      <section className="mx-4 rounded-[2.5rem] bg-[#dfff9a] px-5 py-16 sm:mx-6 lg:mx-auto lg:max-w-7xl lg:px-16">
        <div className="grid gap-10 lg:grid-cols-[1fr_auto] lg:items-center">
          <div>
            <p className="text-xs font-black tracking-[.18em] text-emerald-900/60 uppercase">
              Participación ciudadana
            </p>
            <h2 className="mt-4 text-4xl font-black tracking-tight text-[#073f30] sm:text-6xl">
              Tu alerta también protege.
            </h2>
            <p className="mt-5 max-w-2xl text-lg text-emerald-950/70">
              Reporta de forma segura cualquier situación que pueda representar un riesgo para la
              salud.
            </p>
          </div>
          <button
            type="button"
            onClick={openComplaint}
            className="rounded-full bg-[#073f30] px-7 py-4 font-black text-white shadow-xl transition hover:-translate-y-1 hover:bg-[#0c5c45]"
          >
            Iniciar denuncia <Arrow />
          </button>
        </div>
      </section>

      <section id="aliados" className="mx-auto max-w-7xl px-5 py-24 text-center lg:px-8">
        <p className="landing-eyebrow">Respaldo institucional</p>
        <h2 className="text-3xl font-black tracking-tight text-[#0b4938]">
          Una plataforma al servicio del país.
        </h2>
        <div className="landing-allies mt-12 grid items-stretch gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <figure className="landing-ally-card">
            <img
              src="/assets/government-rd.png"
              alt="Gobierno de la República Dominicana"
              className="landing-ally-logo landing-ally-government"
            />
            <figcaption>Gobierno de la República Dominicana</figcaption>
          </figure>
          <figure className="landing-ally-card">
            <img
              src="/assets/ministerio-de-salud-publica.png"
              alt="Ministerio de Salud Pública"
              className="landing-ally-logo landing-ally-ministry"
            />
            <figcaption>Ministerio de Salud Pública</figcaption>
          </figure>
          <figure className="landing-ally-card">
            <img
              src="/assets/digemaps.png"
              alt="DIGEMAPS"
              className="landing-ally-logo landing-ally-digemaps"
            />
            <figcaption>DIGEMAPS</figcaption>
          </figure>
          <figure className="landing-ally-card">
            <img
              src="/assets/bpm-certificacion.png"
              alt="Certificación BPM"
              className="landing-ally-logo landing-ally-bpm"
            />
            <figcaption>Certificación BPM</figcaption>
          </figure>
        </div>
      </section>

      <footer className="bg-[#052f25] px-5 py-12 text-white lg:px-8">
        <div className="mx-auto flex max-w-7xl flex-col gap-8 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <img src="/assets/logo-white.png" alt="SIGERSA" className="h-11 w-auto" />
            <p className="mt-4 max-w-md text-sm leading-6 text-emerald-50/55">
              Sistema Integral de Gestión de Riesgo y Seguridad Alimentaria.
            </p>
          </div>
          <div className="text-sm text-emerald-50/55">
            <p>Dirección General de Medicamentos, Alimentos y Productos Sanitarios</p>
            <p className="mt-2">© {new Date().getFullYear()} República Dominicana</p>
          </div>
        </div>
      </footer>

      {dialogOpen && (
        <div
          className="sigersa-modal-overlay fixed inset-0 z-50 grid place-items-center p-3 sm:p-6"
          onMouseDown={(event) => event.target === event.currentTarget && setDialogOpen(false)}
        >
          <section
            role="dialog"
            aria-modal="true"
            aria-labelledby="complaint-title"
            className="sigersa-modal-panel w-full max-w-2xl p-5 sm:p-8"
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="landing-eyebrow">Canal ciudadano · Paso {step} de 3</p>
                <h2 id="complaint-title" className="mt-2 text-2xl font-black text-[#103d31]">
                  {step === 1
                    ? '¿Dónde ocurrió?'
                    : step === 2
                      ? 'Cuéntanos lo sucedido'
                      : 'Confirma tu denuncia'}
                </h2>
              </div>
              <button
                type="button"
                aria-label="Cerrar denuncia"
                onClick={() => setDialogOpen(false)}
                className="grid size-10 shrink-0 place-items-center rounded-full bg-slate-100 text-xl"
              >
                ×
              </button>
            </div>
            <div className="mt-5 grid grid-cols-3 gap-2" aria-label={`Paso ${step} de 3`}>
              {[1, 2, 3].map((item) => (
                <span
                  key={item}
                  className={`h-1.5 rounded-full ${item <= step ? 'bg-[#16835f]' : 'bg-slate-200'}`}
                />
              ))}
            </div>
            <form onSubmit={(event) => void submit(event)} className="mt-7">
              {step === 1 && (
                <div className="grid gap-5">
                  <label className="text-sm font-bold">
                    Establecimiento
                    <select
                      autoFocus
                      required
                      value={establishmentId}
                      onChange={(e) => setEstablishmentId(e.target.value)}
                      className="landing-field"
                    >
                      <option value="">Seleccione el establecimiento</option>
                      {options.map((option) => (
                        <option key={option.id} value={option.id}>
                          {option.name}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label className="text-sm font-bold">
                    Tipo de denuncia
                    <select
                      required
                      value={complaintType}
                      onChange={(e) => setComplaintType(e.target.value)}
                      className="landing-field"
                    >
                      <option value="">Seleccione el tipo</option>
                      {publicComplaintTypes.map((type) => (
                        <option key={type} value={type}>
                          {type}
                        </option>
                      ))}
                    </select>
                  </label>
                </div>
              )}
              {step === 2 && (
                <div className="grid gap-5">
                  <label className="text-sm font-bold">
                    Descripción de lo ocurrido
                    <textarea
                      autoFocus
                      required
                      minLength={20}
                      maxLength={4000}
                      rows={7}
                      value={description}
                      onChange={(e) => setDescription(e.target.value)}
                      placeholder="Incluye detalles que ayuden a identificar y verificar la situación."
                      className="landing-field resize-none"
                    />
                  </label>
                  <label className="flex items-start gap-3 rounded-2xl bg-emerald-50 p-4 text-sm">
                    <input
                      type="checkbox"
                      checked={isConfidential}
                      onChange={(e) => setIsConfidential(e.target.checked)}
                      className="mt-1 size-4 accent-[#16835f]"
                    />
                    <span>
                      <b className="block text-[#153e33]">Mantener tratamiento confidencial</b>
                      <small className="text-slate-600">
                        Tu reporte será gestionado por el personal autorizado.
                      </small>
                    </span>
                  </label>
                </div>
              )}
              {step === 3 && (
                <div className="space-y-4 rounded-2xl border border-emerald-900/10 bg-[#f5faf7] p-5 text-sm">
                  <p>
                    <b className="block text-xs tracking-wider text-emerald-700 uppercase">
                      Establecimiento
                    </b>
                    {options.find((option) => option.id === establishmentId)?.name}
                  </p>
                  <p>
                    <b className="block text-xs tracking-wider text-emerald-700 uppercase">Tipo</b>
                    {complaintType}
                  </p>
                  <p>
                    <b className="block text-xs tracking-wider text-emerald-700 uppercase">
                      Descripción
                    </b>
                    <span className="line-clamp-4">{description}</span>
                  </p>
                  <p className="font-bold text-emerald-800">
                    {isConfidential
                      ? '✓ Tratamiento confidencial solicitado'
                      : 'Reporte sin solicitud de confidencialidad'}
                  </p>
                </div>
              )}
              <div className="mt-7 flex items-center justify-between gap-3">
                <button
                  type="button"
                  onClick={() =>
                    step === 1 ? setDialogOpen(false) : setStep((current) => current - 1)
                  }
                  className="rounded-full px-5 py-3 font-bold text-slate-600 hover:bg-slate-100"
                >
                  {step === 1 ? 'Cancelar' : 'Atrás'}
                </button>
                <button
                  disabled={sending}
                  className="rounded-full bg-[#0b684e] px-6 py-3 font-black text-white shadow-lg transition hover:bg-[#084c3a] disabled:opacity-50"
                >
                  {sending ? 'Enviando…' : step === 3 ? 'Enviar denuncia' : 'Continuar'} <Arrow />
                </button>
              </div>
            </form>
          </section>
        </div>
      )}
    </main>
  )
}
