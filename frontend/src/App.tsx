import { HealthBadge } from './components/HealthBadge'
import './App.css'

export default function App() {
  return (
    <main className="app">
      <div className="card">
        <header className="card__head">
          <h1>MusiQL</h1>
          <p>Playlists defined as queries.</p>
        </header>
        <section className="card__status">
          <h2>System status</h2>
          <HealthBadge view={{ status: 'loading' }} />
        </section>
      </div>
    </main>
  )
}
