import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { DashboardOverview } from './DashboardOverview'

vi.mock('../../contexts/useAuth', () => ({
  useAuth: () => ({ roles: ['ADMINISTRADOR'] }),
}))

vi.mock('../../lib/api', () => ({
  getDashboard: vi.fn(async () => ({
    activeCases: 0,
    pendingEvaluations: 0,
    criticalAlerts: 0,
    averageCompliance: null,
    myRequests: 0,
    unreadNotifications: 0,
    scheduledEvaluations: 0,
    openComplaints: 0,
    pendingAssignments: 0,
    pendingReports: 0,
    recentEvaluations: [],
    riskDistribution: [],
    upcomingSchedules: [],
  })),
}))

describe('DashboardOverview', () => {
  it('presenta el resumen operativo sin registrar establecimientos desde el panel', () => {
    render(<DashboardOverview />)

    expect(screen.getByRole('heading', { name: 'Resumen operativo' })).toBeVisible()
    expect(screen.getByText('Casos activos')).toBeVisible()
    expect(screen.getByRole('table')).toBeVisible()
    expect(screen.getByLabelText('Gráfico de actividad mensual')).toBeVisible()
    expect(screen.getByRole('img', { name: /Gráfico circular de riesgo/ })).toBeVisible()

    expect(
      screen.queryByRole('button', { name: /Registrar establecimiento/ }),
    ).not.toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: 'Período del gráfico' })).not.toBeInTheDocument()
  })
})
