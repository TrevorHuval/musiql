import { useState, type FormEvent } from 'react'
import { useUpdatePlaylist } from '../../api/queries'
import { problemToMessage } from '../../api/problem'
import type { Playlist } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Callout } from '../../components/ui/Callout'
import { Dialog } from '../../components/ui/Dialog'
import { Field } from '../../components/ui/Field'
import { TextInput } from '../../components/ui/TextInput'
import dialogStyles from './dialog.module.css'

interface RenameDialogProps {
  playlist: Playlist
  open: boolean
  onClose: () => void
}

export function RenameDialog({ playlist, open, onClose }: RenameDialogProps) {
  const [name, setName] = useState(playlist.name)
  const [description, setDescription] = useState(playlist.description ?? '')
  const [error, setError] = useState<string | null>(null)
  const update = useUpdatePlaylist(playlist.id)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (!name.trim()) return
    setError(null)
    try {
      await update.mutateAsync({
        name: name.trim(),
        description: description.trim() || undefined,
        mql: playlist.mql,
      })
      onClose()
    } catch (caught) {
      setError(problemToMessage(caught, 'Could not rename the playlist.'))
    }
  }

  return (
    <Dialog open={open} onClose={onClose} title="Rename playlist">
      <form className={dialogStyles.form} onSubmit={handleSubmit} noValidate>
        {error && <Callout tone="clay" icon="x">{error}</Callout>}
        <Field label="Name">
          {({ id }) => (
            <TextInput
              id={id}
              value={name}
              onChange={(event) => setName(event.target.value)}
              autoFocus
              required
            />
          )}
        </Field>
        <Field label="Description" hint="Optional">
          {({ id }) => (
            <TextInput
              id={id}
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              placeholder="90s Seattle, minus the obvious"
            />
          )}
        </Field>
        <div className={dialogStyles.actions}>
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" variant="primary" loading={update.isPending} disabled={!name.trim()}>
            Save
          </Button>
        </div>
      </form>
    </Dialog>
  )
}
