import { fireEvent, render, screen } from '@testing-library/react'
import { useState } from 'react'
import { describe, expect, it } from 'vitest'
import { OtpInput } from './OtpInput'

function Harness() {
  const [value, setValue] = useState('')
  return <OtpInput value={value} onChange={setValue} />
}

describe('OtpInput', () => {
  it('distribuye los dígitos pegados entre los seis cuadros', () => {
    render(<Harness />)
    const first = screen.getByLabelText('Dígito 1 de 6')
    fireEvent.paste(first.parentElement!, { clipboardData: { getData: () => '123456' } })
    expect(screen.getByLabelText('Dígito 6 de 6')).toHaveValue('6')
  })
})
