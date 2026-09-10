export interface PasswordRule {
  id: string
  label: string
  valid: boolean
}

export function getPasswordRules(passwordValue: string): PasswordRule[] {
  return [
    { id: 'length', label: 'Mínimo 8 caracteres', valid: passwordValue.length >= 8 },
    { id: 'uppercase', label: 'Una letra mayúscula', valid: /[A-Z]/.test(passwordValue) },
    { id: 'lowercase', label: 'Una letra minúscula', valid: /[a-z]/.test(passwordValue) },
    { id: 'number', label: 'Un número', valid: /\d/.test(passwordValue) },
    { id: 'special', label: 'Un carácter especial', valid: /[^A-Za-z0-9]/.test(passwordValue) },
  ]
}

export function isPasswordValid(passwordValue: string) {
  return getPasswordRules(passwordValue).every((rule) => rule.valid)
}
