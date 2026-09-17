import { playSound } from 'react-sounds'
import Swal from 'sweetalert2'

const brandColor = '#0d563f'
const alertVolume = 0.55
const alertSounds = {
  success: '/sounds/notification/success.mp3',
  error: '/sounds/notification/error.mp3',
  warning: '/sounds/notification/warning.mp3',
  info: '/sounds/notification/info.mp3',
} as const

function playAlertSound(sound: (typeof alertSounds)[keyof typeof alertSounds]) {
  void playSound(sound, { volume: alertVolume }).catch(() => {
    // An alert must remain usable if audio is blocked or unavailable.
  })
}

export const alerts = {
  success(title: string, text?: string) {
    playAlertSound(alertSounds.success)
    return Swal.fire({
      toast: true,
      position: 'top-end',
      icon: 'success',
      title,
      text,
      showConfirmButton: false,
      timer: 3200,
      timerProgressBar: true,
      heightAuto: false,
    })
  },

  error(
    error: unknown,
    title = 'No se pudo completar la acción',
    options: { showUnauthorized?: boolean } = {},
  ) {
    if (
      !options.showUnauthorized &&
      typeof error === 'object' &&
      error !== null &&
      'status' in error &&
      error.status === 401
    ) {
      return Promise.resolve()
    }
    playAlertSound(alertSounds.error)
    return Swal.fire({
      icon: 'error',
      title,
      text:
        error instanceof Error
          ? error.message
          : 'Ocurrió un error inesperado. Inténtelo nuevamente.',
      confirmButtonText: 'Entendido',
      confirmButtonColor: brandColor,
      heightAuto: false,
    })
  },

  async confirm(options: { title: string; text: string; confirmText?: string }) {
    playAlertSound(alertSounds.warning)
    const result = await Swal.fire({
      icon: 'warning',
      title: options.title,
      text: options.text,
      showCancelButton: true,
      confirmButtonText: options.confirmText ?? 'Confirmar',
      cancelButtonText: 'Volver',
      confirmButtonColor: '#b42318',
      cancelButtonColor: '#64748b',
      reverseButtons: true,
      focusCancel: true,
      heightAuto: false,
    })
    return result.isConfirmed
  },

  async textInput(options: { title: string; label: string; confirmText?: string }) {
    playAlertSound(alertSounds.info)
    const result = await Swal.fire({
      title: options.title,
      input: 'textarea',
      inputLabel: options.label,
      inputPlaceholder: 'Escriba una justificación clara…',
      inputAttributes: { maxlength: '2000' },
      showCancelButton: true,
      confirmButtonText: options.confirmText ?? 'Confirmar',
      cancelButtonText: 'Volver',
      confirmButtonColor: brandColor,
      heightAuto: false,
      inputValidator: (value) => (!value?.trim() ? 'La justificación es obligatoria.' : undefined),
    })
    return result.isConfirmed ? result.value.trim() : null
  },
}
