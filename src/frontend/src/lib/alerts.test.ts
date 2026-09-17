import { beforeEach, describe, expect, it, vi } from 'vitest'
import { playSound } from 'react-sounds'
import Swal from 'sweetalert2'
import { alerts } from './alerts'

vi.mock('react-sounds', () => ({
  playSound: vi.fn(() => Promise.resolve()),
}))

vi.mock('sweetalert2', () => ({
  default: {
    fire: vi.fn(() => Promise.resolve({ isConfirmed: true, value: 'Justificación' })),
  },
}))

describe('alerts', () => {
  beforeEach(() => vi.clearAllMocks())

  it.each([
    ['success', () => alerts.success('Guardado'), '/sounds/notification/success.mp3'],
    ['error', () => alerts.error(new Error('Falló')), '/sounds/notification/error.mp3'],
    [
      'confirm',
      () => alerts.confirm({ title: 'Confirmar', text: '¿Desea continuar?' }),
      '/sounds/notification/warning.mp3',
    ],
    [
      'textInput',
      () => alerts.textInput({ title: 'Motivo', label: 'Justificación' }),
      '/sounds/notification/info.mp3',
    ],
  ])('reproduce el sonido correspondiente en %s', async (_name, showAlert, sound) => {
    await showAlert()

    expect(playSound).toHaveBeenCalledWith(sound, { volume: 0.55 })
    expect(Swal.fire).toHaveBeenCalledOnce()
  })
})
