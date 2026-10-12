import { useLayoutEffect, type RefObject } from 'react'

export function useNavPill(list: RefObject<HTMLElement | null>, key: string, mode: string) {
  useLayoutEffect(() => {
    const el = list.current
    if (!el) return
    const place = (animate: boolean) => {
      const pill = el.querySelector<HTMLElement>('.nav-pill')
      const active = el.querySelector<HTMLElement>('.nav-link.is-active')
      if (!pill) return
      if (!active) {
        el.dataset.active = 'false'
        return
      }
      el.dataset.active = 'true'
      if (!animate) pill.style.transition = 'none'
      pill.style.width = `${active.offsetWidth}px`
      pill.style.height = `${active.offsetHeight}px`
      pill.style.transform = `translate(${active.offsetLeft}px, ${active.offsetTop}px)`
      if (!animate) {
        void pill.offsetWidth
        pill.style.transition = ''
      }
    }
    const ready = el.dataset.ready === 'true'
    place(ready)
    if (!ready) {
      void el.offsetWidth
      el.dataset.ready = 'true'
    }
    if (typeof ResizeObserver === 'undefined') return
    let first = true
    const observer = new ResizeObserver(() => {
      if (first) first = false
      else place(false)
    })
    observer.observe(el)
    return () => observer.disconnect()
  }, [list, key, mode])
}
