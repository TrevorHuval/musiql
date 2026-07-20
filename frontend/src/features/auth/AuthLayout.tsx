import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Icon } from '../../components/ui/Icon'
import styles from './AuthLayout.module.css'

interface AuthLayoutProps {
  title: string
  subtitle: string
  children: ReactNode
  footer: ReactNode
}

export function AuthLayout({ title, subtitle, children, footer }: AuthLayoutProps) {
  return (
    <div className={styles.page}>
      <div className={styles.panel}>
        <Link to="/" className={styles.brand}>
          <span className={styles.mark} aria-hidden="true">
            <Icon name="disc" size={22} />
          </span>
          MusiQL
        </Link>
        <div className={styles.intro}>
          <h1 className={styles.title}>{title}</h1>
          <p className={styles.subtitle}>{subtitle}</p>
        </div>
        {children}
        <div className={styles.footer}>{footer}</div>
      </div>
      <p className={styles.tagline}>
        A playlist is a query. Describe the music, and it stays live.
      </p>
    </div>
  )
}
