import { fireEvent, render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { DashboardOverview } from './DashboardOverview'

describe('DashboardOverview', () => {
  it('presenta el resumen operativo y abre el formulario de establecimiento', () => {
    render(<DashboardOverview />)

    expect(screen.getByRole('heading', { name: 'Resumen' })).toBeVisible()
    expect(screen.getByText('Casos activos')).toBeVisible()
    expect(screen.getByRole('table')).toBeVisible()

    fireEvent.click(screen.getByRole('button', { name: /Registrar establecimiento/ }))

    const dialog = screen.getByRole('dialog', { name: 'Registrar establecimiento' })
    expect(dialog).toBeVisible()
    expect(within(dialog).getAllByRole('combobox')).toHaveLength(5)
    within(dialog).getAllByRole('combobox').forEach((select) => {
      expect(select).toHaveClass('appearance-none')
    })
    const observations = screen.getByPlaceholderText(
      'Información adicional sobre el establecimiento...',
    )
    fireEvent.change(observations, { target: { value: 'Nota de prueba' } })
    expect(screen.getByText('14/500')).toBeVisible()
  })

  it('permite cambiar a documentación y alternar el estado', () => {
    render(<DashboardOverview />)
    fireEvent.click(screen.getByRole('button', { name: /Registrar establecimiento/ }))
    fireEvent.click(screen.getByRole('tab', { name: 'Documentación y estado' }))

    const statusSwitch = screen.getByRole('switch')
    expect(statusSwitch).toHaveAttribute('aria-checked', 'true')
    fireEvent.click(statusSwitch)
    expect(statusSwitch).toHaveAttribute('aria-checked', 'false')
    expect(screen.getByText('Arrastre y suelte archivos aquí')).toBeVisible()
  })
})
