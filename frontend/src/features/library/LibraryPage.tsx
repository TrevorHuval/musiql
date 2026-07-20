import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { usePlaylists } from '../../api/queries'
import { problemToMessage } from '../../api/problem'
import type { Playlist } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Callout } from '../../components/ui/Callout'
import { Icon } from '../../components/ui/Icon'
import { IconButton, LivePill, Spinner } from '../../components/ui/misc'
import { formatRelative } from '../../lib/format'
import { RenameDialog } from './RenameDialog'
import { DeleteDialog } from './DeleteDialog'
import styles from './LibraryPage.module.css'

type DialogState = { kind: 'rename' | 'delete'; playlist: Playlist } | null

export function LibraryPage() {
  const navigate = useNavigate()
  const playlists = usePlaylists()
  const [dialog, setDialog] = useState<DialogState>(null)

  return (
    <div className={styles.page}>
      <header className={styles.head}>
        <div>
          <h1 className={styles.title}>Your playlists</h1>
          <p className={styles.subtitle}>
            Each one is a live query. Open a playlist and it re-runs against the catalog.
          </p>
        </div>
        <Button variant="primary" onClick={() => navigate('/playlists/new')}>
          <Icon name="plus" size={16} />
          New playlist
        </Button>
      </header>

      {playlists.isPending ? (
        <div className={styles.centered}>
          <Spinner />
        </div>
      ) : playlists.isError ? (
        <Callout tone="clay" icon="x" title="Couldn’t load your playlists">
          {problemToMessage(playlists.error)}
        </Callout>
      ) : playlists.data.length === 0 ? (
        <EmptyLibrary onCreate={() => navigate('/playlists/new')} />
      ) : (
        <ul className={styles.list}>
          {playlists.data.map((playlist) => (
            <li key={playlist.id}>
              <PlaylistRow
                playlist={playlist}
                onRename={() => setDialog({ kind: 'rename', playlist })}
                onDelete={() => setDialog({ kind: 'delete', playlist })}
              />
            </li>
          ))}
        </ul>
      )}

      {dialog?.kind === 'rename' && (
        <RenameDialog playlist={dialog.playlist} open onClose={() => setDialog(null)} />
      )}
      {dialog?.kind === 'delete' && (
        <DeleteDialog playlist={dialog.playlist} open onClose={() => setDialog(null)} />
      )}
    </div>
  )
}

function PlaylistRow({
  playlist,
  onRename,
  onDelete,
}: {
  playlist: Playlist
  onRename: () => void
  onDelete: () => void
}) {
  return (
    <div className={styles.row}>
      <Link to={`/playlists/${playlist.id}`} className={styles.rowMain}>
        <div className={styles.rowText}>
          <div className={styles.rowTitleLine}>
            <span className={styles.rowName}>{playlist.name}</span>
            <LivePill />
          </div>
          {playlist.description && <p className={styles.rowDesc}>{playlist.description}</p>}
          <code className={styles.rowMql}>{playlist.mql}</code>
        </div>
      </Link>
      <div className={styles.rowMeta}>
        <span className={styles.rowTime}>Updated {formatRelative(playlist.updatedAt)}</span>
        <div className={styles.rowActions}>
          <IconButton icon="pencil" label="Rename" onClick={onRename} />
          <IconButton icon="trash" label="Delete" tone="danger" onClick={onDelete} />
        </div>
      </div>
    </div>
  )
}

function EmptyLibrary({ onCreate }: { onCreate: () => void }) {
  return (
    <div className={styles.empty}>
      <span className={styles.emptyMark} aria-hidden="true">
        <Icon name="library" size={26} />
      </span>
      <h2 className={styles.emptyTitle}>Start your first playlist</h2>
      <p className={styles.emptyText}>
        Describe the music you want — a genre, a stretch of years, an artist to leave out — and
        MusiQL keeps the track list current.
      </p>
      <Button variant="primary" onClick={onCreate}>
        <Icon name="plus" size={16} />
        New playlist
      </Button>
    </div>
  )
}
