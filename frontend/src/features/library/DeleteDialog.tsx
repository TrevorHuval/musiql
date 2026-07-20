import { useState } from 'react'
import { useDeletePlaylist } from '../../api/queries'
import { problemToMessage } from '../../api/problem'
import type { Playlist } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Callout } from '../../components/ui/Callout'
import { Dialog } from '../../components/ui/Dialog'
import dialogStyles from './dialog.module.css'

interface DeleteDialogProps {
  playlist: Playlist
  open: boolean
  onClose: () => void
}

export function DeleteDialog({ playlist, open, onClose }: DeleteDialogProps) {
  const remove = useDeletePlaylist()
  const [error, setError] = useState<string | null>(null)

  async function handleDelete() {
    setError(null)
    try {
      await remove.mutateAsync(playlist.id)
      onClose()
    } catch (caught) {
      setError(problemToMessage(caught, 'Could not delete the playlist.'))
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="Delete playlist"
      description={`“${playlist.name}” and its query definition will be removed. This can’t be undone.`}
    >
      <div className={dialogStyles.stack}>
        {error && <Callout tone="clay" icon="x">{error}</Callout>}
        <div className={dialogStyles.actions}>
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button type="button" variant="danger" loading={remove.isPending} onClick={handleDelete}>
            Delete
          </Button>
        </div>
      </div>
    </Dialog>
  )
}
