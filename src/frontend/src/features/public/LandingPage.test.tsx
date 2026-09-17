import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { LandingPage } from './LandingPage'
import { publicComplaintTypes } from './complaintTypes'

vi.mock('../../lib/api', () => ({
  getPublicComplaintOptions: vi.fn(async () => []),
  createPublicComplaint: vi.fn(),
}))

describe('LandingPage', () => {
  it('presenta el tipo de denuncia como un catálogo desplegable dentro del flujo ciudadano', () => {
    render(<LandingPage />)
    fireEvent.click(screen.getAllByRole('button', { name: /reportar una denuncia/i })[0])

    const complaintType = screen.getByLabelText('Tipo de denuncia')
    expect(complaintType.tagName).toBe('SELECT')
    for (const type of publicComplaintTypes) {
      expect(screen.getByRole('option', { name: type })).toBeInTheDocument()
    }
  })
})
