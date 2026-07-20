import { useId, type ReactNode } from 'react'
import styles from './controls.module.css'

interface FieldProps {
  label: string
  hint?: string
  error?: string
  children: (props: { id: string; invalid: boolean }) => ReactNode
}

export function Field({ label, hint, error, children }: FieldProps) {
  const id = useId()
  return (
    <div className={styles.field}>
      <label className={styles.label} htmlFor={id}>
        {label}
      </label>
      {children({ id, invalid: Boolean(error) })}
      {error ? (
        <span className={styles.error}>{error}</span>
      ) : hint ? (
        <span className={styles.hint}>{hint}</span>
      ) : null}
    </div>
  )
}
