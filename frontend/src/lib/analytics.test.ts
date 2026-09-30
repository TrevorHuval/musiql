import { reportedPath } from './analytics'

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
