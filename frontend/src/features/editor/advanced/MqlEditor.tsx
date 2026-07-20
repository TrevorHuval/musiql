import { useLayoutEffect, useRef, type ReactNode } from 'react'
import type { MqlError } from '../../../api/types'
import { highlightTokens } from './highlight'
import styles from './advanced.module.css'

interface MqlEditorProps {
  value: string
  onChange: (value: string) => void
  errors: MqlError[]
}

export function MqlEditor({ value, onChange, errors }: MqlEditorProps) {
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const highlightRef = useRef<HTMLPreElement>(null)
  const squiggleRef = useRef<HTMLPreElement>(null)

  useLayoutEffect(() => {
    syncScroll()
  }, [value])

  function syncScroll() {
    const textarea = textareaRef.current
    if (!textarea) return
    for (const layer of [highlightRef.current, squiggleRef.current]) {
      if (layer) {
        layer.scrollTop = textarea.scrollTop
        layer.scrollLeft = textarea.scrollLeft
      }
    }
  }

  return (
    <div className={styles.editor}>
      <pre ref={highlightRef} className={styles.highlight} aria-hidden="true">
        {highlightTokens(value).map((token, index) => (
          <span key={index} className={styles[token.cls]}>
            {token.text}
          </span>
        ))}
        {'\n'}
      </pre>
      <pre ref={squiggleRef} className={styles.squiggleLayer} aria-hidden="true">
        {renderSquiggles(value, errors)}
        {'\n'}
      </pre>
      <textarea
        ref={textareaRef}
        className={styles.textarea}
        value={value}
        spellCheck={false}
        autoCapitalize="off"
        autoCorrect="off"
        aria-label="MQL query"
        onChange={(event) => onChange(event.target.value)}
        onScroll={syncScroll}
      />
    </div>
  )
}

function renderSquiggles(text: string, errors: MqlError[]): ReactNode[] {
  const spans = errors
    .map((error) => ({
      start: Math.max(0, Math.min(error.start, text.length)),
      end: Math.max(0, Math.min(error.start + Math.max(error.length, 1), text.length || 1)),
    }))
    .filter((span) => span.end > span.start)
    .sort((a, b) => a.start - b.start)

  const nodes: ReactNode[] = []
  let cursor = 0
  spans.forEach((span, index) => {
    if (span.start < cursor) return
    if (span.start > cursor) nodes.push(text.slice(cursor, span.start))
    const marked = text.slice(span.start, span.end) || ' '
    nodes.push(
      <span key={index} className={styles.squiggle}>
        {marked}
      </span>,
    )
    cursor = span.end
  })
  if (cursor < text.length) nodes.push(text.slice(cursor))
  return nodes
}
