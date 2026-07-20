import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { EditorToolbar } from './EditorToolbar'

function renderToolbar(props: Partial<Parameters<typeof EditorToolbar>[0]> = {}) {
  return render(
    <MemoryRouter>
      <EditorToolbar
        title="Grunge"
        isNew={false}
        mode="builder"
        onModeChange={() => {}}
        dirty={false}
        saving={false}
        onSave={() => {}}
        {...props}
      />
    </MemoryRouter>,
  )
}

describe('EditorToolbar', () => {
  it('invokes the M3U download handler when the button is clicked', () => {
    const onDownloadM3u = vi.fn()
    renderToolbar({ onDownloadM3u })

    fireEvent.click(screen.getByRole('button', { name: /M3U/ }))
    expect(onDownloadM3u).toHaveBeenCalledOnce()
  })

  it('omits the M3U button for a new, unsaved playlist', () => {
    renderToolbar({ isNew: true, onDownloadM3u: undefined })
    expect(screen.queryByRole('button', { name: /M3U/ })).toBeNull()
  })
})
