export type TokenClass = 'entity' | 'kw' | 'str' | 'num' | 'op' | 'field' | 'plain'

export interface HighlightToken {
  text: string
  cls: TokenClass
}

const ENTITIES = new Set(['tracks', 'albums', 'artists'])
const KEYWORDS = new Set([
  'from',
  'library',
  'where',
  'and',
  'or',
  'not',
  'in',
  'between',
  'contains',
  'order',
  'by',
  'limit',
  'asc',
  'desc',
])

export function highlightTokens(text: string): HighlightToken[] {
  const tokens: HighlightToken[] = []
  let i = 0
  let seenEntity = false

  const push = (value: string, cls: TokenClass) => {
    const last = tokens[tokens.length - 1]
    if (last && last.cls === cls) last.text += value
    else tokens.push({ text: value, cls })
  }

  while (i < text.length) {
    const ch = text[i]

    if (/\s/.test(ch)) {
      push(ch, 'plain')
      i += 1
      continue
    }

    if (ch === '"') {
      let value = ch
      i += 1
      while (i < text.length) {
        value += text[i]
        if (text[i] === '\\' && i + 1 < text.length) {
          value += text[i + 1]
          i += 2
          continue
        }
        if (text[i] === '"') {
          i += 1
          break
        }
        i += 1
      }
      push(value, 'str')
      continue
    }

    if (/[0-9]/.test(ch)) {
      let value = ''
      while (i < text.length && /[0-9]/.test(text[i])) {
        value += text[i]
        i += 1
      }
      push(value, 'num')
      continue
    }

    if (/[A-Za-z_]/.test(ch)) {
      let value = ''
      while (i < text.length && /[A-Za-z0-9_]/.test(text[i])) {
        value += text[i]
        i += 1
      }
      const lower = value.toLowerCase()
      if (!seenEntity && ENTITIES.has(lower)) {
        seenEntity = true
        push(value, 'entity')
      } else if (KEYWORDS.has(lower)) {
        push(value, 'kw')
      } else {
        push(value, 'field')
      }
      continue
    }

    push(ch, 'op')
    i += 1
  }

  return tokens
}
