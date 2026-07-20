import { useNavigate } from 'react-router-dom'
import { problemToMessage } from '../../api/problem'
import { useExportPlaylist, useSpotifyStatus } from '../../api/queries'
import type { ExportResult, UnmatchedTrack } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Callout } from '../../components/ui/Callout'
import { Dialog } from '../../components/ui/Dialog'
import { Icon } from '../../components/ui/Icon'
import { Spinner } from '../../components/ui/misc'
import styles from './export.module.css'

interface ExportSpotifyDialogProps {
  playlistId: string
  playlistName: string
  open: boolean
  onClose: () => void
}

export function ExportSpotifyDialog({ playlistId, playlistName, open, onClose }: ExportSpotifyDialogProps) {
  const status = useSpotifyStatus()
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
        <ResultView result={result} onClose={onClose} onReExport={() => exportPlaylist.mutate(false)} />
      ) : busy ? (
        <div className={styles.progress}>
          <Spinner size={22} />
          <p className={styles.progressText}>Matching tracks to Spotify and pushing the playlist…</p>
          <p className={styles.progressHint}>This can take a moment the first time.</p>
        </div>
      ) : (
        <Ready
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
  onExport,
  onClose,
  error,
}: {
  onExport: () => void
  onClose: () => void
  error: string | null
}) {
  return (
    <div className={styles.body}>
      <p className={styles.lead}>
        MusiQL will create or update a Spotify playlist and add every track it can match, in order.
      </p>
      <p className={styles.note}>
        Re-exporting replaces the Spotify playlist’s contents so it mirrors this live query.
      </p>
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
          Export
        </Button>
      </div>
    </div>
  )
}

function ResultView({
  result,
  onClose,
  onReExport,
}: {
  result: ExportResult
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
