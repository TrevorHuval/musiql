import { useEffect, useRef, type RefObject } from 'react'

const CLOSE_FALLBACK_MS = 220

function restoreFocus(openerRef: RefObject<HTMLElement | null>) {
  const opener = openerRef.current
  openerRef.current = null
  if (opener?.isConnected) opener.focus({ preventScroll: true })
}

export function useDialogMotion(
  dialogRef: RefObject<HTMLDialogElement | null>,
  open: boolean,
  onClose: () => void,
) {
  const onCloseRef = useRef(onClose)
  const openRef = useRef(open)
  const openerRef = useRef<HTMLElement | null>(null)
  useEffect(() => {
    onCloseRef.current = onClose
    openRef.current = open
  })

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return
    const handleClose = () => {
      if (openRef.current) onCloseRef.current()
    }
    const handleCancel = (event: Event) => {
      event.preventDefault()
      onCloseRef.current()
    }
    dialog.addEventListener('close', handleClose)
    dialog.addEventListener('cancel', handleCancel)
    return () => {
      dialog.removeEventListener('close', handleClose)
      dialog.removeEventListener('cancel', handleCancel)
      if (dialog.open) restoreFocus(openerRef)
    }
  }, [dialogRef])

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return

    if (open) {
      if (!dialog.open) {
        openerRef.current =
          document.activeElement instanceof HTMLElement ? document.activeElement : null
        dialog.classList.remove('is-open')
        dialog.showModal()
      }
      dialog.classList.remove('is-closing')
      void dialog.offsetWidth
      dialog.classList.add('is-open')
      return
    }

    if (!dialog.open) return

    const finish = () => {
      dialog.classList.remove('is-open', 'is-closing')
      if (dialog.open) dialog.close()
      restoreFocus(openerRef)
    }

    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      finish()
      return
    }

    const onEnd = (event: TransitionEvent) => {
      if (event.target === dialog && event.propertyName === 'opacity' && event.pseudoElement === '') finish()
    }
    dialog.classList.remove('is-open')
    dialog.classList.add('is-closing')
    dialog.addEventListener('transitionend', onEnd)
    const timer = window.setTimeout(finish, CLOSE_FALLBACK_MS)
    return () => {
      window.clearTimeout(timer)
      dialog.removeEventListener('transitionend', onEnd)
    }
  }, [dialogRef, open])
}
