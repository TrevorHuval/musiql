import type { ButtonHTMLAttributes, ReactNode } from 'react'
import { Icon, type IconName } from './Icon'
import styles from './misc.module.css'

type Tone = 'signal' | 'neutral' | 'clay' | 'valid'

export function Pill({ tone = 'neutral', children }: { tone?: Tone; children: ReactNode }) {
  return (
    <span className={styles.pill} data-tone={tone}>
      {children}
    </span>
  )
}

export function LivePill() {
  return (
    <span className={styles.live} title="This playlist re-runs its query every time you open it">
      <span className={styles.liveDot} aria-hidden="true" />
      Live playlist
    </span>
  )
}

interface IconButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  icon: IconName
  label: string
  tone?: 'default' | 'danger'
}

export function IconButton({ icon, label, tone = 'default', className, ...rest }: IconButtonProps) {
  return (
    <button
      className={[styles.iconButton, className].filter(Boolean).join(' ')}
      data-tone={tone}
      aria-label={label}
      title={label}
      {...rest}
    >
      <Icon name={icon} size={16} />
    </button>
  )
}

export function Spinner({ size = 18 }: { size?: number }) {
  return (
    <span
      className={styles.spinner}
      style={{ width: size, height: size }}
      role="status"
      aria-label="Loading"
    />
  )
}
