import { useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../../auth/useAuth'
import { problemToMessage } from '../../api/problem'
import { Button } from '../../components/ui/Button'
import { Callout } from '../../components/ui/Callout'
import { Field } from '../../components/ui/Field'
import { TextInput } from '../../components/ui/TextInput'
import { AuthLayout } from './AuthLayout'
import styles from './auth.module.css'

interface LocationState {
  from?: string
}

export function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      await login({ email: email.trim(), password })
      const target = (location.state as LocationState | null)?.from ?? '/'
      navigate(target, { replace: true })
    } catch (caught) {
      setError(problemToMessage(caught, 'Could not sign in.'))
      setSubmitting(false)
    }
  }

  return (
    <AuthLayout
      title="Sign in"
      subtitle="Pick up your library of live playlists."
      footer={
        <>
          New here? <Link to="/register">Create an account</Link>
        </>
      }
    >
      <form className={styles.form} onSubmit={handleSubmit} noValidate>
        {error && <Callout tone="clay" icon="x">{error}</Callout>}
        <Field label="Email">
          {({ id, invalid }) => (
            <TextInput
              id={id}
              type="email"
              autoComplete="email"
              placeholder="you@example.com"
              value={email}
              invalid={invalid}
              onChange={(event) => setEmail(event.target.value)}
              required
            />
          )}
        </Field>
        <Field label="Password">
          {({ id }) => (
            <TextInput
              id={id}
              type="password"
              autoComplete="current-password"
              placeholder="Your password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              required
            />
          )}
        </Field>
        <Button
          type="submit"
          variant="primary"
          className={styles.submit}
          loading={submitting}
          disabled={!email || !password}
        >
          Sign in
        </Button>
      </form>
    </AuthLayout>
  )
}
