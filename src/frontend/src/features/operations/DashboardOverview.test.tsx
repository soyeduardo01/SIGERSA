import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { DashboardOverview } from './DashboardOverview'

const authState = vi.hoisted(() => ({ roles: ['ADMINISTRADOR'] }))

vi.mock('../../contexts/useAuth', () => ({
  useAuth: () => ({ roles: authState.roles }),
}))

vi.mock('../../lib/api', () => ({
  getDashboard: vi.fn(async () => ({
    activeCases: 1,
    pendingEvaluations: 2,
    criticalAlerts: 3,
    averageCompliance: 88,
    myRequests: 4,
    unreadNotifications: 5,
    scheduledEvaluations: 6,
    openComplaints: 7,
    pendingAssignments: 8,
    pendingReports: 9,
    recentEvaluations: [],
    riskDistribution: [],
    upcomingSchedules: [],
  })),
}))

describe('DashboardOverview por rol', () => {
  beforeEach(() => {
    authState.roles = ['ADMINISTRADOR']
  })

  it('presenta al administrador únicamente accesos permitidos y datos reales', () => {
    render(<DashboardOverview />)

    expect(screen.getByRole('heading', { name: 'Resumen operativo' })).toBeVisible()
    expect(screen.getByText('Casos activos')).toBeVisible()
    expect(screen.getByText('Evaluaciones pendientes')).toBeVisible()
    expect(screen.getByText('Alertas registradas')).toBeVisible()
    expect(screen.getByText('Denuncias registradas')).toBeVisible()
    expect(screen.getByRole('table')).toBeVisible()
    expect(screen.queryByText(/Vista demostrativa/)).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /Casos activos/ })).not.toBeInTheDocument()
  })

  it('no muestra solicitudes ni evaluaciones al administrador de empresa', () => {
    authState.roles = ['ADMINISTRADOR_EMPRESA']
    render(<DashboardOverview />)

    expect(screen.getByText('Usuarios de empresa')).toBeVisible()
    expect(screen.getByText('Notificaciones')).toBeVisible()
    expect(screen.queryByText('Nueva Solicitud BPM')).not.toBeInTheDocument()
    expect(screen.queryByText('Mis Solicitudes')).not.toBeInTheDocument()
    expect(screen.queryByText('Evaluaciones')).not.toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('muestra al delegado solicitudes propias, pero no el módulo de evaluaciones', () => {
    authState.roles = ['USUARIO_DELEGADO']
    render(<DashboardOverview />)

    expect(screen.getByText('Nueva Solicitud BPM')).toBeVisible()
    expect(screen.getByText('Mis Solicitudes')).toBeVisible()
    expect(screen.getByText('Notificaciones')).toBeVisible()
    expect(screen.queryByText('Evaluaciones')).not.toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('muestra al coordinador alertas, denuncias, casos y agenda', () => {
    authState.roles = ['COORDINADOR']
    render(<DashboardOverview />)

    expect(screen.getByText('Casos pendientes')).toBeVisible()
    expect(screen.getByText('Alertas LAPCH')).toBeVisible()
    expect(screen.getByText('Denuncias')).toBeVisible()
    expect(screen.getByLabelText('Próximas programaciones')).toBeVisible()
    expect(screen.queryByText('Nueva Solicitud BPM')).not.toBeInTheDocument()
  })

  it('limita al técnico a evaluaciones, calendario e informes', () => {
    authState.roles = ['TECNICO_EVALUADOR']
    render(<DashboardOverview />)

    expect(screen.getByText('Evaluaciones asignadas')).toBeVisible()
    expect(screen.getByText('Calendario')).toBeVisible()
    expect(screen.getByText('Pendientes de informe')).toBeVisible()
    expect(screen.queryByText('Alertas LAPCH')).not.toBeInTheDocument()
    expect(screen.queryByText('Denuncias')).not.toBeInTheDocument()
  })
})
