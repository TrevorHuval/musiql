import { useQuery } from '@tanstack/react-query'
import { fetchHealth } from './api/health'
import { HealthBadge, type HealthView } from './components/HealthBadge'
import './App.css'

export default function App() {
  const health = useQuery({ queryKey: ['health'], queryFn: fetchHealth })

  const view: HealthView = health.isSuccess
    ? { status: 'ready', report: health.data }
    : health.isError
      ? { status: 'error' }
      : { status: 'loading' }

  return (
    <main className="app">
      <div className="card">
        <header className="card__head">
          <h1>MusiQL</h1>
          <p>Playlists defined as queries.</p>
        </header>
        <section className="card__status">
          <h2>System status</h2>
          <HealthBadge view={view} />
        </section>
      </div>
    </main>
  )
}
