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
      <legend className="text-sm font-semibold text-[#153f34]">Código de verificación</legend>
      <div
        className="mt-3 grid grid-cols-6 gap-2 sm:gap-3"
        onPaste={handlePaste}
        aria-describedby="otp-help"
      >
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
            className={`aspect-square min-w-0 rounded-xl border text-center text-xl font-extrabold transition-all duration-200 ease-out outline-none sm:text-2xl ${
              digit
                ? 'border-[#16835f] bg-[#effaf5] text-[#125640] shadow-[0_5px_14px_rgba(18,86,64,0.12)]'
                : 'border-slate-300 bg-[#f8fafc] text-slate-800'
            } hover:border-slate-400 focus:-translate-y-0.5 focus:border-[#16835f] focus:bg-white focus:ring-4 focus:ring-[#16835f]/12 disabled:cursor-wait disabled:opacity-60`}
          />
        ))}
      </div>
      <p id="otp-help" className="mt-3 text-center text-xs leading-relaxed text-slate-500">
        Ingresa los seis dígitos enviados a tu correo. También puedes pegar el código completo.
      </p>
    </fieldset>
  )
}
