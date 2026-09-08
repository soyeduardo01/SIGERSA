import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { RiskBadge } from './RiskBadge'

describe('RiskBadge', () => {
  it('muestra el nivel de riesgo con un texto accesible', () => {
    render(<RiskBadge level="Alto" />)

    expect(screen.getByText('Riesgo alto')).toBeInTheDocument()
  })
})
