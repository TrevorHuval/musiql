import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { problemToMessage } from '../../api/problem'
import { keys, spotify } from '../../api/queries'
import { Callout } from '../../components/ui/Callout'
import { Spinner } from '../../components/ui/misc'
import styles from './settings.module.css'

export function SpotifyCallbackPage() {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const client = useQueryClient()
  const started = useRef(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (started.current) {
      return
    }
    started.current = true

    const denied = params.get('error')
    const code = params.get('code')
    const state = params.get('state')

    if (denied) {
      setError('Spotify authorization was declined.')
      return
    }
    if (!code || !state) {
      setError('The callback link is missing its authorization details.')
      return
    }

    spotify
      .callback(state, code)
      .then(() => {
        client.invalidateQueries({ queryKey: keys.spotifyStatus })
        navigate('/settings', { replace: true })
      })
      .catch((cause) => setError(problemToMessage(cause)))
  }, [params, navigate, client])

  return (
    <div className={styles.callback}>
      {error ? (
        <div className={styles.callbackCard}>
          <Callout tone="clay" icon="x" title="Couldn’t connect Spotify">
            {error}
          </Callout>
          <Link to="/settings" className={styles.callbackLink}>
            Back to settings
          </Link>
        </div>
      ) : (
        <div className={styles.callbackCard}>
          <Spinner size={22} />
          <p className={styles.callbackText}>Finishing the Spotify connection…</p>
        </div>
      )}
    </div>
  )
}
