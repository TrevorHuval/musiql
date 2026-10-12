import type { ButtonHTMLAttributes, ReactNode } from 'react'
import styles from './Button.module.css'

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger'
type Size = 'sm' | 'md'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant
  size?: Size
  loading?: boolean
  children: ReactNode
}

export function Button({
  variant = 'secondary',
  size = 'md',
  loading = false,
  disabled,
  className,
  children,
  ...rest
}: ButtonProps) {
  const surface = variant === 'primary' ? 'action-primary' : 'action-quiet'
  return (
    <button
      className={[
        'action',
        surface,
        size === 'sm' && 'action-sm',
        styles.button,
        variant === 'ghost' && styles.ghost,
        variant === 'danger' && styles.danger,
        size === 'sm' && styles.sm,
        className,
      ]
        .filter(Boolean)
        .join(' ')}
      disabled={disabled || loading}
      data-loading={loading || undefined}
      {...rest}
    >
      {loading && <span className={styles.spinner} aria-hidden="true" />}
      <span className={styles.label}>{children}</span>
    </button>
  )
}
