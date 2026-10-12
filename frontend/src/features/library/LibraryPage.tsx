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
  const [dialogOpen, setDialogOpen] = useState(false)

  return (
    <div className={styles.page}>
      <header className="page-heading">
        <h1>Your playlists</h1>
        <p className={styles.lead}>
          Each one is a live query. Open a playlist and it re-runs against the catalog.
        </p>
        <Button variant="primary" onClick={() => navigate('/playlists/new')}>
          <Icon name="plus" size={16} />
          New playlist
        </Button>
      </header>

      <div className={styles.body}>
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
              <li key={playlist.id} className={styles.item}>
                <PlaylistCard
                  playlist={playlist}
                  onRename={() => {
                    setDialog({ kind: 'rename', playlist })
                    setDialogOpen(true)
                  }}
                  onDelete={() => {
                    setDialog({ kind: 'delete', playlist })
                    setDialogOpen(true)
                  }}
                />
              </li>
            ))}
          </ul>
        )}
      </div>

      {dialog?.kind === 'rename' && (
        <RenameDialog
          playlist={dialog.playlist}
          open={dialogOpen}
          onClose={() => setDialogOpen(false)}
        />
      )}
      {dialog?.kind === 'delete' && (
        <DeleteDialog
          playlist={dialog.playlist}
          open={dialogOpen}
          onClose={() => setDialogOpen(false)}
        />
      )}
    </div>
  )
}

function PlaylistCard({
  playlist,
  onRename,
  onDelete,
}: {
  playlist: Playlist
  onRename: () => void
  onDelete: () => void
}) {
  return (
    <article className={`card lift ${styles.card}`}>
      <Link to={`/playlists/${playlist.id}`} className={styles.cardMain}>
        <div className={styles.cardTitleLine}>
          <h2 className={`card-title ${styles.cardName}`}>{playlist.name}</h2>
          <LivePill />
        </div>
        {playlist.description && (
          <p className={`card-body ${styles.cardDesc}`}>{playlist.description}</p>
        )}
        <code className={styles.cardMql}>{playlist.mql}</code>
      </Link>
      <div className={`card-footer ${styles.cardFooter}`}>
        <span className={`eyebrow ${styles.cardTime}`}>
          Updated {formatRelative(playlist.updatedAt)}
        </span>
        <div className={styles.cardActions}>
          <IconButton icon="pencil" label="Rename" onClick={onRename} />
          <IconButton icon="trash" label="Delete" tone="danger" onClick={onDelete} />
        </div>
      </div>
    </article>
  )
}

function EmptyLibrary({ onCreate }: { onCreate: () => void }) {
  return (
    <div className={`card ${styles.empty}`}>
      <p className="eyebrow">No playlists yet</p>
      <h2 className="card-title">Start your first playlist</h2>
      <p className={`card-body ${styles.emptyText}`}>
        Describe the music you want, such as a genre, a stretch of years, or an artist to leave out,
        and MusiQL keeps the track list current.
      </p>
      <Button variant="secondary" onClick={onCreate}>
        <Icon name="plus" size={16} />
        New playlist
      </Button>
    </div>
  )
}
