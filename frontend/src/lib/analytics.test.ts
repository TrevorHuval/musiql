import { reportedPath, shouldReport } from './analytics'

describe('reportedPath', () => {
  it('collapses playlist ids', () => {
    expect(reportedPath('/playlists/7d1f3c9e-0a52-4e0b-9a43-1f6d2b8c5e10')).toBe('/playlists/:id')
  })

  it('leaves the new-playlist route and other routes alone', () => {
    expect(reportedPath('/playlists/new')).toBe('/playlists/new')
    expect(reportedPath('/')).toBe('/')
    expect(reportedPath('/settings')).toBe('/settings')
  })
})

describe('shouldReport', () => {
  it('reports the sign-in pages to signed-out visitors', () => {
    expect(shouldReport('/login', false)).toBe(true)
    expect(shouldReport('/register', false)).toBe(true)
  })

  it('skips the signed-out pass through a protected route before the redirect', () => {
    expect(shouldReport('/', false)).toBe(false)
    expect(shouldReport('/', true)).toBe(true)
  })

  it('never reports the Spotify callback', () => {
    expect(shouldReport('/settings/spotify/callback', true)).toBe(false)
  })
})
