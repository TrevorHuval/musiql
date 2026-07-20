import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useCreatePlaylist } from '../../api/queries'
import { problemToMessage } from '../../api/problem'
import { Button } from '../../components/ui/Button'
import { Callout } from '../../components/ui/Callout'
import { Dialog } from '../../components/ui/Dialog'
import { Field } from '../../components/ui/Field'
import { TextInput } from '../../components/ui/TextInput'
import dialogStyles from '../library/dialog.module.css'

interface SavePlaylistDialogProps {
  mql: string
  open: boolean
  onClose: () => void
}

export function SavePlaylistDialog({ mql, open, onClose }: SavePlaylistDialogProps) {
  const navigate = useNavigate()
  const create = useCreatePlaylist()
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (!name.trim()) return
    setError(null)
    try {
      const created = await create.mutateAsync({
        name: name.trim(),
        description: description.trim() || undefined,
        mql,
      })
      navigate(`/playlists/${created.id}`, { replace: true })
    } catch (caught) {
      setError(problemToMessage(caught, 'Could not save the playlist.'))
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="Save as a live playlist"
      description="The query is stored, not the tracks. Every time you open this playlist it runs again."
    >
      <form className={dialogStyles.form} onSubmit={handleSubmit} noValidate>
        {error && <Callout tone="clay" icon="x">{error}</Callout>}
        <Field label="Name">
          {({ id }) => (
            <TextInput
              id={id}
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder="90s grunge, no Nirvana"
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
              placeholder="A short note about this one"
            />
          )}
        </Field>
        <div className={dialogStyles.actions}>
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" variant="primary" loading={create.isPending} disabled={!name.trim()}>
            Save playlist
          </Button>
        </div>
      </form>
    </Dialog>
  )
}
