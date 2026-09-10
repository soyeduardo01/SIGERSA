import { useRef, type ClipboardEvent, type KeyboardEvent } from 'react'

interface OtpInputProps {
  value: string
  onChange: (value: string) => void
  length?: number
  disabled?: boolean
}

export function OtpInput({ value, onChange, length = 6, disabled = false }: OtpInputProps) {
  const inputs = useRef<Array<HTMLInputElement | null>>([])
  const digits = Array.from({ length }, (_, index) => value[index] ?? '')

  function setDigit(index: number, input: string) {
    const numeric = input.replace(/\D/g, '')
    if (!numeric) {
      const next = [...digits]
      next[index] = ''
      onChange(next.join(''))
      return
    }
    const next = [...digits]
    numeric
      .slice(0, length - index)
      .split('')
      .forEach((digit, offset) => {
        next[index + offset] = digit
      })
    onChange(next.join(''))
    inputs.current[Math.min(index + numeric.length, length - 1)]?.focus()
  }

  function handleKeyDown(index: number, event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Backspace' && !digits[index] && index > 0) inputs.current[index - 1]?.focus()
    if (event.key === 'ArrowLeft' && index > 0) inputs.current[index - 1]?.focus()
    if (event.key === 'ArrowRight' && index < length - 1) inputs.current[index + 1]?.focus()
  }

  function handlePaste(event: ClipboardEvent<HTMLDivElement>) {
    event.preventDefault()
    const pasted = event.clipboardData.getData('text').replace(/\D/g, '').slice(0, length)
    if (!pasted) return
    onChange(pasted)
    inputs.current[Math.min(pasted.length, length) - 1]?.focus()
  }

  return (
    <fieldset disabled={disabled}>
      <legend className="text-sm font-bold text-ink-body">Código OTP</legend>
      <div className="mt-2 flex justify-center gap-2 sm:gap-3" onPaste={handlePaste}>
        {digits.map((digit, index) => (
          <input
            key={index}
            ref={(element) => {
              inputs.current[index] = element
            }}
            required
            aria-label={`Dígito ${index + 1} de ${length}`}
            inputMode="numeric"
            autoComplete={index === 0 ? 'one-time-code' : 'off'}
            pattern="[0-9]"
            maxLength={1}
            value={digit}
            onChange={(event) => setDigit(index, event.target.value)}
            onKeyDown={(event) => handleKeyDown(index, event)}
            onFocus={(event) => event.currentTarget.select()}
            className={`aspect-square w-11 rounded-xl border text-center text-xl font-extrabold transition-all duration-200 ease-out outline-none sm:w-12 ${
              digit
                ? 'scale-105 border-brand-500 bg-brand-50 text-brand-900 shadow-sm'
                : 'border-slate-300 bg-white text-ink-strong'
            } focus:-translate-y-0.5 focus:border-brand-600 focus:ring-4 focus:ring-brand-100`}
          />
        ))}
      </div>
      <p className="mt-3 text-center text-xs text-ink-muted">
        Ingrese los seis dígitos enviados a su correo.
      </p>
    </fieldset>
  )
}
