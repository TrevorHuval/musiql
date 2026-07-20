import type { ReactNode } from 'react'
import { Icon, type IconName } from './Icon'
import styles from './Callout.module.css'

type Tone = 'clay' | 'signal' | 'neutral' | 'valid'

interface CalloutProps {
  tone?: Tone
  icon?: IconName
  title?: string
  children?: ReactNode
  action?: ReactNode
}

export function Callout({ tone = 'neutral', icon, title, children, action }: CalloutProps) {
  return (
    <div className={styles.callout} data-tone={tone} role={tone === 'clay' ? 'alert' : undefined}>
      {icon && (
        <span className={styles.icon} aria-hidden="true">
          <Icon name={icon} size={17} />
        </span>
      )}
      <div className={styles.body}>
        {title && <p className={styles.title}>{title}</p>}
        {children && <div className={styles.text}>{children}</div>}
      </div>
      {action && <div className={styles.action}>{action}</div>}
    </div>
  )
}
