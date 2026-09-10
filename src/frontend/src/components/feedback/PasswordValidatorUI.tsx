import { getPasswordRules } from '../../lib/passwordPolicy'

export function PasswordValidatorUI({ passwordValue }: { passwordValue: string }) {
  return (
    <ul className="grid gap-2 sm:grid-cols-2" aria-label="Requisitos de contraseña">
      {getPasswordRules(passwordValue).map((rule) => (
        <li
          key={rule.id}
          className={`flex items-center gap-2 text-xs transition-all duration-300 ease-in-out ${
            rule.valid ? 'scale-[1.02] font-semibold text-emerald-700' : 'text-slate-500'
          }`}
        >
          <span
            aria-hidden="true"
            className={`grid size-5 shrink-0 place-items-center rounded-full border text-[0.7rem] font-black transition-all duration-300 ${
              rule.valid
                ? 'scale-105 border-emerald-500 bg-emerald-500 text-white'
                : 'border-slate-300 bg-white text-slate-400'
            }`}
          >
            {rule.valid ? '✓' : '×'}
          </span>
          <span>{rule.label}</span>
          <span className="sr-only">{rule.valid ? 'cumplido' : 'pendiente'}</span>
        </li>
      ))}
    </ul>
  )
}
