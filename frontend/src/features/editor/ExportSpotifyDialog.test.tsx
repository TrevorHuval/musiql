import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import type { ExportResult, PlaylistSpotifyLink, SpotifyStatus } from '../../api/types'
import { ExportSpotifyDialog } from './ExportSpotifyDialog'

const useSpotifyStatus = vi.fn()
const useExportPlaylist = vi.fn()
const useSpotifyLink = vi.fn()
const setKeepLive = vi.fn()

vi.mock('../../api/queries', () => ({
  useSpotifyStatus: () => useSpotifyStatus(),
  useExportPlaylist: () => useExportPlaylist(),
  useSpotifyLink: () => useSpotifyLink(),
  useSetKeepLive: () => ({ mutate: setKeepLive, isPending: false }),
}))

function link(overrides: Partial<PlaylistSpotifyLink> = {}): { data: PlaylistSpotifyLink } {
  return {
    data: {
      exported: true,
      spotifyUrl: 'https://open.spotify.com/playlist/pl1',
      trackCount: 18,
      lastExportedAt: new Date(Date.now() - 3 * 3_600_000).toISOString(),
      keepLive: false,
      nextRefreshAt: null,
      lastRefreshError: null,
      ...overrides,
    },
  }
}

function renderDialog() {
  return render(
    <MemoryRouter>
      <ExportSpotifyDialog playlistId="p1" playlistName="Grunge" open onClose={() => {}} />
    </MemoryRouter>,
  )
}

function status(connected: boolean): { data: SpotifyStatus; isPending: false; isError: false } {
  return {
    data: {
      connected,
      displayName: 'Fake User',
      spotifyUserId: 'u',
      connectedAt: null,
      librarySyncedAt: null,
      librarySavedCount: 0,
      libraryMatchedCount: 0,
    },
    isPending: false,
    isError: false,
  }
}

describe('ExportSpotifyDialog', () => {
  it('prompts to connect when Spotify is not linked', () => {
    useSpotifyLink.mockReturnValue({ data: undefined })
    useSpotifyStatus.mockReturnValue(status(false))
    useExportPlaylist.mockReturnValue({ mutate: vi.fn(), isPending: false, isError: false })

    renderDialog()

    expect(screen.getByText('Connect Spotify first')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Go to settings' })).toBeInTheDocument()
  })

  it('shows the matched count and an unmatched-track report after export', () => {
    const result: ExportResult = {
      playlistName: 'Grunge',
      spotifyPlaylistId: 'pl1',
      spotifyUrl: 'https://open.spotify.com/playlist/pl1',
      trackCount: 2,
      matchedCount: 2,
      totalCount: 3,
      unmatched: [{ title: 'Obscure B-Side', artist: 'Mudhoney', year: 1992, confidence: 0.41 }],
      exportedAt: new Date().toISOString(),
    }
    useSpotifyLink.mockReturnValue(link())
    useSpotifyStatus.mockReturnValue(status(true))
    useExportPlaylist.mockReturnValue({ mutate: vi.fn(), isPending: false, isError: false, data: result })

    renderDialog()

    expect(screen.getByText('tracks added to Spotify')).toBeInTheDocument()
    expect(screen.getByText('Obscure B-Side')).toBeInTheDocument()
    expect(screen.getByText('41%')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Open in Spotify/ })).toHaveAttribute(
      'href',
      'https://open.spotify.com/playlist/pl1',
    )
  })

  it('offers keep live once exported and shows when it refreshes next', () => {
    useSpotifyStatus.mockReturnValue(status(true))
    useExportPlaylist.mockReturnValue({ mutate: vi.fn(), isPending: false, isError: false })
    useSpotifyLink.mockReturnValue(link())

    const { rerender } = renderDialog()
    expect(screen.getByText(/Last pushed 3h ago with 18 tracks/)).toBeInTheDocument()

    fireEvent.click(screen.getByRole('switch', { name: 'Keep live on Spotify' }))
    expect(setKeepLive).toHaveBeenCalledWith(true)

    useSpotifyLink.mockReturnValue(
      link({ keepLive: true, nextRefreshAt: new Date(Date.now() + 21 * 3_600_000).toISOString() }),
    )
    rerender(
      <MemoryRouter>
        <ExportSpotifyDialog playlistId="p1" playlistName="Grunge" open onClose={() => {}} />
      </MemoryRouter>,
    )
    expect(screen.getByText('Refreshes daily · next in 21h')).toBeInTheDocument()
  })

  it('surfaces the last refresh failure in place of the schedule', () => {
    useSpotifyStatus.mockReturnValue(status(true))
    useExportPlaylist.mockReturnValue({ mutate: vi.fn(), isPending: false, isError: false })
    useSpotifyLink.mockReturnValue(
      link({ keepLive: true, lastRefreshError: 'Spotify is no longer connected. Reconnect it in settings.' }),
    )

    renderDialog()
    expect(screen.getByText(/no longer connected/)).toBeInTheDocument()
  })

  it('does not offer keep live before the first export', () => {
    useSpotifyStatus.mockReturnValue(status(true))
    useExportPlaylist.mockReturnValue({ mutate: vi.fn(), isPending: false, isError: false })
    useSpotifyLink.mockReturnValue(link({ exported: false, lastExportedAt: null }))

    renderDialog()
    expect(screen.queryByRole('switch')).not.toBeInTheDocument()
  })
})
