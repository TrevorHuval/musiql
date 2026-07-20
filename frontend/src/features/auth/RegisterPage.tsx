import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../../auth/useAuth'
import { problemToMessage } from '../../api/problem'
import { Button } from '../../components/ui/Button'
import { Callout } from '../../components/ui/Callout'
import { Field } from '../../components/ui/Field'
import { TextInput } from '../../components/ui/TextInput'
import { AuthLayout } from './AuthLayout'
import styles from './auth.module.css'

function passwordProblem(password: string): string | null {
  if (password.length < 10) return 'Use at least 10 characters.'
  if (!/[a-z]/.test(password)) return 'Add a lowercase letter.'
  if (!/[A-Z]/.test(password)) return 'Add an uppercase letter.'
  if (!/[0-9]/.test(password)) return 'Add a digit.'
  return null
}

export function RegisterPage() {
  const { register } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [touched, setTouched] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const localPasswordError = touched ? passwordProblem(password) : null

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setTouched(true)
    if (passwordProblem(password)) return
    setError(null)
    setSubmitting(true)
    try {
      await register({ email: email.trim(), password })
      navigate('/', { replace: true })
    } catch (caught) {
      setError(problemToMessage(caught, 'Could not create the account.'))
      setSubmitting(false)
    }
  }

  return (
    <AuthLayout
      title="Create your account"
      subtitle="Start building playlists that write themselves."
      footer={
        <>
          Already have an account? <Link to="/login">Sign in</Link>
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
        <Field label="Password" error={localPasswordError ?? undefined}>
          {({ id, invalid }) => (
            <TextInput
              id={id}
              type="password"
              autoComplete="new-password"
              placeholder="At least 10 characters"
              value={password}
              invalid={invalid}
              onChange={(event) => setPassword(event.target.value)}
              onBlur={() => setTouched(true)}
              required
            />
          )}
        </Field>
        <p className={styles.policy}>
          <strong>Ten characters or more</strong>, with an uppercase letter, a lowercase letter, and
          a digit.
        </p>
        <Button
          type="submit"
          variant="primary"
          className={styles.submit}
          loading={submitting}
          disabled={!email || !password}
        >
          Create account
        </Button>
      </form>
    </AuthLayout>
  )
}
