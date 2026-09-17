import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { InstallPwaButton } from './InstallPwaButton'

describe('InstallPwaButton', () => {
  it('ofrece instalar la PWA cuando Chrome publica el evento de instalación', async () => {
    const prompt = vi.fn(async () => undefined)
    const event = Object.assign(new Event('beforeinstallprompt'), {
      prompt,
      userChoice: Promise.resolve({ outcome: 'accepted', platform: 'web' }),
    })
    render(<InstallPwaButton />)

    window.dispatchEvent(event)
    const button = await screen.findByRole('button', { name: 'Instalar aplicación' })
    fireEvent.click(button)

    await waitFor(() => expect(prompt).toHaveBeenCalledOnce())
  })
})
