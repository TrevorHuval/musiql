import { useState, type FormEvent } from 'react'
import { problemToMessage } from '../../api/problem'
import { useRequestSpotifyAccess, useSpotifyAccessRequest } from '../../api/queries'
import { Button } from '../../components/ui/Button'
import { Callout } from '../../components/ui/Callout'
import { Field } from '../../components/ui/Field'
import { TextInput } from '../../components/ui/TextInput'
import styles from './settings.module.css'

const MAX_EMAIL = 254
const MAX_NOTE = 500

// MusiQL's Spotify app runs in Spotify's Development Mode, which only lets in
// accounts added to its allowlist. This explains that and lets a user ask.
export function AccessRequestPanel() {
  const status = useSpotifyAccessRequest()
  const request = useRequestSpotifyAccess()
  const [open, setOpen] = useState(false)
  const [email, setEmail] = useState('')
  const [note, setNote] = useState('')

  if (status.isPending || status.isError || !status.data.enabled) {
    return (
      <Callout tone="signal" icon="link" title="Spotify linking is invite-only">
        Spotify only lets accounts that have been added to MusiQL connect for now.
      </Callout>
    )
  }

  const requestedAt = status.data.requestedAt
  const canRequestAgainAt = status.data.canRequestAgainAt
  const waiting = canRequestAgainAt !== null && new Date(canRequestAgainAt) > new Date()

  const submit = (event: FormEvent) => {
    event.preventDefault()
    request.mutate(
      { spotifyEmail: email.trim(), note: note.trim() },
      { onSuccess: () => setOpen(false) },
    )
  }

  if (requestedAt && (waiting || !open)) {
    return (
      <Callout
        tone="valid"
        icon="check"
        title="Access requested"
        action={
          waiting ? undefined : (
            <Button variant="ghost" size="sm" onClick={() => setOpen(true)}>
              Ask again
            </Button>
          )
        }
      >
        You asked on {formatDate(requestedAt)} for{' '}
        <span className={styles.mono}>{status.data.spotifyEmail}</span>. You’ll be able to connect
        once that account has been added.
      </Callout>
    )
  }

  return (
    <Callout tone="signal" icon="link" title="Spotify linking is invite-only">
      <p className={styles.calloutText}>
        Spotify only lets accounts that have been added to MusiQL connect for now. If yours hasn’t
        been added, ask for access and you’ll be added by hand.
      </p>
      {open ? (
        <form className={styles.requestForm} onSubmit={submit} noValidate>
          <Field label="Spotify account email" hint="The email you sign in to Spotify with">
            {({ id, invalid }) => (
              <TextInput
                id={id}
                type="email"
                autoComplete="email"
                placeholder="you@example.com"
                maxLength={MAX_EMAIL}
                value={email}
                invalid={invalid}
                onChange={(event) => setEmail(event.target.value)}
                required
              />
            )}
          </Field>
          <Field label="Note" hint="Optional">
            {({ id }) => (
              <TextInput
                id={id}
                maxLength={MAX_NOTE}
                placeholder="How do you know Trevor?"
                value={note}
                onChange={(event) => setNote(event.target.value)}
              />
            )}
          </Field>
          {request.isError && (
            <p className={styles.requestError} role="alert">
              {problemToMessage(request.error)}
            </p>
          )}
          <div className={styles.actions}>
            <Button type="submit" variant="primary" loading={request.isPending} disabled={!email.trim()}>
              Send request
            </Button>
            <Button type="button" variant="ghost" onClick={() => setOpen(false)}>
              Cancel
            </Button>
          </div>
        </form>
      ) : (
        <div className={styles.actions}>
          <Button variant="secondary" size="sm" onClick={() => setOpen(true)}>
            Request access
          </Button>
        </div>
      )}
    </Callout>
  )
}

function formatDate(value: string): string {
  const date = new Date(value)
  return Number.isNaN(date.getTime())
    ? ''
    : date.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })
}
