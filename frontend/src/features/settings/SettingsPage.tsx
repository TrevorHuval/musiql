import { useMutation } from '@tanstack/react-query'
import { problemToMessage } from '../../api/problem'
import { spotify, useDisconnectSpotify, useSpotifyStatus, useSyncLibrary } from '../../api/queries'
import { Button } from '../../components/ui/Button'
import { Callout } from '../../components/ui/Callout'
import { Icon } from '../../components/ui/Icon'
import { Pill, Spinner } from '../../components/ui/misc'
import styles from './settings.module.css'

export function SettingsPage() {
  const status = useSpotifyStatus()

  return (
    <div className={styles.page}>
      <header className={styles.head}>
        <div>
          <h1 className={styles.title}>Settings</h1>
          <p className={styles.subtitle}>Connect Spotify to export playlists and sync your library.</p>
        </div>
      </header>

      <section className={styles.card}>
        <div className={styles.cardHead}>
          <span className={styles.mark} aria-hidden="true">
            <Icon name="link" size={18} />
          </span>
          <div className={styles.cardHeadText}>
            <h2 className={styles.cardTitle}>Spotify</h2>
            <p className={styles.cardMeta}>Playlist export and saved-track sync</p>
          </div>
          {status.data?.connected && <Pill tone="valid">Connected</Pill>}
        </div>

        {status.isPending ? (
          <div className={styles.loading}>
            <Spinner />
          </div>
        ) : status.isError ? (
          <Callout tone="clay" icon="x" title="Couldn’t load Spotify status">
            {problemToMessage(status.error)}
          </Callout>
        ) : status.data?.connected ? (
          <ConnectedPanel
            displayName={status.data.displayName}
            connectedAt={status.data.connectedAt}
            librarySyncedAt={status.data.librarySyncedAt}
            savedCount={status.data.librarySavedCount}
            matchedCount={status.data.libraryMatchedCount}
          />
        ) : (
          <DisconnectedPanel />
        )}
      </section>
    </div>
  )
}

function DisconnectedPanel() {
  const connect = useMutation({
    mutationFn: spotify.connect,
    onSuccess: (result) => window.location.assign(result.authorizeUrl),
  })

  return (
    <div className={styles.panel}>
      <p className={styles.lead}>
        Link your Spotify account to push MusiQL playlists to Spotify and to fill{' '}
        <span className={styles.mono}>from library</span> queries with your saved tracks.
      </p>
      {connect.isError && (
        <Callout tone="clay" icon="x" title="Couldn’t start the connection">
          {problemToMessage(connect.error)}
        </Callout>
      )}
      <div className={styles.actions}>
        <Button variant="primary" onClick={() => connect.mutate()} loading={connect.isPending}>
          Connect Spotify
        </Button>
      </div>
    </div>
  )
}

interface ConnectedPanelProps {
  displayName: string | null
  connectedAt: string | null
  librarySyncedAt: string | null
  savedCount: number
  matchedCount: number
}

function ConnectedPanel({
  displayName,
  connectedAt,
  librarySyncedAt,
  savedCount,
  matchedCount,
}: ConnectedPanelProps) {
  const disconnect = useDisconnectSpotify()
  const sync = useSyncLibrary()

  return (
    <div className={styles.panel}>
      <div className={styles.accountRow}>
        <div>
          <p className={styles.accountName}>{displayName ?? 'Spotify account'}</p>
          {connectedAt && (
            <p className={styles.accountMeta}>Connected {formatDate(connectedAt)}</p>
          )}
        </div>
        <Button
          variant="ghost"
          size="sm"
          onClick={() => disconnect.mutate()}
          loading={disconnect.isPending}
        >
          Disconnect
        </Button>
      </div>

      <div className={styles.syncBlock}>
        <div className={styles.syncHead}>
          <span className={styles.sectionLabel}>Library sync</span>
          <Button
            variant="secondary"
            size="sm"
            onClick={() => sync.mutate()}
            loading={sync.isPending}
          >
            <span className={styles.btnIcon}>
              <Icon name="refresh" size={14} />
              {librarySyncedAt ? 'Re-sync' : 'Sync library'}
            </span>
          </Button>
        </div>

        {sync.isError ? (
          <Callout tone="clay" icon="x" title="Sync failed">
            {problemToMessage(sync.error)}
          </Callout>
        ) : sync.isSuccess ? (
          <Callout tone="valid" icon="check" title="Library synced">
            {sync.data.matchedCount} of {sync.data.savedCount} saved tracks matched the catalog and
            joined your library.
          </Callout>
        ) : sync.isPending ? (
          <p className={styles.syncStatus}>Reading your saved tracks and matching them…</p>
        ) : librarySyncedAt ? (
          <p className={styles.syncStatus}>
            Last synced {formatDate(librarySyncedAt)}.{' '}
            <span className={styles.mono}>
              {matchedCount}/{savedCount}
            </span>{' '}
            saved tracks matched.
          </p>
        ) : (
          <p className={styles.syncStatus}>
            Your saved tracks aren’t synced yet. Sync to use{' '}
            <span className={styles.mono}>from library</span> queries.
          </p>
        )}
      </div>
    </div>
  )
}

function formatDate(value: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return ''
  }
  return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })
}
