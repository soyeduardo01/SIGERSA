import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { DashboardOverview } from './DashboardOverview'

describe('DashboardOverview', () => {
  it('presenta el resumen operativo sin registrar establecimientos desde el panel', () => {
    render(<DashboardOverview />)

    expect(screen.getByRole('heading', { name: 'Resumen' })).toBeVisible()
    expect(screen.getByText('Casos activos')).toBeVisible()
    expect(screen.getByRole('table')).toBeVisible()

    expect(
      screen.queryByRole('button', { name: /Registrar establecimiento/ }),
    ).not.toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: 'Período del gráfico' })).not.toBeInTheDocument()
  })
})
