import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import type { ExportResult, SpotifyStatus } from '../../api/types'
import { ExportSpotifyDialog } from './ExportSpotifyDialog'

const useSpotifyStatus = vi.fn()
const useExportPlaylist = vi.fn()

vi.mock('../../api/queries', () => ({
  useSpotifyStatus: () => useSpotifyStatus(),
  useExportPlaylist: () => useExportPlaylist(),
}))

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
})
