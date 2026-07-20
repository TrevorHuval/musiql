import { describe, expect, it } from 'vitest'
import { highlightTokens, type TokenClass } from './highlight'

function classesFor(text: string, target: string): TokenClass[] {
  return highlightTokens(text)
    .filter((token) => token.text === target)
    .map((token) => token.cls)
}

describe('highlightTokens', () => {
  it('marks the leading entity distinctly from later keywords', () => {
    const tokens = highlightTokens('tracks where year >= 1990')
    expect(tokens[0]).toEqual({ text: 'tracks', cls: 'entity' })
    expect(classesFor('tracks where year >= 1990', 'where')).toEqual(['kw'])
  })

  it('classifies fields, strings, numbers, and operators', () => {
    const text = 'tracks where genre = "grunge" and year between 1990 and 2004'
    expect(classesFor(text, 'genre')).toEqual(['field'])
    expect(classesFor(text, '"grunge"')).toEqual(['str'])
    expect(classesFor(text, '1990')).toEqual(['num'])
    expect(classesFor(text, '=')).toEqual(['op'])
    expect(classesFor(text, 'between')).toEqual(['kw'])
  })

  it('keeps escaped quotes inside a single string token', () => {
    const tokens = highlightTokens('tracks where title contains "say \\"hi\\""')
    const strings = tokens.filter((token) => token.cls === 'str')
    expect(strings).toHaveLength(1)
    expect(strings[0].text).toBe('"say \\"hi\\""')
  })

  it('reconstructs the original text exactly', () => {
    const text = 'albums where genre in ("punk", "post-punk")   limit 25'
    expect(highlightTokens(text).map((token) => token.text).join('')).toBe(text)
  })
})
