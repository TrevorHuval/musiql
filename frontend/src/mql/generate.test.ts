import { describe, expect, it } from 'vitest'
import { generateMql } from './generate'
import { parseMql } from './parse'
import {
  emptyState,
  newCondition,
  newGroup,
  newSortKey,
  type BuilderState,
  type FieldTypeResolver,
} from './types'

const TRACK_TYPES: Record<string, 'string' | 'number' | 'genre'> = {
  artist: 'string',
  album: 'string',
  title: 'string',
  genre: 'genre',
  year: 'number',
  decade: 'number',
  length: 'number',
  votes: 'number',
}

const resolve: FieldTypeResolver = (field) => TRACK_TYPES[field]

function condition(field: string, op: Parameters<typeof newCondition>[1], patch: Partial<ReturnType<typeof newCondition>>) {
  return { ...newCondition(field, op), ...patch }
}

function stateWith(children: BuilderState['root']['children'], extra: Partial<BuilderState> = {}): BuilderState {
  const base = emptyState('tracks')
  base.root.children = children
  return { ...base, ...extra }
}

describe('generateMql', () => {
  it('emits the entity alone when there are no filters', () => {
    expect(generateMql(emptyState('albums'), resolve)).toBe('albums')
  })

  it('quotes string and genre values but not numbers', () => {
    const state = stateWith([
      condition('genre', '=', { value: 'grunge' }),
      condition('year', '>=', { value: '1990' }),
    ])
    expect(generateMql(state, resolve)).toBe('tracks where genre = "grunge" and year >= 1990')
  })

  it('builds the canonical grunge query', () => {
    const state = stateWith([
      condition('genre', '=', { value: 'grunge' }),
      condition('year', 'between', { value: '1990', valueHigh: '2004' }),
      condition('artist', '!=', { value: 'Nirvana' }),
    ])
    expect(generateMql(state, resolve)).toBe(
      'tracks where genre = "grunge" and year between 1990 and 2004 and artist != "Nirvana"',
    )
  })

  it('renders in-lists with the field type applied per value', () => {
    const state = stateWith([
      condition('genre', 'in', { values: ['grunge', 'alternative rock'] }),
    ])
    expect(generateMql(state, resolve)).toBe('tracks where genre in ("grunge", "alternative rock")')
  })

  it('parenthesizes a nested or-group inside an and', () => {
    const orGroup = newGroup('or')
    orGroup.children = [
      condition('genre', '=', { value: 'punk' }),
      condition('genre', '=', { value: 'hardcore' }),
    ]
    const state = stateWith([orGroup, condition('year', '>=', { value: '1980' })])
    expect(generateMql(state, resolve)).toBe(
      'tracks where (genre = "punk" or genre = "hardcore") and year >= 1980',
    )
  })

  it('escapes quotes inside string literals', () => {
    const state = stateWith([condition('title', 'contains', { value: 'say "yes"' })])
    expect(generateMql(state, resolve)).toBe('tracks where title contains "say \\"yes\\""')
  })

  it('skips incomplete conditions so partial rows stay valid', () => {
    const state = stateWith([
      condition('genre', '=', { value: 'grunge' }),
      condition('year', 'between', { value: '1990', valueHigh: '' }),
    ])
    expect(generateMql(state, resolve)).toBe('tracks where genre = "grunge"')
  })

  it('appends order-by and limit clauses', () => {
    const state = stateWith([condition('genre', '=', { value: 'shoegaze' })], {
      sort: [newSortKey('votes', 'desc'), newSortKey('year', 'asc')],
      limit: 50,
    })
    expect(generateMql(state, resolve)).toBe(
      'tracks where genre = "shoegaze" order by votes desc, year limit 50',
    )
  })

  it('emits from-library scope before where', () => {
    const state = stateWith([condition('genre', '=', { value: 'grunge' })], { fromLibrary: true })
    expect(generateMql(state, resolve)).toBe('tracks from library where genre = "grunge"')
  })

  it('round-trips the canonical query through parse and back', () => {
    const mql = 'tracks where genre = "grunge" and year between 1990 and 2004 and artist != "Nirvana"'
    const parsed = parseMql(mql)
    expect(parsed.ok).toBe(true)
    if (parsed.ok) expect(generateMql(parsed.state, resolve)).toBe(mql)
  })
})
