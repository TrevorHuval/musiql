import type { HealthReport } from '../api/health'

export type HealthView =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'ready'; report: HealthReport }

export function HealthBadge({ view }: { view: HealthView }) {
  const { tone, label } = describe(view)
  return (
    <div className="badge" data-tone={tone}>
      <span className="badge__dot" aria-hidden="true" />
      <span className="badge__label">{label}</span>
    </div>
  )
}

function describe(view: HealthView): { tone: string; label: string } {
  switch (view.status) {
    case 'loading':
      return { tone: 'neutral', label: 'Checking API…' }
    case 'error':
      return { tone: 'down', label: 'API unreachable' }
    case 'ready':
      return view.report.databaseConnected
        ? { tone: 'up', label: 'API and database healthy' }
        : { tone: 'warn', label: 'API up, database unreachable' }
  }
}
