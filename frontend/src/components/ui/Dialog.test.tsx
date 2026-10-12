import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { Dialog } from './Dialog'

function Harness({ onClose }: { onClose: () => void }) {
  const [open, setOpen] = useState(false)
  return (
    <>
      <button onClick={() => setOpen(true)}>Open it</button>
      <Dialog
        open={open}
        onClose={() => {
          onClose()
          setOpen(false)
        }}
        title="Sample"
      >
        <input aria-label="Name" />
      </Dialog>
    </>
  )
}

function openDialog() {
  const onClose = vi.fn()
  render(<Harness onClose={onClose} />)
  const trigger = screen.getByRole('button', { name: 'Open it' })
  trigger.focus()
  fireEvent.click(trigger)
  const dialog = document.querySelector('dialog') as HTMLDialogElement
  return { onClose, trigger, dialog }
}

describe('Dialog', () => {
  it('opens as a modal and closes once on Escape', async () => {
    const { onClose, dialog } = openDialog()
    expect(dialog).toHaveAttribute('open')

    fireEvent.keyDown(dialog, { key: 'Escape' })
    await waitFor(() => expect(dialog).not.toHaveAttribute('open'))
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('closes on a backdrop click but not when the press began inside', async () => {
    const { onClose, dialog } = openDialog()
    const input = screen.getByLabelText('Name')

    fireEvent.pointerDown(input)
    fireEvent.click(dialog)
    expect(onClose).not.toHaveBeenCalled()

    fireEvent.pointerDown(dialog)
    fireEvent.click(dialog)
    expect(onClose).toHaveBeenCalledTimes(1)
    await waitFor(() => expect(dialog).not.toHaveAttribute('open'))
  })

  it('returns focus to the trigger after closing', async () => {
    const { trigger, dialog } = openDialog()
    screen.getByLabelText('Name').focus()

    fireEvent.keyDown(dialog, { key: 'Escape' })
    await waitFor(() => expect(trigger).toHaveFocus())
  })
})
