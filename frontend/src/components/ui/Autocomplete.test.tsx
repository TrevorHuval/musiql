import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { Suggestion } from '../../api/types'
import { Autocomplete } from './Autocomplete'

const genres = vi.fn<(q: string, limit: number, offset: number) => Promise<Suggestion[]>>()

vi.mock('../../api/endpoints', () => ({
  catalog: {
    genres: (q: string, limit: number, offset: number) => genres(q, limit, offset),
    artists: vi.fn(),
  },
}))

function page(offset: number, size: number): Suggestion[] {
  return Array.from({ length: size }, (_, i) => ({
    mbid: `00000000-0000-0000-0000-${String(offset + i).padStart(12, '0')}`,
    name: `genre ${offset + i}`,
  }))
}

function renderAutocomplete() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <Autocomplete
        kind="genre"
        value=""
        onChange={() => {}}
        placeholder="Any genre"
        ariaLabel="genre value"
      />
    </QueryClientProvider>,
  )
}

describe('Autocomplete', () => {
  it('loads the next page when the menu is scrolled to its end', async () => {
    genres.mockImplementation(async (_q, limit, offset) => page(offset, offset === 0 ? limit : 5))

    renderAutocomplete()
    fireEvent.click(screen.getByLabelText('Show genre suggestions'))
    const listbox = await screen.findByRole('listbox')
    await screen.findByText('genre 29')

    Object.defineProperty(listbox, 'scrollHeight', { value: 1000, configurable: true })
    Object.defineProperty(listbox, 'clientHeight', { value: 280, configurable: true })
    listbox.scrollTop = 720
    fireEvent.scroll(listbox)

    await screen.findByText('genre 34')
    expect(genres).toHaveBeenLastCalledWith('', 30, 30)
    await waitFor(() => expect(screen.getByText('35 genres')).toBeInTheDocument())
  })

  it('does not ask for more when the first page is short', async () => {
    genres.mockReset()
    genres.mockResolvedValue(page(0, 4))

    renderAutocomplete()
    fireEvent.click(screen.getByLabelText('Show genre suggestions'))
    const listbox = await screen.findByRole('listbox')
    await screen.findByText('genre 3')

    fireEvent.scroll(listbox)
    expect(genres).toHaveBeenCalledTimes(1)
    expect(screen.queryByText(/Loading more/)).not.toBeInTheDocument()
  })
})
