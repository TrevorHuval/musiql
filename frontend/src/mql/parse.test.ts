import { describe, expect, it } from 'vitest'
import { parseMql } from './parse'

describe('parseMql', () => {
  it('parses entity, scope, filters, sort, and limit', () => {
    const result = parseMql('tracks from library where genre = "grunge" order by year desc limit 25')
    expect(result.ok).toBe(true)
    if (!result.ok) return
    expect(result.state.entity).toBe('tracks')
    expect(result.state.fromLibrary).toBe(true)
    expect(result.state.root.children).toHaveLength(1)
    expect(result.state.sort).toEqual([
      expect.objectContaining({ field: 'year', direction: 'desc' }),
    ])
    expect(result.state.limit).toBe(25)
  })

  it('decodes string literals without surrounding quotes', () => {
    const result = parseMql('tracks where artist = "Pearl Jam"')
    expect(result.ok).toBe(true)
    if (!result.ok) return
    const condition = result.state.root.children[0]
    expect(condition.kind).toBe('condition')
    if (condition.kind === 'condition') expect(condition.value).toBe('Pearl Jam')
  })

  it('groups an or beneath an and by precedence', () => {
    const result = parseMql('tracks where genre = "punk" or genre = "hardcore" and year >= 1980')
    expect(result.ok).toBe(true)
    if (!result.ok) return
    expect(result.state.root.combinator).toBe('or')
    expect(result.state.root.children).toHaveLength(2)
    const second = result.state.root.children[1]
    expect(second.kind).toBe('group')
    if (second.kind === 'group') expect(second.combinator).toBe('and')
  })

  it('rejects queries that use not', () => {
    const result = parseMql('tracks where not artist contains "tribute"')
    expect(result.ok).toBe(false)
    if (!result.ok) expect(result.reason).toMatch(/not/i)
  })

  it('rejects unknown entities', () => {
    const result = parseMql('songs where genre = "grunge"')
    expect(result.ok).toBe(false)
  })

  it('rejects trailing garbage', () => {
    const result = parseMql('tracks where year >= 1990 wobble')
    expect(result.ok).toBe(false)
  })
})
