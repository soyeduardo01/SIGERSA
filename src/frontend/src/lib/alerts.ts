import Swal from 'sweetalert2'

const brandColor = '#0d563f'

export const alerts = {
  success(title: string, text?: string) {
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

  error(error: unknown, title = 'No se pudo completar la acción') {
    if (typeof error === 'object' && error !== null && 'status' in error && error.status === 401) {
      return Promise.resolve()
    }
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
