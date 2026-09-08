import { cn } from '../../lib/cn'

export type RiskLevel = 'Bajo' | 'Medio' | 'Alto'

const styles: Record<RiskLevel, string> = {
  Bajo: 'bg-risk-low/10 text-risk-low ring-risk-low/20',
  Medio: 'bg-risk-medium/10 text-risk-medium ring-risk-medium/20',
  Alto: 'bg-risk-high/10 text-risk-high ring-risk-high/20',
}

export function RiskBadge({ level }: { level: RiskLevel }) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold ring-1 ring-inset',
        styles[level],
      )}
    >
      <span className="size-1.5 rounded-full bg-current" aria-hidden="true" />
      Riesgo {level.toLowerCase()}
    </span>
  )
}
