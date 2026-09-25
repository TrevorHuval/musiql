import { useNavigate } from 'react-router-dom'
import { problemToMessage } from '../../api/problem'
import { useExportPlaylist, useSetKeepLive, useSpotifyLink, useSpotifyStatus } from '../../api/queries'
import type { ExportResult, PlaylistSpotifyLink, UnmatchedTrack } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Callout } from '../../components/ui/Callout'
import { Dialog } from '../../components/ui/Dialog'
import { Icon } from '../../components/ui/Icon'
import { Spinner } from '../../components/ui/misc'
import { Switch } from '../../components/ui/Switch'
import { formatRelative, formatUntil } from '../../lib/format'
import styles from './export.module.css'

interface ExportSpotifyDialogProps {
  playlistId: string
  playlistName: string
  open: boolean
  onClose: () => void
}

export function ExportSpotifyDialog({ playlistId, playlistName, open, onClose }: ExportSpotifyDialogProps) {
  const status = useSpotifyStatus()
  const link = useSpotifyLink(playlistId)
  const exportPlaylist = useExportPlaylist(playlistId)

  const busy = exportPlaylist.isPending
  const result = exportPlaylist.data

  return (
    <Dialog open={open} onClose={busy ? () => {} : onClose} title="Export to Spotify" description={playlistName}>
      {status.isPending ? (
        <div className={styles.center}>
          <Spinner />
        </div>
      ) : status.data?.connected === false ? (
        <NotConnected onClose={onClose} />
      ) : result ? (
        <ResultView
          result={result}
          playlistId={playlistId}
          link={link.data}
          onClose={onClose}
          onReExport={() => exportPlaylist.mutate(false)}
        />
      ) : busy ? (
        <div className={styles.progress}>
          <Spinner size={22} />
          <p className={styles.progressText}>Matching tracks to Spotify and pushing the playlist…</p>
          <p className={styles.progressHint}>This can take a moment the first time.</p>
        </div>
      ) : (
        <Ready
          playlistId={playlistId}
          link={link.data}
          onExport={() => exportPlaylist.mutate(false)}
          onClose={onClose}
          error={exportPlaylist.isError ? problemToMessage(exportPlaylist.error) : null}
        />
      )}
    </Dialog>
  )
}

function NotConnected({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate()
  return (
    <div className={styles.body}>
      <Callout tone="signal" icon="link" title="Connect Spotify first">
        Link your Spotify account in settings, then come back to export this playlist.
      </Callout>
      <div className={styles.actions}>
        <Button variant="ghost" onClick={onClose}>
          Cancel
        </Button>
        <Button
          variant="primary"
          onClick={() => {
            onClose()
            navigate('/settings')
          }}
        >
          Go to settings
        </Button>
      </div>
    </div>
  )
}

function Ready({
  playlistId,
  link,
  onExport,
  onClose,
  error,
}: {
  playlistId: string
  link: PlaylistSpotifyLink | undefined
  onExport: () => void
  onClose: () => void
  error: string | null
}) {
  const exported = link?.exported === true
  return (
    <div className={styles.body}>
      {exported && link.lastExportedAt ? (
        <p className={styles.lead}>
          Last pushed {formatRelative(link.lastExportedAt)} with {link.trackCount} tracks.{' '}
          {link.spotifyUrl && (
            <a className={styles.inlineLink} href={link.spotifyUrl} target="_blank" rel="noreferrer">
              Open in Spotify
            </a>
          )}
        </p>
      ) : (
        <p className={styles.lead}>
          MusiQL will create or update a Spotify playlist and add every track it can match, in order.
        </p>
      )}
      <p className={styles.note}>
        Re-exporting replaces the Spotify playlist’s contents so it mirrors this live query.
      </p>
      {exported && <KeepLiveRow playlistId={playlistId} link={link} />}
      {error && (
        <Callout tone="clay" icon="x" title="Export failed">
          {error}
        </Callout>
      )}
      <div className={styles.actions}>
        <Button variant="ghost" onClick={onClose}>
          Cancel
        </Button>
        <Button variant="primary" onClick={onExport}>
          {exported ? 'Export now' : 'Export'}
        </Button>
      </div>
    </div>
  )
}

function ResultView({
  result,
  playlistId,
  link,
  onClose,
  onReExport,
}: {
  result: ExportResult
  playlistId: string
  link: PlaylistSpotifyLink | undefined
  onClose: () => void
  onReExport: () => void
}) {
  return (
    <div className={styles.body}>
      <div className={styles.summary}>
        <span className={styles.summaryCount}>
          {result.matchedCount}
          <span className={styles.summaryTotal}>/{result.totalCount}</span>
        </span>
        <span className={styles.summaryLabel}>tracks added to Spotify</span>
      </div>

      {result.unmatched.length > 0 && (
        <div className={styles.unmatched}>
          <div className={styles.unmatchedHead}>
            <span className={styles.sectionLabel}>Couldn’t match ({result.unmatched.length})</span>
          </div>
          <ul className={styles.unmatchedList}>
            {result.unmatched.map((track, index) => (
              <UnmatchedRow key={index} track={track} />
            ))}
          </ul>
        </div>
      )}

      {link?.exported && <KeepLiveRow playlistId={playlistId} link={link} />}

      <div className={styles.actions}>
        <Button variant="ghost" onClick={onReExport}>
          Export again
        </Button>
        <a className={styles.openLink} href={result.spotifyUrl} target="_blank" rel="noreferrer">
          <Icon name="external" size={15} />
          Open in Spotify
        </a>
        <Button variant="primary" onClick={onClose}>
          Done
        </Button>
      </div>
    </div>
  )
}

// Once a playlist exists on Spotify it can follow the query on its own: the
// server re-exports it daily. The row states what will happen next, or what
// went wrong last time, rather than only the switch position.
function KeepLiveRow({ playlistId, link }: { playlistId: string; link: PlaylistSpotifyLink }) {
  const setKeepLive = useSetKeepLive(playlistId)
  const on = setKeepLive.isPending ? setKeepLive.variables === true : link.keepLive

  return (
    <div className={styles.keepLive} data-on={on || undefined}>
      <div className={styles.keepLiveText}>
        <span className={styles.keepLiveTitle}>Keep live on Spotify</span>
        <span className={styles.keepLiveMeta}>
          {link.lastRefreshError
            ? link.lastRefreshError
            : on && link.nextRefreshAt
              ? `Refreshes daily · next ${formatUntil(link.nextRefreshAt)}`
              : 'Re-export every day so Spotify follows this query'}
        </span>
      </div>
      <Switch
        checked={on}
        onChange={(next) => setKeepLive.mutate(next)}
        label="Keep live on Spotify"
        disabled={setKeepLive.isPending}
      />
    </div>
  )
}

function UnmatchedRow({ track }: { track: UnmatchedTrack }) {
  return (
    <li className={styles.unmatchedRow}>
      <div className={styles.unmatchedMain}>
        <span className={styles.unmatchedTitle}>{track.title}</span>
        <span className={styles.unmatchedMeta}>
          {track.artist}
          {track.year ? ` · ${track.year}` : ''}
        </span>
      </div>
      <span className={styles.unmatchedScore} title="Best candidate confidence">
        {track.confidence > 0 ? `${Math.round(track.confidence * 100)}%` : 'no match'}
      </span>
    </li>
  )
}
