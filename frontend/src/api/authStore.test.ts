import { describe, expect, it } from 'vitest'
import { getAuth } from './authStore'

describe('authStore', () => {
  it('picks up token changes written by another tab', () => {
    const tokens = { accessToken: 'a', refreshToken: 'r' }
    localStorage.setItem('musiql.auth', JSON.stringify(tokens))
    window.dispatchEvent(new StorageEvent('storage', { key: 'musiql.auth' }))
    expect(getAuth()).toEqual(tokens)
  })
})
