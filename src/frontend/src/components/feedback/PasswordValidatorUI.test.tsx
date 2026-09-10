import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { isPasswordValid } from '../../lib/passwordPolicy'
import { PasswordValidatorUI } from './PasswordValidatorUI'

describe('PasswordValidatorUI', () => {
  it('valida todas las reglas de la política', () => {
    expect(isPasswordValid('Segura8!')).toBe(true)
    expect(isPasswordValid('segura8!')).toBe(false)
    render(<PasswordValidatorUI passwordValue="Segura8!" />)
    expect(screen.getAllByText('cumplido')).toHaveLength(5)
  })
})
