import { useEffect, useLayoutEffect, useRef } from 'react'
import { Icon, type IconName } from './Icon'
import styles from './Segmented.module.css'

export interface SegmentOption<T extends string> {
  value: T
  label: string
  icon?: IconName
}

interface SegmentedProps<T extends string> {
  value: T
  options: SegmentOption<T>[]
  onChange: (value: T) => void
  ariaLabel: string
  size?: 'sm' | 'md'
}

function placePill(pill: HTMLElement, selected: HTMLElement, animate: boolean) {
  const move = () => {
    pill.style.width = `${selected.offsetWidth}px`
    pill.style.transform = `translateX(${selected.offsetLeft}px)`
  }
  if (animate) {
    move()
    return
  }
  pill.style.transition = 'none'
  move()
  void pill.offsetWidth
  pill.style.transition = ''
}

function measure(bar: HTMLElement, animate: boolean) {
  const pill = bar.querySelector<HTMLElement>('.segmented-pill')
  const selected = bar.querySelector<HTMLElement>('[aria-checked="true"]')
  if (pill && selected) placePill(pill, selected, animate)
}

export function Segmented<T extends string>({
  value,
  options,
  onChange,
  ariaLabel,
  size = 'md',
}: SegmentedProps<T>) {
  const bar = useRef<HTMLDivElement>(null)
  const painted = useRef(false)

  useLayoutEffect(() => {
    const el = bar.current
    if (!el) return
    measure(el, painted.current)
    if (!painted.current) {
      painted.current = true
      void el.offsetWidth
      el.dataset.ready = 'true'
    }
  }, [value])

  useEffect(() => {
    const el = bar.current
    if (!el || typeof ResizeObserver === 'undefined') return
    let first = true
    const observer = new ResizeObserver(() => {
      if (first) first = false
      else measure(el, false)
    })
    observer.observe(el)
    return () => observer.disconnect()
  }, [])

  const selectedIndex = Math.max(
    0,
    options.findIndex((option) => option.value === value),
  )

  function move(to: number) {
    const index = (to + options.length) % options.length
    const next = options[index]
    if (!next) return
    onChange(next.value)
    bar.current?.querySelectorAll<HTMLElement>('[role="radio"]')[index]?.focus()
  }

  return (
    <div
      ref={bar}
      role="radiogroup"
      aria-label={ariaLabel}
      className={`segmented ${styles.group}`}
      data-size={size}
    >
      <span className="segmented-pill" aria-hidden="true" />
      {options.map((option, index) => (
        <button
          key={option.value}
          type="button"
          role="radio"
          aria-checked={option.value === value}
          tabIndex={index === selectedIndex ? 0 : -1}
          className={`segmented-option press ${styles.option}`}
          onClick={() => onChange(option.value)}
          onKeyDown={(event) => {
            if (event.altKey || event.metaKey || event.ctrlKey) return
            const keys: Record<string, number> = {
              ArrowRight: index + 1,
              ArrowDown: index + 1,
              ArrowLeft: index - 1,
              ArrowUp: index - 1,
              Home: 0,
              End: options.length - 1,
            }
            const target = keys[event.key]
            if (target !== undefined) {
              event.preventDefault()
              move(target)
            }
          }}
        >
          {option.icon && <Icon name={option.icon} size={15} />}
          {option.label}
        </button>
      ))}
    </div>
  )
}
