import type { Operator } from '../api/types'
import {
  newCondition,
  newGroup,
  newSortKey,
  type BuilderNode,
  type BuilderState,
  type Combinator,
  type Entity,
  type Group,
  type SortKey,
} from './types'

export type ParseResult =
  | { ok: true; state: BuilderState }
  | { ok: false; reason: string }

const ENTITIES: Entity[] = ['tracks', 'albums', 'artists']
const COMPARE_OPS: Operator[] = ['=', '!=', '<', '<=', '>', '>=']

class NotRepresentable extends Error {}

type Token =
  | { type: 'ident'; value: string }
  | { type: 'number'; value: string }
  | { type: 'string'; value: string }
  | { type: 'op'; value: Operator }
  | { type: 'lparen' }
  | { type: 'rparen' }
  | { type: 'comma' }

function tokenize(input: string): Token[] {
  const tokens: Token[] = []
  let i = 0
  const fail = (message: string): never => {
    throw new NotRepresentable(message)
  }

  while (i < input.length) {
    const ch = input[i]
    if (/\s/.test(ch)) {
      i += 1
      continue
    }
    if (ch === '(') {
      tokens.push({ type: 'lparen' })
      i += 1
      continue
    }
    if (ch === ')') {
      tokens.push({ type: 'rparen' })
      i += 1
      continue
    }
    if (ch === ',') {
      tokens.push({ type: 'comma' })
      i += 1
      continue
    }
    if (ch === '=') {
      tokens.push({ type: 'op', value: '=' })
      i += 1
      continue
    }
    if (ch === '!' && input[i + 1] === '=') {
      tokens.push({ type: 'op', value: '!=' })
      i += 2
      continue
    }
    if (ch === '<' || ch === '>') {
      if (input[i + 1] === '=') {
        tokens.push({ type: 'op', value: (ch + '=') as Operator })
        i += 2
      } else {
        tokens.push({ type: 'op', value: ch as Operator })
        i += 1
      }
      continue
    }
    if (ch === '"') {
      i += 1
      let value = ''
      while (i < input.length && input[i] !== '"') {
        if (input[i] === '\\') {
          const next = input[i + 1]
          const map: Record<string, string> = { '"': '"', '\\': '\\', n: '\n', t: '\t', r: '\r' }
          value += map[next] ?? next
          i += 2
        } else {
          value += input[i]
          i += 1
        }
      }
      if (input[i] !== '"') fail('Unterminated string literal.')
      i += 1
      tokens.push({ type: 'string', value })
      continue
    }
    if (/[0-9]/.test(ch)) {
      let value = ''
      while (i < input.length && /[0-9]/.test(input[i])) {
        value += input[i]
        i += 1
      }
      tokens.push({ type: 'number', value })
      continue
    }
    if (/[A-Za-z_]/.test(ch)) {
      let value = ''
      while (i < input.length && /[A-Za-z0-9_]/.test(input[i])) {
        value += input[i]
        i += 1
      }
      tokens.push({ type: 'ident', value: value.toLowerCase() })
      continue
    }
    fail(`Unexpected character ${JSON.stringify(ch)}.`)
  }
  return tokens
}

class Cursor {
  private readonly tokens: Token[]
  private pos = 0

  constructor(tokens: Token[]) {
    this.tokens = tokens
  }

  peek(): Token | undefined {
    return this.tokens[this.pos]
  }

  next(): Token | undefined {
    return this.tokens[this.pos++]
  }

  atEnd(): boolean {
    return this.pos >= this.tokens.length
  }

  isKeyword(word: string): boolean {
    const token = this.peek()
    return token?.type === 'ident' && token.value === word
  }

  eatKeyword(word: string): boolean {
    if (this.isKeyword(word)) {
      this.pos += 1
      return true
    }
    return false
  }
}

function fail(message: string): never {
  throw new NotRepresentable(message)
}

function parseValue(cursor: Cursor): string {
  const token = cursor.next()
  if (token?.type === 'string' || token?.type === 'number') return token.value
  fail('Expected a value.')
}

