import { useEffect } from 'react'
import { useLocation } from 'react-router-dom'

declare global {
  interface Window {
    /** Set by the init script the build emits; absent in dev and for Do Not Track. */
    gaEnabled?: boolean
    gtag?: (...args: unknown[]) => void
  }
}

/** Sign-in screens and the Spotify callback, whose query string carries an OAuth code. */
const UNTRACKED = /^\/(login|register|settings\/spotify\/callback)(\/|$)/

const base = import.meta.env.BASE_URL.replace(/\/+$/, '')

/** Playlist ids are per-user data; report the route, not the record. */
export function reportedPath(pathname: string): string {
  return pathname.replace(/^\/playlists\/(?!new$)[^/]+/, '/playlists/:id')
}

/**
 * Reports a page view per route change. GA4's own history-based page view is turned off
 * in the property's enhanced measurement, and `send_page_view` is off in the init script,
 * so this is the only source. A no-op whenever the tag was not loaded.
 */
export function usePageViews() {
  const { pathname } = useLocation()

  useEffect(() => {
    if (window.gaEnabled !== true || window.gtag === undefined || UNTRACKED.test(pathname)) return

    const path = base + reportedPath(pathname)
    window.gtag('event', 'page_view', {
      page_path: path,
      page_location: window.location.origin + path,
      page_title: document.title,
    })
  }, [pathname])
}
