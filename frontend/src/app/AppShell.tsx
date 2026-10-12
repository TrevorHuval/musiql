import { useEffect, useRef, useState } from 'react'
import { Link, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { ThemeToggle } from '../components/shell/ThemeToggle'
import { useNavPill } from '../components/shell/useNavPill'
import styles from './AppShell.module.css'

const MOBILE_QUERY = '(max-width: 767px)'

const items = [
  { to: '/', label: 'Library', match: (path: string) => path === '/' || path.startsWith('/playlists') },
  { to: '/settings', label: 'Settings', match: (path: string) => path.startsWith('/settings') },
]

export function AppShell() {
  const { user, logout } = useAuth()
  const { pathname } = useLocation()
  const [open, setOpen] = useState(false)
  const [mobile, setMobile] = useState(() => window.matchMedia(MOBILE_QUERY).matches)
  const nav = useRef<HTMLElement>(null)
  const toggle = useRef<HTMLButtonElement>(null)
  const pages = useRef<HTMLUListElement>(null)
  const panel = useRef<HTMLDivElement>(null)
  const pointerInside = useRef(false)
  const everOpened = useRef(false)

  if (open) everOpened.current = true
  const current = items.find((item) => item.match(pathname))?.to ?? ''
  useNavPill(pages, current, mobile ? 'mobile' : 'desktop')

  useEffect(() => {
    setOpen(false)
  }, [pathname])

  useEffect(() => {
    const query = window.matchMedia(MOBILE_QUERY)
    const update = () => {
      setMobile(query.matches)
      setOpen(false)
    }
    query.addEventListener('change', update)
    return () => query.removeEventListener('change', update)
  }, [])

  useEffect(() => {
    if (!open) return
    const dismiss = (event: PointerEvent) => {
      pointerInside.current = nav.current?.contains(event.target as Node) ?? false
      if (!nav.current?.contains(event.target as Node)) setOpen(false)
    }
    const escape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false)
        toggle.current?.focus()
      }
    }
    document.addEventListener('pointerdown', dismiss)
    document.addEventListener('keydown', escape)
    return () => {
      document.removeEventListener('pointerdown', dismiss)
      document.removeEventListener('keydown', escape)
    }
  }, [open])

  const panelState = open ? 'is-open' : everOpened.current ? 'is-closing' : ''

  return (
    <div className={styles.shell}>
      <a href="#main" className="skip-link">
        Skip to content
      </a>
      <header className="site-header">
        <nav
          ref={nav}
          className="site-nav"
          aria-label="Primary"
          onBlur={(event) => {
            if (event.currentTarget.contains(event.relatedTarget)) return
            if (!event.relatedTarget && pointerInside.current) return
            setOpen(false)
          }}
        >
          <Link to="/" className="wordmark" aria-label="MusiQL home">
            MusiQL<span>.</span>
          </Link>
          <button
            ref={toggle}
            type="button"
            className="icon-button nav-toggle"
            aria-label={open ? 'Close navigation' : 'Open navigation'}
            aria-expanded={open}
            aria-controls="navigation-links"
            onClick={() => setOpen(!open)}
          >
            <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" aria-hidden="true">
              <path className="menu-line menu-line-top" d="M4 7h16" />
              <path className="menu-line menu-line-middle" d="M4 12h16" />
              <path className="menu-line menu-line-bottom" d="M4 17h16" />
            </svg>
          </button>
          <div
            ref={panel}
            id="navigation-links"
            className={mobile ? `nav-content menu-motion ${panelState}` : 'nav-content'}
            data-origin="top-right"
            inert={mobile && !open}
            aria-hidden={mobile && !open ? true : undefined}
            onClick={(event) => {
              if (!(event.target as Element).closest('a')) return
              if (mobile && panel.current?.contains(document.activeElement)) {
                toggle.current?.focus({ preventScroll: true })
              }
              setOpen(false)
            }}
          >
            <ul ref={pages} className="nav-pages">
              <li aria-hidden="true" className="nav-pill" />
              {items.map((item) => {
                const active = item.to === current
                return (
                  <li key={item.to}>
                    <Link
                      to={item.to}
                      aria-current={active ? 'page' : undefined}
                      className={`nav-link press ${active ? 'is-active' : ''}`}
                    >
                      {item.label}
                    </Link>
                  </li>
                )
              })}
            </ul>
            <div className="nav-tools">
              <ThemeToggle />
              <span className={styles.user} title={user?.email}>
                {user?.email}
              </span>
              <button type="button" className="action action-quiet action-sm press" onClick={() => void logout()}>
                Sign out
              </button>
            </div>
          </div>
        </nav>
      </header>

      <main id="main" className={styles.main}>
        <Outlet />
      </main>
    </div>
  )
}