function parseComparison(cursor: Cursor): BuilderNode {
  const fieldToken = cursor.next()
  if (fieldToken?.type !== 'ident') fail('Expected a field name.')
  const field = fieldToken.value

  const opToken = cursor.next()
  let op: Operator
  if (opToken?.type === 'op') {
    op = opToken.value
  } else if (opToken?.type === 'ident' && opToken.value === 'in') {
    op = 'in'
  } else if (opToken?.type === 'ident' && opToken.value === 'between') {
    op = 'between'
  } else if (opToken?.type === 'ident' && opToken.value === 'contains') {
    op = 'contains'
  } else {
    fail('Expected an operator.')
  }

  const condition = newCondition(field, op)

  if (op === 'in') {
    if (cursor.next()?.type !== 'lparen') fail('Expected "(" after in.')
    condition.values = [parseValue(cursor)]
    while (cursor.peek()?.type === 'comma') {
      cursor.next()
      condition.values.push(parseValue(cursor))
    }
    if (cursor.next()?.type !== 'rparen') fail('Expected ")" to close in list.')
  } else if (op === 'between') {
    condition.value = parseValue(cursor)
    if (!cursor.eatKeyword('and')) fail('Expected "and" in between.')
    condition.valueHigh = parseValue(cursor)
  } else if (op === 'contains' || COMPARE_OPS.includes(op)) {
    condition.value = parseValue(cursor)
  }

  return condition
}

function parsePrimary(cursor: Cursor): BuilderNode {
  if (cursor.isKeyword('not')) fail('The builder can’t show queries that use "not".')
  if (cursor.peek()?.type === 'lparen') {
    cursor.next()
    const inner = parseOr(cursor)
    if (cursor.next()?.type !== 'rparen') fail('Expected ")".')
    return inner
  }
  return parseComparison(cursor)
}

function stopsClause(cursor: Cursor): boolean {
  return (
    cursor.atEnd() ||
    cursor.isKeyword('order') ||
    cursor.isKeyword('limit') ||
    cursor.peek()?.type === 'rparen'
  )
}

function parseAnd(cursor: Cursor): BuilderNode {
  const children: BuilderNode[] = [parsePrimary(cursor)]
  while (cursor.isKeyword('and')) {
    cursor.next()
    children.push(parsePrimary(cursor))
  }
  return children.length === 1 ? children[0] : wrapGroup('and', children)
}

function parseOr(cursor: Cursor): BuilderNode {
  const children: BuilderNode[] = [parseAnd(cursor)]
  while (cursor.isKeyword('or')) {
    cursor.next()
    children.push(parseAnd(cursor))
  }
  return children.length === 1 ? children[0] : wrapGroup('or', children)
}

function wrapGroup(combinator: Combinator, children: BuilderNode[]): Group {
  const group = newGroup(combinator)
  group.children = children
  return group
}

function asRootGroup(node: BuilderNode): Group {
  if (node.kind === 'group') return node
  return wrapGroup('and', [node])
}

function parseSort(cursor: Cursor): SortKey[] {
  const keys: SortKey[] = []
  const readKey = () => {
    const fieldToken = cursor.next()
    if (fieldToken?.type !== 'ident') fail('Expected a sort field.')
    let direction: 'asc' | 'desc' = 'asc'
    if (cursor.eatKeyword('desc')) direction = 'desc'
    else cursor.eatKeyword('asc')
    keys.push(newSortKey(fieldToken.value, direction))
  }
  readKey()
  while (cursor.peek()?.type === 'comma') {
    cursor.next()
    readKey()
  }
  return keys
}

export function parseMql(input: string): ParseResult {
  const trimmed = input.trim()
  if (trimmed === '') {
    return { ok: false, reason: 'The query is empty.' }
  }
  try {
    const cursor = new Cursor(tokenize(trimmed))
    const entityToken = cursor.next()
    if (entityToken?.type !== 'ident' || !ENTITIES.includes(entityToken.value as Entity)) {
      fail('Expected tracks, albums, or artists.')
    }
    const entity = entityToken.value as Entity

    let fromLibrary = false
    if (cursor.eatKeyword('from')) {
      if (!cursor.eatKeyword('library')) fail('Expected "library" after "from".')
      fromLibrary = true
    }

    let root = newGroup('and')
    if (cursor.eatKeyword('where')) {
      const expr = parseOr(cursor)
      if (!stopsClause(cursor)) fail('Unexpected tokens after the where clause.')
      root = asRootGroup(expr)
    }

    let sort: SortKey[] = []
    if (cursor.isKeyword('order')) {
      cursor.next()
      if (!cursor.eatKeyword('by')) fail('Expected "by" after "order".')
      sort = parseSort(cursor)
    }

    let limit: number | null = null
    if (cursor.eatKeyword('limit')) {
      const limitToken = cursor.next()
      if (limitToken?.type !== 'number') fail('Expected a number after "limit".')
      limit = Number(limitToken.value)
    }

    if (!cursor.atEnd()) fail('The query has trailing tokens the builder can’t show.')

    return { ok: true, state: { entity, fromLibrary, root, sort, limit } }
  } catch (error) {
    if (error instanceof NotRepresentable) return { ok: false, reason: error.message }
    return { ok: false, reason: 'This query can’t be shown in the builder.' }
  }
}
