import { Link, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { useTheme } from './ThemeProvider'
import { Icon } from '../components/ui/Icon'
import { IconButton } from '../components/ui/misc'
import styles from './AppShell.module.css'

export function AppShell() {
  const { user, logout } = useAuth()
  const { theme, toggle } = useTheme()

  return (
    <div className={styles.shell}>
      <header className={styles.header}>
        <Link to="/" className={styles.brand} aria-label="MusiQL home">
          <span className={styles.brandMark} aria-hidden="true">
            <Icon name="disc" size={20} />
          </span>
          <span className={styles.brandName}>MusiQL</span>
          <span className={styles.brandTag}>playlists as queries</span>
        </Link>

        <div className={styles.actions}>
          <Link to="/settings" className={styles.navIcon} aria-label="Settings" title="Settings">
            <Icon name="settings" size={17} />
          </Link>
          <IconButton
            icon={theme === 'dark' ? 'sun' : 'moon'}
            label={theme === 'dark' ? 'Switch to light theme' : 'Switch to dark theme'}
            onClick={toggle}
          />
          <span className={styles.divider} aria-hidden="true" />
          <span className={styles.user} title={user?.email}>
            {user?.email}
          </span>
          <IconButton icon="logout" label="Sign out" onClick={() => void logout()} />
        </div>
      </header>

      <main className={styles.main}>
        <Outlet />
      </main>
    </div>
  )
}
