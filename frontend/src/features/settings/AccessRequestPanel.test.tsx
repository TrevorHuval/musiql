import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import type { SpotifyAccessRequestStatus } from '../../api/types'
import { AccessRequestPanel } from './AccessRequestPanel'

const useSpotifyAccessRequest = vi.fn()
const mutate = vi.fn()

vi.mock('../../api/queries', () => ({
  useSpotifyAccessRequest: () => useSpotifyAccessRequest(),
  useRequestSpotifyAccess: () => ({ mutate, isPending: false, isError: false, error: null }),
}))

function status(overrides: Partial<SpotifyAccessRequestStatus> = {}) {
  return {
    isPending: false,
    isError: false,
    data: {
      enabled: true,
      requestedAt: null,
      spotifyEmail: null,
      canRequestAgainAt: null,
      ...overrides,
    },
  }
}

describe('AccessRequestPanel', () => {
  beforeEach(() => {
    mutate.mockReset()
  })

  it('explains invite-only access without a button when requests are off', () => {
    useSpotifyAccessRequest.mockReturnValue(status({ enabled: false }))
    render(<AccessRequestPanel />)

    expect(screen.getByText('Spotify linking is invite-only')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Request access' })).not.toBeInTheDocument()
  })

  it('sends the trimmed email and note', () => {
    useSpotifyAccessRequest.mockReturnValue(status())
    render(<AccessRequestPanel />)

    fireEvent.click(screen.getByRole('button', { name: 'Request access' }))
    fireEvent.change(screen.getByLabelText('Spotify account email'), {
      target: { value: '  me@example.com ' },
    })
    fireEvent.change(screen.getByLabelText('Note'), { target: { value: ' hi ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send request' }))

    expect(mutate).toHaveBeenCalledWith(
      { spotifyEmail: 'me@example.com', note: 'hi' },
      expect.anything(),
    )
  })

  it('shows a pending request instead of the form during the cooldown', () => {
    useSpotifyAccessRequest.mockReturnValue(
      status({
        requestedAt: new Date().toISOString(),
        spotifyEmail: 'me@example.com',
        canRequestAgainAt: new Date(Date.now() + 3_600_000).toISOString(),
      }),
    )
    render(<AccessRequestPanel />)

    expect(screen.getByText('Access requested')).toBeInTheDocument()
    expect(screen.getByText('me@example.com')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Ask again' })).not.toBeInTheDocument()
  })
})
